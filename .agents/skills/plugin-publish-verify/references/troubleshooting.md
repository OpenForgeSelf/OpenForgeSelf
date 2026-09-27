# 已知缺陷与坑（实测 2026-08-30；2026-09-24 状态更新）

## 🟢 状态更新（2026-09-24，先读这条）

1. **`PluginHotReloadWatcher` 已一刀切移除**（用户拍板）——宿主**无自动热重载**。所有「热重载」「watcher」「拷贝顺序触发重载」相关坑（坑 2/6 的 FileSystemWatcher 路径）**不再适用**，发布/升级一律走版本化显式更新（`POST /api/plugin/update/{id}`）或冷启动。
2. **`PluginController` 已类级 `[Authorize("ApiKeyPolicy")]`**——`/api/plugin*` 裸请求（无 token）一律 401。**前端已适配**（`src/services/authFetch.ts`，pluginApi/pluginManifestApi 全走它）；手工 curl/Invoke-RestMethod 必须带 `Authorization: Bearer <token>`。
3. **仓内验证工具**（不再每次现写探针）：
   - token：`node scripts/get-forge-token.cjs`（一键解密当前宿主明文 token；宿主重启会轮换，重取）
   - DLL 字符串：`node scripts/probe-dll-string.cjs <dll> <目标串> [--expect-absent]`（UTF-8+UTF-16LE 双检）
   - 插件管理页走查：`e2e/plugin-store.spec.ts`（5 例，正规入口）
4. **e2e 机制（已修）**：Playwright globalSetup env 不传 worker → token 兜底走 `real-auth.ts#readLatestStateToken()`（读 state.json）；vite `/plugins` 代理已加 bypass（仅代理 `/web/` 静态资源，SPA 路由走本地）。

## 🟢 缺陷：版本化插件更新 API 完全不可用 —— **已修复（2026-09-21 实测验证）**

> **修复位置**：`AppBuilder.cs:287`
> `app.Services.GetRequiredService<PluginVersionService>().Initialize(pluginsPath);`
> 关键是必须在 `app.Services`（而非 bootstrap 临时 provider）上解析，否则初始化的是另一个单例实例。
>
> **2026-09-21 全链路实测（AgentHub 1.0.0 → 1.0.1，宿主 :51888 不重启）**：
> `publish-plugin.ps1 -PluginsRoot publish/Plugins` 暂存 → `GET /api/plugin/updates` 正确检出
> 1.0.0→1.0.1 → `POST /api/plugin/update/agent-hub` 返回「插件更新成功」→
> `versions/1.0.1/` side-by-side 落盘 + `current` 指针写入 + 活动清单同步 + ALC 换载 →
> `GET /api/plugin` 显示 1.0.1 且 state 正常、插件 API 存活。
> **改 C# 代码后的正解就是这条 side-by-side 路径**（入口 DLL 被锁覆盖不了的问题一并绕开）。
>
> **版本语义注意**：更新按**版本号比较**（`VersionComparer`），staged 版本必须**严格大于**
> 当前版本；同版本号时 `POST update` 返回成功但实际是「已经是最新版本」空操作。

**以下为原始缺陷记录（存档）**

**现象**
- `GET /api/plugin/updates` → `data: []`（明明 `_backups` 里有更新版本）
- `GET /api/plugin/{id}/versions` → `data: []`
- `POST /api/plugin/update/{id}` → **400 Bad Request**，插件版本不变

**根因**
`PluginVersionService._backupsDirectory` 只在 `Initialize(pluginsDirectory)` 里赋值，
字段默认 `string.Empty`；而全仓从未调用过 `Initialize`。于是 `Path.Combine("", id)` 得到相对路径 → 目录不存在 → 直接返回空/false。

**影响面**：`/api/plugin/updates`、`/api/plugin/update/{id}`、`/api/plugin/{id}/versions`、`/api/plugin/rollback/{id}` 曾是死代码（2026-09-21 已修复并全链路实测）。

---

## 坑 1：`publish-plugin.ps1` 结尾打印的端点路径是错的

脚本 `Print-SuccessTail` 打印 `http://localhost:7102/api/plugins/updates`（**复数**）。
真实路由来自 `[Route("api/[controller]")]` + 控制器 `PluginController` → **`api/plugin`**（单数）。
复数路径实测 **404**。

