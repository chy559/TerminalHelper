#!/usr/bin/env python3
"""Verify Windows release/security contracts that can be checked off Windows."""

from __future__ import annotations

import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path


PROJECT_ROOT = Path(__file__).resolve().parent.parent
REPOSITORY_ROOT = PROJECT_ROOT.parent
FAILURES: list[str] = []


def require(condition: bool, message: str) -> None:
    if not condition:
        FAILURES.append(message)


def read(relative_path: str) -> str:
    return (REPOSITORY_ROOT / relative_path).read_text(encoding="utf-8")


def find_named_element(root: ET.Element, name: str) -> ET.Element | None:
    xaml_name = "{http://schemas.microsoft.com/winfx/2006/xaml}Name"
    return next((element for element in root.iter() if element.get(xaml_name) == name), None)


manifest_root = ET.fromstring(read("TerminalHelper-windows/src/TerminalHelper.Windows/app.manifest"))
manifest_values = {
    element.tag.rsplit("}", 1)[-1]: (element.text or "").strip()
    for element in manifest_root.iter()
}
require(manifest_values.get("dpiAware") == "true/pm", "legacy dpiAware must be true/pm")
require(
    manifest_values.get("dpiAwareness") == "PerMonitorV2",
    "modern dpiAwareness must remain PerMonitorV2",
)

xaml_root = ET.fromstring(read("TerminalHelper-windows/src/TerminalHelper.Windows/MainWindow.xaml"))
for heading_name in ("EmptyHeadingText", "SelectionCountText"):
    heading = find_named_element(xaml_root, heading_name)
    require(heading is not None, f"missing named heading {heading_name}")
    if heading is not None:
        require(
            heading.get("AutomationProperties.HeadingLevel") == "Level1",
            f"{heading_name} must expose AutomationProperties.HeadingLevel=Level1",
        )

for status_name, live_setting in (
    ("EmptyStatusText", "Polite"),
    ("EmptyErrorStatusText", "Assertive"),
    ("SelectedStatusText", "Polite"),
    ("SelectedErrorStatusText", "Assertive"),
):
    status = find_named_element(xaml_root, status_name)
    require(status is not None, f"missing status element {status_name}")
    if status is not None:
        require(
            status.get("AutomationProperties.LiveSetting") == live_setting,
            f"{status_name} must expose AutomationProperties.LiveSetting={live_setting}",
        )

window_code = read("TerminalHelper-windows/src/TerminalHelper.Windows/MainWindow.xaml.cs")
require(
    "using TerminalHelper.Windows.Input;" in window_code,
    "WinUI drop adapter must import the platform-neutral latest-input gate namespace",
)
drop_start = window_code.find("private async void DropRoot_Drop")
drop_end = window_code.find("private void SetDropTargeted", drop_start)
drop_code = window_code[drop_start:drop_end]
begin_index = drop_code.find("BeginInput()")
contains_index = drop_code.find("Contains(StandardDataFormats.StorageItems)")
read_index = drop_code.find("GetStorageItemsAsync()")
apply_index = drop_code.find("TryApply(")
require(begin_index >= 0, "drop adapter must begin a latest-input generation")
require(read_index >= 0, "drop adapter must read storage items")
require(apply_index >= 0, "drop adapter must gate result application")
require(
    0 <= contains_index < begin_index < read_index < apply_index,
    "storage format must be accepted before generation, extraction, and gated apply",
)
require("StorageFolder" in window_code, "drop adapter must accept path-bearing StorageFolder items")
require("StorageFile" in window_code, "drop adapter must accept path-bearing StorageFile items")
require(
    "normalText.Visibility = Visibility.Collapsed;\n"
    "            errorText.Visibility = Visibility.Visible;\n"
    "            errorText.Text = ViewModel.StatusText;" in window_code,
    "error rendering must show then update only the assertive live region",
)
require(
    "errorText.Visibility = Visibility.Collapsed;\n"
    "            normalText.Visibility = Visibility.Visible;\n"
    "            normalText.Text = ViewModel.StatusText;" in window_code,
    "normal rendering must show then update only the polite live region",
)

