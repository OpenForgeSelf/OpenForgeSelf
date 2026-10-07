# Repository Understanding

> 阶段：Stage 0（动手写代码之前必须完成）｜规范：docs/04-standards/ai-native-engineering-workflow.md §2
> 原则：所有条目必须来自**真实仓库内容**，禁止凭常识推测。
> Task ID：2026-10-04-mcp-tool-playground｜工作树：`D:/src/my-proj/OpenForgeSelf/wt-mcp-playground`（分支 `feat/mcp-tool-playground`，HEAD `d88d709`）

## 项目结构

| 路径 | 说明 |
| --- | --- |
| `ForgeSelf.slnx` | 解决方案 |
| `ForgeSelf.Web/` | Vue 3 SPA 宿主前端（含 `e2e/` Playwright 测试） |
| `ForgeSelf.Api/` | ASP.NET Core 宿主后端（**注意**：`ForgeSelf.Api/Plugins/` 是宿主插件**装载器运行时代码**，不是插件源码） |
| `ForgeSelf.Api.Tests/` | 后端测试（xUnit + Moq + FluentAssertions），插件测试在 `Plugins/McpCenterTests/` |
| `Plugins/McpCenter/` | **本次任务对象**：MCP 中心插件源码（后端 C# + `web/` 自带界面） |
| `docs/02-features/034-mcp-center.md`、`022-mcp-tools.md` | MCP 相关功能文档 |
| `docs/ai/pilot/` | AI-Native 闭环工件目录 |

## 技术栈

| 层 | 技术 | 依据（文件/配置） |
| --- | --- | --- |
| 宿主前端 | Vue 3.5 + Vite 6 + TS 5.7 + Element Plus 2.14 + Tailwind 4 + pnpm | `ForgeSelf.Web/`；AGENTS.md §2.1 |
| 宿主后端 | .NET 10 + SQLite + NewLife.XCode（唯一 ORM） | `ForgeSelf.Api/`；AGENTS.md §2.3 |
| 插件前端 | Vite 独立预编译 ESM，`vue`/`vue-router`/`pinia`/`element-plus` 全部 external | `Plugins/McpCenter/web/vite.config.ts`；`plugin-development` 铁律 4 |
| 测试 | xUnit（后端）、vitest（宿主前端 + 部分插件前端）、Playwright e2e | `ForgeSelf.Api.Tests/`、`ForgeSelf.Web/e2e/` |

## 架构特点

- 插件通过 `Plugins/<PascalCase>/plugin.json` 注册；`mcp-center` 的 `frontend.entry = web/dist/index.js`，宿主远程加载。
- **McpCenter 已有外部 MCP 客户端能力（v2.1.0 起）**，本次是在此之上加「测试台」，不是从零建：
  - `Services/McpClientManager.cs`：持有 `ConcurrentDictionary<string, McpClientSession>`，`TryConnectAsync` / `DisconnectAsync` / `GetTools` / `CallExternalAsync` / `TestAsync` / `StopAllAsync`。
  - `Services/McpClient/McpClientSession.cs`：握手 + `tools/list` 快照 + `CallToolAsync`；`SupportedProtocolVersions = { 2025-11-25, 2025-06-18, 2025-03-26, 2024-11-05 }`。
  - 三种传输：`StdioMcpTransport`、`StreamableHttpMcpTransport`、`LegacySseMcpTransport`（`McpClientManager.CreateTransport` 按 `Transport` 字符串分派，默认走 streamable-http）。
  - 管理控制器 `Controllers/McpExternalController.cs`（路由 `api/mcp-center/servers`），**类级 `[Authorize("ApiKeyPolicy")]`**。
- 现有外部工具端点：`GET api/mcp-center/servers`、`POST`、`PUT {id}`、`DELETE {id}`、`POST {id}/connect`、`POST {id}/disconnect`、`GET {id}/tools`、`POST {id}/test`。**没有「按参数调用某个外部工具」的端点**——这是本次要补的缺口。
- `Models/McpExternalServerConfig.cs` 中 `McpExternalToolDto` 只有 `{ FullName, Name, Description, InputSchemaJson }`，`InputSchemaJson` 是 **schema 原文字符串**（前端 `types/external.ts` 里声明的 `inputSchema?: Record<string, unknown>` 与后端字段名不一致，且从未被使用）。
- 前端 `web/src/McpCenterView.vue`（1698 行）的「外部服务器工具」弹窗（约 904–919 行）**只渲染 `fullName` + `description` 两行文本**，无 schema 可视化、无参数输入、无调用入口。
- `Plugins/McpCenter/web/package.json` 只有 `build` / `dev` 两个脚本，**没有 `check` / `test`**（对照 `Plugins/DesignSystem/web/package.json` 已具备，可照抄其 `pnpm -C ../../../ForgeSelf.Web exec vue-tsc/vitest` 写法）。
- `McpClientSession.CallToolAsync` 现有契约（由 `McpClientSessionTests` 钉住）：text content 拼接返回；`isError:true` 时返回 `{"success":false,"error":<文本>}`；非 text 的 content 项原样 JSON 透传；无 content 时返回 raw；传输异常抛 `McpClientException`。

