<#
.SYNOPSIS
  git 内容审计：检查仓库中"不希望提交"的内容 —— 会被提交的（工作区/暂存）与已经提交的（历史）。

.DESCRIPTION
  两种模式（可叠加）：
    1) 工作区模式（默认）：扫描未跟踪文件（是否会被提交、命中黑名单路径或敏感内容）
       与已跟踪文件（内容是否含可疑敏感模式）。
    2) 历史模式（-CheckHistory）：扫描全部 ref 可达历史 —— 黑名单路径是否仍在跟踪 /
       曾入历史（已剔除确认），敏感内容是否出现在任一历史提交中。

  命中分级：
    PATH  路径命中黑名单（.mcp.json / appsettings.Production.json / *.db / *.trx / dot 目录 / specs 等）
    HIGH  高置信敏感内容（随机密钥格式：sk-xxx / AKIA / ghp_ / 私钥头 / JWT 等，基本可判定为泄漏）
    LOW   低置信敏感内容（key=value 形态，可能是正常配置或示例，需人工复核）

  退出码：0 = 未发现问题；1 = 发现需处理项；2 = 执行错误。

.EXAMPLE
  # 仅工作区（快）
  .\scripts\check-git-content.ps1

  # 工作区 + 历史（慢，全量审计）
  .\scripts\check-git-content.ps1 -CheckHistory

  # 落盘报告（UTF-8），便于后续追溯
  .\scripts\check-git-content.ps1 -CheckHistory -ReportFile "$env:TEMP\git-audit-$(Get-Date -Format yyyyMMdd).txt"

.NOTES
  作者：桌面会话（2026-09-21，两轮历史清理后补的防再犯工具）
  注意：本脚本自身被 git 跟踪，模式串不会自命中（正则源不含真实密钥格式示例）。
  历史模式会扫描全部提交，仓库较大时较慢。
#>
[CmdletBinding()]
param(
  [switch]$CheckHistory,
  [string]$ReportFile = "",
  [switch]$SkipLowConfidence
)

$ErrorActionPreference = 'Continue'   # 原生命令 stderr 在 PS5.1 的 Stop 下会抛 NativeCommandError，git 调用需 Continue
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8; $OutputEncoding = [System.Text.Encoding]::UTF8 } catch {}

# ---------- 配置：黑名单路径（正则，git 路径以 / 分隔，锚定片段） ----------
$PathBlacklistRegEx = @(
  '(^|/)\.mcp\.json$',                       # MCP 凭据文件
  '(^|/)appsettings\.Production\.json$',     # 生产配置（含密钥）
  '\.env(\.|$)',                             # 环境变量文件（.env / .env.local / xxx.env）
  '\.(pem|pfx|key|crt|jks)$',                # 证书/私钥
  '\.(db|sqlite|sqlite3|trx)$',              # 数据库 / 测试结果
  '(^|/)package-lock\.json$',                # 锁文件（项目约定不入库）
  '(^|/)(dist|coverage|TestResults|node_modules|bin|obj|publish\.bak)(/|$)', # 构建/发布产物
  '(^|/)(\.agents|\.atomcode|\.forgeself|\.github|\.specify|\.vscode|openwiki|specs)(/|$)', # 历史清理的 dot 目录/文档目录
  '(^|/)\.temp(/|$)'                         # 临时/备份区（含历史重写 bundle）
)

# ---------- 配置：高置信敏感内容（随机密钥格式） ----------
$HighPatterns = @(
  'sk-[A-Za-z0-9]{20,}',                                   # OpenAI / DeepSeek 等 API key
  'AKIA[0-9A-Z]{16}',                                      # AWS Access Key
  'ghp_[A-Za-z0-9]{30,}',                                  # GitHub 个人访问令牌
  'github_pat_[A-Za-z0-9_]{20,}',                          # GitHub 细粒度令牌
  'xox[baprs]-[A-Za-z0-9-]{10,}',                          # Slack token
  '-----BEGIN [A-Z ]*PRIVATE KEY-----',                    # 私钥块头
  'eyJ[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{10,}' # JWT
)

