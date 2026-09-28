# Plan — 插件本地目录更新源

> 阶段：Stage 3｜Task ID：PILOT-027 ｜ 返工 v2：按审查意见补文件清单/编号/测试落点/命令/脚本编码

## Files To Change

### 后端（改/新）

- `ForgeSelf.Api/Models/Plugins/PluginUpdateSettings.cs`（**新增**）
  reason: 插件更新源配置模型（仅 LocalDir，P2-4）
- `ForgeSelf.Api/Services/PluginUpdateSettingsService.cs`（**新增**）
  reason: 配置持久化服务（同构 UpdateSettingsService），落盘 `{数据根}/Config/plugin-update-settings.json`
- `ForgeSelf.Api/AppBuilder.cs`
  reason: 注册 PluginUpdateSettingsService（dataLocation.GetHostDataDirectory()，与 UpdateSettingsService 并列；PluginVersionService 注册在 ServiceCollectionExtensions.cs 由 DI 自动解析新 ctor 参数，**无需改该文件**，P0-1 说明）
- `ForgeSelf.Api/Plugins/Services/PluginVersionService.cs`
  reason: ctor 增注 PluginUpdateSettingsService + PluginPackagerService；CheckForUpdates 增包目录扫描；UpdatePlugin 在读取 _backups **之前**插入 EnsureStagedFromPackageSource（P1-1/P1-4/P1-5/P1-6）
- `ForgeSelf.Api/Models/Plugins/PluginDetailDto.cs`
  reason: PluginUpdateInfo 增 `Source`（string?，backup|package；**不加 SourcePath**，P2-3）
- `ForgeSelf.Api/Controllers/PluginController.cs`
  reason: 新增 GET/PUT update-settings 端点（注入 PluginUpdateSettingsService；响应风格对齐 PluginController 既有 ApiResponse+400，P2-2）

### 后端测试（改/新）

- `ForgeSelf.Api.Tests/Services/PluginUpdateSettingsServiceTests.cs`（**新增**，放 Tests/Services/ 与宿主配置测试同目录）
  reason: 配置默认空/Update 落盘/LoadPersisted 覆盖/清空=停用/损坏回退
- `ForgeSelf.Api.Tests/Plugins/PluginVersionUpdateSourceTests.cs`（**新增**，放 Tests/Plugins/ 与 PluginVersionServiceTests 同目录，P2-7）
  reason: CheckForUpdates 包目录扫描 + UpdatePlugin 从包 stage（含纯包源）+ 版本基准统一 + 入口 DLL 缺失失败
- `ForgeSelf.Api.Tests/Plugins/PluginVersionServiceTests.cs`（**改**，P0-1）
  reason: 直接 `new PluginVersionService(...)` 的既有测试，仅补 ctor 参数（mock/null 注入），不改断言
- `ForgeSelf.Api.Tests/Plugins/PluginMenuItemsMergeTests.cs`（**改**，P0-1）
  reason: 同上，仅补 ctor 参数
- `ForgeSelf.Api.Tests/Plugins/PluginFrontendManifestTests.cs`（**改**，P0-1）
  reason: 同上，仅补 ctor 参数

### 前端

- `ForgeSelf.Web/src/services/pluginApi.ts`
  reason: +fetchPluginUpdateSettings / updatePluginUpdateSettings
- `ForgeSelf.Web/src/types/plugin.ts`
  reason: +PluginUpdateSettings 接口（P2-3 补列）
- `ForgeSelf.Web/src/components/settings/PluginsPanel.vue`
  reason: 「插件更新源」小节（说明 + 输入框 + 保存；清空=停用）——UI 落点按审查 P2-8
- `ForgeSelf.Web/src/components/settings/__tests__/PluginsPanel.test.ts`（**新增**，`.test.ts` 命名，P1-8）
  reason: 插件更新源保存/清空逻辑用例（沿用既有测试结构）

### 脚本

- `scripts/package-plugin.ps1`（**新增**，含中文 → UTF-8 BOM，P2-11）
  reason: 构建插件 → 打 `<id>-<ver>.forgeself-plugin` 包到 OutDir（产物收集规则与 publish-plugin.ps1:139-143 一致）

### 文档

- `docs/02-features/038-plugin-local-update-source.md`（**新增**，编号避开 036，P1-7）
  reason: 功能说明（配置/脚本/页面链路）
- `ForgeSelf.Api/Plugins/README.md` 第九节补「插件更新源（本地目录）」小节
  reason: 开发/运维入口文档同步

## Implementation Steps

