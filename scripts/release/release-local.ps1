# release-local.ps1 - one-command release pipeline orchestrator.
# Runs: build-frontend -> publish-host -> package-release -> make-release-notes.
# The GitHub Actions workflow calls exactly this script; everything CI does is
# reproducible locally with the same command (local-first debugging contract).
#
# Examples:
#   ./release-local.ps1                                  # local dry run, Version=0.0.0-local
#   ./release-local.ps1 -Version 0.1.0 -SkipFrontend     # reuse existing web dist (fast iteration)
#   ./release-local.ps1 -Version v0.1.0 -FrameworkDependent -Sign

[CmdletBinding()]
param(
    [string]$Version = '',
    [string]$OutputRoot = '',
    [string]$UpdateDir = '',
    [switch]$SkipFrontend,
    [switch]$FrameworkDependent,
    [switch]$Sign
)

. (Join-Path $PSScriptRoot 'release-lib.ps1')
$repoRoot = Get-ReleaseRepoRoot

# In CI the tag name arrives via GITHUB_REF_NAME (e.g. v0.1.0); locally default to a dev version.
if (-not $Version) {
    if ($env:GITHUB_REF_NAME) { $Version = $env:GITHUB_REF_NAME } else { $Version = '0.0.0-local' }
}
if (-not $OutputRoot) { $OutputRoot = Join-Path $repoRoot 'artifacts' }
$ver = Get-NormalizedVersion $Version
$publishDir = Join-Path $OutputRoot 'publish'
$releaseDir = Join-Path $OutputRoot 'release'

Write-Host ("release-local: Version={0} OutputRoot={1}" -f $Version, $OutputRoot)

$t0 = Get-Date

if (-not $SkipFrontend) {
    & (Join-Path $PSScriptRoot 'build-frontend.ps1') -RepoRoot $repoRoot
    if ($LASTEXITCODE -ne 0) { throw 'build-frontend failed' }
}
else {
    Write-Host 'SKIPPED build-frontend (-SkipFrontend): using existing wwwroot / plugin dists.'
}

& (Join-Path $PSScriptRoot 'publish-host.ps1') -RepoRoot $repoRoot -Version $Version -OutputDir $publishDir -FrameworkDependent:$FrameworkDependent
if ($LASTEXITCODE -ne 0) { throw 'publish-host failed' }

& (Join-Path $PSScriptRoot 'package-release.ps1') -RepoRoot $repoRoot -Version $Version -PublishDir $publishDir -OutputDir $releaseDir -Sign:$Sign
if ($LASTEXITCODE -ne 0) { throw 'package-release failed' }

& (Join-Path $PSScriptRoot 'make-release-notes.ps1') -RepoRoot $repoRoot -Version $Version -OutFile (Join-Path $releaseDir ("RELEASE-NOTES-{0}.md" -f $ver))
if ($LASTEXITCODE -ne 0) { throw 'make-release-notes failed' }

# 本地目录更新源（2026-09-27）：-UpdateDir <目录> 时把 zip + SHA256SUMS + 更新说明拷到该目录，
# 宿主设置页把「更新地址」填为该目录即可在页面点「检查更新 → 下载 → 重启并更新」（离线/内网更新）。
if ($UpdateDir) {
    New-Item -ItemType Directory -Force -Path $UpdateDir | Out-Null
    $zip = Join-Path $releaseDir ("OpenForgeSelf-{0}-win-x64.zip" -f $ver)
    if (-not (Test-Path $zip)) { throw "zip not found for UpdateDir copy: $zip" }
    foreach ($f in @($zip, (Join-Path $releaseDir 'SHA256SUMS.txt'), (Join-Path $releaseDir ("RELEASE-NOTES-{0}.md" -f $ver)))) {
        if (Test-Path $f) { Copy-Item $f $UpdateDir -Force }
    }
    Write-Host ''
    Write-Host ("release-local: update dir = {0}" -f $UpdateDir) -ForegroundColor Green
    Get-ChildItem $UpdateDir | ForEach-Object {
        Write-Host ("  {0}  ({1:n1} MB)" -f $_.Name, ($_.Length / 1MB))
    }
}

$dt = ((Get-Date) - $t0).TotalSeconds
Write-Host ''
Write-Host ("release-local: ALL DONE in {0:n0}s" -f $dt)
Get-ChildItem $releaseDir | ForEach-Object {
    Write-Host ("  {0}  ({1:n1} MB)" -f $_.Name, ($_.Length / 1MB))
}
