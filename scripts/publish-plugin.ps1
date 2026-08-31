#!/usr/bin/env pwsh
# Publish a single plugin to <PluginsRoot>/_backups/<id>/<version>/
# Use together with host runtime `POST /api/plugin/update/{id}` to enable
# "single plugin, no host restart" end-to-end capability.
#
# Design: this script only handles "build + staged copy" (backend DLLs AND, when
# present, the plugin's built web assets under <Plugin>/web/dist/; its sources
# under <Plugin>/web/src/ are NOT shipped).
# Version comparison,
# `current` pointer switch, unloading old ALC, MVC endpoint refresh, and
# file-system watch are handled by the host runtime (PluginVersionService +
# PluginManager + FileSystemWatcher). Clean separation of concerns.
#
# Usage:
#   ./publish-plugin.ps1 -Plugin AIAgent                                    # dev mode (default)
#   ./publish-plugin.ps1 -Plugin AIAgent -Configuration Release
#   ./publish-plugin.ps1 -Plugin AIAgent -PluginsRoot "D:/deploy/Plugins"   # explicit PluginsRoot
#   ./publish-plugin.ps1 -Plugin AIAgent -Force                              # overwrite existing staged
#   ./publish-plugin.ps1 -Plugin AIAgent -DryRun                             # print only, no side effects
#
# NOTE: -Plugin is the **directory name** (PascalCase, e.g. AIAgent), NOT the
# kebab-case id. The script reads the kebab-case id from plugin.json Id field
# and uses it to build the staged path: <PluginsRoot>/_backups/<id>/<version>/
# This follows the project's two-layer naming convention (PascalCase for code
# identity, kebab-case for runtime identity).
#
# Prereqs:
#   - Host already running (so staged copy is picked up by FileSystemWatcher /
#     reachable via /api/plugin/update). Otherwise next host start uses new version.
#   - .NET 8 SDK (matches ForgeSelf.Api)

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $Plugin,
    [ValidateSet('Debug', 'Release')] [string] $Configuration = 'Release',
    [string] $PluginsRoot,   # Plugin root; default = dev mode (source Plugins)
    [switch] $Force,
    [switch] $DryRun
)

$ErrorActionPreference = 'Stop'
$scriptDir = $PSScriptRoot
$repoRoot = Resolve-Path (Join-Path $scriptDir '..')

# Default PluginsRoot = source Plugins (dev mode); release mode passed explicitly
if (-not $PluginsRoot) {
    $PluginsRoot = Join-Path $repoRoot 'ForgeSelf.Api/Plugins'
}

$csproj = Join-Path $repoRoot "ForgeSelf.Api/Plugins/$Plugin/$Plugin.csproj"
$sourceManifest = Join-Path $repoRoot "ForgeSelf.Api/Plugins/$Plugin/plugin.json"

if (-not (Test-Path $csproj)) { throw "Plugin csproj not found: $csproj" }
if (-not (Test-Path $sourceManifest)) { throw "Plugin manifest not found: $sourceManifest" }

# Read source plugin.json (Version / EntryAssembly / Id; manifest fields are PascalCase)
# Use .NET API to avoid PowerShell encoding pitfalls (default Get-Content may be ANSI on PS 5.1).
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
if ($sourceId -ne $Plugin) { Write-Warning "plugin.json Id='$sourceId' does not match -Plugin '$Plugin'" }

# Compute staged target: <PluginsRoot>/_backups/<id>/<version>/
$stagedDir = Join-Path (Join-Path (Join-Path $PluginsRoot '_backups') $sourceId) $sourceVersion

Write-Host "==============================================================="
Write-Host "[publish-plugin] Plugin:       $sourceId"
Write-Host "[publish-plugin] Source ver:   $sourceVersion"
Write-Host "[publish-plugin] Entry:        $sourceEntry"
Write-Host "[publish-plugin] PluginsRoot:  $PluginsRoot"
Write-Host "[publish-plugin] Staged dir:   $stagedDir"
Write-Host "[publish-plugin] DryRun:       $DryRun"
Write-Host "==============================================================="

# Define helper BEFORE first call (PowerShell is top-down)
function Print-SuccessTail {
    Write-Host ""
    Write-Host "Next steps (host runtime must be running):"
    Write-Host "  1) Check for available updates:"
    Write-Host "     curl.exe http://localhost:7102/api/plugin/updates"
    Write-Host "  2) Trigger update (host will: version compare -> switch current -> unload old ALC -> load new DLL -> refresh MVC endpoints):"
    Write-Host "     curl.exe -X POST http://localhost:7102/api/plugin/update/$sourceId"
    Write-Host "  3) Rollback to a specific version:"
    $rollbackCmd = 'curl.exe -X POST http://localhost:7102/api/plugin/rollback/' + $sourceId + ' -H "Content-Type: application/json" -d "{\"version\":\"' + $sourceVersion + '\"}"'
    Write-Host "     $rollbackCmd"
    Write-Host "  Note: actual endpoint paths follow controllers/PluginController.cs [Route]; port follows appsettings.json (default 7102)."
}

