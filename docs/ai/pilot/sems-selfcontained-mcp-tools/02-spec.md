# Specification

> 阶段：Stage 2｜从真实 Repository Understanding（00 号工件）与 Intent 推导。
> 规则：① 所有内容与实际项目一致；② 不发明不存在的接口/类/模块；③ 不确定点显式记为 `Unknown`。
> Task ID：PILOT-sems-selfcontained-mcp-tools

## 术语与现状锚点

| 名词 | 真实落点 |
| --- | --- |
| 项目（Project） | 宿主库表 `Project`（`ForgeSelf.Api/Entities/Project.cs:22`，`ConnName=ForgeSelf`，唯一索引 `UX_Project_Root`） |
| 运行命令（RunCommand） | 宿主库表 `RunCommand`（`RunCommand.cs:21`），外键 `ProjectId` |
| 接缝 | `IProjectRegistry`（`ForgeSelf.Abstractions/IProjectRegistry.cs:12-37`），宿主实现 `HostProjectRegistry` seed 进 root 上下文 |
| 运行会话 | sems 内存态 `RunSession`（`Plugins/Sems/Services/RunnerService.cs:17-39`），`Origin` = `Launched`/`Detected` |
| 工具 | `IToolFunctionExtension`（`ForgeSelf.Abstractions/IToolFunctionExtension.cs:6-24`）→ 宿主 `ToolRegistry`（按 `Name` 去重） |
| MCP 对外面 | `universal_tool`（唯一对外工具）+ `list_tools`（枚举宿主 registry）+ 网关 `:18889` Bearer token |

## Functional Requirements

### FR-A 契约：让「插件内部登记」在数据层成立（宿主侧，最小改动）

- **FR-A1**：`IProjectRegistry` 新增重载 `bool Register(string root, string? source, out string? error)`。
  - `source` 非空 → 新记录写该值（sems 传 `"manual"`）；
  - `source` 空/null → 行为与现 2 参版完全一致（写 `"ai-agent"`）。
  - 既有 `bool Register(string root, out string? error)` **保留**并委托新重载（AIAgent 零改动）。
- **FR-A2**：`Register` 命中已存在 Root 时**只刷新 `LastActiveAt`**，不得覆写 `Name`（修 `HostProjectRegistry.cs:68` 的覆盖行为），不得改动 `Source`/`Type`/`Description`/`Tags`。
- **FR-A3**：`IProjectRegistry` 新增 `bool Remove(int id, out string? error)`：删除该项目行并**级联删除其全部 RunCommand 行**；`id` 不存在返回 `false`。
- **FR-A4**：**不改任何表结构、不加列、不打迁移**（规范 §1.2）。

### FR-B sems 内部自洽：操作面收敛到服务层

- **FR-B1**：`IProjectService` 扩为 sems 全部项目/命令操作的**唯一实现落点**（现仅 `GetProjects()/Count`）：

  | 成员 | 语义 | 校验/失败 |
  | --- | --- | --- |
  | `List<ProjectInfo> GetProjects()` | 全量（既有，保留） | 接缝 null → 空列表 |
  | `int Count { get; }` | 计数（既有，保留） | 同上 |
  | `ProjectInfo? GetProject(int id)` | 单项（含命令） | 不存在 → null |
  | `(ProjectInfo? project, string? error) Register(string root, string? name = null)` | 登记（`Source="manual"`），返回登记后的项目 | 空/非法/目录不存在 → (null, 原因) |
  | `bool UpdateProject(int id, ProjectUpdate update)` | 改档案 | 不存在/接缝 null → false |
  | `(bool ok, string? error) RemoveProject(int id)` | 删除项目（含级联命令） | 有存活会话 → 拒绝且**不删数据**；不存在 → 未找到 |
  | `List<RunCommandInfo> GetCommands(int projectId)` | 命令列表（Sort 升序） | 接缝 null → 空 |
  | `(int id, string? error) AddCommand(int projectId, RunCommandInfo command)` | 新命令 | 项目不存在/名称或脚本空 → (0, 原因) |
  | `bool UpdateCommand(int id, RunCommandUpdate update)` | 改命令 | 不存在 → false |
  | `bool DeleteCommand(int id)` | 删命令 | 不存在 → false |
  | `List<DirectoryEntry> Browse(string? path)` | 目录浏览（见 FR-C3） | 非法/无权限 → 抛 `DirectoryNotFoundException`/`UnauthorizedAccessException` 由控制器转 400 |

