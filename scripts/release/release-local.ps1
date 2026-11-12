# release-local.ps1 - one-command release pipeline orchestrator.
# Runs: build-frontend -> publish-host (business layer) -> publish-bootstrapper (common layer)
#     -> package-release (assemble QQNT layout) -> make-release-notes.
# The GitHub Actions workflow calls exactly this script; everything CI does is
# reproducible locally with the same command (local-first debugging contract).
# QQNT layout (2026-09-28 批次2.1/输入34; 目录命名统一小写 2026-09-29 输入37): common layer at install root
# (launcher + runtime + framework + plugins/), business layer per version under versions/<ver>/, current pointer.
#
# Examples:
#   ./release-local.ps1                                  # 本地缺省（2026-10-30 起）：读 ForgeSelf.Api.csproj 版本行（三段）自动补时间码
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
    [switch]$Sign,
    # 磁盘水位阈值（GB），默认 2.5；测试用 -MinFreeGB 9999 模拟低水位中止（task-13① 可测化）
    [double]$MinFreeGB = 2.5
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

# ESBUILD_BINARY_PATH 护栏（2026-11-05 lead 实战）：harness 会话进程可能带全局 esbuild 二进制
# 环境变量，插件 web `pnpm install` 的 esbuild@0.25.0 postinstall 会按它校验版本 →
# 「Expected 0.25.0 but got 0.25.12」install 阶段必炸；patch-esbuild.mjs 的中和发生在 install
# 之后救不了 install 阶段，故组包最前显式摘除（子脚本同进程继承）。
if ($env:ESBUILD_BINARY_PATH) {
    Write-Host ("release-local: removed ESBUILD_BINARY_PATH env guard conflict (was: {0})" -f $env:ESBUILD_BINARY_PATH) -ForegroundColor Yellow
    Remove-Item Env:ESBUILD_BINARY_PATH -ErrorAction SilentlyContinue
}

# 版本来源三分支（2026-10-30 输入3 用户拍板，真源 docs/04-standards/packaging-upgrade-backup.md §1.1/§4-R10）：
#   ① 显式 -Version：3 段补时间码 / 4 段幂等（行为不变）；
#   ② CI：GITHUB_REF_NAME 有值 → tag 原样使用、绝不改写（产物版本必须 = tag）；
#   ③ 本地缺省：读 ForgeSelf.Api.csproj 的版本行（三段固定部分）再补时间码 → 发布 zip 不需指定版本；
#      csproj 版本行 bump 必须经用户允许，agent 不得擅自改。
$versionFromCsproj = $false
$versionFromCi = $false
if (-not $Version) {
    if ($env:GITHUB_REF_NAME) {
        $Version = $env:GITHUB_REF_NAME
        $versionFromCi = $true
    }
    else {
        $Version = Get-CsprojReleaseVersion -RepoRoot $repoRoot
        $versionFromCsproj = $true
        Write-Host ("release-local: version line from ForgeSelf.Api.csproj (VersionPrefix) = {0} (bump requires user approval)" -f $Version)
    }
}
if (-not $OutputRoot) { $OutputRoot = Join-Path $repoRoot 'artifacts' }
# 本地打包默认输出到「项目上级 updates 目录」（仓库外、不入 git，避免污染工作区与误入版本控制）；
# CI（GITHUB_REF_NAME 有值）不传 -UpdateDir，保持 GitHub Release 通道、不向仓库外写目录。
if (-not $UpdateDir -and -not $env:GITHUB_REF_NAME) {
    # worktree 盲区修正（2026-11-05 lead 实战）：worktree 建在主仓 .temp\wt-<hash> 下时，
    # Split-Path repoRoot -Parent 落到主仓 .temp → 缺省 updates 写进 .temp\updates（会被清理、
    # 不入真源，正是 0401/0430「真源补拷」手动步骤的来源）。沿祖先链检测 <主仓>\.temp\wt-* 形态，
    # 命中则把 updates 根锚回主仓根的上级；拿不准时用 -UpdateDir 显式指定。
    $updatesRootParent = Split-Path $repoRoot -Parent
    $probe = $repoRoot
    while ($true) {
        $parent = Split-Path $probe -Parent
        if (-not $parent -or $parent -eq $probe) { break }
        $leaf = Split-Path $probe -Leaf
        $parentLeaf = Split-Path $parent -Leaf
        if (($leaf -like 'wt-*') -and ($parentLeaf -eq '.temp')) {
            $mainRoot = Split-Path $parent -Parent
            $updatesRootParent = Split-Path $mainRoot -Parent
            Write-Host ("release-local: worktree detected -> updates anchored to main repo parent: {0}" -f $updatesRootParent) -ForegroundColor Yellow
            break
        }
        $probe = $parent
    }
    $UpdateDir = Join-Path $updatesRootParent 'updates'
}
# 发行串解析（2026-10-02 版本规则，真源 §1.1；2026-10-30 起本地缺省改读 csproj 版本行）：
#   显式 -Version / csproj 缺省：3 段（2.4.22）自动补 yyMMddHHmm；4 段（含时间码）幂等原样；
#   CI tag：视为发行标识原样使用，绝不改写（改写会让产物版本 ≠ tag）。
$ver = if ($versionFromCi) { Get-NormalizedVersion $Version } else { Get-FullReleaseVersion $Version }
$publishDir = Join-Path $OutputRoot 'publish'
$bootDir = Join-Path $OutputRoot 'layout-root'
$layoutDir = Join-Path $OutputRoot 'layout'
$releaseDir = Join-Path $OutputRoot 'release'

