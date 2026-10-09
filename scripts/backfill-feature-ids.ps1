#!/usr/bin/env pwsh
<#
.SYNOPSIS
  半自动回填：为 docs/02-features/*.md 补 front-matter（可选补「需求清单」章节）。只增不改、幂等、默认 dry-run。

.DESCRIPTION
  规范见 docs/04-standards/feature-requirement-ids.md（阶段 2 迁移）。
  - 绝不删除或改写既有内容；只在文件头注入 front-matter，按需在文尾追加「需求清单」骨架。
  - 已带 front-matter 的文件自动跳过。
  - 编号冲突（同一 NNN 多篇）自动分配字母后缀：F028a / F028b。
  - status 按正文推断：前 12 行含「已实现」→ implemented，否则 draft（避免误标）。
  - -FrontMatterOnly：只注入 front-matter，不追加需求清单（推荐先做这一步，需求逐篇另填）。
  - 默认只打印计划；确认后加 -Apply 才落盘。UTF-8 带 BOM 写出。

.EXAMPLE
  pwsh scripts/backfill-feature-ids.ps1                              # dry-run
  pwsh scripts/backfill-feature-ids.ps1 -Apply -FrontMatterOnly      # 只补 front-matter
  pwsh scripts/backfill-feature-ids.ps1 -Apply -FeatureNo 034        # 单篇（含需求清单骨架）
#>
[CmdletBinding()]
param(
    [switch]$Apply,
    [switch]$FrontMatterOnly,
    [string]$FeatureNo = '',
    [string]$RepoRoot = ''
)

$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }
if (-not $RepoRoot) { $RepoRoot = Split-Path -Parent $PSScriptRoot }

$dir = Join-Path $RepoRoot 'docs/02-features'
if (-not (Test-Path -LiteralPath $dir)) { throw "找不到 $dir" }

$all = @(Get-ChildItem -LiteralPath $dir -Filter '*.md' | Sort-Object Name)

# ---- 冲突检测 -> feature_key 分配 ----
$byNo = @{}
foreach ($f in $all) {
    if ($f.BaseName -match '^(\d{3})-') {
        $n = $Matches[1]
        if (-not $byNo.ContainsKey($n)) { $byNo[$n] = @() }
        $byNo[$n] += $f
    }
}
$keyOf = @{}
foreach ($n in $byNo.Keys) {
    $fs = @($byNo[$n] | Sort-Object Name)
    if ($fs.Count -eq 1) { $keyOf[$fs[0].Name] = "F$n" }
    else { for ($i = 0; $i -lt $fs.Count; $i++) { $keyOf[$fs[$i].Name] = "F$n" + [char](97 + $i) } }
}

$today = (Get-Date).ToString('yyyy-MM-dd')
$enc = New-Object System.Text.UTF8Encoding($true)
$plan = 0; $skip = 0; $done = 0

foreach ($f in $all) {
    if ($FeatureNo -and $f.BaseName -notmatch "^$FeatureNo-") { continue }
    $text = [System.IO.File]::ReadAllText($f.FullName, [System.Text.Encoding]::UTF8)
    $hasFm = ($text -match '^\uFEFF?---\s*\r?\n')
    if ($hasFm) { Write-Output "  skip（已有 front-matter）: $($f.Name)"; $skip++; continue }

    $fk = $keyOf[$f.Name]; if (-not $fk) { $fk = 'F???' }
    $no = ''; if ($f.BaseName -match '^(\d{3})') { $no = $Matches[1] }

    # status 推断：取前 12 行
    $head = (@($text -split "`r?`n") | Select-Object -First 12) -join "`n"
    $st = if ($head -match '已实现') { 'implemented' } else { 'draft' }

    $fm = "---`r`n" +
          "feature_key: $fk`r`n" +
          "feature_no: $no`r`n" +
          "status: $st`r`n" +
          "last_updated: $today`r`n" +
          "aliases: [`"$($f.BaseName)`"]`r`n" +
          "---`r`n`r`n"

    $stub = "`r`n`r`n## 需求清单（稳定 ID，只增不改号）`r`n`r`n" +
            ("> 规范见 `docs/04-standards/feature-requirement-ids.md`；ID 形如 $fk-R01，只增不改号，" +
             "每行「验收要点」须是可执行判定。`r`n`r`n") +
            "| ID | 需求 | 类型 | 验收要点 | 状态 | 覆盖测试 | 来源 |`r`n" +
            "|----|------|------|----------|------|----------|------|`r`n"

    $doStub = (-not $FrontMatterOnly)
    Write-Output ("  {0} {1}  ->  key={2} status={3} ; front-matter=注入 ; 需求清单={4}" -f `
            $(if ($Apply) { 'APPLY' } else { 'PLAN ' }), $f.Name, $fk, $st, $(if ($doStub) { '追加' } else { '略过(-FrontMatterOnly)' }))
    $plan++

    if ($Apply) {
        $bom = ''
        $body = $text
        if ($body.StartsWith([char]0xFEFF)) { $bom = [char]0xFEFF; $body = $body.Substring(1) }
        $new = $bom + $fm + $body
        if ($doStub) { $new = $new.TrimEnd() + "`r`n" + $stub }
        [System.IO.File]::WriteAllText($f.FullName, $new, $enc)
        $done++
    }
}

Write-Output ''
Write-Output ("汇总：待处理 {0} 篇，跳过 {1} 篇，已落盘 {2} 篇（{3}）" -f `
        $plan, $skip, $done, $(if ($Apply) { '-Apply' } else { 'dry-run，未改动任何文件' }))
if (-not $Apply -and $plan -gt 0) { Write-Output '确认无误后重跑：pwsh scripts/backfill-feature-ids.ps1 -Apply -FrontMatterOnly' }
