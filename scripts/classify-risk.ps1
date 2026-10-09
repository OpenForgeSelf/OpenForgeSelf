#!/usr/bin/env pwsh
<#
.SYNOPSIS
  对一次改动的 diff 机械判定最低风险级（L0~L4）。

.DESCRIPTION
  依据 docs/04-standards/risk-policy.md §3 触发表。只上调不下调：取所有命中规则的最高级别。
  内容触发（关键词出现在 diff 的增删行）至少 L3；规模触发（>10 文件 或 >300 行）上调一级，最高 L3。
  本脚本属 L4 宪法层，Agent 不得自行修改。

.EXAMPLE
  pwsh scripts/classify-risk.ps1 -Base origin/main
  pwsh scripts/classify-risk.ps1 -Staged -Format json
#>
[CmdletBinding()]
param(
    [string]$Base = '',
    [string]$Head = 'WORKTREE',
    [switch]$Staged,
    [ValidateSet('text', 'json')][string]$Format = 'text',
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

if (-not $Base) { $Base = 'HEAD~1' }
if ($Staged) { $diffArgs = @('diff', '--cached') }
elseif ($Head -eq 'WORKTREE') { $diffArgs = @('diff', $Base) }
else { $diffArgs = @('diff', "$Base..$Head") }

# ---- 收集改动文件 ----
$files = @()
foreach ($line in (Invoke-Git @($diffArgs + @('--name-status', '--diff-filter=ACMR')))) {
    if (-not $line) { continue }
    $parts = $line -split "`t"
    if ($parts.Count -ge 2) { $files += ($parts[-1] -replace '\\', '/') }
}

# 未跟踪的新文件也必须参与判级（git diff 不包含未跟踪文件）
foreach ($u in (Invoke-Git @('ls-files', '--others', '--exclude-standard'))) {
    if ($u) { $files += ($u -replace '\', '/') }
}
$files = @($files | Sort-Object -Unique)

# ---- 触发表（顺序敏感：先具体后一般） ----
$rules = @(
    @{ p = '^openwiki/';                                                    l = -1; r = 'openwiki 由 CI 生成，禁止手改' }
    @{ p = '^AGENTS\.md$';                                                  l = 4; r = 'AGENTS.md（宪法层）' }
    @{ p = '^docs/04-standards/ai-native-engineering-workflow\.md$';        l = 4; r = '流程规范（宪法层）' }
    @{ p = '^docs/04-standards/risk-policy\.md$';                           l = 4; r = '风险策略（宪法层）' }
    @{ p = '^docs/18-templates/';                                           l = 4; r = '模板目录' }
    @{ p = '^scripts/hooks/';                                               l = 4; r = 'git hooks' }
    @{ p = '^scripts/(verify-pilot-artifacts|install-git-hooks|classify-risk|verify-pilot-gate)\.ps1$'; l = 4; r = '闸门脚本' }
    @{ p = '(^|/)appsettings\.Production\.json$';                           l = 4; r = '生产配置' }
    @{ p = '^\.env';                                                        l = 4; r = '敏感路径' }

    @{ p = '(^|/)plugin\.json$';                                            l = 3; r = '插件契约' }
    @{ p = '^ForgeSelf\.Api/(Entities|Security|Data)/';                     l = 3; r = '数据模型/鉴权/加密' }
    @{ p = '^ForgeSelf\.Api/(AppBuilder|Program|StartupPortResolver|WindowsService)\.cs$'; l = 3; r = '宿主装配' }
    @{ p = '^ForgeSelf\.Api/[^/]+\.csproj$';                                l = 3; r = '构建配置' }
    @{ p = '^ForgeSelf\.Web/package\.json$';                                l = 3; r = '构建配置' }
    @{ p = 'pnpm-lock\.yaml$';                                             l = 3; r = '锁文件' }
    @{ p = '^ForgeSelf\.Web/e2e/(global-setup|global-teardown)\.ts$';       l = 3; r = 'e2e 全局装配' }
    @{ p = '^ForgeSelf\.Web/e2e/fixtures/';                                 l = 3; r = 'e2e 夹具' }
    @{ p = '^ForgeSelf\.Web/playwright\..*\.config\.ts$';                   l = 3; r = 'e2e 配置' }
    @{ p = '^scripts/release/';                                             l = 3; r = '发布链' }
    @{ p = '^scripts/(sign-publish|update-agent|package-plugin|publish-plugin|publish-plugin-full|build-plugin-web|dev-plugin-web)\.ps1$'; l = 3; r = '发布/打包脚本' }
    @{ p = '^\.github/workflows/';                                          l = 3; r = 'CI 工作流' }
    @{ p = '^Plugins/[^/]+/[^/]+\.csproj$';                                 l = 3; r = '插件构建配置' }
    @{ p = '^Plugins/DesignSystem/web/src/(design|tokens)/';                l = 3; r = '设计系统全局 token' }

    @{ p = '^ForgeSelf\.Web/src/(router|stores|components)/';               l = 2; r = '路由/公共组件/store 契约' }
    @{ p = '^ForgeSelf\.Web/src/styles/';                                   l = 2; r = '全局样式' }
    @{ p = '^Plugins/[^/]+/(Controllers|Services)/';                        l = 2; r = '插件内部 API' }
    @{ p = '^docs/03-design/';                                              l = 2; r = '交互模式库' }
    @{ p = '^\.agents/skills/';                                             l = 2; r = '技能' }
    @{ p = '^docs/04-standards/agent-workflow\.md$';                        l = 2; r = '规则库' }
    @{ p = '^ForgeSelf\.Api/';                                              l = 2; r = '后端默认' }

    @{ p = '^ForgeSelf\.Web/src/';                                          l = 1; r = '前端页面/逻辑' }
    @{ p = '^Plugins/[^/]+/web/src/';                                       l = 1; r = '插件前端' }
    @{ p = '^ForgeSelf\.(Api|Core|Abstractions)\.Tests/';                   l = 1; r = '测试' }
    @{ p = '^ForgeSelf\.(Core|Abstractions|Bootstrapper)/';                 l = 1; r = '内核/抽象/引导' }
    @{ p = '^forgeself-design/';                                            l = 1; r = '设计原型' }
    @{ p = '^ForgeSelf\.Web/e2e/';                                          l = 1; r = 'e2e 用例' }
    @{ p = '^scripts/';                                                     l = 1; r = '脚本' }

    @{ p = '^docs/';                                                        l = 0; r = '文档' }
    @{ p = '^TODO\.md$';                                                    l = 0; r = '队列' }
    @{ p = '^README';                                                       l = 0; r = '说明文档' }
    @{ p = '^\.forgeself/';                                                 l = 0; r = '会话日志' }
    @{ p = '\.md$';                                                         l = 0; r = 'markdown' }
)

$school = @{}   # level -> filenames
$forbidden = @()
$unmatched = @()
foreach ($f in $files) {
    $hit = $null
    foreach ($rule in $rules) {
        if ($f -match $rule.p) { $hit = $rule; break }
    }
    if (-not $hit) { $unmatched += $f; $hit = @{ l = 2; r = '未匹配（保守 L2）' } }
    if ($hit.l -lt 0) { $forbidden += $f; continue }
    $lv = [int]$hit.l
    if (-not $school.ContainsKey($lv)) { $school[$lv] = @() }
    $school[$lv] += "$f [$($hit.r)]"
}

$maxLevel = 0
if ($school.Keys.Count -gt 0) { $maxLevel = ($school.Keys | Measure-Object -Maximum).Maximum }

# ---- 内容触发 -> 至少 L3 ----
$contentPat = '(Authorize|ApiKey|AES|Encrypt|Password|Secret|DROP\s|ALTER\s|Process\.Kill|Process\.Start|File\.Delete|Directory\.Delete|Remove-Item -Recurse|git push|--force)'
$contentHits = @()
$diffText = Invoke-Git @($diffArgs + @('-U0'))
foreach ($line in $diffText) {
    if ($line -notmatch '^[+-]') { continue }
    if ($line -match '^(\+\+\+|---)') { continue }
    $m = [regex]::Matches($line, $contentPat)
    foreach ($x in $m) { $contentHits += $x.Value }
}
$contentHits = @($contentHits | Sort-Object -Unique)
if ($contentHits.Count -gt 0 -and $maxLevel -lt 3) { $maxLevel = 3 }

# ---- 规模触发 -> 上调一级（最高 L3） ----
$fileCount = $files.Count
$lineCount = 0
foreach ($l in (Invoke-Git @($diffArgs + @('--numstat')))) {
    if (-not $l) { continue }
    $c = $l -split "`t"
    foreach ($v in $c[0..1]) { if ($v -match '^\d+$') { $lineCount += [int]$v } }
}
$sizeTriggered = ($fileCount -gt 10 -or $lineCount -gt 300)
if ($sizeTriggered -and $maxLevel -lt 3) { $maxLevel = [Math]::Min(3, $maxLevel + 1) }

$levelName = "L$maxLevel"
if ($maxLevel -ge 4) { $levelName = 'L4' }

# ---- 输出 ----
if ($Format -eq 'json') {
    $obj = [ordered]@{
        level          = $levelName
        fileCount      = $fileCount
        lineCount      = $lineCount
        sizeTriggered  = $sizeTriggered
        contentHits    = $contentHits
        forbidden      = $forbidden
        unmatched      = $unmatched
        byLevel        = $school
        base           = $Base
        head           = $(if ($Staged) { 'STAGED' } else { $Head })
    }
    $obj | ConvertTo-Json -Depth 6
}
else {
    Write-Output "== classify-risk =="
    Write-Output ("范围: {0}..{1}  改动 {2} 文件 / {3} 行（含增删）" -f $Base, $(if ($Staged) { 'STAGED' } else { $Head }), $fileCount, $lineCount)
    if ($files.Count -eq 0) { Write-Output '无改动 -> L0'; exit 0 }
    foreach ($k in ($school.Keys | Sort-Object)) { Write-Output ("  L{0}: {1} 个文件" -f $k, $school[$k].Count) }
    if ($contentHits.Count -gt 0) { Write-Output ("内容触发（>=L3）: {0}" -f ($contentHits -join ', ')) }
    if ($sizeTriggered) { Write-Output '规模触发: 上调一级（最高 L3）' }
    if ($unmatched.Count -gt 0) { Write-Output ("未匹配（保守 L2）: {0}" -f ($unmatched -join ', ')) }
    Write-Output ""
    Write-Output "结论: $levelName"
    if ($forbidden.Count -gt 0) {
        Write-Output ("[VIOLATION] 命中禁止手改路径: {0}" -f ($forbidden -join ', '))
    }
    if ($levelName -eq 'L4') {
        Write-Output '提示: L4 只能由人执行；Agent 只出方案（AGENTS.md §2.2 / §3.8）。'
    }
}

if ($forbidden.Count -gt 0) { exit 2 }
exit 0
