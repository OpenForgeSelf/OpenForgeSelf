# ForgeSelf 一键打包脚本
# 构建前端 → 输出到后端 wwwroot → 发布后端
#
# 用法:
#   .\build.ps1              # 默认 Release 发布
#   .\build.ps1 -Config Debug # Debug 发布
#   .\build.ps1 -SkipFrontend # 仅打包后端（前端已构建）
#   .\build.ps1 -SkipPublish  # 仅构建不发布
#   .\build.ps1 -Sign         # 发布后对产物做 Authenticode 签名（自签/已有证书，见 scripts/sign-publish.ps1）
#   .\build.ps1 -Sign -SignAll # 签名范围覆盖 publish 下全部 DLL

param(
    [ValidateSet('Debug','Release')][string]$Config = 'Release',
    [switch]$SkipFrontend,
    [switch]$SkipPublish,
    [switch]$Sign,
    [switch]$SignAll
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

<#
 🔴 原子发布（避免发布失败打挂线上，2026-09-18 重构）：
   旧逻辑先全量清空 publish/ 再构建——一旦前端/后端编译失败，publish/ 已被清空且无回滚，
   运行中宿主会 404（实测：AIAgent 编译失败导致 publish/Plugins 被清空）。
   本环境还有 safe-delete shim 会 fail-closed 拦截 Remove-Item publish/...，故也不能用「先删后换」。
   新逻辑（shim 安全）：
     1) 前端构建输出到 ForgeSelf.Api/wwwroot（不碰 publish/）；
     2) 后端 dotnet publish 到唯一命名的 publish.staging.<时间戳> 临时目录（绝不先删 publish/）；
     3) 仅当构建【成功】才：把当前 publish/ 备份为 publish.bak.<时间戳>（复制，不删）+ 覆盖进 publish/；
     4) 任意构建失败 -> 直接退出，publish/ 原封不动，宿主继续跑旧版本，绝不 404。
   效果：编译失败天然回滚（旧部署保留）；成功才替换，并提供 publish.bak.* 供人工回滚。
#>

# 一次性唯一 staging 目录：避免对已有目录 Remove-Item（触发 shim），也避免与上次残留冲突
$stagingDir = Join-Path $root ("publish.staging." + (Get-Date -Format 'yyyyMMddHHmmssfff'))

# ── 第 1 步：构建前端（输出到后端 wwwroot，不触碰 publish/）──
if (-not $SkipFrontend) {
    Write-Host "[1/3] 构建前端 (pnpm run build)..." -ForegroundColor Cyan
    Write-Host "      输出目标: ForgeSelf.Api/wwwroot/" -ForegroundColor Gray
    Push-Location $frontendDir
    try {
        & pnpm run build
        if ($LASTEXITCODE -ne 0) {
            Write-Host "    [错误] 前端构建失败 (exit code: $LASTEXITCODE)，publish/ 保持不变" -ForegroundColor Red
            exit $LASTEXITCODE
        }
        Write-Host "    前端构建完成" -ForegroundColor Green
    } finally {
        Pop-Location
    }
    Write-Host ""
} else {
    Write-Host "[1/3] 跳过前端构建 (--SkipFrontend)" -ForegroundColor Gray
    Write-Host ""
}

# ── 第 2 步：发布后端到 staging（绝不先删 publish/）──
if (-not $SkipPublish) {
    Write-Host "[2/3] 发布后端 (dotnet publish -c $Config) -> $stagingDir ..." -ForegroundColor Cyan
    Push-Location $backendDir
    try {
        & dotnet publish -c $Config -o $stagingDir
        if ($LASTEXITCODE -ne 0) {
            Write-Host "    [错误] 后端发布失败 (exit code: $LASTEXITCODE)，publish/ 保持不变" -ForegroundColor Red
            Pop-Location
            exit $LASTEXITCODE
        }
    } finally {
        if ($PSScriptRoot) { Pop-Location }
    }

    # 排除运行时遗留目录：发布产物不应带上 Data/（运行时数据库）与 Log/（运行时日志）。
    # 注意：Plugins/ 必须保留——它承载插件程序集，由 Backend.csproj 的 StagePluginsToPublish 拷入。
    foreach ($dir in @("Data", "Log")) {
        $p = Join-Path $stagingDir $dir
        if (Test-Path $p) { Remove-Item $p -Recurse -Force -ErrorAction SilentlyContinue }
    }

    # 复制环境配置文件（dotnet publish 默认不包含 appsettings.*.json 以外部分）
    foreach ($file in @("appsettings.json", "appsettings.Development.json", "appsettings.Production.json")) {
        $src = Join-Path $backendDir $file
        if (Test-Path $src) { Copy-Item $src $stagingDir -Force }
    }
    Write-Host "    后端发布完成（staging）" -ForegroundColor Green
    Write-Host ""
} else {
    Write-Host "[2/3] 跳过后端发布 (--SkipPublish)" -ForegroundColor Gray
    Write-Host ""
}

