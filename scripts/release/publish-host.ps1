# publish-host.ps1 - dotnet-publish the ForgeSelf host business layer (exe entry + all plugins
# staged by csproj targets). Produces a folder ready to be packaged by package-release.ps1.
# QQNT layout (2026-09-28 批次2.1 + 输入36 single-file): the host business layer is
# FRAMEWORK-DEPENDENT SINGLE-FILE by default — the .NET runtime + ASP.NET Core shared framework
# live once in the install root common layer (published by publish-bootstrapper.ps1 as a single
# file), so each versions/<ver>/ business layer is one ForgeSelf.exe (managed assemblies +
# satellite resources embedded) plus externally-extracted wwwroot/appsettings/native SQLite DLLs,
# keeping versions/ small and coexisting without duplicating the runtime.
#
# Examples:
#   ./publish-host.ps1 -Version 0.1.0
#   ./publish-host.ps1 -Version 0.1.0 -OutputDir D:/tmp/pub -SelfContained

[CmdletBinding()]
param(
    [string]$RepoRoot = '',
    [string]$Version = '0.0.0-local',
    [string]$OutputDir = '',
    [string]$Configuration = 'Release',
    [switch]$SelfContained
)

. (Join-Path $PSScriptRoot 'release-lib.ps1')
if (-not $RepoRoot) { $RepoRoot = Get-ReleaseRepoRoot }
if (-not $OutputDir) { $OutputDir = Join-Path $RepoRoot 'artifacts/publish' }

$ver = Get-NormalizedVersion $Version
$proj = Join-Path $RepoRoot 'ForgeSelf.Api/ForgeSelf.Api.csproj'
# 注意：变量与 switch 参数（$SelfContained）大小写不敏感同名会触发 SwitchParameter 类型转换错误，
# 赋值目标必须用不同名字（PS 5.1 实测坑）。
$selfContainedFlag = if ($SelfContained) { 'true' } else { 'false' }
# 版本注入（2026-10-02 版本规则）：发行串（4 段，第 4 段 = 10 位时间码 yyMMddHHmm）必须成为业务层
# exe 的文件版本——更新判定（UpdateChecker.GetCurrentVersion 读 FileVersion）与设置页「当前版本」都以此为准。
# 通道 = 环境变量 FORGESELF_RELEASE_VERSION（由 ForgeSelf.Api.csproj 主动读取）；非发行串
# （本地 dry run 的 0.0.0-local）不设，csproj 走兜底日期版本机制。
# ⚠ 不用命令行 -p:Version 注入：-p: 是**全局属性**，会传播到项目图里所有被引用项目
# （ForgeSelf.Core / ForgeSelf.Abstractions / Plugins/*）——它们没有显式 AssemblyVersion/PackageVersion，
# 10 位时间码会同时触发 NuGet 项目版本校验（NU1105「不是有效的版本字符串」，错误只写进各自 obj 日志 →
# restore 静默失败）与 GenerateAssemblyInfo 的 GetAssemblyVersion 校验（NETSDK1018 无效的 NuGet
# 版本字符串）→ restore/编译直接失败（2026-10-02 两次实测后定案）。
$injectVersion = Get-ReleaseVersionInjectible $ver
$hasReleaseVersion = [bool]$injectVersion
$prevReleaseVersion = $env:FORGESELF_RELEASE_VERSION
if ($hasReleaseVersion) { $env:FORGESELF_RELEASE_VERSION = $injectVersion }

try {
    Invoke-ReleaseStep "host: dotnet publish ($Configuration, win-x64, selfContained=$selfContainedFlag, SINGLE-FILE, Version=$(if ($hasReleaseVersion) { $injectVersion } else { 'csproj-fallback' }))" {
        # 单文件（FDD 默认）：PublishSingleFile=true 内嵌全部托管程序集 + satellite 资源；
        # IncludeNativeLibrariesForSelfExtract=false → SQLite 等原生 DLL 外置（内嵌会解压到临时目录、
        # 破坏路径基准）；IncludeAllContentForSelfExtract=false → wwwroot/appsettings 等内容外置
        # （AppBuilder 路径基准 = Assembly.Location = 单文件 exe 目录，外置同目录即可解析）。
        # 输入43 曾移除版本注入（当时发行号与文件版本各司其职，2.2.12 / 2.2.2026.0929 双轨）。
        # 2026-10-02 版本规则改为「发行串 = 三段号 + 时间码」后**有意**恢复注入：发行串即文件版本，
        # 两者同串（release-local 单点生成 → 本脚本以环境变量注入 → 更新判定与设置页显示同源）。
        & dotnet publish $proj -c $Configuration -r win-x64 --self-contained $selfContainedFlag `
            -p:ContinuousIntegrationBuild=true `
            -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=false `
            -p:IncludeAllContentForSelfExtract=false `
            -o $OutputDir `
            -nologo -v minimal
        Assert-ExitCode 'dotnet publish'
    }
} finally {
    $env:FORGESELF_RELEASE_VERSION = $prevReleaseVersion
}

$exe = Join-Path $OutputDir 'ForgeSelf.exe'
if (-not (Test-Path $exe)) { throw "publish output missing: $exe" }

# 输入38：System.Data.SQLite.dll 从 NuGet 缓存外置复制到发布目录。
# XCode 12 的 SQLite provider 以「程序目录文件探测」加载 System.Data.SQLite.dll：
# 单文件内嵌后文件不存在 → 探测回退创建 Plugins/ 并外网下载（x.newlifex.com，实测 AgentHub 初始化失败）。
# 外置同名文件后：探测直接命中，零下载、零 Plugins/ 生成（XCode.SQLite 包已把驱动做成正式依赖）。
$sqliteDll = Join-Path $OutputDir 'System.Data.SQLite.dll'
if (-not (Test-Path $sqliteDll)) {
    $pkgSrc = Join-Path $env:USERPROFILE '.nuget\packages\system.data.sqlite\2.0.2\lib\netstandard2.0\System.Data.SQLite.dll'
    if (-not (Test-Path $pkgSrc)) { throw "System.Data.SQLite.dll not found in NuGet cache: $pkgSrc" }
    Copy-Item $pkgSrc $sqliteDll
    Write-Host ("publish-host: System.Data.SQLite.dll externalized ({0} KB)" -f [math]::Round((Get-Item $sqliteDll).Length / 1KB))
}

$pluginDir = Join-Path $OutputDir 'plugins'
$manifests = @(Get-ChildItem $pluginDir -Recurse -Filter 'plugin.json' -ErrorAction SilentlyContinue)
Write-Host ("publish-host: exe ok, plugin manifests staged: {0}" -f $manifests.Count)
