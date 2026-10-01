# Agent Task

> 阶段：Stage 4｜把任务变成 **Agent 可以直接执行的工作单元**，零自我决策空间。
> 前序工件：00-repository-understanding / 01-intent / 02-spec / 03-plan 齐备且经闸门1 确认。

## Task ID

PILOT-mcp2-protocol

## Objective

MCP 中心插件（McpCenter）对外网关与外部客户端支持 MCP 2.0 协议协商（`2025-11-25`），1.x 兼容零回归，版本统一 2.2.0，单测 + e2e 全绿。

## Scope

### Allowed

严格按 `03-plan.md` 的 Files To Change：

- 源码：`Plugins/McpCenter/Services/McpJsonRpcHandler.cs`（协议白名单 + serverInfo.description + 注释）、`Plugins/McpCenter/Services/McpClient/McpClientSession.cs`（客户端白名单）。
- 版本号：`Plugins/McpCenter/plugin.json`、`Plugins/McpCenter/McpCenter.csproj` → 2.2.0。
- 测试：`ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpJsonRpcHandlerTests.cs`、`McpClientSessionTests.cs`。
- 夹具：`ForgeSelf.Api.Tests/Plugins/McpCenterTests/Fixtures/mock-mcp-server.js`。
- e2e：`ForgeSelf.Web/e2e/plugins/mcp-center/mcp-center.spec.ts`。
- 文档：`docs/02-features/034-mcp-center.md`；工件 `docs/ai/pilot/2026-10-01-mcp2-protocol/` 的 05/06/07。

### Forbidden

- ❌ 修改生产环境 / 运行实例（主仓库 18889 网关、:51888 宿主）——本任务只在 worktree 内改动与验证。
- ❌ 修改数据库结构、实体（无 Data/Model.xml 变更）。
- ❌ 修改鉴权/权限/支付/安全核心逻辑：网关 token（`McpGatewayServer.cs` `IsAuthorized`）、管理面 `[Authorize("ApiKeyPolicy")]` 一律不动。
- ❌ 新增大规模依赖（零 MCP SDK 延续）。
- ❌ 无关重构 / 修改 Plan 之外的文件（含 TodoTracker 缺陷、UI、宿主代码）。
- ❌ 实现 MCP 2.0 可选能力（OAuth / tasks / icons / elicitation / annotations）。
- ❌ 改变 D1（SEP-1303 输入校验错误语义）现状——除非闸门1 用户另行批准。
- ❌ 任何 git commit / push / tag / 发布动作（等闸门2 验收 + 用户明确指示）。

## Acceptance Criteria

- [ ] AC1：`dotnet build`（worktree）0 error。
- [ ] AC2：`dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~McpCenter"` 全绿。
- [ ] AC3：`McpJsonRpcHandlerTests` 新增用例：声明 `2025-11-25` → 回显 `2025-11-25`；`serverInfo.description` 非空；未知版本回退 `2025-06-18` 回归通过。
- [ ] AC4：`McpClientSessionTests` 断言客户端声明列表含 `2025-11-25`；旧版回退接受用例回归通过。
- [ ] AC5：`McpClientIntegrationTests`（真连 mock 三传输）全绿。
- [ ] AC6：插件层 e2e `mcp-center.spec.ts` 全过（含新增 2.0 声明断言）。
- [ ] AC7：`plugin.json` Version=2.2.0 与 `McpCenter.csproj` Version/AssemblyVersion/FileVersion=2.2.0.0 一致。
- [ ] AC8：`docs/02-features/034-mcp-center.md` 协议表/验证记录已更新 2.0 支持说明。

## Expected Files

- 改：`Plugins/McpCenter/Services/McpJsonRpcHandler.cs`
- 改：`Plugins/McpCenter/Services/McpClient/McpClientSession.cs`
- 改：`Plugins/McpCenter/plugin.json`
- 改：`Plugins/McpCenter/McpCenter.csproj`
- 改：`ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpJsonRpcHandlerTests.cs`
- 改：`ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpClientSessionTests.cs`
- 改：`ForgeSelf.Api.Tests/Plugins/McpCenterTests/Fixtures/mock-mcp-server.js`
- 改：`ForgeSelf.Web/e2e/plugins/mcp-center/mcp-center.spec.ts`
- 改：`docs/02-features/034-mcp-center.md`
- 增：`docs/ai/pilot/2026-10-01-mcp2-protocol/05-evidence.md`、`06-review.md`、`07-final-report.md`

## Verification Commands

```bash
cd D:\src\my-proj\OpenForgeSelf\wt-mcp2\ForgeSelf.Api
dotnet restore && dotnet build

cd D:\src\my-proj\OpenForgeSelf\wt-mcp2
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~McpCenter" --nologo

cd D:\src\my-proj\OpenForgeSelf\wt-mcp2\ForgeSelf.Web
pnpm install
bash node_modules/.bin/playwright test e2e/plugins/mcp-center --reporter=line
pnpm run check    # 存量 lint 红（global-setup.ts:183）为基线，本批不得新增
```
