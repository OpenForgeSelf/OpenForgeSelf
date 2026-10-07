# Repository Understanding

> 阶段：Stage 0（动手写代码之前必须完成）｜规范：docs/04-standards/ai-native-engineering-workflow.md §2
> 原则：所有条目来自**本次真实读取的仓库内容**（文件:行号可复核），禁止凭常识推测。
> Task ID：PILOT-054 ｜ 日期：2026-10-07 ｜ 本 worktree HEAD：`a1f7ce8`（`git worktree list` 实测 detached HEAD，工作区起始 clean）

## 项目结构

| 组成 | 路径 | 依据 |
| --- | --- | --- |
| 解决方案 | `ForgeSelf.slnx` | 仓库根 `ls` 实测 |
| 后端 API + 插件装载器 | `ForgeSelf.Api/` | 含 `Program.cs`、`AppBuilder.cs`、`Data/XCodeConfig.cs`、`Plugins/PluginManager.cs` |
| 契约层 | `ForgeSelf.Abstractions/`（54 个 .cs，含 `IProjectRegistry.cs`、`IToolFunctionExtension.cs`、`Agents.cs`） | 目录列表实测 |
| 内核层 | `ForgeSelf.Core/`（仅 `Context.cs`/`IContext.cs`/`EventBus.cs`/`Fiber.cs`/`Service.cs`/`Disposable.cs`，**零 PackageReference**） | `ls ForgeSelf.Core/` + plugin-development 铁律（§六） |
| 插件源码 | `Plugins/<PascalCase>/`（19 个：AIAgent、AgentHub、CostScope、DesignSystem、…、**TodoTracker**、WorkflowEngine） | `ls Plugins/` 实测 |
| 前端 SPA | `ForgeSelf.Web/`（Vue 3.5 + Vite + Element Plus + Pinia） | AGENTS.md §2.1 + `ForgeSelf.Web/package.json` |
| 后端测试 | `ForgeSelf.Api.Tests/`（含 `Plugins/TodoTracker/`、`Plugins/PluginFrontendManifestTests.cs`） | `ls` 实测 |
| 工件目录 | `docs/ai/pilot/YYYY-MM-DD-<task-id>/`（00-07 八件） | 规范 §2 + `scripts/verify-pilot-artifacts.ps1:52-69` |
| 模板 | `docs/18-templates/ai-pilot/`（00-07 tpl + README） | `ls` 实测 |

## 技术栈

| 层 | 技术 | 依据（文件/配置） |
| --- | --- | --- |
| 后端 | .NET 10（插件 `net10.0`，宿主/测试 `net10.0-windows`）+ ASP.NET Core | `Plugins/TodoTracker/TodoTracker.csproj:4`（`net10.0`）、`ForgeSelf.Api.csproj` |
| ORM | NewLife.XCode（唯一 ORM），插件自带库 | `TodoTracker.csproj:17` `NewLife.XCode 12.0.2026.701`；`ForgeSelf.Api/Data/XCodeConfig.cs:24-41` `PluginDbs` |
| 数据库 | SQLite，插件库隔离到 `{数据根}/Plugins/{插件Id}/{连接名}.db` | `XCodeConfig.cs:60-61`（`Path.Combine(PluginDataRootName, pluginId, connName + ".db")`） |
| 插件机制 | `plugin.json` 清单 + `IPlugin.Apply(IContext)` + 独立 ALC | `Plugins/TodoTracker/plugin.json`、`ForgeSelf.Api/Plugins/PluginLoadContext.cs` |
| 能力供给 | `IContext.Register/Get`（root 共享表）+ `ForgeSelf.Abstractions` 契约 | `ForgeSelf.Core/IContext.cs`（Register/Get 注释）；`docs/01-architecture/host-capability-seams.md:31,43-45,75-81` |
| 前端 | Vue 3.5 + Vite 6 + TS 5.7 + Element Plus + Tailwind + Pinia + pnpm | AGENTS.md §2.1；`ForgeSelf.Web/package.json` |
| 插件前端 | 独立 `web/` vite **lib 模式**，产物 `dist/index.js` + `style.css`，vue/router/pinia/element-plus external | `.agents/skills/plugin-frontend-scaffold/SKILL.md:69-87`；`Plugins/QuickLinks/plugin.json:15-21`（`entry:"web/dist/index.js"`） |
| 测试 | xUnit + Moq + FluentAssertions（后端）；vitest（前端单测）；Playwright 单配置 + globalSetup 零 mock（e2e） | `.agents/skills/e2e-testing/SKILL.md:36-54` |

## 架构特点（与本任务直接相关的事实）

