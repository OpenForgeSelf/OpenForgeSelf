#Requires -Version 5.1
<#
.SYNOPSIS
    运行所有测试
.DESCRIPTION
    运行 OpenForgeSelf.Backend.Tests 项目中的所有测试
.PARAMETER Configuration
    构建配置 (Debug/Release)，默认为 Debug
.PARAMETER Filter
    测试过滤器表达式
.PARAMETER Parallel
    是否并行运行测试
.PARAMETER Diagnostic
    是否启用诊断输出
#>

param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    
    [string]$Filter,
    
    [switch]$Parallel,
    
    [switch]$Diagnostic
)

$ErrorActionPreference = 'Stop'
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$TestProjectPath = Join-Path $ProjectRoot 'OpenForgeSelf.Backend.Tests\OpenForgeSelf.Backend.Tests.csproj'

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  OpenForgeSelf.Backend 测试运行器" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "配置: $Configuration" -ForegroundColor Gray
Write-Host "测试项目: $TestProjectPath" -ForegroundColor Gray
if ($Filter) {
    Write-Host "过滤器: $Filter" -ForegroundColor Gray
}
Write-Host ""

# 检查项目是否存在
if (-not (Test-Path $TestProjectPath)) {
    Write-Error "测试项目不存在: $TestProjectPath"
    exit 1
}

# 构建 dotnet test 命令
$TestArgs = @(
    'test',
    $TestProjectPath,
    '--configuration', $Configuration,
    '--no-build',
    '--verbosity', 'normal'
)

if ($Filter) {
    $TestArgs += '--filter', $Filter
}

if ($Parallel) {
    $TestArgs += '--parallel'
}

if ($Diagnostic) {
    $TestArgs += '--verbosity', 'diagnostic'
}

Write-Host "正在运行测试..." -ForegroundColor Yellow
Write-Host ""

try {
    & dotnet @TestArgs
    $exitCode = $LASTEXITCODE
    
    Write-Host ""
    if ($exitCode -eq 0) {
        Write-Host "========================================" -ForegroundColor Green
        Write-Host "  所有测试通过!" -ForegroundColor Green
        Write-Host "========================================" -ForegroundColor Green
    } else {
        Write-Host "========================================" -ForegroundColor Red
        Write-Host "  测试失败 (退出码: $exitCode)" -ForegroundColor Red
        Write-Host "========================================" -ForegroundColor Red
    }
    
    exit $exitCode
}
catch {
    Write-Error "运行测试时发生错误: $_"
    exit 1
}
