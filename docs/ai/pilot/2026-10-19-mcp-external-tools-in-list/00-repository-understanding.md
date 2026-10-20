# Repository Understanding

> 阶段：Stage 0 ｜ Task ID：PILOT-072 ｜ 日期：2026-10-19

## 复核基线

- 后端 .NET 10 + 插件架构（`Plugins/` + `plugin.json`）；前端 Vue 3.5（本任务不涉及 UI）；测试 xUnit（`ForgeSelf.Api.Tests`）+ Playwright e2e（`ForgeSelf.Web/e2e/plugins/`）。
- 门禁命令：`dotnet build ForgeSelf.Api` / `dotnet test --filter FullyQualifiedName~McpCenter`（含 AIAgent 时另跑 `~AIAgent`）/ 插件前端本批无改动。

## 关键事实（全部核码，非记忆）

1. **`list_tools` 是注册进宿主 IToolRegistry 的工具函数**（`Plugins/McpCenter/Services/ListToolsToolFunction.cs:17-31`，经 `McpCenterPlugin.ToolExtensions` 属性由宿主自动注册，`:47`）——网关 `universal_tool` 转发与内置 AIAgent 的 `universal_tool` **共用同一份**。因此「进 list_tools」改这一个文件即内外同时生效。
2. 其数据源 = `IToolRegistry.GetAllTools()` 实时快照（`:78`），外部 MCP 工具从不进该表（决策 D10）——现状 `list_tools` 只见宿主工具。
3. **网关转发器有 `mcp.` 前缀路由**（`UniversalToolForwarder.cs:92`）：`mcp.<服务器id>.<工具名>` 按首段点拆分 → `McpClientManager.CallExternalAsync`（`McpClientManager.cs:104`）；失败封套 `{success:false,error}`（`:214-218`）。
4. **内置 AIAgent 的 `universal_tool`（`Plugins/AIAgent/Services/ToolFunctions/UniversalTool.cs`）零 `mcp.` 匹配**（2026-10-19 grep 坐实）——只走宿主注册表，直传 `mcp.*` 落「未知工具」。这是用户所说「工具内用不了」。
5. 外部工具清单同步可得：`McpClientManager.GetStates()`（`:125`，含连接态）+ `GetTools(id)`（`:153`，返回 `McpExternalToolDto`：`FullName`/`ServerId`/原生名/描述/输入 schema，`Models/McpExternalServerConfig.cs:90-98`）。
6. **跨插件接缝既有裁决**（architecture-design 技能 + `docs/06-research/001` §5.6 裁决 A/F + FAQ Q1/Q2/Q6）：契约入 `ForgeSelf.Abstractions`；提供方 `Apply` eager 构造 + `ctx.Register<T>(实例)` 提供到 root 共享表（随 Fiber 摘除）；消费方每次 `ctx.Get<T>()` 禁缓存字段；能力缺失走 null 降级；IWorkflowAIAdvisor 为判例。
7. 真源文档 `docs/02-features/034-mcp-center.md` §转发契约已载「list_tools 不枚举外部工具」「三条可用通道」（2026-10-18/19 回写），本批落地后需同步改写；决策 D10 需修订。

## 适用问题类

查 `docs/11-troubleshooting/problem-classes/README.md` tags（MCP/枚举/转发）：本批为新功能增强非缺陷修复。相邻 PC-008（外部 MCP 协议合规）/PC-010（stdio 接入）均落在**连接与传输层**，本批不改传输；lens 吸收一条：**跨层同类缺陷扫查**（三传输/双消费侧）→ 本批消费侧有两个（网关 + AIAgent），改一处必查另一处，列入 Spec。

## 候选低风险任务及选择理由

本任务即入选：用户拍板需求（输入3），改动集中在①Abstractions 新契约②McpCenter 两文件③AIAgent 一路由分支，无内核语义变更、无 DB、无 UI。
