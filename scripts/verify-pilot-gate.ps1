#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Pilot 闸门（CI / 本地通用）：非文档改动必须有工件 + Pilot trailer，且 diff 不得越出 04 头部 expected_files。

.DESCRIPTION
  本脚本是阶段 1 落地物，用于取代 verify-pilot-artifacts.ps1 注释里声称、实际并不存在的
  .github/workflows/artifact-gate.yml。调用方：.github/workflows/ci.yml 与需要在本地复现 CI 的人。

  规则来源：AGENTS.md §2.3（Task 头部块）/ §2.5（闸门3 提交与回写）、docs/04-standards/risk-policy.md。
  本脚本属 L4 宪法层，Agent 不得自行修改。

.EXAMPLE
  pwsh scripts/verify-pilot-gate.ps1 -Base origin/main -Head HEAD
  pwsh scripts/verify-pilot-gate.ps1 -Base HEAD~1
#>
[CmdletBinding()]
param(
    [string]$Base = '',
    [string]$Head = 'HEAD',
    [switch]$SkipTrailer,   # 只校验"被触碰的 pilot 目录工件齐全"，不要求 trailer
    [switch]$SkipScope,     # 不校验 expected_files 范围
    [string]$RepoRoot = ''
)

$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }
if (-not $RepoRoot) { $RepoRoot = Split-Path -Parent $PSScriptRoot }

# git 可执行文件解析：Windows 下 pwsh 的 PATH / $env:ProgramFiles 未必可靠（Git Bash 的 PATH 不继承，32/64 位重定向会偏），故同时查文字路径。
$script:GitExe = $null
function Get-GitExe {
    if ($script:GitExe) { return $script:GitExe }
    $c = Get-Command git -ErrorAction SilentlyContinue
    if ($c -and $c.Source) { $script:GitExe = $c.Source; return $script:GitExe }
    $cands = @()
    foreach ($base in @($env:ProgramW6432, $env:ProgramFiles, ${env:ProgramFiles(x86)}, $env:LOCALAPPDATA)) {
        if ($base) {
            $cands += (Join-Path $base 'Git\cmd\git.exe')
            $cands += (Join-Path $base 'Programs\Git\cmd\git.exe')
        }
    }
    $cands += 'C:\Program Files\Git\cmd\git.exe'
    $cands += 'C:\Program Files (x86)\Git\cmd\git.exe'
    foreach ($p in $cands) { if (Test-Path -LiteralPath $p) { $script:GitExe = $p; return $script:GitExe } }
    Write-Host ('ERROR: 找不到 git 可执行文件。已尝试: ' + ($cands -join '; '))
    exit 2
}
# 启动自检：git 必须真的可执行；否则大声失败，绝不静默当作“无改动”。
$script:GitProbe = $null
try {
    $script:GitProbe = & (Get-GitExe) --version
}
catch {
    Write-Host ('ERROR: 无法执行 git（' + $script:GitExe + '）：' + $_.Exception.Message)
    Write-Host '本脚本必须在能访问本仓库的 git 环境下运行（Git Bash / 系统 PowerShell 7），或设置 GIT_EXE 环境变量指向 git.exe。'
    exit 2
}
if (-not $script:GitProbe) {
    Write-Host ('ERROR: git 可执行但无输出（' + $script:GitExe + '）。')
    exit 2
}
function Invoke-Git {
    param([string[]]$A)
    $out = & (Get-GitExe) -C $RepoRoot @A 2>$null
    if ($LASTEXITCODE -ne 0) { return @() }
    return @($out)
}

if (-not $Base -or $Base -match '^0+$') { $Base = 'HEAD~1' }
$range = "$Base..$Head"

$changed = @(Invoke-Git @('diff', '--name-only', '--diff-filter=ACMR', $range) |
        Where-Object { $_ } | ForEach-Object { $_ -replace '\\', '/' })

Write-Output "== pilot-gate =="
Write-Output "范围: $range"
if ($changed.Count -eq 0) {
    Write-Output 'PASS: 无改动'
    exit 0
}

