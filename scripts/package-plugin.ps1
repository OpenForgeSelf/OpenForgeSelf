#!/usr/bin/env pwsh
# Package a single plugin into a standalone .forgeself-plugin package
# for the plugin update source (local package directory, 2026-09-28, 输入27).
#
# Package layout (root): plugin.json + entry DLL (+ deps) + web/dist/**
# Host-shared assemblies (ForgeSelf.* / NewLife.* / XCode.dll / MX.dll) are
# EXCLUDED (they are loaded by the host default context; including them breaks
# plugin ALC type identity - see publish-plugin.ps1 notes).
#
# Usage:
#   ./package-plugin.ps1 -Plugin AIAgent                                   # -> artifacts/plugin-packages
#   ./package-plugin.ps1 -Plugin AIAgent -OutDir "D:\plugin-packages"
#   ./package-plugin.ps1 -Plugin AIAgent -Configuration Debug
#   ./package-plugin.ps1 -Plugin AIAgent -Force                             # overwrite existing package
#   ./package-plugin.ps1 -Plugin AIAgent -DryRun                            # print only, no side effects
#
# NOTE: -Plugin is the **directory name** (PascalCase, e.g. AIAgent), NOT the
# kebab-case id. Package file name: <id>-<ver>.forgeself-plugin

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $Plugin,
    [string] $OutDir,                # default: <repo>\artifacts\plugin-packages
    [ValidateSet('Debug', 'Release')] [string] $Configuration = 'Release',
    [switch] $Force,
    [switch] $DryRun
)

$ErrorActionPreference = 'Stop'
$scriptDir = $PSScriptRoot
$repoRoot = Resolve-Path (Join-Path $scriptDir '..')

if (-not $OutDir) {
    $OutDir = Join-Path (Join-Path $repoRoot 'artifacts') 'plugin-packages'
}

$csproj = Join-Path $repoRoot "Plugins/$Plugin/$Plugin.csproj"
$sourceManifest = Join-Path $repoRoot "Plugins/$Plugin/plugin.json"

if (-not (Test-Path $csproj)) { throw "Plugin csproj not found: $csproj" }
if (-not (Test-Path $sourceManifest)) { throw "Plugin manifest not found: $sourceManifest" }

# Read source plugin.json (fields are PascalCase; use .NET API to avoid PS5.1 encoding pitfalls)
$jsonText = [System.IO.File]::ReadAllText($sourceManifest, [System.Text.Encoding]::UTF8)
try {
    $sourceJson = $jsonText | ConvertFrom-Json
    $sourceVersion = [string]$sourceJson.Version
    $sourceId      = [string]$sourceJson.Id
    $sourceEntry   = [string]$sourceJson.EntryAssembly
} catch {
    throw "Failed to parse plugin.json as JSON: $_"
}
if ([string]::IsNullOrWhiteSpace($sourceEntry)) { $sourceEntry = "$Plugin.dll" }
if ([string]::IsNullOrWhiteSpace($sourceVersion)) { throw "plugin.json missing Version field" }

$pkgName = "$sourceId-$sourceVersion.forgeself-plugin"
$pkgPath = Join-Path $OutDir $pkgName

Write-Host "==============================================================="
Write-Host "[package-plugin] Plugin:      $sourceId"
Write-Host "[package-plugin] Version:     $sourceVersion"
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

# 宿主共享程序集：由宿主默认加载上下文加载，插件 ALC 回落宿主获取。
# 若拷进插件包，插件 ALC 会再加载一份副本 → IPlugin 等类型身份不一致 → 宿主启动即崩/插件无效。
$HostSharedAssemblies = {
    param($file)
    $name = [System.IO.Path]::GetFileName($file)
    ($name -match '^ForgeSelf\..*\.dll$') -or ($name -match '^NewLife\..*\.dll$') -or ($name -eq 'XCode.dll') -or ($name -eq 'MX.dll')
}

# 2) Stage: plugin.json + entry DLL + deps (excl. host-shared) + web/dist
Copy-Item -LiteralPath $sourceManifest -Destination (Join-Path $staging 'plugin.json') -Force

$sourceWebDist = Join-Path (Join-Path (Join-Path (Join-Path $repoRoot 'Plugins') $Plugin) 'web') 'dist'
if (Test-Path $sourceWebDist) {
    $webDest = Join-Path $staging (Join-Path 'web' 'dist')
    New-Item -ItemType Directory -Force -Path $webDest | Out-Null
    Copy-Item -Path (Join-Path $sourceWebDist '*') -Destination $webDest -Recurse -Force
    Write-Host "[package-plugin] Web assets staged from: $sourceWebDist"
}
else {
    Write-Host "[package-plugin] No web/dist dir: plugin ships backend only (skip)."
}

Get-ChildItem -LiteralPath $publishTemp -File | Where-Object { -not (& $HostSharedAssemblies $_.FullName) } | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $staging -Force
}
Get-ChildItem -LiteralPath $publishTemp -Directory | ForEach-Object {
    Get-ChildItem -LiteralPath $_.FullName -File -Recurse | Where-Object { -not (& $HostSharedAssemblies $_.FullName) } | ForEach-Object {
        $rel = $_.FullName.Substring($publishTemp.Length).TrimStart('\', '/')
        $target = Join-Path $staging $rel
        $targetDir = [System.IO.Path]::GetDirectoryName($target)
        if (-not (Test-Path $targetDir)) { New-Item -ItemType Directory -Force -Path $targetDir | Out-Null }
        Copy-Item -LiteralPath $_.FullName -Destination $target -Force
    }
}

# 3) Zip package (root = staging contents: plugin.json + DLLs + web/dist)
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
# Compress-Archive rejects any destination extension other than .zip, while the host
# scans *.forgeself-plugin (PluginVersionService), so archive to a temp .zip then rename.
$zipTemp = Join-Path ([System.IO.Path]::GetTempPath()) ("plugin-pkg-" + [Guid]::NewGuid().ToString('N') + ".zip")
Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $zipTemp -CompressionLevel Optimal -Force
Move-Item -LiteralPath $zipTemp -Destination $pkgPath -Force

# 4) Verify + hash
if (-not (Test-Path $pkgPath)) { throw "Package not produced: $pkgPath" }
$hash = (Get-FileHash -LiteralPath $pkgPath -Algorithm SHA256).Hash
Write-Host "[package-plugin] Package OK: $pkgPath"
Write-Host "[package-plugin] SHA256: $hash"

# cleanup temp dirs
Remove-Item -LiteralPath $publishTemp -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "Next steps: put this package into the plugin update source directory,"
Write-Host "then in the settings page (插件管理 - 插件更新源) set that directory,"
Write-Host "and click 检查更新 / 更新 in the plugin market (版本必须高于当前已安装版本)."
exit 0
