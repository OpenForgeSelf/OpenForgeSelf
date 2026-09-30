# 028 · 项目工作区（Project Workspace）

> 状态：已实现/已闭环（sems v1.1.0：插件内自助项目生命周期 + 13 个对外工具，详见文末「v1.1.0 变更」）
> 最后更新：2026-09-28
> 关联：`docs/ai/pilot/sems-selfcontained-mcp-tools/`（本轮 AI-Native 九阶段工件：00~06）、架构依据 `docs/01-architecture/host-capability-seams.md`（L1 契约 + L2 事件）

## 概述

项目工作区把「会话选定工作目录即登记为一个项目」的产物，升级为**宿主级数据库管理的项目档案 + 可运行命令 + 运行列表**系统。

**sems 插件自身是完整的应用（v1.1.0 起）**，不再依赖任何其他插件的动作：用户在 sems 面板可以——

- **添加项目**（目录浏览逐级进入或直接输入绝对路径；来源记 `manual`）、查看所有项目（类型徽标、标签、描述、git/可达状态）；
- 编辑项目档案（类型 / 描述 / 标签 / 名称）、**移除项目档案**（级联删命令，不动磁盘）；
- 为每个项目维护一组**运行命令**（名称、脚本、运行后访问地址、排序）；
- 在运行面板**启动**命令、**停止**进程（面板启动或外部捕获）、点「检查」**捕获本机已运行**的该项目进程（WMI 路径+类型归属判定），并对运行中条目提供**快捷访问图标**；
- 上述全部能力同时以 **13 个 `sems_*` 工具**经 mcp-center 对外提供（外部 AI/客户端可发现、可调用）。

AIAgent 的「选工作目录」仍是登记触发源之一（来源记 `ai-agent`），与 sems 手工登记互不覆盖。

## 关联文档

| 文档 | 位置 | 作用 |
|------|------|------|
| 本轮工程工件 | [`docs/ai/pilot/sems-selfcontained-mcp-tools/`](../ai/pilot/sems-selfcontained-mcp-tools/) | 九阶段：仓库理解 / 意图 / 规格 / 计划 / 任务 / 证据 / 审查 |
| 架构依据 | [`01-architecture/host-capability-seams.md`](../01-architecture/host-capability-seams.md) | L1 契约（pull 默认）/ L2 事件（push 补充）三层模型；§4.1 方案 B（宿主 seed，sems 亦是登记方） |
| 对外工具链路 | [`02-features/034-mcp-center.md`](034-mcp-center.md) | `universal_tool` / `list_tools` 对外形态与网关鉴权 |

> 历史说明：原 `specs/028-project-workspace/`（feature/requirements/design）不在当前仓库内，其结论已并入本文与 pilot 工件。

## 代码落点

| 层 | 文件 |
|----|------|
| 宿主实体 | `ForgeSelf.Api/Entities/Project.cs` + `Project.Biz.cs`、`RunCommand.cs` + `RunCommand.Biz.cs`（手写实体，ConnName=`ForgeSelf`，落宿主库 `~/.forgeself/ForgeSelf.db`；`DeleteByProjectId` 供级联删除） |
| 宿主接缝实现 | `ForgeSelf.Api/Services/HostProjectRegistry.cs`（`IProjectRegistry` 实现，宿主 `ProvideHostServices` seed 进 root，常驻；含一次性 JSON→库迁移，原文件保留） |
| 契约 | `ForgeSelf.Abstractions/IProjectRegistry.cs`（`Register(root,source,out err)` / `Remove(id,out err)` + `ProjectInfo` / `RunCommandInfo` / `ProjectUpdate` / `RunCommandUpdate`） |
| sems 业务落点 | `Plugins/Sems/Services/ProjectService.cs`（`IProjectService` = 项目/命令全部操作 + `SemsResult<T>` 状态语义 + `Browse` 目录浏览） |
| sems 运行管理 | `Plugins/Sems/Services/RunnerService.cs`（`RunSession` / `Launch` / `Stop` / `StopExternal` / `CheckAndMerge` / `DetectViaWmi`） |
| sems 控制器 | `Plugins/Sems/Controllers/`：`ProjectsController.cs`、`ProjectCommandsController.cs`、`RunsController.cs`（薄控制器，一律委托服务层；冗余 `RunnerController` 已移除） |
| sems 进程匹配 | `Plugins/Sems/Services/ProcessMatcher.cs`（路径前缀+分隔符边界+类型二次确认） |
| sems 对外工具 | `Plugins/Sems/ToolExtensions.cs`（13 个 `sems_*` 工具 + 统一基类），注册点 `Plugins/Sems/SemsPlugin.cs` |
| sems 前端 | `Plugins/Sems/web/src/`：`SemsView.vue`（壳+统计+添加/移除）、`ProjectCard.vue`、`ProjectEditDialog.vue`、`DirectoryPickerDialog.vue`、`CommandList.vue`、`RunPanel.vue`、`confirmOps.ts`（确认编排）、`http.ts`、`types.ts` |

