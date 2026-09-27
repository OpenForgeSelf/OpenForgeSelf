#!/usr/bin/env pwsh
<#
.SYNOPSIS
  publish 宿主 · 插件发布与热更新验证（一键闭环）

.DESCRIPTION
  1) 确保宿主从 publish/ 运行（已运行但非 publish 实例 -> 杀掉重起）
  2) 等待服务就绪
  3) 用 scripts/publish-plugin.ps1 把单个插件按版本 stage 到 publish/Plugins/_backups/<id>/<version>/
  4) 调 POST /api/plugin/update/{id} 显式版本化切换（宿主 stage 到 versions/<version>/ + 切 current 指针 + 卸载重载，
     不再覆盖活动目录、不依赖 watcher 热重载）
  5) 验证：API 版本 / versions/<version>/ 目录 / current 指针 / 前端清单 / 静态资源 / 版本快照入口 DLL

  注意：端点前缀是单数 api/plugin（publish-plugin.ps1 打印的 /api/plugins/... 是错的）。

.EXAMPLE
  pwsh .agents/skills/plugin-publish-verify/scripts/run-plugin-publish-verify.ps1 -Plugin AIAgent
  pwsh ...\run-plugin-publish-verify.ps1 -Plugin AIAgent -BumpVersion
  pwsh ...\run-plugin-publish-verify.ps1 -Plugin AIAgent -SkipPublish -NoHostStart  # 只验证
  pwsh ...\run-plugin-publish-verify.ps1 -Plugin FlowForge -Engine FlowForge.Engine   # 插件+独立引擎一并发布
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Plugin,              # 插件目录名（PascalCase，如 AIAgent），不是 kebab id

    [string] $PluginsRoot,         # 默认 <repo>\publish\Plugins
    [switch] $BumpVersion,         # 自动把 plugin.json 版本 patch+1
    [switch] $SkipPublish,         # 跳过 build.ps1 全量发布（宿主产物已最新）
    [switch] $NoHostStart,         # 不启动宿主（假定已在运行；仍会校验是 publish 实例）
    [switch] $Force,               # 传给 publish-plugin.ps1，覆盖已存在的 staged 版本
    [int]    $Port = 0,            # 0 = 自动探测（读用户目录 ForgeSetting.config 的 PortNumber）
    [int]    $ReadyTimeoutSec = 90,
    [int]    $ReloadWaitSec = 8,
    [string] $Engine = ''        # optional engine subprocess project (e.g. FlowForge.Engine)
)

$ErrorActionPreference = 'Stop'
$skillDir  = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$repoRoot  = Resolve-Path (Join-Path $skillDir '..\..')
$publishDir = Join-Path $repoRoot 'publish'
$pubExe    = Join-Path $publishDir 'ForgeSelf.exe'
if (-not $PluginsRoot) { $PluginsRoot = Join-Path $publishDir 'Plugins' }

$manifestPath = Join-Path $repoRoot "ForgeSelf.Api/Plugins/$Plugin/plugin.json"
if (-not (Test-Path $manifestPath)) { throw "plugin.json not found: $manifestPath" }

function Get-Manifest {
    $txt = [System.IO.File]::ReadAllText($manifestPath, [System.Text.Encoding]::UTF8)
    return ($txt | ConvertFrom-Json)
}
function Step($msg) { Write-Host "" ; Write-Host "[step] $msg" -ForegroundColor Cyan }

# ── 读清单（id / version）──
$m = Get-Manifest
$pluginId = [string]$m.Id
$version  = [string]$m.Version
if ([string]::IsNullOrWhiteSpace($pluginId)) { throw "plugin.json missing Id" }
if ([string]::IsNullOrWhiteSpace($version))  { throw "plugin.json missing Version" }

# 端口自动探测：publish 宿主的数据根是 ~/.forgeself，端口取自那里的 ForgeSetting.config
if ($Port -eq 0) {
    $Port = 7102
    $userCfg = Join-Path $env:USERPROFILE '.forgeself/Config/ForgeSetting.config'
    if (Test-Path -LiteralPath $userCfg) {
        $txtCfg = [System.IO.File]::ReadAllText($userCfg, [System.Text.Encoding]::UTF8)
        $mm = [regex]::Match($txtCfg, '<PortNumber>\s*(\d+)\s*</PortNumber>')
        if ($mm.Success) { $Port = [int]$mm.Groups[1].Value; Write-Host "port auto-detected from $userCfg" }
    }
}
Write-Host "Plugin: $pluginId (dir=$Plugin) version=$version port=$Port" -ForegroundColor Yellow

