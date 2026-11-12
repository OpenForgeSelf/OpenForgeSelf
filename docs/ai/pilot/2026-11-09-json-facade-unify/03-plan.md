# 03 Plan — 2026-11-09-json-facade-unify

## Implementation Steps（file → 动作）
1. `ForgeSelf.Abstractions/ForgeJson.cs` → v2 扩展（FR-1 全 API；remarks 记「Deserialize 忽略 Encoder/策略 ⇒ 等价」依据，引批1 03 §Design Decision）。
2. `ForgeSelf.Api.Tests/Architecture/JsonNonAsciiEscapingGuardTests.cs` → 守卫 v2 不变量 + G1 新单测；**先跑出 RED**（预期 ~800 处命中明细留证，TDD 铁律）。
3. `ForgeSelf.Api` 36 文件（Lead 亲扫）：18 处 options 普查点、`AppBuilder.cs:161`、ForgeSelf.Api/Plugins 基建 8 文件、ChatSessionResolver 回环点、custom converter ref-reader 3 处。
4. 根 `Plugins` 119 文件 → 3 子代理分区机械改写（A: AIAgent/AgentHub/BrowserUse/ClockBadge/CostScope/DbAdmin*/DesignSystem/DevTools；B: FileTools/Home/ImGateway/McpCenter/MemorySystem/QuickLinks/SamplePlugin/Scheduler/ScriptRunner；C: Sems/SmartTerminal*/SystemMonitor/TodoTracker/ToolBridge/WorkflowEngine/其余）；禁改 Tests/web/plugin.json/Data；改前重读文件（并行批在动同一批文件，只碰 JSON 调用行）。
5. 全量构建 + 守卫 GREEN + 全量 `dotnet test` 对表基线（净新增红 = 0）。
6. 工件 05-07 + TODO/日记收口。

## 改写映射 v2（唯一规则表）
| 原形态 | 改为 |
|---|---|
| `JsonSerializer.Serialize(x)` / `(x, ForgeJson.Default)` | `ForgeJson.Serialize(x)` |
| `JsonSerializer.Serialize(x, opts)`（opts 已来自门面/静态字段） | `ForgeJson.Serialize(x, opts)` |
| `Serialize(x, new JsonSerializerOptions { WriteIndented = true, Encoder = … })` | `ForgeJson.SerializeIndented(x)` |
| `… new JsonSerializerOptions { 其他配置 }` | 冷路径内联 `ForgeJson.CreateOptions(o => { …原配置逐项搬入… })`；热路径提 `static readonly` 字段 |
| `JsonSerializer.Deserialize<T>(json\|bytes)` / `(+ opts)` | `ForgeJson.Deserialize<T>(json\|bytes[, opts])`（原裸调不传 opts） |
| `JsonSerializer.Deserialize<T>(ref reader, options)` | `ForgeJson.Deserialize<T>(ref reader, options)` |
| `element.Deserialize<T>(opts)` | `ForgeJson.Deserialize<T>(element, opts)` |
| `JsonDocument.Parse(s)` / `(s, jsonDocOpts)` | `ForgeJson.Parse(s[, jsonDocOpts])` |
| `new JsonWriterOptions { WriteIndented = …, Encoder = … }` | `ForgeJson.WriterOptions` / `WriterOptionsIndented`（按原缩进取用） |
| `.ToJsonString(ForgeJson.Default)` | 保持（已是门面形态，不动） |
| `AppBuilder.cs:161` Encoder 字面量 | `ForgeJson.Encoder` |
| target-typed `= new()` options 字段（PermissionProfileService.cs:20 等） | `= ForgeJson.CreateOptions(o => { … })` |

## options 配置普查（18 处改写基准，逐点原样搬移、禁止丢失配置项）
- CaseInsensitive(+Encoder) 读侧：PluginManager.cs:403、PluginArtifactValidator.cs:89、PluginPackagerService.cs:129/187/233、McpGatewayConfig.cs:67、PermissionClassifier.cs:213、PlanGeneratorService.cs:174、OpenAICompatibleProvider.cs:212/277、OpenAIResponsesProvider.cs:77（sweep 时逐点重读确认是否另含策略）。
- 写侧策略：AnthropicMessagesController.cs:285（SnakeCaseLower + WhenWritingNull + Encoder，SSE 热路径 → `static readonly`）、:375（CamelCase）；UnifiedUpstreamErrors.cs:159、DesignToolBase.cs:35、BuiltInAgentExecutionService.cs:99、WorkflowPlannerService.cs:196（sweep 时读点核对）。
- 等价化简：CatalogRepository.cs:428 inline ≡ Default → `ForgeJson.Default`；PermissionProfileService.cs:20 → CreateOptions。
- DevTools WriterOptions：JsonFormatterService.cs:22/31/59/66/376、YamlFormatterService.cs:87 → WriterOptions/WriterOptionsIndented。

## Test Plan
- 守卫 v2（G2）+ G1 新单测先 RED 后 GREEN；既有守卫（ProblemClassRegistryTests / ToolSchemaJsonGuardTests / RepositoryScriptTests）随全量复跑。
- 全量 `dotnet test`（中档：碰宿主源码 + 仓库级守卫）。

## Verification
- Build：`cd ForgeSelf.Api && dotnet build`（0 error）。
- Test：ForgeSelf.Api.Tests 全量，净新增红 = 0。
- E2E：不触发（未碰 e2e 基建/未发版）。
- Other：`pwsh scripts/check-problem-classes.ps1` 绿（非缺陷任务，确认台账不受扰）。

## 插件六步豁免（沿批1 07 §4 先例）
本批不改任何 plugin.json / web / e2e / 插件文档；插件侧改动仅 Services 内 JSON 调用点单行收敛；宿主 `dotnet build` 项目图编译全部插件 + 全量测试兜底 ⇒ 插件维护闭环六步豁免，07 记录豁免依据。

## Design Decision 补记
- 为什么收 Parse（198 处）：解析侧将来可统一旋钮真实存在（CommentHandling 容忍用户手改配置注释 / MaxDepth / AllowTrailingCommas），单点切换正是输入9 诉求。
- 为什么不收 JsonNode.Parse（21 处）：唯一选项 `JsonNodeOptions` 仅属性名大小写、无编码器/深度语义，无可统一项（YAGNI，避免门面膨胀）。
- 为什么保留 `Default`/`Indented` 并新增 `CreateOptions`：92 文件已引用 Default/Indented（批1 未提交基线），保 API 稳定；CreateOptions 覆盖 18 处自定义配置。
- 为什么 Async 不入面：生产代码 `SerializeAsync/DeserializeAsync` 实测 0 处；守卫禁令将迫使将来首个用点进面（届时加包装，不预发明接口）。
- 为什么不删 449 处既有 `ForgeJson.Default` 二参写法：`(x, ForgeJson.Default)` → `ForgeJson.Serialize(x)` 的简化属批1 面，本批顺带收敛（同文件顺手、零风险），不强制跨文件补刀。
