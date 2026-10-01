#!/usr/bin/env pwsh
# dev-plugin —— 插件开发态一条命令热重载（dev-only，PILOT-plugin-dev-experience）
#
# 用途：改插件 C# 后，不动 plugin.json 版本号，直接在 dev 宿主上重载生效（shadow-copy 装载）。
# 前提：dev 宿主已以 FORGESELF_DEV_MODE=1 + --plugins-dir <repo>/Plugins 启动（或 dev 宿主
#       使用默认插件目录但 shadow 已装此插件）。
#
# 用法:
#   ./dev-plugin.ps1 -Plugin FileTools                          # 编译单个插件并热重载
#   ./dev-plugin.ps1 -Plugin FileTools -Configuration Release   # 指定构建配置（须与宿主 FORGESELF_DEV_CONFIG 一致）
#   ./dev-plugin.ps1 -All                                       # 全量编译（宿主）+ 全量重载
#   ./dev-plugin.ps1 -Plugin FileTools -HostUrl http://localhost:7200 -Token sk-xxx
#   ./dev-plugin.ps1 -Plugin FileTools -DataRoot D:/dev-data    # 从自定义数据根读 token
#
# 端口约定：禁止硬编码 7102/7002（AGENTS.md §2.3），默认取 FORGESELF_DEV_HOST 环境变量，
# 未设置时回落 http://localhost:7102（当前 ForgeSetting.config 的实际端口可不同，请显式传 -HostUrl）。

[CmdletBinding()]
param(
    [string] $Plugin,                       # PascalCase 源码目录名（如 FileTools）；-All 时可省
    [switch] $All,                          # 全量：dotnet build 宿主 + POST reload-all
    [ValidateSet('Debug', 'Release')] [string] $Configuration = 'Debug',
    [string] $HostUrl,                      # dev 宿主地址；默认 $env:FORGESELF_DEV_HOST，再回落 http://localhost:7102
    [string] $Token,                        # API token（sk-...）；缺省经 get-forge-token.cjs 解密 ForgeSetting.config
    [string] $DataRoot                      # dev 宿主数据根（token 解密用）；默认 %USERPROFILE%\.forgeself
)

$ErrorActionPreference = 'Stop'
$scriptDir = $PSScriptRoot
$repoRoot = Resolve-Path (Join-Path $scriptDir '..')

if (-not $HostUrl) { $HostUrl = if ($env:FORGESELF_DEV_HOST) { $env:FORGESELF_DEV_HOST } else { 'http://localhost:7102' } }
$HostUrl = $HostUrl.TrimEnd('/')

if (-not $All -and [string]::IsNullOrWhiteSpace($Plugin)) {
    throw "必须提供 -Plugin <PascalCase目录名> 或 -All"
}

# ── 1. 构建 ────────────────────────────────────────────────────────────────
if ($All) {
    Write-Host "[dev-plugin] 全量构建宿主（含全部插件项目引用）..."
    & dotnet build (Join-Path $repoRoot 'ForgeSelf.Api/ForgeSelf.Api.csproj') -c $Configuration --nologo -v minimal
    if ($LASTEXITCODE -ne 0) { throw "dotnet build 失败 (exit=$LASTEXITCODE)" }
}
else {
    $csproj = Join-Path $repoRoot "Plugins/$Plugin/$Plugin.csproj"
    if (-not (Test-Path $csproj)) { throw "插件 csproj 不存在: $csproj（-Plugin 传 PascalCase 目录名，如 FileTools）" }

    Write-Host "[dev-plugin] 构建插件 $Plugin ($Configuration)..."
    & dotnet build $csproj -c $Configuration --nologo -v minimal
    if ($LASTEXITCODE -ne 0) { throw "dotnet build 失败 (exit=$LASTEXITCODE)" }
}

# ── 2. Token ───────────────────────────────────────────────────────────────
if ([string]::IsNullOrWhiteSpace($Token)) {
    $configPath = $null
    if ($DataRoot) {
        $configPath = Join-Path $DataRoot 'config/ForgeSetting.config'
    }
    else {
        $configPath = Join-Path $env:USERPROFILE '.forgeself/Config/ForgeSetting.config'
        if (-not (Test-Path $configPath)) {
            $configPath = Join-Path $env:USERPROFILE '.forgeself/config/ForgeSetting.config'
        }
    }

    if (Test-Path $configPath) {
        Write-Host "[dev-plugin] 从 $configPath 解密 token..."
        $Token = & node (Join-Path $scriptDir 'get-forge-token.cjs') --config $configPath
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($Token)) {
            throw "token 解密失败（宿主每次启动轮换 ApiToken，确认脚本指向的是目标 dev 宿主的数据根；或显式传 -Token）"
        }
    }
    else {
        throw "找不到 ForgeSetting.config: $configPath（dev 宿主请传 -DataRoot <数据根> 或 -Token）"
    }
}

# ── 3. 重载 ────────────────────────────────────────────────────────────────
$headers = @{ Authorization = "Bearer $Token" }

if ($All) {
    Write-Host "[dev-plugin] 全量重载 POST $HostUrl/api/dev/plugin/reload-all ..."
    $resp = Invoke-RestMethod -Method Post -Uri "$HostUrl/api/dev/plugin/reload-all" -Headers $headers -TimeoutSec 120
    $results = $resp.data
    $ok = @($results | Where-Object { $_.success }).Count
    Write-Host "[dev-plugin] 全量重载完成: 成功 $ok/$($results.Count)"
    $results | ForEach-Object {
        $mark = if ($_.success) { '✓' } else { '✗' }
        Write-Host ("  {0} {1}  state={2}" -f $mark, $_.pluginId, $_.state)
        $_.warnings | ForEach-Object { Write-Host "      ⚠ $_" }
    }
}
else {
    # 读取 plugin.json 的 kebab-case Id（.NET API 解析避免 PS5.1 编码坑）
    $manifestPath = Join-Path $repoRoot "Plugins/$Plugin/plugin.json"
    $jsonText = [System.IO.File]::ReadAllText($manifestPath, [System.Text.Encoding]::UTF8)
    $pluginId = [string] (($jsonText | ConvertFrom-Json).Id)
    if ([string]::IsNullOrWhiteSpace($pluginId)) { throw "plugin.json 缺少 Id 字段: $manifestPath" }

    Write-Host "[dev-plugin] 热重载 POST $HostUrl/api/dev/plugin/$pluginId/reload ..."
    $resp = Invoke-RestMethod -Method Post -Uri "$HostUrl/api/dev/plugin/$pluginId/reload" -Headers $headers -TimeoutSec 120
    $r = $resp.data

    if ($r.success) {
        Write-Host "[dev-plugin] ✓ $($r.pluginId) 已同版本重载（v$($r.versionAfter)）state=$($r.state)"
        if ($r.shadowPath) { Write-Host "[dev-plugin]   shadow: $($r.shadowPath)" }
    }
    else {
        Write-Warning "[dev-plugin] ✗ $($r.pluginId) 重载失败（state=$($r.state)）"
    }
    $r.warnings | ForEach-Object { Write-Host "      ⚠ $_" }
}

Write-Host "[dev-plugin] 完成。"