# ---------- 配置：低置信敏感内容（key=value 形态，可能误报） ----------
$LowPatterns = @(
  '(?i)(password|passwd|pwd)\s*[:=]\s*["''][^"'']{4,}["'']',
  '(?i)(api[_-]?key|secret|access[_-]?key|client[_-]?secret)\s*[:=]\s*["''][A-Za-z0-9+/=_\-]{12,}["'']',
  '(?i)\btoken\s*[:=]\s*["''][A-Za-z0-9+/=_\-]{20,}["'']'
)

# ---------- 全局状态 ----------
$Findings = New-Object System.Collections.Generic.List[string]
$Counts = @{ PATH = 0; HIGH = 0; LOW = 0; INFO = 0 }

function Write-Finding {
  param([string]$Level, [string]$Message)
  $script:Findings.Add("[$Level] $Message")
  $script:Counts[$Level]++
  Write-Host "[$Level] $Message"
}
function Write-ReportLine {
  param([string]$Line)
  if ($ReportFile) { $Line | Add-Content -Encoding UTF8 $ReportFile }
}

# ---------- 工具函数 ----------
function Test-BlacklistPath {
  param([string]$Path)
  $p = $Path -replace '\\', '/'
  foreach ($rx in $PathBlacklistRegEx) {
    if ($p -match $rx) { return $rx }
  }
  return $null
}

function Test-TextFile {
  # 返回 true 表示可作文本扫描；二进制/过大跳过
  param([string]$Path)
  try {
    $info = Get-Item -LiteralPath $Path -ErrorAction Stop
    if ($info.Length -gt 2MB) { return $false }
    $bytes = [IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -eq 0) { return $false }
    # 前 8KB 含 NUL 即视为二进制
    $limit = [Math]::Min($bytes.Length, 8192)
    for ($i = 0; $i -lt $limit; $i++) { if ($bytes[$i] -eq 0) { return $false } }
    return $true
  } catch { return $false }
}

function Invoke-ContentScanFile {
  # 单文件内容扫描，返回命中字符串数组（行号: 模式级别: 截断内容）
  param([string]$Path, [string]$DisplayPath)
  $result = New-Object System.Collections.Generic.List[string]
  if (-not (Test-TextFile -Path $Path)) { return $result }
  try {
    $text = [IO.File]::ReadAllText($Path)   # 默认 UTF-8；含 BOM 亦正确
  } catch { return $result }
  $lines = $text -split "`n"
  for ($i = 0; $i -lt $lines.Count; $i++) {
    $line = $lines[$i].TrimEnd("`r")
    foreach ($p in $HighPatterns) {
      if ($line -match $p) {
        $snippet = if ($line.Length -gt 90) { $line.Substring(0, 90) + '…' } else { $line }
        $result.Add("HIGH`t$($i+1)`t$snippet")
        break
      }
    }
    if (-not $SkipLowConfidence) {
      foreach ($p in $LowPatterns) {
        if ($line -match $p) {
          $snippet = if ($line.Length -gt 90) { $line.Substring(0, 90) + '…' } else { $line }
          $result.Add("LOW`t$($i+1)`t$snippet")
          break
        }
      }
    }
  }
  return $result
}

# ---------- 主流程 ----------
$RepoRoot = & git rev-parse --show-toplevel 2>$null
if (-not $RepoRoot) {
  Write-Error "当前目录不在 git 仓库内：$PWD"
  exit 2
}
Set-Location -LiteralPath $RepoRoot

Write-Host "=== git 内容审计 === 仓库: $RepoRoot"
Write-ReportLine "=== git 内容审计 === 仓库: $RepoRoot | 时间: $(Get-Date -Format s)"
if ($ReportFile) { Write-ReportLine "报告文件: $ReportFile" }

# ---------- A. 当前跟踪层：已被 git 跟踪（= 已入库）且命中黑名单路径 ----------
$tracked = @(& git ls-files)
$trackedPathHits = 0
foreach ($t in $tracked) {
  $rx = Test-BlacklistPath -Path $t
  if ($rx) {
    Write-Finding 'PATH' "已入库且命中黑名单路径: $t   (规则: $rx)"
    $trackedPathHits++
  }
}
Write-Host "  [A] 已跟踪文件命中黑名单路径: $trackedPathHits"
Write-ReportLine "  [A] 已跟踪文件命中黑名单路径: $trackedPathHits"

