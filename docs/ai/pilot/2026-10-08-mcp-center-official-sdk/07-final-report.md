# 07 Final Report — McpCenter 接入官方 MCP SDK（有状态会话）

## 1 Repository Understanding

OpenForgeSelf：.NET 10 + SQLite + NewLife.XCode 后端（`ForgeSelf.Api/`）、插件架构（`Plugins/` + `plugin.json`）、Vue 3.5 前端（`ForgeSelf.Web/`）；测试 xUnit + Playwright。MCP 网关为插件内自管理 Kestrel，默认端口 18890，原由自研 `McpGatewayServer` + `McpJsonRpcHandler` 实现传输与协议分发。

## 2 Selected Task

用户 2026-10-08 在三方案对比后拍板 **A**：全量替换为官方 `ModelContextProtocol.AspNetCore` **2.2.0**，并**显式开启有状态会话**（`Stateless = false`）。

## 3 Changed Files

见 `05-evidence.md` §1。核心：`McpUniversalTool.cs`（新建）、`McpGatewayServer.cs`（重写为 `MapMcp`）、`McpJsonRpcHandler.cs`（仅注释）、`McpCenterPlugin.cs`/`McpCenterRuntimeTests.cs`（装配同步）、`McpCenter.csproj`（+包）。

## 4 Validation

| 层 | 命令 | 结果 |
| --- | --- | --- |
| Build | `dotnet build Plugins/McpCenter/McpCenter.csproj` | **EXIT=1**；我侧 0 错，18 错全在他人 `DshMcpConfigWriter.cs` |
| Unit | `dotnet test --filter McpCenterTests` | **未跑**（被同一错误阻断） |
| Integration | 官方 MCP 客户端对真实插件网关 | **未跑**（同上） |
| Spike | 复刻网关接线的最小工程 + 官方客户端 | **PASS**：会话 id / 单工具 / DI 注入 / `-32022` 全部实测通过 |

## 5 Evidence

见 `05-evidence.md`。尖峰实证为 **Verified**；真实插件的端到端行为为 **Unknown**，未以推断冒充。

## 6 Review

见 `06-review.md`。**Final Decision = BLOCKED**：Requirement Check 中 AC1 FAIL、AC2~AC7 在真实插件上 Unknown；Scope / Architecture Check PASS。

## 7 Risk

L2 ×2（外部阻断未解除；SSE 帧变化未复测 DSH）、L3 ×1（分块请求不再受 1 MB 保护）。

## 8 Problems Found

1. **【阻断】并行会话文件未收口**：`DshMcpConfigWriter.cs`（未跟踪，mtime 00:43:40）引用 `DshMcpConfigDto` 已移除的 `EntryId/ServerName/Transport/Url/EntryExists` ⇒ 18 个 CS 错误。多次复核未变化。
2. **提交范围污染风险（已处置）**：共享文件 `McpCenter.csproj`（版本号 2.2.1→2.3.0）与 `McpCenterPlugin.cs`（`AddSingleton(new DshMcpConfigWriter(config))`）含他人改动，已用「暂存后还原」剔除，不纳入本次提交。
3. **官方 SDK 不改变版本覆盖**：其支持列表与我们原白名单逐字一致，`2026-07-28` 仍不支持——但改为显式报错 `-32022`，消除了静默降级。

## 9 Process Evaluation

- 有效：先做可行性尖峰再写 Plan，成功识别出「官方 SDK 默认也不下发会话 id」这一与直觉相反的事实，避免了错误预期。
- 待改进：本任务在**未解除外部阻断**的情况下推进到提交，导致 Evidence 中 Unknown 项偏多；更稳妥做法是先解除阻断再进 Implement。

## 10 最重要的问题

**代码接线已被尖峰证明正确，但真实插件无法编译验证**——阻塞完全来自并行会话的 `DshMcpConfigWriter.cs`。在它被修复前，任何「能用」的结论都是推断而非证据。

## 11 下一步建议

1. 解除阻断（对方对齐 DTO/writer，或授权我处置）。
2. 补跑 T006（新增 `McpGatewayServerSdkTests.cs`）、`dotnet test --filter McpCenterTests`、T007 官方客户端集成复测。
3. 宿主升级后复测 DSH 补丁层（SSE 帧解析）。
4. 链路稳定后下线 `McpJsonRpcHandler` 及其测试。
5. 回写 `06-review.md` 重审至 APPROVED，再走闸门2/3。
