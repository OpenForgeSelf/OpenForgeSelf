# OpenForgeSelf 一键打包脚本
# 构建前端 → 输出到后端 wwwroot → 发布后端
#
# 用法:
#   .\build.ps1              # 默认 Release 发布
#   .\build.ps1 -Config Debug # Debug 发布
#   .\build.ps1 -SkipFrontend # 仅打包后端（前端已构建）
#   .\build.ps1 -SkipPublish  # 仅构建不发布

param(
    [ValidateSet('Debug','Release')][string]$Config = 'Release',
    [switch]$SkipFrontend,
    [switch]$SkipPublish
)

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$frontendDir = Join-Path $root "OpenForgeSelf.Frontend"
$backendDir  = Join-Path $root "OpenForgeSelf.Backend"
$publishDir  = Join-Path $root "publish"

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "  OpenForgeSelf 铸己匣 - 一键打包" -ForegroundColor Yellow
Write-Host "  配置: $Config" -ForegroundColor Green
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

# 临时记录开始时间
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

# ── 第 1 步：清理 publish 目录 ──
Write-Host "[1] 清理旧发布目录..." -ForegroundColor Cyan
if (Test-Path $publishDir) {
    Remove-Item -Path $publishDir -Recurse -Force
    Write-Host "    已删除 $publishDir" -ForegroundColor Gray
}
New-Item -Path $publishDir -ItemType Directory -Force | Out-Null
Write-Host "    完成" -ForegroundColor Green
Write-Host ""

# ── 第 2 步：构建前端 ──
if (-not $SkipFrontend) {
    Write-Host "[2/3] 构建前端 (pnpm run build)..." -ForegroundColor Cyan
    Write-Host "      输出目标: OpenForgeSelf.Backend/wwwroot/" -ForegroundColor Gray
    Push-Location $frontendDir
    try {
        & pnpm run build
        if ($LASTEXITCODE -ne 0) {
            Write-Host "    [错误] 前端构建失败 (exit code: $LASTEXITCODE)" -ForegroundColor Red
            exit $LASTEXITCODE
        }
        Write-Host "    前端构建完成" -ForegroundColor Green
    } finally {
        Pop-Location
    }
    Write-Host ""
} else {
    Write-Host "[2/3] 跳过前端构建 (--SkipFrontend)" -ForegroundColor Gray
    Write-Host ""
}

# ── 第 3 步：发布后端 ──
if (-not $SkipPublish) {
    Write-Host "[3/3] 发布后端 (dotnet publish -c $Config)..." -ForegroundColor Cyan
    Push-Location $backendDir
    try {
        & dotnet publish -c $Config -o $publishDir
        if ($LASTEXITCODE -ne 0) {
            Write-Host "    [错误] 后端发布失败 (exit code: $LASTEXITCODE)" -ForegroundColor Red
            exit $LASTEXITCODE
        }
        if ($LASTEXITCODE -eq 0) {
            # 复制环境配置文件（dotnet publish 默认不包含 appsettings.*.json 以外部分）
            $configFiles = @(
                "appsettings.json",
                "appsettings.Development.json",
                "appsettings.Production.json"
            )
            foreach ($file in $configFiles) {
                $src = Join-Path $backendDir $file
                if (Test-Path $src) {
                    Copy-Item -Path $src -Destination $publishDir -Force
                    Write-Host "    已复制 $file" -ForegroundColor Gray
                }
            }

            Write-Host "    后端发布完成" -ForegroundColor Green
        } else {
            Write-Host "    [错误] 后端发布失败 (exit code: $LASTEXITCODE)" -ForegroundColor Red
            exit $LASTEXITCODE
        }
    } finally {
        Pop-Location
    }
    Write-Host ""
} else {
    Write-Host "[3/3] 跳过后端发布 (--SkipPublish)" -ForegroundColor Gray
    Write-Host ""
}

$stopwatch.Stop()

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "  打包完成！" -ForegroundColor Yellow
Write-Host "  耗时: $($stopwatch.Elapsed.TotalSeconds.ToString('0.0')) 秒" -ForegroundColor Green
Write-Host "  发布目录: $publishDir" -ForegroundColor Green
Write-Host "  可直接运行: $publishDir\OpenForgeSelf.Backend.exe" -ForegroundColor Green
Write-Host "  或安装为服务: $publishDir\OpenForgeSelf.Backend.exe --install" -ForegroundColor Green
Write-Host "================================================" -ForegroundColor Cyan