# Idempotency: if staged dir exists and source version <= highest staged version, skip (unless -Force)
$backupDir = Join-Path (Join-Path $PluginsRoot '_backups') $sourceId
$hasExisting = Test-Path $stagedDir
$existingHighest = $null
if (Test-Path $backupDir) {
    $existingHighest = Get-ChildItem -LiteralPath $backupDir -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^\d+(\.\d+){0,3}$' } |
        ForEach-Object { [version]$_.Name } |
        Sort-Object -Descending |
        Select-Object -First 1
}

if ($hasExisting -and -not $Force) {
    Write-Host "[publish-plugin] Staged dir already exists and -Force not set: skip."
    Write-Host "[publish-plugin] To force overwrite, add -Force; to publish a new version, bump Version in plugin.json and rerun."
    Print-SuccessTail
    exit 0
}

# 1) dotnet publish the plugin csproj (output contains entry.dll + all NuGet deps)
$publishTemp = Join-Path ([System.IO.Path]::GetTempPath()) ("plugin-publish-" + [Guid]::NewGuid().ToString('N'))
if ($DryRun) { $publishTemp = "<dry-run>" }

Write-Host "[publish-plugin] dotnet publish $Plugin ($Configuration) -> $publishTemp"
if (-not $DryRun) {
    New-Item -ItemType Directory -Force -Path $publishTemp | Out-Null
    & dotnet publish $csproj -c $Configuration -o $publishTemp --nologo -v minimal
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit=$LASTEXITCODE)" }

    $entryPath = Join-Path $publishTemp $sourceEntry
    if (-not (Test-Path $entryPath)) { throw "Published output missing entry DLL: $entryPath" }
}

# 2) Copy to staged dir
if (-not $DryRun) {
    if (Test-Path $stagedDir) { Remove-Item -LiteralPath $stagedDir -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $stagedDir | Out-Null

    # 宿主共享程序集（ForgeSelf.* / NewLife.*）：由宿主在默认加载上下文加载，插件经 ALC 回落宿主获取。
    # 若把它们复制进插件目录，插件 ALC 会再加载一份副本 → IPlugin 等类型身份不一致 →
    # 「插件类型未实现 IPlugin 接口」。工作插件目录（如 TodoTracker）只含入口 dll + plugin.json，故此处排除。
    $HostSharedAssemblies = {
        param($file)
        $name = [System.IO.Path]::GetFileName($file)
        ($name -match '^ForgeSelf\..*\.dll$') -or ($name -match '^NewLife\..*\.dll$') -or ($name -eq 'XCode.dll') -or ($name -eq 'MX.dll')
    }

    # Copy plugin.json (force overwrite to ensure match with source)
    Copy-Item -LiteralPath $sourceManifest -Destination (Join-Path $stagedDir 'plugin.json') -Force

    # Copy self-contained web assets (spec 010 / FR-011):
    # <Plugin>/web/dist/** -> <stagedDir>/web/dist/**
    # Only 'dist' (built ESM output) is shipped: 'src' and 'node_modules' are
    # deliberately excluded so the distributed unit stays small and does not leak sources.
    # Layout: see specs/010-plugin-frontend-runtime/design/plugin-directory-layout.md
    $sourceWebDist = Join-Path (Join-Path (Join-Path (Join-Path $repoRoot 'ForgeSelf.Api/Plugins') $Plugin) 'web') 'dist'
    if (Test-Path $sourceWebDist) {
        $webDest = Join-Path $stagedDir (Join-Path 'web' 'dist')
        New-Item -ItemType Directory -Force -Path $webDest | Out-Null
        Copy-Item -Path (Join-Path $sourceWebDist '*') -Destination $webDest -Recurse -Force
        Write-Host "[publish-plugin] Web assets staged from: $sourceWebDist"
    }
    else {
        Write-Host "[publish-plugin] No web/dist dir: plugin ships backend only (skip)."
    }

    # Copy all published files (entry.dll + deps + resources), excluding host-shared assemblies
    Get-ChildItem -LiteralPath $publishTemp -File | Where-Object { -not (& $HostSharedAssemblies $_.FullName) } | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $stagedDir -Force
    }
    # Recurse subdirectories
    Get-ChildItem -LiteralPath $publishTemp -Directory | ForEach-Object {
        $sub = $_.Name
        $subDest = Join-Path $stagedDir $sub
        New-Item -ItemType Directory -Force -Path $subDest | Out-Null
        Get-ChildItem -LiteralPath $_.FullName -File -Recurse | Where-Object { -not (& $HostSharedAssemblies $_.FullName) } | ForEach-Object {
            $rel = $_.FullName.Substring($_.FullName.IndexOf($sub, [System.StringComparison]::OrdinalIgnoreCase))
            $target = Join-Path $stagedDir $rel
            $targetDir = Split-Path -LiteralPath $target -Parent
            if (-not (Test-Path $targetDir)) { New-Item -ItemType Directory -Force -Path $targetDir | Out-Null }
            Copy-Item -LiteralPath $_.FullName -Destination $target -Force
        }
    }

    Remove-Item -LiteralPath $publishTemp -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "[publish-plugin] Staged complete: $stagedDir"
Print-SuccessTail
