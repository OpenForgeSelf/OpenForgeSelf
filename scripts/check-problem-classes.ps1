# check-problem-classes.ps1 — 问题类台账机器守卫（G3 执行器 + 缺陷类 pilot 07 三节门禁）
#
# 用法（一律 pwsh，禁 powershell 5.1，见 AGENTS §2.3 / PC-006）：
#   pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/check-problem-classes.ps1              # 默认：扫全部 PC 的 signature + allowlist 到期 + guards 完整性
#   pwsh ... -PilotTask 2026-10-17-xxx     # pre-commit 挂接：缺陷类任务（task_kind: defect 且目录日期≥2026-10-17）校验 07 三个新节
#   pwsh ... -Baseline [-PcId PC-002]      # 打印当前签名违例为 allowlist 行草稿（存量入豁免由人审后粘进 PC，必带 expires）
# schema 单一来源 = docs/18-templates/postmortem/problem-class.tpl.md；解析仅支持「一行一个 flow-map」子集（见技能 references/07-recording-spec.md）。
[CmdletBinding()]
param(
    [string]$PilotTask = '',
    [string]$PcId = '',
    [switch]$Baseline
)
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$repoRoot = (Get-Item $PSScriptRoot).Parent.FullName
$pcDir = Join-Path $repoRoot 'docs/11-troubleshooting/problem-classes'
$today = Get-Date

# ---------- PilotTask 模式：缺陷类 pilot 的 07 三节门禁 ----------
if ($PilotTask) {
    $dir = Join-Path $repoRoot "docs/ai/pilot/$PilotTask"
    if (-not (Test-Path $dir)) { Write-Host "[pilot-gate] 目录不存在，跳过：$PilotTask"; exit 0 }
    $prefix = if ($PilotTask.Length -ge 10) { $PilotTask.Substring(0, 10) } else { '' }
    if ($prefix -notmatch '^\d{4}-\d{2}-\d{2}$') {
        Write-Host "[pilot-gate] 目录名前缀非日期，跳过：$PilotTask"; exit 0
    }
    $taskDate = [datetime]::ParseExact($prefix, 'yyyy-MM-dd', [cultureinfo]::InvariantCulture)
    if ($taskDate -lt [datetime]'2026-10-17') { Write-Host "[pilot-gate] 早于机制生效日(2026-10-17)，不追溯：$PilotTask"; exit 0 }
    $task04 = Get-ChildItem $dir -Filter '04-task.md' | Select-Object -First 1
    if (-not $task04) { Write-Host '[pilot-gate] 无 04-task，工件链门禁另有校验，本脚本跳过'; exit 0 }
    $kindMatch = Select-String -Path $task04.FullName -Pattern '^task_kind:\s*(\S+)' | Select-Object -First 1
    if (-not $kindMatch -or $kindMatch.Matches[0].Groups[1].Value -ne 'defect') { Write-Host "[pilot-gate] task_kind≠defect，免三节：$PilotTask"; exit 0 }
    $report = Join-Path $dir '07-final-report.md'
    if (-not (Test-Path $report)) { Write-Host "[pilot-gate] FAIL：缺陷类任务缺 07-final-report.md（$PilotTask）"; exit 1 }
    $missing = @()
    foreach ($h in '问题类归档', '重构提案', '悬空教训') {
        if (-not (Select-String -Path $report -Pattern ("^#{1,4}.*" + $h) -Quiet)) { $missing += $h }
    }
    if ($missing.Count) {
        Write-Host "[pilot-gate] FAIL：07 缺节【$($missing -join '、')】（$PilotTask）——教训不得悬空（problem-class-closure 铁律 8）"
        exit 1
    }
    Write-Host "[pilot-gate] OK：缺陷类 07 三节齐全（$PilotTask）"
    exit 0
}