## 架构要点

- **数据供给 = L1 契约 + 宿主 seed（方案 B）**：`Project` / `RunCommand` 入宿主库，经 `IProjectRegistry`（宿主实现，seed 进 root 上下文）供给；AIAgent 与 sems **都是平等的登记方/消费方**，`ctx.Get<IProjectRegistry>()` 每次用每次取，不缓存实例。sems **不自建库**（方案 A 的空洞问题正是当初否掉它的理由）。
- **一处实现，三个消费面**：`IProjectService` / `IRunnerService` 是 sems 唯一业务落点；自带界面（HTTP 控制器）、对外工具（`ToolExtensions`）、未来第二个消费面都走它，控制器与工具不再直连接缝。
- **状态语义统一**：服务层返回 `SemsResult<T>`（`Ok/Invalid/NotFound/Conflict/Unavailable` + `StatusCode`），HTTP 端点据此选 400/404/409/503，工具据此出 `{success,data|error}` 封套——不靠字符串嗅探分流。
- **进程运行管理留在 sems 插件内**（`RunnerService`）：当前唯一消费方，未升格为宿主接缝；第二个消费方出现时再升 `IProcessRunner`。
- **不引入 L2 事件**：运行列表靠请求时拉取 + 手动「检查」，无需实时推送。
- **检测（检查）算法**：一次 WMI 查询全量 `Win32_Process`（ProcessId/Name/ExecutablePath/CommandLine）→ 对每条按「候选路径集合（exe + 命令行出现的绝对路径）是否以某项目 Root 为前缀（带分隔符边界）→ 类型二次确认（node↔frontend/fullstack、dotnet↔backend/fullstack，宁漏勿误）」归属 → 与 Launched 会话按 PID 合并。只读、不落库、每次全量重建 Detected。WMI 不可用则降级仅 Launched。
- **启动 / 停止**：`cmd /c {script}` 在项目根执行；停止用 `taskkill /T /F /PID {pid}`（比 .NET `Kill(entireProcessTree)` 对 `cmd` 子进程树更可靠），Launched 与 Detected 统一按 PID 杀树。
- **移除项目 = 删档案不删盘**：`Remove` 级联删 `RunCommand` 行后删 `Project` 行，**绝不触碰磁盘目录/文件**；有本面板启动的存活会话时拒绝（409），被拒时零数据变更。

## API（实际路由；全部类级 `[Authorize("ApiKeyPolicy")]`，`api/projects/count` 例外为公开统计卡）

