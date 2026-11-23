# 发布原生自更新代理 update-agent.exe（2026-11-21 输入15）。
# 用法: pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/release/publish-updateagent.ps1 [-Configuration Release] [-OutputDir <dir>]
# 形态: net10.0/win-x64 FDD（依赖框架）单文件、零 NuGet（BCL only）。
# 大小断言防误打自包含：自包含单文件 ~70MB+，FDD 应 < 8MB；超限即 fail，防止把整包运行时打进代理。
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$OutputDir = ''
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$project = Join-Path $repoRoot 'ForgeSelf.UpdateAgent/ForgeSelf.UpdateAgent.csproj'
if (-not (Test-Path $project)) { throw "找不到更新代理工程: $project" }
if (-not $OutputDir) { $OutputDir = Join-Path $repoRoot ".temp/publish-updateagent/$Configuration" }

Write-Host "[publish-updateagent] dotnet publish (FDD single-file, win-x64) ..."
& dotnet publish $project `
    -c $Configuration `
    -r win-x64 `
    --self-contained false `
    -p:PublishSingleFile=true `
    -o $OutputDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish 失败，退出码 $LASTEXITCODE" }

$exe = Join-Path $OutputDir 'update-agent.exe'
if (-not (Test-Path $exe)) { throw "publish 产物缺 update-agent.exe: $OutputDir" }

# 大小断言：FDD 单文件（BCL only）远小于自包含体积；超 8MB 视为误打自包含
$sizeMB = [math]::Round((Get-Item $exe).Length / 1MB, 2)
if ($sizeMB -gt 8) {
    throw "update-agent.exe 体积 $sizeMB MB 超 8MB 上限——疑似误打成 self-contained，拒绝交付（应为 FDD 依赖框架单文件）"
}

Write-Host "[publish-updateagent] OK: $exe ($sizeMB MB)"
Write-Output $exe