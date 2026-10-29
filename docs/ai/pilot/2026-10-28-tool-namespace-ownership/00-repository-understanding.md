# 00 · Repository Understanding — 工具命名空间统一与归属重估

> 任务：工具名全量加插件前缀防冲突 + 工具归属重估（AIAgent 文件工具迁出）+ 文件工具去项目目录绑定
> 日期：2026-10-28 ｜ 级别：全量（插件任务 + 公共契约变更）｜ 风险：高（跨 ~16 插件的模型可见契约变更）

## 1. 现状事实（全部读码实证，file:line）

### 1.1 工具注册与键
- 工具以 `IToolFunctionExtension`（`ForgeSelf.Abstractions/IToolFunctionExtension.cs`）注册进宿主 `ToolRegistry`；
  **注册键 = `Name`**，`ConcurrentDictionary.TryAdd`，重名**先到先得、后到者注册失败仅告警**（`ForgeSelf.Api/Services/ToolRegistry.cs:46-68`）。
- 注册入口：宿主 `ExtensionPointManager.cs:174-176` 收集各插件 `Apply` 产出的扩展点实例调 `RegisterTool`。
- 工具名当前**无统一命名规则**：部分自带前缀（`sems_*`、`design_*`、`file_*`），大量裸名（`read_file`、`create_workflow`、`get_cpu_usage`、`create_todo`…）。
- **重名冲突 5 处（实测）**：`execute_workflow`/`get_workflow_status`/`list_workflows`（AIAgent `AIAgentPlugin.cs:475/619/724` vs WorkflowEngine `WorkflowEnginePlugin.cs:123/488/568`）、`format_json`/`compute_hash`（DevTools `DevToolsPlugin.cs:286/1022` vs TextTools `TextToolsPlugin.cs:127/375`）→ 谁先加载谁生效，另一份永不生效。

### 1.2 工具名消费点（改名影响面）
| # | 消费点 | 位置 | 类型 |
|---|--------|------|------|
| 1 | FreeLoop 挂载与作用域 | `AIAgentService.ResolveToolDefinitions`（:602-633，按名排除 submit_plan/complete_step/request_help）；`ToolScopePluginIds`（:579，按 PluginId） | 源码字面量 |
| 2 | 记忆注入 | `AIAgentService.cs:1263/1287`、`ReactLoopAgent.cs:721` 硬编码 `get_relevant_memories` | 源码字面量 |
| 3 | 内置 Agent 工具清单 | `BuiltInAgentDefinitions.cs:170`（程序员：`execute_script`,`list_workflows`,`plan_workflow`） | 源码字面量 |
| 4 | **存量数据** Agent 工具配置 | `AgentDefinition.ConfigJson`（Tools 名单，`AgentDefinition.Biz.cs:93-137`）；库表 seed 仅空表时执行（`AgentRegistryService.cs:38-44`）→ 用户库里的旧定义**不会**被源码更新覆盖 | 数据迁移 |
| 5 | 计划 allowedTools | `PlanGeneratorService.cs:213` → `AgentRun.PlanJson`；执行侧 `RunOrchestratorService.cs:740`（ToolAllowlist 过滤挂载） | 历史数据（别名兜底） |
| 6 | AgentHub 信任域 | `DelegationTask.TrustedScopes` 字符串名单（如 `write_file`，`AgentHubRegistryTests.cs:172`） | 历史数据（别名兜底） |
| 7 | 前端 composer 白名单 | `Plugins/AIAgent/web/src/composerContext.ts:47/51/63`（readonly/standard 档硬编码名 + 读类分支） | 源码字面量 |
| 8 | 前端危险工具标 | `toolEvents.ts:39-40` / `agentBridge.ts:291-292` 的 `DANGER_RE` 正则（按 write/exec/kill 等词根） | 前缀化后**天然兼容**（词根保留） |
| 9 | 万能工具/搜索/枚举 | AIAgent `UniversalTool.cs`（自引用检查 + unknown 提示）、`SearchToolsToolFunction.cs`；McpCenter `UniversalToolForwarder.cs:26/40-47`（`universal_tool` 为外部 MCP tools/list 唯一条目）、`ListToolsToolFunction.cs:25`、`McpUniversalTool.cs:34` 描述举例 | 描述文案 + 平台协议名 |
| 10 | e2e | `e2e/plugins/ai-agent/`（agent-execute / ai-agent.live / agent-loop-autonomous / tool-gateway-031）大量 `enabledToolNames`、提示词、轨迹断言 | 测试字面量 |
| 11 | 单测 | `UniversalToolTests` / `SearchToolsToolFunctionTests` / `ListToolsToolFunctionTests` / `FakeToolPlugin` 等 | fixture（随手更） |
| 12 | 文档 | `021-ai-agent.md`、`034-mcp-center.md`、`039-tool-bridge.md`、根目录 `fs工具集HTTP-MCP调用文档-1.9.2.md`、`design-system-consume` 技能、`DesignBriefBuilder.cs:401` 内嵌指引文案 | 文档同步 |
| 13 | ToolBridge | 自有词表（`ToolSpec.cs:12-13` read_file/write_file/list_dir/run_command），HTTP 桥独立词汇，**不经 ToolRegistry，不受影响** | 无需动 |

