# 00 Repository Understanding — McpCenter 接入官方 .NET MCP SDK

> 任务目录：`docs/ai/pilot/2026-10-08-mcp-center-official-sdk/`
> 生成时间：2026-10-08｜全部结论基于**仓库实读**，非常识推断。

## 1. 项目速查（复核自规范基线 + 实读）

| 项 | 实际值 | 来源 |
| --- | --- | --- |
| 后端 | .NET 10 + SQLite + NewLife.XCode + 插件架构 | `ForgeSelf.Api/`、`Plugins/` |
| 前端 | Vue 3.5 + Vite 6 + TS 5.7 + Element Plus + pnpm | `ForgeSelf.Web/` |
| 测试 | xUnit + Moq + FluentAssertions（后端）；Playwright（e2e） | `ForgeSelf.Api.Tests/` |
| 构建 | `dotnet build` / `dotnet test`；宿主前端 `pnpm run check`+`pnpm run test` | AGENTS.md §5 |
| MCP 网关 | 独立 Kestrel，默认端口 **18890** | `Plugins/McpCenter/Services/McpGatewayConfig.cs:14` |

## 2. McpCenter 现状（实读 `find Plugins/McpCenter -name "*.cs"`）

**传输与协议层**
- `Services/McpGatewayServer.cs` — 自管理 Kestrel：
  - `WebApplication.CreateSlimBuilder(...)` + `builder.WebHost.UseUrls(_config.ListenUrl)`（`:53-55`）
  - `app.MapPost("/mcp", HandlePostAsync)`、`MapGet("/mcp", HandleGetAsync)`、`MapGet("/health", HandleHealthAsync)`（`:59-61`）
  - `HandlePostAsync`（`:120-157`）：内联 Bearer 鉴权 → 读 body（上限 1MB）→ 转 `_handler.HandleRequestAsync` → **仅设 `Content-Type: application/json` 写回**；**无任何会话 id 逻辑**
  - `HandleGetAsync`（`:159-193`）：要求 `Accept: text/event-stream`，仅 15s 心跳，无真实服务端推送
  - `HandleHealthAsync`（`:195-201`）：**不鉴权**
- `Services/McpJsonRpcHandler.cs` — 自研 JSON-RPC 2.0 分发器：
  - `ProtocolVersion = "2025-06-18"`、`SupportedProtocolVersions = {"2025-11-25","2025-06-18","2025-03-26","2024-11-05"}`（`:15-18`）
  - 支持 `initialize` / `notifications/initialized` / `ping` / `tools/list` / `tools/call`；单条 + 批处理；通知返回 null → 202

**业务转发层（本次必须保留）**
- `Services/UniversalToolForwarder.cs` — 万能工具转发器：
  - 常量 `ToolName = "universal_tool"`（`:26`）；`ToolDefinitionJson`（`:38-57`）为对外唯一工具契约
  - `ForwardAsync(argumentsJson)`（`:63+`）：解析 `{tool, parameters}`；`tool` 以 `mcp.` 开头 → 外部 MCP 服务器；否则经宿主 `IToolRegistry` 分发
  - **不注册进宿主注册表**（避免与 AIAgent 同名冲突）
- `Services/ListToolsToolFunction.cs`、`Services/McpClientManager.cs`、`Services/McpClient/*` — 外部 MCP 客户端接入（v2.1.0）

**装配与配置**
- `McpCenterPlugin.cs`、`Services/McpCenterRuntime.cs` — 插件注册与网关生命周期
- `Services/McpGatewayConfig.cs` — 端口三级优先：环境变量 `FORGESELF_MCP_GATEWAY_PORT` > 插件数据根 `config.json` > 默认 18890
- `Controllers/` — `McpController` / `McpCenterConfigController` / `McpCenterDshController` / `McpExternalController`（宿主 REST 面，**本次不动**）

## 3. 受影响面（实读）

**后端测试（13 个文件，`ForgeSelf.Api.Tests/Plugins/McpCenterTests/`）**
`ListToolsToolFunctionTests` / `McpAdminAuthTests` / `McpCenterRuntimeTests` / `McpClientIntegrationTests` / `McpClientSessionTests` / `McpControllerIntegrationTests` / `McpExternalConfigTests` / `McpFrameTests` / `McpGatewayConfigTests` / **`McpJsonRpcHandlerTests`** / `McpServiceTests` / `McpServiceToolRegistryTests` / `UniversalToolForwarderTests`

> ⚠️ `McpJsonRpcHandlerTests` 直接测自研分发器，是本次**最大回归面**。

**前端 / e2e**
- 插件前端：`Plugins/McpCenter/web/src/`（`McpCenterView.vue`、`DshMcpPanel.vue`、`api/`、`types/`）
- e2e：`ForgeSelf.Web/e2e/plugins/mcp-center/`

**外部依赖方（实读确认）**
- DSH 补丁层：MCP 中心「网关配置→写入 dsh 配置」产出地址 `http://127.0.0.1:18890/mcp`（用户 2026-10-07 确认）

## 4. 上一轮尖峰实证（已跑通，`.temp/mcp-spike` 已删）

| 结论 | 读数 |
| --- | --- |
| 包版本 | `ModelContextProtocol.AspNetCore` **2.2.0**（`dotnet package search`，nuget.org） |
| net10.0 兼容 | `dotnet add package` 成功，**0 警告** |
| 编译形态 | `AddMcpServer().WithHttpTransport().WithTools<T>() + MapMcp("/mcp")` **0 错**；`WithTools<T>` 不可传 static 类（CS0718） |
| 官方支持版本 | `["2024-11-05","2025-03-26","2025-06-18","2025-11-25"]` —— **与我们白名单逐字一致** |
| 不支持版本 | 显式错误 `code=-32022` + `data.supported`（非静默回退） |
| 会话 id（默认） | **不下发**（无状态） |
| 会话 id（有状态） | `WithHttpTransport(o => o.Stateless = false)` → 下发 `Mcp-Session-Id: PP1XODUk-x6r_Epoq5j-zQ` ✅ |
| facade 兼容 | 单个 `[McpServerTool(Name="universal_tool")]` → 官方 Python SDK 客户端 `list_tools`=1、`call_tool` 成功 ✅ |
| 响应格式 | 官方默认 **SSE 帧**（`event: message` / `data: {...}`），我们现为纯 JSON |

## 5. 候选低风险切入与选择理由

本次任务由用户直接指定（选项 A），非从候选中挑选。范围边界见 `01-intent.md`。

## 6. 关键约束（写入 Spec/Plan 的依据）

1. 插件只引用 `ForgeSelf.Core` + `ForgeSelf.Abstractions`，**不引用宿主**（实测 19 个插件 csproj 同此约束）⇒ 加 NuGet 包无结构障碍。
2. 网关**必须保留**：Bearer 鉴权（`/mcp` 要、`/health` 不要）、`McpGatewayConfig` 三级端口、XTrace 日志、`StartAsync/StopAsync` 幂等生命周期（铁律 14）。
3. **不得停/启/杀用户运行中的宿主进程**（AGENTS.md 红线）⇒ 验证只能打运行实例只读请求，或另起隔离实例。
4. 官方包属**新增依赖**；规范 §1 硬性约束 4「不新增大规模依赖」需显式论证（见 `02-spec.md` 兼容性章）。
