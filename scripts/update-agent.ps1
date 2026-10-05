# update-agent.ps1 — OpenForgeSelf 宿主自更新代理（spec 036 + QQNT 版本化应用，2026-09-28 批次2）
# 由宿主 StagedUpdateService 拉起：等待宿主进程退出 → 新版本落 versions/<ver>/（不动公共层旧版本）
# → 更新 versions/current 指针 → 重启根启动器（公共层跨版本共享，无需复刻）。
# 目录结构唯一真源：docs/04-standards/packaging-upgrade-backup.md §3（QQNT T1-T6）
#   安装根（公共层，跨版本共享）：ForgeSelf.exe + .NET 运行时 + 框架 + update-agent.ps1
#   versions/<ver>/（业务层，每次更新新增，多版本共存 = 回滚能力，不设任何备份目录）
#     └ plugins/：内置插件随版本走（2026-10-04 输入18，不再外置到安装根）
# 约定（2026-09-28；目录命名统一小写 2026-09-29 输入37）：
#   - 不创建任何整目录备份（Backups/ 已退役并清理存量；版本回退 = 保留 versions 多版本）
#   - 运行数据目录（data / log / config / _backups）不备份也不覆盖
#   - 更新成功后清理本次 staged 暂存目录（Updates/<tag>/），防缓存堆积
#   - 版本保留：current + 最新一个旧版本（与插件 MaxRetainedVersions=2 对齐），其余清理
#   - 首次 QQNT 升级时迁移：清掉安装根旧扁平布局残留（业务层已入 versions/）+ 目录名规范化为小写
#   - 全程写日志 %LOCALAPPDATA%\ForgeSelf\Updates\agent-<yyyyMMddHHmmss>.log
param(
    [Parameter(Mandatory = $true)][int]$HostPid,
    [Parameter(Mandatory = $true)][string]$InstallDir,
    [Parameter(Mandatory = $true)][string]$StagedDir,
    [Parameter(Mandatory = $true)][string]$ExeName,
    [string]$ArgList = ''
)

$ErrorActionPreference = 'Stop'

$appData = Join-Path $env:LOCALAPPDATA 'ForgeSelf'
$logDir = Join-Path $appData 'Updates'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$logPath = Join-Path $logDir ("agent-{0}.log" -f (Get-Date -Format 'yyyyMMddHHmmss'))

function Write-AgentLog([string]$msg) {
    $line = '[{0}] {1}' -f (Get-Date -Format 'HH:mm:ss'), $msg
    Add-Content -Path $logPath -Value $line -Encoding UTF8
    Write-Host $line
}

# robocopy 退出码 0-7 均为成功
function Invoke-Robocopy([string]$source, [string]$dest, [string[]]$extra) {
    $roboArgs = @($source, $dest, '/E', '/NP', '/NFL', '/NDL', '/R:2', '/W:2') + $extra
    Write-AgentLog "robocopy $($roboArgs -join ' ')"
    & robocopy @roboArgs
    $code = $LASTEXITCODE
    if ($code -gt 7) { throw "robocopy 失败，退出码 $code（详见日志）" }
    Write-AgentLog "robocopy 完成，退出码 $code"
}

# 安装根归一化（2026-10-04 输入19）：宿主业务层在 versions/<ver>/ 内，旧宿主会把该层当安装根传入，
# 新版本就落进 versions/<ver>/versions/<new>/，每升一代多一层嵌套（现场实测三层）。规则与宿主
# ForgeSelf.Api/Services/HostInstallRoot.cs 完全一致：父目录名为 versions 就一路上跳两级。
function Resolve-ForgeInstallRoot([string]$StartDir) {
    $current = [IO.Path]::GetFullPath($StartDir).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    $layers = 0
    while ($true) {
        $versionsDir = [IO.Path]::GetDirectoryName($current)
        if (-not $versionsDir) { break }
        if (([IO.Path]::GetFileName($versionsDir)) -ne 'versions') { break }
        $root = [IO.Path]::GetDirectoryName($versionsDir)
        if (-not $root) { break }
        $current = $root
        $layers++
    }
    return @{ Root = $current; Layers = $layers }
}

# 纯大小写目录改名（Plugins → plugins）：PS5.1 Rename-Item / .NET Directory.Move 均报
# 「源路径和目标路径必须不同」，必须走 Win32 MoveFileEx（NTFS 大小写不敏感，改名只改大小写标志）。
try {
    Add-Type @"
using System.Runtime.InteropServices;
public static class ForgeNativeMove {
    [DllImport("kernel32.dll", SetLastError=true, CharSet=CharSet.Unicode)]
    public static extern bool MoveFileEx(string existing, string New, int flags);
}
"@
} catch { }

