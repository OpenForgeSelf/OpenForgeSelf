# 插件热更新局限性 SOP（2026-08-30）

> 适用：当你「改了插件想让它生效」时，先用本 SOP 判定走哪条路。详细坑位见技能
> `.agents/skills/plugin-publish-verify/references/troubleshooting.md`，本文件只给**决策表**。

## 1. 决策表（先判你改了什么）

| 你改了 | 能否靠「覆盖活动插件目录」热更新 | 正解 |
|--------|----------------------------------|------|
| 仅 `plugin.json`（菜单 / 路由 / 版本号） | ✅ 可以 | 直接覆盖 `publish/Plugins/<Dir>/plugin.json`，watcher 自动重载 |
| 仅 `web/dist/**`（前端资源） | ✅ 可以 | 覆盖 `publish/Plugins/<Dir>/web/dist/**`；宿主加载器按 `?v=version` 刷新，**无需重启宿主** |
| 改了 C# 代码（入口 `<Dir>.dll`） | ❌ **不行** | 入口 DLL 被 ALC 独占锁，覆盖必失败（且 `Copy-Item` 误报 `FileNotFoundException`，**假成功陷阱**）。正解二选一：① 停宿主再覆盖；② 走 side-by-side 版本（`versions/<ver>/` + `current` 指针切换）—— 但需先修 `PluginVersionService.Initialize` 未调用缺陷 |

## 2. 铁律

1. **覆盖顺序：payload（DLL、web/dist）先，`plugin.json` 最后**。先写清单会在拷贝中途触发重载，后续 DLL 拷贝报 `Could not find file`。
2. **活动插件目录只放插件自身程序集**：`<Dir>.dll` + `plugin.json` [+ `web/dist`]。**绝不能**放入 `XCode.dll` / `NewLife.*.dll` / `ForgeSelf.*.dll` —— 类型标识分裂 + ALC 卸载中加载 → **宿主启动即崩**。
3. **版本 API 当前不可用（兜底失效）**：`GET /api/plugin/updates`、`POST /api/plugin/update/{id}` 全死（根因 `PluginVersionService.Initialize` 从未调用）。当前**主路径 = 覆盖活动目录被 watcher 自动热重载**，不要调版本 API。
4. **更新成功判定**：接口返回的该插件 `version` == 当前活动目录 `plugin.json` 的 `Version` 即成功（清单是版本唯一真源）。**但** C# 改动场景下版本号会升、二进制却是旧的 —— 必须比哈希：`publish/Plugins/<Dir>/<Dir>.dll` vs `Plugins/<id>/versions/<ver>/<Dir>.dll` 必须一致，否则是**假成功**（2026-09-28 输入31 去 `_backups`）。
5. **必须用 publish 实例验证**：`publish/ForgeSelf.exe --console`（Production → 数据根 `~/.forgeself`）。dev 实例数据根在程序目录 `Data`，与发布态不互通。

## 3. 判哈希（防假成功）

```powershell
(Get-FileHash "publish\Plugins\<Dir>\<Dir>.dll").Hash
(Get-FileHash "Plugins\<id>\versions\<ver>\<Dir>.dll").Hash
# 两者必须一致
```

## 4. 相关

- 发布与验证技能：`.agents/skills/plugin-publish-verify/`
- 插件前端开发 SOP：`docs/05-guides/plugin-frontend-development.md`
