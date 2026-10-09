# 03 Plan — 实施步骤（文件级）

## 决策记录（本任务）

| ID | 决策 | 理由 |
| --- | --- | --- |
| D1 | **保留** `McpJsonRpcHandler.cs` 与 `McpJsonRpcHandlerTests.cs` 不删，仅加「已不作为网关链路使用」头注释 | 删除会牵动 `McpCenterRuntimeTests` 与插件装配，属扩大范围；保留可作回退路径（依赖规模论证需要）。最终清理列后续 TODO |
| D2 | 网关鉴权改为**中间件**（`app.Use` 拦截 `/mcp`），不再内联在 handler | `MapMcp()` 后无法在 handler 内联鉴权；中间件是最小改动且覆盖全部 MCP 方法 |
| D3 | 新增 `Services/McpUniversalTool.cs` 承载 SDK 工具注册，内部转调 `UniversalToolForwarder` | 转发逻辑零改动，SDK 只做外壳；满足 FR-3/FR-4 |
| D4 | 有状态模式 `Stateless = false` | 用户明确指定「显式打开会话 id」（尖峰实证：默认不下发） |
| D5 | 请求体上限（BC-2）与 `GET` 405（BC-4）**先实测**再决定是否补中间件 | 不确定 SDK 是否自带；不臆造 |

## Implementation Steps

| # | 文件 | 动作 |
| --- | --- | --- |
| S1 | `Plugins/McpCenter/McpCenter.csproj` | 新增 `<PackageReference Include="ModelContextProtocol.AspNetCore" Version="2.2.0" />` |
| S2 | `Plugins/McpCenter/Services/McpUniversalTool.cs`（**新建**） | 非静态类（避 CS0718）；`[McpServerToolType]` + `[McpServerTool(Name="universal_tool")]`；方法签名 `(string tool, JsonElement? parameters)`；内部 `await forwarder.ForwardAsync(...)` 返回 `ForwardResult.Text`；`IsError` 时抛/标记由 SDK 处理 |
| S3 | `Plugins/McpCenter/Services/McpGatewayServer.cs` | ① 构造参数由 `McpJsonRpcHandler` 改为 `UniversalToolForwarder`（保留 `_pluginVersion`）；② `builder.Services.AddSingleton(forwarder)` + `AddMcpServer().WithHttpTransport(o => o.Stateless = false).WithTools<McpUniversalTool>()`；③ 删 `MapPost/MapGet("/mcp")`，改 `app.MapMcp("/mcp")`；④ `/mcp` 前置鉴权中间件；⑤ 保留 `/health`；⑥ 启动日志改口径（不再引用 `McpJsonRpcHandler.ProtocolVersion`） |
| S4 | `Plugins/McpCenter/McpCenterPlugin.cs`（`:57-59`、`:81`） | 改装配：不再 new `McpJsonRpcHandler`；日志行去掉 `McpJsonRpcHandler.ProtocolVersion` 引用 |
| S5 | `ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpCenterRuntimeTests.cs`（`:27-29`） | 同步新构造签名 |
| S6 | `Plugins/McpCenter/Services/McpJsonRpcHandler.cs` | 仅加头注释（D1），**不改逻辑** |
| S7 | `ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpGatewayServerSdkTests.cs`（**新建**） | 会话 id / `tools/list`=1 / 版本报错 / 鉴权 / health 用例 |

> **风险点（S3）**：`WithTools<T>` 的 DI 注入能否拿到 `UniversalToolForwarder` 实例未经本项目验证。若失败，改用 `McpServerTool.Create(...)` 手工构造 + `WithTools(tool)` 重载。实施时先编译验证，不臆造。

## Test Plan

| 层 | 命令 | 判据 |
| --- | --- | --- |
| 编译 | `dotnet build Plugins/McpCenter/McpCenter.csproj` | 0 错误 |
| 单测 | `dotnet test ForgeSelf.Api.Tests --filter McpCenterTests` | 全绿（含新增 S7 用例） |
| 集成（真实客户端） | 起隔离实例（非用户运行实例）+ 官方 MCP Python SDK 客户端 | AC2/AC3/AC4/AC5/AC6 |
| 回归 | `dotnet build ForgeSelf.Api` | 宿主 0 错误（插件登记未破） |

## Verification

- **Build**：`dotnet build Plugins/McpCenter/McpCenter.csproj` + `dotnet build ForgeSelf.Api`
- **Unit**：`dotnet test ForgeSelf.Api.Tests --filter McpCenterTests`
- **Integration**：隔离端口起网关 → 官方 SDK 客户端握手 → 断言 `Mcp-Session-Id` 存在 + `tools/list`=1 + `call_tool` 真实转发
- **Other**：BC-2（1MB 上限）与 BC-4（GET 405）实测确认；DSH 补丁层**不在本任务验证**（需用户升级宿主后复测，登记 TODO）
- ⛔ **禁止**停/启/杀用户运行中的 `:18890` / `:51888` 实例（红线）⇒ 集成验证一律用**隔离端口的独立进程**
