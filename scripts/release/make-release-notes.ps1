# make-release-notes.ps1 - generates RELEASE-NOTES.md for a tag.
# Sources, in priority order:
#   1) tag annotation text (git tag -a -m ...) as the headline
#   2) commit subjects between the previous tag and this tag (needs full history, fetch-depth 0)
#   3) fallback placeholder line
#
# Example:
#   ./make-release-notes.ps1 -Version v0.1.0 -OutFile artifacts/release/RELEASE-NOTES.md

[CmdletBinding()]
param(
    [string]$RepoRoot = '',
    [string]$Version = '',
    [string]$OutFile = ''
)

. (Join-Path $PSScriptRoot 'release-lib.ps1')
if (-not $RepoRoot) { $RepoRoot = Get-ReleaseRepoRoot }
if (-not $Version) { throw 'make-release-notes: -Version is required (e.g. v0.1.0)' }
$ver = Get-NormalizedVersion $Version
$tag = "v$ver"
if (-not $OutFile) { $OutFile = Join-Path $RepoRoot ("artifacts/release/RELEASE-NOTES-{0}.md" -f $ver) }

Push-Location $RepoRoot
try {
    $annotation = ''
    git tag -l --format='%(contents)' $tag 2>$null | ForEach-Object { $annotation += $_ + "`r`n" }

    $prev = ''
    # 本地打包（release-local 不打 git tag）时 $tag 不存在：跳过 prev 查找，fallback 用 HEAD。
    $tagExists = $false
    try { $tagExists = [bool]((& git tag -l $tag 2>$null) -join '').Trim() } catch { $tagExists = $false }
    if ($tagExists) {
        try {
            $candidate = (& git describe --tags --abbrev=0 "$tag^" 2>$null)
            if ($LASTEXITCODE -eq 0 -and $candidate) { $prev = $candidate.Trim() }
        } catch { $prev = '' }
    }

    # git 输出是 UTF-8；PowerShell 5.1 默认按控制台 OEM 码页（本环境 = GBK 936）解码子进程 stdout，
# 中文提交标题会在捕获瞬间被烤成乱码再写进 RELEASE-NOTES（宿主只是原样显示）。故捕获期间强制 UTF-8，用完复位。
function Invoke-GitUtf8 {
    param([Parameter(Mandatory = $true, ValueFromRemainingArguments = $true)] [string[]] $GitArgs)
    $prev = $null
    try {
        $prev = [Console]::OutputEncoding
        [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
        & git @GitArgs
    } finally {
        if ($null -ne $prev) { [Console]::OutputEncoding = $prev }
    }
}

$lines = @()
    $lines += "# OpenForgeSelf $tag"
    $lines += ''
    if ($annotation.Trim()) {
        $lines += $annotation.Trim()
        $lines += ''
    }
    if ($prev) {
        $lines += "## Changes since $prev"
        $log = Invoke-GitUtf8 log --no-merges --pretty=format:'- %s (%h)' "$prev..$tag"
        if ($log) { $lines += $log }
    }
    else {
        # 无 tag（本地打包）：取最近 20 条提交（HEAD）
        $log = Invoke-GitUtf8 log --no-merges -20 --pretty=format:'- %s (%h)' HEAD
        if ($log) {
            $lines += '## Recent commits'
            $lines += $log
        }
    }
    if ($lines.Count -le 2) {
        $lines += 'Initial packaged build (auto-generated release).'
    }
}
finally { Pop-Location }

New-Item -ItemType Directory -Force -Path (Split-Path $OutFile -Parent) | Out-Null
Set-Content -Path $OutFile -Value ($lines -join "`r`n") -Encoding utf8
Write-Host "make-release-notes: wrote $OutFile"