- **FR-B2**：`ProjectService` 构造 `(IContext ctx, IRunnerService runner)`；接缝**每次用每次 `ctx.Get`**，禁止缓存为字段（`host-capability-seams.md` §3、现 `ProjectService.cs:30` 已是此形）。
- **FR-B3**：`ProjectsController` / `ProjectCommandsController` / `RunsController` 变为**薄控制器**：参数装配 → 调服务 → 映射 HTTP 状态码。**删除三个控制器里所有 `ctx.Get<IProjectRegistry>()` 直连**（现 `ProjectsController.cs:29`、`ProjectCommandsController.cs:31`、`RunnerService.cs:79` 中 `RunnerService` 保留其自有读取，见「不做」）。
- **FR-B4**：`RunnerService`（进程启停/检测）保留在 sems 插件内（现设计如此，`028-project-workspace.md:40`），不升格为宿主接缝。

### FR-C sems HTTP 面：补齐生命周期 + 收敛冗余

- **FR-C1**：新增 `POST api/projects`，body `{ root: string, name?: string }`。
  - 200 → `{ success:true, project: ProjectInfo }`
  - 400 → `{ success:false, message }`（目录为空/非法/不存在）
  - 503 → 接缝不可用；类级 `[Authorize("ApiKeyPolicy")]`（铁律 17）。
- **FR-C2**：新增 `DELETE api/projects/{id:int}`。
  - 200 `{success:true}`；404 项目不存在；409 `{success:false, message:"项目有 N 个运行中的命令，请先停止"}`；503 接缝不可用。
- **FR-C3**：新增 `GET api/projects/browse?path=<abs>`（供前端目录选择器）。
  - 省略 `path` → 返回本机盘符列表；
  - 传目录 → 返回 `{ success, path, parent, directories:[{ name, path }] }`（**只列子目录，不列文件、不递归**）；
  - 400 路径非法/不存在；无权限 → 400 带原因；必须 `[Authorize("ApiKeyPolicy")]`。
- **FR-C4**：既有端点（`GET api/projects`、`GET api/projects/count`、`PUT api/projects/{id}`、命令 CRUD、`api/commands/{id}/run|stop`、`api/runs`、`api/runs/check`、`api/runs/{pid}/stop`）**请求/响应形状不变**（仅实现搬到服务层）。
- **FR-C5**：删除 `RunnerController`（`api/runner/*`，`Plugins/Sems/Controllers/RunnerController.cs`）——与 `RunsController`+`ProjectCommandsController` 端点语义完全重复，前端与后端测试均零引用（00 号工件「候选低风险任务」已实证），闭环 `028-project-workspace.md` 已知问题 #1。

### FR-D 经 MCP 中心对外提供工具（13 个 `sems_*`）

