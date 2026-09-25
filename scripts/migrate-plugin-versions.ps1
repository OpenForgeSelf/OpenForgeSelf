# 一次性存量迁移：把扁平布局插件迁移到 side-by-side 版本化布局（versions/<ver>/ + current 指针）
# 用法：pwsh scripts/migrate-plugin-versions.ps1 [-PluginsRoot "<repo>\publish\Plugins"]
# 行为：
#   - 只对「无 current 指针或 versions/<当前清单版本> 缺失」的插件目录补建快照 + current
#   - 只新增文件，不删除/不覆盖活动目录任何文件（运行中宿主无感知，下次 DiscoverPlugins 生效）
#   - 版本快照拷贝内容 = 非宿主共享 DLL + web/dist + plugin.json（对齐发布脚本产物）
# 产物格式（PluginVersionLayout 约定）：versions/<semver>/ + current（纯文本版本号）
# 验证：迁移后 GET /api/plugin/{id}/versions 返回该版本；frontend-manifest 版本化读取生效。

param(
    [string]$PluginsRoot = ""
)

$ErrorActionPreference = "Stop"

if (-not $PluginsRoot) {
    $repo = Split-Path -Parent $PSScriptRoot
    $PluginsRoot = Join-Path $repo "publish\Plugins"
}
if (-not (Test-Path $PluginsRoot)) { throw "插件目录不存在: $PluginsRoot" }

# 宿主共享 DLL 白名单（禁止进版本快照——否则插件 ALC 加载第二份宿主程序集导致类型分裂）
$sharedDlls = @(
    "XCode.dll", "NewLife.Core.dll", "NewLife.Agent.dll", "NewLife.Remoting.dll",
    "ForgeSelf.Abstractions.dll", "ForgeSelf.Core.dll", "Stardust.dll"
)

$migrated = 0
$skipped = 0

Get-ChildItem $PluginsRoot -Directory | ForEach-Object {
    $dir = $_.FullName
    $name = $_.Name
    if ($name -eq "_backups" -or $name -like "_*") { return }

    $manifestPath = Join-Path $dir "plugin.json"
    if (-not (Test-Path $manifestPath)) {
        Write-Host "[skip] $name : 无 plugin.json"
        return
    }
    $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
    $version = [string]$manifest.Version
    if ([string]::IsNullOrWhiteSpace($version)) {
        Write-Host "[skip] $name : plugin.json 无 Version"
        return
    }

    # 已版本化判定：current 存在且 versions/<current>/ 存在
    $currentFile = Join-Path $dir "current"
    $current = if (Test-Path $currentFile) { (Get-Content $currentFile -Raw).Trim() } else { "" }
    $verDir = Join-Path $dir "versions\$version"
    if (-not [string]::IsNullOrWhiteSpace($current) -and (Test-Path $verDir)) {
        $skipped++
        Write-Host "[skip] $name : 已版本化 (current=$current)"
        return
    }

    # 迁移：建 versions/<ver>/
    New-Item -ItemType Directory -Force -Path $verDir | Out-Null

    # ① 非共享 DLL
    Get-ChildItem $dir -Filter *.dll -File | Where-Object { $sharedDlls -notcontains $_.Name } | ForEach-Object {
        Copy-Item $_.FullName $verDir -Force
    }
    # ② web/dist（若存在）
    $webDist = Join-Path $dir "web\dist"
    if (Test-Path $webDist) {
        $verWebDist = Join-Path $verDir "web\dist"
        New-Item -ItemType Directory -Force -Path $verWebDist | Out-Null
        Copy-Item "$webDist\*" $verWebDist -Recurse -Force
    }
    # ③ plugin.json 快照
    Copy-Item $manifestPath $verDir -Force

    # ④ current 指针（原子写：先临时再 Move 覆盖）
    $temp = Join-Path $dir "current.tmp"
    [System.IO.File]::WriteAllText($temp, $version)
    [System.IO.File]::Move($temp, $currentFile, $true)

    $migrated++
    Write-Host "[migrate] $name -> versions/$version (current=$version)"
}

Write-Host ""
Write-Host "完成：迁移 $migrated 个，已版本化跳过 $skipped 个"
