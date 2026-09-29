# publish-bootstrapper.ps1 - 产出 ForgeSelf 安装根公共层（QQNT layout public layer，2026-09-28 输入36 单文件化）。
# 公共层 = ① ForgeSelf.exe（FDD 单文件薄壳启动器，~170KB）② .NET 运行时 DOTNET_ROOT 结构
#          （host/fxr + shared 三框架，公共一份，业务层 FDD 共用）③ 根 hostfxr/hostpolicy
#          （app-local shim，让 FDD 启动器自身解析公共运行时）④ update-agent.ps1（组装时拷入）
#          ⑤ plugins/（组装时移入，与 versions/ 并排；2026-09-29 输入37 目录小写统一）。业务层（versions/<ver>/，publish-host.ps1
#          产 FDD 单文件）由启动器以子进程拉起（DOTNET_ROOT=安装根）。
# ⚠ 为什么不用自包含单文件：自包含单文件（无论压缩与否）启动时把 bundle 解压到
#   DOTNET_BUNDLE_EXTRACT_BASE_DIR 缓存运行，AppContext.BaseDirectory / Environment.ProcessPath
#   均指向提取目录（实测 2026-09-28），启动器无法定位安装根；且解压缓存长期占盘（版本变化
#   残留旧缓存）——与「省空间」目标相悖。FDD 单文件（managed-only bundle）原地直跑，
#   BaseDirectory = exe 目录（实测），无解压缓存；运行时由公共层 DOTNET_ROOT 结构提供。
#
# Examples:
#   ./publish-bootstrapper.ps1
#   ./publish-bootstrapper.ps1 -OutputDir artifacts/layout-root

[CmdletBinding()]
param(
    [string]$RepoRoot = '',
    [string]$OutputDir = '',
    [string]$Configuration = 'Release'
)

. (Join-Path $PSScriptRoot 'release-lib.ps1')
if (-not $RepoRoot) { $RepoRoot = Get-ReleaseRepoRoot }
if (-not $OutputDir) { $OutputDir = Join-Path $RepoRoot 'artifacts/layout-root' }

$proj = Join-Path $RepoRoot 'ForgeSelf.Bootstrapper/ForgeSelf.Bootstrapper.csproj'

# 清空目标目录（构建产物；FDD publish 不会清理 -o 已有内容，残留会与运行时文件混装）
if (Test-Path $OutputDir) { Remove-Item $OutputDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

# 第一步：FDD 单文件 publish → 薄壳启动器 exe（managed-only bundle，原地直跑，
# BaseDirectory = exe 目录；无解压缓存）。应用自身不内嵌 native/content（无 wwwroot 等）。
Invoke-ReleaseStep "boot: launcher publish ($Configuration, win-x64, FDD single-file)" {
    & dotnet publish $proj -c $Configuration -r win-x64 --self-contained false `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=false `
        -p:IncludeAllContentForSelfExtract=false `
        -o $OutputDir -nologo -v minimal
    Assert-ExitCode 'dotnet publish (bootstrapper launcher)'
}

# 第二步：组装公共运行时（DOTNET_ROOT 结构）——从本机 .NET 安装目录拷贝 host/fxr + shared
# 三框架（NETCore + AspNetCore + WindowsDesktop，业务层 FDD 运行时依赖）+ 根 app-local shim。
# 版本取本机最新 10.0.x（runtimeconfig 请求 10.0.0，rollForward Minor 可滚到最新补丁）。
$dn = Split-Path (Get-Command dotnet).Source
$fxrDir = Get-ChildItem "$dn\host\fxr" -Directory | Where-Object { $_.Name -like '10.0*' } |
    Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
if (-not $fxrDir) { throw "no .NET 10 host/fxr found under $dn\host\fxr" }
$fxr = $fxrDir.Name
$runtimeVerDir = "$dn\shared\Microsoft.NETCore.App\$fxr"
if (-not (Test-Path $runtimeVerDir)) { throw "no Microsoft.NETCore.App\$fxr under $dn\shared" }

Invoke-ReleaseStep "boot: runtime assembly (DOTNET_ROOT structure, fxr=$fxr)" {
    # host/fxr（业务层 DOTNET_ROOT 用完整 host 树）
    New-Item -ItemType Directory -Force -Path "$OutputDir\host\fxr\$fxr" | Out-Null
    Copy-Item "$dn\host\fxr\$fxr\*" "$OutputDir\host\fxr\$fxr" -Recurse -Force
    # shared 三框架
    foreach ($fw in @('Microsoft.NETCore.App', 'Microsoft.AspNetCore.App', 'Microsoft.WindowsDesktop.App')) {
        $src = "$dn\shared\$fw\$fxr"
        if (-not (Test-Path $src)) { Write-Host "skip missing shared framework: $fw\$fxr"; continue }
        New-Item -ItemType Directory -Force -Path "$OutputDir\shared\$fw\$fxr" | Out-Null
        Copy-Item "$src\*" "$OutputDir\shared\$fw\$fxr" -Recurse -Force
    }
    # 根 app-local shim：让 FDD 启动器自身（apphost）用公共层运行时（同目录 hostfxr 探测优先）
    Copy-Item "$dn\host\fxr\$fxr\hostfxr.dll" "$OutputDir\hostfxr.dll" -Force
    Copy-Item "$dn\host\fxr\$fxr\hostpolicy.dll" "$OutputDir\hostpolicy.dll" -Force -ErrorAction SilentlyContinue
}

# 组装收尾：exe 改名 ForgeSelf.exe；清理残留（含冗余的原名 exe）
$bootExe = Join-Path $OutputDir 'ForgeSelf.Bootstrapper.exe'
if (-not (Test-Path $bootExe)) { throw "bootstrapper exe missing: $bootExe" }
$exe = Join-Path $OutputDir 'ForgeSelf.exe'
if (-not (Test-Path $exe)) { Copy-Item $bootExe $exe -Force } else { Write-Host 'ForgeSelf.exe already present (keep)' }
Remove-Item $bootExe -Force -ErrorAction SilentlyContinue
# 公共层启动器 exe 大小断言（FDD 薄壳单文件：apphost + 内嵌启动器 IL，实测 ~170KB；
# 显著小于自包含（>50MB）且非空壳（<0.05MB 即异常））
$exeSizeMB = (Get-Item $exe).Length / 1MB
if ($exeSizeMB -gt 50 -or $exeSizeMB -lt 0.05) { throw "unexpected launcher size: {0:N2} MB (FDD single-file expected)" -f $exeSizeMB }
# 清理误产出的其它 ForgeSelf.* 残留（保留启动器自身 + ForgeSelf.exe）
Get-ChildItem $OutputDir -Filter 'ForgeSelf.*' | Where-Object { $_.Name -notlike 'ForgeSelf.Bootstrapper*' -and $_.Name -ne 'ForgeSelf.exe' } | Remove-Item -Force
$fileCount = @(Get-ChildItem $OutputDir -File -Recurse).Count
Write-Host ("publish-bootstrapper: common layer ok ({0} files incl. shared frameworks, ForgeSelf.exe {1:N2} MB, fxr={2})" -f $fileCount, $exeSizeMB, $fxr)
