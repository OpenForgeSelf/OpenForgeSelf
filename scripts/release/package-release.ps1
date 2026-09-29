# package-release.ps1 - turns publish folders into a QQNT-layout distributable zip.
# QQNT layout (2026-09-28 批次2.1/输入34 + 输入36 single-file; 目录命名统一小写 2026-09-29 输入37):
#   install root (common, unchanged across versions):
#     ForgeSelf.exe (FDD single-file launcher) + .NET runtime DOTNET_ROOT structure
#       (host/fxr + shared frameworks, from BootDir/bootstrapper publish)
#     update-agent.ps1
#     plugins/                      (side-by-side with versions/, multi-version coexistence)
#   versions/<ver>/                  (business layer, FDD single-file, per release)
#     ForgeSelf.exe (single-file: managed assemblies + satellites embedded)
#     wwwroot + appsettings.json + SQLite native libs (externally extracted)
#   versions/current                 (pointer file)
# The SQLite probe libs are NOT NuGet deps: NewLife.XCode probes System.Data.SQLite.dll /
# e_sqlite3.dll at runtime from the plugins folder, so every clean build must inject them.
#
# Example:
#   ./package-release.ps1 -Version 0.1.0

[CmdletBinding()]
param(
    [string]$RepoRoot = '',
    [string]$Version = '0.0.0-local',
    [string]$PublishDir = '',
    [string]$BootDir = '',
    [string]$OutputDir = '',
    [string]$LayoutDir = '',
    [switch]$IncludePdb,
    [switch]$Sign
)

. (Join-Path $PSScriptRoot 'release-lib.ps1')
if (-not $RepoRoot) { $RepoRoot = Get-ReleaseRepoRoot }
if (-not $PublishDir) { $PublishDir = Join-Path $RepoRoot 'artifacts/publish' }
if (-not $BootDir) { $BootDir = Join-Path $RepoRoot 'artifacts/layout-root' }
if (-not $OutputDir) { $OutputDir = Join-Path $RepoRoot 'artifacts/release' }
if (-not $LayoutDir) { $LayoutDir = Join-Path $RepoRoot 'artifacts/layout' }

$ver = Get-NormalizedVersion $Version
if (-not (Test-Path (Join-Path $PublishDir 'ForgeSelf.exe'))) { throw "not a publish dir (business layer single-file): $PublishDir" }
if (-not (Test-Path (Join-Path $BootDir 'ForgeSelf.exe'))) { throw "not a bootstrapper dir (common layer): $BootDir" }
# 业务层版本目录（脚本主体级，供各 Invoke-ReleaseStep 块共用；块内赋值跨块不可见——StrictMode 实测坑）
$versionDir = Join-Path (Join-Path $LayoutDir 'versions') $ver

# ---- 组装 QQNT 布局 ----
Invoke-ReleaseStep 'package: assemble QQNT layout' {
    if (Test-Path $LayoutDir) { Remove-Item $LayoutDir -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $LayoutDir | Out-Null

    # 1) 公共层（根）：启动器 + 运行时 + 框架（跨版本共享，只存一份）
    Copy-Item (Join-Path $BootDir '*') $LayoutDir -Recurse -Force

    # 2) 业务层：versions/<ver>/（$versionDir 已在脚本主体定义）
    New-Item -ItemType Directory -Force -Path $versionDir | Out-Null
    Copy-Item (Join-Path $PublishDir '*') $versionDir -Recurse -Force

    # 3) 内置插件初始版本：业务层 publish 的 plugins/ 移到公共根 plugins/（与 versions/ 并排）
    #    （业务层 publish 会随 csproj targets 带上内置插件；宿主包负责首次安装形态）
    $pubPlugins = Join-Path $PublishDir 'plugins'
    $layoutPlugins = Join-Path $LayoutDir 'plugins'
    if (Test-Path $pubPlugins) {
        New-Item -ItemType Directory -Force -Path $layoutPlugins | Out-Null
        Get-ChildItem $pubPlugins | ForEach-Object {
            Copy-Item $_.FullName $layoutPlugins -Recurse -Force
        }
        # 业务层不再携带插件目录（插件公共外置，避免每版本复制）
        Remove-Item (Join-Path $versionDir 'plugins') -Recurse -Force -ErrorAction SilentlyContinue
    }

    # 4) 更新代理脚本（公共层）
    $agentSrc = Join-Path $PublishDir 'update-agent.ps1'
    if (-not (Test-Path $agentSrc)) { $agentSrc = Join-Path $RepoRoot 'scripts/update-agent.ps1' }
    if (Test-Path $agentSrc) { Copy-Item $agentSrc (Join-Path $LayoutDir 'update-agent.ps1') -Force }
    # 业务层不重复携带（代理属于公共层）
    Remove-Item (Join-Path $versionDir 'update-agent.ps1') -Force -ErrorAction SilentlyContinue
}

Invoke-ReleaseStep 'package: sanitize output' {
    # Runtime-generated dirs never belong in a distributable (they appear if someone ran the exe in-place).
    # 业务层 + 公共层统一清理运行残留；插件 _backups 防御性清除（历史布局，同意保留）。
    foreach ($base in @($LayoutDir, $versionDir)) {
        foreach ($d in @('data', 'log', 'config')) {
            $p = Join-Path $base $d
            if (Test-Path $p) { Remove-Item $p -Recurse -Force }
        }
    }
    $bk = Join-Path $LayoutDir 'plugins/_backups'
    if (Test-Path $bk) { Remove-Item $bk -Recurse -Force }
    if (-not $IncludePdb) {
        Get-ChildItem $LayoutDir -Recurse -Filter '*.pdb' | Remove-Item -Force
    }
    # 输入36 单文件化后业务层本体 = ForgeSelf.exe（FDD 单文件，托管程序集内嵌），必须保留；
    # 不再删除（旧 ALC 模式曾删 exe/runtimeconfig/deps——该逻辑已废除）。
}

# 输入38：SQLite 驱动已通过 XCode.SQLite 包成为正式依赖（System.Data.SQLite.dll 随发布外置/由 publish-host 复制），
# 不再需要 runtime-probe 注入块（旧 build/runtime/plugins 注入已废除）。
# ---- current 指针 ----
Invoke-ReleaseStep 'package: write versions/current' {
    $versionsDir = Join-Path $LayoutDir 'versions'
    [IO.File]::WriteAllText((Join-Path $versionsDir 'current'), $ver, (New-Object Text.UTF8Encoding $false))
}

if ($Sign) {
    Invoke-ReleaseStep 'package: Authenticode signing' {
        & (Join-Path $RepoRoot 'scripts/sign-publish.ps1') -PublishDir $LayoutDir
        Assert-ExitCode 'sign-publish'
    }
}

$zipPath = Join-Path $OutputDir ("OpenForgeSelf-{0}-win-x64.zip" -f $ver)
Invoke-ReleaseStep 'package: zip + sha256' {
    New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory($LayoutDir, $zipPath,
        [System.IO.Compression.CompressionLevel]::Optimal, $false)
    $hash = (Get-FileHash $zipPath -Algorithm SHA256).Hash.ToLower()
    $sumsPath = Join-Path $OutputDir 'SHA256SUMS.txt'
    Set-Content -Path $sumsPath -Value ("{0}  {1}" -f $hash, (Split-Path $zipPath -Leaf)) -Encoding ascii
}

Write-Host ("package-release: artifact = {0}" -f $zipPath)
Write-Host ("  size = {0:n1} MB" -f ((Get-Item $zipPath).Length / 1MB))
Write-Host ("  layout = {0}" -f $LayoutDir)
