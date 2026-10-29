# 03 · Plan — 工具命名空间统一与归属重估（v2，按用户拍板修订）

> 变化 vs v1：删别名层与存量数据迁移（不做兼容）；迁移目标 FileTools → **McpCenter**；IProjectWorkspaceService 契约上移 Abstractions。
> 四批（B1→B4），每批独立可验证、git 可回滚。

## B1 命名空间基建（宿主 + 契约 + 守卫）

| # | 文件 | 改动 | 理由 |
|---|------|------|------|
| B1-1 | `ForgeSelf.Abstractions/ToolNamespace.cs`（新建） | ① 保留名清单（11 个，02-spec FR-1.2）；② 迁移映射表常量（唯一真源，FR-2）；③ `ResolveName`/`TryGetPrefix` 纯函数 | 契约入 Abstractions（铁律 2） |
| B1-2 | `ForgeSelf.Abstractions/IProjectWorkspaceService.cs`（新建，接口上移） | 从 `Plugins/AIAgent/Services/ProjectWorkspaceService.cs:19` 抽接口（含 ResolveSafePath/ListEntries/ReadFile/WriteFile/IsProjectSet/ProjectRoot/DefaultRoot 等成员，按实际接口成员） | FR-3.3；AIAgent 实现改继承该接口 |
| B1-3 | `ForgeSelf.Api/Services/ToolRegistry.cs` | `GetTool`/`ExecuteAsync` 入口经 `ToolNamespace` 校验正式名（重名 TryAdd 行为保留 + Error 日志已有） | FR-1.3 执行面 |
| B1-4 | `ForgeSelf.Api/Plugins/ExtensionPointManager.cs`（:174-176） | 注册校验：插件 toolPrefix + 保留名，不合规拒绝注册 + Error | FR-1.3 |
| B1-5 | `ForgeSelf.Api.Tests/Architecture/ToolNamespaceGuardTests.cs`（新建） | FR-4.1 四断言（映射表引用 B1-1 常量）；mutation 辅助用例 | FR-4 防复发 |

**暂态策略**：B1-4 校验随 B2 同 PR 生效（校验开关 `ToolNamespace.EnforcementEnabled`，B2 完成置 true），避免中间态全线工具消失。

## B2 全插件改名（机械批，按 FR-2 映射表）

| 插件 | 文件 | 动作 |
|------|------|------|
| workflow-engine | `WorkflowEnginePlugin.cs` | 5 个 Name+描述 |
| memory-system | `MemorySystemPlugin.cs`（:85/114/156/193/229） | 5 个改名 |
| dev-tools | `DevToolsPlugin.cs` | 26 个 Name |
| text-tools | `TextToolsPlugin.cs` | 5 个 Name |
| system-monitor | `SystemMonitorPlugin.cs` | 5 个 Name |
| script-runner | `ScriptRunnerPlugin.cs` + `Services/ScriptRunnerToolFunctions.cs` + `Services/CodeSnippetToolFunctions.cs` | 11 个 Name |
| quick-links | `QuickLinksPlugin.cs` | 4 个 Name |
| scheduler | `SchedulerPlugin.cs` | 3 个 Name |
| todo-tracker | `TodoTrackerPlugin.cs`（:65-140） | 8 个名 |
| agent-hub | `Tools/AgentHubTools.cs`（5 Name）+ Trust 校验点 | 改名 |
| sample | `SamplePlugin.cs`（:137） | 改名 |
| file-tools | `FolderStatsToolFunction.cs` | `folder_stats`→`file_dir_stats` |
| ai-agent | `AIAgentPlugin.cs`（plan_workflow Name :343）、`Data/Entities/BuiltInAgentDefinitions.cs`（:170 workflow_* 新名） | 本批只改名；迁出/删除在 B3 |
| 15 个 plugin.json | 加 `"toolPrefix"`（FR-1.4 表） | 声明 |

**B2 验证**：`dotnet build` + ToolNamespaceGuardTests 绿（映射表与实际一致才绿）。

## B3 归属迁移（公共工具 → McpCenter；删 AIAgent 重复）

