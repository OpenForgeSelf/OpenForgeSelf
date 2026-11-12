#!/usr/bin/env pwsh
# Package a single plugin into a .zip release (flat root).
# .forgeself-plugin is no longer produced or accepted by the host.
#
# Package layout (root): plugin.json + entry DLL (+ deps) + web/dist/**
# Host-shared assemblies and their pdb (ForgeSelf.* / NewLife.* / XCode / MX) are EXCLUDED.
# Microsoft.Extensions.* dll/pdb are excluded too; the host already provides them.
# A 3-part Version in plugin.json is stamped with yyMMddHHmm inside the package only.
#
# Usage:
#   pwsh -NoProfile -File scripts/package-plugin.ps1 -Plugin AIAgent
#   pwsh -NoProfile -File scripts/package-plugin.ps1 -Source <plugin-src-dir>

[CmdletBinding()]
param(
    [string] $Plugin,
    [string] $Source,
    [string] $OutDir,
    [ValidateSet('Debug', 'Release')] [string] $Configuration = 'Release',
    [switch] $Force,
    [switch] $DryRun
)

$ErrorActionPreference = 'Stop'
$scriptDir = $PSScriptRoot
$repoRoot = Resolve-Path (Join-Path $scriptDir '..')

function Test-PluginPackageRelExcluded([string] $relative) {
    foreach ($part in ($relative -split '[\\/]')) {
        if ($part -in @('src', '.git', 'tests', '.forge', '.github')) { return $true }
    }
    if ($relative -like '*.forgeself-plugin') { return $true }
    return $false
}

function Test-HostSharedPackageFile([string] $pathOrName) {
    $name = [System.IO.Path]::GetFileName($pathOrName)
    if ([string]::IsNullOrWhiteSpace($name)) { return $false }
    $ext = [System.IO.Path]::GetExtension($name)
    if ($ext -ne '.dll' -and $ext -ne '.pdb') { return $false }
    if ($name -like 'ForgeSelf.*' -or $name -like 'NewLife.*' -or $name -like 'Microsoft.Extensions.*') { return $true }
    if ($name -eq 'XCode.dll' -or $name -eq 'XCode.pdb' -or $name -eq 'MX.dll' -or $name -eq 'MX.pdb') { return $true }
    return $false
}

if (-not $OutDir) {
    $OutDir = Join-Path (Join-Path $repoRoot 'artifacts') 'plugin-packages'
}

if (-not $Plugin -and -not $Source) { throw "Pass -Plugin or -Source" }

$csproj = if ($Source) {
    $found = @(Get-ChildItem -LiteralPath $Source -Filter *.csproj -File -ErrorAction SilentlyContinue)
    if ($found.Count -eq 0) {
        $srcRoot = Join-Path $Source 'src'
        if (Test-Path -LiteralPath $srcRoot) {
            $found = @(Get-ChildItem -LiteralPath $srcRoot -Filter *.csproj -File -Recurse -ErrorAction SilentlyContinue |
                Where-Object { $_.FullName -notmatch '[\\/](tests|bin|obj)[\\/]' })
        }
    }
    if ($found.Count -eq 0) { throw "No csproj under -Source $Source" }
    $found[0].FullName
} else {
    Join-Path $repoRoot "Plugins/$Plugin/$Plugin.csproj"
}
$sourceManifest = if ($Source) { Join-Path $Source 'plugin.json' } else { Join-Path $repoRoot "Plugins/$Plugin/plugin.json" }

if (-not (Test-Path $csproj)) { throw "Plugin csproj not found: $csproj" }
if (-not (Test-Path $sourceManifest)) { throw "Plugin manifest not found: $sourceManifest" }

# Read source plugin.json (fields are PascalCase; use .NET API to avoid PS5.1 encoding pitfalls)
$jsonText = [System.IO.File]::ReadAllText($sourceManifest, [System.Text.Encoding]::UTF8)
try {
    $sourceJson = $jsonText | ConvertFrom-Json
    $sourceVersion = [string]$sourceJson.Version
    $sourceId      = [string]$sourceJson.Id
    $sourceEntry   = [string]$sourceJson.EntryAssembly
    $sourceName    = [string]$sourceJson.Name
} catch {
    throw "Failed to parse plugin.json as JSON: $_"
}
if ([string]::IsNullOrWhiteSpace($sourceEntry)) { $sourceEntry = "$(if ($Plugin) { $Plugin } else { 'Plugin' }).dll" }
if ([string]::IsNullOrWhiteSpace($sourceVersion)) { throw "plugin.json missing Version field" }

# Same rule as scripts/release/release-lib.ps1 Get-FullReleaseVersion: 3 segments gain a 10-digit timestamp.
$releaseVersion = $sourceVersion
if ($sourceVersion -match '^\d+\.\d+\.\d+$') {
    $releaseVersion = "$sourceVersion.$((Get-Date).ToString('yyMMddHHmm'))"
}

$safeName = if ([string]::IsNullOrWhiteSpace($sourceName)) { $sourceId } else { ($sourceName -replace '[^\w\.-]+', '') }
if ([string]::IsNullOrWhiteSpace($safeName)) { $safeName = $sourceId }
$pkgName = "$safeName-$releaseVersion.zip"
$pkgPath = Join-Path $OutDir $pkgName

Write-Host "==============================================================="
Write-Host "[package-plugin] Plugin:      $sourceId"
Write-Host "[package-plugin] Version:     $releaseVersion"
Write-Host "[package-plugin] Entry:       $sourceEntry"
Write-Host "[package-plugin] Package:     $pkgPath"
Write-Host "[package-plugin] DryRun:      $DryRun"
Write-Host "==============================================================="

