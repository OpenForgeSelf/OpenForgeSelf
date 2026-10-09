#!/usr/bin/env pwsh
<#
.SYNOPSIS
  AI-Native Pilot 工件链门禁：核验 docs/ai/pilot/<task-id>/ 工件齐全、关键节存在、04 头部块合法、AC 覆盖。

.DESCRIPTION
  自动暴露「PILOT 文档缺失 / 关键节缺位 / 头部块缺失 / AC 无证据」，不依赖 agent 自觉、不需用户提醒。
  闸门2 前置校验：不通过即 FAIL（exit 1），禁止宣称任务完成。

  两种形态：
    - 全量（默认）：要求 00 ~ 07 八件齐全且非空。
    - 轻量（自动）：目录内存在 mini-task.md 时，改要求 mini-task.md + 05 + 06 + 07
      （流程规范 §4 允许 Intent/Spec/Plan/Task 合并为 mini-task.md；Evidence 与 Review 仍必须单独产出）。

  机械校验开关（供 CI / pre-commit 打开；历史全量扫描默认关闭以免对旧目录假红）：
    -EnforceHeader        04-task.md 头部 front-matter 必须存在且含 risk/expected_files/feature/rollback
    -EnforceAcCoverage    02 中每个 ACn 必须在 05 出现

  配套：.github/workflows/ci.yml → scripts/verify-pilot-gate.ps1；本地 pre-commit / commit-msg。
  本脚本属 L4 宪法层，Agent 不得自行修改。

.EXAMPLE
  pwsh -File scripts/verify-pilot-artifacts.ps1                                         # 扫全部任务目录
  pwsh -File scripts/verify-pilot-artifacts.ps1 -TaskId 2026-10-06-example -EnforceHeader -EnforceAcCoverage
  pwsh scripts/verify-pilot-artifacts.ps1 -RepoRoot <repo>                              # CI 用法
#>
[CmdletBinding()]
param(
    [string]$TaskId = '',      # docs/ai/pilot/<TaskId> 目录名；空 = 扫描全部
    [string]$RepoRoot = '',    # 仓库根；缺省取脚本上级目录
    [switch]$EnforceHeader,
    [switch]$EnforceAcCoverage
)

$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }

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

function Read-Utf8 {
    param([string]$Path)
    return [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8)
}
function Get-Artifact {
    param([string]$Dir, [int]$Idx)
    return @(Get-ChildItem -LiteralPath $Dir -Filter ('{0:D2}-*.md' -f $Idx) -ErrorAction SilentlyContinue)
}
function Get-FrontMatter {
    param([string]$Text)
    if ($Text -notmatch '(?s)^\s*---\s*\r?\n(.*?)\r?\n---') { return $null }
    return $Matches[1]
}

$globalFail = 0
$globalWarn = 0