| # | 文件 | 动作 |
|---|------|------|
| B3-1 | `Plugins/McpCenter/Services/PublicTools/UniversalTool.cs`（新，自 AIAgent 迁） | universal_tool；描述举例同步；ctx.Get<IToolRegistry> 模式不变 |
| B3-2 | `Plugins/McpCenter/Services/PublicTools/SearchToolsToolFunction.cs`（新） | search_tools + ToolSearchScorer 一并迁 |
| B3-3 | `Plugins/McpCenter/Services/PublicTools/RunTerminalCommandTool.cs` + `TerminalCommandGuard.cs`（新） | run_terminal_command；IProjectWorkspaceService 改引 Abstractions 契约（CWD 门不变） |
| B3-4 | `Plugins/McpCenter/Services/PublicTools/FileToolFunctions.cs`（新） | read_file/write_file/list_files + FileToolLineOps；绝对路径化（FR-5）；list_files 回退经 ctx.Get<IProjectWorkspaceService> |
| B3-5 | `Plugins/McpCenter/Services/PublicTools/BasicTools.cs`（新） | mcp_get_current_time + mcp_calculate |
| B3-6 | `Plugins/McpCenter/McpCenterPlugin.cs` | Apply 里 ToolExtensions.Add 上述 8 实例（既有模式） |
| B3-7 | `Plugins/AIAgent/Services/ProjectFileToolFunctions.cs`、`Services/ToolFunctions/UniversalTool.cs`、`SearchToolsToolFunction.cs`、`RunTerminalCommandTool.cs`、`AIAgentPlugin.cs`（:203-273 时间/计算、:138-167 注册行、:470-855 三个工作流工具类） | **删除**（AIAgentPlugin 只留 plan_workflow 注册 + runFlow 三件 + ToolExtensions 瘦身） |
| B3-8 | `Plugins/AIAgent/Services/ProjectWorkspaceService.cs` | 接口声明改自 Abstractions 继承（`public class ProjectWorkspaceService : IProjectWorkspaceService`，接口本体已上移）；AIAgent 命名空间 using 调整 |
| B3-9 | McpCenter csproj / AIAgent csproj | 确认仅引 Core+Abstractions（AC-5） |

**B3 验证**：`dotnet build`；新增 `McpCenterTests/PublicToolFunctionsTests`（绝对路径读/写/list、行区间、未选项目根 read/write 可用）；`McpCenterTests` 过滤集绿。

## B4 源码字面量 + 前端 + e2e + 文档

| # | 文件 | 动作 |
|---|------|------|
| B4-1 | `AIAgentService.cs`（:611 排除名不变[豁免]、:1263/:1287 `memory_relevant`、:403-409 白名单过滤直接用新名）、`ReactLoopAgent.cs`（:721） | 字面量换新 |
| B4-2 | `Plugins/AIAgent/web/src/composerContext.ts`（三张表）+ `composerContext.test.ts` + `composer.test.ts` + `agent-core/trigger.test.ts` + `toolEvents*.test.ts` | 新名替换（DANGER_RE 不动） |
| B4-3 | `ForgeSelf.Api.Tests` 字面量：UniversalToolTests、SearchToolsToolFunctionTests、ListToolsToolFunctionTests、PlanDslTests、UniversalToolForwarderTests、FakeToolPlugin、ProjectWorkspaceDefaultRootTests 等（per grep 清单） | 换新名/迁目录 |
| B4-4 | `ForgeSelf.Web/e2e/plugins/ai-agent/*.spec.ts`（4 个） | enabledToolNames/提示词/断言换新名新归属 |
| B4-5 | 描述举例：McpCenter `UniversalToolForwarder.cs:47`、`ListToolsToolFunction.cs:41`、`McpUniversalTool.cs:34`；AIAgent `BuiltInAgentExecutionService.cs`（:212 `write_file` 裸名豁免，核对即可） | 校对+换 |
| B4-6 | 文档：021（清单节重写）、034、根目录 fs工具集文档、design-system-consume 技能、ADR `docs/07-decisions/004-tool-namespace.md`（新）、not-taken 增补（不做别名/存量迁移——用户拍板；run_terminal CWD 门不动；不做根白名单） | 同步 |

**B4 验证**：ForgeSelf.Web check+test；AIAgent web build；宿主全量 `dotnet test`（中档收口）；ai-agent tool-gateway-031 e2e + 涉改插件冒烟。

## Test Plan（守卫覆盖）
- 新增：ToolNamespaceGuardTests（4 断言+mutation）、McpCenter PublicToolFunctionsTests（绝对路径/行区间/未选根）、ToolRegistry 重名锚定。
- 复跑：ToolRegistryTests、UniversalToolTests、SearchToolsToolFunctionTests、ListToolsToolFunctionTests、各插件过滤集。
- 非缺陷任务（无 PC 命中）；守卫=命名空间防复发（预防类）。

## Verification（分档）
- 快档（每批）：相关过滤集 + 涉改插件 build。
- 中档（B3/B4 收口）：宿主全量 `dotnet test` + Forge.Web check/test。
- e2e：tool-gateway-031（4 场景）+ 涉改插件冒烟。
- 回滚点：每批一 commit（闸门3 后执行；工作区阶段靠 git diff/stash）。

## 风险与顺序
- B1→B2 强序（校验开关同 PR）；B3 依赖 B1（契约上移）；B4 依赖 B2/B3。
- 高风险（模型可见契约全量变更）→ 已获闸门1（2026-10-28 用户拍板，含 v2 修订）；执行偏差先记录再修正（Stage 5.7）。