# Idempotency: package already exists and -Force not set -> skip
if ((Test-Path $pkgPath) -and -not $Force) {
    Write-Host "[package-plugin] Package already exists and -Force not set: skip."
    Write-Host "[package-plugin] To overwrite, add -Force; to package a new version, bump Version in plugin.json and rerun."
    exit 0
}

if ($DryRun) { Write-Host "[package-plugin] DRY-RUN: would publish + zip to $pkgPath"; exit 0 }

# 1) dotnet publish plugin csproj
$publishTemp = Join-Path ([System.IO.Path]::GetTempPath()) ("plugin-package-pub-" + [Guid]::NewGuid().ToString('N'))
$staging = Join-Path ([System.IO.Path]::GetTempPath()) ("plugin-package-stage-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $publishTemp | Out-Null
New-Item -ItemType Directory -Force -Path $staging | Out-Null

Write-Host "[package-plugin] dotnet publish $Plugin ($Configuration) -> $publishTemp"
& dotnet publish $csproj -c $Configuration -o $publishTemp --nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit=$LASTEXITCODE)" }

$entryPath = Join-Path $publishTemp $sourceEntry
if (-not (Test-Path $entryPath)) { throw "Published output missing entry DLL: $entryPath" }

# 宿主共享程序集及其 pdb、以及 Microsoft.Extensions.*：由宿主加载，不进插件包。
# 若拷进插件包，插件 ALC 会再加载一份副本 → IPlugin 等类型身份不一致 → 宿主启动即崩/插件无效。

# 2) Stage: plugin.json + entry DLL + deps (excl. host-shared) + web/dist
Copy-Item -LiteralPath $sourceManifest -Destination (Join-Path $staging 'plugin.json') -Force
$stagedManifest = Join-Path $staging 'plugin.json'
$manifestText = [System.IO.File]::ReadAllText($stagedManifest, [System.Text.Encoding]::UTF8)
$manifestText = [regex]::Replace($manifestText, '("Version"\s*:\s*")[^"]*"', ('${1}' + $releaseVersion + '"'))
$manifestText = [regex]::Replace($manifestText, '("version"\s*:\s*")[^"]*"', ('${1}' + $releaseVersion + '"'))
[System.IO.File]::WriteAllText($stagedManifest, $manifestText, [System.Text.UTF8Encoding]::new($false))

$sourceWebDist = if ($Source) { Join-Path $Source 'web/dist' } else { Join-Path (Join-Path (Join-Path (Join-Path $repoRoot 'Plugins') $Plugin) 'web') 'dist' }
if (Test-Path $sourceWebDist) {
    $webDest = Join-Path $staging (Join-Path 'web' 'dist')
    New-Item -ItemType Directory -Force -Path $webDest | Out-Null
    Copy-Item -Path (Join-Path $sourceWebDist '*') -Destination $webDest -Recurse -Force
    Write-Host "[package-plugin] Web assets staged from: $sourceWebDist"
}
else {
    Write-Host "[package-plugin] No web/dist dir: plugin ships backend only (skip)."
}

Get-ChildItem -LiteralPath $publishTemp -File | Where-Object { -not (Test-HostSharedPackageFile $_.Name) -and -not (Test-PluginPackageRelExcluded $_.Name) } | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $staging -Force
}
Get-ChildItem -LiteralPath $publishTemp -Directory | ForEach-Object {
    Get-ChildItem -LiteralPath $_.FullName -File -Recurse | Where-Object { -not (Test-HostSharedPackageFile $_.Name) } | ForEach-Object {
        $rel = $_.FullName.Substring($publishTemp.Length).TrimStart('\', '/')
        if (Test-PluginPackageRelExcluded $rel) { return }
        $target = Join-Path $staging $rel
        $targetDir = [System.IO.Path]::GetDirectoryName($target)
        if (-not (Test-Path $targetDir)) { New-Item -ItemType Directory -Force -Path $targetDir | Out-Null }
        Copy-Item -LiteralPath $_.FullName -Destination $target -Force
    }
}

# 3) Zip package. Host only accepts .zip.
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
if (Test-Path $pkgPath) { Remove-Item -LiteralPath $pkgPath -Force }
Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $pkgPath -CompressionLevel Optimal

# 4) Verify + hash. Fail closed if host-shared files, source, or the retired extension slipped in.
if (-not (Test-Path $pkgPath)) { throw "Package not produced: $pkgPath" }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$opened = [System.IO.Compression.ZipFile]::OpenRead($pkgPath)
try {
    $names = @($opened.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
    $bad = @($names | Where-Object {
        $_ -match '(^|/)(src|\.git|tests|\.forge|\.github)(/|$)' -or
        $_ -like '*.forgeself-plugin' -or
        (Test-HostSharedPackageFile ([System.IO.Path]::GetFileName($_)))
    })
    if ($bad.Count -gt 0) { throw "Package hygiene failed: $($bad -join ', ')" }
    Write-Host "[package-plugin] Zip entries:"
    $names | ForEach-Object { Write-Host "  $_" }
}
finally { $opened.Dispose() }
$hash = (Get-FileHash -LiteralPath $pkgPath -Algorithm SHA256).Hash
Write-Host "[package-plugin] Package OK: $pkgPath"
Write-Host "[package-plugin] SHA256: $hash"

# cleanup temp dirs
Remove-Item -LiteralPath $publishTemp -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "Next steps: put this .zip into the plugin update source directory."
Write-Host "The host scans *.zip only. .forgeself-plugin is retired."
exit 0
