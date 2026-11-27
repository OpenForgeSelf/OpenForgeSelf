# build-frontend.ps1 - builds the host SPA and every plugin web UI.
# Outputs (all git-ignored build artifacts):
#   ForgeSelf.Web            -> ForgeSelf.Api/wwwroot            (vite outDir)
#   Plugins/<X>/web          -> Plugins/<X>/web/dist             (picked up by StageAllPlugins)
# Usable locally and in CI. Requires: node >= 20, pnpm on PATH (or corepack).
#
# Examples:
#   ./build-frontend.ps1                    # host web + all plugin webs
#   ./build-frontend.ps1 -HostOnly          # host web only
#   ./build-frontend.ps1 -PluginsOnly -Plugin AIAgent   # one plugin web only (fast local debug)

[CmdletBinding()]
param(
    [string]$RepoRoot = '',
    [switch]$HostOnly,
    [switch]$PluginsOnly,
    [string]$Plugin = ''   # filter plugin dir name, e.g. AIAgent
)

. (Join-Path $PSScriptRoot 'release-lib.ps1')
if (-not $RepoRoot) { $RepoRoot = Get-ReleaseRepoRoot }

# 构建临时/缓存目录指向项目内 .temp，避开沙箱 safe-delete shim 对系统 TEMP（AppData\Local\Temp）
# 删除的拦截——esbuild/vite 构建中会清理临时文件，落到系统 TEMP 会报 Access is denied（2026-10-06 实证）。
# .temp 已被 .gitignore / check-git-content 排除，不会入库。
$buildTemp = Join-Path $RepoRoot '.temp'
New-Item -ItemType Directory -Force -Path $buildTemp | Out-Null
$env:TEMP   = $buildTemp
$env:TMP    = $buildTemp
$env:TMPDIR = $buildTemp

$pnpm = Get-ReleaseToolPath 'pnpm'
if (-not $pnpm) { throw 'pnpm not found on PATH. Install pnpm (corepack enable) first.' }

# ---- 版本感知增量构建（2026-11-27 输入3 用户拍板）----
# - 以 package.json version 为构建版本标识：宿主紧跟 ForgeSelf.Api.csproj 的 VersionPrefix 三段
#   （手动同步修改，不带时间码）；插件与各自 plugin.json 版本纪律同步（手动维护）。
# - manifest: artifacts/frontend-build-versions.json（artifacts/ 已 gitignore，不入库），
#   完整构建成功后记录 { "<key>": { "version": "...", "builtAt": "..." } }。
# - 跳过条件：manifest 记录版本 == 当前 package.json version 且构建产物存在
#   （宿主=ForgeSelf.Api/wwwroot/index.html，插件=dist/index.js）；否则重建并回写 manifest。
# - manifest 缺失/损坏按「无记录」处理走全量重建（安全侧）；改码未 bump 版本不在本机制防护范围
#   （用户明确选择人工版本纪律，弃用源码指纹方案）。
$manifestPath = Join-Path (Join-Path $RepoRoot 'artifacts') 'frontend-build-versions.json'

function Read-BuildManifest {
    # 读 manifest；缺失/损坏返回 $null（调用方按无记录处理，不静默造数据）
    if (Test-Path -LiteralPath $manifestPath) {
        try { return (Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json) } catch { return $null }
    }
    return $null
}

function Save-BuildManifest {
    param([object]$Manifest)
    New-Item -ItemType Directory -Force -Path (Split-Path $manifestPath) | Out-Null
    $Manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $manifestPath -Encoding utf8
}

function Update-BuildManifestEntry {
    param([string]$Key, [string]$Version)
    $m = Read-BuildManifest
    if (-not $m) { $m = [PSCustomObject]@{} }
    $entry = [PSCustomObject]@{ version = $Version; builtAt = (Get-Date -Format 'yyyy-MM-dd HH:mm:ss') }
    if ($m.PSObject.Properties[$Key]) { $m.PSObject.Properties[$Key].Value = $entry }
    else { $m | Add-Member -NotePropertyName $Key -NotePropertyValue $entry }
    Save-BuildManifest $m
}

function Test-BuildUpToDate {
    # 跳过判断：产物存在 + manifest 记录版本 == 当前 package.json version
    param([string]$Key, [string]$Version, [string]$Artifact)
    if (-not (Test-Path -LiteralPath $Artifact)) { return $false }
    $m = Read-BuildManifest
    if (-not $m -or -not $m.PSObject.Properties[$Key]) { return $false }
    return ($m.$Key.version -eq $Version)
}

