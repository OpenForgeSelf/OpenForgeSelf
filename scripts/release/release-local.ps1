# release-local.ps1 - one-command release pipeline orchestrator.
# Runs: build-frontend -> publish-host (business layer) -> publish-bootstrapper (common layer)
#     -> package-release (assemble QQNT layout) -> make-release-notes.
# The GitHub Actions workflow calls exactly this script; everything CI does is
# reproducible locally with the same command (local-first debugging contract).
# QQNT layout (2026-09-28 批次2.1/输入34; 目录命名统一小写 2026-09-29 输入37): common layer at install root
# (launcher + runtime + framework + plugins/), business layer per version under versions/<ver>/, current pointer.
#
# Examples:
#   ./release-local.ps1                                  # local dry run, Version=0.0.0-local（不补时间码）
#   ./release-local.ps1 -Version 2.3.0 -SkipFrontend     # 三段号自动补时间码 → 2.3.0.<yyMMddHHmm>
#   ./release-local.ps1 -Version 2.3.0.2609161125        # 四段完整发行串，幂等原样使用
#   ./release-local.ps1 -Version v2.3.0.2609161125 -Sign
# 发行串规则（2026-10-02）：<major>.<minor>.<patch>.<yyMMddHHmm>；tag / versions/<ver>/ / zip 名 /
# 两个 exe 的文件版本 / 设置页「当前版本」全部同串。真源 docs/04-standards/packaging-upgrade-backup.md §1.1

[CmdletBinding()]
param(
    [string]$Version = '',
    [string]$OutputRoot = '',
    [string]$UpdateDir = '',
    [switch]$SkipFrontend,
    [switch]$Sign
)

. (Join-Path $PSScriptRoot 'release-lib.ps1')
$repoRoot = Get-ReleaseRepoRoot

# 构建/发布临时目录统一指向项目内 .temp，避开沙箱 safe-delete shim 对系统 TEMP（AppData\Local\Temp）
# 删除的拦截——dotnet publish / esbuild 在构建与收尾时会清理大量临时文件，落到系统 TEMP 会报
# Access is denied / ETIMEDOUT（2026-10-06 实证，曾多次复发）。.temp 已被 .gitignore 与
# check-git-content.ps1 排除，不会入库。子脚本同进程继承此环境变量。
$buildTemp = Join-Path $repoRoot '.temp'
if (-not (Test-Path $buildTemp)) { New-Item -ItemType Directory -Force -Path $buildTemp | Out-Null }
$env:TEMP   = $buildTemp
$env:TMP    = $buildTemp
$env:TMPDIR = $buildTemp

# In CI the tag name arrives via GITHUB_REF_NAME (e.g. v2.3.0.2609161125); locally default to a dev version.
$versionGiven = [bool]$Version
if (-not $Version) {
    if ($env:GITHUB_REF_NAME) { $Version = $env:GITHUB_REF_NAME } else { $Version = '0.0.0-local' }
}
if (-not $OutputRoot) { $OutputRoot = Join-Path $repoRoot 'artifacts' }
# 发行串解析（2026-10-02 版本规则，真源 §1.1）：
#   显式 -Version：3 段（2.3.0）自动补 yyMMddHHmm；4 段（含时间码）幂等原样；
#   CI tag / 本地缺省串：视为发行标识原样使用，绝不改写（改写会让产物版本 ≠ tag）。
$ver = if ($versionGiven) { Get-FullReleaseVersion $Version } else { Get-NormalizedVersion $Version }
$publishDir = Join-Path $OutputRoot 'publish'
$bootDir = Join-Path $OutputRoot 'layout-root'
$layoutDir = Join-Path $OutputRoot 'layout'
$releaseDir = Join-Path $OutputRoot 'release'

Write-Host ("release-local: Version={0} -> release={1} OutputRoot={2}" -f $Version, $ver, $OutputRoot)

$t0 = Get-Date

if (-not $SkipFrontend) {
    & (Join-Path $PSScriptRoot 'build-frontend.ps1') -RepoRoot $repoRoot
    if ($LASTEXITCODE -ne 0) { throw 'build-frontend failed' }
}
else {
    Write-Host 'SKIPPED build-frontend (-SkipFrontend): using existing wwwroot / plugin dists.'
}

& (Join-Path $PSScriptRoot 'publish-host.ps1') -RepoRoot $repoRoot -Version $ver -OutputDir $publishDir
if ($LASTEXITCODE -ne 0) { throw 'publish-host failed' }

& (Join-Path $PSScriptRoot 'publish-bootstrapper.ps1') -RepoRoot $repoRoot -Version $ver -OutputDir $bootDir
if ($LASTEXITCODE -ne 0) { throw 'publish-bootstrapper failed' }

& (Join-Path $PSScriptRoot 'package-release.ps1') -RepoRoot $repoRoot -Version $ver `
    -PublishDir $publishDir -BootDir $bootDir -LayoutDir $layoutDir -OutputDir $releaseDir -Sign:$Sign
if ($LASTEXITCODE -ne 0) { throw 'package-release failed' }

& (Join-Path $PSScriptRoot 'make-release-notes.ps1') -RepoRoot $repoRoot -Version $ver -OutFile (Join-Path $releaseDir ("RELEASE-NOTES-{0}.md" -f $ver))
if ($LASTEXITCODE -ne 0) { throw 'make-release-notes failed' }

# 本地目录更新源（2026-09-27）：-UpdateDir <目录> 时把 zip + SHA256SUMS + 更新说明拷到该目录，
# 宿主设置页把「更新地址」填为该目录即可在页面点「检查更新 → 下载 → 重启并更新」（离线/内网更新）。
if ($UpdateDir) {
    New-Item -ItemType Directory -Force -Path $UpdateDir | Out-Null
    $zip = Join-Path $releaseDir ("OpenForgeSelf-{0}-win-x64.zip" -f $ver)
    if (-not (Test-Path $zip)) { throw "zip not found for UpdateDir copy: $zip" }
    foreach ($f in @($zip, (Join-Path $releaseDir 'SHA256SUMS.txt'), (Join-Path $releaseDir ("RELEASE-NOTES-{0}.md" -f $ver)))) {
        if (Test-Path $f) { Copy-Item $f $UpdateDir -Force }
    }
    Write-Host ''
    Write-Host ("release-local: update dir = {0}" -f $UpdateDir) -ForegroundColor Green
    Get-ChildItem $UpdateDir | ForEach-Object {
        Write-Host ("  {0}  ({1:n1} MB)" -f $_.Name, ($_.Length / 1MB))
    }
}

$dt = ((Get-Date) - $t0).TotalSeconds
Write-Host ''
Write-Host ("release-local: ALL DONE in {0:n0}s" -f $dt)
# 输入43：-File 过滤，避免目录项无 Length 属性在 StrictMode 下报错导致整脚本 exit 1（发布成功但假失败）
Get-ChildItem $releaseDir -File | ForEach-Object {
    Write-Host ("  {0}  ({1:n1} MB)" -f $_.Name, ($_.Length / 1MB))
}
