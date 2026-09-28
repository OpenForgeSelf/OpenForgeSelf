# 038 · 插件更新源（本地包目录）

> 状态：已实现（2026-09-28，输入27）｜设计：`docs/ai/pilot/027-plugin-local-update-source/`
> 关联：`docs/02-features/035-plugin-versioned-layout.md`（版本化侧载）、`docs/02-features/036-github-release-auto-update.md`（宿主自动更新）

## 一、背景与目标

宿主更新已支持「打 tag 自动发布 / 本地目录更新源」；但**插件更新**仍只有「开发期侧载
（`publish-plugin.ps1` stage + `POST /api/plugin/update/{id}`）」一条路，没有面向使用者的
独立更新源配置。本功能为插件补齐与宿主同构的「本地包目录更新源」：

- 设置页「插件管理 - 插件更新源」填写本地目录 → 插件市场「检查更新」扫描该目录 `*.forgeself-plugin` 包
  发现更高版本 → 点更新走既有版本化侧载（不重启宿主）。
- 与宿主更新源（`UpdateConfig` / 版本更新页）**完全分离**，各自独立配置与链路。

## 二、配置与持久化

- 模型：`PluginUpdateSettings`（仅 `LocalDir`，空字符串 = 未配置/停用）。
- 服务：`PluginUpdateSettingsService`（同构 `UpdateSettingsService`），落盘
  `{数据根}/Config/plugin-update-settings.json`；`PluginVersionService` / `PluginController`
  共享同一实例引用，运行时修改立即可见，无需重启宿主。
- 校验：非空目录必须 `Directory.Exists`，否则 `PUT` 返回 400「插件更新源目录不存在」。

## 三、API

| 方法 | 路径 | 说明 |
|---|---|---|
| GET | `/api/plugin/update-settings` | 回显当前配置 `{ "localDir": "..." }` |
| PUT | `/api/plugin/update-settings` | body `{ "localDir": "..." }`；空 = 停用；目录不存在 400 |
| GET | `/api/plugin/updates` | 现有端点，返回项新增 `source` 字段：`backup` / `package` |
| POST | `/api/plugin/update/{id}` | 现有端点；无 _backups 更高版本时自动从包目录 stage |

## 四、包格式与打包脚本

- 包：`<id>-<ver>.forgeself-plugin`（zip），根布局 = `plugin.json` + 入口 DLL + 依赖 + `web/dist/**`。
- 打包：`scripts/package-plugin.ps1 -Plugin <PascalCase目录> -OutDir <目录>`
  （`dotnet publish` → 排除宿主共享 DLL `ForgeSelf.*.dll|NewLife.*.dll|XCode.dll|MX.dll` → 压缩）。
  参数：`-Configuration Debug|Release`、`-Force`（覆盖已存在包）、`-DryRun`。含中文脚本已带 UTF-8 BOM。

## 五、更新发现与执行规则

- **发现**（`CheckForUpdates`）：`_backups` 最高版本（source=backup）与包目录最高版本（source=package）取更高者；
  包版本必须 **> 当前生效版本** 且 **> _backups 最高版本**，Id 匹配、版本号合法
  （`^\d+(\.\d+){0,3}$`，防路径穿越）、包内存在入口 DLL，才列为可更新。
- **执行**（`UpdatePlugin`）：在 `_backups` 检查**之前**调用 `EnsureStagedFromPackageSource`
  （纯包源场景也能更新）；选最高版本包 → 校验（入口 DLL + `ValidatePackage`）→ 解包到
  `_backups/<id>/<ver>/`（已存在则保留）→ 走既有版本化切换。包源全程内部捕获异常，失败仅记日志不传播 500。
- **回滚**：`POST /api/plugin/rollback/{id}` 既有链路不变。

## 六、前端

- 设置 - 插件管理 tab（`PluginsPanel.vue`）新增「插件更新源」卡片：说明 + 本地目录输入框 + 保存；
  留空保存 = 停用（后端 `LocalDir=""`）。加载时回显已保存目录。
- `src/services/pluginApi.ts` 新增 `fetchPluginUpdateSettings` / `updatePluginUpdateSettings`（走 `authFetch`）。

## 七、验证

- 后端：`ForgeSelf.Api.Tests/Services/PluginUpdateSettingsServiceTests.cs`（配置可变/落盘重载/清空停用）、
  `ForgeSelf.Api.Tests/Plugins/PluginVersionUpdateSourceTests.cs`（纯包源发现与更新/版本基准/包版本不高于当前/
  Id 不匹配/缺入口 DLL/无效包/清空停用）。手工包用 `ZipFile.CreateFromDirectory` 构造，全落临时目录。
- 前端：`ForgeSelf.Web/src/__tests__/PluginsPanel.test.ts`（回显/保存/清空停用/失败提示）。
- 门禁：`dotnet build` + `dotnet test`（含 3 个 ctor 适配测试回归）+ `pnpm run check` + `pnpm run test` + RepositoryScriptTests（BOM）。