# ---------- B. 工作区/暂存层：会被提交的不希望内容 ----------
$statusLines = @(& git status --porcelain)
$untrackedCount = 0; $untrackedWillCommit = 0
foreach ($line in $statusLines) {
  if ($line.Length -lt 4) { continue }
  $xy = $line.Substring(0, 2)
  $path = $line.Substring(3)
  # 处理引号包裹路径（含空格/特殊字符时 git 加引号；本项目罕见，简单处理）
  if ($path.StartsWith('"')) { $path = $path.Trim('"') -replace '\\"', '"' }

  if ($xy -like '??*' -or $xy[1] -eq '?') {
    # 未跟踪文件：是否会被提交（未被 .gitignore 忽略）+ 路径/内容检查
    $untrackedCount++
    & git check-ignore -q --no-index -- $path 2>$null
    $ignored = ($LASTEXITCODE -eq 0)
    if ($ignored) { continue }   # 被忽略 = 不会被提交，跳过
    $untrackedWillCommit++
    $rx = Test-BlacklistPath -Path $path
    if ($rx) {
      Write-Finding 'PATH' "未跟踪且会被提交，命中黑名单路径: $path   (规则: $rx)"
    }
    if (Test-Path -LiteralPath $path -PathType Leaf) {
      $hits = Invoke-ContentScanFile -Path $path -DisplayPath $path
      foreach ($h in $hits) {
        $parts = $h -split "`t", 3
        $level = if ($parts[0] -eq 'HIGH') { 'HIGH' } else { 'LOW' }
        Write-Finding $level "未跟踪会被提交，内容命中($level): $path : $($parts[1]) : $($parts[2])"
      }
    }
  }
  elseif ($xy[0] -eq 'D' -or $xy[1] -eq 'D') {
    continue   # 已删除文件无需扫描
  }
  else {
    # 已跟踪文件（M/A/R 等）：内容扫描工作区版本（即未来提交的样子）
    if (Test-Path -LiteralPath $path -PathType Leaf) {
      $hits = Invoke-ContentScanFile -Path $path -DisplayPath $path
      foreach ($h in $hits) {
        $parts = $h -split "`t", 3
        $level = if ($parts[0] -eq 'HIGH') { 'HIGH' } else { 'LOW' }
        Write-Finding $level "已跟踪文件内容命中($level): $path : $($parts[1]) : $($parts[2])"
      }
    }
  }
}
Write-Host "  [B] 未跟踪文件: $untrackedCount（其中会被提交: $untrackedWillCommit）"
Write-ReportLine "  [B] 未跟踪文件: $untrackedCount（其中会被提交: $untrackedWillCommit）"

