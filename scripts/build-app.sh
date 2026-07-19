#!/bin/bash
set -euo pipefail

script_dir=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)
project_root=$(cd -- "$script_dir/.." && pwd -P)
dist_dir="$project_root/dist"
app_bundle="$dist_dir/Terminal Helper.app"

swift build -c release --package-path "$project_root"
bin_path=$(swift build -c release --package-path "$project_root" --show-bin-path)
executable="$bin_path/TerminalHelper"

if [[ ! -x "$executable" ]]; then
    echo "Release executable not found: $executable" >&2
    exit 1
fi

mkdir -p "$dist_dir"
staging_dir=$(mktemp -d "$dist_dir/.terminal-helper.XXXXXX")
trap 'rm -rf -- "$staging_dir"' EXIT

staged_app="$staging_dir/Terminal Helper.app"
contents_dir="$staged_app/Contents"
mkdir -p "$contents_dir/MacOS" "$contents_dir/Resources"
cp "$executable" "$contents_dir/MacOS/TerminalHelper"
cp "$project_root/Resources/Info.plist" "$contents_dir/Info.plist"
cp "$project_root/Resources/TerminalHelper.icns" "$contents_dir/Resources/TerminalHelper.icns"
chmod 755 "$contents_dir/MacOS/TerminalHelper"

codesign --force --deep --sign - "$staged_app"

rm -rf -- "$app_bundle"
mv "$staged_app" "$app_bundle"
printf '%s\n' "$app_bundle"