1. **TodoTracker 现状（后端）**：单实体 `Todo`（`Data/Model.xml:25-40`：Id/Title/Remark/Status(0=Pending,1=Completed)/DueDate/CreatedAt/UpdatedAt/CompletedAt）、单服务 `TodoService`（`Services/TodoService.cs`，7 方法）、单控制器 `api/todos`（`Controllers/TodosController.cs:10`，7 端点）、3 个 AI 工具函数 `create_todo`/`list_todos`/`complete_todo`（`TodoTrackerPlugin.cs:53-99`）。
2. **TodoTracker 现状（界面在宿主，不在插件）**：`plugin.json:15-20` 的 frontend **没有 `entry`**，靠 `ForgeSelf.Web/src/router/dynamicPlugins.ts:73-74` 的 `case 'TodoView'` 回退到宿主 `src/views/TodoView.vue`。宿主侧足迹：`views/TodoView.vue`、`components/todo/{TodoListItem,TodoEditDialog}.vue`（+ 两个 `.test.ts`）、`stores/todo.ts`、`services/todoApi.ts`、`types/todo.ts`、`router/index.ts:22,98-100`、`components.d.ts:103-104`、`data/features.ts:305-318`（`signals.views:['TodoView']`，被 `ForgeSelf.Web/scripts/check-features.mjs:92-96` 幽灵页门禁硬校验）。
3. **两处既有规范缺口（实测）**：① `TodosController` **没有** `[Authorize("ApiKeyPolicy")]`（`Controllers/TodosController.cs:9-11` 只有 `[ApiController]`/`[Route]`），违反 plugin-development 铁律 17；② 插件**没有** `Data/TodoTrackerTables.cs`，建表靠宿主反射（`TodoTrackerPlugin.cs:104-118` 注释自陈「仅触发静态构造」），违反铁律 12（`XCodeConfig.cs:171-179` 的 `EnsureTablesCreated` 只扫「当前已加载程序集」）。
4. **项目工作区已有宿主级真相**：`ForgeSelf.Abstractions/IProjectRegistry.cs:13`（`Register(root,source,out error)` / `GetAll` / `Get` / `Update` / `Remove` / `AddCommand`…），DTO `ProjectInfo{Id,Root,Name,Type,Description,Tags,Source,CreatedAt,UpdatedAt,LastActiveAt,PathExists,IsGitRepo,Commands}`（`:54-94`）；宿主实现 `ForgeSelf.Api/Services/HostProjectRegistry.cs:20`，数据落**宿主库** `Project`/`RunCommand` 表（`:17`），已 seed 进插件 root 上下文（`AppBuilder.cs:120-125`）。消费先例：`Plugins/Sems/Services/ProjectService.cs:136-147`（`private IProjectRegistry? Registry => _ctx.Get<IProjectRegistry>()`，每次用每次取）、`Plugins/AIAgent/Controllers/ProjectController.cs`。
5. **宿主路径归一化的真实行为（决定了本任务的规范化必须落在插件内）**：`HostProjectRegistry.Register` 只做 `Path.GetFullPath(root)`（`:56`）+ `Project.FindByRoot(fullPath)` 精确匹配（`:71`），目录不存在即返回「目录不存在」（`:64-68`）。⇒ 在 Windows/.NET 下 Git-Bash 风格 `/d/project` 会被解析为 `C:\d\project`，`Directory.Exists` 为假 ⇒ **直接登记失败**。插件侧同类先例只做 `GetFullPath + Trim('"')` 且按 `OrdinalIgnoreCase` 回读匹配（`Sems/Services/ProjectService.cs:337-377` `Locate`）。**用户已拍板：本任务在插件内部支持多种路径格式，不改宿主注册表。**
6. **AgentHub 已具备「委派 CLI agent 执行」的真实能力**（一键执行的目标）：`POST /api/agent-hub/tasks`（`Controllers/AgentHubTasksController.cs:78-100`），请求体 `DelegationRequest{prompt,agentId?,accessPointId?,cwd?,permissionMode?,sessionRef?,facet?,tag?,createdBy?,wait}`（`Services/DelegationRuntime.cs:120-151`，camelCase）；`DelegationRuntime` 是**插件内单例**（`AgentHubPlugin.cs:74` `services.AddSingleton<DelegationRuntime>()`）；状态机 `Queued|Running|AwaitingPermission|Succeeded|Failed|Cancelled|Timeout|Interrupted`（`DelegationRuntime.cs:13-46`）；`taskKey` = GUID("N")（`Data/Entities/DelegationTask.Biz.cs:35`）；`permissionMode` 只走**黑名单**（禁 `yolo`/`danger-full-access`/`dangerously-skip-permissions`，`Models/AgentPolicy.cs:56`）；`cwd` 必须落在 `policy.allowedCwds` 白名单否则 400（`DelegationRuntime.cs:618-648`）；错误一律 **HTTP 400**（`:243,249,252,259`）；`GET /tasks/by-key/{taskKey}`（`:66-73`）。
7. **插件间能力的既定做法 = Abstractions 契约 + `ctx.Register`/`ctx.Get`（禁止直连 HTTP）**：architecture-design 技能铁律 2/3/4；仓内已成立的提供方/消费方对：`Plugins/MemorySystem/MemorySystemPlugin.cs:49` `ctx.Register<IMemoryService>` → `Plugins/AIAgent/Controllers/AIChatController.cs:518` `ctx.Get<IMemoryService>()`；`Plugins/AIAgent/AIAgentPlugin.cs:68` `ctx.Register<IWorkflowAIAdvisor>` → `Plugins/WorkflowEngine/Services/WorkflowExecutor.cs:287` `ctx.Get<IWorkflowAIAdvisor>()`；`AIAgentPlugin.cs:92` `ctx.Register<IAgentRegistry>` → `AIChatController.cs:61`。（反面存量债：`Plugins/Home/web/src/homeStore.ts:130,145,171-175` 前端直连 6 个别的插件 API —— 属既有实践，**本批不照抄**。）
8. **既有工具函数解析容错**：插件 `Apply` 拿到的 `IServiceProvider` 常是插件上下文，缺 `IServiceScopeFactory`，故 `TodoToolProvider.Resolve` 回落宿主根容器后再 `CreateScope()`（`ToolExtensions.cs:11-21,83-88`）。新工具函数沿用同一形状。