1. 新建 `PluginUpdateSettings` 模型 + `PluginUpdateSettingsService`（复制 UpdateSettingsService 骨架：ctor(initial, path) + LoadPersisted + Update + Current + _gate）。
2. `AppBuilder.cs`：在 UpdateSettingsService 注册旁注册 PluginUpdateSettingsService（initial = new PluginUpdateSettings()，path = `{数据根}/Config/plugin-update-settings.json`）。
3. `PluginDetailDto.cs`：PluginUpdateInfo 增 `Source`（string?）。
4. `PluginVersionService.cs`：
   - ctor 增注 `PluginUpdateSettingsService` + `PluginPackagerService`；
   - `CheckForUpdates()`：_backups 扫描保留；新增包目录扫描（顶层 *.forgeself-plugin → ReadPackageMetadata → 版本更高且 > _backups 最高 → Source=package 更新项）；
   - `UpdatePlugin(pluginId)`：在 `var backupDir = ...` 与两个提前 return **之前**插入 `EnsureStagedFromPackageSource`：
     a. 扫包目录 → 候选 = Id 匹配 + Version.TryParse 通过 + version > currentVersion 且 > _backups/<id>/ 最高目录版本；
     b. ValidatePackage → 校验包内根存在 EntryAssembly 文件（ZipArchive 查 entry）；
     c. ExtractPackage 到 `_backups/<id>/<ver>/`；
     d. 整体 try/catch，异常 → XTrace.Error + 返回 null（无包源），不传播；
   - 主流程不变。
5. `PluginController.cs`：注入 PluginUpdateSettingsService；GET/PUT update-settings（PUT：空=停用、非空目录校验 400）。
6. 后端测试：PluginUpdateSettingsServiceTests + PluginVersionUpdateSourceTests（临时目录 + 真实 zip 包 + 纯包源场景）+ 3 个既有测试仅补 ctor 参数。
7. `pluginApi.ts` + `types/plugin.ts`：两个新方法 + 类型。
8. `PluginsPanel.vue`：插件更新源小节（GET 回显 + PUT 保存/清空；成功/失败消息）。
9. 前端测试：`PluginsPanel.test.ts`（mock pluginApi）。
10. `scripts/package-plugin.ps1`：publish → 收集（排除宿主共享 DLL）→ plugin.json + web/dist → Compress-Archive；-Force 覆盖；幂等跳过；**UTF-8 BOM**。
11. 文档：features 038 + Plugins/README.md 补节。

## Test Plan

1. `PluginUpdateSettingsServiceTests`：默认 LocalDir 空；Update 落盘后 LoadPersisted 恢复；清空（""）落盘生效；损坏 JSON 回退默认不崩。
2. `PluginVersionUpdateSourceTests`（Tests/Plugins/）：
   - 临时目录放真实 .forgeself-plugin（手工 zip：plugin.json + 假 DLL）→ CheckForUpdates 发现 Source=package 更新项；
   - UpdatePlugin 纯包源（无 _backups）→ stage 成功 → current 指针 == 包版本；
   - 包版本 ≤ 当前 或 ≤ _backups 最高 → 无更新项、不覆盖；
   - 包缺入口 DLL → 更新失败（false）；
   - 无效包 → 跳过不中断。
3. 前端 vitest：PluginsPanel 保存调用（成功/失败消息/清空=停用）。
4. 回归：`dotnet test --filter "FullyQualifiedName~Plugin"` 全绿（含 3 个适配后的既有测试）。

## Verification

### Build

```powershell
cd D:\src\my-proj\OpenForgeSelf\OpenForgeSelf\ForgeSelf.Api
dotnet build
cd D:\src\my-proj\OpenForgeSelf\OpenForgeSelf\ForgeSelf.Web
pnpm run check
```

### Unit Test

```powershell
cd D:\src\my-proj\OpenForgeSelf\OpenForgeSelf\ForgeSelf.Api.Tests
dotnet test --filter "FullyQualifiedName~PluginUpdateSettingsServiceTests|FullyQualifiedName~PluginVersionUpdateSourceTests"
dotnet test --filter "FullyQualifiedName~Plugin"   # 回归（含 3 个适配测试）
cd D:\src\my-proj\OpenForgeSelf\OpenForgeSelf\ForgeSelf.Web
pnpm run test
```

### Integration Test

N/A（ForgeSelf.Api.Tests/Integration 存在但本任务不新增集成测试：服务层单测已覆盖配置/扫描/stage 链路；宿主更新链路的集成式验证由既有 Update 相关测试与用户实测承担，P2-12 说明理由）

### E2E

本期不新增 e2e 用例（理由，P2-12）：插件更新源配置 UI 在插件管理 tab，其主链路（检查更新→更新→回滚）已有既有 e2e/plugin-store 相关用例覆盖服务端契约；新配置小节的可视化走查在发布后用户实测 + 截图确认；若后续要自动化，按 e2e-testing 技能补 `e2e/plugin-update-source.spec.ts`（记 TODO）。

### Other Checks

- 脚本编码守卫：`dotnet test --filter "FullyQualifiedName~RepositoryScriptTests"`（package-plugin.ps1 含中文必须 BOM）
- 打包脚本产物交叉验证：跑 `package-plugin.ps1` 后解包核对 plugin.json/入口 DLL 存在，并用 ValidatePackage 语义（plugin.json 三字段）核对

## Plan 偏差记录

> 实现中发现 Plan 与仓库实际不符时，先在此记录偏差，再修正 Plan，不得直接绕过。

| 时间 | 偏差点 | 原 Plan | 修正后 |
| --- | --- | --- | --- |