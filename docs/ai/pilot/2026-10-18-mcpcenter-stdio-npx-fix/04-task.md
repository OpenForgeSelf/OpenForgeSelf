---
feature: “stdio 外部 MCP npx 三层修复”
risk: { level: L2, rationale: “插件传输层内部实现” }
expected_files:
  - Plugins/McpCenter/Services/McpClient/
  - ForgeSelf.Api.Tests/
rollback: “git revert 本批 commit”
---

# 04-task（stdio 外部 MCP npx 三层修复批，2026-10-18）

## Allowed Files
StdioMcpTransport.cs, McpClientProtocol.cs, StreamableHttpMcpTransport.cs, LegacySseMcpTransport.cs, McpExternalServerConfig.cs, McpExternalController.cs, ExternalServersStore.cs, mock-mcp-server.js, test files, e2e spec, plugin.json, csproj

## Forbidden
宿主核心服务 / 其他插件 / 数据库 / 前端 UI 布局

## 任务表

| # | 任务 | 文件 | 状态 |
|---|------|------|------|
| T1 | 红灯锚：真实 npx 集成测（修复前红=用户报错逐字同款） | `ForgeSelf.Api.Tests/.../McpStdioWindowsShimTests.cs`（新） | ✅ |
| T2 | S1 启动层：ResolveLaunch PATH 探测 + ComSpec 包装 + 可行动报错 + ResolvePathDirs(env.PATH) | `StdioMcpTransport.cs` | ✅ |
| T3 | S1 纯函数单测 6 组（注入式跨平台确定性，含包装器形状逐字断言） | `McpStdioWindowsShimTests.cs` | ✅ |
| T4 | S2 帧层：双帧读侧自适应 + auto 协商（NDJSON 首发/5s 探测/LSP 重发一次）+ ParseFraming | `StdioMcpTransport.cs` | ✅ |
| T5 | S2 配置链：Framing 字段 config→Dto→controller merge→store 透传 | `McpExternalServerConfig.cs` / `McpExternalController.cs` / `ExternalServersStore.cs` | ✅ |
| T6 | S2 靶子：mock stdio-ndjson 模式 + 帧协商测试 4 组 + 既有集成测锁 lsp | `Fixtures/mock-mcp-server.js` / `McpStdioFramingTests.cs`（新）/ `McpClientIntegrationTests.cs` | ✅ |
| T7 | S3 序列化层：SerializeRequest 单点 + 三传输同改 + arguments {} 封堵 | `McpClientProtocol.cs` / 三传输 | ✅ |
| T8 | S3 守卫：null 省略键直测 + 三传输源码扫描（G3 签名同源） | `McpClientProtocolGuardTests.cs` | ✅ |
| T9 | e2e 零容忍用例「stdio npx 真实世界」+ UI 截图段（含 scrollIntoViewIfNeeded 修正复跑） | `mcp-center.spec.ts` | ✅ |
| T10 | 定向 32/32 + 全量归因（60 红全在他批域，git status 证实零重叠，登记不修） | dotnet test | ✅ |
| T11 | 门禁③ dev-stack API 用户原文配置实测（connected=True toolCount=25 /test 3851ms）+ Browser UI 走查 | `.temp/g3-live-npx.ps1`（已删） | ✅ |
| T12 | 版本 bump 2.5.1（plugin.json + csproj 三连） | `Plugins/McpCenter/` | ✅ |
| T13 | PC-010 建档 + case + 三索引 + check-problem-classes/RegistryTests 绿 | `docs/11-troubleshooting/` | ✅ |
| T14 | 034 功能文档回写（R03 状态 / v2.5.1 块 / framing 字段 / 版本迭代节） | `docs/02-features/034-mcp-center.md` | ✅ |
| T15 | pilot 工件 00-07 | 本目录 | ✅ |
| T16 | 清测试数据（live-npx-playwright）+ 删探针 4 件 | `.temp/` | ✅ |
| T17 | 门禁④ 发布 release-local -Version 2.4.11 -Sign（原计 2.4.10，因并行会话同号包不含本批修复而升版，用户拍板）；发布后解包核 dll FileVersion=2.5.1.0 + SHA256 MATCH | `scripts/release-local.ps1` | ✅ |
| T18 | 技能回写（宽松断言掩盖三现强化）+ TODO/日记勾账 | `.agents/skills/` / `TODO.md` / 日记 | ✅ |
