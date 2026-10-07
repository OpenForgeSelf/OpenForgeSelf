#!/usr/bin/env pwsh
<#
.SYNOPSIS
  一键起「宿主后端 + 宿主前端」开发栈（dev-stack）。被占用自动换端口。
.DESCRIPTION
  本脚本是**唯一**允许启动本地前后端的方式（见 plugin-development 铁律 20：禁止手敲
  dotnet / vite 运行命令）。手工启动有两个已实证的静默陷阱，脚本已内置规避：

  1) dev 宿主首参必须是 --console（Program.cs:104）。
     Program.Main 只看 args[0] 分支，其余首参会落到 Program.cs:146 的兜底分支
     `new WindowsService().Main(args)` —— NewLife.Agent 把它当命令解析，
     日志只留 ProcessCommand / ProcessFinished 就 exit 0，无 error、无端口，探活全 000。

  2) vite dev 的 optimizeDeps 会让 esbuild（Go 二进制）写 `node_modules/.vite/deps_temp_*`，
     在沙箱里报「Failed to write to output file: ... Access is denied」并崩掉 dev server。
     这与目录权限无关（实测 os.tmpdir() 同样失败），而同目录 Node fs 写是成功的。
     规避：探测 esbuild 写盘能力（probe-esbuild-write.mjs），不可写时生成的配置里
     关闭依赖预打包（optimizeDeps.noDiscovery）—— 实测裸导入 vue/pinia/element-plus
     仍能被正确重写到 .pnpm 下的 ESM 文件，功能不受影响。

  端口被占用自动顺延（+1 重试，最多 50 次），实际端口写进状态文件，不会与既有实例打架。
  宿主以 FORGESelf_INSTANCE_ID 隔离，绕开全局 Mutex，不影响用户正在运行的宿主实例。

.EXAMPLE
  pwsh scripts/dev-stack.ps1                    # 起后端 7301 + 前端 7399（被占则顺延）
  pwsh scripts/dev-stack.ps1 -SkipBuild         # 跳过 dotnet build（产物已是最新时）
  pwsh scripts/dev-stack.ps1 -Stop              # 停止并清理状态文件与生成配置
#>
param(
  [int]$BackendPort = 7301,
  [int]$FrontendPort = 7399,
  [string]$PluginsDir,          # 缺省 <repo>/Plugins（dev 模式加载源码插件）
  [string]$DataRoot,            # 缺省 <repo>/.temp/dev-stack/data（隔离数据根）
  [switch]$SkipBuild,
  [switch]$Stop
)

$ErrorActionPreference = 'Stop'

$repo     = Split-Path -Parent $PSScriptRoot
$web      = Join-Path $repo 'ForgeSelf.Web'
$binDir   = Join-Path $repo 'ForgeSelf.Api/bin/Debug/net10.0-windows'
$hostDll  = Join-Path $binDir 'ForgeSelf.dll'
$stateDir = Join-Path $repo '.temp'
$statePath = Join-Path $stateDir 'dev-stack.json'
$devConfig = Join-Path $web '.temp/vite.dev-host.mjs'
$backendLog = Join-Path $stateDir 'dev-stack-backend.log'
$frontendLog = Join-Path $stateDir 'dev-stack-frontend.log'

if (-not $PluginsDir) { $PluginsDir = Join-Path $repo 'Plugins' }
if (-not $DataRoot)   { $DataRoot   = Join-Path $stateDir 'dev-stack/data' }

# ─────────────────────────────────────────────────────────────────────────────
# 工具函数
# ─────────────────────────────────────────────────────────────────────────────
function Test-PortFree {
    param([int]$Port)
    $l = $null
    try {
        $l = New-Object System.Net.Sockets.TcpListener([System.Net.IPAddress]::Loopback, $Port)
        $l.Start()
        return $true
    } catch { return $false }
    finally { if ($null -ne $l) { $l.Stop() } }
}

function Get-FreePort {
    param([int]$Start, [string]$Label)
    $p = $Start
    for ($i = 0; $i -lt 50; $i++) {
        if (Test-PortFree -Port $p) {
            if ($p -ne $Start) { Write-Host "[dev-stack] $Label 端口 $Start 被占用，顺延到 $p" -ForegroundColor Yellow }
            return $p
        }
        $p++
    }
    throw "$Label 端口 $Start 起连续 50 个端口均被占用"
}

