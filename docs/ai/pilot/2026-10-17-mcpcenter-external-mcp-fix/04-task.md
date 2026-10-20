---
feature: 外部 MCP 客户端协议合规 + 编辑回显修复
risk: { level: L2, rationale: “涉及插件对外通信契约” }
expected_files:
  - Plugins/McpCenter/Services/McpClient/
  - Plugins/McpCenter/web/src/
rollback: “git revert 本批 commit”
---

# 04-task：外部 MCP 三缺陷修复批（2026-10-17）

## Allowed Files
Plugins/McpCenter/Services/McpClient/*, Controllers/McpExternalController.cs, ExternalServersStore.cs, McpClientManager.cs, web/src/*, plugin.json, csproj, test files, e2e spec

## Forbidden
宿主核心服务 / 其他插件 / 数据库 Schema / ForgeSelf.Api 主体

## 任务表

| # | 任务 | 文件 | 完成判据 | 状态 |
|---|------|------|----------|------|
| T1 | canonical 协议常量与 initialize 载荷构造 | `McpClientProtocol.cs`（新） | 编译过；被三处引用 | ☐ |
| T2 | Streamable HTTP：clientInfo + initialized 通知 + 会话头 | `StreamableHttpMcpTransport.cs` | 红灯测试绿；会话头单测绿 | ☐ |
| T3 | stdio：clientInfo + initialized 通知 | `StdioMcpTransport.cs` | 集成测试 stdio 往返绿 | ☐ |
| T4 | http-sse：clientInfo + initialized 通知 | `LegacySseMcpTransport.cs` | 集成测试 sse 往返绿 | ☐ |
| T5 | 掩码形态拒绝 | `ExternalServersStore.cs` | 新增 fact 绿 | ☐ |
| T6 | 前端类型 + 编辑全回显 + 掩码不提交 + 说明文案 | `types/external.ts`、`McpCenterView.vue` | `pnpm run build/check/test` 绿 | ☐ |
| T7 | 守卫测试（G1 转绿改写 + G2/G3 源码扫描 + 会话头） | `ForgeSelf.Api.Tests/.../McpCenterTests/` | `dotnet test --filter McpCenter` 全绿 | ☐ |
| T8 | 冒烟 e2e 外部 MCP 主链路 | `ForgeSelf.Web/e2e/plugins/mcp-center/mcp-center.spec.ts` | spec 绿（真实前后端+真实 DeepWiki） | ☐ |
| T9 | dev-stack 本地走查 + 截图 | — | 验收标准逐项核对 + 截图读图 | ☐ |
| T10 | PC-008 + case 归档 + 台账索引 | `docs/11-troubleshooting/` | `check-problem-classes.ps1` 绿 | ☐ |
| T11 | 文档回写 + 插件版本 | `034-mcp-center.md`、`plugin.json` | 契约与实现一致 | ☐ |
| T12 | 发布（本地目录更新源 + 签名） | `release-local.ps1 -UpdateDir -Sign` | 产物打包成功、签名有效 | ☐ |
| T13 | 05/06/07 工件 + 日志勾账 | 本目录 | 门禁齐全 | ☐ |