## 测试方式

| 层 | 入口 |
| --- | --- |
| 插件后端单测 | `dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~McpCenter"`（现有 14 个测试文件，`McpClientSessionTests` 用 `Mock<IMcpClientTransport>`） |
| 宿主前端 check/test | `cd ForgeSelf.Web && pnpm run check && pnpm run test` |
| 插件前端构建 | `cd Plugins/McpCenter/web && pnpm run build`（沙箱内按 `plugin-development` §3.2 走宿主树内出树构建兜底） |
| 插件层 e2e | `ForgeSelf.Web/e2e/plugins/mcp-center/mcp-center.spec.ts`（现 406 行，零 mock） |

## 构建命令

```bash
cd ForgeSelf.Api && dotnet build
cd Plugins/McpCenter/web && pnpm run build
cd ForgeSelf.Web && pnpm run check && pnpm run test
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~McpCenter"
```

## 主要目录职责

| 目录 | 职责 |
| --- | --- |
| `Plugins/McpCenter/Controllers/` | 管理面 HTTP 端点（须类级 `[Authorize("ApiKeyPolicy")]`） |
| `Plugins/McpCenter/Services/McpClient/` | MCP 协议传输层与会话 |
| `Plugins/McpCenter/Models/` | DTO / 配置模型（纯数据，无实体） |
| `Plugins/McpCenter/web/src/` | 插件自带界面（api / types / 视图） |

## 代码组织方式

插件后端按 Controller / Service / Model 三层，无 XCode 实体（配置落 `external-servers.json`），故本次**不涉及 `Data/Model.xml`、不涉及建表、不涉及数据库结构变更**。

## 现有工程规范

- `docs/04-standards/ai-native-engineering-workflow.md`（v1.1.0，**强制**，九阶段 + 三道闸门；闸门1 未批不得 Implement）。
- `.agents/skills/plugin-development/SKILL.md`：铁律 17（管理面鉴权）、铁律 13（版本徽标）、铁律 7（改完插件五步闭环）、§3.4（交互设计统一要求：操作成败可见 / 空态分级 / 二次确认）。
- `AGENTS.md` §0 预飞铁律、§2.3（`pwsh` 统一、端口覆盖）、§5.0（禁止一次性 `temp/*.cjs` 当验证手段）。

## 候选低风险任务

1. **【选定】MCP 中心 · 外部工具测试台**：新增 1 个管理端点 + 前端动态参数表单与调用结果面板。
   - 低风险依据：不碰实体/建表/鉴权核心；新增端点落在**已有**带 `[Authorize]` 的控制器内，天然继承鉴权；后端调用链路（`McpClientSession.CallToolAsync`）已存在且被单测覆盖；前端为新增组件 + 纯函数，可单测。
2. 改造 `InputSchemaJson` 为结构化 `InputSchema` 字段（**否决**：跨前后端契约破坏性变更，收益 < 风险，改为前端解析字符串）。
3. 给 `StreamableHttpMcpTransport` 补 `Mcp-Session-Id` 支持（**否决**：超出本次范围，DeepWiki 实测无状态不需要；另立 TODO）。

## 选择该任务的原因

用户直接指令（输入1）：完善 MCP 插件、增加测试功能，按 MCP 返回的工具列表与参数说明动态可视化界面，并可输入参数发起调用；指定用 DeepWiki MCP 做联调靶子。上述候选 1 是唯一同时满足「用户意图 + 低风险 + 易测试」的切口。

## 靶子可行性实测（2026-10-04，Verified）

| 项 | 实测结果 |
| --- | --- |
| 端点 | `https://mcp.deepwiki.com/mcp`，**streamable-http，无需鉴权** |
| `initialize` | 200，SSE（`event: message`）；请求 `2025-11-25` 时原样回 `2025-11-25`（与插件 `SupportedProtocolVersions[0]` 一致），`serverInfo = DeepWiki 2.14.3` |
| 会话 | 响应头**无 `Mcp-Session-Id`** ⇒ 无状态，插件现有「每次请求独立」的写法可直连 |
| `tools/list` | 公开 3 个工具：`ask_wiki_question` / `read_wiki_contents` / `read_wiki_structure` |
| `tools/call` | `read_wiki_structure{repoName:"facebook/react"}` 实测 200 并返回文本目录 ⇒ **端到端连通** |
| 覆盖到的 schema 形态 | `string`（必填）、`anyOf[string, array<string>]`（联合类型）、`required`、`description` ⇒ 正好覆盖动态表单需要处理的分支 |
