# update-agent.ps1 — OpenForgeSelf 宿主自更新代理（spec 036）
# 由宿主 StagedUpdateService 拉起：等待宿主进程退出 → 覆盖 staged 新文件 → 重启宿主。
# 约定（2026-09-28 输入31 去整目录备份）：
#   - 非删除性操作：robocopy /E 覆盖，旧文件**不再整体备份**（Backups/ 退役并清理存量；
#     版本回退能力由发布版本 tag + 可重发安装包承担，避免每次升级双倍磁盘占用）
#   - 运行数据目录（Data / Log / Config / _backups）不备份也不覆盖
#   - 更新成功后清理本次 staged 暂存目录（Updates/<tag>/），防缓存堆积
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

try {
    Write-AgentLog "更新代理启动: HostPid=$HostPid InstallDir=$InstallDir StagedDir=$StagedDir Exe=$ExeName"

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

    # ── 2. 覆盖式应用新文件（不删除安装目录中的运行数据） ──
    Invoke-Robocopy $StagedDir $InstallDir @('/XF', 'update.zip')
    Write-AgentLog '新文件覆盖完成'

    # ── 3. 清理（2026-09-28 输入31）：本次 staged 暂存目录 + 退役的 Backups 整目录备份存量 ──
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

    # ── 4. 重启宿主 ──
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
    Write-AgentLog '未产生备份（Backups 已退役）：请重新下载更新包后再试；如已部分覆盖，可从 %LOCALAPPDATA%\ForgeSelf\Updates 中的暂存目录核对版本'
    # 尽力重启旧版本，避免应用失联
    try {
        $exePath = Join-Path $InstallDir $ExeName
        if (Test-Path $exePath) {
            Start-Process -FilePath $exePath -WorkingDirectory $InstallDir
            Write-AgentLog '已尽力重启旧版本'
        }
    } catch {
        Write-AgentLog "旧版本重启也失败: $($_.Exception.Message)"
    }
    exit 1
}
finally {
    Write-AgentLog "日志: $logPath"
}
