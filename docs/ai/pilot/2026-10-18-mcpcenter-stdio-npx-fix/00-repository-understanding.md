# 00-repository-understanding（stdio 外部 MCP npx 三层修复批，2026-10-18）

## 涉及模块与文件（已读码确认）

- `Plugins/McpCenter/Services/McpClient/StdioMcpTransport.cs` —— stdio 传输：ProcessStartInfo（UseShellExecute=false 直启）+ LSP 单帧读写（ReadFrameAsync/WriteFrameAsync）+ RequestAsync 手写 `@params=prms` 载荷；构造函数有 AllowedCommands 白名单校验（npx/node/python/python3/uvx/uv/dotnet）。
- `Plugins/McpCenter/Services/McpClient/StreamableHttpMcpTransport.cs` / `LegacySseMcpTransport.cs` —— 各自手写请求载荷 `@params=prms`（与 Stdio 同族三处）。
- `Plugins/McpCenter/Services/McpClient/McpClientProtocol.cs` —— PC-008 建的 canonical 单点（BuildInitializeParams），本批扩展 SerializeRequest。
- `Plugins/McpCenter/Models/McpExternalServerConfig.cs` + `Services/ExternalServersStore.cs` + `Controllers/McpExternalController.cs` —— 配置链：store 的 ValidateAndNormalize **逐字段拷贝**（新增属性必须手动透传）；controller PUT 逐字段 merge。
- `ForgeSelf.Api.Tests/Plugins/McpCenterTests/` —— 既有 McpClientIntegrationTests（node mock LSP 帧）、McpClientProtocolGuardTests（PC-008 守卫）、Fixtures/mock-mcp-server.js（stdio=LSP 帧，与客户端**同构**）。
- `ForgeSelf.Web/e2e/plugins/mcp-center/mcp-center.spec.ts` —— 外部 MCP 三传输用例（stdio 用自家 mock）；旧应用层套曾有「stdio npx 优雅降级」分支（宽松断言掩盖源）。

## 既有约束

- 缺陷类任务按 problem-class-closure 闭环（红灯→同类扫查→守卫→归档）；插件任务按 plugin-development 六步门禁；
- 禁止 agent 停/启/杀用户运行中宿主（:51888）；发布默认本地目录更新源 + -Sign；
- 宿主版本线由并行会话推进（已发 2.4.9），本批插件版本独立 bump：McpCenter 2.5.0 → 2.5.1；
- 终端安全会误拦含 "cmd" 字样的 Add-Content → 日记追加走 .temp 中转文件。