| 方法 | 路由 | 说明 | 控制器 |
|------|------|------|--------|
| GET | `api/projects` | 列表（含每项目 commands 概要） | `ProjectsController` |
| GET | `api/projects/count` | 项目总数（`AllowAnonymous`） | `ProjectsController` |
| **POST** | **`api/projects`** | **插件内登记项目** `{root, name?}` → `{success, project}`；目录不存在/非法 400；接缝缺席 503 | `ProjectsController` |
| PUT | `api/projects/{id:int}` | 编辑档案（name/type/description/tags） | `ProjectsController` |
| **DELETE** | **`api/projects/{id:int}`** | **移除项目**（级联删命令，不动磁盘）；不存在 404；有存活运行 409 | `ProjectsController` |
| **GET** | **`api/projects/browse?path=`** | **目录浏览**：省略 path 列本机驱动器，否则列直接子目录（不含文件、不递归）→ `{success, path, parent, directories[]}` | `ProjectsController` |
| GET | `api/projects/{projectId}/commands` | 命令列表（按 Sort 升序） | `ProjectCommandsController` |
| POST | `api/projects/{projectId}/commands` | 新增命令 | `ProjectCommandsController` |
| PUT | `api/commands/{id:int}` | 编辑命令 | `ProjectCommandsController` |
| DELETE | `api/commands/{id:int}` | 删除命令 | `ProjectCommandsController` |
| POST | `api/commands/{id:int}/run` | 启动（同命令存活会话存在→409 拒绝重复） | `ProjectCommandsController` |
| POST | `api/commands/{id:int}/stop` | 停止（面板启动的） | `ProjectCommandsController` |
| GET | `api/runs` | 运行列表（Launched） | `RunsController` |
| POST | `api/runs/check` | 触发 WMI 检测并返回合并结果 | `RunsController` |
| POST | `api/runs/{pid}/stop` | 停止外部捕获进程 | `RunsController` |

> `RunnerController`（`api/runner/*`，与上表 `api/commands` + `api/runs` 语义重复、前端与测试零引用）已于 2026-09-28 收敛移除——原「已知问题 #1」闭环。

`RunSession` 响应字段：`commandId` / `projectId` / `projectName` / `commandName` / `pid` / `startedAt`(ISO；Detected 为 MinValue) / `origin`(`Launched`|`Detected`)。

## 对外工具（sems v1.1.0 · 13 个 `sems_*`）

注册链：`SemsPlugin.ToolExtensions` →（宿主 `ExtensionPointManager` 反射）→ `IToolRegistry.RegisterTool` → mcp-center `list_tools` 枚举 / `universal_tool` 转发。**McpCenter 与宿主无需任何改动**。

| 工具名 | 必填参数 | 作用 |
|--------|----------|------|
| `sems_list_projects` | — | 全部项目（含命令概要）；其余工具 id 的来源 |
| `sems_register_project` | `root` | 插件内登记项目（`source=manual`），可选 `name` |
| `sems_update_project` | `id` | 部分更新档案 |
| `sems_remove_project` | `id` | 【不可恢复】删档案+级联删命令，不动磁盘；有存活运行则拒绝 |
| `sems_list_commands` | `projectId` | 某项目命令列表 |
| `sems_add_command` | `projectId`,`name`,`script` | 新增命令（可选 url/sort） |
| `sems_update_command` | `commandId` | 部分更新命令 |
| `sems_delete_command` | `commandId` | 【不可恢复】删除命令 |
| `sems_list_runs` | — | 面板启动的存活会话 |
| `sems_check_runs` | — | WMI 检测 + 合并（Detected 的 commandId=0，按 pid 停） |
| `sems_run_command` | `commandId` | 启动**已登记**命令（不接受任意脚本） |
| `sems_stop_command` | `commandId` | 【不可恢复】停止面板启动的会话 |
| `sems_stop_run` | `pid` | 【高危·不可恢复】按 PID 杀整棵树（外部捕获进程） |

外部调用姿势（与 `034-mcp-center.md` 一致）：

```jsonc
// tools/list 恒只返回 universal_tool；能力经 list_tools 发现
{ "name": "universal_tool", "arguments": { "tool": "list_tools", "parameters": { "keyword": "sems", "includeSchema": true } } }
{ "name": "universal_tool", "arguments": { "tool": "sems_list_projects", "parameters": {} } }
```

- 封套：`{"success":true,"data":{…}}` / `{"success":false,"error":"…"}`；异常一律内层捕获，不外抛。
- 每个 `Description` 都写明「id 来自哪个 list 工具」+「`universal_tool{"tool":"list_tools"}` 可枚举全部工具」+ 破坏性提示（铁律 18）。
- 安全边界：对外只有 MCP 网关一层 token（`FORGESELF_MCP_GATEWAY_TOKEN`），管理面另有 `ApiKeyPolicy`（铁律 17）；不提供任意脚本执行入口（记入 not-taken-decisions）。

