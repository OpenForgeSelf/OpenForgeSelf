#!/usr/bin/env pwsh
# dev-plugin-web —— 插件前端真 HMR dev server（2B，opt-in，dev-only；PILOT-plugin-dev-experience）
#
# 机制：在插件 web/ 目录用「生成式 vite 配置」vite.config.dev.mjs 启动 vite serve——
#   - 复用插件自身 vite.config.ts（import 合并，保留 vue/tailwind 等插件管线）；
#   - 把 external 的五个共享包 alias 到宿主 shim 绝对 URL（http://<宿主>/shared/*.js），
#     保住宿主 import map 语义（单 Vue 实例 / Pinia 共享 / Element Plus 主题），
#     绕开 vite dev 对裸导入的 node_modules 重写（重写即双 Vue，静默失效）；
#   - CORS 打开（宿主侧已 AllowAll）。
# 启动后把 {pluginId,port,url} 写入 web/.dev-server.json；宿主在 FORGESELF_DEV_WEB_HMR=1 时
# 读该文件把 frontend-manifest 的 entry 指向 dev server。Ctrl+C 退出时自动清理该文件。
#
# ⚠ 已知限制（选型报告 §三-2B）：alias 漏配/插件用到 shim 未导出的组件时可能静默双 Vue，
#   深度调试后建议回归静态产物验证。默认关闭，仅供需要保状态/秒级反馈的调试场景。
#
# 用法:
#   ./dev-plugin-web.ps1 -Plugin AIAgent
#   ./dev-plugin-web.ps1 -Plugin AIAgent -Port 5311 -HostFrontend http://localhost:7002
#   ./dev-plugin-web.ps1 -Plugin AIAgent -Stop          # 停止并清理 .dev-server.json

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $Plugin,   # PascalCase 目录名
    [int] $Port = 0,                                   # dev server 端口；0 = 自动（vite 默认 5173 起顺延）
    [string] $HostFrontend,                            # 宿主前端地址；默认 $env:FORGESELF_DEV_WEB_FRONTEND，再回落 http://localhost:7002
    [switch] $Stop                                     # 仅清理 .dev-server.json
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$webDir = Join-Path $repoRoot "Plugins/$Plugin/web"
$markerPath = Join-Path $webDir '.dev-server.json'

if (-not (Test-Path $webDir)) { throw "插件 web 目录不存在: $webDir" }

# ── -Stop：清理标记文件即退出 ──────────────────────────────────────────────
if ($Stop) {
    if (Test-Path $markerPath) {
        Remove-Item -LiteralPath $markerPath -Force
        Write-Host "[dev-plugin-web] 已清理 $markerPath"
    }
    else {
        Write-Host "[dev-plugin-web] 无标记文件，无需清理"
    }
    exit 0
}

if (-not $HostFrontend) {
    $HostFrontend = if ($env:FORGESELF_DEV_WEB_FRONTEND) { $env:FORGESELF_DEV_WEB_FRONTEND } else { 'http://localhost:7002' }
}
$HostFrontend = $HostFrontend.TrimEnd('/')

$manifestPath = Join-Path $repoRoot "Plugins/$Plugin/plugin.json"
$pluginId = [string] (([System.IO.File]::ReadAllText($manifestPath, [System.Text.Encoding]::UTF8) | ConvertFrom-Json).Id)
if ([string]::IsNullOrWhiteSpace($pluginId)) { throw "plugin.json 缺少 Id: $manifestPath" }

# ── 生成 dev 配置（不改动插件自身 vite.config.ts）─────────────────────────
$devConfigPath = Join-Path $webDir 'vite.config.dev.mjs'
# 注意：以 ESM 单引号 here-string 写入；五个共享包 alias 必须与宿主 index.html import map 一致
$devConfig = @'
// ⚠ 生成文件（scripts/dev-plugin-web.ps1）：dev-only HMR 配置，勿手工编辑、勿提交。
// 机制见脚本头注释：alias 到宿主 /shared/*.js shim，保住宿主 import map 的单 Vue 语义。
import { defineConfig, mergeConfig } from 'vite'

export default defineConfig(async (env) => {
  const baseMod = await import('./vite.config.ts')
  const base = typeof baseMod.default === 'function' ? await baseMod.default(env) : baseMod.default

  const host = process.env.FORGE_DEV_HOST_FRONTEND || 'http://localhost:7002'
  const alias = {
    vue: host + '/shared/vue.js',
    'vue-router': host + '/shared/vue-router.js',
    pinia: host + '/shared/pinia.js',
    'element-plus': host + '/shared/element-plus.js',
    '@element-plus/icons-vue': host + '/shared/element-plus-icons.js',
  }

  return mergeConfig(base, defineConfig({
    resolve: { alias },
    server: {
      cors: { origin: true, credentials: false },
      strictPort: false,
    },
    // dev server 下不做依赖预打包：共享包已被 alias 到宿主 shim（外部 URL）
    optimizeDeps: {
      exclude: ['vue', 'vue-router', 'pinia', 'element-plus', '@element-plus/icons-vue'],
    },
  }))
})
'@
$devConfig = $devConfig.Replace('FORGE_DEV_HOST_FRONTEND_PLACEHOLDER', 'FORGE_DEV_HOST_FRONTEND')
[System.IO.File]::WriteAllText($devConfigPath, $devConfig, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "[dev-plugin-web] 已生成 dev 配置: $devConfigPath"

# ── 启动 vite serve（前台，Ctrl+C 退出并清理）──────────────────────────────
$env:FORGE_DEV_HOST_FRONTEND = $HostFrontend

$viteArgs = @('exec', 'vite', '--config', 'vite.config.dev.mjs')
if ($Port -gt 0) { $viteArgs += @('--port', "$Port") }

Write-Host "[dev-plugin-web] 启动 $Plugin HMR dev server（宿主前端: $HostFrontend）..."
Write-Host "[dev-plugin-web] 提示：宿主需以 FORGESELF_DEV_WEB_HMR=1 启动才会把 entry 指向本 dev server"

try {
    # 写标记文件（vite 实际端口未知时先记预期值；宿主按此拼 URL）
    # 先启动进程拿不到端口 → 用 PowerShell 后台 job 探测实际端口后再写标记：简化为直接前台运行，
    # 标记文件在 vite 输出 ready 后由宿主按 -Port（或 vite 默认 5173）约定读取。
    $actualPort = if ($Port -gt 0) { $Port } else { 5173 }
    [System.IO.File]::WriteAllText($markerPath,
        (@{ pluginId = $pluginId; plugin = $Plugin; port = $actualPort; url = "http://localhost:$actualPort" } | ConvertTo-Json),
        (New-Object System.Text.UTF8Encoding($false)))

    Push-Location $webDir
    & pnpm @viteArgs
}
finally {
    Pop-Location
    if (Test-Path $markerPath) {
        Remove-Item -LiteralPath $markerPath -Force
        Write-Host "[dev-plugin-web] 已清理 $markerPath"
    }
}
