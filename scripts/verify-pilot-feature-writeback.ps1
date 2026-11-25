# UTF-8 BOM required (PS 5.1). Do not strip.
<#
.SYNOPSIS
  Pilot 04/mini-task 头部 feature / requirements 回写校验（pre-commit 与 CI pilot-gate 共用）。

.DESCRIPTION
  对单个 pilot 任务目录：
  1) feature ≠ none 时，docs/02-features/{feature}-*.md 必须存在
     （例：feature: 032 → 032-agent-hub.md；feature: 032-agent-hub → 找不到 032-agent-hub-*.md）
  2) 若传入 -ChangedFiles，则该功能文档相对路径必须出现在变更列表（回写同批）
  3) 若头部声明 requirements: [...]，各 id 须出现在功能文档正文
  轻量任务（无 04-task.md、用 mini-task.md）同样适用：mini-task.md 若带 YAML 头部则校验；
  无 YAML 头部的轻量任务直接放行（2026-11-24 修复对 mini-task 硬 exit 1 的假红）。

.EXAMPLE
  pwsh -File scripts/verify-pilot-feature-writeback.ps1 -TaskId 2026-11-19-oar-agenthub-runtime -ChangedFiles @('docs/02-features/032-agent-hub.md')
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$TaskId,
    [string[]]$ChangedFiles = @(),
    [string]$ChangedFilesPath = '',
    # CI 区间校验：要求功能文档出现在变更列表。pre-commit 跟进提交默认关闭（只校验存在）。
    [switch]$StrictWriteback,
    [string]$RepoRoot = ''
)

$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }
if (-not $RepoRoot) { $RepoRoot = Split-Path -Parent $PSScriptRoot }

if ($ChangedFilesPath) {
    if (-not (Test-Path -LiteralPath $ChangedFilesPath)) {
        Write-Output ("FAIL: ChangedFilesPath 不存在: {0}" -f $ChangedFilesPath)
        exit 1
    }
    $fromFile = @(Get-Content -LiteralPath $ChangedFilesPath -Encoding utf8 | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    $ChangedFiles = @($ChangedFiles) + $fromFile
}

$pilotDir = Join-Path $RepoRoot "docs/ai/pilot/$TaskId"
if (-not (Test-Path -LiteralPath $pilotDir)) {
    Write-Output ("FAIL: pilot 目录不存在: docs/ai/pilot/{0}" -f $TaskId)
    exit 1
}

# 来源文件：优先 04-task.md；轻量任务允许 mini-task.md 承载 feature/requirements（2026-11-24 修复假红）
$f04 = @(Get-ChildItem -LiteralPath $pilotDir -Filter '04-*.md' -ErrorAction SilentlyContinue | Select-Object -First 1)
$miniPath = Join-Path $pilotDir 'mini-task.md'
$isLite = Test-Path -LiteralPath $miniPath
if ($f04.Count -eq 1) {
    $srcFile = $f04[0].FullName
    $srcLabel = '04'
}
elseif ($isLite) {
    $srcFile = $miniPath
    $srcLabel = 'mini-task'
}
else {
    Write-Output ("FAIL: {0} 缺少 04-*.md 且无 mini-task.md（轻量任务须有 mini-task.md）" -f $TaskId)
    exit 1
}

$t04 = [System.IO.File]::ReadAllText($srcFile, [System.Text.Encoding]::UTF8)
if ($t04 -notmatch '(?s)^\s*---\s*\r?\n(.*?)\r?\n---') {
    if ($srcLabel -eq 'mini-task') {
        Write-Output ("OK: {0} 为轻量任务（mini-task.md 无 YAML 头部，跳过 feature/回写校验）" -f $TaskId)
        exit 0
    }
    Write-Output ("FAIL: {0} 的 {1} 缺少 YAML 头部" -f $TaskId, $srcLabel)
    exit 1
}
$fm = $Matches[1]
$fail = 0
$normChanged = @($ChangedFiles | ForEach-Object { ($_ -replace '\\', '/').Trim() } | Where-Object { $_ })

$mF = [regex]::Match($fm, '(?m)^\s*feature\s*:\s*(\S+)')
$featText = ''
if ($mF.Success) {
    $feat = $mF.Groups[1].Value.Trim('"').Trim("'")
    if ($feat -and $feat -ne 'none') {
        $cand = @(Get-ChildItem -LiteralPath (Join-Path $RepoRoot 'docs/02-features') -Filter "$feat-*.md" -ErrorAction SilentlyContinue)
        if ($cand.Count -eq 0) {
            Write-Output ("FAIL: pilot {0} 的 04 头部 feature={1} 找不到 docs/02-features/{1}-*.md（通常应写短编号如 032 以匹配 032-*.md，不要写成 032-agent-hub）" -f $TaskId, $feat)
            $fail++
        }
        else {
            $rel = 'docs/02-features/' + $cand[0].Name
            $featText = [System.IO.File]::ReadAllText($cand[0].FullName, [System.Text.Encoding]::UTF8)
            if ($StrictWriteback -and $normChanged.Count -gt 0 -and ($normChanged -notcontains $rel)) {
                Write-Output ("FAIL: 回写缺失：pilot {0} 声明 feature={1}，但变更列表未包含 {2}" -f $TaskId, $feat, $rel)
                $fail++
            }
            else {
                if (-not $StrictWriteback -and $normChanged.Count -gt 0 -and ($normChanged -notcontains $rel)) {
                    Write-Output ("OK: feature={0} → {1}（存在；本提交未改功能文档，StrictWriteback 未开）" -f $feat, $rel)
                }
                else {
                    Write-Output ("OK: feature={0} → {1}" -f $feat, $rel)
                }
            }
        }
    }
    else {
        Write-Output 'OK: feature=none（跳过功能文档匹配）'
    }
}
else {
    Write-Output ("WARN: {0} 头部无 feature 字段（跳过）" -f $srcLabel)
}

$mR = [regex]::Match($fm, '(?m)^\s*requirements\s*:\s*\[(.*?)\]')
if ($mR.Success -and $featText) {
    foreach ($raw in ($mR.Groups[1].Value -split ',')) {
        $id = $raw.Trim().Trim('"').Trim("'")
        if (-not $id) { continue }
        if ($featText -notmatch [regex]::Escape($id)) {
            Write-Output ("FAIL: requirements 里的 {0} 未在功能文档登记" -f $id)
            $fail++
        }
    }
}

if ($fail -gt 0) {
    Write-Output ("FAIL: feature/回写校验未通过（{0} 项）" -f $fail)
    exit 1
}
Write-Output 'PASS: feature/回写校验通过'
exit 0
