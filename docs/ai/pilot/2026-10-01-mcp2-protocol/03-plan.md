# Plan

> 阶段：Stage 3｜**必须具体到真实文件路径**，禁止只写「修改 Service、增加测试」。
> Task ID：PILOT-mcp2-protocol ｜ 日期：2026-10-01 ｜ 工作树：`D:\src\my-proj\OpenForgeSelf\wt-mcp2`

## Files To Change

### 源码（2 处协议常量 + 1 处响应增强）

- file: `Plugins/McpCenter/Services/McpJsonRpcHandler.cs`
  reason: FR1/FR2/FR4 —— :14-17 白名单加 `"2025-11-25"`（默认/回退常量保持 `2025-06-18`）；:10 类注释补 2.0 支持；`HandleInitializeAsync` :183-190 result 中 `serverInfo` 增加 `description = "OpenForgeSelf MCP Center（万能工具网关，支持 MCP 2.0）"`（2.0 Implementation.description，可选字段，1.x 客户端忽略）。
- file: `Plugins/McpCenter/Services/McpClient/McpClientSession.cs`
  reason: FR3 —— :14 `SupportedProtocolVersions = { "2025-11-25", "2025-06-18", "2025-03-26", "2024-11-05" }`；:9 注释同步。

### 版本号（2 处，一致性）

- file: `Plugins/McpCenter/plugin.json`
  reason: FR6 —— `"Version": "2.1.1"` → `"2.2.0"`。
- file: `Plugins/McpCenter/McpCenter.csproj`
  reason: FR6 —— :9-11 `<Version>2.1.0</Version>` / `<AssemblyVersion>2.1.0.0</AssemblyVersion>` / `<FileVersion>2.1.0.0</FileVersion>` → `2.2.0` / `2.2.0.0` / `2.2.0.0`。

### 测试与夹具（3 处）

- file: `ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpJsonRpcHandlerTests.cs`
  reason: AC3 —— 新增 `Initialize_NewClientVersion2025_11_25_IsNegotiated`（声明 `2025-11-25` → 回显 `2025-11-25`）；`Initialize_ReturnsProtocolAndServerInfo` 补 `serverInfo.description` 非空断言；`Initialize_UnknownClientVersion_FallsBackToDefault` 不变（仍断言回退 `McpJsonRpcHandler.ProtocolVersion` = `2025-06-18`）。
- file: `ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpClientSessionTests.cs`
  reason: AC4 —— `Connect_ServerReturnsLegacyVersion_ClientAcceptsAndKeepsIt` :44-46 的 `InitializeAsync` 版本列表断言补 `v.Contains("2025-11-25")`。
- file: `ForgeSelf.Api.Tests/Plugins/McpCenterTests/Fixtures/mock-mcp-server.js`
  reason: FR7/AC5 —— :11 `PROTOCOL_VERSIONS` 数组加入 `'2025-11-25'`（协商逻辑已通用，无需其他改动）。

### e2e（1 处）

- file: `ForgeSelf.Web/e2e/plugins/mcp-center/mcp-center.spec.ts`
  reason: FR8/AC6 —— :107-115 之后新增第 2 个 initialize 调用：声明 `protocolVersion: '2025-11-25'` → 断言回显 `'2025-11-25'`（保留原 2025-06-18 断言）；evidence 记录。

### 文档（1 处）

- file: `docs/02-features/034-mcp-center.md`
  reason: FR9/AC8 —— :82 协议版本行、:84-86 契约表（initialize 版本协商）、:107-124 协议契约节补 `2025-11-25`（MCP 2.0）说明与 `serverInfo.description`；验证记录追加 v2.2.0 小节；插件版本号（:3 当前版本 2.1.0 → 2.2.0）。

### 工件（本任务）

- file: `docs/ai/pilot/2026-10-01-mcp2-protocol/00-repository-understanding.md` … `04-task.md`（本批）
- file: `docs/ai/pilot/2026-10-01-mcp2-protocol/05-evidence.md` / `06-review.md` / `07-final-report.md`（Implement + Test 后补齐）

## Implementation Steps

1. 改 `McpJsonRpcHandler.cs`：白名单 + 注释 + `serverInfo.description`。
2. 改 `McpClientSession.cs`：客户端白名单。
3. 改 `plugin.json` + `McpCenter.csproj`：版本 2.2.0。
4. 改测试：`McpJsonRpcHandlerTests` 新增/补断言；`McpClientSessionTests` 补列表断言。
5. 改夹具：`mock-mcp-server.js` 加 `2025-11-25`。
6. 改 e2e：`mcp-center.spec.ts` 新增 2.0 声明断言。
7. 更新文档：`docs/02-features/034-mcp-center.md`。
8. 跑验证（见 Verification），记录真实结果 → 补 05/06/07 工件。

## Test Plan

1. **后端构建**：`cd ForgeSelf.Api && dotnet build`（worktree 首构前先 `dotnet restore`）。
2. **插件单测**：`dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~McpCenter"`（覆盖 McpJsonRpcHandlerTests 新用例 + McpClientSessionTests + McpClientIntegrationTests 回归）。
3. **集成测试**（同 2 的过滤集内含 `McpClientIntegrationTests` 真连 mock 三传输；mock 已支持 2.0 协商）。
4. **插件层 e2e**：`cd ForgeSelf.Web && pnpm install && npx playwright test e2e/plugins/mcp-center`（按 `e2e-testing` 技能；globalSetup 自动构建宿主 + 起发布宿主；MCP 端口按 worktree 派生）。
5. **前端门禁**（e2e spec 改动涉及）：`cd ForgeSelf.Web && pnpm run check && pnpm run test`——注意存量 lint 红（`global-setup.ts:183` preserve-caught-error 致 check exit 1，TODO 输入51），如实记录基线。

## Verification

### Build

```bash
cd D:\src\my-proj\OpenForgeSelf\wt-mcp2\ForgeSelf.Api
dotnet restore
dotnet build
```

### Unit Test

```bash
cd D:\src\my-proj\OpenForgeSelf\wt-mcp2
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~McpCenter" --nologo
```

### Integration Test

```bash
# 同 Unit Test 过滤集（McpClientIntegrationTests 真连 mock 三传输）；无独立集成工程
```

### E2E

```bash
cd D:\src\my-proj\OpenForgeSelf\wt-mcp2\ForgeSelf.Web
pnpm install
bash node_modules/.bin/playwright test e2e/plugins/mcp-center --reporter=line
```

### Other Checks

```bash
cd D:\src\my-proj\OpenForgeSelf\wt-mcp2\ForgeSelf.Web
pnpm run check   # 存量 lint 红（global-setup.ts:183）为基线问题，如实记录；本批 e2e spec 改动不得新增错误
pnpm run test    # 宿主 vitest；本批未改宿主源码，预期既有结果
```

## Plan 偏差记录

> 实现中发现 Plan 与仓库实际不符时，先在此记录偏差，再修正 Plan，不得直接绕过。

| 时间 | 偏差点 | 原 Plan | 修正后 |
| --- | --- | --- | --- |
| （待 Implement 后填） |  |  |  |
