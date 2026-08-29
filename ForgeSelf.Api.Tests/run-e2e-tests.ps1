#Requires -Version 5.1
<#
.SYNOPSIS
    运行 E2E 测试
.DESCRIPTION
    运行 ForgeSelf.Api.Tests 项目中的 E2E 测试
.PARAMETER Configuration
    构建配置 (Debug/Release)，默认为 Debug
.PARAMETER Parallel
    是否并行运行测试
.PARAMETER Diagnostic
    是否启用诊断输出
#>

param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    
    [switch]$Parallel,
    
    [switch]$Diagnostic
)

$ErrorActionPreference = 'Stop'
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$TestProjectPath = Join-Path $ProjectRoot 'ForgeSelf.Api.Tests\ForgeSelf.Api.Tests.csproj'

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  ForgeSelf.Api E2E 测试运行器" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "配置: $Configuration" -ForegroundColor Gray
Write-Host "测试项目: $TestProjectPath" -ForegroundColor Gray
Write-Host "测试类型: E2E" -ForegroundColor Gray
Write-Host ""

# 检查项目是否存在
if (-not (Test-Path $TestProjectPath)) {
    Write-Error "测试项目不存在: $TestProjectPath"
    exit 1
}

# 首先构建项目
Write-Host "正在构建项目..." -ForegroundColor Yellow
$BuildArgs = @(
    'build',
    $TestProjectPath,
    '--configuration', $Configuration,
    '--no-restore'
)

try {
    & dotnet @BuildArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Error "项目构建失败"
        exit 1
    }
    Write-Host "构建成功" -ForegroundColor Green
}
catch {
    Write-Error "构建时发生错误: $_"
    exit 1
}

Write-Host ""
Write-Host "正在运行 E2E 测试..." -ForegroundColor Yellow
Write-Host ""

# 构建 dotnet test 命令
$TestArgs = @(
    'test',
    $TestProjectPath,
    '--configuration', $Configuration,
    '--no-build',
    '--verbosity', 'normal',
    '--filter', 'FullyQualifiedName~E2E'
)

if ($Parallel) {
    $TestArgs += '--parallel'
}

if ($Diagnostic) {
    $TestArgs += '--verbosity', 'diagnostic'
}

try {
    & dotnet @TestArgs
    $exitCode = $LASTEXITCODE
    
    Write-Host ""
    if ($exitCode -eq 0) {
        Write-Host "========================================" -ForegroundColor Green
        Write-Host "  所有 E2E 测试通过!" -ForegroundColor Green
        Write-Host "========================================" -ForegroundColor Green
    } else {
        Write-Host "========================================" -ForegroundColor Red
        Write-Host "  E2E 测试失败 (退出码: $exitCode)" -ForegroundColor Red
        Write-Host "========================================" -ForegroundColor Red
    }
    
    exit $exitCode
}
catch {
    Write-Error "运行 E2E 测试时发生错误: $_"
    exit 1
}
