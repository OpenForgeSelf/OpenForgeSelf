# Repository Understanding

> Stage 0｜依据 `docs/04-standards/ai-native-engineering-workflow.md` §2。Task ID: 2026-10-23-mcp-gateway-cors

## 项目结构与技术栈

- MCP Center 插件：`Plugins/McpCenter/`，运行时 id `mcp-center`；独立 UI 位于 `Plugins/McpCenter/web/`。
- 后端测试：`ForgeSelf.Api.Tests/Plugins/McpCenterTests/`；插件 e2e：`ForgeSelf.Web/e2e/plugins/mcp-center/mcp-center.spec.ts`。
- MCP 功能真源：`docs/02-features/034-mcp-center.md`。
- 后端使用 ASP.NET Core 自托管 Kestrel 与 `ModelContextProtocol.AspNetCore`；配置类型为 `McpGatewayConfig`；依据 `McpGatewayServer.cs`、`McpGatewayConfig.cs`。

## 当前网关事实

- `McpGatewayServer.StartAsync` 创建 SlimBuilder，监听 `config.ListenUrl`，映射 `/mcp` SDK endpoint 和 `/health`。
- 当前 `/mcp` 中间件先校验 Bearer token，再校验 1MB 请求体上限，最后调用 SDK handler；token 为空时 `IsAuthorized` 放行。
- 网关配置由插件数据目录 `config.json` 加载，管理 API/UI 提供查询、更新和热重启；配置加载测试位于 `McpGatewayConfigTests.cs`。
- 用户要求仅涉及 MCP 网关跨域，不涉及宿主 API 全局 CORS。

## 验证方式

- 后端：`dotnet build Plugins/McpCenter`；`dotnet test ForgeSelf.Api.Tests --filter FullyQualifiedName~McpCenter`。
- 前端：`cd Plugins/McpCenter/web && pnpm run check && pnpm run test && pnpm run build`（脚本实现前复核）。
- 插件冒烟：`cd ForgeSelf.Web && pnpm exec playwright test e2e/plugins/mcp-center/mcp-center.spec.ts`。

## 风险与任务选择

候选任务为仅在 MCP 独立网关加入按 Origin 配置的 CORS，覆盖网关配置 API/UI、自动化测试和 034 文档。因为网关 token 默认可为空，建议来源白名单默认空；只给精确匹配来源跨域响应，并保持实际请求鉴权。
