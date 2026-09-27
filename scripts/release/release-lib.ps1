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
