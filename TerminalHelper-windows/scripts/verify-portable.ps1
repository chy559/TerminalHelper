[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PublishDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

try {
    $resolvedPublishDirectory = if ([System.IO.Path]::IsPathRooted($PublishDirectory)) {
        [System.IO.Path]::GetFullPath($PublishDirectory)
    }
    else {
        [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot $PublishDirectory))
    }

    if (-not (Test-Path -LiteralPath $resolvedPublishDirectory -PathType Container)) {
        throw "发布目录不存在 / Publish directory does not exist: $resolvedPublishDirectory"
    }

    $requiredFiles = @(
        'TerminalHelper.Windows.exe'
        'TerminalHelper.Windows.dll'
        'TerminalHelper.Windows.deps.json'
        'TerminalHelper.Windows.runtimeconfig.json'
        'TerminalHelper.Core.dll'
        'TerminalHelper.Presentation.dll'
        'TerminalHelper.WindowsPlatform.dll'
        'Microsoft.WindowsAppRuntime.Bootstrap.dll'
        'Microsoft.WindowsAppRuntime.dll'
        'Microsoft.UI.Xaml.dll'
        'coreclr.dll'
        'hostfxr.dll'
        'hostpolicy.dll'
    )

    foreach ($requiredFile in $requiredFiles) {
        $requiredPath = Join-Path $resolvedPublishDirectory $requiredFile
        if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
            throw "缺少必需文件 / Missing required file: $requiredFile"
        }
    }

    $debugSymbols = @(
        Get-ChildItem -LiteralPath $resolvedPublishDirectory -Filter '*.pdb' -File -Recurse
    )
    if ($debugSymbols.Count -ne 0) {
        $relativePaths = $debugSymbols |
            ForEach-Object { [System.IO.Path]::GetRelativePath($resolvedPublishDirectory, $_.FullName) }
        throw "便携包包含 PDB / Portable package contains PDB files: $($relativePaths -join ', ')"
    }

    Write-Host "便携包验证通过 / Portable package verified: $resolvedPublishDirectory"
}
catch {
    throw "便携包验证失败 / Portable package verification failed: $($_.Exception.Message)"
}
