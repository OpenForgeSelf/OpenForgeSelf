# 01 Intent — McpCenter 接入官方 .NET MCP SDK 并开启有状态会话

## Problem（为什么做）

当前 McpCenter 的 MCP 传输层与协议分发器为**自研实现**（`McpGatewayServer` + `McpJsonRpcHandler`），已实证存在三类问题：

1. **无状态、不下发 `Mcp-Session-Id`**（实测 initialize 响应头无该头，伪造 id 也被接受）⇒ 依赖有状态会话/断点续传的客户端能力缺失。
2. **协议版本静默降级**：客户端声明非白名单版本（含 `2026-07-28`）时，服务端静默回退 `2025-06-18` 且不告知 ⇒ 客户端误判协商结果。
3. **协议层需自行维护**：SSE 帧、批处理、续传、未来规范演进都要自己实现与排错。

## Why（为什么是现在）

用户在 2026-10-08 对比三条路线后**明确拍板选 A**：全量替换为官方 `ModelContextProtocol.AspNetCore` **2.2.0**，并**显式打开有状态会话**（`WithHttpTransport(o => o.Stateless = false)`）。

> 闸门1 授权记录：用户原话「A 显式打开会话id」（2026-10-08，本会话），对应选项 A = 全量替换 + 显式开启有状态会话 id。

## Expected Outcome（做到什么程度）

1. MCP 传输层与协议分发交由官方 SDK 托管（`MapMcp()`），自研 `McpJsonRpcHandler` 退出网关链路。
2. **服务端下发 `Mcp-Session-Id`**（有状态模式），并被官方 MCP 客户端实测拿到。
3. 对外**仍只有 1 个 `universal_tool`**：`tools/list` 恒返回 1 条，转发逻辑（`UniversalToolForwarder`）**行为不变**。
4. 不支持的协议版本 → **显式报错 `-32022` + supported 列表**（消除静默降级）。
5. 既有能力不回退：Bearer 鉴权、`/health` 免鉴权、`McpGatewayConfig` 端口三级优先、XTrace 日志、启停幂等。

## Constraints（约束）

| # | 约束 | 依据 |
| --- | --- | --- |
| C1 | 只改 McpCenter 插件，**不改宿主**、不改其他插件 | 范围控制（AGENTS.md §1.3） |
| C2 | 保留 `UniversalToolForwarder` 语义与 `ToolDefinitionJson` 契约 | 107 工具转发面 + 外部 MCP 客户端接入面 |
| C3 | 保留 `/health` 免鉴权、`/mcp` Bearer 鉴权 | 现状行为，DSH/运维依赖 |
| C4 | 保留 `McpGatewayConfig` 三级端口优先与默认 18890 | `McpGatewayConfig.cs:14` |
| C5 | **禁止停/启/杀用户运行中的宿主进程** | AGENTS.md 红线 |
| C6 | 新增 NuGet 依赖需论证「非大规模」并留回退路径 | 规范 §1 硬性约束 4 |
| C7 | 不动 `Controllers/`（宿主 REST 面）与插件前端 | 范围外 |

## Success Criteria（完成标准）

1. `dotnet build Plugins/McpCenter/McpCenter.csproj` **0 错误**。
2. `McpCenterTests` 过滤集**全绿**（含新增的会话 id / 版本报错用例）。
3. **官方 MCP 客户端实测**：`initialize` 响应头**含 `Mcp-Session-Id`**，`list_tools`=1，`call_tool(universal_tool)` 返回真实转发结果。
4. 声明 `2026-07-28` → 返回**显式错误**而非静默回退。
5. `/health` 仍 200 且免鉴权；`/mcp` 无/错 token → 401。
