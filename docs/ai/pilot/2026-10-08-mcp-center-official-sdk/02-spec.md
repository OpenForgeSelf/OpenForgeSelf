# 02 Spec — McpCenter 接入官方 .NET MCP SDK（有状态会话）

## 1 Functional Requirements

| ID | 需求 |
| --- | --- |
| FR-1 | 网关改用官方 SDK 托管传输与协议：`builder.Services.AddMcpServer(...).WithHttpTransport(o => o.Stateless = false).WithTools<McpUniversalTool>()` + `app.MapMcp("/mcp")` |
| FR-2 | **有状态**：`initialize` 响应下发 `Mcp-Session-Id` 头；后续请求按会话关联（由 SDK 负责） |
| FR-3 | 对外工具面**保持 1 个** `universal_tool`（名称/描述/入参沿用 `UniversalToolForwarder.ToolDefinitionJson`），`tools/list` 恒返回 1 条 |
| FR-4 | `universal_tool` 的调用**仍经 `UniversalToolForwarder.ForwardAsync`** 分发（宿主 `IToolRegistry` + `mcp.<id>.<tool>` 外部服务器），语义与返回值不变 |
| FR-5 | 保留 Bearer 鉴权：配置了 token 时 `/mcp` 需 `Authorization: Bearer <token>`，否则 401 |
| FR-6 | 保留 `/health` 免鉴权探活，返回体含 status/version/tools/listen |
| FR-7 | 保留 `McpGatewayConfig` 三级端口优先（环境变量 > 插件数据根 config.json > 默认 18890） |
| FR-8 | 保留启停幂等与 XTrace 日志口径（铁律 14）；启动失败降级不抛、不阻塞宿主 |
| FR-9 | 不支持的协议版本 → 由 SDK 返回**显式错误** `-32022` + `supported` 列表（不再静默回退 `2025-06-18`） |

## 2 Input

- 入站：MCP Streamable HTTP 请求（`POST /mcp`，JSON-RPC 2.0；`GET /mcp` SSE；`GET /health`）。
- 工具入参：`{ "tool": "<目标工具名|mcp.<服务器id>.<工具名>>", "parameters": { ... } }`（`tool` 必填）。
- 配置：`McpGatewayConfig`（ListenUrl/Token）。

## 3 Output

- `tools/list`：`tools` 数组**长度恒为 1**，条目为 `universal_tool`。
- `tools/call(universal_tool)`：目标工具结果 JSON 原文（成功）；`{success:false, error:...}`（失败）。
- `initialize`：响应头含 `Mcp-Session-Id`；`result.protocolVersion` 为协商结果。
- 不支持版本：`error.code = -32022`，`error.data.supported = ["2024-11-05","2025-03-26","2025-06-18","2025-11-25"]`。

## 4 Business Rules

| ID | 规则 |
| --- | --- |
| BR-1 | `universal_tool` **不得**注册进宿主 `IToolRegistry`（避免与 AIAgent 同名冲突）——沿用现状 |
| BR-2 | 转发器**不得**调用自身（防自引用）——沿用 `UniversalToolForwarder` 现状 |
| BR-3 | `/health` 永不鉴权；`/mcp` 在配置了 token 时必须鉴权 |
| BR-4 | 日志仍走 `XTrace`，`builder.Logging.ClearProviders()` 保持，避免与宿主控制台重复 |
| BR-5 | 会话状态由 SDK 管理；**业务转发仍无状态**（每次 `ForwardAsync` 自包含），不引入业务侧会话依赖 |

## 5 Boundary Conditions

| ID | 边界 | 期望 |
| --- | --- | --- |
| BC-1 | 未配置 token | `/mcp` 不鉴权（现状行为保持） |
| BC-2 | 请求体 > 1 MB | 413（现状 `MaxRequestBodyBytes`；改用 SDK 后若丢失该上限，需以中间件补齐，**需实测确认**） |
| BC-3 | 有状态模式下客户端断开 | SDK 负责会话清理，网关不泄漏 |
| BC-4 | `GET /mcp` 不带 `Accept: text/event-stream` | 405（现状行为；改用 SDK 后由 SDK 决定，**需实测确认**） |
| BC-5 | 并发多会话 | 各会话独立，转发结果互不污染 |
| BC-6 | 客户端声明 `2026-07-28` | 显式 `-32022` 错误（**不静默降级**） |

## 6 Error Handling

| 场景 | 处理 |
| --- | --- |
| 未知工具名（非 `universal_tool`） | SDK 层返回 method not found；转发层沿用现状「未知工具预检带已注册数量」 |
| 转发失败 | 返回 `{success:false, error:...}` 原样透传（不抛、不吞） |
| 鉴权失败 | 401，且不进入 SDK 链路 |
| 网关启动失败 | XTrace.Error 记录，降级不可用，**不向上抛** |
| 协议版本不支持 | SDK 返回 `-32022` + supported 列表 |

## 7 Compatibility

| 项 | 结论（尖峰实证） |
| --- | --- |
| 官方 2.2.0 支持版本 | `["2024-11-05","2025-03-26","2025-06-18","2025-11-25"]` —— **与我们白名单逐字一致**，无版本覆盖回退 |
| 响应帧格式 | 官方默认 **SSE 帧**（`event: message` / `data: {...}`），现状为纯 JSON ⇒ **客户端解析方式变化** |
| 受影响的外部方 | DSH 补丁层（`http://127.0.0.1:18890/mcp`）；官方 MCP 客户端已实测可解析 SSE ⇒ 兼容；**DSH 侧需在升级后复测** |
| 依赖规模论证 | 新增 1 个 NuGet（`ModelContextProtocol.AspNetCore` 2.2.0，Apache-2.0，官方维护，3796 万下载），传递依赖随 ASP.NET Core 已有栈，**非大规模自造依赖**；回退路径：保留 `McpJsonRpcHandler` 不删，可切回 |

## 8 Non-functional Requirements

- 启动耗时不显著劣化（网关启动在插件加载路径上）。
- 单会话内存占用可控；会话随客户端断开释放。
- 不引入对宿主的引用（仅 Core + Abstractions + NuGet）。
- 日志口径不变（`[McpCenter]` 前缀）。

## 9 Acceptance Criteria

- [ ] AC1 `dotnet build Plugins/McpCenter/McpCenter.csproj` 0 错误
- [ ] AC2 `initialize` 响应头**含 `Mcp-Session-Id`**（官方 MCP 客户端实测）
- [ ] AC3 `tools/list` 返回 **1** 个工具 `universal_tool`
- [ ] AC4 `call_tool(universal_tool, {tool:"list_tools"})` 返回真实已注册工具清单
- [ ] AC5 声明 `2026-07-28` → 显式 `-32022` 错误（非静默回退）
- [ ] AC6 `/health` 免鉴权 200；`/mcp` 错/无 token → 401
- [ ] AC7 `McpCenterTests` 过滤集全绿
- [ ] AC8 未改 `Controllers/`、未改插件前端、未改宿主
