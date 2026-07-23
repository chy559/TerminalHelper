[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$failures = [System.Collections.Generic.List[string]]::new()
$scriptNames = @(
    'build-portable.ps1'
    'verify-package.ps1'
    'verify-portable.ps1'
    'verify-powershell-syntax.ps1'
)

foreach ($scriptName in $scriptNames) {
    $scriptPath = Join-Path $PSScriptRoot $scriptName
    $tokens = $null
    $parseErrors = $null
    [System.Management.Automation.Language.Parser]::ParseFile(
        $scriptPath,
        [ref]$tokens,
        [ref]$parseErrors) | Out-Null

    foreach ($parseError in $parseErrors) {
        $failures.Add(
            "${scriptName}:$($parseError.Extent.StartLineNumber): $($parseError.Message)")
    }
}

if ($failures.Count -ne 0) {
    throw "PowerShell syntax validation failed:`n$($failures -join [Environment]::NewLine)"
}

Write-Host 'PowerShell syntax verified.'
