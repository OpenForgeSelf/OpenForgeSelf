# publish-host.ps1 - dotnet-publish the ForgeSelf host (exe + all plugins staged by csproj targets).
# Produces a folder ready to be packaged by package-release.ps1. Default is self-contained
# win-x64 (end user downloads zip, double-clicks exe, no .NET install needed).
#
# Examples:
#   ./publish-host.ps1 -Version 0.1.0
#   ./publish-host.ps1 -Version 0.1.0 -OutputDir D:/tmp/pub -FrameworkDependent

[CmdletBinding()]
param(
    [string]$RepoRoot = '',
    [string]$Version = '0.0.0-local',
    [string]$OutputDir = '',
    [string]$Configuration = 'Release',
    [switch]$FrameworkDependent
)

. (Join-Path $PSScriptRoot 'release-lib.ps1')
if (-not $RepoRoot) { $RepoRoot = Get-ReleaseRepoRoot }
if (-not $OutputDir) { $OutputDir = Join-Path $RepoRoot 'artifacts/publish' }

$ver = Get-NormalizedVersion $Version
$proj = Join-Path $RepoRoot 'ForgeSelf.Api/ForgeSelf.Api.csproj'
$selfContained = if ($FrameworkDependent) { 'false' } else { 'true' }

Invoke-ReleaseStep "host: dotnet publish ($Configuration, win-x64, selfContained=$selfContained, Version=$ver)" {
    # Note: --no-self-contained is implied by SelfContained=false together with Runtime.
    & dotnet publish $proj -c $Configuration -r win-x64 --self-contained $selfContained `
        -p:Version=$ver -p:ContinuousIntegrationBuild=true -o $OutputDir `
        -nologo -v minimal
    Assert-ExitCode 'dotnet publish'
}

$exe = Join-Path $OutputDir 'ForgeSelf.exe'
if (-not (Test-Path $exe)) { throw "publish output missing: $exe" }
$pluginDir = Join-Path $OutputDir 'Plugins'
$manifests = @(Get-ChildItem $pluginDir -Recurse -Filter 'plugin.json' -ErrorAction SilentlyContinue)
Write-Host ("publish-host: exe ok, plugin manifests staged: {0}" -f $manifests.Count)
