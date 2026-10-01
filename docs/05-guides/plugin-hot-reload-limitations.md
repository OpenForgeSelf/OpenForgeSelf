# 插件热更新局限性 SOP（2026-10-01 修订，对齐当前代码）

> 适用：当你「改了插件想让它生效」时，先用本 SOP 判定走哪条路。详细坑位见技能
> `.agents/skills/plugin-publish-verify/references/troubleshooting.md`，本文件只给**决策表**。
>
> 修订背景：本文旧版描述的「FileSystemWatcher 自动重载」与「版本 API 全死」均已过时——
> watcher 已于 2026-09-24 一刀切删除（`AppBuilder.cs` 注释），`PluginVersionService.Initialize`
> 已在 `AppBuilder.cs:362` 接线（版本化侧载可用）。

## 1. 决策表（先判你改了什么、在哪类宿主）

### A. dev 宿主（FORGESELF_DEV_MODE=1 起的开发实例，推荐日常迭代用）

| 你改了 | 生效方式 |
|--------|----------|
| 插件 C#（入口 DLL） | `dotnet build Plugins/<X>` → `scripts/dev-plugin.ps1 -Plugin <X>`（= `POST /api/dev/plugin/{id}/reload`）。shadow-copy 装载：**无需 bump 版本、无 DLL 锁**，秒级生效 |
| 插件 UI（web/src） | `pnpm run build`（或 `pnpm run dev` watch 构建）→ 内容指纹 `?v=` 自动破缓存，刷新页面即生效（dev 下 web 资源 no-store 双保险） |
| 插件 UI（需保状态/秒级反馈） | opt-in 真 HMR：`scripts/dev-plugin-web.ps1 -Plugin <X>` + 宿主 `FORGESELF_DEV_WEB_HMR=1` |
| 宿主本体 / Abstractions 契约 | 冷启动 dev 宿主 |

### B. publish / 运行实例（版本化宿主）

| 你改了 | 能否覆盖活动目录热更 | 正解 |
|--------|----------------------|------|
| 仅 `plugin.json` / 仅 `web/dist/**` | ✅ 可以（覆盖后刷新页面；内容指纹 `?v=` 破缓存，无需重启宿主） | 直接覆盖 `plugins/<Dir>/web/dist/**` |
| 改了 C#（入口 `<Dir>.dll`） | ❌ **不行** | 入口 DLL 被 ALC 独占锁，覆盖必失败（且 `Copy-Item` 误报 `FileNotFoundException`，**假成功陷阱**）。正解：side-by-side 版本——bump `plugin.json` 版本 → `scripts/publish-plugin.ps1 -Plugin <X> -Force` → `POST /api/plugin/update/{id}`（热切换，不重启宿主） |

## 2. 铁律

1. **版本 API 可用**：`GET /api/plugin/updates`、`POST /api/plugin/update/{id}`、`POST /api/plugin/rollback/{id}` 已由 `PluginVersionService.Initialize` 接线（`AppBuilder.cs:362`），走版本化侧载是 C# 改动的唯一正解（dev 实例另有 reload 通道）。
2. **活动插件目录只放插件自身程序集**：`<Dir>.dll` + `plugin.json` [+ `web/dist`]。**绝不能**放入 `XCode.dll` / `NewLife.*.dll` / `ForgeSelf.*.dll` —— 类型标识分裂 + ALC 卸载中加载 → **宿主启动即崩**（R8②）。
3. **禁停/启/杀用户运行中的宿主进程**（AGENTS.md §0/R8③）；宿主升级由 update-agent 自更新。
4. **更新成功判定**：接口返回的该插件 `version` == 当前活动目录 `plugin.json` 的 `Version` 即成功（清单是版本唯一真源）。**但** C# 改动场景下版本号会升、二进制却是旧的 —— 必须比哈希（`Get-FileHash`）。
5. **必须用 publish 实例验证**：`publish/ForgeSelf.exe --console`（Production → 数据根 `~/.forgeself`）。dev 实例数据根在程序目录 `Data`，与发布态不互通。
6. **dev reload 的已知边界**：宿主级 HostedService（如 ImGateway 的常驻连接）不随 reload 重启——reload 响应会带此 warning；改动含 HostedService 时冷启动 dev 宿主。

## 3. 判哈希（防假成功）

```powershell
(Get-FileHash "publish\plugins\<Dir>\<Dir>.dll").Hash
(Get-FileHash "Plugins\<id>\versions\<ver>\<Dir>.dll").Hash
# 两者必须一致
```

## 4. 相关

- dev 快速回路：`scripts/dev-plugin.ps1`（后端）、`scripts/dev-plugin-web.ps1`（前端 HMR，opt-in）
- 发布与验证技能：`.agents/skills/plugin-publish-verify/`
- 插件前端开发 SOP：`docs/05-guides/plugin-frontend-development.md`
