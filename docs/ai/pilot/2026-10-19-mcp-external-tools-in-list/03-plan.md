# Plan

> 阶段：Stage 3 ｜ Task ID：PILOT-072 ｜ 日期：2026-10-19
> 顺序按 TDD：契约 → 提供方（红→绿）→ 消费方两处（红→绿）→ 文案/版本 → e2e → 门禁。

## Implementation Steps

### A. 契约层（ForgeSelf.Abstractions）

- `ForgeSelf.Abstractions/IMcpExternalGateway.cs`:新建 —— `interface IMcpExternalGateway { IReadOnlyList<McpExternalToolInfo> ListConnectedTools(); Task<string> InvokeAsync(string fullName, string argumentsJson, CancellationToken ct); }` + `record McpExternalToolInfo(string FullName, string ServerId, string Name, string Description, string InputSchemaJson)`。reason：跨插件契约必须入 Abstractions（architecture-design 铁律 2）。
- `ForgeSelf.Abstractions.Tests/McpExternalGatewayContractTests.cs`:新建 —— 契约形状守卫（接口成员签名 + record 字段，反射断言，防签名漂移）。reason：契约层有测试先例（Abstractions.Tests 现存 9 件）。

### B. McpCenter 提供方

- `Plugins/McpCenter/Services/McpExternalGatewayAdapter.cs`:新建 —— internal 薄适配包 `McpClientManager`：`ListConnectedTools` = `GetStates()` 过滤已连接 → `GetTools(id)` 映射 DTO → `McpExternalToolInfo`（BC-4 按服务器 try/catch）；`InvokeAsync` = 复用 `ForwardExternalAsync` 同款「首段点拆 `mcp.<id>.<tool>`」拆分（坏格式抛中文异常）→ `CallExternalAsync` 透传。reason：拆分逻辑单一真源对齐（同类扫查：网关/AIAgent 两消费侧对同一全名解析必须一致）。
- `Plugins/McpCenter/McpCenterPlugin.cs`:Apply 内 `clientManager` 构造后 —— `ctx.Register<IMcpExternalGateway>(new McpExternalGatewayAdapter(clientManager))`。reason：裁决 A/F 标准提供姿势（eager + 共享表 + Fiber 摘除）。
- `Plugins/McpCenter/Services/ListToolsToolFunction.cs`:构造改 `(IContext? ctx, IMcpExternalGateway? external = null)`（McpCenterPlugin 传实例，测试可传假）；ExecuteAsync 合并外部条目（FR-3 字段/排序/过滤/schema 规则）；类注释同步（「数据源 = 注册表 + 外部网关合并」）。reason：枚举出口单点合并，网关与 AIAgent 自动同享（AC-3）。
- `Plugins/McpCenter/Services/UniversalToolForwarder.cs`:仅 `ToolDefinitionJson` 描述文案（FR-5）。reason：文案与行为一致，防「列出来但提示语说没有」。

### C. AIAgent 消费方

- `Plugins/AIAgent/Services/ToolFunctions/UniversalTool.cs`:①注册表分发前插 `mcp.` 分支（FR-4 三态：null 契约/异常/成功）；②`Description`（`:25`）文案更新（FR-5）。reason：与网关路由同语义，内外一致；工具已持 IContext，无需新注册。

### D. 测试

- `ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpExternalGatewayAdapterTests.cs`:新建 —— 拆分正确/坏格式抛/未连接抛/已连接映射（Moq 假 Manager 或真 Manager + 临时 store）。
- `ForgeSelf.Api.Tests/Plugins/McpCenterTests/ListToolsToolFunctionTests.cs`:新建 —— ①假契约三工具合并出现且排序在宿主后；②keyword 命中外部名/描述；③includeSchema 带 schema、坏 JSON 置 null；④契约 null → 与现状等价（AC-1/2/5）。
- `ForgeSelf.Api.Tests/Plugins/AIAgentTests/UniversalToolExternalRouteTests.cs`:新建 —— Moq `IMcpExternalGateway` + 假 IContext：成功透传/异常封套/契约缺失中文错误不落 unknown（AC-4）。既有 `UniversalToolTests` 全量复跑保不回归（AC-5）。
- 既有集成用例检查：`McpClientIntegrationTests`（真连 mock 走 universal_tool）不受影响；`list_tools` 相关既有用例若断言 total 精确值 → 无外部连接时结果不变，预期零改动，红则按可期断言更新。

### E. e2e

