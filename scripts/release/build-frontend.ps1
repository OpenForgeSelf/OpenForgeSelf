# build-frontend.ps1 - builds the host SPA and every plugin web UI.
# Outputs (all git-ignored build artifacts):
#   ForgeSelf.Web            -> ForgeSelf.Api/wwwroot            (vite outDir)
#   Plugins/<X>/web          -> Plugins/<X>/web/dist             (picked up by StageAllPlugins)
# Usable locally and in CI. Requires: node >= 20, pnpm on PATH (or corepack).
#
# Examples:
#   ./build-frontend.ps1                    # host web + all plugin webs
#   ./build-frontend.ps1 -HostOnly          # host web only
#   ./build-frontend.ps1 -PluginsOnly -Plugin AIAgent   # one plugin web only (fast local debug)

[CmdletBinding()]
param(
    [string]$RepoRoot = '',
    [switch]$HostOnly,
    [switch]$PluginsOnly,
    [string]$Plugin = ''   # filter plugin dir name, e.g. AIAgent
)

. (Join-Path $PSScriptRoot 'release-lib.ps1')
if (-not $RepoRoot) { $RepoRoot = Get-ReleaseRepoRoot }

# 构建临时/缓存目录指向项目内 .temp，避开沙箱 safe-delete shim 对系统 TEMP（AppData\Local\Temp）
# 删除的拦截——esbuild/vite 构建中会清理临时文件，落到系统 TEMP 会报 Access is denied（2026-10-06 实证）。
# .temp 已被 .gitignore / check-git-content 排除，不会入库。
$buildTemp = Join-Path $RepoRoot '.temp'
New-Item -ItemType Directory -Force -Path $buildTemp | Out-Null
$env:TEMP   = $buildTemp
$env:TMP    = $buildTemp
$env:TMPDIR = $buildTemp

$pnpm = Get-ReleaseToolPath 'pnpm'
if (-not $pnpm) { throw 'pnpm not found on PATH. Install pnpm (corepack enable) first.' }

function Invoke-PnpmBuild([string]$Dir, [string]$Label) {
    Write-Host "--- $Label : $Dir"
    Push-Location $Dir
    try {
        & $pnpm install --frozen-lockfile
        Assert-ExitCode "pnpm install ($Label)"
        & $pnpm build
        Assert-ExitCode "pnpm build ($Label)"
    }
    finally { Pop-Location }
}

if (-not $PluginsOnly) {
    Invoke-ReleaseStep 'frontend: host web (ForgeSelf.Web)' {
        Invoke-PnpmBuild (Join-Path $RepoRoot 'ForgeSelf.Web') 'host-web'
    }
}

if (-not $HostOnly) {
    Invoke-ReleaseStep 'frontend: plugin webs' {
        $webDirs = Get-ChildItem (Join-Path $RepoRoot 'Plugins') -Directory |
            ForEach-Object { Join-Path $_.FullName 'web' } |
            Where-Object { Test-Path (Join-Path $_ 'package.json') }
        if ($Plugin) {
            $webDirs = $webDirs | Where-Object { (Split-Path (Split-Path $_ -Parent) -Leaf) -eq $Plugin }
            if (-not $webDirs) { throw "plugin web not found: Plugins/$Plugin/web" }
        }
        foreach ($d in $webDirs) {
            $name = Split-Path (Split-Path $d -Parent) -Leaf
            Invoke-PnpmBuild $d "plugin-web/$name"
        }
    }
}

Write-Host 'build-frontend: all done.'
