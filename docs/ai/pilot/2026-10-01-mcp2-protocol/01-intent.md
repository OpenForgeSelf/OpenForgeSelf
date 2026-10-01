# Intent

> 阶段：Stage 1｜只描述「为什么做 / 做什么 / 做到什么程度」，**不提前决定具体代码实现**。
> Task ID：PILOT-mcp2-protocol ｜ 日期：2026-10-01

## Problem

- 对外 MCP 网关（`McpJsonRpcHandler`）协议协商白名单仅 `2025-06-18 / 2025-03-26 / 2024-11-05`，**最高 2025-06-18**；实测声明 MCP 2.0 版本 `2025-11-25` 会被降级回 2025-06-18。
- 外部 MCP 客户端（`McpClientSession`）声明列表同样不含 `2025-11-25`，面对只支持 2.0 的外部服务器无法协商。
- MCP 2.0（2025-11-25）已是官方**当前**协议版本，ForgeSelf 作为"对外暴露全部工具"的 MCP 服务器处于协议滞后状态。

## Why

- 用户输入3 明确要求（开 worktree 升级支持 2.0）。
- MCP 2.0 是官方现行版本，2.0 客户端（Claude/Cursor 等生态演进）声明 `2025-11-25` 时应获得 2.0 协商结果而非被降级。

## Expected Outcome

- 对外 MCP 网关**支持协商 `2025-11-25`（MCP 2.0）**：2.0 客户端 initialize 声明 `2025-11-25` → 服务端回显 `2025-11-25`。
- 外部 MCP 客户端同步支持声明 `2025-11-25`（对 2.0 外部服务器协商；服务器回退旧版本时保持既有接受逻辑）。
- 1.x 客户端（2025-06-18 / 2025-03-26 / 2024-11-05）与未知版本客户端行为**完全不变**（回显/回退语义保持）。
- 2.0 契约面合规：`initialize` 响应 `serverInfo` 带可选 `description`（2.0 minor #2）；其余 2.0 可选能力（OAuth/tasks/icons/elicitation）**不声明 capabilities 即合规，不实现**。
- 插件版本统一 bump 至 **2.2.0**（plugin.json + csproj），`docs/02-features/034-mcp-center.md` 协议表同步更新。
- 全部改动在 worktree `mcp2-support` 完成；**不 commit / 不 push / 不发布 / 不动运行实例**，等闸门2 验收 + 用户指示。

## Constraints

- 硬性约束（规范 §1 逐条）：不修改生产环境；不修改数据库结构；不修改鉴权/权限/安全核心逻辑（管理面 `[Authorize("ApiKeyPolicy")]` 与网关 token 逻辑**不动**）；不新增大规模依赖（延续零 MCP SDK）；不进行无关重构；不修改与本任务无关的文件。
- 范围边界：仅 `Plugins/McpCenter/` 相关源码 + 其测试/夹具/e2e 断言 + `docs/02-features/034-mcp-center.md` + 版本号；不实现 2.0 可选能力；不改 TodoTracker 等无关缺陷。
- 用户偏好：汇总一次性提交（本次不提交）；重大决策先请示（本任务无架构级变更，但 D1 语义决策在闸门1 一并呈报）。

## Success Criteria

（全部须可实际验证，闸门2 用真实命令结果判定）

1. `dotnet build`（worktree）0 error。
2. `dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~McpCenter"` 全绿（新增用例 + 既有回归）。
3. 单测实测：initialize 声明 `2025-11-25` → 回显 `2025-11-25`；声明 `2025-06-18/2025-03-26/2024-11-05` → 原回显不变；声明未知版本 → 回退 `2025-06-18` 不变。
4. 客户端 `McpClientSession.SupportedProtocolVersions` 含 `2025-11-25`，且旧版回退接受逻辑回归通过。
5. 插件层 e2e `mcp-center.spec.ts` 全过（含新增 2.0 声明断言；mock 服务器支持 2.0 协商）。
6. `plugin.json` Version=2.2.0 与 `McpCenter.csproj` `<Version>/<AssemblyVersion>/<FileVersion>`=2.2.0.0 一致。
7. `docs/02-features/034-mcp-center.md` 协议版本表/验证记录已同步 2.0 支持说明。
