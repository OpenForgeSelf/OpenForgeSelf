# Evidence

> Stage 7｜只记录本次真实执行结果。Task ID: 2026-10-23-mcp-gateway-cors

## Task

仅对 `/mcp` 开启 CORS，并支持在 MCP 网关配置处管理精确 Origin 白名单；默认空白名单，实际 MCP 请求保留既有 Bearer 校验。

## Changed Files

- `Plugins/McpCenter/Services/McpGatewayServer.cs`
- `Plugins/McpCenter/Services/McpGatewayConfig.cs`
- `Plugins/McpCenter/Models/McpCenterConfigDto.cs`
- `Plugins/McpCenter/Services/McpCenterRuntime.cs`
- `Plugins/McpCenter/web/src/types/gateway.ts`
- `Plugins/McpCenter/web/src/McpCenterView.vue`
- `Plugins/McpCenter/plugin.json`、`Plugins/McpCenter/McpCenter.csproj`（版本 2.7.0）
- `ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpGatewayConfigTests.cs`
- `ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpCenterRuntimeTests.cs`
- `ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpGatewayCorsTests.cs`
- `ForgeSelf.Web/e2e/plugins/mcp-center/mcp-center.spec.ts`
- `docs/02-features/034-mcp-center.md`

## Build

Command: `dotnet build Plugins/McpCenter --no-restore -m:1 -p:BuildInParallel=false`（TEMP/TMP 指向仓库 `.temp/tmp`，禁用 MSBuild 节点复用）

Result: PASS（Verified）

```text
McpCenter -> ...\Plugins\McpCenter\bin\Debug\net10.0\McpCenter.dll
已成功生成。
    0 个警告
    0 个错误
```

## Unit Test

Command: `dotnet test ForgeSelf.Api.Tests --no-restore --filter FullyQualifiedName~McpCenter -m:1 -p:BuildInParallel=false`

Result: PASS（Verified）

```text
已通过! - 失败: 0，通过: 191，已跳过: 0，总计: 191，持续时间: 1 m 5 s
```

日志：`.temp/mcp-cors-tests-single.log`。

## Integration Test

Result: PASS（Verified）

依据：测试集合含真实本机 Kestrel 的 CORS HTTP 测试，验证允许来源的无 token OPTIONS 返回 204 与白名单 ACAO、未允许来源 OPTIONS 返回 403 且无 ACAO、实际请求保留 401 鉴权、`/health` 不返回 CORS。

## E2E

Result: FAIL / 未进入用例执行（Verified）

已尝试插件快档：`node node_modules/@playwright/test/cli.js test --config=playwright.config.ts e2e/plugins/mcp-center --output=../.pw-out-mcp-center --reporter=list`。

```text
[WebServer] canonicalizing the `--dir` argument: ...\ForgeSelf.Web
拒绝访问。 (os error 5)
Error: Process from config.webServer was not able to start. Exit code: 1
```

Node `fs.realpathSync.native(process.cwd())` 在相同工作目录返回 `ENOENT`；没有启动 e2e 宿主/页面，也没有截图。日志：`.temp/mcp-cors-e2e.log`。

## Static Analysis

Result: BLOCKED（Verified）

插件 `pnpm run check/test/build` 无法正常启动：本机 pnpm/Node 对仓库路径 canonicalize/realpath 失败（拒绝访问/ENOENT）；直接调用宿主 `vue-tsc` 时 Vue 包类型解析报缺少 `ref/computed/onMounted/createApp/h`，Vitest 单线程运行则在 pnpm store 的 `spy.js` realpath 失败。该结果不能作为代码静态检查通过证据。

## Screenshots

N/A。插件 e2e 未能启动；源码 dev-stack 视觉走查尚未完成。

## Acceptance Criteria Evidence

- **AC1 — PASS（Verified）**：`McpGatewayCorsTests.McpEndpoint_AllowsConfiguredOriginPreflightAndKeepsBearerGuard` 断言允许来源 OPTIONS 返回 204、精确 ACAO、允许方法和请求头；过滤集 191/191 通过。
- **AC2 — PASS（Verified）**：同一 Kestrel 测试断言白名单 Origin 的实际 POST 响应带 ACAO；该 POST 无 token 返回 401，证明 CORS 响应与原鉴权并存。
- **AC3 — PASS（Verified）**：同一测试断言未允许来源预检 403 且无 ACAO、未允许来源实际请求无 ACAO；另断言 `/health` 与 `/mcp/extra` 不带 ACAO。
- **AC4 — PARTIAL（Verified/Unknown）**：无 token 的白名单实际 POST 返回 401（Verified）；正确 token 后继续真实 MCP initialize 协议的浏览器 e2e 未运行（Unknown，Node realpath 阻塞）。
- **AC5 — PARTIAL（Verified/Unknown）**：配置默认空、旧配置兼容、Origin 规范化、运行时持久化回读和无效更新不变更均由后端测试覆盖并通过（Verified）；UI 热重启链路未运行（Unknown）。
- **AC6 — PARTIAL（Verified/Unknown）**：插件后端 build 和 McpCenter 过滤集通过（Verified）；插件前端 check/test/build 与插件 e2e 未运行（Unknown，见上方环境阻塞）。

## Known Limitations

- 前端类型检查、单测、构建、插件冒烟 e2e 和源码页面走查仍待在 Node 能正常访问仓库路径的环境中执行。
- 本地目录发布及运行实例只读复验未执行；未改动/重启任何用户运行实例。
- 工作区有与本任务无关的终端输出编码变更；没有将其纳入本任务或发布包。

## Unresolved Issues

- 环境阻塞：Node 对 `D:\src\my-proj\OpenForgeSelf\OpenForgeSelf\ForgeSelf.Web` 的 realpath/canonicalize 失败。已尝试 pnpm 命令、直接宿主 `vue-tsc`、Vitest 单线程和 Playwright 项目快档；最终状态 BLOCKED，需修复/切换本地 Node 文件系统访问环境后重跑前端与 e2e 门禁。
- 插件开发闭环中的 dev 走查、发布和发布实例复验待完成，因此本任务暂不满足“完成”门槛。
