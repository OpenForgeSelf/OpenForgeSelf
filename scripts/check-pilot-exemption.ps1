[CmdletBinding()]
param(
    [string[]]$ChangedFiles = @(),
    [string]$ChangedFilesPath = '',
    [string]$OutUnexemptPath = '',
    [string]$RepoRoot = ''
)

$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }

if (-not $RepoRoot) {
    $RepoRoot = Split-Path -Parent $PSScriptRoot
}

$configPath = Join-Path $RepoRoot 'scripts/pilot-exemption-allowlist.json'
if (-not (Test-Path -LiteralPath $configPath)) {
    Write-Output "FAIL: Pilot 豁免配置缺失: scripts/pilot-exemption-allowlist.json"
    exit 2
}

try {
    $config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json -ErrorAction Stop
}
catch {
    Write-Output "FAIL: Pilot 豁免配置不是合法 JSON: scripts/pilot-exemption-allowlist.json"
    exit 2
}

$exemptions = @()
if (-not ($config.PSObject.Properties.Name -contains 'exemptions') -or
    $null -eq $config.exemptions -or
    $config.exemptions -is [string]) {
    Write-Output "FAIL: Pilot 豁免配置缺少有效的 exemptions 数组: scripts/pilot-exemption-allowlist.json"
    exit 2
}
else {
    foreach ($item in @($config.exemptions)) {
        if ($null -eq $item) { continue }
        if ($item -is [string]) {
            $exemptions += $item
        }
        elseif ($item.PSObject.Properties.Name -contains 'path') {
            $path = [string]$item.path
            if (-not [string]::IsNullOrWhiteSpace($path)) {
                $exemptions += $path
            }
        }
        else {
            Write-Output "FAIL: Pilot 豁免配置包含非法条目（须为字符串或含 path 字段的对象）"
            exit 2
        }
    }
}

if ($ChangedFilesPath) {
    if (-not (Test-Path -LiteralPath $ChangedFilesPath)) {
        Write-Output "FAIL: 变更文件清单不存在: $ChangedFilesPath"
        exit 2
    }
    $ChangedFiles = @([System.IO.File]::ReadAllLines($ChangedFilesPath) |
        ForEach-Object { $_.Trim().Replace('\', '/') } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

function ConvertTo-GlobRegex {
    param([string]$Glob)

    $g = ($Glob.Trim() -replace '\\', '/').TrimEnd('/')
    if ($g -eq '') {
        return '^$'
    }

    $sb = [System.Text.StringBuilder]::new('^')
    $i = 0
    while ($i -lt $g.Length) {
        $c = $g[$i]
        if ($c -eq '*') {
            if ($i + 1 -lt $g.Length -and $g[$i + 1] -eq '*') {
                if ($i + 2 -lt $g.Length -and $g[$i + 2] -eq '/') {
                    [void]$sb.Append('(?:.*/)?')
                    $i += 3
                    continue
                }
                [void]$sb.Append('.*')
                $i += 2
                continue
            }
            [void]$sb.Append('[^/]*')
            $i++
            continue
        }
        if ($c -eq '?') {
            [void]$sb.Append('[^/]')
            $i++
            continue
        }
        [void]$sb.Append([regex]::Escape([string]$c))
        $i++
    }
    [void]$sb.Append('$')
    return $sb.ToString()
}

function Test-PilotExempt {
    param([string]$File)

    foreach ($pattern in $exemptions) {
        $regex = ConvertTo-GlobRegex $pattern
        if ($File -match $regex) {
            return $true
        }
    }
    return $false
}

$ChangedFiles = @($ChangedFiles | ForEach-Object {
    ([string]$_).Trim().Replace('\', '/')
} | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })

$unexempt = @($ChangedFiles | Where-Object { -not (Test-PilotExempt $_) })

if ($OutUnexemptPath) {
    $parent = Split-Path -Parent $OutUnexemptPath
    if ($parent -and -not (Test-Path -LiteralPath $parent)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }
    [System.IO.File]::WriteAllLines($OutUnexemptPath, $unexempt, [System.Text.UTF8Encoding]::new($false))
}

if ($unexempt.Count -eq 0) {
    Write-Output "PASS: 所有变更文件均在 Pilot 豁免白名单内"
    exit 0
}

Write-Output "FAIL: 以下变更文件不在 Pilot 豁免白名单内:"
$unexempt | ForEach-Object { Write-Output "    $_" }
exit 1
