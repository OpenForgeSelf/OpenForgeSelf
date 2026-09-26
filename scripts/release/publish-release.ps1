# publish-release.ps1 - creates the GitHub Release and uploads artifacts, via gh CLI.
# Same entry point locally (needs gh auth + a github remote) and in CI (uses secrets.GITHUB_TOKEN).
# Local dry run: ./publish-release.ps1 -Version 0.1.0 -DryRun
#
# Requires: a 'github' git remote (https://github.com/<owner>/<repo>) or -Repo override.

[CmdletBinding()]
param(
    [string]$RepoRoot = '',
    [string]$Version = '',
    [string]$ReleaseDir = '',
    [string]$Repo = '',
    [switch]$Prerelease,
    [switch]$Overwrite,
    [switch]$DryRun
)

. (Join-Path $PSScriptRoot 'release-lib.ps1')
if (-not $RepoRoot) { $RepoRoot = Get-ReleaseRepoRoot }
if (-not $Version) { throw 'publish-release: -Version is required (e.g. v0.1.0)' }
$ver = Get-NormalizedVersion $Version
$tag = "v$ver"
if (-not $ReleaseDir) { $ReleaseDir = Join-Path $RepoRoot 'artifacts/release' }

$gh = Get-ReleaseToolPath 'gh' 'C:\Program Files\GitHub CLI'
if (-not $gh) { throw 'gh CLI not found (PATH or C:\Program Files\GitHub CLI).' }

if (-not $Repo) {
    Push-Location $RepoRoot
    try {
        $url = (& git remote get-url github 2>$null)
        if ($LASTEXITCODE -ne 0 -or -not $url) {
            $url = (& git remote get-url origin 2>$null)
        }
    }
    finally { Pop-Location }
    if ($url -and $url -match 'github\.com[:/](.+?/.+?)(\.git)?$') { $Repo = $Matches[1] }
    if (-not $Repo) { throw 'no GitHub remote found. Add one: git remote add github https://github.com/<owner>/<repo>.git' }
}

$zip = Join-Path $ReleaseDir ("OpenForgeSelf-{0}-win-x64.zip" -f $ver)
if (-not (Test-Path $zip)) { throw "artifact missing: $zip (run release-local.ps1 first)" }
$assets = @($zip)
$sums = Join-Path $ReleaseDir 'SHA256SUMS.txt'
if (Test-Path $sums) { $assets += $sums }
$notes = Join-Path $ReleaseDir ("RELEASE-NOTES-{0}.md" -f $ver)

if (Test-Path $notes) {
    $noteArgs = @('--notes-file', $notes)
}
else {
    $noteArgs = @('--generate-notes')
}

if (-not $Prerelease -and $ver -match '-') {
    # Semver prerelease suffix (e.g. 0.1.0-test / 1.2.0-rc.1) marks the release as prerelease.
    $Prerelease = $true
    Write-Host 'publish-release: prerelease auto-detected from version suffix.'
}

Push-Location $RepoRoot
try {
    & $gh release view $tag --repo $Repo 2>$null | Out-Null
    $exists = ($LASTEXITCODE -eq 0)
    if ($exists) {
        if (-not $Overwrite) { throw "release $tag already exists on $Repo (use -Overwrite to replace)." }
        if ($DryRun) {
            Write-Host "DRY: gh release delete $tag --repo $Repo --yes"
        }
        else {
            & $gh release delete $tag --repo $Repo --yes
            Assert-ExitCode 'gh release delete'
        }
    }
    $cmd = "gh release create $tag --repo $Repo --title 'OpenForgeSelf $tag' $($noteArgs -join ' ')"
    if ($Prerelease) { $cmd += ' --prerelease' }
    Write-Host "publish-release: $cmd <assets>"
    if ($DryRun) {
        Write-Host "DRY: assets = $($assets -join ', ')"
        Write-Host 'publish-release: dry run finished (nothing published).'
        return
    }
    $createArgs = @('release', 'create', $tag, '--repo', $Repo, '--title', "OpenForgeSelf $tag") + $noteArgs
    if ($Prerelease) { $createArgs += '--prerelease' }
    $createArgs += $assets
    & $gh $createArgs
    Assert-ExitCode 'gh release create'
    Write-Host "publish-release: https://github.com/$Repo/releases/tag/$tag"
}
finally { Pop-Location }
