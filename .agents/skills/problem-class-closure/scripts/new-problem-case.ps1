# new-problem-case.ps1 — 问题类闭环脚手架：建 case 骨架 + 挂/建 PC + 更新两处索引
# 用法：pwsh -NoProfile -File .agents/skills/problem-class-closure/scripts/new-problem-case.ps1 -Slug <kebab-slug> -Title <一句话> -Pc PC-006
#   新问题类另加：-NewPc -Tags tag1,tag2
# 规则本体见 problem-class-closure 技能 references/07-recording-spec.md；本脚本只做机械铺位，内容仍需人/Agent 填写。
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Slug,
    [Parameter(Mandatory)][string]$Title,
    [string]$Pc = '',
    [ValidateSet('P1','P2','P3')] [string]$Severity = 'P1',
    [switch]$NewPc,
    [string[]]$Tags = @()
)
$ErrorActionPreference = 'Stop'
# 本仓铁律（PC-006）：捕获外部命令输出前先切 UTF-8 码页
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$repoRoot = (Get-Item $PSScriptRoot).Parent.Parent.Parent.Parent.FullName  # scripts→skill→skills→.agents→根
$kb = Join-Path $repoRoot 'docs/11-troubleshooting'
$tplDir = Join-Path $repoRoot 'docs/18-templates/postmortem'
$today = Get-Date -Format 'yyyy-MM-dd'

if (-not (Test-Path $kb)) { throw "找不到知识库目录：$kb" }
if ($Pc -and $NewPc) { throw '-Pc（挂既有类）与 -NewPc（新建类）互斥' }
if (-not $Pc -and -not $NewPc) { throw '必须 -Pc PC-NNN 挂既有类，或 -NewPc 建新类（case 不允许悬空，见 Registry 测试）' }

# ---------- 1. PC：定位或新建 ----------
$pcDir = Join-Path $kb 'problem-classes'
if ($NewPc) {
    $exist = Get-ChildItem $pcDir -Filter 'PC-*.md' | ForEach-Object { if ($_.Name -match '^PC-(\d{3})') { [int]$Matches[1] } }
    $num = [int](($exist | Measure-Object -Maximum).Maximum) + 1  # Measure-Object 对 int 序列返回 Double，D3 对 Double 非法，必须显式收整
    $pcId = 'PC-{0:D3}' -f $num
    $pcSlug = $Slug
    $pcFile = Join-Path $pcDir "$pcId-$pcSlug.md"
    if (Test-Path $pcFile) { throw "PC 已存在：$pcFile" }
    $tpl = Get-Content (Join-Path $tplDir 'problem-class.tpl.md') -Raw
    $tagLine = if ($Tags.Count) { "[$($Tags -join ', ')]" } else { '[]' }
    $tpl = $tpl -replace '(?m)^id: .*', "id: $pcId" `
                -replace '(?m)^title: .*', "title: $Title" `
                -replace '(?m)^tags: .*', "tags: $tagLine" `
                -replace '(?m)^first_seen: .*', "first_seen: $today"
    Set-Content -Path $pcFile -Value $tpl -Encoding UTF8
    Write-Host "[新建] PC：$pcFile （status 默认 open；不变量/签名/lens 待填）"
} else {
    $pcFile = Get-ChildItem $pcDir -Filter "$Pc-*.md" | Select-Object -First 1
    if (-not $pcFile) { throw "台账中找不到 $Pc（$pcDir）——挂错号或类未建？" }
    $pcId = $Pc
    # 把 case id 追加进 PC 的 cases[] 行（行内手工拼接，不用 scriptblock 替换）
    $caseId = "$today-$Slug"
    $lines = @(Get-Content $pcFile)
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '^cases: \[(.*)\]\s*$') {
            $v = $Matches[1].Trim()
            if ($v) { $lines[$i] = "cases: [$v, $caseId]" } else { $lines[$i] = "cases: [$caseId]" }
            break
        }
    }
    Set-Content -Path $pcFile -Value $lines -Encoding UTF8
    Write-Host "[挂载] case id 已写入 $($pcFile.Name) 的 cases[]（recurrences[] 若为复发请手工追加）"
}

# ---------- 2. case 骨架 ----------
$caseId = "$today-$Slug"
$caseFile = Join-Path $kb "cases/$caseId.md"
if (Test-Path $caseFile) { throw "case 已存在：$caseFile（同日同类请改已有档）" }
$tpl = Get-Content (Join-Path $tplDir 'case.tpl.md') -Raw
$tpl = $tpl -replace '(?m)^id: .*', "id: $caseId" `
            -replace '(?m)^title: .*', "title: $Title" `
            -replace '(?m)^pc: (\S+).*', "pc: $pcId" `
            -replace '(?m)^severity: .*', "severity: $Severity"
Set-Content -Path $caseFile -Value $tpl -Encoding UTF8
Write-Host "[新建] case：$caseFile"

# ---------- 3. cases/README.md 索引行（插到最后一条表数据行之后） ----------
$casesIdx = Join-Path $kb 'cases/README.md'
$row = "| $caseId | $today | $Title | [$pcId](../problem-classes/$($pcFile.Name)) | Unknown | [链接]($caseId.md) |"
$idxLines = @(Get-Content $casesIdx)
if (-not ($idxLines -join "`n") -match [regex]::Escape($caseId)) {
    $lastDataRow = -1
    for ($i = 0; $i -lt $idxLines.Count; $i++) {
        if ($idxLines[$i] -match '^\| 20\d{2}-') { $lastDataRow = $i }
    }
    if ($lastDataRow -ge 0) {
        $idxLines = @($idxLines[0..$lastDataRow]) + @($row) + @($idxLines[($lastDataRow+1)..($idxLines.Count-1)])
        Set-Content -Path $casesIdx -Value $idxLines -Encoding UTF8
        Write-Host '[索引] cases/README.md 已加行'
    } else {
        Write-Warning "cases/README.md 未找到表数据行，请手工加行：$row"
    }
}

# ---------- 4. 出口提醒 ----------
Write-Host @"

下一步（脚手架不代做的部分）：
  1) 填 case 十一节（现象/红灯/根因+第6问/扫查表/…/证据等级）；
  2) 新 PC：填 invariant/signature/guards/lens，并在 problem-classes/README.md 台账加行（tags 检索表若属新区域也加）；
  3) index.md 加/改现象速查行（新报错关键字时）；
  4) 跑守卫确认索引一致：dotnet test --filter FullyQualifiedName~ProblemClassRegistry
"@