# ---------- C. 历史层（-CheckHistory）：已提交内容审计 ----------
if ($CheckHistory) {
  $tmpDir = Join-Path $env:TEMP ("git-audit-" + [guid]::NewGuid().ToString('N'))
  New-Item -ItemType Directory -Force -Path $tmpDir | Out-Null
  try {
    # C1. 历史曾存在的黑名单路径（rev-list --objects 全量 blob 路径）
    $objFile = Join-Path $tmpDir 'objects.txt'
    & git rev-list --objects --all 2>$null | Set-Content -Encoding UTF8 $objFile
    $seenPath = New-Object System.Collections.Generic.HashSet[string]
    foreach ($l in (Get-Content -Encoding UTF8 $objFile)) {
      $sp = $l.IndexOf(' ')
      if ($sp -le 0) { continue }
      $p = $l.Substring($sp + 1)
      [void]$seenPath.Add($p)
    }
    $histPathHits = 0; $histPathCleared = 0
    foreach ($p in $seenPath) {
      $rx = Test-BlacklistPath -Path $p
      if (-not $rx) { continue }
      if ($tracked -contains $p) { continue }   # 已在 A 段报告
      # 历史曾存在但当前 index 已无 = 已清理
      Write-Finding 'INFO' "历史曾存在、当前已无（清理确认 ✓）: $p"
      $histPathCleared++
    }
    Write-Host "  [C1] 历史黑名单路径: 当前仍跟踪 $trackedPathHits / 已从 index 剔除 $histPathCleared"
    Write-ReportLine "  [C1] 历史黑名单路径: 当前仍跟踪 $trackedPathHits / 已从 index 剔除 $histPathCleared"

    # C2. 历史内容敏感模式（git grep 全提交）
    $allCommits = @(& git rev-list --all)
    if ($allCommits.Count -gt 0) {
      foreach ($scan in @(@{ Label = 'HIGH'; Patterns = $HighPatterns }, @{ Label = 'LOW'; Patterns = $LowPatterns })) {
        if ($scan.Label -eq 'LOW' -and $SkipLowConfidence) { continue }
        # 模式写入临时文件用 -f 读取，避免 Windows 命令行引号转义问题（含 " ' 的模式直传会 fatal）
        # 注意必须无 BOM（Set-Content -Encoding UTF8 在 PS5.1 会写 BOM，污染第一个模式导致失效）
        $patFile = Join-Path $tmpDir ("patterns-" + $scan.Label + '.txt')
        [IO.File]::WriteAllLines($patFile, $scan.Patterns, (New-Object Text.UTF8Encoding $false))
        $tmpOut = Join-Path $tmpDir ("grep-" + $scan.Label + '.txt')
        # 注意：git grep 语法为 `git grep [opts] <pattern> <rev>...`，rev 前不能加 `--`（`--` 后是 pathspec）
        & git grep -n -I -E -f $patFile @allCommits 2>$null | Set-Content -Encoding UTF8 $tmpOut
        $grepHits = @()
        if (Test-Path -LiteralPath $tmpOut) { $grepHits = @(Get-Content -Encoding UTF8 $tmpOut) }
        $hitCount = 0
        foreach ($g in $grepHits) {
          if (-not $g) { continue }
          # 格式: <sha>:<file>:<行号>:<内容>
          $c1 = $g.IndexOf(':'); if ($c1 -le 0) { continue }
          $sha = $g.Substring(0, $c1)
          $rest = $g.Substring($c1 + 1)
          $c2 = $rest.IndexOf(':'); if ($c2 -le 0) { continue }
          $file = $rest.Substring(0, $c2)
          $snippet = $rest.Substring($c2 + 1)
          if ($snippet.Length -gt 90) { $snippet = $snippet.Substring(0, 90) + '…' }
          $log = (& git log -1 --format='%h %s' $sha 2>$null)
          if ($scan.Label -eq 'HIGH') { Write-Finding 'HIGH' "历史内容命中: commit $log | $file : $snippet" }
          else { Write-Finding 'LOW' "历史内容命中(低置信): commit $log | $file : $snippet" }
          $hitCount++
          if ($hitCount -ge 200) {
            Write-Host "  (历史内容命中超过 200 条，停止列举；请人工核验)"
            Write-ReportLine "  (历史内容命中超过 200 条，停止列举；请人工核验)"
            break
          }
        }
        Write-Host "  [C2] 历史内容扫描($($scan.Label)): 命中 $hitCount 条"
        Write-ReportLine "  [C2] 历史内容扫描($($scan.Label)): 命中 $hitCount 条"
      }
    }
  } finally {
    Remove-Item -LiteralPath $tmpDir -Recurse -Force -ErrorAction SilentlyContinue
  }
}

# ---------- 汇总 ----------
Write-Host ""
Write-Host "=== 汇总 ==="
Write-Host "  路径命中(PATH): $($Counts.PATH)"
Write-Host "  高置信内容(HIGH): $($Counts.HIGH)"
Write-Host "  低置信内容(LOW): $($Counts.LOW)"
if ($CheckHistory) { Write-Host "  清理确认(INFO): $($Counts.INFO)" }
Write-ReportLine "=== 汇总: PATH=$($Counts.PATH) HIGH=$($Counts.HIGH) LOW=$($Counts.LOW) INFO=$($Counts.INFO)"

$total = $Counts.PATH + $Counts.HIGH + $Counts.LOW
if ($total -eq 0) {
  Write-Host "结论: 未发现不希望提交的内容（当前工作区/$(if ($CheckHistory) {'历史'} else {'未检查历史，可加 -CheckHistory 复核'}))"
  if ($ReportFile) { Write-ReportLine "结论: 未发现不希望提交的内容" }
  exit 0
} else {
  Write-Host "结论: 发现 $total 项需处理内容（HIGH/LOW 需人工复核，PATH 建议 gitignore 或移除）"
  if ($ReportFile) { Write-ReportLine "结论: 发现 $total 项需处理内容" }
  exit 1
}
