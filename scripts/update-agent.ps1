# update-agent.ps1 — OpenForgeSelf 宿主自更新代理（spec 036）
# 由宿主 StagedUpdateService 拉起：等待宿主进程退出 → 备份安装目录 → 覆盖 staged 新文件 → 重启宿主。
# 约定：
#   - 非删除性操作：robocopy /E 覆盖，旧文件先整体备份到 %LOCALAPPDATA%\ForgeSelf\Backups\<ts>
#   - 运行数据目录（Data / Log / Config / _backups）不备份也不覆盖
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

    # ── 2. 备份安装目录（排除运行数据） ──
    $backupDir = Join-Path $appData ("Backups\{0}" -f (Get-Date -Format 'yyyyMMddHHmmss'))
    New-Item -ItemType Directory -Force -Path $backupDir | Out-Null
    Invoke-Robocopy $InstallDir $backupDir @('/XD', (Join-Path $InstallDir 'Data'), (Join-Path $InstallDir 'Log'), (Join-Path $InstallDir 'Config'), (Join-Path $InstallDir '_backups'))
    Write-AgentLog "备份完成: $backupDir"

    # ── 3. 覆盖式应用新文件（不删除安装目录中的运行数据） ──
    Invoke-Robocopy $StagedDir $InstallDir @('/XF', 'update.zip')
    Write-AgentLog '新文件覆盖完成'

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
    Write-AgentLog "可从备份恢复: 停止应用后将 '$backupDir' 内容复制回 '$InstallDir'，再手动启动 $ExeName"
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