foreach ($t in $tasks) {
    $id = Split-Path -Leaf $t
    Write-Output "== $id =="

    $mini = Join-Path $t 'mini-task.md'
    $isLite = Test-Path -LiteralPath $mini

    if ($isLite) {
        Write-Output '  [mode] 轻量（mini-task.md）'
        if ((Get-Item -LiteralPath $mini).Length -eq 0) {
            Write-Output '  [FAIL] mini-task.md 为空文件'
            $globalFail++
        }
        foreach ($idx in @(5, 6, 7)) {
            $fs = Get-Artifact $t $idx
            if ($fs.Count -ne 1) {
                Write-Output ("  [FAIL] {0:D2}-*.md 缺失或重复（找到 {1} 个）" -f $idx, $fs.Count)
                $globalFail++
            }
            elseif ((Get-Item -LiteralPath $fs[0].FullName).Length -eq 0) {
                Write-Output ("  [FAIL] {0:D2} 为空文件：{1}" -f $idx, $fs[0].Name)
                $globalFail++
            }
        }
    }
    else {
        # ① 八件工件必须存在且非空（00-repository-understanding ~ 07-final-report）
        for ($i = 0; $i -le 7; $i++) {
            $fs = Get-Artifact $t $i
            if ($fs.Count -ne 1) {
                Write-Output ("  [FAIL] {0:D2}-*.md 缺失或重复（找到 {1} 个）" -f $i, $fs.Count)
                $globalFail++
            }
            elseif ((Get-Item -LiteralPath $fs[0].FullName).Length -eq 0) {
                Write-Output ("  [FAIL] {0:D2} 为空文件：{1}" -f $i, $fs[0].Name)
                $globalFail++
            }
        }
    }

    # ② 关键节抽查（防止「文件在但内容空转」）
    $checks = @(
        @{ idx = '02'; kind = 'count'; pat = '(?m)^## ';   min = 5; label = 'Spec 至少 5 节' },
        @{ idx = '04'; kind = 'match'; pat = 'Allowed|Forbidden'; min = 0; label = 'Task 含 Allowed/Forbidden 边界' },
        @{ idx = '05'; kind = 'match'; pat = 'Verified';   min = 0; label = 'Evidence 含来源等级 Verified' },
        @{ idx = '06'; kind = 'match'; pat = 'Final Decision|APPROVED|CHANGES_REQUIRED|BLOCKED'; min = 0; label = 'Review 含 Final Decision 结论' },
        @{ idx = '07'; kind = 'match'; pat = 'Validation|Review'; min = 0; label = 'Final Report 含 Validation/Review 节' }
    )
    foreach ($chk in $checks) {
        $fs = Get-Artifact $t ([int]$chk.idx)
        if ($fs.Count -ne 1) { continue }   # 缺失已在上一步报
        $txt = Read-Utf8 $fs[0].FullName
        if ($chk.kind -eq 'count') {
            $hits = ([regex]::Matches($txt, $chk.pat)).Count
            if ($hits -lt $chk.min) {
                Write-Output ("  [FAIL] {0}: {1}（实际 {2} < 要求 {3}）" -f $chk.idx, $chk.label, $hits, $chk.min)
                $globalFail++
            }
        }
        elseif ($txt -notmatch $chk.pat) {
            Write-Output ("  [FAIL] {0}: {1} 缺失" -f $chk.idx, $chk.label)
            $globalFail++
        }
    }

    # ③ 04 头部块（stage 4 / 闸门3 回写锚点）
    $f04 = Get-Artifact $t 4
    if ($f04.Count -eq 1) {
        $fm = Get-FrontMatter (Read-Utf8 $f04[0].FullName)
        if (-not $fm) {
            if ($EnforceHeader) {
                Write-Output '  [FAIL] 04-task.md 缺少 YAML 头部块（模板 docs/18-templates/ai-pilot/04-task.tpl.md）'
                $globalFail++
            }
            else {
                Write-Output '  [WARN] 04-task.md 无头部块（未开 -EnforceHeader，仅记录）'
                $globalWarn++
            }
        }
        else {
            foreach ($key in @('risk', 'expected_files', 'feature', 'rollback')) {
                if ($fm -notmatch ('(?m)^\s*' + $key + '\s*:')) {
                    Write-Output ("  [FAIL] 04 头部块缺字段: {0}" -f $key)
                    $globalFail++
                }
            }
            if ($fm -notmatch '(?m)^\s*risk\s*:.*level\s*:\s*L[0-4]') {
                Write-Output '  [FAIL] 04 头部块 risk.level 缺失或不是 L0~L4'
                $globalFail++
            }
        }
    }

    # ④ AC 覆盖：02 的每个 ACn 必须在 05 有对应证据
    if ($EnforceAcCoverage) {
        $f02 = Get-Artifact $t 2
        $f05 = Get-Artifact $t 5
        if ($f02.Count -eq 1 -and $f05.Count -eq 1) {
            $t02 = Read-Utf8 $f02[0].FullName
            $t05 = Read-Utf8 $f05[0].FullName
            $acs = @([regex]::Matches($t02, '\bAC[-\s]?(\d+)\b') |
                    ForEach-Object { 'AC' + $_.Groups[1].Value } | Sort-Object -Unique)
            if ($acs.Count -eq 0) {
                Write-Output '  [WARN] 02 未使用 ACn 编号，无法机械校验 AC 覆盖'
                $globalWarn++
            }
            else {
                $missing = @($acs | Where-Object { $t05 -notmatch ('\b' + [regex]::Escape($_) + '\b') })
                if ($missing.Count -gt 0) {
                    Write-Output ('  [FAIL] 02 的验收标准在 05 无对应证据: ' + ($missing -join ', '))
                    $globalFail++
                }
            }
        }
    }
}

Write-Output ''
if ($globalFail -eq 0) {
    Write-Output 'PASS: 全部 PILOT 工件链齐全（00-07 八件 或 轻量 mini-task.md + 05/06/07；含关键节）'
    if ($globalWarn -gt 0) { Write-Output ("（提示：{0} 项 WARN，未阻断）" -f $globalWarn) }
    exit 0
}
else {
    Write-Output ("FAIL: 共 {0} 项缺失/缺位，补齐前禁止宣称任务完成（闸门2 前置）" -f $globalFail)
    exit 1
}