## 前端结构（sems `web/`，原生 HTML+CSS + `--el-*` 变量）

- `SemsView.vue`：标题 + 版本徽标（铁律 13，数据来自 `GET /api/plugin`，恒等于 `plugin.json` 的 `Version`，文档与用例都不写死版本号）+ 「添加项目/刷新」工具条 + 统计卡（项目总数 / 运行命令 / 运行中）+ 项目网格 + 运行面板；空态分级（加载中 / 失败可重试 / 无项目引导 / 有项目）。
- `DirectoryPickerDialog.vue`：目录浏览（盘符起步、逐级进入、上级、可直接输入绝对路径回车跳转）+ 可选项目名 → `POST api/projects`（点即登记，无需二次保存）。
- `ProjectCard.vue`：类型徽标、标签 chip、描述省略、git/不可达徽标、展开内嵌 `CommandList`、✎ 编辑、**✕ 移除项目**（`ElMessageBox` 二次确认，文案明示不动磁盘）。
- `CommandList.vue`：命令列表 + 新增/编辑/删除（`confirmOps` 二次确认）/排序（上移下移并持久化 Sort）+ 启动；零命令引导文案。
- `RunPanel.vue`：运行列表（项目/命令/PID/已运行时长/来源徽标）+ 运行数回传父级统计；启动全部（父级遍历）；停止（Launched→`api/commands/{id}/stop`，Detected→`api/runs/{pid}/stop`，均走 `confirmOps`）；快捷访问 `<a target=_blank rel=noopener>`；检查（loading 态）；空态分级。不常驻轮询。
- `confirmOps.ts`：**纯编排、不依赖 UI 框架**（确认动作由调用方注入），锁死「取消 → 一个请求都不发」；破坏性文案集中于此并有 vitest 断言（移除项目/删命令/停会话/停外部进程）。

## 测试覆盖

- 后端（`ForgeSelf.Api.Tests`）：
  - `HostProjectRegistryTests`（登记/来源/唯一键/**改名不被自动登记覆写**/更新/命令 CRUD/**Remove 级联 + 磁盘仍在**/迁移幂等）—— XCode SQLite 随机隔离目录，只创建不删除。
  - `Plugins/Sems/ProjectServiceTests`（**新**，18 例）：登记与来源、重登记不覆写、列表补命令概要、移除（含存活会话 409 与零变更）、命令校验与往返、目录浏览（盘符/子目录/不存在/无权限降级）、接缝缺席全操作 503 降级而 browse 仍可用。
  - `Plugins/Sems/SemsControllerTests`（**新**，16 例）：三个新端点状态码矩阵、既有形状不变、**反射 Theory 断言每个控制器带 `ApiKeyPolicy`**、`RunnerController` 类型已不存在。
  - `Plugins/Sems/SemsToolExtensionTests`（**新**，14 例）：13 工具名/Id 唯一、schema 合法且 `required` 被宿主 `ValidateParameters` 真实拒绝、Description 可发现性与破坏性提示、端到端往返、异常/降级/服务缺失。
  - `Plugins/Sems/RunnerServiceTests`（启停进程树/重复启动拒绝/外部 PID）、`ProcessMatchTests`（归属判定）、`SemsSharedContractTests`（旧契约 JSON 兼容）。
- 插件前端：`vitest`（`confirmOps.spec.ts`，8 例）+ `pnpm run build`（产物 `index.js`+`style.css`，裸导入自检）。
- 插件层 e2e（`ForgeSelf.Web/e2e/plugins/sems/sems.spec.ts`，零 mock）：① 远程加载 + 版本徽标 + 自洽入口 + 无致命报错；② 界面全生命周期（添加→刷新持久→加命令→统计=1→启动→停止→移除取消零请求→确认移除→磁盘仍在）；③ 经 MCP 网关 list_tools 枚举 13 工具 + 真调登记/查询/命令/移除与 `GET api/projects` 对账 + required 拒绝；④ 无令牌访问 sems 全部管理端点 401。

## v1.1.0 变更（2026-09-28）

