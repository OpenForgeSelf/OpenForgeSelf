# package-release.ps1 - turns a publish folder into a distributable zip.
# Steps: inject runtime-probe libs (SQLite) -> strip debug/runtime junk -> zip -> SHA256SUMS.
# The two DLLs under build/runtime/Plugins are NOT NuGet dependencies: NewLife.XCode probes
# System.Data.SQLite.dll / e_sqlite3.dll at runtime from the Plugins folder, so every clean
# build must inject them (historically they were placed manually in publish/ and leaked out of git).
#
# Example:
#   ./package-release.ps1 -Version 0.1.0
#   ./package-release.ps1 -Version 0.1.0 -PublishDir artifacts/publish -IncludePdb

[CmdletBinding()]
param(
    [string]$RepoRoot = '',
    [string]$Version = '0.0.0-local',
    [string]$PublishDir = '',
    [string]$OutputDir = '',
    [switch]$IncludePdb,
    [switch]$Sign
)

. (Join-Path $PSScriptRoot 'release-lib.ps1')
if (-not $RepoRoot) { $RepoRoot = Get-ReleaseRepoRoot }
if (-not $PublishDir) { $PublishDir = Join-Path $RepoRoot 'artifacts/publish' }
if (-not $OutputDir) { $OutputDir = Join-Path $RepoRoot 'artifacts/release' }

$ver = Get-NormalizedVersion $Version
if (-not (Test-Path (Join-Path $PublishDir 'ForgeSelf.exe'))) { throw "not a publish dir: $PublishDir" }

Invoke-ReleaseStep 'package: inject runtime-probe libs' {
    $src = Join-Path $RepoRoot 'build/runtime/Plugins'
    $dstPlugs = Join-Path $PublishDir 'Plugins'
    New-Item -ItemType Directory -Force -Path $dstPlugs | Out-Null
    foreach ($f in @('System.Data.SQLite.dll', 'e_sqlite3.dll')) {
        Copy-Item (Join-Path $src $f) (Join-Path $dstPlugs $f) -Force
        if (-not (Test-Path (Join-Path $dstPlugs $f))) { throw "runtime lib not injected: $f" }
    }
}

Invoke-ReleaseStep 'package: sanitize output' {
    # Runtime-generated dirs never belong in a distributable (they appear if someone ran the exe in-place).
    foreach ($d in @('Data', 'Log', 'Config')) {
        $p = Join-Path $PublishDir $d
        if (Test-Path $p) { Remove-Item $p -Recurse -Force }
    }
    $bk = Join-Path $PublishDir 'Plugins/_backups'
    if (Test-Path $bk) { Remove-Item $bk -Recurse -Force }
    if (-not $IncludePdb) {
        Get-ChildItem $PublishDir -Recurse -Filter '*.pdb' | Remove-Item -Force
    }
}

if ($Sign) {
    Invoke-ReleaseStep 'package: Authenticode signing' {
        & (Join-Path $RepoRoot 'scripts/sign-publish.ps1') -PublishDir $PublishDir
        Assert-ExitCode 'sign-publish'
    }
}

$zipPath = Join-Path $OutputDir ("OpenForgeSelf-{0}-win-x64.zip" -f $ver)
Invoke-ReleaseStep 'package: zip + sha256' {
    New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory($PublishDir, $zipPath,
        [System.IO.Compression.CompressionLevel]::Optimal, $false)
    $hash = (Get-FileHash $zipPath -Algorithm SHA256).Hash.ToLower()
    $sumsPath = Join-Path $OutputDir 'SHA256SUMS.txt'
    Set-Content -Path $sumsPath -Value ("{0}  {1}" -f $hash, (Split-Path $zipPath -Leaf)) -Encoding ascii
}

Write-Host ("package-release: artifact = {0}" -f $zipPath)
Write-Host ("  size = {0:n1} MB" -f ((Get-Item $zipPath).Length / 1MB))
