# 完整流程：publish 宿主 · 插件发布与版本化显式更新验证

约定：`<repo>` = 仓库根；`<Dir>` = 插件**目录名**（PascalCase，如 `AIAgent`）；`<id>` = 插件 id（kebab-case，如 `ai-agent`）。

## 步骤 1：全量发布

```powershell
.\build.ps1
```

产出 `publish/ForgeSelf.exe` + `publish/wwwroot` + `publish/Plugins/**`。
（脚本开头清空 `publish/` 的动作会被 safe-delete 拦截，属已知无害现象；`dotnet publish -o` 会覆盖全部当前产物。
**前端（ForgeSelf.Web/src）改动后必须走 build.ps1 重建 wwwroot**，dev/e2e 通过 ≠ 发布态生效。）

## 步骤 2：启动 publish 宿主实例（核心约束）

**判定规则**：

1. 查所有 `ForgeSelf.exe` 进程的 `ExecutablePath`。
2. 路径是 `<repo>\publish\ForgeSelf.exe` → **直接复用**，不重启。
3. 有实例但路径不是 publish（例如 `bin\Debug\...` 的 dev 实例）→ **杀掉**，再起 publish 实例。
4. 无实例 → 起 publish 实例。

```powershell
$pubExe = "<repo>\publish\ForgeSelf.exe"

$running = Get-CimInstance Win32_Process -Filter "Name='ForgeSelf.exe'"
$publishInstance = $running | Where-Object {
    $_.ExecutablePath -and ($_.ExecutablePath -replace '\\','/') -like '*/publish/ForgeSelf.exe'
}

if ($publishInstance) {
    Write-Host "复用已运行的 publish 实例 PID=$($publishInstance.ProcessId)"
} else {
    $running | ForEach-Object { Stop-Process -Id $_.ProcessId -Force }   # 杀掉非 publish 实例
    Start-Sleep -Seconds 2
    Start-Process -FilePath $pubExe -ArgumentList "--console" `
        -WorkingDirectory "<repo>\publish" `
        -RedirectStandardOutput "<repo>\publish-host.out.log" `
        -RedirectStandardError  "<repo>\publish-host.err.log" -NoNewWindow
}
```

**为什么必须 publish 实例**（`DataLocationService.ResolveHostDataDirectory`）：

| 运行形态 | 环境 | 数据根 |
|---------|------|--------|
| `dotnet run` / Debug | Development | `程序目录\Data` |
| `publish\ForgeSelf.exe` | Production | `%USERPROFILE%\.forgeself` |

两者数据库/配置**不互通**。要验证发布态行为（用户目录数据），必须是 publish 实例。

## 步骤 3：等待就绪

```powershell
# 轮询直到 200（首次启动需 build+init，留足时间）
for ($i=0; $i -lt 30; $i++) {
    Start-Sleep -Seconds 3
    try { $r = Invoke-WebRequest "http://localhost:51888/api/health" -UseBasicParsing -TimeoutSec 3
          if ([int]$r.StatusCode -eq 200) { Write-Host "HOST READY"; break } } catch {}
}
```

确认日志里 `Hosting environment: Production` 且 `Content root path: ...\publish`。
⚠ `PluginController` 已类级 `[Authorize("ApiKeyPolicy")]`（2026-09-24）：**凡请求 `/api/plugin*` 必须带
`Authorization: Bearer <token>`**，token 用仓内工具 `node scripts/get-forge-token.cjs` 取（勿每次现写探针）。

## 步骤 4：只发布单个插件（stage 版本快照）

```powershell
.\scripts\publish-plugin.ps1 -Plugin <Dir> -PluginsRoot "<repo>\publish\Plugins"
```

行为：
- 目标暂存目录 `publish\Plugins\_backups\<id>\<version>\`
- 只做 **编译 + staged 拷贝**（后端 DLL + `web/dist`；不含 `web/src`）
- **不会**触发宿主更新，需手动触发（步骤 5）
- 同版本已存在会 skip → 升版本或用 `-Force`
- `-Plugin` 是**目录名**（PascalCase），不是 id

> 升版本：改 `ForgeSelf.Api\Plugins\<Dir>\plugin.json` 的 `Version`。

## 步骤 5：版本化显式更新（唯一发布动作）

`PluginHotReloadWatcher` 已**一刀切移除**（2026-09-24 用户拍板）——**宿主无自动热重载**。
发布/升级一律走版本化 side-by-side：

```powershell
$tok = node scripts/get-forge-token.cjs
# 触发显式更新：宿主落 versions/<version>/ + 切 current 指针 + ALC 换载
Invoke-RestMethod "http://localhost:51888/api/plugin/update/<id>" `
  -Method Post -Headers @{ Authorization = "Bearer $tok" }
```

- 新版本落 `versions/<new>/` 永不覆盖在加载文件 → DLL 锁与热重载竞态从机制上消除
- 版本语义：按版本号比较，staged 版本必须**严格大于**当前版本；同版本是空操作
- 若仅改前端资源 / 需要冷启动强制生效：停宿主 → 覆盖 → 起（冷启动扫描发现）

## 步骤 6：验证

见 [verification.md](verification.md)（含 `scripts/get-forge-token.cjs` / `scripts/probe-dll-string.cjs` 仓内工具）。

## 清理

- 宿主日志：`publish-host.out.log` / `publish-host.err.log`（仓库根，用完可删）
- 停止宿主：`Stop-Process -Name ForgeSelf -Force`
