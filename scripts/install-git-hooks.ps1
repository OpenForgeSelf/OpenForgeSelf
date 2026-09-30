<#
.SYNOPSIS
  安装 OpenForgeSelf git pre-commit hook（幂等）：复制 scripts/hooks/pre-commit 到 .git/hooks/pre-commit。

.DESCRIPTION
  hook 在每次 git commit 前自动校验本次提交触碰的 docs/ai/pilot/<task>/ 工件链（00-07 八件 + 关键节），
  缺失则拒绝提交——自动暴露，不依赖 agent 自觉、不需用户提醒。
  新 clone / 新环境第一步执行本脚本；hook 源文件更新后重跑本脚本即同步。

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File scripts/install-git-hooks.ps1
#>
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot

# hooks 目录必须问 git 本身：git worktree 的 <worktree>/.git 是**文件**（gitdir 指针），
# 硬拼 '.git\hooks' 会 DirectoryNotFound（2026-09-30 worktree 实测）。
# rev-parse 返回的路径可能相对当前目录，统一解析成绝对路径。
$gitHooks = (& git -C $repoRoot rev-parse --git-path hooks) -join ''
if (-not $gitHooks) {
    throw "git rev-parse --git-path hooks 无输出（$repoRoot 不是 git 仓库？）"
}
$dstDir = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($repoRoot, $gitHooks))
if (-not (Test-Path -LiteralPath $dstDir)) {
    New-Item -ItemType Directory -Force -Path $dstDir | Out-Null
}

$src = Join-Path $repoRoot 'scripts\hooks\pre-commit'
$dst = Join-Path $dstDir 'pre-commit'
if (-not (Test-Path -LiteralPath $src)) {
    throw "hook 源文件不存在: $src"
}

Copy-Item -LiteralPath $src -Destination $dst -Force
Write-Output "已安装 pre-commit hook -> $dst"
Write-Output '每次 git commit 将自动校验 docs/ai/pilot 工件链（缺失拒绝提交）'

# 校验安装结果：hook 文件存在且非空
if (-not (Test-Path -LiteralPath $dst) -or (Get-Item -LiteralPath $dst).Length -eq 0) {
    throw 'hook 安装失败（文件缺失或为空）'
}
Write-Output '安装校验通过'