- **FR-D1**：新增 `Plugins/Sems/ToolExtensions.cs`，在 `SemsPlugin.Apply` 内 `ToolExtensions.Add(...)` 注册 **13 个工具**（宿主 `ExtensionPointManager` 反射收集 → `IToolRegistry.RegisterTool`）：

  | # | `Name`（全局唯一） | `Id` | 参数（required 加粗） | 返回 `data` |
  | --- | --- | --- | --- | --- |
  | 1 | `sems_list_projects` | `sems.tool.list_projects` | `{}` | `projects[]`（id/root/name/type/tags/source/commandCount） |
  | 2 | `sems_register_project` | `sems.tool.register_project` | **root**, name | `project` |
  | 3 | `sems_update_project` | `sems.tool.update_project` | **id**, name, type, description, tags | `updated:true` |
  | 4 | `sems_remove_project` | `sems.tool.remove_project` | **id** | `removed:true` |
  | 5 | `sems_list_commands` | `sems.tool.list_commands` | **projectId** | `commands[]` |
  | 6 | `sems_add_command` | `sems.tool.add_command` | **projectId**, **name**, **script**, url, sort | `commandId` |
  | 7 | `sems_update_command` | `sems.tool.update_command` | **commandId**, name, script, url, sort | `updated:true` |
  | 8 | `sems_delete_command` | `sems.tool.delete_command` | **commandId** | `deleted:true` |
  | 9 | `sems_list_runs` | `sems.tool.list_runs` | `{}` | `runs[]`（commandId/projectId/projectName/commandName/pid/startedAt/origin） |
  | 10 | `sems_check_runs` | `sems.tool.check_runs` | `{}` | `runs[]`（WMI 检测 + Launched 合并） |
  | 11 | `sems_run_command` | `sems.tool.run_command` | **commandId** | `session`（含 pid） |
  | 12 | `sems_stop_command` | `sems.tool.stop_command` | **commandId** | `stopped:true` |
  | 13 | `sems_stop_run` | `sems.tool.stop_run` | **pid** | `stopped:true` |

- **FR-D2**：统一结果封套：成功 `{"success":true,"data":{…}}`；失败 `{"success":false,"error":"<中文原因>"}`。**任何情况下不抛异常到 registry 外**（工具异常会污染 `universal_tool` 转发结果）。
- **FR-D3**：`ParametersJsonSchema` 必须是合法 JSON 对象、含 `type:"object"` + `properties` + `required`（宿主 `ToolRegistry.ValidateParameters:141-179` 真实按此校验；schema 解析失败的工具会在 `GetToolDefinitions` 被静默丢弃）。
- **FR-D4**（铁律 18 可发现性）：每个工具 `Description` 必须写明 ①做什么 ②参数从哪来（「id 须先经 `sems_list_projects` / `sems_list_commands` 取得」）③破坏性与不可恢复点（`sems_remove_project`/`sems_delete_command`/`sems_stop_run` 明示）④如何发现全部工具（「`universal_tool{tool:"list_tools"}`」）。
- **FR-D5**：工具与 HTTP 端点**共用同一服务实现**（FR-B），McpCenter、宿主 `ToolRegistry`、AIAgent **零改动**。

### FR-E 前端自洽（`Plugins/Sems/web/`）

- **FR-E1**：头部新增「添加项目」按钮 → 打开目录浏览弹层（数据来自 FR-C3）：可输入绝对路径 + 逐级进入子目录 + 「选择此目录」→ `POST api/projects` → 成功后卡片出现、统计更新（点即生效，无需二次保存）。
- **FR-E2**：项目卡片新增「移除」→ `ElMessageBox.confirm` 二次确认，文案必须明示「只删除项目档案与其运行命令记录，**不会删除磁盘文件**」→ `DELETE api/projects/{id}`；失败保留弹窗打印原因（技能 §3.4-2）。
- **FR-E3**：确认/提示编排抽为纯模块 `src/confirmOps.ts`（确认动作由调用方注入，参照 `Plugins/AIAgent/web/src/sessionArchive.ts`），使「用户取消 → 一个请求都不发」可被 vitest 锁死；把现存 `window.confirm`/`window.alert`（`CommandList.vue:150`、`RunPanel.vue:148,163`、`SemsView.vue:137`）迁到该模块。
- **FR-E4**：空态分级（替换 `SemsView.vue:9,35` 的「请前往 AI Agent 页」话术）：
  - 无任何项目 → 「还没有项目。点『添加项目』选择一个目录即可，无需经过其他页面」；
  - 加载失败 → 红色错误态 + 原因 + 「重试」；
  - 有项目但无命令 → 卡片内「还没有运行命令，先添加一条」；
  - 浏览无子目录 → 「该目录下没有子目录」。