function Get-PkgVersion {
    param([string]$Dir)
    $pkg = Join-Path $Dir 'package.json'
    if (Test-Path -LiteralPath $pkg) { return (Get-Content -LiteralPath $pkg -Raw | ConvertFrom-Json).version }
    return ''
}

function Invoke-PnpmBuild([string]$Dir, [string]$Label, [string]$Key = '', [string]$Artifact = '') {
    # 版本一致且产物在 → 跳过复用（2026-11-27 输入3）
    $pkgVersion = Get-PkgVersion -Dir $Dir
    if ($Key -and $Artifact -and (Test-BuildUpToDate -Key $Key -Version $pkgVersion -Artifact $Artifact)) {
        Write-Host ("[skip] {0}: package.json version {1} unchanged, reusing artifact {2}" -f $Label, $pkgVersion, $Artifact)
        return
    }
    Write-Host "--- $Label : $Dir"
    Push-Location $Dir
    try {
        # 非 TTY 兼容（2026-11-24 输入34/35）：pnpm 检测到 node_modules 需重建时会弹
        # "Proceed? (Y/n)" 交互确认；智能终端/CI 无 TTY 时直接 ERR_PNPM_ABORTED_REMOVE_MODULES_DIR_NO_TTY。
        # 显式关闭该确认 = 等价于人工按 Y，发布场景本就要干净重装，不影响 --frozen-lockfile。
        & $pnpm install --frozen-lockfile --config.confirmModulesPurge=false
        Assert-ExitCode "pnpm install ($Label)"
        # Env workaround: the sandbox safe-delete shim blocks esbuild's Go binary from
        # deleting its temp files (inputs >1MB / outputs >buffer), aborting the build
        # with "remove <path>: Access is denied". patch-esbuild.mjs keeps inputs inline;
        # ESBUILD_MAX_BUFFER keeps outputs inline. Both are valid in every environment,
        # so applying them unconditionally is safe.
        & node (Join-Path $PSScriptRoot 'patch-esbuild.mjs') $Dir
        $env:ESBUILD_MAX_BUFFER = '1073741824'
        & $pnpm build
        Assert-ExitCode "pnpm build ($Label)"
        # 构建成功才回写版本记录（Assert-ExitCode 保证走到这里的 build 都已成功）
        if ($Key) { Update-BuildManifestEntry -Key $Key -Version $pkgVersion }
    }
    finally { Pop-Location }
}

# 宿主版本一致性校验（2026-11-27 输入3 约定：web package.json version 紧跟 csproj VersionPrefix 三段，
# 需要重打宿主前端时手动同步修改 api 与 web 两处版本；不一致仅警告不阻断，提示人工同步）
$apiVersion = Get-CsprojReleaseVersion -RepoRoot $RepoRoot
$hostVersion = Get-PkgVersion -Dir (Join-Path $RepoRoot 'ForgeSelf.Web')
if ($hostVersion -ne $apiVersion) {
    Write-Host ("build-frontend: WARNING ForgeSelf.Web package.json version ({0}) != ForgeSelf.Api.csproj VersionPrefix ({1}); bump both together when a frontend rebuild is needed" -f $hostVersion, $apiVersion) -ForegroundColor Yellow
}

if (-not $PluginsOnly) {
    Invoke-ReleaseStep 'frontend: host web (ForgeSelf.Web)' {
        Invoke-PnpmBuild (Join-Path $RepoRoot 'ForgeSelf.Web') 'host-web' `
            -Key 'ForgeSelf.Web' -Artifact (Join-Path $RepoRoot 'ForgeSelf.Api/wwwroot/index.html')
    }
}

if (-not $HostOnly) {
    Invoke-ReleaseStep 'frontend: plugin webs' {
        $webDirs = Get-ChildItem (Join-Path $RepoRoot 'Plugins') -Directory |
            ForEach-Object { Join-Path $_.FullName 'web' } |
            Where-Object { Test-Path (Join-Path $_ 'package.json') }
        if ($Plugin) {
            $webDirs = $webDirs | Where-Object { (Split-Path (Split-Path $_ -Parent) -Leaf) -eq $Plugin }
            if (-not $webDirs) { throw "plugin web not found: Plugins/$Plugin/web" }
        }
        foreach ($d in $webDirs) {
            $name = Split-Path (Split-Path $d -Parent) -Leaf
            Invoke-PnpmBuild $d "plugin-web/$name" -Key $name -Artifact (Join-Path $d 'dist/index.js')
        }
    }
}

Write-Host 'build-frontend: all done.'
