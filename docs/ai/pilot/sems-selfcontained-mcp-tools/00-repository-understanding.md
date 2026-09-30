# Repository Understanding

> 阶段：Stage 0（动手写代码之前必须完成）｜规范：docs/04-standards/ai-native-engineering-workflow.md §2
> 原则：所有条目来自**真实仓库内容**（本文件每条都带 file:line 依据），禁止凭常识推测。
> Task ID：PILOT-sems-selfcontained-mcp-tools ｜ 日期：2026-09-28 ｜ 级别：**全量**（插件任务 + 公共契约变更，规范 §4）

## 项目结构

| 组成 | 路径 | 依据 |
| --- | --- | --- |
| 解决方案 | `ForgeSelf.slnx` | 仓库根实测存在 |
| 后端宿主 | `ForgeSelf.Api/`（ASP.NET Core，Controllers/Services/Entities/Plugins） | `ForgeSelf.Api/ForgeSelf.Api.csproj` |
| 契约层 | `ForgeSelf.Abstractions/` + `ForgeSelf.Core/` | 目录实测 |
| 插件源码 | `Plugins/<PascalCase>/`（18 个：AIAgent/AgentHub/…/Sems/…/WorkflowEngine） | `ls Plugins/` 实测 |
| 前端宿主 | `ForgeSelf.Web/`（Vue 3.5 + Vite 6 + TS 5.7 + pnpm） | `ForgeSelf.Web/package.json` |
| 后端测试 | `ForgeSelf.Api.Tests/`（xUnit + Moq + FluentAssertions） | `ForgeSelf.Api.Tests/` 实测 |
| e2e | `ForgeSelf.Web/e2e/`（Playwright，globalSetup 自动构建并起宿主） | `ForgeSelf.Web/playwright.config.ts:18,27` |
| AI-Native 工件 | `docs/ai/pilot/<task-id>/`（既有：batch-a-menu-route-consistency、batch-b-provider-catalog） | 目录实测 |
| 规范 | `docs/04-standards/ai-native-engineering-workflow.md`（v1.1.0 强制）、`AGENTS.md` §0/§11 | 文件实测 |

> 注：本 worktree **无 `specs/` 目录**（`docs/02-features/028-project-workspace.md:20-22` 仍指向 `specs/028-*`，链接失效）；`.forgeself/` 也不在版本库内（本轮已重建当天日记）。`docs/` 是本次可用的文档真源。

## 技术栈

| 层 | 技术 | 依据（文件/配置） |
| --- | --- | --- |
| 后端 | .NET 10 + ASP.NET Core + SQLite + NewLife.XCode（唯一 ORM） | `ForgeSelf.Api/ForgeSelf.Api.csproj`、`ForgeSelf.Api/Entities/Project.Biz.cs:1-20`（`Entity<Project>`/`DataMethod`） |
| 插件 | `IPlugin.Apply(IContext)` + `plugin.json` 清单；扩展点反射发现 | `ForgeSelf.Abstractions/IPlugin.cs:6-9`、`ForgeSelf.Api/Plugins/ExtensionPointManager.cs:151-177` |
| 前端 | Vue 3.5 + Vite 6 + TS + Element Plus + Pinia + pnpm（非 npm） | `ForgeSelf.Web/package.json`、AGENTS.md §2.3 |
| 插件前端 | 独立 Vite lib 构建 → `web/dist/index.js`，`vue/vue-router/pinia/element-plus` 全 external，宿主 import map 解析同一实例 | `Plugins/Sems/web/vite.config.ts`（`rollupOptions.external`） |
| 后端测试 | xUnit + Moq + FluentAssertions；XCode 测试用随机隔离目录 | `ForgeSelf.Api.Tests/Services/HostProjectRegistryTests.cs` |
| e2e | Playwright，testDir `./e2e`，baseURL `http://localhost:7002`，MCP 网关端口默认 `18889` | `ForgeSelf.Web/playwright.config.ts:18,35`、`Plugins/McpCenter/Services/McpGatewayConfig.cs:14` |

## 架构特点（与本任务直接相关的四条）

