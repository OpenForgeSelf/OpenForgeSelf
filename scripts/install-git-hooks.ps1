<#
.SYNOPSIS
  安装 OpenForgeSelf git hooks（幂等）：pre-commit + commit-msg。

.DESCRIPTION
  pre-commit：本次提交触碰 docs/ai/pilot/<task>/ 时校验该任务工件链（缺失则拒绝提交）。
  commit-msg：非文档改动必须带 `Pilot: <目录名>` trailer，且该目录工件链齐全。
  新 clone / 新环境第一步执行本脚本；hook 源文件更新后重跑本脚本即同步。

.EXAMPLE
  pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/install-git-hooks.ps1
#>
$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }

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

$hooks = @('pre-commit', 'commit-msg')
foreach ($h in $hooks) {
    $src = Join-Path $repoRoot "scripts\hooks\$h"
    $dst = Join-Path $dstDir $h
    if (-not (Test-Path -LiteralPath $src)) {
        throw "hook 源文件不存在: $src"
    }
    Copy-Item -LiteralPath $src -Destination $dst -Force

    # 校验安装结果：hook 文件存在且非空
    if (-not (Test-Path -LiteralPath $dst) -or (Get-Item -LiteralPath $dst).Length -eq 0) {
        throw "hook 安装失败（文件缺失或为空）: $h"
    }
    Write-Output "已安装 $h -> $dst"
}

Write-Output ''
Write-Output '安装校验通过：'
Write-Output '  - pre-commit：触碰 docs/ai/pilot/<task>/ 时校验工件链（缺失拒绝提交）'
Write-Output '  - commit-msg：非文档改动必须有 Pilot: <目录名> trailer 且工件链齐全'
