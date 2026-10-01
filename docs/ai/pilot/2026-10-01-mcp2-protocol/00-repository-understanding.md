# Repository Understanding

> 阶段：Stage 0（动手写代码之前必须完成）｜规范：docs/04-standards/ai-native-engineering-workflow.md §2
> 原则：所有条目必须来自**真实仓库内容**，禁止凭常识推测。
> Task ID：PILOT-mcp2-protocol ｜ 日期：2026-10-01 ｜ 工作树：D:\src\my-proj\OpenForgeSelf\wt-mcp2（分支 mcp2-support，基于 e118b04）

## 项目结构

- 解决方案级目录：`ForgeSelf.Api/`（ASP.NET Core 后端，.NET 10）、`ForgeSelf.Web/`（Vue 3 SPA，Vite + pnpm）、`ForgeSelf.Core/` + `ForgeSelf.Abstractions/`（宿主核心/抽象）、`ForgeSelf.Api.Tests/`（后端测试，xUnit）、`Plugins/`（插件源码，本任务目标 `Plugins/McpCenter/`）。
- 文档：`docs/`（02-features 功能档案、04-standards 规范、ai/pilot 任务工件、18-templates 模板）。
- 构建/发布脚本：`build.ps1`、`scripts/`（release、plugin 发布、git hooks）。

## 技术栈

| 层 | 技术 | 依据（文件/配置） |
| --- | --- | --- |
| 后端 | .NET 10（net10.0） | `Plugins/McpCenter/McpCenter.csproj:4` `<TargetFramework>net10.0</TargetFramework>` |
| 后端插件 | 插件架构 `IPlugin` + `plugin.json` 注册 | `Plugins/McpCenter/plugin.json`、`McpCenterPlugin.cs:20` `class McpCenterPlugin : IPlugin` |
| MCP 网关 | 自托管 Kestrel（`WebApplication.CreateSlimBuilder`），零 MCP SDK 依赖 | `Services/McpGatewayServer.cs:53`；`McpCenter.csproj:29-31` 仅 NewLife.Core + 宿主引用 |
| 前端 | Vue 3 SPA（宿主侧；本任务不涉及插件 UI） | `ForgeSelf.Web/` |
| 后端测试 | xUnit + Moq + FluentAssertions | `ForgeSelf.Api.Tests/Plugins/McpCenterTests/`（12 个测试文件） |
| e2e | Playwright（`ForgeSelf.Web/e2e/`） | `ForgeSelf.Web/e2e/plugins/mcp-center/mcp-center.spec.ts` |

## 架构特点

- **McpCenter 插件双半身**（`docs/02-features/034-mcp-center.md`）：
  - ① **对外 MCP 服务端**：`McpGatewayServer`（自托管 Kestrel，独立端口，默认 18889）暴露 `POST /mcp`（JSON-RPC 2.0 Streamable HTTP）、`GET /mcp`（SSE keep-alive 心跳）、`GET /health`（探活）；请求经 `McpJsonRpcHandler` 分发（initialize/ping/tools/list/tools/call + 通知 + 批处理），对外恒 1 个工具 `universal_tool`，由 `UniversalToolForwarder` 按名转发宿主 `IToolRegistry` 全部工具。
  - ② **外部 MCP 客户端**（v2.1.0）：`McpClientManager` + `McpClientSession`（`Services/McpClient/`）按 stdio / Streamable HTTP / HTTP+SSE 三传输连接外部服务器，外部工具经 `mcp.<服务器id>.<工具名>` 命名空间转发。
- **协议版本协商现状（代码事实）**：
  - 服务端：`Services/McpJsonRpcHandler.cs:14-17` —— `ProtocolVersion = "2025-06-18"`；`SupportedProtocolVersions = { "2025-06-18", "2025-03-26", "2024-11-05" }`；协商逻辑（:165-191）：客户端声明在白名单内 → 回显；否则回退默认 `2025-06-18`。
  - 客户端：`Services/McpClient/McpClientSession.cs:14` —— `SupportedProtocolVersions = { "2025-06-18", "2025-03-26", "2024-11-05" }`；服务器返回所选版本、会话保持该版本语义（:9 注释）。
  - **实测（2026-10-01，主仓库运行实例 18889）**：initialize 声明 `2025-11-25` → 服务端回退返回 `2025-06-18`；`2025-03-26`/`2024-11-04` 等旧版正常回显。→ **不支持 MCP 2.0（2025-11-25）**。
- **MCP 2.0 官方事实（2025-11-25 版 changelog，modelcontextprotocol.io/specification/2025-11-25/changelog）**：与 2025-06-18 相比，2.0 的 Major 变更（OAuth/OpenID 发现、icons 元数据 SEP-973、elicitation、sampling 工具调用、OAuth Client ID 文档、experimental tasks）对**不声明相应 capabilities 的服务器均为可选**；Minor 变更与本网关相关者：② `Implementation` 接口新增可选 `description` 字段；⑤ 输入校验错误应返回 Tool Execution Error 而非协议错误（SEP-1303，澄清性）；⑥⑦ SSE 轮询语义（服务器可随时断开，本网关现状 keep-alive 合规）；⑩ JSON Schema 2020-12 为默认方言（向后兼容超集）。→ **服务器支持 2.0 的核心 = 版本协商接受 `2025-11-25` + 不缺失 2.0 契约面（本网关现状即合规，无破坏性强制项）**。

## 测试方式

