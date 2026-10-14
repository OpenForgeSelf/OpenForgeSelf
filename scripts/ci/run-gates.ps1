#!/usr/bin/env pwsh
<#
.SYNOPSIS
  CI / 本地共用的门禁总入口：解析 diff 基线 → 依次跑各门禁 → 汇总退出码。
.DESCRIPTION
  workflow 只负责装工具链并调用本脚本；逻辑全在这里，本地与 CI 同一入口。
  门禁：pilot（必过）、risk（信息性）、style（必过）。
  基线解析顺序：-Base > $env:INPUT_BASE > 事件 JSON（PR base / push before）
                > 本地上游 merge-base > HEAD~1。
  基线或 Head 不是有效提交 → 硬失败（exit 2），不静默放行。
.EXAMPLE
  pwsh scripts/ci/run-gates.ps1
  pwsh scripts/ci/run-gates.ps1 -Base a00e45d -Only pilot
#>
[CmdletBinding()]
param(
    [string]$Base = '',
    [string]$Head = 'HEAD',
    [ValidateSet('all', 'pilot', 'risk', 'style')][string]$Only = 'all'
)
$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Set-Location -LiteralPath $repo

function Test-Commit([string]$ref) {
    git cat-file -e "$ref^{commit}" 2>$null
    return ($LASTEXITCODE -eq 0)
}

function Resolve-Base {
    if ($Base) { return @{ value = $Base; src = '-Base 参数' } }
    if ($env:INPUT_BASE) { return @{ value = $env:INPUT_BASE; src = 'workflow_dispatch 输入' } }
    if ($env:GITHUB_EVENT_PATH -and (Test-Path -LiteralPath $env:GITHUB_EVENT_PATH)) {
        $ev = Get-Content -LiteralPath $env:GITHUB_EVENT_PATH -Raw | ConvertFrom-Json
        if ($ev.pull_request.base.sha) { return @{ value = $ev.pull_request.base.sha; src = 'PR base' } }
        if ($ev.before -and $ev.before -notmatch '^0+$') { return @{ value = $ev.before; src = 'push before' } }
    }
    $up = git rev-parse --abbrev-ref --symbolic-full-name '@{upstream}' 2>$null
    if ($LASTEXITCODE -eq 0 -and $up) {
        $mb = git merge-base $Head $up 2>$null
        if ($LASTEXITCODE -eq 0 -and $mb) { return @{ value = $mb; src = "merge-base($up)" } }
    }
    return @{ value = 'HEAD~1'; src = '回退 HEAD~1' }
}

$r = Resolve-Base
Write-Output "diff 基线: $($r.value)（来源: $($r.src)）"
foreach ($ref in @($r.value, $Head)) {
    if (-not (Test-Commit $ref)) {
        Write-Output "ERROR: '$ref' 不是有效提交，无法确认改动范围，拒绝放行。"
        Write-Output "       强推后请用 workflow_dispatch 手动指定 base，或本地传 -Base。"
        exit 2
    }
}
Write-Output ("Head: {0}  tree: {1}" -f (git rev-parse $Head), (git rev-parse "$Head^{tree}"))

$results = [ordered]@{}
function Invoke-Gate([string]$name, [bool]$required, [scriptblock]$run) {
    Write-Output ''
    Write-Output ("##### 门禁: {0}{1}" -f $name, $(if ($required) { '' } else { '（信息性）' }))
    $code = 99
    try { & $run; $code = $LASTEXITCODE } catch { Write-Output "ERROR: $($_.Exception.Message)" }
    $results[$name] = @{ code = $code; required = $required }
}

if ($Only -in 'all', 'pilot') {
    Invoke-Gate 'pilot-gate' $true {
        pwsh -NoProfile -File scripts/verify-pilot-gate.ps1 -Base $r.value -Head $Head
    }
}
if ($Only -in 'all', 'risk') {
    Invoke-Gate 'risk-level' $false {
        pwsh -NoProfile -File scripts/classify-risk.ps1 -Base $r.value -Head $Head
    }
}
if ($Only -in 'all', 'style') {
    Invoke-Gate 'style-tokens' $true { node scripts/check-style-tokens.mjs }
}

Write-Output ''
Write-Output '===== 汇总 ====='
$fail = 0
foreach ($k in $results.Keys) {
    $x = $results[$k]
    $tag = if ($x.code -eq 0) { 'PASS' } elseif ($x.required) { 'FAIL' } else { 'WARN' }
    Write-Output ("{0,-14} {1}  (exit {2})" -f $k, $tag, $x.code)
    if ($x.required -and $x.code -ne 0) { $fail++ }
}
exit $(if ($fail -gt 0) { 1 } else { 0 })