try {
    Write-AgentLog "更新代理启动: HostPid=$HostPid InstallDir=$InstallDir StagedDir=$StagedDir Exe=$ExeName"

    # ── 0. 安装根归一化 + 嵌套硬拦（2026-10-04 输入19：不允许 versions/<ver>/ 内再建 versions/） ──
    $resolved = Resolve-ForgeInstallRoot $InstallDir
    if ($resolved.Root -ne $InstallDir) {
        Write-AgentLog "安装根归一化: $InstallDir → $($resolved.Root)（versions 层 $($resolved.Layers)）"
    }
    $InstallDir = $resolved.Root
    $parentOfRoot = [IO.Path]::GetDirectoryName($InstallDir)
    if ($parentOfRoot -and ([IO.Path]::GetFileName($parentOfRoot)) -eq 'versions') {
        throw "安装根仍位于 versions/<ver>/ 内，拒绝产生逐代嵌套: $InstallDir"
    }
    Write-AgentLog "安装根 = $InstallDir（版本层 $($resolved.Layers)，公共层在此、业务层在 versions/<ver>/）"

    # ── 1. 等待宿主进程退出（最多 60s，超时则强制结束） ──
    $deadline = (Get-Date).AddSeconds(60)
    while (Get-Process -Id $HostPid -ErrorAction SilentlyContinue) {
        if ((Get-Date) -gt $deadline) {
            Write-AgentLog '宿主 60s 未退出，强制结束进程'
            Stop-Process -Id $HostPid -Force -ErrorAction SilentlyContinue
            Start-Sleep -Seconds 3
            break
        }
        Start-Sleep -Milliseconds 500
    }
    Start-Sleep -Seconds 2   # 等待文件句柄释放
    Write-AgentLog '宿主进程已退出'

    # ── 2. 定位新版本目录（staged 解压 = QQNT 布局：根公共层 + versions/<ver>/ + plugins/） ──
    $versionsSrc = Join-Path $StagedDir 'versions'
    $verDirs = @(Get-ChildItem $versionsSrc -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -ne 'current' })
    if ($verDirs.Count -eq 0) { throw "更新包缺少 versions/<ver>/ 目录（QQNT 布局）: $versionsSrc" }
    $newVer = $verDirs[0].Name
    if ($verDirs.Count -gt 1) { Write-AgentLog "更新包含多个版本目录，取第一个: $newVer" }
    Write-AgentLog "新版本: $newVer"

    # ── 3. 业务层落 versions/<ver>/（全新目录，无文件锁；同版本重装走 robocopy 覆盖） ──
    $versionsDest = Join-Path $InstallDir 'versions'
    New-Item -ItemType Directory -Force -Path $versionsDest | Out-Null
    Invoke-Robocopy (Join-Path $versionsSrc $newVer) (Join-Path $versionsDest $newVer) @()
    Write-AgentLog "业务层已落 versions/$newVer/"

    # ── 4. 公共层合并（根启动器/运行时/框架/update-agent/plugins 等；排除 versions/ 免整树覆盖旧版本） ──
    Invoke-Robocopy $StagedDir $InstallDir @('/XD', 'versions')
    Write-AgentLog '公共层合并完成'

    # ── 5. current 指针 → 新版本 ──
    Set-Content -Path (Join-Path $versionsDest 'current') -Value $newVer -Encoding ASCII -NoNewline
    Write-AgentLog "versions/current → $newVer"

    # ── 6. 扁平存量迁移（首次 QQNT 升级：安装根旧扁平布局残留清掉，业务层已在新版本目录） ──
    $flatResidue = @(
        'ForgeSelf.Api.dll', 'ForgeSelf.Api.deps.json', 'ForgeSelf.Api.runtimeconfig.json', 'ForgeSelf.Api.exe',
        'ForgeSelf.dll', 'ForgeSelf.deps.json', 'ForgeSelf.runtimeconfig.json',
        'wwwroot', 'appsettings.json', 'appsettings.Development.json', 'web.config', 'staticwebassets.endpoints.json'
    )
    $foundFlat = $false
    foreach ($f in $flatResidue) {
        $fp = Join-Path $InstallDir $f
        if (Test-Path -LiteralPath $fp) {
            Remove-Item -LiteralPath $fp -Recurse -Force -ErrorAction SilentlyContinue
            $foundFlat = $true
        }
    }
    if ($foundFlat) { Write-AgentLog '已清理旧扁平布局残留（业务层已入 versions/）' }

    # ── 6.5 目录名规范化（2026-09-29 输入37）：存量大写目录 → 全小写（Plugins/Data/Log/Config → plugins/data/log/config）。
    # Windows NTFS 大小写不敏感：改名只改大小写标志、不搬数据；仅当小写形式不存在时执行（幂等）。
    # 用 MoveFileEx 而非 Rename-Item（PS5.1 对纯大小写改名报「源路径和目标路径必须不同」）。
    function Invoke-NameNormalize([string]$baseDir, [string]$oldName, [string]$newName) {
        if (-not (Test-Path -LiteralPath $baseDir)) { return }
        $item = Get-ChildItem -LiteralPath $baseDir -Force -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -ceq $oldName -and $_.Name -cne $newName } | Select-Object -First 1
        if ($item) {
            $ok = [ForgeNativeMove]::MoveFileEx($item.FullName, (Join-Path $baseDir $newName), 0)
            if (-not $ok) { throw "目录名规范化失败（$oldName→$newName in $baseDir, Win32=$([Runtime.InteropServices.Marshal]::GetLastWin32Error())）" }
            Write-AgentLog "目录名规范化: $oldName → $newName（$baseDir）"
        }
    }
    foreach ($pair in @(@('Plugins','plugins'), @('Data','data'), @('Log','log'), @('Config','config'))) {
        Invoke-NameNormalize $InstallDir $pair[0] $pair[1]
    }
    $dataRoot = Join-Path $env:USERPROFILE '.forgeself'
    foreach ($pair in @(@('Plugins','plugins'), @('Config','config'), @('Log','log'), @('Data','data'))) {
        Invoke-NameNormalize $dataRoot $pair[0] $pair[1]
    }

    # ── 7. 版本保留：current + 最高一个旧版本（多版本共存 = 回滚能力；不留备份目录） ──
    $keep = @($newVer)
    $verOthers = @(Get-ChildItem $versionsDest -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^\d+(\.\d+){0,3}$' -and $_.Name -ne $newVer })
    if ($verOthers.Count -gt 0) {
        $top = $verOthers | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
        $keep += $top.Name
    }
    foreach ($v in $verOthers) {
        if ($v.Name -notin $keep) {
            Remove-Item -LiteralPath $v.FullName -Recurse -Force -ErrorAction SilentlyContinue
            Write-AgentLog "已清理旧版本 versions/$($v.Name)/（保留 current+上一版）"
        }
    }

    # ── 8. 清理：本次 staged 暂存目录 + 退役的 Backups 整目录备份存量 ──
    $stagedTagDir = Split-Path $StagedDir -Parent   # Updates/<tag>（extracted 的父目录）
    if (Test-Path $stagedTagDir) {
        try {
            Remove-Item -LiteralPath $stagedTagDir -Recurse -Force -ErrorAction Stop
            Write-AgentLog "已清理本次更新暂存目录: $stagedTagDir"
        } catch {
            Write-AgentLog "清理暂存目录失败（不影响本次更新结果）: $($_.Exception.Message)"
        }
    }
    $backupsRoot = Join-Path $appData 'Backups'
    if (Test-Path $backupsRoot) {
        try {
            Remove-Item -LiteralPath $backupsRoot -Recurse -Force -ErrorAction Stop
            Write-AgentLog "已退役并清理历史整目录备份: $backupsRoot"
        } catch {
            Write-AgentLog "清理 Backups 失败（可下次更新再清）: $($_.Exception.Message)"
        }
    }

    # ── 9. 重启宿主（根启动器，公共层） ──
    $exePath = Join-Path $InstallDir $ExeName
    if (-not (Test-Path $exePath)) { throw "安装目录中未找到 $ExeName" }

    $hostArgs = @()
    if ($ArgList) { $hostArgs = $ArgList -split ';' | Where-Object { $_ } }

    if ($hostArgs.Count -gt 0) {
        Start-Process -FilePath $exePath -ArgumentList $hostArgs -WorkingDirectory $InstallDir
    }
    else {
        Start-Process -FilePath $exePath -WorkingDirectory $InstallDir
    }
    Write-AgentLog "宿主已重启: $exePath $($hostArgs -join ' ')"
    Write-AgentLog '更新完成 ✔'
}
catch {
    Write-AgentLog "更新失败: $($_.Exception.Message)"
    Write-AgentLog '未产生备份（Backups 已退役）：请重新下载更新包后再试；已落盘的新版本目录保留可核对'
    # 尽力重启旧版本（current 指针未动 = 旧版本仍生效），避免应用失联
    try {
        $exePath = Join-Path $InstallDir $ExeName
        if (Test-Path $exePath) {
            Start-Process -FilePath $exePath -WorkingDirectory $InstallDir
            Write-AgentLog '已尽力重启（当前 versions/current 指向的旧版本仍生效）'
        }
    } catch {
        Write-AgentLog "旧版本重启也失败: $($_.Exception.Message)"
    }
    exit 1
}
finally {
    Write-AgentLog "日志: $logPath"
}
