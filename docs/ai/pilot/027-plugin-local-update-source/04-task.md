# Agent Task — 插件本地目录更新源

> 阶段：Stage 4｜Task ID：PILOT-027｜前序工件（00-04 返工 v2）齐备且经闸门1 确认。

## Task ID

PILOT-027

## Objective

仓库达到：插件支持「本地插件包目录更新源」——插件管理 tab 可配置目录（可清空停用），`GET /api/plugin/updates` 能发现目录中更高版本的 `.forgeself-plugin` 包（Source=package），`POST /api/plugin/update/{id}` 能从包 stage（含纯包源场景）并走版本化切换（不重启宿主）；`scripts/package-plugin.ps1` 可打出插件包；前后端门禁与新增测试全绿、插件既有测试零回归。

## Scope

### Allowed

按 03-plan.md「Files To Change」（返工 v2）修改/新增：
- 后端：`Models/Plugins/PluginUpdateSettings.cs`（新）、`Services/PluginUpdateSettingsService.cs`（新）、`AppBuilder.cs`（注册）、`Plugins/Services/PluginVersionService.cs`（ctor + 扫描 + EnsureStagedFromPackageSource）、`Models/Plugins/PluginDetailDto.cs`（Source 字段）、`Controllers/PluginController.cs`（update-settings 端点）
- 后端测试：`Tests/Services/PluginUpdateSettingsServiceTests.cs`（新）、`Tests/Plugins/PluginVersionUpdateSourceTests.cs`（新）；`Tests/Plugins/PluginVersionServiceTests.cs`、`Tests/Plugins/PluginMenuItemsMergeTests.cs`、`Tests/Plugins/PluginFrontendManifestTests.cs`（**仅补 ctor 参数，不改断言**，P0-1）
- 前端：`src/services/pluginApi.ts`、`src/types/plugin.ts`、`src/components/settings/PluginsPanel.vue`、`src/components/settings/__tests__/PluginsPanel.test.ts`（新，`.test.ts` 命名，P1-8）
- 脚本：`scripts/package-plugin.ps1`（新，含中文 → UTF-8 BOM）
- 文档：`docs/02-features/038-plugin-local-update-source.md`（新，编号避开 036）、`ForgeSelf.Api/Plugins/README.md` 补节
- 设计工件：`docs/ai/pilot/027-plugin-local-update-source/`（00-04 返工 v2 已落盘）

### Forbidden

- 停/启/杀任何用户运行中的宿主进程（`D:\src\tools\ForgeSelf`、`:51888` 等）
- 修改 `UpdateFromPackage` 覆盖式语义、`/api/plugin/install` 上传流程、rollback/versions 既有行为
- 改动宿主更新源（`UpdateConfig`/`UpdateController`/`UpdateChecker`/`UpdateSettingsService`/`UpdatePanel.vue` 宿主更新源部分）
- 混入会话B（dsh）工作区未提交文件（SessionEvents/SessionEventMap/ISessionStore/ChatController 等）
- 删除任何数据目录/数据库文件（铁律 10）
- 引入新 NuGet/前端依赖
- 超出 Plan 文件清单的无关重构；3 个既有测试除 ctor 参数外不得改断言/逻辑

## Acceptance Criteria

- [ ] AC1 后端 `dotnet build` 0 error
- [ ] AC2 新增测试文件用例全绿（配置持久化/包目录扫描/从包 stage/纯包源/版本基准统一/入口 DLL 缺失失败）
- [ ] AC3 前端 `pnpm run check` 0 error、vitest 全绿
- [ ] AC4 `GET/PUT /api/plugin/update-settings` 符合 FR-2（目录不存在 400、清空=停用）
- [ ] AC5 `package-plugin.ps1` 产出包 ValidatePackage 通过 + 入口 DLL 存在 + 含中文时 BOM（RepositoryScriptTests 绿）
- [ ] AC6 插件既有测试零回归（3 个 ctor 适配 + rollback/versions/install 相关用例全绿）
- [ ] AC7 文档同步（038 features + Plugins/README.md）

## Expected Files

- `ForgeSelf.Api/Models/Plugins/PluginUpdateSettings.cs`（新）
- `ForgeSelf.Api/Services/PluginUpdateSettingsService.cs`（新）
- `ForgeSelf.Api/Plugins/Services/PluginVersionService.cs`（改）
- `ForgeSelf.Api/Controllers/PluginController.cs`（改）
- `ForgeSelf.Api/Models/Plugins/PluginDetailDto.cs`（改）
- `ForgeSelf.Api/AppBuilder.cs`（改）
- `ForgeSelf.Api.Tests/Services/PluginUpdateSettingsServiceTests.cs`（新）
- `ForgeSelf.Api.Tests/Plugins/PluginVersionUpdateSourceTests.cs`（新）
- `ForgeSelf.Api.Tests/Plugins/{PluginVersionServiceTests,PluginMenuItemsMergeTests,PluginFrontendManifestTests}.cs`（仅 ctor 参数）
- `ForgeSelf.Web/src/services/pluginApi.ts`（改）
- `ForgeSelf.Web/src/types/plugin.ts`（改）
- `ForgeSelf.Web/src/components/settings/PluginsPanel.vue`（改）
- `ForgeSelf.Web/src/components/settings/__tests__/PluginsPanel.test.ts`（新）
- `scripts/package-plugin.ps1`（新）
- `docs/02-features/038-plugin-local-update-source.md`（新）

## Verification Commands

```powershell
# 后端（PowerShell 5.1 不支持 &&，拆分执行）
cd D:\src\my-proj\OpenForgeSelf\OpenForgeSelf\ForgeSelf.Api
dotnet build
cd D:\src\my-proj\OpenForgeSelf\OpenForgeSelf\ForgeSelf.Api.Tests
dotnet test --filter "FullyQualifiedName~PluginUpdateSettingsServiceTests|FullyQualifiedName~PluginVersionUpdateSourceTests"
dotnet test --filter "FullyQualifiedName~Plugin"
dotnet test --filter "FullyQualifiedName~RepositoryScriptTests"
# 前端
cd D:\src\my-proj\OpenForgeSelf\OpenForgeSelf\ForgeSelf.Web
pnpm run check
pnpm run test
# 脚本产物交叉验证（临时目录）
powershell -NoProfile -ExecutionPolicy Bypass -File D:\src\my-proj\OpenForgeSelf\OpenForgeSelf\scripts\package-plugin.ps1 -Plugin <X> -OutDir <临时目录>
# 解包核对 zip 根含 plugin.json 与入口 DLL
```