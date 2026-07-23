[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ArchivePath,

    [Parameter(Mandatory = $true)]
    [string]$ChecksumPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$extractionDirectory = $null

try {
    $resolvedArchivePath = if ([System.IO.Path]::IsPathRooted($ArchivePath)) {
        [System.IO.Path]::GetFullPath($ArchivePath)
    }
    else {
        [System.IO.Path]::GetFullPath($ArchivePath)
    }
    $resolvedChecksumPath = if ([System.IO.Path]::IsPathRooted($ChecksumPath)) {
        [System.IO.Path]::GetFullPath($ChecksumPath)
    }
    else {
        [System.IO.Path]::GetFullPath($ChecksumPath)
    }

    if (-not (Test-Path -LiteralPath $resolvedArchivePath -PathType Leaf)) {
        throw "ZIP 不存在 / ZIP does not exist: $resolvedArchivePath"
    }
    if (-not (Test-Path -LiteralPath $resolvedChecksumPath -PathType Leaf)) {
        throw "校验文件不存在 / Checksum file does not exist: $resolvedChecksumPath"
    }

    $expectedChecksumPath = "$resolvedArchivePath.sha256"
    if (-not [string]::Equals(
            $resolvedChecksumPath,
            $expectedChecksumPath,
            [System.StringComparison]::Ordinal)) {
        throw "校验文件名不匹配 / Checksum sidecar name does not match: $resolvedChecksumPath"
    }

    $archiveName = [System.IO.Path]::GetFileName($resolvedArchivePath)
    $checksumText = [System.IO.File]::ReadAllText($resolvedChecksumPath)
    $checksumPattern = "\A(?<hash>[0-9A-Fa-f]{64})  $([regex]::Escape($archiveName))(?:\r?\n)?\z"
    $checksumMatch = [regex]::Match(
        $checksumText,
        $checksumPattern,
        [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
    if (-not $checksumMatch.Success) {
        throw "校验文件格式错误 / Invalid checksum syntax; expected HASH  $archiveName"
    }

    $expectedHash = $checksumMatch.Groups['hash'].Value
    $actualHash = (Get-FileHash -LiteralPath $resolvedArchivePath -Algorithm SHA256).Hash
    if (-not [string]::Equals(
            $actualHash,
            $expectedHash,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "SHA-256 不匹配 / SHA-256 mismatch"
    }

    $tempRoot = [System.IO.Path]::GetTempPath()
    $uniqueDirectoryName = "TerminalHelper-package-$([guid]::NewGuid().ToString('N'))"
    $extractionDirectory = Join-Path $tempRoot $uniqueDirectoryName
    New-Item -ItemType Directory -Path $extractionDirectory | Out-Null
    Expand-Archive `
        -LiteralPath $resolvedArchivePath `
        -DestinationPath $extractionDirectory

    $verifyPortableScript = Join-Path $PSScriptRoot 'verify-portable.ps1'
    & $verifyPortableScript -PublishDirectory $extractionDirectory

    Write-Host "ZIP 与校验文件验证通过 / ZIP and checksum verified: $resolvedArchivePath"
}
catch {
    throw "发布包验证失败 / Package verification failed: $($_.Exception.Message)"
}
finally {
    if ($null -ne $extractionDirectory -and (Test-Path -LiteralPath $extractionDirectory)) {
        Remove-Item -LiteralPath $extractionDirectory -Recurse -Force
    }
}
