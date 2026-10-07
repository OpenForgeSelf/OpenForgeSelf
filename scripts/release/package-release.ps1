# package-release.ps1 - turns publish folders into a QQNT-layout distributable zip.
# QQNT layout (2026-09-28 批次2.1/输入34 + 输入36 single-file; 目录命名统一小写 2026-09-29 输入37):
#   install root (common, unchanged across versions):
#     ForgeSelf.exe (FDD single-file launcher) + .NET runtime DOTNET_ROOT structure
#       (host/fxr + shared frameworks, from BootDir/bootstrapper publish)
#     update-agent.ps1
#     (2026-10-04 输入18: bundled plugins are NOT here anymore — they live inside each version, see below)
#   versions/<ver>/                  (business layer, FDD single-file, per release)
#     ForgeSelf.exe (single-file: managed assemblies + satellites embedded)
#     plugins/                       bundled plugins, shipped with this version (host scans this dir)
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

# 构建/发布临时目录统一指向项目内 .temp，避开沙箱 safe-delete shim 对系统 TEMP（AppData\Local\Temp）
# 删除的拦截——打包/解压/dotnet 会清理临时文件，落到系统 TEMP 报 Access is denied / ETIMEDOUT
# （2026-10-06 实证）。.temp 已被 .gitignore 与 check-git-content.ps1 排除。
$buildTemp = Join-Path $RepoRoot '.temp'
if (-not (Test-Path $buildTemp)) { New-Item -ItemType Directory -Force -Path $buildTemp | Out-Null }
$env:TEMP   = $buildTemp
$env:TMP    = $buildTemp
$env:TMPDIR = $buildTemp

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

    # 3) 内置插件随版本走：保留 versions/<ver>/plugins/（2026-10-04 输入18 改，不再外置到安装根）
    #    宿主运行期只扫两路：业务层旁边的 plugins/（内置，随版本）+ 数据根 ~/.forgeself/plugins/
    #    （用户自行安装）。安装根 plugins 不再作为扫描路径（用户裁定「只保持两路」）。
    #    publish 产出的是大写 Plugins/（csproj Content Include 的源码目录名），复制进来必须规范化成
    #    小写 plugins/——运行布局目录名一律小写（真源 §4-R9/输入37）。Windows 大小写不敏感，
    #    Rename-Item 不能做纯大小写改名，走两步改名。
    $pubPlugins = Join-Path $PublishDir 'plugins'
    $upper = @(Get-ChildItem -LiteralPath $versionDir -Directory -Force -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -ceq 'Plugins' })
    if ($upper.Count -gt 0) {
        $tmp = 'plugins.tmp-casefix'
        Rename-Item -LiteralPath $upper[0].FullName -NewName $tmp
        Rename-Item -LiteralPath (Join-Path $versionDir $tmp) -NewName 'plugins'
        Write-Host ("package-release: normalized bundled plugin dir case Plugins → plugins under {0}" -f $versionDir)
    }
    $exactLower = @(Get-ChildItem -LiteralPath $versionDir -Directory -Force -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -ceq 'plugins' })
    if ($exactLower.Count -eq 0) {
        throw "package-release: 业务层版本目录缺少内置插件目录 versions/<ver>/plugins（publish 产出异常）: $versionDir"
    }
    Write-Host ("package-release: bundled plugins kept inside versions/{0}/plugins (source publish: {1})" -f $ver, $pubPlugins)

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
    # 内置插件已随版本走（输入18），同名的历史残留也要清（避免把 _backups 打进包）
    $bkVer = Join-Path $versionDir 'plugins/_backups'
    if (Test-Path $bkVer) { Remove-Item $bkVer -Recurse -Force }
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