function Test-IsDoc {
    param([string]$P)
    return ($P -match '^docs/' -or $P -match '^TODO\.md$' -or $P -match '^README' -or
            $P -match '^\.forgeself/' -or $P -match '\.md$')
}

$pilotDirs = @($changed |
        Where-Object { $_ -match '^docs/ai/pilot/([^/]+)/' } |
        ForEach-Object { [regex]::Match($_, '^docs/ai/pilot/([^/]+)/').Groups[1].Value } |
        Sort-Object -Unique)
$codeChanged = @($changed | Where-Object { -not (Test-IsDoc $_) })

$fail = 0

# ---- 1) 非文档改动：每个触碰代码的 commit 必须带 Pilot: <目录名> trailer ----
if (-not $SkipTrailer -and $codeChanged.Count -gt 0) {
    $commits = @(Invoke-Git @('log', '--no-merges', '--format=%H', $range) | Where-Object { $_ })
    if ($commits.Count -eq 0) { $commits = @($Head) }
    foreach ($sha in $commits) {
        $cFiles = @(Invoke-Git @('show', '--name-only', '--format=', '--diff-filter=ACMR', $sha) |
                Where-Object { $_ } | ForEach-Object { $_ -replace '\\', '/' } |
                Where-Object { -not (Test-IsDoc $_) })
        if ($cFiles.Count -eq 0) { continue }
        $body = (Invoke-Git @('log', '-1', '--format=%B', $sha)) -join "`n"
        $m = [regex]::Match($body, '(?m)^Pilot:\s*(\S+)\s*$')
        if (-not $m.Success) {
            Write-Output ("  [FAIL] commit {0} 改了非文档文件但缺少 'Pilot: <目录名>' trailer" -f $sha.Substring(0, [Math]::Min(8, $sha.Length)))
            Write-Output ("         涉及: " + (($cFiles | Select-Object -First 5) -join ', '))
            $fail++
            continue
        }
        $dir = $m.Groups[1].Value
        if ($pilotDirs -notcontains $dir) { $pilotDirs += $dir }
        if (-not (Test-Path -LiteralPath (Join-Path $RepoRoot "docs/ai/pilot/$dir"))) {
            Write-Output ("  [FAIL] commit {0} 的 Pilot trailer 指向不存在的目录 docs/ai/pilot/{1}" -f $sha.Substring(0, [Math]::Min(8, $sha.Length)), $dir)
            $fail++
        }
    }
}

# ---- 2) 被触碰的 pilot 目录：工件链齐全（含 04 头部块与 AC 覆盖） ----
if ($pilotDirs.Count -gt 0) {
    $verify = Join-Path $PSScriptRoot 'verify-pilot-artifacts.ps1'
    if (-not (Test-Path -LiteralPath $verify)) {
        Write-Output "  [FAIL] 找不到 scripts/verify-pilot-artifacts.ps1"
        $fail++
    }
    else {
        foreach ($d in $pilotDirs) {
            $out = & pwsh -NoProfile -ExecutionPolicy Bypass -File $verify -RepoRoot $RepoRoot -TaskId $d -EnforceHeader -EnforceAcCoverage 2>&1
            $rc = $LASTEXITCODE
            $out | ForEach-Object { Write-Output "  $_" }
            if ($rc -ne 0) {
                Write-Output ("  [FAIL] pilot 工件链不通过: {0}" -f $d)
                $fail++
            }
        }
    }
}