## 测试方式（本仓唯一测试体系，AGENTS.md §5.3）

| 层 | 正规入口 | 现状证据 |
| --- | --- | --- |
| 后端单测/集成 | `cd ForgeSelf.Api.Tests && dotnet test`（定向 `--filter "FullyQualifiedName~TodoTracker"`） | `ForgeSelf.Api.Tests/Plugins/TodoTracker/TodoToolProviderTests.cs`、`Integration/TodosControllerTests.cs`、`TodoServiceTests.cs`（grep 实测）；`ForgeSelf.Api.Tests.csproj:49` 已 `ProjectReference` TodoTracker |
| 前端 | `cd ForgeSelf.Web && pnpm run check` / `pnpm run test` | AGENTS.md §5.1 |
| 插件前端 | `cd Plugins/<X>/web && pnpm install && pnpm build`；发布链按 CI 同参数复跑 | plugin-development 铁律 20、§四 1 |
| e2e | `pnpm exec playwright test --config=playwright.config.ts e2e/plugins/<id>` | e2e-testing 技能；`e2e/plugins/` 现有 9 个插件目录，**无 todo-tracker**（`ls` 实测）；现存应用层 `e2e/todo.spec.ts`（370 行，`/todo` CRUD + 首页面板） |
| 工件门禁 | `pwsh scripts/verify-pilot-artifacts.ps1 -TaskId <dir>`（00-07 八件 + 关键节） | `scripts/verify-pilot-artifacts.ps1:52-96`；`02` 需 ≥5 个 `## ` 节，`04` 需含 Allowed/Forbidden |

## 构建命令

```bash
cd ForgeSelf.Api && dotnet build                                  # 宿主（含插件 ProjectReference）
cd ForgeSelf.Api.Tests && dotnet test --filter "FullyQualifiedName~TodoTracker"
cd Plugins/TodoTracker/Data && ~/.dotnet/tools/xcode Model.xml    # 实体生成（实测工具在位，v11.25.2026.912）
cd Plugins/TodoTracker/web && pnpm install && pnpm build          # 插件前端（本批新建）
cd ForgeSelf.Web && pnpm run check && pnpm run test && npx vue-tsc -b
cd ForgeSelf.Web && pnpm exec playwright test --config=playwright.config.ts e2e/plugins/todo-tracker
```

## 主要目录职责

| 目录 | 职责 |
| --- | --- |
| `Plugins/TodoTracker/` | 本任务主体：`Data/Model.xml`（列真源）+ `Data/Entities/*.cs`（生成件，禁手改）+ `*.Biz.cs`（人工）+ `Services/` + `Controllers/` + `Models/` + `ToolExtensions.cs` + 新增 `web/` |
| `ForgeSelf.Abstractions/` | 跨边界契约与共享 DTO（本批新增 agent 委派契约） |
| `Plugins/AgentHub/` | 委派运行时提供方（本批只**新增** `ctx.Register<IAgentDelegation>` 与适配器，不改其状态机/控制器） |
| `ForgeSelf.Web/src/**` | 迁移后**删除** todo 页面代码（铁律 3：界面归插件） |
| `ForgeSelf.Web/e2e/plugins/todo-tracker/` | 本批新增插件层 e2e |
| `docs/02-features/005-todo-tracker.md` | 功能文档（能力/契约变化必须同步，plugin-development §四 5） |