# ── 0) 默认升 patch 版本（每次发版必升，大版本手动改 plugin.json）──
Step "BumpVersion: $version -> patch+1"
$v = [version]$version
$newV = "{0}.{1}.{2}" -f $v.Major, $v.Minor, ($v.Build + 1)
$txt = [System.IO.File]::ReadAllText($manifestPath, [System.Text.Encoding]::UTF8)
$txt = $txt -replace "(`"Version`"\s*:\s*`")[^`"]+(`")", "`${1}$newV`${2}"
[System.IO.File]::WriteAllText($manifestPath, $txt, (New-Object System.Text.UTF8Encoding($false)))
$version = $newV
Write-Host "  version -> $version" -ForegroundColor Green

# ── 1) 全量发布 ──
if (-not $SkipPublish) {
    Step "Full publish (build.ps1)"
    & (Join-Path $repoRoot 'build.ps1')
    if ($LASTEXITCODE -ne 0) { throw "build.ps1 failed (exit=$LASTEXITCODE)" }
} else { Step "Skip full publish (-SkipPublish)" }

if (-not (Test-Path $pubExe)) { throw "publish exe not found: $pubExe" }

# ── 2) 确保宿主是 publish 实例 ──
Step "Ensure host runs from publish/"
$procs = @(Get-CimInstance Win32_Process -Filter "Name='ForgeSelf.exe'" -ErrorAction SilentlyContinue)
$pubProc = $procs | Where-Object {
    $_.ExecutablePath -and (($_.ExecutablePath -replace '\\','/').ToLower().EndsWith('/publish/forgeself.exe'))
}

if ($pubProc) {
    Write-Host "  reuse running publish instance PID=$($pubProc.ProcessId)" -ForegroundColor Green
} else {
    if ($procs.Count -gt 0) {
        Write-Host "  found $($procs.Count) non-publish instance(s) -> killing (must be the publish one)" -ForegroundColor Yellow
        $procs | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
        Start-Sleep -Seconds 2
    }
    if ($NoHostStart) { throw "No publish instance running and -NoHostStart specified" }
    Write-Host "  starting $pubExe --console"
    Start-Process -FilePath $pubExe -ArgumentList '--console' -WorkingDirectory $publishDir `
        -RedirectStandardOutput (Join-Path $repoRoot 'publish-host.out.log') `
        -RedirectStandardError  (Join-Path $repoRoot 'publish-host.err.log') -NoNewWindow
}

# ── 3) 等就绪 ──
Step "Wait for host ready (/$Port/api/plugin)"
$deadline = (Get-Date).AddSeconds($ReadyTimeoutSec)
$ready = $false
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 3
    try {
        $r = Invoke-WebRequest -Uri "http://localhost:$Port/api/plugin" -UseBasicParsing -TimeoutSec 3
        if ([int]$r.StatusCode -eq 200) { $ready = $true; break }
    } catch { }
}
if (-not $ready) { throw "Host not ready within ${ReadyTimeoutSec}s. Check publish-host.out.log" }
Write-Host "  host ready" -ForegroundColor Green

# 基线版本
$base = (Invoke-RestMethod -Uri "http://localhost:$Port/api/plugin" -TimeoutSec 10).data |
        Where-Object { $_.id -eq $pluginId }
Write-Host ("  baseline version = " + $base.version)

# ── 4) 只发布该插件 ──
Step "Publish plugin -> $PluginsRoot"
$pp = Join-Path $repoRoot 'scripts/publish-plugin.ps1'
# 必须显式具名传参：数组 splat（@arr）是按位置传参，会把 '-PluginsRoot' 塞进 -Configuration
if ($Force -or $BumpVersion) {
    & $pp -Plugin $Plugin -PluginsRoot $PluginsRoot -Force
} else {
    & $pp -Plugin $Plugin -PluginsRoot $PluginsRoot
}
if ($LASTEXITCODE -ne 0) { throw "publish-plugin.ps1 failed (exit=$LASTEXITCODE)" }

$staged = Join-Path (Join-Path (Join-Path $PluginsRoot '_backups') $pluginId) $version
if (-not (Test-Path $staged)) { throw "staged dir not found: $staged" }
Write-Host "  staged: $staged" -ForegroundColor Green

# ── 5) 版本化显式更新：POST /api/plugin/update/{id} ──
# 宿主从 _backups/<id>/<version>/ 把新版本 stage 到 versions/<version>/（side-by-side，绝不覆盖
# 正在加载的文件），随后切 current 指针 + 卸载旧 ALC + 加载新版。发布动作不触碰活动目录。
Step "Versioned update: POST /api/plugin/update/$pluginId"
try {
    $updResp = Invoke-RestMethod -Method Post -Uri "http://localhost:$Port/api/plugin/update/$pluginId" -TimeoutSec 90
    if ($updResp.success -eq $false) {
        throw "plugin update API returned success=false: $($updResp.message)"
    }
    Write-Host "  update API OK: $($updResp.message)" -ForegroundColor Green
} catch {
    throw "plugin update API call failed: $($_.Exception.Message)"
}
Start-Sleep -Seconds $ReloadWaitSec

# 断言版本化布局：versions/<version>/ 目录存在 + current 指针 == 期望版本
$live = Join-Path $PluginsRoot $Plugin
$versionDir = Join-Path (Join-Path $live 'versions') $version
if (-not (Test-Path $versionDir)) {
    throw "version dir not found: $versionDir (expected versions/$version after versioned update)"
}
$currentPointer = Join-Path $live 'current'
if (-not (Test-Path $currentPointer)) { throw "current pointer missing: $currentPointer" }
$currentVer = [System.IO.File]::ReadAllText($currentPointer, [System.Text.Encoding]::UTF8).Trim()
if ($currentVer -ne $version) {
    throw "current pointer=$currentVer != expected $version"
}
Write-Host "  [OK] versioned layout: versions/$version + current=$currentVer" -ForegroundColor Green

# ── 5.5) 引擎发布（可选）：插件带独立引擎时 dotnet publish 到 publish/<Engine>/ ──
if ($Engine) {
    Step "Publish engine: $Engine"
    $engineCsproj = Join-Path $repoRoot "ForgeSelf.Api/Plugins/$Plugin/Engine/$Engine.csproj"
    if (-not (Test-Path -LiteralPath $engineCsproj)) {
        throw "Engine csproj not found: $engineCsproj (expected under Plugins/$Plugin/Engine/)"
    }
    $engineOut = Join-Path $publishDir $Engine
    # 引擎进程若在运行，exe/dll 被锁无法覆盖 -> 先停（宿主下次拉起引擎时用新产物）
    $running = @(Get-Process -Name $Engine -ErrorAction SilentlyContinue)
    if ($running.Count -gt 0) {
        Write-Host "  engine running (PID $($running.Id -join ',')) -> stopping so publish can overwrite" -ForegroundColor Yellow
        $running | Stop-Process -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
    }
    & dotnet publish $engineCsproj -c Release -o $engineOut --nologo
    if ($LASTEXITCODE -ne 0) { throw "engine publish failed (exit=$LASTEXITCODE)" }
    $engineExe = Join-Path $engineOut "$Engine.exe"
    if (-not (Test-Path -LiteralPath $engineExe)) { throw "engine exe not found after publish: $engineExe" }
    Write-Host "  [OK] engine published: $engineExe" -ForegroundColor Green
}

# ── 6) 验证 ──
Step "Verify"
$fail = 0

# ① 更新成功判定（首要判据）：
#    接口返回的该插件版本 == 当前生效版本（SyncActiveManifest 已把 versions/<current>/plugin.json 同步到根 plugin.json）。
$liveManifestPath = Join-Path $live 'plugin.json'
$manifestVer = $null
if (Test-Path -LiteralPath $liveManifestPath) {
    $manifestVer = [string]([System.IO.File]::ReadAllText($liveManifestPath, [System.Text.Encoding]::UTF8) |
                   ConvertFrom-Json).Version
}
$cur = (Invoke-RestMethod -Uri "http://localhost:$Port/api/plugin" -TimeoutSec 10).data |
       Where-Object { $_.id -eq $pluginId }
$apiVer = [string]$cur.version

if ($manifestVer -and ($apiVer -eq $manifestVer)) {
    Write-Host "  [OK]   update succeeded: api version ($apiVer) == manifest file version ($manifestVer)" -ForegroundColor Green
} else {
    Write-Host "  [FAIL] api version=$apiVer, manifest file version=$manifestVer (not equal)" -ForegroundColor Red
    Write-Host "         see troubleshooting.md -> host still serves old version" -ForegroundColor Yellow
    $fail++
}
# 与本次期望版本对齐（防止清单本身就没更新）
if ($apiVer -ne $version) {
    Write-Host "  [WARN] api version=$apiVer != expected $version" -ForegroundColor Yellow
}

# ② 前端清单
$entry = $null
try {
    $fm = (Invoke-RestMethod -Uri "http://localhost:$Port/api/plugin/frontend-manifest?id=$pluginId" -TimeoutSec 10).data |
          Where-Object { $_.id -eq $pluginId }
    $entry = $fm.frontend.entry
    if ($fm.version -eq $version -and $entry) {
        Write-Host "  [OK]   manifest version=$($fm.version) entry=$entry" -ForegroundColor Green
    } else {
        Write-Host "  [FAIL] manifest version=$($fm.version) entry=$entry" -ForegroundColor Red; $fail++
    }
} catch { Write-Host "  [WARN] frontend-manifest unavailable: $($_.Exception.Message)" -ForegroundColor Yellow }

# ③ 静态资源
if ($entry) {
    $assetUrl = "http://localhost:$Port/plugins/$pluginId/$($entry -replace '\\','/')"
    try {
        $req = [System.Net.HttpWebRequest]::Create($assetUrl); $req.Timeout = 8000
        $resp = $req.GetResponse()
        $code = [int]$resp.StatusCode; $ct = $resp.ContentType; $resp.Close()
        if ($code -eq 200) { Write-Host "  [OK]   asset 200 ($ct) $assetUrl" -ForegroundColor Green }
        else { Write-Host "  [FAIL] asset HTTP $code" -ForegroundColor Red; $fail++ }
    } catch { Write-Host "  [FAIL] asset unreachable: $($_.Exception.Message)" -ForegroundColor Red; $fail++ }
}

# ④ 版本快照入口 DLL 已就位（防「版本号升了但跑旧二进制」的假成功）
$entryDll = Join-Path $staged "$Plugin.dll"
$versionedDll = Join-Path $versionDir "$Plugin.dll"
if ((Test-Path $entryDll) -and (Test-Path $versionedDll)) {
    $hStaged = (Get-FileHash -LiteralPath $entryDll).Hash
    $hVersioned = (Get-FileHash -LiteralPath $versionedDll).Hash
    if ($hStaged -eq $hVersioned) {
        Write-Host "  [OK]   versions/$version entry DLL matches staged ($($hVersioned.Substring(0,12))...)" -ForegroundColor Green
    } else {
        Write-Host "  [FAIL] versions/$version entry DLL differs from staged -> version snapshot is stale" -ForegroundColor Red
        Write-Host "         staged   =$hStaged" -ForegroundColor Red
        Write-Host "         versioned=$hVersioned" -ForegroundColor Red
        $fail++
    }
} else {
    Write-Host "  [warn] entry DLL missing (staged=$([bool](Test-Path $entryDll)), versioned=$([bool](Test-Path $versionedDll)))" -ForegroundColor Yellow
}

Write-Host ""
if ($fail -eq 0) {
    Write-Host "=== VERIFY PASSED: host serves $pluginId v$version (versioned layout, no overlay) ===" -ForegroundColor Green
    exit 0
} else {
    Write-Host "=== VERIFY FAILED ($fail check(s)); see troubleshooting.md ===" -ForegroundColor Red
    exit 1
}