- **FR-E5**：根视图标题旁版本徽标（铁律 13）：从 `GET /api/plugin` 解 `.data` 按 `id=="sems"` 取 `version`，小字灰底徽标（参照 `Plugins/ImGateway/web/src/ImGatewayView.vue`）。
- **FR-E6**：滚动容器直接子项 `flex-shrink: 0`（铁律 8），避免窄视口下内容被压扁裁切。
- **FR-E7**：样式只走 `--el-*` 变量 + 现有原生 CSS 体系；不 `import` 宿主模块；`element-plus` 已在 `vite.config.ts` external（已核实）。

### FR-F 测试补齐

- **FR-F1**（后端 xUnit）：
  - 契约：3 参 `Register`（source 写入 / null 走 `ai-agent` / 已存在只刷 `LastActiveAt` 且不覆写 Name）；`Remove`（项目与命令级联消失 / 不存在 id 返回 false / 不触碰磁盘目录）。
  - 服务层：`ProjectService` 全成员含校验分支与接缝 null 降级。
  - 控制器：新增三端点状态码矩阵（200/400/404/409/503）+ 反射断言 sems 全部 `Controllers/` 类带 `[Authorize("ApiKeyPolicy")]`（仿 `McpAdminAuthTests.cs:23-28`）。
  - 工具：13 个工具在 `IToolRegistry` 齐备且无重名丢失；每个 schema 可 `JsonDocument.Parse` 且 `required` 生效（缺参 → `ValidateParameters.IsValid==false`）；`ExecuteAsync` 端到端往返（登记→列→改→删）。
  - 不破坏既有：`HostProjectRegistryTests`（10）/`RunnerServiceTests`（7）/`ProcessMatchTests`（8）/`SemsSharedContractTests`（4）全绿。
- **FR-F2**（前端 vitest）：`confirmOps` 单测——取消路径零请求、确认路径参数正确。
- **FR-F3**（Playwright 插件层 e2e，零 mock，扩 `ForgeSelf.Web/e2e/plugins/sems/sems.spec.ts`）：
  - UI 链：添加项目（用 e2e 临时目录，测试内创建目录、**只创建不删除**）→ 卡片出现 → **刷新页面仍在** → 加命令 → 启动 → 运行面板出现该 PID → 停止 → 移除项目（先取消，断言无 DELETE 请求；再确认，断言卡片消失）。
  - MCP 链：网关 `tools/list` = `universal_tool`；`universal_tool{tool:"list_tools",parameters:{keyword:"sems"}}` 含全部 13 个 `sems_*`；`universal_tool{tool:"sems_list_projects"}` 返回 total ≥ 1 且与 `GET api/projects` 一致；`sems_register_project` 真登记后 UI 可见。
  - 视觉：按 `e2e-testing` Level 3 截图读图（落 `ForgeSelf.Web/screenshots/e2e/sems/`），核对徽标/空态/弹层不破版。
- **FR-F4**（回归面）：`menu-route-consistency.spec.ts`（不改 route，须证明未回归）+ `mcp-center.spec.ts`（工具表格新增 13 行后仍通过；若其断言精确计数则按事实调整该断言并记入偏差）。

## Input

| 输入 | 来源 | 约束 |
| --- | --- | --- |
| `root`（项目目录绝对路径） | sems UI / `POST api/projects` / `sems_register_project` | 必须真实存在的目录；经 `Path.GetFullPath` 归一化（`HostProjectRegistry.cs:50`） |
| `path`（browse 目标） | 目录选择器 | 绝对路径或省略（列盘符）；只读列举 |
| 命令 `{name, script, url, sort}` | UI / 工具 | `name`、`script` 非空（沿用 `ProjectCommandsController.cs:56`） |
| `commandId` / `projectId` / `pid` | 先经 list 类工具/接口取得 | 不接受调用方臆造 id（找不到即明确报错） |
| MCP 请求 | `POST http://127.0.0.1:18889/mcp` + `Authorization: Bearer <token>` | 网关 token 缺失/错误 → 401（`McpGatewayServer.cs:109-126`） |

