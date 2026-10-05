#!/usr/bin/env pwsh
<#
.SYNOPSIS
  AI-Native Pilot 工件链门禁：核验 docs/ai/pilot/<task-id>/ 下 00-07 八件工件齐全且关键节存在。

.DESCRIPTION
  自动暴露「PILOT 文档缺失 / 关键节缺位」，不依赖 agent 自觉、不需用户提醒。
  闸门2 前置校验：缺失或关键节缺位即 FAIL（exit 1），禁止宣称任务完成。
  配套：.github/workflows/artifact-gate.yml 在 push/PR 时自动执行，漏文件 CI 直接红。

.EXAMPLE
  pwsh -File scripts/verify-pilot-artifacts.ps1                                          # 扫全部任务目录
  pwsh -File scripts/verify-pilot-artifacts.ps1 -TaskId 027-plugin-local-update-source
  pwsh scripts/verify-pilot-artifacts.ps1 -RepoRoot <repo>                              # CI 用法
#>
[CmdletBinding()]
param(
    [string]$TaskId = '',      # docs/ai/pilot/<TaskId> 目录名；空 = 扫描全部
    [string]$RepoRoot = ''     # 仓库根；缺省取脚本上级目录
)

$ErrorActionPreference = 'Stop'

if (-not $RepoRoot) { $RepoRoot = Split-Path -Parent $PSScriptRoot }
$pilotRoot = Join-Path $RepoRoot 'docs/ai/pilot'
if (-not (Test-Path -LiteralPath $pilotRoot)) {
    Write-Output "FAIL: no pilot root at $pilotRoot"
    exit 1
}

$tasks = @()
if ($TaskId) {
    $d = Join-Path $pilotRoot $TaskId
    if (-not (Test-Path -LiteralPath $d)) {
        Write-Output "FAIL: task dir not found: $d"
        exit 1
    }
    $tasks = @($d)
}
else {
    $tasks = @(Get-ChildItem $pilotRoot -Directory -ErrorAction SilentlyContinue | ForEach-Object { $_.FullName })
}
if ($tasks.Count -eq 0) {
    Write-Output 'FAIL: no pilot task directories under docs/ai/pilot'
    exit 1
}

$globalFail = 0

foreach ($t in $tasks) {
    $id = Split-Path -Leaf $t
    Write-Output "== $id =="

    # ① 八件工件必须存在且非空（00-repository-understanding ~ 07-final-report）
    for ($i = 0; $i -le 7; $i++) {
        $pattern = ('{0:D2}-*.md' -f $i)
        $files = @(Get-ChildItem -LiteralPath $t -Filter $pattern -ErrorAction SilentlyContinue)
        if ($files.Count -ne 1) {
            Write-Output ("  [FAIL] {0:D2}-*.md 缺失或重复（找到 {1} 个）" -f $i, $files.Count)
            $globalFail++
        }
        else {
            $len = (Get-Item -LiteralPath $files[0].FullName).Length
            if ($len -eq 0) {
                Write-Output ("  [FAIL] {0:D2} 为空文件：{1}" -f $i, $files[0].Name)
                $globalFail++
            }
        }
    }

    # ② 关键节抽查（防止「文件在但内容空转」）
    $checks = @(
        @{ idx = '02'; kind = 'count'; pat = '(?m)^## ';      min = 5; label = 'Spec 至少 5 节' },
        @{ idx = '04'; kind = 'match'; pat = 'Allowed|Forbidden'; min = 0; label = 'Task 含 Allowed/Forbidden 边界' },
        @{ idx = '05'; kind = 'match'; pat = 'Verified';          min = 0; label = 'Evidence 含来源等级 Verified' },
        @{ idx = '06'; kind = 'match'; pat = 'Final Decision|APPROVED|CHANGES_REQUIRED|BLOCKED'; min = 0; label = 'Review 含 Final Decision 结论' },
        @{ idx = '07'; kind = 'match'; pat = 'Validation|Review'; min = 0; label = 'Final Report 含 Validation/Review 节' }
    )
    foreach ($chk in $checks) {
        $files = @(Get-ChildItem -LiteralPath $t -Filter ($chk.idx + '-*.md') -ErrorAction SilentlyContinue)
        if ($files.Count -ne 1) { continue }   # 缺失已在上一步报
        $txt = [System.IO.File]::ReadAllText($files[0].FullName, [System.Text.Encoding]::UTF8)
        if ($chk.kind -eq 'count') {
            $hits = ([regex]::Matches($txt, $chk.pat)).Count
            if ($hits -lt $chk.min) {
                Write-Output ("  [FAIL] {0}: {1}（实际 {2} < 要求 {3}）" -f $chk.idx, $chk.label, $hits, $chk.min)
                $globalFail++
            }
        }
        else {
            if ($txt -notmatch $chk.pat) {
                Write-Output ("  [FAIL] {0}: {1} 缺失" -f $chk.idx, $chk.label)
                $globalFail++
            }
        }
    }
}

if ($globalFail -eq 0) {
    Write-Output 'PASS: 全部 PILOT 工件链齐全（00-07 八件 + 关键节）'
    exit 0
}
else {
    Write-Output ("FAIL: 共 {0} 项缺失/缺位，补齐前禁止宣称任务完成（闸门2 前置）" -f $globalFail)
    exit 1
}