# ── 第 3 步：原子替换 publish/（成功才换，失败原样保留）──
if (-not $SkipPublish) {
    Write-Host "[3/3] 原子替换发布目录 publish/ ..." -ForegroundColor Cyan
    $ts = Get-Date -Format 'yyyyMMddHHmmss'

    # 成功才备份当前发布（复制，不删除；即便宿主运行中个别文件被锁也仅跳过，不阻断）
    if (Test-Path $publishDir) {
        $backupDir = Join-Path $root ("publish.bak.$ts")
        robocopy $publishDir $backupDir /E /R:1 /W:1 /NFL /NDL /NJH /NJS | Out-Null
        # robocopy exit 0-7 均视为成功；立即复位，避免残留退出码污染脚本整体 exit code
        $LASTEXITCODE = 0
        Write-Host "    旧发布已备份: $backupDir" -ForegroundColor Gray
    }

    # 覆盖进 publish/（staging 内容整体写入；运行期被宿主锁定的文件跳过，由宿主重启后生效）
    if (-not (Test-Path $publishDir)) { New-Item -Path $publishDir -ItemType Directory -Force | Out-Null }
    # 🔴 PS 5.1 下 Copy-Item -Path '<dir>\*' -Recurse 对已存在目标目录的合并递归不可靠，
    #    会静默漏拷深层文件（-ErrorAction SilentlyContinue 吞错 → 打印"已覆盖"但产物是旧的，
    #    2026-09-23 实证：publish/Plugins/McpCenter/McpCenter.dll 保持旧版哈希）。
    #    改用 robocopy 整目录镜像（exit 0-7 均视为成功，8+ 才是失败）。
    robocopy $stagingDir $publishDir /E /R:1 /W:1 /NFL /NDL /NJH /NJS | Out-Null
    if ($LASTEXITCODE -ge 8) {
        Write-Host "    [错误] 覆盖 publish/ 失败 (robocopy exit: $LASTEXITCODE)，publish/ 保持原样" -ForegroundColor Red
        exit $LASTEXITCODE
    }
    # robocopy 成功（0-7）立即复位退出码，避免残留污染本脚本整体 exit code（实测曾 exit=3 被发布脚本误判失败）
    $LASTEXITCODE = 0
    Write-Host "    已覆盖为新发布（publish.bak.* 可手动回滚）" -ForegroundColor Green

    # 清理本次 staging（best-effort；若 shim 拦截则保留，无碍）
    Remove-Item $stagingDir -Recurse -Force -ErrorAction SilentlyContinue
    # 仅保留最近 3 份 publish.bak.* 备份，避免磁盘堆积（备份为脚本自产副本，非用户文件）
    $olds = Get-ChildItem -Path $root -Directory -Filter 'publish.bak.*' -ErrorAction SilentlyContinue |
            Sort-Object Name -Descending | Select-Object -Skip 3
    foreach ($o in $olds) { Remove-Item $o.FullName -Recurse -Force -ErrorAction SilentlyContinue }
    Write-Host ""
} else {
    Write-Host "[3/3] 跳过后端替换 (--SkipPublish)" -ForegroundColor Gray
    Write-Host ""
    # SkipPublish 仅构建不发布：清理 staging，避免残留
    Remove-Item $stagingDir -Recurse -Force -ErrorAction SilentlyContinue
}

# ── 第 4 步（可选）：对发布产物做 Authenticode 代码签名 ──
# 必须放在 publish 之后（第 3 步才是最终发布目录）。
if ($Sign) {
    Write-Host "[+] 对发布产物签名 (scripts/sign-publish.ps1)..." -ForegroundColor Cyan
    $signScript = Join-Path $root "scripts\sign-publish.ps1"
    if (-not (Test-Path $signScript)) {
        Write-Host "    [错误] 签名脚本不存在: $signScript" -ForegroundColor Red
        exit 1
    }
    # 用哈希表 splat（而非数组 splat）显式绑定命名参数，避免数组 splat 在 $publishDir 为空时
    # 把 '-PublishDir' 自身当作参数值传入（实测：数组 splat 会令脚本收到 $PublishDir='-PublishDir'）。
    $signParams = @{ PublishDir = $publishDir }
    if ($SignAll) { $signParams['AllAssemblies'] = $true }
    & $signScript @signParams
    if ($LASTEXITCODE -ne 0) {
        Write-Host "    [错误] 签名失败 (exit code: $LASTEXITCODE)" -ForegroundColor Red
        exit $LASTEXITCODE
    }
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

# 显式成功退出码：防止原生命令（robocopy/pnpm/dotnet）残留退出码被调用方误判（run-plugin-publish-verify 曾收到 exit=3）
exit 0
