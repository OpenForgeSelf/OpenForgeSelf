# Specification

> 阶段：Stage 2｜必须从真实 Repository Understanding 与 Intent 推导。
> 规则：① 所有内容与实际项目一致；② 不得发明不存在的接口、类、模块；③ 不确定点显式记录为 `Unknown`，不得自行假定。
> Task ID：PILOT-mcp2-protocol ｜ 日期：2026-10-01

## Functional Requirements

| # | 需求 | 现状（代码事实） | 目标 |
| --- | --- | --- | --- |
| FR1 | 对外服务端协议协商支持 MCP 2.0 | `Services/McpJsonRpcHandler.cs:17` `SupportedProtocolVersions = {2025-06-18,2025-03-26,2024-11-05}` | 白名单加入 `2025-11-25`；客户端声明 `2025-11-25` → `initialize` 回显 `2025-11-25` |
| FR2 | 默认/回退版本保持 1.x | `McpJsonRpcHandler.cs:14-15` `ProtocolVersion="2025-06-18"`（兼作未知版本回退值） | **保持不变**：未知/缺失版本客户端仍回退 `2025-06-18`（向后兼容，不静默升 2.0） |
| FR3 | 外部客户端声明支持 2.0 | `Services/McpClient/McpClientSession.cs:14` `SupportedProtocolVersions = {2025-06-18,2025-03-26,2024-11-05}` | 列表加入 `2025-11-25`（放最前）；服务器返回所选版本的既有接受逻辑不变（`ConnectAsync` :33-43 已通用） |
| FR4 | initialize 响应 2.0 契约面增强 | `McpJsonRpcHandler.cs:183-190` serverInfo = {name, version} | serverInfo 增加可选 `description`（2.0 minor #2：`Implementation.description`，人读上下文） |
| FR5 | tools/list 保持现状 | `McpJsonRpcHandler.cs:193-200` 恒 1 个 `universal_tool`；`UniversalToolForwarder.ToolDefinitionJson` 无 icon/annotations | **不变**：2.0 中 icon（SEP-973）/annotations 均为可选元数据，不声明即合规；不为展示能力扩范围 |
| FR6 | 版本号统一 bump | `plugin.json` Version=2.1.1；`McpCenter.csproj:9-11` Version/AssemblyVersion/FileVersion=2.1.0.0（双源不一致存量） | 统一为 **2.2.0 / 2.2.0.0**（协议能力变更） |
| FR7 | 测试夹具支持 2.0 协商 | `ForgeSelf.Api.Tests/Plugins/McpCenterTests/Fixtures/mock-mcp-server.js:11` `PROTOCOL_VERSIONS = [2025-06-18,2025-03-26,2024-11-05]` | 加入 `2025-11-25`（协商逻辑已通用：白名单回显/否则回退第一个） |
| FR8 | e2e 断言覆盖 2.0 | `ForgeSelf.Web/e2e/plugins/mcp-center/mcp-center.spec.ts:108-110` initialize 仅断言 2025-06-18 | 新增：声明 `2025-11-25` → 回显 `2025-11-25`（保留旧断言） |
| FR9 | 功能文档同步 | `docs/02-features/034-mcp-center.md` 协议版本表（:82）与验证记录 | 协议版本行补 `2025-11-25`（MCP 2.0）说明 + 验证记录 v2.2.0 |

## Input

- 外部 MCP 客户端经 `POST /mcp` 发送 JSON-RPC 2.0（单条/批处理/通知），核心为 `initialize {protocolVersion, capabilities, clientInfo}`、`tools/list`、`tools/call`。
- 外部 MCP 服务器经三传输（stdio / Streamable HTTP / HTTP+SSE）回应客户端 `initialize`/`tools/list`/`tools/call`。

## Output

- `initialize` 响应：`protocolVersion`（白名单回显 / 默认回退 `2025-06-18`）、`capabilities.tools.listChanged=false`、`serverInfo {name:"ForgeSelf McpCenter", version, description?}`。
- 客户端侧：连接后 `ProtocolVersion` 保存服务器所选版本（含 `2025-11-25`），后续会话按该版本语义工作（本网关不依赖版本差异分支，无额外处理）。

## Business Rules

1. 版本协商：客户端声明的 `protocolVersion` 在支持白名单内 → 服务端回显该版本；不在白名单（含缺失）→ 服务端回退默认 `2025-06-18`。（现状规则，仅白名单扩一员）
2. 客户端声明列表 = 客户端**偏好顺序**（2.0 最前），服务器所选版本为准；服务器返回旧版本时客户端接受并保持（`McpClientSession` 现状）。
3. 对外工具面不因协议版本改变：恒 1 个 `universal_tool`，`tools/list` 契约不变。
4. 版本号一致性：`plugin.json.Version` 与 `csproj <Version>/<AssemblyVersion>/<FileVersion>` 必须同值（2.2.0）。

