<#
.SYNOPSIS
  安装 OpenForgeSelf git hooks（幂等）：pre-commit + commit-msg（强制可执行位）。

.DESCRIPTION
  pre-commit：触碰 docs/ai/pilot/<task>/ 时校验工件链 + feature/回写（缺失则拒绝提交）。
  commit-msg：非文档改动必须带 `Pilot: <目录名>` trailer，且该目录工件链齐全。
  安装后对源文件与目标 hook 均 `chmod +x` 并断言可执行——Cursor Cloud 的 hooksPath
  dispatcher 只调用带 +x 的原 hook，缺位会静默跳过仓库门禁（2026-11-19 实证）。
  新 clone / 新环境第一步执行本脚本；hook 源文件更新后重跑本脚本即同步。

.EXAMPLE
  pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/install-git-hooks.ps1
#>
$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }

$repoRoot = Split-Path -Parent $PSScriptRoot

# hooks 目录：装到 git-common-dir/hooks（worktree 共享主仓 hooks；勿硬拼 '.git\hooks'）。
# 禁止用 `rev-parse --git-path hooks`：它服从 core.hooksPath。
# Cloud Agent 常把 core.hooksPath 指到 Cursor dispatcher —— 往那里装会覆盖 symlink（2026-11-19）。
# 也不要用 `git -c core.hooksPath= rev-parse --git-path hooks`：空 hooksPath 在本环境会解析成 `./`。
$commonDir = (& git -C $repoRoot rev-parse --git-common-dir) -join ''
if (-not $commonDir) {
    throw "git rev-parse --git-common-dir 无输出（$repoRoot 不是 git 仓库？）"
}
if (-not [System.IO.Path]::IsPathRooted($commonDir)) {
    $commonDir = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($repoRoot, $commonDir))
}
else {
    $commonDir = [System.IO.Path]::GetFullPath($commonDir)
}
$dstDir = Join-Path $commonDir 'hooks'
if (-not (Test-Path -LiteralPath $dstDir)) {
    New-Item -ItemType Directory -Force -Path $dstDir | Out-Null
}
$activeHooksPath = (& git -C $repoRoot config --get core.hooksPath) -join ''
if ($activeHooksPath) {
    Write-Output "注意：core.hooksPath=$activeHooksPath（平台 dispatcher）；仓库 hook 仍安装到 $dstDir（须 +x 才会被链式调用）"
}

function Set-ExecutableBit([string]$Path) {
    # Cursor Cloud / Linux：dispatcher 仅在原 hook 带 +x 时才会调用；
    # Copy-Item 默认 644 → 仓库门禁被静默跳过（2026-11-19 实证）。必须强制可执行。
    if (Get-Command chmod -ErrorAction SilentlyContinue) {
        & chmod +x -- $Path
        if ($LASTEXITCODE -ne 0) { throw "chmod +x 失败: $Path" }
        return
    }
    # Windows + NTFS 没有可执行位语义（git 执行 hook 不看模式位），UnixFileMode 探测只会
    # 抛「not supported on this platform」警告噪声——静默跳过（输入5 2026-11-23）。
    if ($IsWindows) { return }
    try {
        $item = Get-Item -LiteralPath $Path
        if ($item.PSObject.Properties.Name -contains 'UnixFileMode') {
            $item.UnixFileMode = $item.UnixFileMode -bor [System.IO.UnixFileMode]::UserExecute -bor [System.IO.UnixFileMode]::GroupExecute -bor [System.IO.UnixFileMode]::OtherExecute
        }
    } catch {
        Write-Output "警告：无法设置可执行位 $Path — $($_.Exception.Message)"
    }
}

function Test-IsExecutable([string]$Path) {
    # 平台短路必须最先（输入5 2026-11-23，PC-043）：本机 bash 在 PATH（git-bash）时，
    # 若先走 POSIX 分支，`test -x` 收到的是盘符:\... 形态的 Windows 路径，MSYS 内建 test 不识别
    # 反斜杠路径 → 恒 false → hook 被误判「不可执行」→ postinstall 恒败 → 发布链 pnpm install 全挂。
    # Windows 上可执行位不影响 hook 执行（dispatcher +x 是 Linux 侧关切），直接通过。
    if ($IsWindows) { return $true }
    if (Get-Command bash -ErrorAction SilentlyContinue) {
        # 注意：POSIX test 不接受 `test -x -- path`（-- 会被当成操作数）
        & bash -lc ('test -x "{0}"' -f ($Path -replace '"', '\"'))
        return ($LASTEXITCODE -eq 0)
    }
    try {
        $item = Get-Item -LiteralPath $Path
        if ($item.PSObject.Properties.Name -contains 'UnixFileMode') {
            return [bool]($item.UnixFileMode -band [System.IO.UnixFileMode]::UserExecute)
        }
    } catch { }
    return $false
}

$hooks = @('pre-commit', 'commit-msg')
foreach ($h in $hooks) {
    $src = Join-Path $repoRoot "scripts\hooks\$h"
    $dst = Join-Path $dstDir $h
    if (-not (Test-Path -LiteralPath $src)) {
        throw "hook 源文件不存在: $src"
    }
    # 源文件也保持 +x，避免下次 Copy 再丢可执行位；git 可跟踪 100755
    Set-ExecutableBit $src
    Copy-Item -LiteralPath $src -Destination $dst -Force
    Set-ExecutableBit $dst

    # 校验安装结果：hook 文件存在、非空、可执行
    if (-not (Test-Path -LiteralPath $dst) -or (Get-Item -LiteralPath $dst).Length -eq 0) {
        throw "hook 安装失败（文件缺失或为空）: $h"
    }
    if (-not (Test-IsExecutable $dst)) {
        throw "hook 安装失败（不可执行，Cursor/Git dispatcher 会跳过）: $h → $dst"
    }
    Write-Output "已安装 $h -> $dst（可执行）"
}

Write-Output ''
Write-Output '安装校验通过：'
Write-Output '  - pre-commit：触碰 docs/ai/pilot/<task>/ 时校验工件链 + feature/回写（缺失拒绝提交）'
Write-Output '  - commit-msg：非文档改动必须有 Pilot: <目录名> trailer 且工件链齐全'
Write-Output '  - 可执行位：已 chmod +x（否则 Cloud Agent hooksPath dispatcher 会静默跳过）'