## Output

- HTTP：既有信封风格（`{ success, total?, projects?|commands?|runs?|session?, message? }`），前端 `http.ts` 已解信封。
- 工具：JSON 字符串 `{"success":bool,"data":{…}|null,"error":string|null}`。
- 数据落盘：宿主库 `Project`/`RunCommand` 行（`~/.forgeself/ForgeSelf.db`）；运行会话仍为内存态（宿主重启清空，既有 NFR）。
- 插件产物：`Plugins/Sems/web/dist/index.js` + `style.css`；`plugin.json` `Version` → `1.1.0`。

## Business Rules

| # | 规则 | 依据 |
| --- | --- | --- |
| BR1 | Root 是项目唯一键；重复登记只刷新活跃时间 | `UX_Project_Root`（`Project.cs:20`）+ FR-A2 |
| BR2 | **手工编辑优先于自动登记**：任何登记动作不得覆写用户已改的 Name/Type/Description/Tags | 修 `HostProjectRegistry.cs:68` |
| BR3 | 登记来源可辨识：sems 内部登记 = `manual`，AIAgent 选目录 = `ai-agent` | `IProjectRegistry.cs:60-61` 注释已预留 |
| BR4 | 有存活 Launched 会话的项目**不可删除**，必须提示先停止；被拒时零数据变更 | Intent SC-3 |
| BR5 | 删除项目 = 删档案行 + 级联删命令行；**永不触碰磁盘文件/目录** | 技能铁律 10 |
| BR6 | browse 只列目录名，不列文件、不递归、不读内容 | 最小信息面 |
| BR7 | MCP 工具不提供「执行任意脚本」：`sems_run_command` 仅能启动**已登记**命令 | 风险收口（见 Error Handling/Risk） |
| BR8 | sems 写路径唯一：控制器与工具都只经 `IProjectService`/`IRunnerService` | FR-B3（一处真相） |
| BR9 | Detected 会话（`commandId==0`）只能通过 `pid` 停止（`sems_stop_run` / `api/runs/{pid}/stop`） | `RunnerService.cs:270` |
| BR10 | 停止外部捕获进程必须文案警告「杀整棵树可能含他人进程」 | 既有 UI 文案 `RunPanel.vue:163` |

## Boundary Conditions

- 接缝不可用（提供方热重载/未 seed）→ HTTP 503、工具 `success:false`、`GetProjects()` 空列表，**不得抛 500**。
- 目录不存在 / 无权限 / 路径非法字符 / UNC 路径 / 超长路径 → 400 + 具体原因（不吞成「失败」）。
- 盘符列表：仅有可枚举驱动器时返回；`DriveInfo.GetDrives()` 异常（无盘/权限）→ 400。
- `id` 不存在、跨项目 commandId、`pid<=0`（`RunnerService.cs:151` 既有拒绝）。
- 重复启动已存活命令 → 409（既有）。
- 命令 script 为空、name 为空 → 400。
- 项目命令很多时 `RemoveProject` 的存活判定必须覆盖全部命令（不能只看第一条）。
- 并发：同 root 并发登记（唯一索引兜底）、工具与 UI 同时改同一命令。
- 宿主重启后运行会话清空（既有 NFR，`028-project-workspace.md:99-101`），本任务不改。
- 视口 1280×720（Playwright 默认）下内容不被裁切（铁律 8）。

## Error Handling

