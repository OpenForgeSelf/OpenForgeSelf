# Intent — 插件本地目录更新源

> 阶段：Stage 1｜Task ID：PILOT-027 ｜ 日期：2026-09-28（返工 v2：采纳审查 CHANGES_REQUIRED 意见）

## Problem

宿主已支持「本地目录 zip 更新源 + 页面自动更新」，但**插件没有独立更新源**：插件新版本要么由开发侧脚本 stage 进本机 `_backups`（仅开发/内网可见），要么随宿主 zip 整体重打包（插件更新频率高时宿主被迫频繁发版）。插件市场页的「检查更新」目前只能发现 `_backups` 里已手动 stage 的版本，无法从用户指定目录自动发现新插件包。

## Why

用户已确认推荐方案：给插件做「本地插件包更新源」，与宿主本地目录更新**同构**——打包脚本把单插件打成 `.forgeself-plugin` 包输出到指定目录；配置页可配置该目录；插件市场「检查更新」自动扫描目录发现更高版本；点更新走现有版本化侧载（**不重启宿主**）。

## Expected Outcome

1. 新增「插件更新源」配置（MVP 仅本地目录，**无 Provider 字段**，审查 P2-4），落盘 `{数据根}/Config/plugin-update-settings.json`，配置页可读写、**可清空停用**（清空 = 未配置，审查 P1-3）。
2. `GET /api/plugin/updates` 除 `_backups` 外，还能发现插件更新源目录中版本更高的 `.forgeself-plugin` 包（来源标注 `Source=package`）。
3. `POST /api/plugin/update/{id}` 当 `_backups` 无更高版本时，自动从更新源目录取包、校验（含入口 DLL）、解包 stage 到 `_backups/<id>/<ver>/`，再走既有版本化切换（回滚/版本历史/保留 N 版本全部复用）；**纯包源（从未有 `_backups`）场景也可更新**（P1-1）。
4. 新增 `scripts/package-plugin.ps1 -Plugin <X> -OutDir <目录>`：构建插件并打出 `<id>-<ver>.forgeself-plugin` 包到指定目录（干净布局：根 = plugin.json + DLL + web/dist）。
5. **插件管理 tab（PluginsPanel.vue）**新增插件更新源（本地目录）输入框 + 保存（审查 P2-8：不放宿主更新面板，避免语义污染）。

## Constraints

- 不重启宿主：插件更新全程走 `POST /api/plugin/update/{id}` 版本化侧载（AGENTS 发布规范 + plugin-publish-verify）。
- 管理面鉴权铁律 17：新端点随 PluginController 类级 `[Authorize("ApiKeyPolicy")]` 自动受保护。
- 不破坏既有链路：`_backups` 扫描、`POST /api/plugin/install`（上传包）、`UpdateFromPackage` 覆盖式更新、回滚/版本历史全部保持现状。
- 插件包格式不变：`.forgeself-plugin`（zip，根含 plugin.json + 入口 DLL + web/dist），复用 `PluginPackagerService` 校验/解包。
- 安全：从用户指定目录读包是新的暴露面——路径拼接用已安装插件 `metadata.Id`、版本号必须 `Version.TryParse` 通过、解包目标必须位于 `_backups` 之下（P1-5）；包内入口 DLL 缺失必须视为更新失败（P1-6）。
- 禁止 agent 停/启/杀宿主；禁止删除数据目录（铁律 10）。
- 前端 UI 简洁（延续输入22 的更新源 UI 风格：简短说明 + 输入框，不解释原理）。

## Success Criteria

1. 后端 `dotnet build` 0 error；新增插件更新源相关单测全绿（配置持久化 + 目录扫描发现更高版本包 + 从包 stage 链路 + 纯包源场景）。
2. 前端 `pnpm run check` 0 error、vitest 全绿（新增用例覆盖配置保存/清空逻辑，文件命名 `.test.ts`，P1-8）。
3. 既有 3 个直接 `new PluginVersionService(...)` 的测试文件经「仅补 ctor 参数」适配后仍全绿（P0-1）。
4. 手工链路验证：插件管理页填本地目录 → `package-plugin.ps1` 打出包放该目录 → 插件市场「检查更新」出现该插件可更新 → 点更新后版本切换生效、回滚可用。（发布到用户实例后由用户实测；e2e 隔离实例用例列为后续项）