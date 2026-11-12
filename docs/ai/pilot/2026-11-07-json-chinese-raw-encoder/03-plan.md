# 03 Plan — 2026-11-07-json-chinese-raw-encoder

## Implementation Steps（file:line → 动作）
1. `ForgeSelf.Abstractions/ForgeJson.cs` → 新建共享选项（Default/Indented）。
2. `ForgeSelf.Api.Tests/Architecture/JsonNonAsciiEscapingGuardTests.cs` → 新建 G2 源码枚举守卫 + G1 单测；**先跑出红**（TDD 铁律）。
3. `ForgeSelf.Api/AppBuilder.cs:158` → `AddControllers()` 追加 `AddJsonOptions` 设 Encoder。
4. 宿主生产代码扫点（~25 文件）：`ToolRegistry.cs`（9 处工具结果）、`SessionEventJsonConverter.cs`、`ChatController.cs`、`WebSocketBroadcaster.cs`、`PersistentApprovalService.cs`、`PermissionProfileService.cs`、`UsageStatsService.cs`、`WorkflowUsageService.cs`、`UpdateSettingsService.cs`、`PluginUpdateSettingsService.cs`、`LocalModelGate.cs`、`LocalFileImageRecognitionCache.cs`、`UnifiedUpstreamErrors.cs`、`ChatSessionResolver.cs`、`AgentStreamTranslator.cs`、`AgentChatController.cs`、`OpenAIChatController.cs`、`AnthropicMessagesController.cs`、`PluginArtifactValidator.cs`、`PluginSrcScaffold.cs`、`PluginSourceResolver.cs`、`PluginScaffolderService.cs`、`PluginHostTools.cs`、`PluginJobStore.cs`。
5. 插件扫点（22 插件 `*.cs`，3 个子代理并行分区；禁改 Tests/web/plugin.json/Data）：同映射规则。
6. 全量构建 + 守卫转绿 + 全量 `dotnet test` 对表基线。
7. 工件 05-07 + PC-027/case/索引 + TODO/日记收口。

## 改写映射（唯一规则表）
- `Serialize(x)` → `Serialize(x, ForgeJson.Default)`
- `Serialize(x, new JsonSerializerOptions { WriteIndented = true })` → `Serialize(x, ForgeJson.Indented)`
- `Serialize(x, new JsonSerializerOptions { WriteIndented = false })` → `Serialize(x, ForgeJson.Default)`
- 其他 `new JsonSerializerOptions/JsonWriterOptions {…}`（含 target-typed `new()`）→ 初始化器补 `Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,`
- `ToJsonString()` → `ToJsonString(ForgeJson.Default)`
- 仅 Deserialize 使用的 options → 不动。

## Test Plan（守卫覆盖）
- 新增守卫：G1（ForgeJson 行为单测）+ G2（全仓源码枚举：单参 Serialize / 缺 Encoder options 初始化器；阳性对照扫描计数下限）。红灯→绿灯计划：步骤 2 先 RED 留证，步骤 3-6 后 GREEN。
- 既有守卫复跑：全量 `dotnet test` 覆盖（含 ProblemClassRegistryTests / ToolSchemaJsonGuardTests / RepositoryScriptTests）。

## Verification
- Build：`cd ForgeSelf.Api && dotnet build`（0 error）。
- Unit/Integration：`dotnet test`（ForgeSelf.Api.Tests 全量，中档——碰宿主源码触发 §5.6 中档）。
- E2E：不触发（未碰 e2e 基建/未发版；批次级裁剪理由见 07 §问题类归档）。
- Other：`pwsh scripts/check-problem-classes.ps1` 绿。

## Design Decision 补记：为什么不用全局设置（2026-11-07 输入8 Q&A 回写）
- 问：`JsonSerializer` 是静态类，有没有全局设置默认行为的方式，避免修改面过大？
- 判定：**System.Text.Json 无进程级默认配置 API**，三层"全局点"逐一核查：① `JsonSerializerOptions.Default`（裸调用的隐式选项）静态属性只读（实测 `CanWrite=False`）且实例冻结（实测赋 Encoder 抛 `InvalidOperationException: This JsonSerializerOptions instance is read-only or has already been used in serialization or deserialization`），无环境变量/AppContext 开关/官方钩子可换默认 Encoder；② ASP.NET Core `AddJsonOptions` 只覆盖 MVC 管线（本批 `AppBuilder.cs:161` 改 1 行），管不到直调 `JsonSerializer.Serialize` 的落盘配置/审计/工具回显/插件程序集内部序列化；③ inline `new JsonSerializerOptions{…}` 的语义本就是显式覆盖默认，全局 API 即便存在也管不到。对照：Newtonsoft.Json 有 `JsonConvert.DefaultSettings` 全局回调，STJ 有意不提供（显式优于隐式）。
- 实测（pwsh 7.6.9 / .NET STJ）：`Serialize("目录上溯中文测试", null)` → `"\u76EE\u5F55\u4E0A\u6EAF..."`；同调用传 `UnsafeRelaxedJsonEscaping` → `"目录上溯中文测试"`。
- 结论：中文原文必须在**每个写出入口**拿到正确 Encoder ⇒ 逐入口单行替换（映射表见上「改写映射」）是该框架约束下的最小修改面；框架约束摘要已同步 `ForgeSelf.Abstractions/ForgeJson.cs:13-16` remarks。
- 备选不取：迁 Newtonsoft（有全局 `JsonConvert.DefaultSettings`，但换库 = 契约/依赖/行为三重风险 > 单行替换）；只改 HTTP 层 1 行（落盘/审计/插件面仍 `\uXXXX`，不满足"全项目"诉求）；DI 包一层序列化服务（插件是隔离程序集，内部静态直调不经宿主 DI，不可达）；反射/IL 织入改写调用点（无官方支持点，脆弱度极高）。