## Boundary Conditions

- 客户端声明 `2025-11-25` 但 `capabilities`/`clientInfo` 缺失：现状不校验（宽容），保持——2.0 中 clientInfo 必填是客户端义务，服务端不强校验（现有测试 `Initialize_OldClientVersion_IsNegotiated` 即无 capabilities/clientInfo）。
- 通知（无 id）与批处理：不涉及版本差异，行为不变。
- 请求体 1MB 上限、GET /mcp SSE keep-alive（2.0 允许服务器随时断开，现状合规）：不变。
- 运行实例（主仓库 18889 网关）**不在本任务触碰范围**：改动仅存在于 worktree，未发布前运行实例保持 1.x 行为。

## Error Handling

- JSON-RPC 错误码现状不变：`-32700`（解析）/`-32600`（无效请求）/`-32601`（方法不存在）/`-32602`（参数无效）/`-32603`（内部错误）。
- **D1（待闸门1 确认）**：MCP 2.0 minor #5（SEP-1303）澄清"输入校验错误应返回 Tool Execution Error（isError）而非协议错误"。本任务**默认保持现状**（未知工具名/未知目标工具仍走 `-32602`/`isError` 现状：未知 `name`→`-32602`，未知目标 `tool`→`isError:true`），理由：①澄清性建议非 2.0 强制破坏变更；②改动会改变既有 1.x 客户端与测试断言（e2e :146-149 断言 `-32602`）的行为；③与"支持 2.0 协商"核心目标解耦。如用户选择改，作为单独决策项评估。

## Compatibility

- **1.x 客户端**（2025-06-18/2025-03-26/2024-11-05）：协商回显不变，零影响。
- **未知版本客户端**：回退 `2025-06-18` 不变。
- **2.0 客户端**：获得 `2025-11-25` 协商结果；本网关 capabilities 仅声明 `tools.listChanged=false`，未声明 OAuth/tasks 等能力 → 2.0 客户端按未声明能力处理，合规。
- **外部 2.0-only 服务器**：客户端声明含 `2025-11-25` 后可协商；服务器回退旧版时既有接受逻辑不变。
- 传输层（Streamable HTTP / SSE / stdio 帧）与鉴权（网关 token、管理面 ApiKeyPolicy）零改动。

## Non-functional Requirements

- 零新增依赖（延续零 MCP SDK 手写实现）。
- 性能：无额外开销（仅常量表 + 一个可选 JSON 字段）。
- 安全：鉴权/令牌逻辑零改动；不引入新的攻击面。
- 可维护性：协议版本常量集中在 `McpJsonRpcHandler` / `McpClientSession` 两处，注释同步。

## Acceptance Criteria

（闸门2 逐条以真实命令结果勾验）

- [ ] AC1：`dotnet build`（worktree）0 error。
- [ ] AC2：`dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~McpCenter"` 全绿（新增 + 回归）。
- [ ] AC3：`McpJsonRpcHandlerTests` 新增用例：声明 `2025-11-25` → 回显 `2025-11-25`；未知版本回退 `2025-06-18` 回归通过。
- [ ] AC4：`McpClientSessionTests` 断言客户端声明列表含 `2025-11-25`；旧版回退接受用例回归通过。
- [ ] AC5：`McpClientIntegrationTests`（真连 mock 三传输）全绿；mock 服务器支持 `2025-11-25` 协商。
- [ ] AC6：插件层 e2e `mcp-center.spec.ts` 全过（含新增 2.0 声明断言）。
- [ ] AC7：`plugin.json` 与 `McpCenter.csproj` 版本一致为 2.2.0 / 2.2.0.0。
- [ ] AC8：`docs/02-features/034-mcp-center.md` 协议版本表与验证记录已更新。

## Unknown

| 不确定点 | 影响 | 处理方式 |
| --- | --- | --- |
| worktree 内 e2e 动态端口/浏览器环境可用性 | 影响 AC6 能否实跑 | playwright.config.ts 已按 worktree 哈希派生端口（19000-19899）且全局浏览器目录共享（`$LOCALAPPDATA/ms-playwright`）；worktree 需先 `pnpm install`（sems pilot 同前置）；实跑时如实记录 |
| `dotnet test` 全量基线红（1500+/13 基线） | 影响过滤测试判定 | 按 AGENTS.md §5.6：只跑 McpCenter 过滤集 + 比对基线清单，新增红才归本任务 |
| D1 输入校验错误语义（SEP-1303）是否纳入 | 影响 Error Handling 面 | 默认不纳入（见 Error Handling D1），闸门1 呈报用户拍板 |
