# ForgeSelf 一键打包脚本
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
$frontendDir = Join-Path $root "ForgeSelf.Web"
$backendDir  = Join-Path $root "ForgeSelf.Api"
$publishDir  = Join-Path $root "publish"

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "  ForgeSelf 铸己匣 - 一键打包" -ForegroundColor Yellow
Write-Host "  配置: $Config" -ForegroundColor Green
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

# 临时记录开始时间
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

# ── 第 1 步：清理 publish 目录（全量删除重建，发布即全新干净）──
# 逐项由深到浅删除，占用/只读等错误静默跳过（避免单个文件被锁导致整目录删除中断），
# 尽可能清空所有文件与目录后重建，确保不带历史运行时遗留（Data/、Log/、Config/）。
Write-Host "[1] 清理旧发布目录（全量删除重建）..." -ForegroundColor Cyan
if (Test-Path $publishDir) {
    Get-ChildItem -Path $publishDir -Recurse -Force |
        Sort-Object { $_.FullName.Length } -Descending |
        Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -Path $publishDir -Force -ErrorAction SilentlyContinue
    Write-Host "    已清空旧发布目录" -ForegroundColor Gray
}
New-Item -Path $publishDir -ItemType Directory -Force | Out-Null
Write-Host "    完成" -ForegroundColor Green
Write-Host ""

# ── 第 2 步：构建前端 ──
if (-not $SkipFrontend) {
    Write-Host "[2/3] 构建前端 (pnpm run build)..." -ForegroundColor Cyan
    Write-Host "      输出目标: ForgeSelf.Api/wwwroot/" -ForegroundColor Gray
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

            # 排除运行时遗留目录，避免发布产物带上 Data/（运行时数据库）与 Log/（运行时日志）。
            # 注意：Plugins/ 必须保留——它承载插件程序集，删除即导致发布版零插件。
            # 插件 DLL 由 Backend.csproj 的 StagePluginsToPublish 目标在 Publish 后拷入。
            foreach ($dir in @("Data", "Log")) {
                $p = Join-Path $publishDir $dir
                if (Test-Path $p) {
                    Remove-Item $p -Recurse -Force
                    Write-Host "    已排除 $dir/" -ForegroundColor Gray
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
Write-Host "  可直接运行: $publishDir\ForgeSelf.exe" -ForegroundColor Green
Write-Host "  或安装为服务: $publishDir\ForgeSelf.exe --install" -ForegroundColor Green
Write-Host "================================================" -ForegroundColor Cyan
