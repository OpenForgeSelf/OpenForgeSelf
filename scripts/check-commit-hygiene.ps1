#Requires -Version 7
<#
.SYNOPSIS
  提交内容卫生守卫（2026-11-14 用户输入1 立；规则真源 docs/04-standards/agent-workflow/b-engineering-platform.md §B13）。
.DESCRIPTION
  扫描提交内容中的「工作区/工作区外绝对地址、指向具体个人的标识、本地拒绝列表词」。
  -Staged 只拦暂存区新增行（棘轮：存量不拦）；-All 为全仓盘点口径（本地清理批跟踪用），不作提交阻断。
  退出码：0=干净；1=存在违规；2=用法/基础设施错误。
  规则 ID（对外契约）：hygiene.abs-fileurl / hygiene.abs-drive / hygiene.abs-posix / hygiene.abs-unc / hygiene.denylist。
  自扫安全声明：本脚本源码不含任何违规样本字符串（正则字面量自指不匹配）；入库文档引用违规一律只写「规则ID + file:line + 计数」。
.NOTES
  已知局限（如实记录）：
  - 文件名含换行符的路径无法解析（git ls-files 文本口径；本仓无此类文件）。
  - 新增行内容若以「++ 」开头且后随 b/ 或 /dev/null，极小概率被当作 diff 文件头（业界解析器同源歧义）。
  - 非 UTF-8 文件按 UTF-8 解码，无法解码字节以替换符呈现（不影响匹配盘符/ASCII 形态）。
#>
[CmdletBinding()]
param(
    [switch]$All,
    [switch]$Staged,
    [switch]$Json,
    [string[]]$Files,
    [string]$DiffFile,
    [string]$DenylistPath
)

$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }

$script:Violations = [System.Collections.Generic.List[object]]::new()
$script:DenylistSkipped = $false

# L1 内置规则（顺序即报告顺序；fileurl 与 drive 的 lookbehind 互斥，防同一串双报）
$script:L1Rules = @(
    @{ Id = 'hygiene.abs-fileurl'; Rx = [regex]::new('file:///[A-Za-z]:[/\\]', 'IgnoreCase') }
    @{ Id = 'hygiene.abs-drive';   Rx = [regex]::new('(?<![A-Za-z0-9+.\-:/])[A-Za-z]:[/\\]', 'IgnoreCase') }
    @{ Id = 'hygiene.abs-posix';   Rx = [regex]::new('(?<![A-Za-z0-9+.\-:/])/(?:Users|home|root|mnt|media|srv)/', 'IgnoreCase') }
    @{ Id = 'hygiene.abs-unc';     Rx = [regex]::new('\\\\[A-Za-z0-9_.\-]+\\', 'IgnoreCase') }
)

function Write-Usage {
    Write-Host '用法: pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/check-commit-hygiene.ps1 [-Staged|-All|-Files <路径...>|-DiffFile <diff文本路径>] [-Json] [-DenylistPath <路径>]'
    Write-Host '  -Staged        默认模式。解析暂存区新增行（git diff --cached -U0），只拦新增'
    Write-Host '  -All           全仓已跟踪文本文件盘点（跳过二进制/锁文件/超过 2MB；供清理批跟踪）'
    Write-Host '  -Files         显式文件点查（自测与点查用）'
    Write-Host '  -DiffFile      解析统一 diff 文本（测试用，不触碰 git index）'
    Write-Host '  -Json          以 JSON 输出命中清单'
    Write-Host '  -DenylistPath  覆盖默认拒绝列表路径（默认 .forgeself/commit-hygiene.denylist.txt）'
}

function Get-RepoRoot {
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) { throw 'git 不可用' }
    $root = @(& git rev-parse --show-toplevel 2>$null)
    if ($LASTEXITCODE -ne 0 -or $root.Count -eq 0) { throw 'git rev-parse 失败（当前目录不在仓库内）' }
    return $root[0].Trim()
}

function Get-DenylistRules {
    param([string]$RepoRoot)
    $path = $DenylistPath
    if (-not $path) { $path = Join-Path $RepoRoot '.forgeself/commit-hygiene.denylist.txt' }
    $script:DenylistPathUsed = $path
    if (-not (Test-Path -LiteralPath $path)) {
        $script:DenylistSkipped = $true
        return @()
    }
    $rules = @()
    foreach ($raw in [System.IO.File]::ReadAllLines($path)) {
        $line = $raw.Trim()
        if (-not $line -or $line.StartsWith('#')) { continue }
        if ($line.StartsWith('re:')) {
            $expr = $line.Substring(3)
            $rx = $null
            try { $rx = [regex]::new('(?i)' + $expr) } catch { throw ('拒绝列表正则无效：' + $expr) }
            $rules += ,@{ Kind = 'regex'; Value = $rx; Term = $expr }
        }
        else {
            $rules += ,@{ Kind = 'substring'; Value = $line; Term = $line }
        }
    }
    return $rules
}