- 后端单测/集成：`cd ForgeSelf.Api && dotnet build`；`dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~McpCenter"`（McpCenterTests 12 文件，文档记录 88/88 绿；含 `McpJsonRpcHandlerTests` 协议契约、`McpClientSessionTests` 客户端协商、`McpClientIntegrationTests` 真连 mock 三传输）。
- 插件层 e2e：`ForgeSelf.Web/e2e/plugins/mcp-center/mcp-center.spec.ts`（3 用例，`serial` 模式，真实后端零 mock；依赖 `Fixtures/mock-mcp-server.js` 作为外部服务器 mock）。
- 测试夹具：`ForgeSelf.Api.Tests/Plugins/McpCenterTests/Fixtures/mock-mcp-server.js` —— `PROTOCOL_VERSIONS = ['2025-06-18','2025-03-26','2024-11-05']`（:11），协商逻辑：声明在白名单内回显、否则回退第一个。
- e2e 基建（PILOT-050）：`playwright.config.ts` 按 worktree 派生动态端口（前后端 + MCP 端口 19000-19899），地址真源 `e2e/helpers/e2e-env.ts`；运行目录 `<仓库根>/.temp/e2e/wt-<hash8>`。

## 构建命令

```bash
cd ForgeSelf.Api && dotnet build                                   # 后端构建
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~McpCenter"   # 插件单测
cd ForgeSelf.Web && pnpm install && pnpm run check && pnpm run test # 前端门禁（e2e spec 改动涉及）
# 插件层 e2e（见 ForgeSelf.Web/e2e，playwright；需 globalSetup 构建宿主）
```

> 注（agent-workflow.md:593）：新检出 worktree 无 obj/project.assets.json，首构前须 `dotnet restore`/`pnpm install`。

## 主要目录职责

| 目录 | 职责 |
| --- | --- |
| `Plugins/McpCenter/` | MCP 中心插件：`McpCenterPlugin.cs`（Apply 生命周期）、`Services/`（网关/处理器/转发器/客户端）、`Controllers/`（管理 API）、`plugin.json`、`web/`（自带界面） |
| `ForgeSelf.Api.Tests/Plugins/McpCenterTests/` | 插件测试（含 mock 夹具） |
| `ForgeSelf.Web/e2e/plugins/mcp-center/` | 插件层 e2e |
| `docs/02-features/034-mcp-center.md` | 功能档案（协议契约/配置/验证记录真源） |
| `docs/ai/pilot/2026-10-01-mcp2-protocol/` | 本任务工件 |

## 代码组织方式

- 插件目录结构：`plugin.json`（Id/Version/EntryAssembly/EntryType/frontend）+ `<PascalCase>Plugin.cs` + `Controllers/`（`[Route("api/mcp...")]`，类级 `[Authorize("ApiKeyPolicy")]`）+ `Services/` + `web/`。
- 版本号双源：`plugin.json` `Version`（2.1.1）+ `McpCenter.csproj` `<Version>/<AssemblyVersion>/<FileVersion>`（2.1.0.0）；运行实例 health 显示程序集版本 2.1.0。
- 协议契约集中在 `McpJsonRpcHandler`（纯逻辑，可脱离 HTTP 单测）；传输在 `McpGatewayServer`。

## 现有工程规范

- `AGENTS.md` §11 + `docs/04-standards/ai-native-engineering-workflow.md`：开发任务唯一流程，三道闸门（闸门1 规格确认 → Implement → 闸门2 验收 → 闸门3 提交归档）；本任务级别=全量（插件任务、协议契约变更 → 00~06 七件）。
- `.agents/skills/plugin-development/SKILL.md`：插件任务总入口（已读）。相关铁律：铁律 17 管理面鉴权（本次**不动**鉴权，保持现状）；铁律 16 发布（本次**不发布**，等闸门2/3 用户指示）；§四 维护闭环五步（代码→门禁→e2e→发布→走查→运行实例只读复验，本任务先到门禁+e2e，发布等用户授权）。
- `AGENTS.md` §5.3：禁止一次性临时脚本代替正规测试；e2e 地址走 `e2e-env.ts`。
- 用户偏好（会话注入）：同一任务改动汇总一次性提交；重大决策先请示；未明确指示不 commit/push。

## 候选低风险任务

- **MCP 2.0（2025-11-25）协议协商支持**（本任务）：改动集中在 `McpJsonRpcHandler.cs` / `McpClientSession.cs` 两处常量白名单 + `initialize` 响应增强 + 版本号 + 测试/夹具/e2e 断言 + 文档；不涉数据库/鉴权/宿主核心/新依赖；协议面有官方规范 + 现有测试网格可验证。
- （不选）MCP 2.0 可选能力（OAuth / tasks / icons / elicitation）：均为可选能力，本网关不声明 capabilities 即合规，从零实现成本高且超用户诉求。
- （不选）TodoTracker `IServiceScopeFactory` 缺陷修复：独立缺陷（TODO 输入19），与本任务无关，不越界。

## 选择该任务的原因

- 用户输入3 明确指示（开 worktree 升级 MCP 支持 2.0）。
- MCP 2.0（2025-11-25）是官方**当前**协议版本（modelcontextprotocol.io/docs/2025-11-25/learn/versioning 标注 current）；实测当前网关仅支持至 2025-06-18，2.0 客户端会被降级。
- 范围小、可验证（单测 + e2e 网格齐备）、零风险面（不碰鉴权/DB/宿主核心）。