## 代码组织方式

- 插件实体一律 `Model.xml` → `xcode` 生成；自定义查询/校验写 `.Biz.cs`（`Todo.Biz.cs:29-81` 现有 `Valid()` 长度校验与时序补齐即为样例）。
- 插件服务经 `services?.AddScoped<ITodoService, TodoService>()`（`TodoTrackerPlugin.cs:24`）注册进插件子容器；宿主契约一律 `ctx.Get<T>()`。
- 控制器统一返回 `ApiResponse<T>.Ok/Error` 封套（`TodosController.cs:32-37`），`ArgumentException ⇒ 400`、其余冒泡 500 的既有口径（`:103-112`）。
- 前端：宿主 service 层 `parseResponse` 已解包 `json.data`（铁律 15）；插件 `web/` 自带 `http.ts` 从 `localStorage['forge_api_token']` 取 Bearer（`Plugins/QuickLinks/web/src/http.ts:14,124-136`）。

## 现有工程规范（对本任务有约束力）

- **AGENTS.md**：§0 预飞五步 + 插件任务五步硬门禁；§5.6 门禁分档（改 `ForgeSelf.Api/**`、`scripts/**`、共享夹具 ⇒ 中档全量 `dotnet test`；改 `e2e/global-setup.ts`/config/fixtures ⇒ 深档）；§3.1 对外方案六段 + 方案必须落盘并报路径；§10 汇报铁律（Verified/Inferred/Unknown）。
- **plugin-development**：铁律 3（界面归插件）、9（Model.xml 真源 + xcode 生成，改完三件套）、12（插件自行建表 `Data/<X>Tables.cs`）、12b（三处登记：`PluginDbs`/`ForgeSelf.Api.csproj`/测试 csproj；本插件三处**均已在位**：`XCodeConfig.cs:32`、`ForgeSelf.Api.csproj:107`、`ForgeSelf.Api.Tests.csproj:49`）、13（版本徽标）、15（parseResponse 已解包）、17（管理面控制器必须 `[Authorize("ApiKeyPolicy")]`）、19（菜单/路由只声明一处：`plugin.json` frontend 与 `IMenuExtension` **不得并存冲突**）、20（`web/` 必须带 `pnpm-lock.yaml` + `pnpm-workspace.yaml`）、21（解析外部文本 ⇒ 真实样例驱动，围栏/裸发成对）。
- **architecture-design**：铁律 2（契约入 Abstractions）、3（禁止直连 HTTP 当契约）、4（`ctx.Get` 每次取、禁缓存为字段）、5（DB 迁移/契约破坏属高风险须升级）；产出与门禁要求 ADR + `docs/07-decisions/not-taken-decisions.md` 登记。
- **e2e-testing**：零 mock、地址取自 `e2e/helpers/e2e-env.ts`、交互确认类两条路径都测、截图读图 Level 3、判定只读落盘日志不读 exit code。
- **环境前置（AGENTS.md §5.0）**：`NO_PROXY=localhost,127.0.0.1,::1`、`TEMP/TMP=<repo>\.temp\tmp`（后端测试/vite 预构建/esbuild 三处假红）、脚本一律 `pwsh` 禁 `powershell` 5.1。

## 候选低风险任务（同仓可选，用于对照本任务的取舍）

1. 给 `TodosController` 补 `[Authorize("ApiKeyPolicy")]`（1 文件，纯安全）——最小，但不满足用户需求。
2. 只加 `Priority`/`Category` 列（Model.xml + xcode）——范围小，但同样不满足需求。
3. 给 todo-tracker 补插件层 e2e（当前 `e2e/plugins/` 缺该目录）——可独立做。

## 选择该任务的原因

用户指令（2026-10-07 输入1）明确要求升级 TodoTracker 为「可向 agent 下发任务」格式，且已就四个分叉拍板（界面迁插件 `web/`、路径规范化落插件内、下发做到「一键交给 AgentHub 执行」、数据模型原地扩 `Todo` + 新增 `TaskExecution`）。它不是低风险任务：含插件自有库的表结构变更（规范 §1.2 须升级审批项，已在闸门1 摆到台面）与 Abstractions 新增契约（中风险，触发中档全量后端测试）。候选 1/3 作为本任务的**内含子项**一并闭合（补鉴权、补插件 e2e），候选 2 被更完整的下发格式取代。