function Add-Violation {
    param([string]$RuleId, [string]$FilePath, [int]$LineNumber, [string]$LineText)
    $excerpt = ($LineText -replace "`t", ' ').Trim()
    if ($excerpt.Length -gt 120) { $excerpt = $excerpt.Substring(0, 120) + '…' }
    $script:Violations.Add([pscustomobject]@{ rule = $RuleId; file = $FilePath; line = $LineNumber; excerpt = $excerpt })
}

function Invoke-LineScan {
    param([string]$FilePath, [int]$LineNumber, [string]$Text, [object[]]$DenyRules)
    if (-not $Text) { return }
    foreach ($r in $script:L1Rules) {
        if ($r.Rx.IsMatch($Text)) { Add-Violation $r.Id $FilePath $LineNumber $Text }
    }
    foreach ($d in $DenyRules) {
        $hit = $false
        if ($d.Kind -eq 'regex') { $hit = $d.Value.IsMatch($Text) }
        else { $hit = ($text.IndexOf([string]$d.Value, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) }
        if ($hit) { Add-Violation 'hygiene.denylist' $FilePath $LineNumber $Text }
    }
}

# 解析统一 diff 的新增行（含新文件行号）；-Staged 与 -DiffFile 共用
function Get-DiffAddedLines {
    param([string[]]$DiffLines)
    $out = [System.Collections.Generic.List[object]]::new()
    $cur = $null
    $newLine = 0
    foreach ($l in $DiffLines) {
        $l = $l.TrimEnd("`r")
        if ($l.StartsWith('+++ ')) {
            $rest = $l.Substring(4)
            if ($rest.StartsWith('b/')) { $cur = $rest.Substring(2); continue }
            if ($rest.StartsWith('/dev/null')) { $cur = $null; continue }
            # 其余形态视为新增内容行（行内容本身以「++ 」开头的罕见情形），落入下方 + 分支
        }
        if ($l.StartsWith('@@')) {
            $m = [regex]::Match($l, '^@@ -\d+(?:,\d+)? \+(\d+)(?:,\d+)? @@')
            if ($m.Success) { $newLine = [int]$m.Groups[1].Value }
            continue
        }
        if ($l.StartsWith('+')) {
            if ($cur) { $out.Add([pscustomobject]@{ file = $cur; line = $newLine; text = $l.Substring(1) }) }
            $newLine++
            continue
        }
        # 删除行 / 元信息 / 二进制标记：忽略
    }
    return $out
}

# 二进制嗅探：头部 8KB 含 NUL 视为二进制，返回 $null；否则按 UTF-8 读全文
function Read-TextLines {
    param([string]$Path)
    $fs = [System.IO.File]::OpenRead($Path)
    try {
        $buf = [byte[]]::new(8192)
        $n = $fs.Read($buf, 0, $buf.Length)
        for ($i = 0; $i -lt $n; $i++) { if ($buf[$i] -eq 0) { return $null } }
    }
    finally { $fs.Dispose() }
    return [System.IO.File]::ReadAllLines($Path)
}

function Test-SkippableName {
    param([string]$FileName)
    $binExt = @('.exe', '.dll', '.pdb', '.png', '.jpg', '.jpeg', '.gif', '.ico', '.zip', '.7z', '.rar', '.gz', '.woff', '.woff2', '.ttf', '.eot', '.otf', '.mp3', '.mp4', '.pdf', '.bin', '.dat', '.snk', '.pfx', '.bak', '.cache', '.svgz')
    $ext = [System.IO.Path]::GetExtension($FileName).ToLowerInvariant()
    if ($binExt -contains $ext) { return $true }
    if ($FileName -in @('package-lock.json', 'pnpm-lock.yaml', 'yarn.lock')) { return $true }
    if ($FileName -like '*.min.js' -or $FileName -like '*.min.css') { return $true }
    return $false
}

# ---- 主流程 ----
try {
    $modeCount = 0
    if ($All) { $modeCount++ }
    if ($Files) { $modeCount++ }
    if ($DiffFile) { $modeCount++ }
    if ($Staged) { $modeCount++ }
    if ($modeCount -gt 1) { Write-Usage; exit 2 }
    if ($modeCount -eq 0) { $Staged = $true }

    $root = $null
    if (-not $Files -and -not $DiffFile) { $root = Get-RepoRoot }
    if ($DiffFile -and -not (Test-Path -LiteralPath $DiffFile)) { throw ('diff 文件不存在：' + $DiffFile) }

    $denyRules = Get-DenylistRules -RepoRoot ($root ?? (Get-Location).Path)
    $scannedFiles = 0
    $scannedLines = 0

    if ($Staged) {
        Push-Location $root
        try { $diff = @(& git -c core.quotepath=false diff --cached -U0 --no-color --no-ext-diff) }
        finally { Pop-Location }
        if ($LASTEXITCODE -ne 0) { throw 'git diff --cached 失败' }
        $adds = Get-DiffAddedLines -DiffLines $diff
        $scannedFiles = @($adds | Select-Object -ExpandProperty file -Unique).Count
        $scannedLines = $adds.Count
        foreach ($a in $adds) { Invoke-LineScan -FilePath $a.file -LineNumber $a.line -Text $a.text -DenyRules $denyRules }
    }
    elseif ($DiffFile) {
        $diffLines = [System.IO.File]::ReadAllLines((Resolve-Path -LiteralPath $DiffFile))
        $adds = Get-DiffAddedLines -DiffLines $diffLines
        $scannedFiles = @($adds | Select-Object -ExpandProperty file -Unique).Count
        $scannedLines = $adds.Count
        foreach ($a in $adds) { Invoke-LineScan -FilePath $a.file -LineNumber $a.line -Text $a.text -DenyRules $denyRules }
    }
    elseif ($Files) {
        $flat = @($Files | ForEach-Object { $_ -split '[,;\s]+' } | Where-Object { $_ })
        foreach ($f in $flat) {
            if (-not (Test-Path -LiteralPath $f)) { throw ('文件不存在：' + $f) }
            $full = (Resolve-Path -LiteralPath $f).Path
            $lines = Read-TextLines -Path $full
            if ($null -eq $lines) { continue }
            $scannedFiles++
            $scannedLines += $lines.Count
            for ($i = 0; $i -lt $lines.Count; $i++) {
                Invoke-LineScan -FilePath $f -LineNumber ($i + 1) -Text $lines[$i] -DenyRules $denyRules
            }
        }
    }
    elseif ($All) {
        Push-Location $root
        try { $tracked = @(& git -c core.quotepath=false ls-files) }
        finally { Pop-Location }
        if ($LASTEXITCODE -ne 0) { throw 'git ls-files 失败' }
        foreach ($rel in $tracked) {
            if (-not $rel) { continue }
            $fileName = [System.IO.Path]::GetFileName($rel)
            if (Test-SkippableName -FileName $fileName) { continue }
            $full = Join-Path $root $rel
            $fi = Get-Item -LiteralPath $full -ErrorAction SilentlyContinue
            if (-not $fi) { continue }
            if ($fi.Length -gt 2MB) { continue }
            $lines = Read-TextLines -Path $full
            if ($null -eq $lines) { continue }
            $scannedFiles++
            $scannedLines += $lines.Count
            for ($i = 0; $i -lt $lines.Count; $i++) {
                Invoke-LineScan -FilePath $rel -LineNumber ($i + 1) -Text $lines[$i] -DenyRules $denyRules
            }
        }
    }

    if ($script:DenylistSkipped) {
        Write-Host ('[hygiene] 注：拒绝列表文件不存在，L2 已跳过（' + $script:DenylistPathUsed + '）；称谓类红线靠 §B13 条文与 review 兜底')
    }

    if ($Json) {
        $result = [pscustomobject]@{
            result   = ($(if ($script:Violations.Count -gt 0) { 'FAIL' } else { 'PASS' }))
            files    = $scannedFiles
            lines    = $scannedLines
            hits     = $script:Violations.Count
            violations = $script:Violations
        }
        Write-Host ($result | ConvertTo-Json -Depth 4)
    }
    else {
        foreach ($v in $script:Violations) {
            Write-Host ('[hygiene] FAIL {0} {1}:{2}: {3}' -f $v.rule, $v.file, $v.line, $v.excerpt)
        }
    }

    if ($script:Violations.Count -gt 0) {
        Write-Host ('check-commit-hygiene: FAIL (files={0} lines={1} hits={2})' -f $scannedFiles, $scannedLines, $script:Violations.Count)
        exit 1
    }
    Write-Host ('check-commit-hygiene: PASS (files={0} lines={1} hits=0)' -f $scannedFiles, $scannedLines)
    exit 0
}
catch {
    Write-Host ('[hygiene] 守卫异常：' + $_.Exception.Message)
    Write-Host 'check-commit-hygiene: ERROR'
    exit 2
}
