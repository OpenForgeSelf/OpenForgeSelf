# release-lib.ps1 - shared helpers for the release scripts.
# Dot-sourced by every script under scripts/release/. No side effects on import.
# Works on Windows PowerShell 5.1 and PowerShell 7+ (local debug + GitHub Actions windows runner).

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-ReleaseRepoRoot {
    # scripts/release/ -> repo root
    return (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
}

function Invoke-ReleaseStep {
    param([string]$Name, [scriptblock]$Body)
    Write-Host ""
    Write-Host "==> $Name"
    $t0 = Get-Date
    & $Body
    $dt = ((Get-Date) - $t0).TotalSeconds
    Write-Host ("<== {0} ok ({1:n1}s)" -f $Name, $dt)
}

function Assert-ExitCode {
    param([string]$What)
    if ($LASTEXITCODE -ne 0) {
        throw "$What failed with exit code $LASTEXITCODE"
    }
}

function Get-ReleaseToolPath {
    # Resolve an external tool: PATH first, then a known install fallback.
    param([string]$Name, [string]$FallbackDir = '')
    $cmd = Get-Command $Name -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    if ($FallbackDir) {
        $p = Join-Path $FallbackDir "$Name.exe"
        if (Test-Path $p) { return (Resolve-Path $p).Path }
    }
    return $null
}

function Get-NormalizedVersion {
    param([string]$Version)
    # 'v0.1.0' / '0.1.0' -> '0.1.0'
    return $Version.TrimStart('v', 'V')
}

function Get-FullReleaseVersion {
    param([string]$Version)
    # 发行串规则（2026-10-02，真源 docs/04-standards/packaging-upgrade-backup.md §1.1）：
    #   发行串 = <major>.<minor>.<patch>.<yyMMddHHmm>（例：2.3.0.2609161125）
    #   - 3 段输入（2.3.0 / v2.3.0）→ 自动补本地时间 yyMMddHHmm（10 位，唯一、单调）
    #   - 4 段输入（已含时间码）→ 原样返回（幂等，重复执行不会叠加第二个时间码）
    #   - 可选预发布后缀（-preview / -rc.1）：作「预览版」标识，只留在 tag / zip 名 / Release 页，
    #     不进 PE 文件版本（AssemblyFileVersion 只接受纯数字段）→ 注入用 Get-ReleaseVersionInjectible
    #   - 其它段数 → 抛错（不静默兜底，避免产出无法解释的版本串）
    $v = Get-NormalizedVersion $Version
    $suffix = ''
    if ($v -match '^(?<base>[^-]+)-(?<pre>.+)$') {
        $v = $Matches['base']
        $suffix = '-' + $Matches['pre']
    }
    $segments = $v.Split('.')
    if ($segments.Count -eq 3) {
        return ('{0}.{1}{2}' -f $v, (Get-Date).ToString('yyMMddHHmm'), $suffix)
    }
    if ($segments.Count -eq 4) {
        return ($v + $suffix)
    }
    throw "invalid version '$Version': expect 3 segments (e.g. 2.3.0) or 4 segments with date code (e.g. 2.3.0.2609161125), optional -preview suffix"
}

function Test-ReleaseVersionInjectible {
    param([string]$Version)
    # 只有纯数字 3/4 段发行串才注入版本：
    # 本地 dry run 默认串（0.0.0-local）含预发布后缀，作 AssemblyFileVersion 非法，故不注入，
    # 交给 csproj 的兜底日期版本机制（2.3.0.<yyMMddHHmm>）生成。
    return ($Version -match '^\d+\.\d+\.\d+(\.\d+)?$')
}

function Get-ReleaseVersionInjectible {
    param([string]$Version)
    # 交给 exe csproj（环境变量 FORGESELF_RELEASE_VERSION）的注入串：
    #   - 纯数字 3/4 段（2.3.0.2609161125）→ 原样
    #   - 带预发布后缀（2.3.0.2609161125-preview）→ 只注入数字前半段（PE 文件版本不能带后缀），
    #     后缀保留在 tag / zip 名 / Release 页；预览版语义由 GitHub prerelease 标识承载
    #   - 其它（0.0.0-local 等）→ $null（不注入，走 csproj 兜底）
    if (Test-ReleaseVersionInjectible $Version) { return $Version }
    if ($Version -match '^(?<base>\d+\.\d+\.\d+(?:\.\d+)?)-[0-9A-Za-z][0-9A-Za-z.\-]*$') { return $Matches['base'] }
    return $null
}

function Get-CsprojReleaseVersion {
    param(
        [string]$RepoRoot = '',
        [string]$CsprojPath = ''
    )
    # 版本行真源读取器（PILOT-076，2026-10-30 输入3 用户拍板）：
    #   ForgeSelf.Api.csproj 的 VersionPrefix 属性承载三段发行线（如 2.4.22）；release-local.ps1 本地缺省
    #   （不传 -Version 且无 GITHUB_REF_NAME）读这里再补时间码 → 发布 zip 不需指定版本。
    #   版本行 bump 必须经用户允许（agent 不得擅自改），真源 docs/04-standards/packaging-upgrade-backup.md §1.1/§4-R10。
    # 入参：-RepoRoot（真实仓库用）或 -CsprojPath（测试夹具/单文件用）至少给一，CsprojPath 优先。
    # 解析「固定的部分」= 面值的前三个数字段（2.4.22.9 → 2.4.22；两段/缺标签/缺文件 → 显式抛错不静默兜底）。
    # 约定：csproj 注释里不得出现字面的 VersionPrefix 标签对（本函数取首个标签对，注释会被误配）；
    # 报错文案保持 ASCII——子进程 stderr 捕获不做码页假设（PC-011 lens）。
    if (-not $CsprojPath) {
        if (-not $RepoRoot) {
            throw "Get-CsprojReleaseVersion: either -RepoRoot or -CsprojPath is required"
        }
        $CsprojPath = Join-Path $RepoRoot 'ForgeSelf.Api/ForgeSelf.Api.csproj'
    }
    if (-not (Test-Path -LiteralPath $CsprojPath)) {
        throw "Get-CsprojReleaseVersion: csproj not found: $CsprojPath"
    }
    $text = Get-Content -LiteralPath $CsprojPath -Raw
    if ($text -match '(?s)<VersionPrefix>\s*(?<v>[^<]*?)\s*</VersionPrefix>') {
        $raw = $Matches['v'].Trim()
        $segments = $raw -split '\.'
        $numeric = 0
        foreach ($s in $segments) { if ($s -match '^\d+$') { $numeric++ } else { break } }
        if ($numeric -ge 3) {
            return ('{0}.{1}.{2}' -f $segments[0], $segments[1], $segments[2])
        }
        throw ("Get-CsprojReleaseVersion: VersionPrefix value '{0}' in {1} must carry the full 3-segment release line (e.g. 2.4.22); bump requires user approval" -f $raw, $CsprojPath)
    }
    throw ("Get-CsprojReleaseVersion: VersionPrefix tag not found in {0}" -f $CsprojPath)
}