- `ForgeSelf.Web/e2e/plugins/mcp-center/mcp-center.spec.ts`:新用例（v2.6.0）——stdio mock 连接 → 网关 `universal_tool {tool:"list_tools"}` 断言 `mcp.<id>.echo` 条目在列（含 description）→ `mcp.<id>.echo` 实调成功回显 → finally DELETE + kill。reason：AC-6 走真实网关链路零 mock。

### F. 版本与文档

- `Plugins/McpCenter/McpCenter.csproj` + `plugin.json`:2.5.2 → **2.6.0**，Description 追加「外部 MCP 工具进 list_tools 枚举 + 跨插件直调契约」。
- `Plugins/AIAgent/plugin.json`:1.8.0 → **1.9.0**（AIAgent 版本单一落点=plugin.json，csproj 无 Version 行，已核），Description 追加「universal_tool 支持 mcp. 前缀直调外部 MCP 工具」。
- `docs/02-features/034-mcp-center.md`:§转发契约改写（「list_tools 不枚举外部工具」条目反转为合并规则；「三条可用通道」通道 3 更新）；F034-R04 行补「枚举出口合并」；**决策 D10 修订记录**。
- `docs/07-decisions/`:D10 修订落 ADR 或台账补记（枚举合并 vs 注册表隔离的裁决理由）。
- `TODO.md`:勾掉「内置 AIAgent 无 mcp. 路由」P3 条目（被本批取代）。

## Test Plan

| 层 | 命令 | 判定 |
| --- | --- | --- |
| 构建 | `dotnet build ForgeSelf.Api` | 0 错 |
| 单测 | `dotnet test --filter "FullyQualifiedName~McpCenter\|FullyQualifiedName~AIAgent\|FullyQualifiedName~Abstractions"` | 全绿（新增 ≈12 用例） |
| 插件 e2e | `playwright test mcp-center.spec.ts -g "v2.6.0"` | 1 passed |
| 现场 | dev-stack（已运行）真连 deepwiki → `list_tools` 含 3 个 `mcp.deepwiki.*` → 直调成功 | 截图/DOM 证据 |

中风险（契约层新增）→ 相关域全量复跑，不跑全仓 sln 全量（既有漂移基线另账）。

## Verification

- Build/Unit/Integration/E2e 如上表；Evidence 阶段逐条 AC 标 Verified/Inferred。
- 回滚点：全部改动 git 可回溯；契约纯增量，回滚不影响 v2.5.2 超时批。

## 风险与协调

- 与 v2.5.2 批同插件：两批代码叠加（2.6.0 含 2.5.2 全部改动），**发布合并为一次**（2.6.0 发版时超时批门禁④⑤⑥同窗收口，避免连发两版）。
- 悬空引用风险已被裁决 F + FM-3 覆盖（每次 Get + null 降级 + 单测锚定）。

## 实施偏差记录（Implement 阶段回填，规范 §Stage5-7）

1. **ListToolsToolFunction 改为运行期 `ctx.Get<IMcpExternalGateway>()`**（原定构造注入 `(IContext?, IMcpExternalGateway?)`）：更符合 architecture-design 铁律 4「禁缓存为字段」（构造字段注入 = 缓存实例，提供方热重载后悬空）；测试改经 Moq 假 IContext 驱动，覆盖等价（假契约 6 新例全绿）。
2. **全名解析单一真源上移 `ForgeSelf.Abstractions.McpExternalToolNames`**（原定真源在 `McpClientManager.TrySplitExternalToolName`）：AIAgent 因 ALC 隔离无法引用 McpCenter 程序集，不则两侧拆解语义会漂移；`McpClientManager.ExternalPrefix` 改指向契约常量，`UniversalToolForwarder`/AIAgent `UniversalTool` 均改用 `McpExternalToolNames.TrySplit/Prefix`。铁律 2 同源精神的一致适用（命名规则也是跨插件契约）。
3. **AIAgent 路由测试落点**：原定 `ForgeSelf.Api.Tests/Plugins/AIAgentTests/`目录不存在，实际与既有 `UniversalToolTests.cs` 同目录同命名空间（`ForgeSelf.Api.Tests/Plugins/UniversalToolExternalRouteTests.cs`）。
4. **e2e 用例外部服务器用 streamable-http mock**（原定 stdio mock）：仓内 `startMock` 参数化只支持 http/sse 两模式（stdio 无需 mock 进程、由宿主自拉 node），http 模式链路等价且更快；断言面不变（枚举/实调/断开归零）。

