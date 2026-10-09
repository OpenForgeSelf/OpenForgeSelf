#!/usr/bin/env pwsh
<#
.SYNOPSIS
  单插件"含前端构建"发布：先构建插件 web/dist，再走 plugin-publish-verify 一键闭环。

.DESCRIPTION
  build.ps1 全量发布只构建宿主前端（ForgeSelf.Web），不构建插件前端。
  本脚本补齐这个缺口：
    1) 若插件含 web/package.json -> pnpm install(缺 node_modules 时) + pnpm run build
    2) 调用 .agents/skills/plugin-publish-verify/scripts/run-plugin-publish-verify.ps1
       （全量发布 -> 确保 publish 宿主 -> 单插件发布 -> 覆盖触发热重载 -> 验证）

.EXAMPLE
  pwsh scripts/publish-plugin-full.ps1 -Plugin AIAgent -BumpVersion
  pwsh scripts/publish-plugin-full.ps1 -Plugin AIAgent -BumpVersion -SkipPublish   # 宿主产物已最新
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Plugin,            # 插件目录名（PascalCase，如 AIAgent）

    [switch] $BumpVersion,       # 透传：plugin.json patch+1（推荐——同版本字节不变无法证明热重载生效）
    [string] $PluginsRoot,       # 透传：默认 <repo>\publish\Plugins
    [int]    $Port = 0           # 透传：0 = 自动探测
)

$ErrorActionPreference = 'Stop'
$repoRoot  = Resolve-Path (Join-Path $PSScriptRoot '..')

# 构建临时/缓存目录指向项目内 .temp，避开沙箱 safe-delete shim 对系统 TEMP（AppData\Local\Temp）
# 删除的拦截——esbuild/vite 构建中会清理临时文件，落到系统 TEMP 会报 Access is denied（2026-10-06 实证）。
# .temp 已被 .gitignore / check-git-content 排除，不会入库。
$buildTemp = Join-Path $repoRoot '.temp'
New-Item -ItemType Directory -Force -Path $buildTemp | Out-Null
$env:TEMP   = $buildTemp
$env:TMP    = $buildTemp
$env:TMPDIR = $buildTemp

$pluginDir = Join-Path $repoRoot "Plugins/$Plugin"
if (-not (Test-Path $pluginDir)) { throw "plugin dir not found: $pluginDir" }

# ---- 1) 插件前端构建（build.ps1 不覆盖这一步）----
$webDir = Join-Path $pluginDir 'web'
if (Test-Path (Join-Path $webDir 'package.json')) {
    Write-Host "[1/2] plugin frontend build: $webDir" -ForegroundColor Cyan
    Push-Location $webDir
    try {
        if (-not (Test-Path (Join-Path $webDir 'node_modules'))) {
            Write-Host "  node_modules missing -> pnpm install"
            & pnpm install
            if ($LASTEXITCODE -ne 0) { throw "pnpm install failed (exit=$LASTEXITCODE)" }
        }
        & pnpm run build
        if ($LASTEXITCODE -ne 0) { throw "pnpm build failed (exit=$LASTEXITCODE)" }
    } finally { Pop-Location }
    Write-Host "  dist ready: $(Join-Path $webDir 'dist')" -ForegroundColor Green
} else {
    Write-Host "[1/2] no web/package.json - skip frontend build" -ForegroundColor Gray
}

# ---- 2) 发布 + 热重载 + 验证 ----
Write-Host "[2/2] publish-verify pipeline" -ForegroundColor Cyan
$runScript = Join-Path $repoRoot '.agents/skills/plugin-publish-verify/scripts/run-plugin-publish-verify.ps1'
if (-not (Test-Path $runScript)) { throw "skill script not found: $runScript" }

# 铁律：必须哈希表 splat（按名绑定）；数组 splat 是按位置传参，会把 '-PluginsRoot' 塞进别的参数
$runParams = @{ Plugin = $Plugin }
if ($BumpVersion) { $runParams['BumpVersion'] = $true }
if ($PluginsRoot) { $runParams['PluginsRoot'] = $PluginsRoot }
if ($Port -gt 0)  { $runParams['Port'] = $Port }

& $runScript @runParams
exit $LASTEXITCODE