# ---------- front-matter 解析（无 YAML 库，正则子集） ----------
function Parse-FlowMap([string]$line) {
    $o = @{}
    foreach ($k in 'id', 'kind', 'level', 'status', 'expect', 'expires') {
        if ($line -match "\b$k\s*:\s*(?:`"([^`"]*)`"|([^,}]+))") {
            $v = $Matches[1]; if (-not $v) { $v = $Matches[2] }
            $o[$k] = $v.Trim()
        }
    }
    foreach ($k in 'glob', 'path', 'regex', 'reason', 'todo') {
        if ($line -match "\b$k\s*:\s*`"((?:[^`"\\]|\\.)*)`"") { $o[$k] = $Matches[1] }
        elseif ($line -match "\b$k\s*:\s*([^,}]+)") { $o[$k] = $Matches[1].Trim() }
    }
    return $o
}
function Get-FmValue([string[]]$lines, [string]$key) {
    $l = $lines | Where-Object { $_ -match "^${key}\s*:\s*(.*)$" } | Select-Object -First 1
    if ($null -eq $l) { return '' }
    return ($l -replace "^${key}\s*:\s*", '').Trim()
}
function Get-FmListItems([string[]]$lines, [string]$key) {
    # 取 "key:" 段下以 - 开头的行（缩进 flow-map 或纯文本），直到下一个顶层键
    $items = @(); $in = $false
    foreach ($l in $lines) {
        if ($l -match "^${key}\s*:(.*)$") { $in = $true; continue }
        if (-not $in) { continue }
        if ($l -match '^\s+-\s+(.*)$') { $items += $Matches[1] }
        elseif ($l -match '^\S') { break }
    }
    return $items
}
function Glob-ToRegex([string]$glob) {
    # 支持子集：**/ / ** / * / {a,b}；先摘 {..} 与 ** 为占位符，剩余按 * 切段转义
    $g = $glob -replace '\\', '/'
    $altMap = @{}; $i = 0
    while ($g -match '\{([^{}]+)\}') {
        $key = "@A$i@"; $i++
        $alts = (([regex]::Matches($Matches[1], '[^,]+') | ForEach-Object { [regex]::Escape($_.Value.Trim()) }) -join '|')
        $altMap[$key] = "(?:$alts)"
        $g = $g.Replace($Matches[0], $key)
    }
    $g = $g.Replace('**/', '@S@').Replace('**', '@D@')
    $r = (($g -split '\*') | ForEach-Object { [regex]::Escape($_) }) -join '[^/]*'
    $r = $r.Replace('@S@', '(?:.*/)?').Replace('@D@', '.*')
    foreach ($k in @($altMap.Keys)) { $r = $r.Replace($k, $altMap[$k]) }
    return '^' + $r + '$'
}
$excludeRe = '(^|/)(\.git|node_modules|bin|obj|artifacts|publish|publish\.bak[^/]*|temp|\.temp|\.trash|screenshots|\.playwright[^/]*|\.pw-out[^/]*|\.codebuddy|\.atomcode[^/]*|\.gsk|\.claude|\.specify|\.trae|\.vscode|\.workbuddy|\.codegraph|\.forgeself|\.qoder[^/]*)(/|$)'
function Get-FilesByGlob([string]$globPattern) {
    $g = Glob-ToRegex $globPattern
    # 锚点优化：取首个 * 前的目录层级作扫描根（全仓 -Recurse 太慢）
    $gp = $globPattern -replace '\\', '/'
    $idx = $gp.IndexOf('*')
    $base = $repoRoot
    if ($idx -gt 0) {
        $anchor = $gp.Substring(0, $idx)
        $cut = $anchor.LastIndexOf('/')
        if ($cut -gt 0) {
            $cand = Join-Path $repoRoot ($anchor.Substring(0, $cut) -replace '/', [IO.Path]::DirectorySeparatorChar)
            if (Test-Path $cand) { $base = $cand }
        }
    }
    Get-ChildItem -LiteralPath $base -Recurse -File | Where-Object {
        $p = $_.FullName.Substring($repoRoot.Length + 1).Replace('\', '/')
        ($p -notmatch $excludeRe) -and ($p -match $g)
    }
}

# ---------- 台账扫描 ----------
$fail = New-Object System.Collections.Generic.List[string]
$pcFiles = @(Get-ChildItem $pcDir -Filter 'PC-*.md' | Where-Object { -not $PcId -or $_.Name -like "$PcId-*" } | Sort-Object Name)
if (-not $pcFiles.Count) { Write-Host "找不到 PC 文件（$pcDir / PcId=$PcId）"; exit 1 }

foreach ($f in $pcFiles) {
    $all = @(Get-Content $f.FullName -Encoding UTF8)
    $fm = @(); $inFm = $false
    foreach ($l in $all) {
        if ($l.Trim() -eq '---') { if ($inFm) { break } else { $inFm = $true; continue } }
        if ($inFm) { $fm += $l }
    }
    $id = Get-FmValue $fm 'id'
    $status = Get-FmValue $fm 'status'
    if ($status -notin 'open', 'guarded-partial', 'guarded', 'structural') { $fail.Add("$id : status 非法 '$status'") }

    # allowlist：必带到期日；过期即红
    $allow = @(Get-FmListItems $fm 'allowlist' | ForEach-Object { Parse-FlowMap $_ })
    foreach ($m in $allow) {
        if (-not $m['expires']) { $fail.Add("$id allowlist[$($m['path'])] 缺 expires（豁免必带到期日）"); continue }
        if ([datetime]::Parse($m['expires'], [cultureinfo]::InvariantCulture) -lt $today) { $fail.Add("$id allowlist[$($m['path'])] 已过期 $($m['expires']) ——债到期，修或续期给理由") }
    }

    # guards：active 的 path 必须存在；planned/none 必带 todo+expires 且 TODO.md 逐字可检索
    foreach ($g in @(Get-FmListItems $fm 'guards' | ForEach-Object { Parse-FlowMap $_ })) {
        if ($g['status'] -eq 'active') {
            $gp = ($g['path'] -replace '/', [IO.Path]::DirectorySeparatorChar)
            if (-not (Test-Path (Join-Path $repoRoot $gp))) { $fail.Add("$id guard[$($g['level'])] path 不存在: $($g['path'])") }
        } elseif ($g['status'] -eq 'planned') {
            if (-not $g['todo'] -or -not $g['expires']) { $fail.Add("$id planned guard 必须带 todo+expires（悬空守卫）") }
            elseif ([datetime]::Parse($g['expires'], [cultureinfo]::InvariantCulture) -lt $today) { $fail.Add("$id planned guard 已过期: $($g['expires'])") }
            elseif (-not (Select-String -Path (Join-Path $repoRoot 'TODO.md') -Pattern ([regex]::Escape($g['todo'])) -Quiet)) { $fail.Add("$id planned guard todo 在 TODO.md 逐字检索不到: $($g['todo'])") }
        } else { $fail.Add("$id guard 状态非法: '$($g['status'])'（只允许 active|planned）") }
    }

    # signature 扫描
    foreach ($s in @(Get-FmListItems $fm 'signature' | ForEach-Object { Parse-FlowMap $_ })) {
        if ($s['kind'] -eq 'test') {
            $sp = ($s['path'] -replace '/', [IO.Path]::DirectorySeparatorChar)
            if (-not (Test-Path (Join-Path $repoRoot $sp))) { $fail.Add("$id $($s['id']) test 签名路径不存在: $($s['path'])") }
            continue
        }
        if ($s['kind'] -ne 'grep') { $fail.Add("$id $($s['id']) 未知 kind '$($s['kind'])'"); continue }
        $files = @(Get-FilesByGlob $s['glob'])
        if (-not $files.Count) { $fail.Add("$id $($s['id']) glob 命中 0 文件（签名失效？）: $($s['glob'])"); continue }
        $violations = @()
        foreach ($file in $files) {
            $rel = $file.FullName.Substring($repoRoot.Length + 1).Replace('\', '/')
            if (-not (Select-String -Path $file.FullName -Pattern $s['regex'] -Quiet)) { continue }
            if ($s['expect'] -eq 'no-match') { $violations += $rel }
            elseif ($s['expect'] -eq 'any-match-with-file-level-token-check') {
                # PC-002 §2 判据：命中文件必须有文件级 token 供给证据
                if (-not (Select-String -Path $file.FullName -Pattern 'authFetch|buildHeaders|Authorization' -Quiet)) { $violations += $rel }
            }
            else { $fail.Add("$id $($s['id']) 未知 expect '$($s['expect'])'"); break }
        }
        foreach ($v in $violations) {
            if ($allow | Where-Object { $_['path'] -and $v -eq $_['path'] }) { continue }
            if ($Baseline) {
                $exp = $today.AddDays(90).ToString('yyyy-MM-dd')
                Write-Host "  ALLOWLINE-OPTION(${id}): - {path: `"$v`", reason: `"<填合规理由>`", expires: `"$exp`"}"
            }
            else { $fail.Add("$id $($s['id']) 签名违例: $v（可跑 -Baseline 评估豁免）") }
        }
    }
    Write-Host ("[scan] {0} status={1} 完成" -f $id, $status)
}

Write-Host ''
if ($fail.Count) {
    Write-Host "check-problem-classes FAIL（$($fail.Count) 项）："
    $fail | ForEach-Object { Write-Host "  - $_" }
    exit 1
}
Write-Host "check-problem-classes OK：$($pcFiles.Count) 个 PC 签名/allowlist/guards 全部通过"
exit 0