function Wait-Http {
    param([string]$Url, [int]$TimeoutSec)
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    while ($sw.Elapsed.TotalSeconds -lt $TimeoutSec) {
        try {
            $req = [System.Net.HttpWebRequest]::Create($Url)
            $req.Timeout = 3000
            $resp = $req.GetResponse()
            $code = [int]$resp.StatusCode
            $resp.Close()
            if ($code -lt 500) { return $code }
        } catch { }
        Start-Sleep -Seconds 1
    }
    return 0
}

function Stop-DevStack {
    if (-not (Test-Path $statePath)) { Write-Host "[dev-stack] 无状态文件，无需停止"; return }
    $st = Get-Content $statePath -Raw | ConvertFrom-Json
    foreach ($pidName in @('backendPid', 'frontendPid')) {
        $procId = $st.$pidName
        if ($procId) {
            $p = Get-Process -Id $procId -ErrorAction SilentlyContinue
            if ($p) {
                Write-Host "[dev-stack] 停止进程 $($p.ProcessName) (PID $procId)"
                Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue
            }
        }
    }
    Remove-Item -LiteralPath $statePath -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $devConfig -Force -ErrorAction SilentlyContinue
    Write-Host "[dev-stack] 已停止并清理状态文件"
}

# ─────────────────────────────────────────────────────────────────────────────
# -Stop：清理即退出
# ─────────────────────────────────────────────────────────────────────────────
if ($Stop) { Stop-DevStack; return }

New-Item -ItemType Directory -Force -Path $stateDir | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $web '.temp') | Out-Null
New-Item -ItemType Directory -Force -Path $DataRoot | Out-Null

# 幂等：已有存活实例先停掉，避免同一个数据根被两个宿主抢
if (Test-Path $statePath) {
    Write-Host "[dev-stack] 检测到上次实例，先停止" -ForegroundColor Yellow
    Stop-DevStack
}

# 1) 端口：被占用自动顺延
$bePort = Get-FreePort -Start $BackendPort -Label '后端'
$fePort = Get-FreePort -Start $FrontendPort -Label '前端'

# 2) 构建宿主后端（产物缺失或显式要求）
if (-not $SkipBuild) {
    Write-Host "[dev-stack] dotnet build ForgeSelf.Api (Debug)..."
    & dotnet build (Join-Path $repo 'ForgeSelf.Api/ForgeSelf.Api.csproj') -c Debug --nologo -v minimal
    if ($LASTEXITCODE -ne 0) { throw "dotnet build 失败（exit $LASTEXITCODE）" }
}
if (-not (Test-Path $hostDll)) { throw "宿主产物不存在: $hostDll（先跑一次不带 -SkipBuild 的启动）" }

# 3) 探测 esbuild 写盘能力 → 决定 vite dev 是否关闭依赖预打包
$probeJson = & node (Join-Path $PSScriptRoot 'probe-esbuild-write.mjs') $web | Out-String
$canWrite = $false
try {
    $probe = $probeJson | ConvertFrom-Json
    $canWrite = ($probe.canWrite -eq $true)
    Write-Host "[dev-stack] esbuild 写盘探测: canWrite=$canWrite (v$($probe.esbuildVersion))"
} catch {
    Write-Host "[dev-stack] esbuild 写盘探测失败，按不可写处理" -ForegroundColor Yellow
}
if (-not $canWrite) {
    Write-Host "[dev-stack] → 关闭依赖预打包（noDiscovery），绕开 esbuild 写盘" -ForegroundColor Yellow
}

$noDiscovery = if ($canWrite) { 'false' } else { 'true' }
$configBody = @"
// ⚠ 生成文件（scripts/dev-stack.ps1）：dev-only，勿手工编辑、勿提交。
// 唯一目的：按 esbuild 能否写盘决定是否关闭依赖预打包（optimizeDeps.noDiscovery）。
// 其余一律沿用宿主 vite.config.ts（proxy 经 VITE_APP_BASE_API 注入，端口经 --port 指定）。
import { defineConfig, mergeConfig } from 'vite'

