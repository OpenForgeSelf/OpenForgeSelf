---
task_id: 2026-10-23-mcp-gateway-cors
feature: 034-mcp-center
risk: { level: L2, triggers: ["Plugins/McpCenter/Services/McpGatewayServer.cs", "McpCenter configuration API/UI"], raised_by_agent: false }
gate1: { mode: user, at: 2026-10-23, ref: "用户确认：只对 /mcp 开启 CORS，并支持在 MCP 网关配置处配置" }
gate2: { mode: user, at: 2026-10-24, decision: "用户在知悉前端/e2e 及插件交付闭环仍待完成后，明确指令提交并推送当前阶段性结果；该授权不代表剩余门禁已完成" }
gate3: { mode: agent, at: null }
expected_files: ["Plugins/McpCenter/Services/McpGatewayServer.cs", "Plugins/McpCenter/Services/McpGatewayConfig.cs", "Plugins/McpCenter/Models/McpCenterConfigDto.cs", "Plugins/McpCenter/Services/McpCenterRuntime.cs", "Plugins/McpCenter/web/src/types/gateway.ts", "Plugins/McpCenter/web/src/McpCenterView.vue", "Plugins/McpCenter/plugin.json", "Plugins/McpCenter/McpCenter.csproj", "ForgeSelf.Api.Tests/Plugins/McpCenterTests/", "ForgeSelf.Web/e2e/plugins/mcp-center/mcp-center.spec.ts", "docs/02-features/034-mcp-center.md"]
rollback: "回退本任务提交；无数据库迁移。"
writeback: { feature_doc: pending, reason: "闸门1 批准后更新 MCP Center 功能真源" }
---

# Agent Task

## Objective

MCP 独立网关按 Origin 白名单支持浏览器跨域访问，同时保持实际请求现有 Bearer 鉴权语义。

## Scope

### Allowed

- 按批准的 Spec 修改 MCP Center 网关、配置 API/UI、测试与 034 功能文档。
- 新增预检、来源匹配和鉴权边界自动化覆盖。

### Forbidden

- 不改宿主 API 全局 CORS、生产实例、数据库结构、MCP 工具权限或 Bearer 策略。
- 不新增依赖，不实现未经批准的任意来源通配，不做无关重构。

## Acceptance Criteria

- [x] 允许 Origin 的预检与 MCP 请求满足批准规格（后端 HTTP 集成测试通过）。
- [x] 非白名单 Origin 不获得跨域读取授权（后端 HTTP 集成测试通过）。
- [x] OPTIONS 不执行工具；实际请求继续遵循 Bearer 规则（后端 HTTP 集成测试通过）。
- [x] 配置经 API/UI 持久化与热重启链路实现；API 持久化/验证有后端测试，UI/e2e 未能运行。
- [ ] 后端通过；插件前端与单条插件冒烟验证受 Node 工作目录 realpath 环境错误阻断。
- [x] 034 功能文档同步更新。

## Expected Files

见 `03-plan.md` Files To Change。

## Verification Commands

```powershell
dotnet build Plugins/McpCenter
dotnet test ForgeSelf.Api.Tests --filter FullyQualifiedName~McpCenter
cd Plugins/McpCenter/web; pnpm run check; pnpm run test; pnpm run build
cd ForgeSelf.Web; pnpm exec playwright test e2e/plugins/mcp-center/mcp-center.spec.ts
```
