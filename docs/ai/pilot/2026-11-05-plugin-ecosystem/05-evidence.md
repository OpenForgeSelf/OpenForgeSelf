# Evidence

> 阶段：Stage 7。只记录实际跑过的命令和日志正文。

## Task

2026-11-05-plugin-ecosystem

## Changed Files

宿主与测试：`ForgeSelf.Api/AppBuilder.cs`、`PluginController.cs`、`PluginManager.cs`、`PluginVersionLayout.cs`、`ServiceCollectionExtensions.cs`、`PluginInstallerService.cs`、`PluginPackagerService.cs`、`PluginVersionService.cs`、`PluginUpdateSettings.cs`、`PluginMetadata.cs`，以及新增 `PluginReleaseVersion.cs`、`PluginArtifactValidator.cs`、`PluginBuildService.cs`、`PluginHostTools.cs`、`PluginJobStore.cs`、`PluginPackageProviders.cs`、`PluginSourceResolver.cs`、`PluginSrcScaffold.cs`、`PluginEcosystemContractTests.cs`。

前端：`PluginStore.vue`、`pluginApi.ts`、`stores/plugin.ts`、`types/plugin.ts`、`PluginsPanel.vue`、`PluginImportExport.vue`、`PluginScaffolder.vue`。

契约与脚本：`docs/04-standards/packaging-upgrade-backup.md` §1.1 / §1.9、`scripts/package-plugin.ps1`、`scripts/README.md`。

隔离骨架（不进 git）：`.temp/dev-stack/data/plugins/hello-world/src/`。

未改：`DataLocationService` 默认分支、`ForgeSelf.Api.csproj` 的 VersionPrefix、`.env`。

## Build

Command:

```bash
dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj --nologo -v q
```

Result: PASS（来源等级：Verified）

```text
0 个错误
1461 个警告
已成功生成。
已用时间 00:06:41.48
BUILD_EXIT=0
日志：.temp/tmp/plugin-eco-build.log
```

HelloWorld 骨架：

```bash
dotnet build .temp/dev-stack/data/plugins/hello-world/src/HelloWorld.csproj --nologo -v q
```

Result: PASS（来源等级：Verified）。前两次失败已改掉后重跑。

```text
第一次 HW_EXIT=1：NETSDK1018 无效的 NuGet 版本字符串 1.0.0.2611052140
第二次 HW2_EXIT=1：CS0246 未找到 ForgeSelf / IPlugin / IContext（Exists 相对路径未从工程目录解析）
第三次 HW3_EXIT=0：已成功生成。0 个警告 0 个错误。已用时间 00:00:05.15
```

## Unit Test

第一次过滤集（断言按大小写敏感反序列化 camelCase 清单，失败）：

```text
失败: 1，通过: 56，总计: 57
失败项：PluginInstallerServiceTests.UpdateFromPackage_StagesWithoutActivating
Expected activeManifest!.Version to be "1.0.0" ... but "" has a length of 0
TEST_EXIT=1
日志：.temp/tmp/plugin-eco-test.log
```

修正测试的 `PropertyNameCaseInsensitive` 后重跑。

Command:

```bash
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj --filter "FullyQualifiedName~PluginEcosystem|FullyQualifiedName~PluginInstaller|FullyQualifiedName~PluginVersionUpdateSource|FullyQualifiedName~PluginPackager|FullyQualifiedName~PluginRoots|FullyQualifiedName~PluginVersionService" --nologo --verbosity minimal
```

Result: PASS（来源等级：Verified）

```text
已通过! - 失败: 0，通过: 57，已跳过: 0，总计: 57，持续时间: 4 s
TEST2_EXIT=0
日志：.temp/tmp/plugin-eco-test2.log
环境：TEMP/TMP=.temp\tmp，NO_PROXY=localhost,127.0.0.1,::1
```

## Integration Test

中档全量（`scripts/package-plugin.ps1` 改动触发 §5.6 中档）。

Command:

```bash
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj --nologo --verbosity minimal
```

Result: FAIL（来源等级：Verified）。本批引入的 1 条已修并复测通过。其余 35 条不在本批改动的插件安装/版本/工具文件上。

```text
失败! - 失败: 36，通过: 3687，已跳过: 0，总计: 3723，持续时间: 19 m 50 s
FULL_EXIT=1
日志：.temp/tmp/plugin-eco-fulltest.log
```

本批新增红（已修）：

```text
ProcessOutputEncodingGuardTests.生产源码_子进程输出_禁止固定码页解码
PluginBuildService.cs 使用了 BeginOutputReadLine / BeginErrorReadLine
```

改为 `ProcessOutputText.CaptureAsync` + `Decode` 后：