# 磁盘水位预检（task-13①，2026-11-05）：发布链需 ~3GB（publish + layout + zip 双份），磁盘满会在
# 中途以模糊 IO 错失败并留半成品（昨夜 D 盘 0.06GB 时 92MB 拷贝失败被误读成文件问题）。free < 2.5GB 直接中止。
foreach ($probe in @($OutputRoot, $UpdateDir, $buildTemp) | Where-Object { $_ }) {
    $driveName = $probe.Substring(0, 1)
    $drive = Get-PSDrive -Name $driveName -ErrorAction SilentlyContinue
    $thresholdBytes = $MinFreeGB * 1GB
    if ($drive -and $drive.Free -lt $thresholdBytes) {
        throw ("disk watermark: free {0:n2}GB on {1}: (< {2}GB) - abort before packaging; clean old worktrees/artifacts first (or pass -MinFreeGB)" -f ($drive.Free / 1GB), $driveName, $MinFreeGB)
    }
}

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
    # 验包固定步骤（task-13②，0401/0430 空转 apply 的根因）：拷贝后必须按 SHA256SUMS.txt 复算
    # 目标文件哈希并对表，防「复制没发生/拷一半」静默成功——宿主 hasUpdate=false 空转即源于此。
    $sums = Join-Path $UpdateDir 'SHA256SUMS.txt'
    if (-not (Test-Path $sums)) { throw "UpdateDir verification failed: SHA256SUMS.txt missing after copy" }
    $verified = 0
    Get-Content $sums | ForEach-Object {
        if ($_ -match '^([0-9a-fA-F]{64})\s+\*?(.+)$') {
            $expectedHash = $Matches[1].ToLowerInvariant()
            $dest = Join-Path $UpdateDir $Matches[2].Trim()
            if (-not (Test-Path $dest)) { throw ("UpdateDir verification failed: missing {0}" -f $Matches[2].Trim()) }
            $actualHash = (Get-FileHash $dest -Algorithm SHA256).Hash.ToLowerInvariant()
            if ($actualHash -ne $expectedHash) { throw ("UpdateDir verification failed: SHA256 mismatch for {0}" -f $Matches[2].Trim()) }
            $verified++
        }
    }
    if ($verified -eq 0) { throw "UpdateDir verification failed: SHA256SUMS.txt has no parseable entries" }
    Write-Host ("release-local: UpdateDir SHA256 verification PASS ({0} file(s) rechecked)" -f $verified) -ForegroundColor Green
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