- 契约：`IProjectRegistry` 新增 `Register(root, source, out err)` 与 `Remove(id, out err)`；修 `HostProjectRegistry` 命中同 Root 时覆写 `Name` 的行为（手工编辑优先）。旧 2 参 `Register` 保留 → **AIAgent 零改动**。
- sems：`IProjectService` 扩为完整操作面并新增 `browse`；控制器改薄委托；**移除冗余 `RunnerController`**；`ToolExtensions.cs` 提供 13 个对外工具；`plugin.json` `1.0.3 → 1.1.0`。
- 前端：添加项目弹层、移除项目二次确认、空态分级、版本徽标、确认编排模块（含 vitest）。
- 缺陷修复：`GET /api/projects` 之前每条项目 `commands` 恒为空（`GetAll` 不附带、服务层未补），导致统计「运行命令」恒 0、运行面板快捷访问图标永不出现、**「启动全部」空转**；现由 `ProjectService.GetProjects()` 补齐并有回归测试 + e2e 断言。

## v1.1.1 变更（2026-09-30）

- 缺陷修复（走查实抓）：**在运行面板内点「停止」后，统计卡「运行中」仍停在旧值**（面板 0 / 统计 1，两者自相矛盾）。根因是 `runningCount` 只在父级 `reloadProjects()` 里赋值，面板自身的 `refresh()`/`check()` 不回传。现由 `RunPanel` 在两条取列表路径后统一 `emit('count', …)`，`SemsView` 只经该事件写 `runningCount`（**单一写入点**）。
- 回归断言：`sems.spec.ts` 停止步骤后新增「`.sems__stat-num` 第 3 格（运行中）== 0」，先红（实抓 `1 failed`）后绿。
- 版本：`plugin.json`/`web/package.json` `1.1.0 → 1.1.1`（`1.1.0` 已按原样侧载进运行实例，内容变更不复用同一版本号）。
- 用例治理：徽标断言由「等于硬编码 `1.1.0`」改为「等于清单版本 + semver 格式」，避免每次正常升版把 e2e 拖红。

## 已知问题 / 待办

### 1. 宿主重启丢运行态（设计如此）

运行列表来自进程实时状态 + WMI 检测，宿主重启后 Launched 会话清空；「检查」按钮可恢复 Detected 捕获。属 NFR 设计接受项。

### 2. AIAgent 目录浏览端点缺失（跨插件，不在本功能内修）

`Plugins/AIAgent/web/src/components/DirectoryPickerDialog.vue` 调 `POST/GET /api/project/browse-directories`，而全仓无该端点实现（`Plugins/AIAgent/Controllers/ProjectController.cs` 未提供）→ 该弹层会 404。已记 `TODO.md`（P2），sems 的浏览能力是**自己新增的 `api/projects/browse`**，与该缺陷无关。

## 使用要点

- 添加项目：sems 首页「添加项目」→ 浏览/输入目录 → 选择此目录（或外部经 `sems_register_project` 工具）。AIAgent 选目录仍会自动登记（`source=ai-agent`）。
- 运行命令在项目卡片展开后维护；「启动全部」遍历所有命令（已运行由后端 409 拒绝，前端继续下一个并汇总提示）。
- 停止外部捕获进程前务必确认——杀树可能波及他人进程且不可恢复。
- 移除项目只删档案与命令记录；磁盘文件不受影响，重新「添加项目」选同一目录即可再次登记。

## 历史遗留说明（spec028 期间确立，本轮仍有效）

- **CA1416**：`RunnerService.cs` 的 WMI 调用是 Windows-only，已在**文件作用域** `#pragma warning disable CA1416` 消除，不改 `Sems.csproj` 的 TargetFramework（避免掩盖其他真问题）。
- **旧共享契约**：`ForgeSelf.Abstractions/SemsShared.cs` 的 `SemsShared`/`ProjectRecord` 已标 `[Obsolete]`；`HostProjectRegistry` 迁移改用内部 `LegacyProjectRecord`，运行态不再依赖 Abstractions 的 `ProjectRecord`（仅 `SemsSharedContractTests` 引用以验证 JSON 兼容）；未硬删，避免回归。
