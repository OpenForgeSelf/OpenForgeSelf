# Plan

> Stage 3｜Task ID: 2026-10-23-mcp-gateway-cors

## Files To Change

- `Plugins/McpCenter/Services/McpGatewayServer.cs`：独立 Kestrel pipeline 的预检、Origin 检查与 CORS 响应。
- `Plugins/McpCenter/Services/McpGatewayConfig.cs`：来源配置模型、读取和校验。
- `Plugins/McpCenter/Models/McpCenterConfigDto.cs`、`Plugins/McpCenter/Services/McpCenterRuntime.cs`：管理 API 的查询、部分更新、持久化和热重启。
- `Plugins/McpCenter/web/src/types/gateway.ts`、网关配置 Vue 组件：编辑来源并展示保存反馈。
- `ForgeSelf.Api.Tests/Plugins/McpCenterTests/`：配置、HTTP 预检、来源匹配、鉴权边界测试。
- `ForgeSelf.Web/e2e/plugins/mcp-center/mcp-center.spec.ts`：一条配置主链路冒烟用例。
- `Plugins/McpCenter/plugin.json`、`Plugins/McpCenter/McpCenter.csproj`：插件版本提升到 2.7.0。
- `docs/02-features/034-mcp-center.md`：功能与配置真源更新。

## Implementation Steps

1. 闸门1 批准后，按批准策略实现来源配置、校验、API 部分更新、落盘和回滚。
2. 在网关 pipeline 加入预检与允许 Origin 的 CORS 响应；确保预检早于 token 校验，实际 `/mcp` 仍受 token 校验。
3. 配置 UI 加来源编辑，复用现有保存/错误/热重启反馈。
4. 增加自动化覆盖，更新 034 文档。

## Test Plan

1. 配置默认空、合法/非法输入、旧配置兼容、API 更新和回滚。
2. HTTP OPTIONS 无 token、允许/拒绝来源、无 Origin 兼容、POST Bearer 正反边界。
3. 插件前端 check/test/build 与 MCP Center 单条冒烟 e2e。

## Verification

### Build

`dotnet build Plugins/McpCenter`

### Unit / Integration Test

`dotnet test ForgeSelf.Api.Tests --filter FullyQualifiedName~McpCenter`

### E2E

`cd ForgeSelf.Web; pnpm exec playwright test e2e/plugins/mcp-center/mcp-center.spec.ts`

### Other Checks

`cd Plugins/McpCenter/web; pnpm run check; pnpm run test; pnpm run build`

## Plan 偏差记录

| 时间 | 偏差点 | 原 Plan | 修正后 |
| --- | --- | --- | --- |
| 2026-10-23 | 前端/e2e 工具启动受本机 Node 文件系统 realpath 错误阻断 | 执行 pnpm check/test/build 与 MCP 插件 e2e | 命令已尝试；Node 对仓库工作目录 realpath 返回拒绝访问/ENOENT，未执行用例；记录为环境阻塞，不改产品配置规避 |