# ---- 3b) 回写检查：feature 文档须同区间修改；requirements 须已登记 ----
foreach ($d in $pilotDirs) {
    $f04 = @(Get-ChildItem -LiteralPath (Join-Path $RepoRoot "docs/ai/pilot/$d") -Filter '04-*.md' -ErrorAction SilentlyContinue | Select-Object -First 1)
    if ($f04.Count -ne 1) { continue }
    $t04 = [System.IO.File]::ReadAllText($f04[0].FullName, [System.Text.Encoding]::UTF8)
    if ($t04 -notmatch '(?s)^\s*---\s*?
(.*?)?
---') { continue }
    $fm = $Matches[1]
    $featText = ''
    $mF = [regex]::Match($fm, '(?m)^\s*feature\s*:\s*(\S+)')
    if ($mF.Success) {
        $feat = $mF.Groups[1].Value.Trim('"').Trim("'")
        if ($feat -and $feat -ne 'none') {
            $cand = @(Get-ChildItem -LiteralPath (Join-Path $RepoRoot 'docs/02-features') -Filter "$feat-*.md" -ErrorAction SilentlyContinue)
            if ($cand.Count -eq 0) {
                Write-Output ("  [FAIL] pilot {0} 的 04 头部 feature={1} 找不到 docs/02-features/{1}-*.md" -f $d, $feat)
                $fail++
            } else {
                $rel = 'docs/02-features/' + $cand[0].Name
                $featText = [System.IO.File]::ReadAllText($cand[0].FullName, [System.Text.Encoding]::UTF8)
                if ($changed -notcontains $rel) {
                    Write-Output ("  [FAIL] 回写缺失：pilot {0} 声明 feature={1}，但同一区间未修改 {2}" -f $d, $feat, $rel)
                    $fail++
                }
            }
        }
    }
    $mR = [regex]::Match($fm, '(?m)^\s*requirements\s*:\s*\[(.*?)\]')
    if ($mR.Success -and $featText) {
        foreach ($raw in ($mR.Groups[1].Value -split ',')) {
            $id = $raw.Trim().Trim('"').Trim("'")
            if (-not $id) { continue }
            if ($featText -notmatch [regex]::Escape($id)) {
                Write-Output ("  [FAIL] requirements 里的 {0} 未在功能文档登记" -f $id)
                $fail++
            }
        }
    }
}

# ---- 3) 范围守卫：代码 diff 必须落在某个 pilot 的 04 expected_files 内 ----
if (-not $SkipScope -and $codeChanged.Count -gt 0) {
    $allowed = @()
    foreach ($d in $pilotDirs) {
        $f04 = @(Get-ChildItem -LiteralPath (Join-Path $RepoRoot "docs/ai/pilot/$d") -Filter '04-*.md' -ErrorAction SilentlyContinue | Select-Object -First 1)
        if ($f04.Count -ne 1) { continue }
        $text = [System.IO.File]::ReadAllText($f04[0].FullName, [System.Text.Encoding]::UTF8)
        $inBlock = $false
        foreach ($line in ($text -split "`r?`n")) {
            if (-not $inBlock) {
                if ($line -match '^expected_files\s*:') { $inBlock = $true }
                continue
            }
            if ($line -match '^\s*-\s*(.+?)\s*$') { $allowed += ($Matches[1].Trim().Trim('"').Trim("'") -replace '\\', '/'); continue }
            if ($line -match '^\S') { break }
        }
    }
    if ($allowed.Count -eq 0) {
        $fail++
        Write-Output '  [FAIL] 改了非文档文件，但没有任何 pilot 的 04-task.md 声明 expected_files（无法界定范围）'
    }
    else {
        foreach ($f in $codeChanged) {
            $ok = $false
            foreach ($p in $allowed) {
                $pp = ($p -replace '\*\*', '*').TrimEnd('/')
                if ($f -eq $pp -or $f -like "$pp/*" -or $f -like $pp) { $ok = $true; break }
            }
            if (-not $ok) {
                Write-Output ("  [FAIL] 越界 diff（未在 expected_files 中）: {0}" -f $f)
                $fail++
            }
        }
    }
}

if ($fail -eq 0) {
    Write-Output 'PASS: pilot 闸门通过（工件齐全 + trailer + 范围）'
    exit 0
}
Write-Output ("FAIL: pilot 闸门未通过（{0} 项）" -f $fail)
exit 1