**① sems 的数据不在插件内，在宿主库，经 L1 接缝读。**
- 契约：`ForgeSelf.Abstractions/IProjectRegistry.cs:12-37` = `Register(root, out error)` / `GetAll` / `Get` / `Update` / `AddCommand` / `UpdateCommand` / `DeleteCommand` / `GetCommands`。
  **没有 Remove/Unregister（项目不可删）**；`ProjectInfo.Source` 注释已预留 `manual`（`IProjectRegistry.cs:60-61`）但无人写入。
- 实现：`ForgeSelf.Api/Services/HostProjectRegistry.cs:37-88`——`Source` 硬编码 `"ai-agent"`（:81），且**Root 已存在时会用目录名覆写 `Name`**（:68）→ 手工改名会被下一次登记冲掉。
- 表落宿主库 `ConnName=ForgeSelf`（`ForgeSelf.Api/Entities/Project.cs:22`、`RunCommand.cs:21`）→ `~/.forgeself/ForgeSelf.db`（`ForgeSelf.Api/Data/XCodeConfig.cs:16`）。
- **sems 自身无 `Data/`、无 `Model.xml`、无实体**（`find Plugins/Sems` 实测 23 个文件，无数据层）。
- 架构裁决：`docs/01-architecture/host-capability-seams.md` §4.1 方案 B（宿主 seed）明确列出的优点就是「其他来源（**手动添加**、第三方插件登记）」有地方挂，且「AIAgent/sems/未来任何插件都是平等的消费方/**登记方**」。**→ 「sems 自己加项目」是既有 ADR 授权的方向，不是新架构决策。**

**② 「等外部调用」的具体表现：全仓只有一个 Register 触发点。**
- `Plugins/AIAgent/Controllers/ProjectController.cs:30-41`（`POST /api/project/directory` → `_registry.Register(...)`）。
- sems 端只读不写：`Plugins/Sems/Services/ProjectService.cs:38`、`Controllers/ProjectsController.cs:29`、`ProjectCommandsController.cs:31`、`Services/RunnerService.cs:79`。
- sems 首页文案直接把用户支走：`Plugins/Sems/web/src/SemsView.vue:9`「选择目录请前往「AI Agent」页」、:35 空态同样话术。
- 前端无任何「添加项目 / 移除项目」入口（`Plugins/Sems/web/src/` 全量 grep：只有 GET/PUT projects、命令 CRUD、启停）。

**③ 工具对外暴露走宿主 ToolRegistry，McpCenter 零改动即可达。**
- 契约：`ForgeSelf.Abstractions/IToolFunctionExtension.cs:6-24` = `Description` + `ParametersJsonSchema`（raw JSON Schema 串）+ `Task<string> ExecuteAsync(string parameters)`；基 `IExtensionPoint`（`Id`/`Name`/`PluginId`）。
- 发现链：插件 `ToolExtensions` 属性反射 → `ExtensionPointManager.cs:151-177` → `IToolRegistry.RegisterTool` → `ForgeSelf.Api/Services/ToolRegistry.cs:23-45`，**按 `tool.Name` 去重（重名静默不注册 :43）**；参数校验 `ValidateParameters` :109-198（`required`/`type`/`enum`）。
- 对外面：`Plugins/McpCenter/Services/McpGatewayServer.cs:59-61`（自建 Kestrel `POST /mcp`，:109-118 Bearer token）→ `McpJsonRpcHandler.cs:193-200` `tools/list` **恒返回 1 个 `universal_tool`** → `UniversalToolForwarder.cs:63-136` 按名转 `IToolRegistry`。
- 发现能力：`Plugins/McpCenter/Services/ListToolsToolFunction.cs:75-78` 读 `registry.GetAllTools()`——**sems 工具注册进宿主 registry 后自动可枚举**（AGENTS.md 铁律 18 的实现）。
- sems 现状：`Plugins/Sems/SemsPlugin.cs:12` 的 `ToolExtensions` 是**空列表**（全仓仅 Home 与之同为空）。
- 参照实现：`Plugins/TodoTracker/ToolExtensions.cs` + `TodoTrackerPlugin.cs:53-89`（同类形态：一个 `ToolExtensions.cs` 承载 N 个工具类、`Id="sems.tool.<name>"`、注入 `IServiceProvider` + `CreateScope()`）。
- 命名占用实测：`list_files`/`run_script`/`kill_process` 等已被别的插件占用；**`sems_` 前缀全仓零占用**。

**④ sems 内部结构已有「两套真相」的苗头。**
- 写路径散在控制器里直连接缝（`ProjectCommandsController.cs:59,73,86`、`ProjectsController.cs:55`），`IProjectService` 只有 `GetProjects()/Count` → 工具/前端要复用逻辑没有落点。
- 冗余控制器：`Plugins/Sems/Controllers/RunnerController.cs`（`api/runner/*`）与 `ProjectCommandsController`+`RunsController` 端点语义完全重复（:25-67 vs `ProjectCommandsController.cs:93-110`），前端与测试均未使用 → `docs/02-features/028-project-workspace.md:91-97` 已登记为已知问题 #1。
- 前端确认走 `window.confirm/alert`（`CommandList.vue:150`、`RunPanel.vue:148,163`），非项目约定的 `ElMessageBox` + 可单测编排函数（技能 §3.2）。
- 缺版本徽标（铁律 13）：`SemsView.vue` 全文件无 version 相关代码。

## 测试方式

| 层 | 正规入口 | 现有 sems 覆盖 |
| --- | --- | --- |
| 后端单测 | `cd ForgeSelf.Api.Tests && dotnet test [--filter ...]` | `ForgeSelf.Api.Tests/Services/HostProjectRegistryTests.cs`（Register/Update/命令 CRUD/迁移 10 例）、`Plugins/Sems/RunnerServiceTests.cs`（启停/重复拒绝/外部 PID 7 例）、`Plugins/Sems/ProcessMatchTests.cs`（归属判定 8 例） |
| 控制器 | 同上（无 sems 控制器测试；仿 `Plugins/McpCenterTests/McpAdminAuthTests.cs:23-28` 反射断言鉴权特性） | **无**（`ProjectsController`/`ProjectCommandsController`/`SemsPlugin` 零测试） |
| 工具 | `ForgeSelf.Api.Tests/Plugins/McpCenterTests/*`（`ListToolsToolFunctionTests`、`UniversalToolForwarderTests`） | **无 sems 工具测试**（因为还没有工具） |
| 插件前端 | `cd Plugins/Sems/web && pnpm run build`（沙箱内走技能 §3.2 出树构建兜底） | 无 vitest（`Plugins/Sems/web/package.json` 仅 build/dev） |
| 宿主前端 | `cd ForgeSelf.Web && pnpm run check && pnpm run test` | 本次不改宿主前端 |
| e2e | `bash node_modules/.bin/playwright test --config=playwright.config.ts e2e/plugins/sems/sems.spec.ts` | `ForgeSelf.Web/e2e/plugins/sems/sems.spec.ts`（155 行，**仅远程加载冒烟**：`.sems` 渲染 + 标题文本 + entry 200 + 截图；无业务断言） |
| 菜单/路由对账 | `ForgeSelf.Web/e2e/menu-route-consistency.spec.ts` | 本次不改 route/menu（见 Plan「不做」） |

## 构建命令

```bash
# 后端（本次已实测基线：0 错误 / 823 警告，全部为既有 CS8618 等）
dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj -v q --nologo

# 后端测试
cd ForgeSelf.Api.Tests && dotnet test

# 插件前端
cd Plugins/Sems/web && pnpm run build   # 沙箱内不可用时走技能 §3.2 出树构建兜底

# 宿主前端门禁
cd ForgeSelf.Web && pnpm run check && pnpm run test

# 插件层 e2e（Playwright CLI 在 Git-Bash 下须加 bash 前缀）
cd ForgeSelf.Web && bash node_modules/.bin/playwright test --config=playwright.config.ts e2e/plugins/sems/sems.spec.ts
```

## 主要目录职责

| 目录 | 职责 |
| --- | --- |
| `Plugins/Sems/` | sems 插件本体：`SemsPlugin.cs`（Apply/扩展点）、`Controllers/`（4 个）、`Services/`（`ProjectService`/`RunnerService`/`ProcessMatcher`）、`web/`（自带界面） |
| `ForgeSelf.Abstractions/` | L1 契约层（`IProjectRegistry.cs`、`IToolFunctionExtension.cs`、`ToolDtos.cs`） |
| `ForgeSelf.Api/Services/` | 宿主接缝实现（`HostProjectRegistry.cs`）+ `ToolRegistry.cs` |
| `Plugins/McpCenter/` | MCP 对外网关（`tools/list` 恒 1 个 universal_tool + `list_tools` 枚举宿主 registry） |
| `ForgeSelf.Web/e2e/plugins/sems/` | sems 插件层 e2e |

## 代码组织方式

- 插件 = 独立 csproj（`Plugins/Sems/Sems.csproj`，AssemblyName `Sems`）编译进宿主插件目录；目录 PascalCase、运行时 id kebab（`sems`）。
- 实体：改结构必须 `Data/Model.xml` → `xcode Model.xml`，业务写 `.Biz.cs`（铁律 9）。**本任务不涉及**（sems 无自有表，宿主 Project/RunCommand 表结构不改）。
- 插件服务经 `IContext` 取宿主接缝，**每次用每次 `ctx.Get`，禁止缓存实例为字段**（`docs/01-architecture/host-capability-seams.md` §3 L1、`ProjectService.cs:30` 已如此实现）。
- 插件界面只依赖 HTTP + `localStorage['forge_api_token']`（`Plugins/Sems/web/src/http.ts` 已带信封解包）。

## 现有工程规范（对本任务有约束力）

1. `docs/04-standards/ai-native-engineering-workflow.md`：九阶段 + 三道闸门；§1 硬性约束 **2 不改数据库结构**、**5 不无关重构**、**7 不为展示能力扩大范围**；§4 本任务级别=**全量**（00~06 七件）。
2. `.agents/skills/plugin-development/SKILL.md`：铁律 **13**（根视图版本徽标）、**16**（发布=打 tag 自动发布，禁止 agent 停/启/杀宿主）、**17**（管理面控制器必须类级 `[Authorize("ApiKeyPolicy")]`）、**18**（工具可发现性：list_tools 枚举）；§四 维护闭环四步（门禁 → 插件 e2e → 发布 → 走查）；§3.4 交互设计统一要求（点即保存、成败可见、空态分级、破坏性操作二次确认）。
3. `docs/01-architecture/host-capability-seams.md` §4.1：项目数据归**宿主 seed**（否掉「插件提供接缝」）→ 本任务**不把 sems 改成数据 owner**，而是补 sems 的**登记/删除操作面**。
4. AGENTS.md §5.3 测试方式铁律：验证必须沉淀为可重复测试，禁止一次性 `temp/*.cjs`。

## 基线事实（Verified，本轮亲跑）

- `dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj` → **0 错误 / 823 警告**（警告全为既有实体 CS8618 等），耗时 13.96s。
- `Plugins/Sems/plugin.json` 当前 `Version = 1.0.3`；`Plugins/Sems/web/package.json` `version = 1.0.0`（两者不同步，非缺陷，插件生效版本以 plugin.json 为准）。

## 候选低风险任务（本目录内的可裁剪项）

| 项 | 风险 | 说明 |
| --- | --- | --- |
| 契约扩展（Register 带 source / Remove） | 中 | 公共接口变更；保留 2 参重载 → AIAgent 零改动 |
| sems 服务层收口 + 控制器委托 | 低 | 端点契约不变，只搬实现 |
| 13 个 `sems_*` 工具 | 低 | 纯新增，宿主/McpCenter 零改动 |
| 前端「添加/移除项目」+ 目录浏览 | 中 | 新端点 `POST/DELETE api/projects`、`GET api/projects/browse`（新增，不影响存量） |
| 删 `RunnerController` | 低 | 前端/测试零引用（已 grep 实证）；但属对外路由删除，需用户点头 |
| 确认逻辑从 `window.confirm` 迁 `ElMessageBox` | 低 | 可单测化 + e2e 可断言；范围仅 sems 插件内 |

## 选择该任务的原因

用户指令即任务来源（AGENTS.md §1.1 最高优先级），且四项诉求可各自映射到上面已核实的事实缺口：
「等外部调用」= §架构特点①②（Source 硬编码、无 Remove、前端无添加入口、空态把用户支走）；
「插件内自洽」= §架构特点④（写路径散在控制器、无服务层落点、两套真相）；
「通过 MCP 中心向外提供工具」= §架构特点③（`ToolExtensions` 空列表，`sems_` 前缀零占用，链路其余环节均已实现并有测试）；
「完善并测试」= 测试方式表（控制器/工具零测试、e2e 仅冒烟）。
