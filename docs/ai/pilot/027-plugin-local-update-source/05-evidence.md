# Evidence

> 阶段：Stage 7｜只记录实际发生的事情。
> 来源等级：Verified（亲自跑过，有真实输出）/ Inferred（凭代码推断）/ Unknown（未验证）。

## Task

PILOT-027：插件本地目录更新源（设置页插件管理 tab 配置本地插件包目录 → 插件市场检查更新发现更高版本 → 版本化侧载更新，不重启宿主；新增 `scripts/package-plugin.ps1` 打包插件包）

## Changed Files

后端（ForgeSelf.Api）：
- `Models/Plugins/PluginUpdateSettings.cs`（新建）
- `Services/PluginUpdateSettingsService.cs`（新建）
- `AppBuilder.cs`（注册 PluginUpdateSettingsService）
- `Models/Plugins/PluginDetailDto.cs`（PluginUpdateInfo.Source）
- `Plugins/Services/PluginVersionService.cs`（整文件重写：ctor 注入、CheckForUpdates 包源扫描、EnsureStagedFromPackageSource、ScanPackageSource、入口 DLL 校验、IsValidVersion）
- `Controllers/PluginController.cs`（注入 + GET/PUT update-settings）

测试（ForgeSelf.Api.Tests）：
- `Services/PluginUpdateSettingsServiceTests.cs`（新建，5 用例）
- `Plugins/PluginVersionUpdateSourceTests.cs`（新建，7 用例）
- `Plugins/PluginVersionServiceTests.cs` / `PluginFrontendManifestTests.cs` / `PluginMenuItemsMergeTests.cs`（仅补 ctor 参数适配，断言未改）

前端（ForgeSelf.Web）：
- `src/types/plugin.ts`（PluginUpdateInfo.source + PluginUpdateSettings）
- `src/services/pluginApi.ts`（fetchPluginUpdateSettings / updatePluginUpdateSettings）
- `src/components/settings/PluginsPanel.vue`（插件更新源卡片）
- `src/__tests__/PluginsPanel.test.ts`（新建，4 用例）

脚本/文档：
- `scripts/package-plugin.ps1`（新建，UTF-8 BOM）
- `docs/02-features/038-plugin-local-update-source.md`（新建）
- `ForgeSelf.Api/Plugins/README.md`（9.6 小节）

## Build

Command: `dotnet build ForgeSelf.Api`

Result: PASS（Verified）

```text
0 个错误（325 个警告均为既有文件：ChatSession/UpdateService 等，非本次改动引入）
BUILD_EXIT=0
```

## Unit Test

Command: `dotnet test --filter "FullyQualifiedName~PluginUpdateSettingsServiceTests|FullyQualifiedName~PluginVersionUpdateSourceTests"`

Result: PASS（Verified，12/12）

```text
已通过! - 失败: 0，通过: 12，已跳过: 0，总计: 12
```

Command: `dotnet test --filter "FullyQualifiedName~Plugin"`（插件相关全量回归，含 3 个 ctor 适配测试）

Result: 部分 FAIL（Verified）——509 用例中 507 通过、2 失败，**2 个失败均为既有问题、与本任务改动无文件交集**：

```text
失败! - 失败: 2，通过: 507，已跳过: 0，总计: 509
1) ScriptRunnerDiIntegrationTests.GetRuntimes_ShouldResolvePluginControllerThroughCordisContext
   → GET /api/scripts/runtimes 返回 404（期望 200）。请求的是 ScriptsController，
     与本任务改动（PluginController 仅新增端点）无交集；判定为集成测试环境/宿主插件发现问题。
2) TerminalCommandGuardTests.Check_EncodedCommand_DestructivePayload_Rejected
   → 断言 reason 包含 "remove-item"（小写），实际文案 "Remove-Item"（Pascal）。大小写断言不匹配，
     与 Terminal 安全策略文案相关，本任务未触碰。
```

Command: `dotnet test --filter "FullyQualifiedName~RepositoryScriptTests"`（.ps1 脚本 BOM 守卫）

Result: PASS（Verified，10/10）

```text
已通过! - 失败: 0，通过: 10，已跳过: 0，总计: 10
```

## Integration Test

Result: 见 Unit Test 第 2 项（插件全量回归中的 2 个既有失败，非本任务引入）

## E2E

Result: N/A（本次为宿主配置面板 + API + 打包脚本改动，未要求浏览器 e2e；前端交互以 vitest 覆盖，UI 走查待发布后按需进行）

## Static Analysis

Command: `cd ForgeSelf.Web && pnpm run check`（vue-tsc + eslint）

Result: PASS（Verified）

```text
✖ 81 problems (0 errors, 81 warnings) —— 0 错误；81 warnings 均为既有文件（systemMonitorApi/pluginViewLoader/SettingsView 等），新文件无 error 无 warning
CHECK_EXIT=0
```

Command: `cd ForgeSelf.Web && pnpm run test`（vitest 全量）

Result: PASS（Verified，477/477）

```text
Test Files  44 passed (44)
     Tests  477 passed (477)
```

## Screenshots

N/A（无 UI 截图；前端交互由单测覆盖）

## Known Limitations

- 插件更新源 MVP 仅支持「本地包目录」一种；远程插件源（Gitee/GitHub 镜像）未实现（设计 02-spec 已声明）。
- 包内入口 DLL 校验基于 plugin.json EntryAssembly 与 zip entry 存在性（防缺 DLL 假成功），不做强名称/签名校验。
- 本地目录更新源与宿主更新源完全分离（各自配置、各自链路）。

## Unresolved Issues

- 插件全量回归中 2 个既有失败测试（见上）：ScriptRunnerDiIntegrationTests（/api/scripts/runtimes 404）与 TerminalCommandGuardTests（大小写断言），均非本任务引入、与本次改动文件无交集；建议由各自 owner 另行处理（已记入 TODO 待办区，本次不顺手解决）。
- 会话B（dsh）在途文件曾导致测试项目一次编译抖动（InMemorySessionStore CS0246 过时缓存），重编译后恢复；会话B 文件本次未触碰、未提交。
- 未提交 git（用户审批中）；未发布（用户未要求；发布走打 tag 或本地打包路径，待指示）。