## 坑 2：拷贝顺序反了会导致 DLL 拷不进去 —— ⚠ 历史坑（watcher 已移除）

> 2026-09-24 起**不再适用**（无 FileSystemWatcher）。保留存档：先拷 `plugin.json` → watcher 触发重载 → 旧 DLL 被卸载/移走 → 拷贝报 `Could not find file`。
> 正确顺序曾是「payload 先拷，`plugin.json` 最后拷」；现在发布走版本化更新接口，不存在覆盖竞态。

## 坑 3：dev 实例和 publish 实例数据不互通

| 形态 | 数据根 |
|------|--------|
| `dotnet run`（Development） | `程序目录\Data` |
| `publish\ForgeSelf.exe`（Production） | `%USERPROFILE%\.forgeself` |

在 dev 里建的数据，publish 实例看不到——这是设计如此，不是 bug。本流程**强制**用 publish 实例验证。

## 坑 4：启动后端别用 `--urls`

`dotnet run --urls http://localhost:7102` 会被 NewLife.Agent 宿主吞掉并回退到默认 **5000**，
导致前端代理（7102）对不上。端口来自 `ForgeSetting.config` 的 `PortNumber`，直接 `dotnet run` 即可。

## 坑 5：`build.ps1` 清不掉旧 publish 目录

`Remove-Item` 被 safe-delete 包装拦截（`SAFE_DELETE_FAIL_CLOSED` non-empty-directory），
所以重命名前的陈旧产物可能残留。`dotnet publish -o` 会覆盖全部当前产物，残留属无害。

## 🔴 坑 0（最严重）：把宿主共享 DLL 拷进插件目录 → 宿主启动即崩

**现象**（2026-08-30 实测）：下次启动宿主直接未处理异常退出：

```
Unhandled exception. System.Reflection.ReflectionTypeLoadException:
  Unable to load one or more of the requested types.
System.IO.FileLoadException: Could not load file or assembly
  'XCode, Version=12.0.2026.701 ...' An operation is not legal in the current state.
  ---> System.InvalidOperationException: AssemblyLoadContext is unloading or was already unloaded.
```

**根因**：把宿主已提供的 `XCode.dll` / `NewLife.Core.dll` / `ForgeSelf.*.dll` 拷进活动插件目录 → `PluginLoadContext` 把宿主正在用的程序集再加载一份到插件 ALC → 类型标识分裂（`IPlugin` 判等失败）+ ALC 卸载中加载。

**正确布局**：

```
publish/Plugins/<Dir>/
  <Dir>.dll        ← 只有插件自己的程序集
  plugin.json
  web/dist/        ← 可选，插件自带界面
```

**绝不出现**：`XCode.dll`、`NewLife.Core.dll`、`NewLife.Agent.dll`、`ForgeSelf.Abstractions.dll`、`ForgeSelf.Core.dll` 等宿主共享 DLL。
发布脚本已内置白名单过滤 + 启动前防御性清理。

## 坑 6：入口 DLL 覆盖有竞态，代码改动可能没真正生效

> 2026-09-24 起主路径 = 版本化 side-by-side（天然绕开文件锁，见顶部修复条目），本坑只影响「冷启动覆盖」场景。

宿主运行期间 `<Dir>.dll` **被独占锁定**（已加载进 ALC），覆盖会失败（误报 `FileNotFoundException: Could not find file`）。判据：比较哈希（`Get-FileHash`）。改 C# 代码走版本化更新接口，或停 → 覆盖 → 起。

## 坑 7：插件版本没升 → publish-plugin.ps1 直接 skip

同版本 staged 目录已存在且未加 `-Force` 时脚本会 `exit 0` 跳过。
改了代码却没看到更新，先确认 `plugin.json` 的 `Version` 升了（或用 `-Force` 覆盖）。

## 坑 8：前端管理页裸 fetch 不带 token（2026-09-24 新增，已修）

`PluginController` 鉴权后，前端 `pluginApi.ts`/`pluginManifestApi.ts` 裸 `fetch()` 全部 401 → 插件管理页/远程视图空态（曾误判为「浏览器环境问题」）。**已修**：`src/services/authFetch.ts`（自动带 Bearer，与 request.ts 同键）。教训：**后端控制器加鉴权后，必须 grep 前端全部 `await fetch(` 调用方**，只验后端 401/200 会漏前端适配。