| 场景 | HTTP | 工具 |
| --- | --- | --- |
| 目录不存在/非法 | 400 `{success:false,message}` | `{success:false,error}` |
| 项目/命令不存在 | 404 | `error:"项目不存在：12"` |
| 有存活运行 | 409 | `error` 含「请先停止」 |
| 重复启动 | 409（既有） | 同 |
| 接缝缺失 | 503 | `error:"项目注册表不可用"` |
| WMI 不可用 | 200 + 仅 Launched（既有降级 `RunnerService.cs:279-283`） | 同，`data.note:"WMI 不可用，仅返回面板启动的会话"` |
| 参数缺 required | — | 由 `ToolRegistry.ValidateParameters` 在执行前拒绝 |
| 进程杀不掉（权限） | 404（既有 `RunnerController.cs:65` 语义搬入 `RunsController`） | `success:false` |
- 任何工具内部异常必须被捕获并转成 `success:false`，禁止抛到 `UniversalToolForwarder`（否则外部只看到 JSON-RPC 错误、无业务原因）。

## Compatibility

- **AIAgent 零改动**：2 参 `Register` 保留并委托；`POST /api/project/directory` 行为不变（回归由 `HostProjectRegistryTests` 保证）。
- **McpCenter / 宿主 `ToolRegistry` / 宿主前端零改动**。
- 既有 sems HTTP 端点形状不变（FR-C4）→ 现有 e2e/前端不回归。
- `api/runner/*` 删除（FR-C5）：grep 实证前端与后端测试零引用；属未使用面收敛。**若用户否决，回滚为保留**（03-plan 标为可裁剪项）。
- `ProjectInfo`/`RunCommandInfo` DTO 不加字段 → 前端 `types.ts` 只加可选字段与新 DTO。
- 无 DB schema 变更 → 无迁移、无回滚脚本需求。
- 版本 `1.0.3 → 1.1.0`（新增能力，向后兼容）。
- **副作用需实测**：`McpService.SyncToolsFromToolRegistry()`（`Plugins/McpCenter/Services/McpService.cs:103-140`）会把 13 个新工具镜像进 mcp-center 管理界面工具表格 → 须跑 `mcp-center.spec.ts` 确认无精确计数断言被打破（FR-F4）。

## Non-functional Requirements

- 零新依赖（NuGet/npm）；不新增后台线程、长连接、常驻轮询。
- 本地 `GET api/projects` / `browse` 单层列举 P95 < 500ms（本机磁盘）；`check` 一次 WMI 全量查询（既有）。
- 鉴权：新增三端点全部类级 `[Authorize("ApiKeyPolicy")]`（铁律 17），对外可达另有 MCP 网关 token（两层不互相替代）。
- 日志：登记/删除/启停留 `XTrace` 信息级日志（含插件前缀），便于走查追trace。
- 可观测：工具执行经 `RecordUsageAsync`（`ForgeSelf.Abstractions/ToolFunctionUsageReportingExtensions.cs:18`）计入用量统计，与 TodoTracker 等一致。

## Acceptance Criteria

> 闸门1 用户确认的就是这张清单。