process_runner = read(
    "TerminalHelper-windows/src/TerminalHelper.WindowsPlatform/Launching/SystemProcessRunner.cs"
)
require(
    re.search(r"using\s+var\s+\w+\s*=\s*Process\.Start\(", process_runner) is not None,
    "SystemProcessRunner must dispose the Process wrapper returned by Process.Start",
)
require("WaitForExit" not in process_runner, "SystemProcessRunner must not wait for child exit")

ignore_lines = {
    line.strip() for line in read("TerminalHelper-windows/.gitignore").splitlines()
}
require("TestResults/" in ignore_lines, "Windows .gitignore must ignore TestResults/")

workflow = read(".github/workflows/windows.yml")
icon_command = "python scripts/verify-icon-assets.py"
require(icon_command in workflow, "Windows CI must run the icon validator")
require(
    workflow.find(icon_command) < workflow.find("- name: Build"),
    "Windows CI must validate icon assets before build",
)
require(
    "TerminalHelper-mac/Resources/AppIcon/TerminalHelper-1024.png" in workflow,
    "Windows CI path filters must include the icon source",
)
require(
    "python scripts/verify-static-contracts.py" in workflow,
    "Windows CI must run the static contract verifier",
)
require(
    "pwsh -File scripts/verify-powershell-syntax.ps1" in workflow,
    "Windows CI must parse PowerShell scripts before build/package",
)

build_script = read("TerminalHelper-windows/scripts/build-portable.ps1")
publish_index = build_script.find("& dotnet publish")
archive_cleanup_index = build_script.find("Remove-Item -LiteralPath $archivePath")
checksum_cleanup_index = build_script.find("Remove-Item -LiteralPath $checksumPath")
checksum_write_index = build_script.find("[System.IO.File]::WriteAllText")
package_verify_index = build_script.find("& $verifyPackageScript")
require(
    0 <= archive_cleanup_index < publish_index,
    "build must remove the prior archive before publish",
)
require(
    0 <= checksum_cleanup_index < publish_index,
    "build must remove the prior checksum before publish",
)
require(
    0 <= checksum_write_index < package_verify_index,
    "build must verify the package after writing its checksum",
)

package_verifier_path = PROJECT_ROOT / "scripts/verify-package.ps1"
require(package_verifier_path.is_file(), "missing scripts/verify-package.ps1")
if package_verifier_path.is_file():
    package_verifier = package_verifier_path.read_text(encoding="utf-8")
    require(
        "Join-Path $PSScriptRoot $ArchivePath" not in package_verifier
        and "Join-Path $PSScriptRoot $ChecksumPath" not in package_verifier,
        "package verifier path arguments must resolve from the caller's working directory",
    )
    for required_fragment, message in (
        ("$expectedChecksumPath", "package verifier must enforce the exact sidecar name"),
        ("Get-FileHash", "package verifier must recompute SHA-256"),
        ("Expand-Archive", "package verifier must expand the actual ZIP"),
        ("[guid]::NewGuid()", "package verifier must use a unique extraction directory"),
        ("& $verifyPortableScript", "package verifier must reuse portable-content checks"),
        ("finally", "package verifier must clean up in finally"),
        ("Remove-Item", "package verifier must remove its extraction directory"),
    ):
        require(required_fragment in package_verifier, message)
    require(
        "(?<hash>[0-9A-Fa-f]{64})  " in package_verifier,
        "package verifier must require exactly 64 hex characters and two spaces",
    )
    finally_index = package_verifier.find("finally {")
    cleanup_index = package_verifier.find("Remove-Item", finally_index)
    require(
        0 <= finally_index < cleanup_index,
        "package verifier cleanup must occur inside finally",
    )

require(
    (PROJECT_ROOT / "scripts/verify-powershell-syntax.ps1").is_file(),
    "missing scripts/verify-powershell-syntax.ps1",
)

if FAILURES:
    for failure in FAILURES:
        print(f"ERROR: {failure}", file=sys.stderr)
    sys.exit(1)

print("Windows static contracts verified.")