```bash
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj --filter "FullyQualifiedName~ProcessOutputEncodingGuardTests" --nologo --verbosity minimal
```

```text
已通过! - 失败: 0，通过: 1，总计: 1，持续时间: 4 s
ENC_EXIT=0
日志：.temp/tmp/plugin-eco-encoding.log
```

同一次全量里另外 35 条失败的名字：`ProjectWorkspaceDefaultRootTests` 15、`TodoDispatchFlowTests` 2（期望 `d:\` 实际 `D:\`）、`MafApprovalLoopTests` 1、`UnifiedAIGatewayIntegrationTests` 8、`RunTerminalCommandCwdRequiredTests` 1（同样是 `D:\` 与 `d:\`）、`DesignAgentToolContractTests` 1（`success` 为 false）、`ScriptRunnerDiIntegrationTests` 1（404）、`WorkflowPlanningIntegrationTests` 6（404）。日志里没有 `Unable to resolve`。这些断言不指向本批新增的安装器或工具文件。全量没有在编码修复后再跑一遍。

## E2E

Result: N/A（未跑 Playwright）。没有 `.temp/dev-stack.json`。没有启动或停止 dev-stack，没有接触 :51888。

## Static Analysis

Command（`ForgeSelf.Web`）：

```bash
pnpm run check
pnpm run test
pnpm run design:check
```

Result: PASS（来源等级：Verified）

```text
CHECK_EXIT=0
TEST_EXIT=0
 Test Files  106 passed (106)
      Tests  1154 passed (1154)
   Duration  44.88s
DESIGN_EXIT=0
日志：.temp/tmp/plugin-eco-web.log
```

`scripts/package-plugin.ps1` 读盘时已带 UTF-8 BOM（BOM_BEFORE=True），本次没有重写 BOM。

## Screenshots

N/A。PluginStore 的安装区与「激活此版本」没有在浏览器里走查。

## Acceptance Criteria Coverage

| AC | 证据 | 等级 |
|----|------|------|
| AC1 | `PluginEcosystemContractTests.FirstInstall_LandsUnderDataRootPlugins` | Verified（单测） |
| AC2 | `PluginEcosystemContractTests.ProductionShapedRoot_UsesThatPluginsDirectory` | Verified（单测） |
| AC3 | `PluginEcosystemContractTests.LegacyExtension_IsRejected` | Verified（单测） |
| AC4 | `PluginEcosystemContractTests.ZipSlip_IsRejectedAndDoesNotEscape` | Verified（单测） |
| AC5 | `PluginEcosystemContractTests.DllManifest_IsReadWithoutLoadingAssembly` | Verified（单测） |
| AC6 | `PluginEcosystemContractTests.FirstInstall_LandsUnderDataRootPlugins` + `PluginInstallerServiceTests.UpdateFromPackage_StagesWithoutActivating`（再次安装不动 `current`） | Verified（单测） |
| AC7 | `PluginEcosystemContractTests.Uninstall_KeepsSourceAndForge` | Verified（单测） |
| AC8 | `PluginEcosystemContractTests.Activate_SwitchesPointer_AndFailedLoadRollsBack`（激活后 `current` = 目标串） | Verified（单测） |
| AC9 | 同左用例（坏版本激活 → `current` 回到旧串） | Verified（单测） |
| AC10 | `PluginEcosystemContractTests.VersionComparer_DateCode_OrdersByTimestamp` | Verified（单测） |
| AC11 | `PluginEcosystemContractTests.Package_OmitsSourceDirectory`（另 `Package_OmitsHostSharedAssembliesPdbAndFrameworkExtensions`） | Verified（单测） |
| AC12 | `PluginEcosystemContractTests.PublishGithub_WithoutToken_FailsClosed` | Verified（单测） |
| AC13 | `PluginStore.vue` 源码断言（安装区 + 「激活此版本」）+ 宿主 `pnpm run check` / `pnpm run test`（106 文件 / 1154 通过） | Verified（源码+门禁）；未做浏览器走查（见 Screenshots/Known Limitations） |

## Known Limitations

- `plugin.publish.github` / `plugin.publish.nuget` 在密钥存在时也返回失败，宿主不代上传、不打 tag。无 `GITHUB_TOKEN` 的失败闭合有单测。
- 未对公网 GitHub Release 或 NuGet 做真实下载。
- 未执行 `package-plugin.ps1` 打出一个真实插件 zip。脚本行为来源等级：Unknown。
- 激活回滚的证据是单测 `Activate_SwitchesPointer_AndFailedLoadRollsBack`，不是运行中的宿主。

## Unresolved Issues

- 隔离实例走查与浏览器验收未做。
- 用户禁止的 push / tag / GitHub Release / NuGet 发布未做。