### 1.3 AIAgent 文件工具与项目目录绑定
- `ProjectFileToolFunctions.cs`：`list_files/read_file/write_file` 三工具，全部经 `IProjectWorkspaceService`：
  - 未选项目根直接报错（:57-60/150-153/232-235）；路径经 `ResolveSafePath`（`ProjectWorkspaceService.cs:150`）约束在项目根内（防穿越）；
  - `read_file`/`write_file` 支持 start_line/end_line 行区间读写（先读后写语义，:112/:190）。
- `run_terminal_command` 第 3 道门 CWD 限项目根内（`RunTerminalCommandTool.cs:104-117`，复用 ResolveSafePath）——用户未点名，本次不动（记 not-taken）。
- **FileTools 插件已是「任意绝对路径」模式**：控制器注释明示"按请求传入的任意绝对路径读写宿主文件系统"（`FileToolsController.cs:11`），`folder_stats` 参数即绝对路径（`FolderStatsToolFunction.cs:44`）→ 文件工具的归属与参数形态有现成先例。

### 1.4 plugin.json Id（前缀基准，kebab-case）
ai-agent / agent-hub / dev-tools / im-gateway / workflow-engine / cost-scope / home / file-tools / sample / scheduler / design-system / memory-system / mcp-center / script-runner / sems / quick-links / tool-bridge / text-tools / todo-tracker / system-monitor（各 `plugin.json:2`）。

### 1.5 注册表全量清单
见真源 `docs/02-features/021-ai-agent.md`「工具清单（2026-10-27 实测读码）」节：16 插件 130 个注册 + finish（循环控制）+ 外部 MCP（D10，`mcp.<id>.<tool>` 经 universal_tool 转发）；ToolBridge 无工具。

## 2. 适用问题类
查 `docs/11-troubleshooting/problem-classes/README.md`：本次触碰区域（ToolRegistry/插件注册面）无命中 PC。关联事实：021 文档已记录重名冲突（2026-10-27 盘点），本任务是它的定向修复而非复发。**无命中**。

## 3. 技术栈与验证方式（不变）
后端 .NET 10 + XCode（`dotnet build` + `dotnet test`）；插件前端 vite+pnpm（`pnpm run build`）；宿主前端 `pnpm run check`+`test`；e2e Playwright（插件冒烟档）；门禁分档按 AGENTS §5.6——本批属**中档**（碰宿主源码 ToolRegistry/ExtensionPointManager + 仓库级守卫须全量 `dotnet test`）+ 涉改插件各自的冒烟 e2e。