- [ ] AC1 `POST api/projects{root}` → 200 + 新记录 `Source=="manual"`；同 root 重复调用不新增行。
- [ ] AC2 `IProjectRegistry.Register(root, source, out err)` 存在；2 参版仍可用且 AIAgent 未改动（`git diff` 不含 `Plugins/AIAgent/`）。
- [ ] AC3 先改名再触发同 Root 登记 → Name 保持用户所改值（后端测试断言）。
- [ ] AC4 `DELETE api/projects/{id}` → 项目行与其全部命令行消失；磁盘目录仍在（测试断言 `Directory.Exists`）；id 不存在 → 404。
- [ ] AC5 项目有存活会话时删除 → 409 且数据零变更（测试断言行仍在）。
- [ ] AC6 `GET api/projects/browse`：无参列盘符；有参列子目录 + `parent`；不存在路径 → 400；无 token → 401。
- [ ] AC7 `Plugins/Sems/Controllers/` 下每个控制器类都有类级 `[Authorize("ApiKeyPolicy")]`（反射 Theory 测试）。
- [ ] AC8 冗余 `RunnerController` 已删除；`api/runner/*` 全仓零引用（grep 实证并记入 Evidence）。
- [ ] AC9 `sems` 的 13 个工具全部注册进宿主 `IToolRegistry`（测试断言名字集合相等，无静默丢失）。
- [ ] AC10 每个工具 schema 合法且 `required` 被 `ValidateParameters` 真实拒绝缺参调用（参数化测试，13 例）。
- [ ] AC11 每个工具 `Description` 含「id 从哪个 list 工具取」+ 破坏性提示（测试断言含关键字）。
- [ ] AC12 工具端到端：`sems_register_project` → `sems_list_projects` 可见 → `sems_add_command` → `sems_run_command` 拿到 pid → `sems_stop_command` → `sems_remove_project` 成功。
- [ ] AC13 sems UI 不依赖 AIAgent 即可完成：添加 → 刷新持久 → 命令 CRUD → 启动/停止 → 移除（取消路径零请求 + 确认路径生效），e2e 全绿。
- [ ] AC14 经 MCP 网关：`tools/list` = `universal_tool`；`list_tools` keyword=`sems` 含 13 个；`sems_list_projects` 的 total 与 `GET api/projects` 一致，e2e 真实断言（零 mock）。
- [ ] AC15 根视图版本徽标显示 `v1.1.0`；`plugin.json` Version = `1.1.0`。
- [ ] AC16 空态分级四态文案齐备（含替换掉「请前往 AI Agent 页」话术）。
- [ ] AC17 门禁全绿：`dotnet build` 0 error、`dotnet test` 0 fail、sems web 构建产出 `index.js`+`style.css`、宿主 `pnpm run check`+`pnpm run test` 通过。
- [ ] AC18 `menu-route-consistency.spec.ts` 与 `mcp-center.spec.ts` 实跑通过（未回归）。
- [ ] AC19 维护闭环四步证据齐备（门禁 / 插件 e2e / 发布产物可下载且 SHA256 一致 / 浏览器走查截图 + 清理测试数据）。
- [ ] AC20 `docs/02-features/028-project-workspace.md` 同步（新端点/新工具/契约变化/RunnerController 已删）；`05-evidence.md` + `06-review.md` 齐备且 Review = APPROVED。

## Unknown

| 不确定点 | 影响 | 处理方式 |
| --- | --- | --- |
| ~~CI 打包是否已含 `Plugins/Sems/**`（含 `web/dist`）~~ **已在闸门1 前查清（不再 Unknown）** | — | 已含：`scripts/release/build-frontend.ps1:1,4,45-55` 遍历构建每个 `Plugins/<X>/web` → `web/dist`；`ForgeSelf.Api.csproj:26` `<Content Include="Plugins\**\web\dist\**" CopyToOutputDirectory="PreserveNewest" />` 把 dist 带进 publish/发布产物（`dist/` 本身 gitignore，由 CI 构建）。后端 DLL 由 `ForgeSelf.Api.csproj:77` `ProjectReference ..\Plugins\Sems\Sems.csproj` 参与构建。→ 打 tag 即发布 sems，无需改打包脚本 |
| sems e2e 与 mcp-center e2e 并发抢占网关端口 `18889` | MCP 链 e2e 稳定性 | 实跑时观察；冲突则用 `FORGESELF_MCP_GATEWAY_PORT` 指派端口并在同一 worker 串行（`mcp-center.spec.ts:67-71` 已有同做法） |
| 宿主 `exposeSharedDeps` 是否已暴露 `ElMessageBox` 给插件运行时 | FR-E3 能否用 EP 弹窗 | AIAgent 已实证可用（`AiAgentView.vue:116,489`）→ 按同法；若构建期发现缺失，回退为「自绘确认弹层 + 同一 `confirmOps` 编排」，不改宿主 |
| AIAgent 前端调用的 `/api/project/browse-directories` 全仓无后端实现（grep 实证） | 与本任务无关的跨插件缺陷（该弹层会 404） | **不在本任务修**，记入 `TODO.md`（来源:输入1 附带发现） |
