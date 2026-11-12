# Plan

> 阶段：Stage 3｜`2026-11-05-plugin-ecosystem`｜闸门1：用户点 Build 批准计划文件，视为已批。

## Files To Change

| 文件 | 动作 | 原因 |
| --- | --- | --- |
| `ForgeSelf.Api/Plugins/PluginVersionLayout.cs` | 增加 `SourceDirectory` / `ForgeDirectory` / `JobsDirectory` / `EnsureLayout` | 运行目录契约 |
| `ForgeSelf.Api/Plugins/Services/PluginArtifactValidator.cs` | 新增 | ZIP 路径校验；DLL 内嵌清单只读元数据 |
| `ForgeSelf.Api/Plugins/Services/PluginPackagerService.cs` | 只打版本目录；安全解压；拒绝 `.forgeself-plugin` | P1 |
| `ForgeSelf.Api/Plugins/Services/PluginInstallerService.cs` | 注入 `IDataLocationService`；首次写 current；再次只暂存；卸载留 src | p-data-root + P1 |
| `ForgeSelf.Api/Plugins/Services/PluginVersionService.cs` | 公开 `Activate`；失败回滚；扫描 `*.zip`；`GetPluginVersions` 用 `PluginDirectory`；`VersionComparer` 改世代比较 | P1 |
| `ForgeSelf.Api/Plugins/PluginManager.cs` | `ComparePluginVersions` 对纯数字段走世代比较，畸形串仍返回 0 | 与现有 PluginRootsTests 对齐 |
| `ForgeSelf.Abstractions/PluginMetadata.cs` | `HostApiVersion` | 清单字段 |
| `ForgeSelf.Api/Controllers/PluginController.cs` | install 只收 zip/dll；`activate`；下载名 `.zip`；source 安装 | P1/P4 |
| `ForgeSelf.Api/AppBuilder.cs` | 安装器接到数据根 plugins；注册宿主工具 | p-data-root / P2 |
| `ForgeSelf.Api/Models/Plugins/PluginUpdateSettings.cs` | GitHub/NuGet 源地址；删 `.forgeself-plugin` 说明 | P4 |
| `ForgeSelf.Api/Plugins/Services/PluginSourceResolver.cs` 及 GitHub/NuGet provider | 新增 | P4 |
| `ForgeSelf.Api/Plugins/Services/PluginJobStore.cs`、`PluginBuildService.cs`、`PluginHostTools.cs` | 新增 | P2/P3 |
| `ForgeSelf.Api/Plugins/ServiceCollectionExtensions.cs` | 注册新服务 | DI |
| `ForgeSelf.Web/src/views/PluginStore.vue`、`services/pluginApi.ts`、`stores/plugin.ts`、`types/plugin.ts` | 安装入口与激活 | P4 |
| `ForgeSelf.Web/src/views/PluginImportExport.vue`、`PluginScaffolder.vue`、`components/settings/PluginsPanel.vue` | 去掉 `.forgeself-plugin` 文案与 accept | 与契约一致，避免死格式 |
| `scripts/package-plugin.ps1` | ZIP 规则 | P5 |
| `docs/04-standards/packaging-upgrade-backup.md` | §1.9 契约 + 变更记录；修正仍写旧扩展名的现状句 | P0，不另起文件 |
| `ForgeSelf.Api.Tests/Plugins/PluginEcosystemContractTests.cs` 及既有安装/更新源测试 | 新断言；旧包扩展名改为 `.zip` | 验收 |
| `.temp/dev-stack/data/plugins/hello-world/src/**` | 隔离骨架，不进 git 跟踪范围（`.temp`） | P5 |

## Implementation Steps

1. 布局与校验器、世代比较。
2. 打包/安装/版本/控制器接线。
3. 工具、job、GitHub/NuGet 解析（HttpClient 可注入，测试不访问公网）。
4. 前端市场。
5. `package-plugin.ps1` 与 HelloWorld 骨架。
6. 真源文档。
7. 过滤测试，再前端三门禁。

## Test Plan

- `PluginEcosystemContractTests`：AC1–AC12。
- 更新 `PluginInstallerServiceTests`：再次安装不激活。
- 更新 `PluginVersionUpdateSourceTests`：包扩展名为 `.zip`，并断言 `.forgeself-plugin` 被忽略。
- `PluginRootsTests` 畸形版本用例保持期望值。
- 前端：check / test / design:check。

## Verification

- Build：`dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj`
- Unit：`dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj --filter "FullyQualifiedName~PluginEcosystem|FullyQualifiedName~PluginInstaller|FullyQualifiedName~PluginVersionUpdateSource|FullyQualifiedName~PluginPackager|FullyQualifiedName~PluginRoots|FullyQualifiedName~PluginVersionService"`
- 前端：`pnpm run check`、`pnpm run test`、`pnpm run design:check`
- 中档：改了 `scripts/package-plugin.ps1`。全量 `dotnet test` 若与已知基线红重叠，只把新增失败记为本批。时间不够时在 Evidence 写明未跑全量及原因，不以过滤集冒充全量。

## 偏差记录

实施中若与仓库不符，先改本文件再改代码。