export default defineConfig(async (env) => {
  const baseMod = await import('../vite.config.ts')
  const base = typeof baseMod.default === 'function' ? await baseMod.default(env) : baseMod.default
  return mergeConfig(base, defineConfig({
    optimizeDeps: { noDiscovery: $noDiscovery, include: [] },
  }))
})
"@
[System.IO.File]::WriteAllText($devConfig, $configBody, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "[dev-stack] 已生成 dev 配置: $devConfig"

# 4) 起宿主后端（--console 必须打头）
$env:FORGESELF_DEV_MODE      = '1'
$env:FORGESelf_INSTANCE_ID   = 'dev-stack'
$env:FORGESELF_PORT          = "$bePort"
$env:FORGESELF_DATA_ROOT     = $DataRoot
$env:FORGESELF_NO_TRAY       = '1'
$env:FORGESELF_PLUGINS_DIR   = $PluginsDir

Write-Host "[dev-stack] 起宿主后端 :$bePort（--console 打头，实例 dev-stack）"
$beProc = Start-Process -FilePath 'dotnet' `
    -ArgumentList @($hostDll, '--console', '--plugins-dir', $PluginsDir) `
    -WorkingDirectory $binDir -WindowStyle Hidden -PassThru `
    -RedirectStandardOutput $backendLog -RedirectStandardError "$backendLog.err"

# 5) 起宿主前端 dev server（直连 vite bin，避免 npx 的 cmd 包装导致 PID 不精确）
$env:VITE_APP_BASE_API = "http://localhost:$bePort"
$viteBin = Join-Path $web 'node_modules/vite/bin/vite.js'
if (-not (Test-Path $viteBin)) { throw "vite bin 不存在: $viteBin（前端依赖未安装？）" }

Write-Host "[dev-stack] 起宿主前端 :$fePort（代理 /api → :$bePort）"
$feProc = Start-Process -FilePath 'node' `
    -ArgumentList @($viteBin, '--config', '.temp/vite.dev-host.mjs', '--port', "$fePort", '--strictPort') `
    -WorkingDirectory $web -WindowStyle Hidden -PassThru `
    -RedirectStandardOutput $frontendLog -RedirectStandardError "$frontendLog.err"

# 6) 探活
$beCode = Wait-Http -Url "http://localhost:$bePort/api/health" -TimeoutSec 180
if ($beCode -eq 0) {
    Write-Host "[dev-stack] 后端未就绪，日志: $backendLog" -ForegroundColor Red
} else { Write-Host "[dev-stack] 后端就绪 ($beCode)" -ForegroundColor Green }

$feCode = Wait-Http -Url "http://localhost:$fePort/" -TimeoutSec 120
if ($feCode -eq 0) {
    Write-Host "[dev-stack] 前端未就绪，日志: $frontendLog" -ForegroundColor Red
} else { Write-Host "[dev-stack] 前端就绪 ($feCode)" -ForegroundColor Green }

# 7) 取令牌（写入状态文件，供走查/e2e 复用）
$token = ''
$cfgPath = Join-Path $DataRoot 'config/ForgeSetting.config'
if (Test-Path $cfgPath) {
    try { $token = ((& node (Join-Path $PSScriptRoot 'get-forge-token.cjs') --config $cfgPath | Out-String).Trim()) } catch { }
}

$state = [ordered]@{
    backendUrl   = "http://localhost:$bePort"
    frontendUrl  = "http://localhost:$fePort"
    backendPort  = $bePort
    frontendPort = $fePort
    backendPid   = $beProc.Id
    frontendPid  = $feProc.Id
    dataRoot     = $DataRoot
    pluginsDir   = $PluginsDir
    token        = $token
    backendLog   = $backendLog
    frontendLog  = $frontendLog
    startedAt    = (Get-Date).ToString('o')
}
$state | ConvertTo-Json | Set-Content -LiteralPath $statePath -Encoding UTF8

Write-Host ""
Write-Host "[dev-stack] ============ 就绪 ============" -ForegroundColor Cyan
Write-Host "  后端  $($state.backendUrl)   (PID $($beProc.Id))"
Write-Host "  前端  $($state.frontendUrl)  (PID $($feProc.Id))"
Write-Host "  数据根 $DataRoot"
Write-Host "  插件源 $PluginsDir"
Write-Host "  状态  $statePath"
if ($token) { Write-Host "  令牌  $token" }
Write-Host "  停止  pwsh scripts/dev-stack.ps1 -Stop"
Write-Host "========================================" -ForegroundColor Cyan

if ($beCode -eq 0 -or $feCode -eq 0) { exit 1 }
# 显式 exit 0：后端/前端是长驻子进程，某些调用方（后台任务封装）会把子进程句柄的退出码
# 当成脚本退出码，导致「明明起成功了却报 exit 1」。
exit 0
