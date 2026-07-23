[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$projectPath = Join-Path $projectRoot 'src/TerminalHelper.Windows/TerminalHelper.Windows.csproj'
$artifactDirectory = Join-Path $projectRoot 'artifacts'
$publishDirectory = Join-Path $artifactDirectory 'publish/win-x64'
$archiveName = 'TerminalHelper-windows-win-x64.zip'
$archivePath = Join-Path $artifactDirectory $archiveName
$checksumPath = "$archivePath.sha256"
$verifyScript = Join-Path $PSScriptRoot 'verify-portable.ps1'
$verifyPackageScript = Join-Path $PSScriptRoot 'verify-package.ps1'

if (Test-Path -LiteralPath $archivePath) {
    Remove-Item -LiteralPath $archivePath -Force
}
if (Test-Path -LiteralPath $checksumPath) {
    Remove-Item -LiteralPath $checksumPath -Force
}

if (Test-Path -LiteralPath $publishDirectory) {
    Remove-Item -LiteralPath $publishDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null

& dotnet publish $projectPath `
    -c Release -r win-x64 --self-contained true `
    -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true `
    -p:DebugType=None -p:DebugSymbols=false `
    -o $publishDirectory
if ($LASTEXITCODE -ne 0) {
    throw "发布失败 / dotnet publish failed with exit code $LASTEXITCODE"
}

& $verifyScript -PublishDirectory $publishDirectory

Compress-Archive -Path (Join-Path $publishDirectory '*') -DestinationPath $archivePath -Force

$hash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToUpperInvariant()
$checksumLine = "$hash  $archiveName"
[System.IO.File]::WriteAllText(
    $checksumPath,
    $checksumLine + [Environment]::NewLine,
    [System.Text.UTF8Encoding]::new($false))

& $verifyPackageScript -ArchivePath $archivePath -ChecksumPath $checksumPath

Write-Host "便携包已创建 / Portable package created: $archivePath"
Write-Host "SHA-256: $checksumLine"
