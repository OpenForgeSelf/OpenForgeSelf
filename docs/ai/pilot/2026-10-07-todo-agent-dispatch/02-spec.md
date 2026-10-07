# Specification

> 阶段：Stage 2｜从真实 Repository Understanding 与 Intent 推导。
> 规则：① 内容与实际项目一致；② 不发明不存在的接口/类/模块（引用处给 file:line）；③ 不确定点显式记 `Unknown`。
> Task ID：PILOT-054

## Functional Requirements

### FR-1 任务即工作单元（下发格式）

一条待办在原有 `Title/Remark/Status/DueDate` 之外，承载以下可填字段（全部落在 `Todo` 表，见 BR-1）：

| 字段 | 语义 | 对应 04-task 模板节 |
| --- | --- | --- |
| `Objective` | 一句话可验证目标 | `## Objective` |
| `Content` | 任务正文（markdown，可由工件组装或手工写） | 01/02/03 正文 |
| `AllowedScope` / `ForbiddenScope` | 允许改 / 禁止改的范围 | `### Allowed` / `### Forbidden` |
| `Acceptance` | 验收判据，逐行一条 | `## Acceptance Criteria` |
| `Verification` | 验证命令，逐行一条 | `## Verification Commands` |
| `Priority` | 1=P1 2=P2 3=P3（对齐 AGENTS.md §0.2 优先级口径） | — |
| `Assignee` | 下发对象（agent 名 / `manual`） | — |
| `Stage` | 下发状态机（BR-4） | — |
| `TaskKey` | 外部键 GUID("N")，agent 侧引用任务用（同 `DelegationTask.Biz.cs:35` 的做法） | — |
| `ProjectId`/`ProjectRoot`/`ProjectPathRaw` | 项目关联（BR-2/BR-3） | — |
| `ArtifactRef` | 来源工件目录（相对项目根） | — |
| `DispatchedAt` | 下发时间 | — |
| `AgentTaskKey`/`AgentId`/`PermissionMode` | 一键交给 AgentHub 后的回填与下次提交默认值（FR-6） | — |

- **FR-1.1** 提供「下发预览」：给定任务产出①可直接粘贴给任意 agent 的 markdown 提示词、②AgentHub 兼容 JSON 载荷、③缺口清单（哪些必填项还空着）。
- **FR-1.2** 提供「下发」动作：校验必填齐备 → `Stage=Dispatched` + `DispatchedAt` + 自动写一条执行记录（`Actor=manual`，Action=「下发」）。
- **FR-1.3** 必填口径：`Title`、`Objective`；`Content`、`Acceptance`、`Verification` 任一为空 ⇒ 下发被拒（400 + 指明缺哪一项）。老数据（只有标题的便签）保持可读可改可删，只是不能「下发」。

### FR-2 项目路径关联与归一

- **FR-2.1** 任务可关联一个项目；输入任何受支持的路径写法（BR-3 归一规则），系统先归一再匹配宿主项目档案（`IProjectRegistry.GetAll()`），命中即复用其 `Id`；未命中则调 `Register(root, "todo-tracker", out error)` 登记后回读。
- **FR-2.2** 「一致的路径认为是同一个项目」：同一目录的不同写法必须落到同一 `ProjectId`，且 `ProjectRoot` 存归一后的根、`ProjectPathRaw` 存用户原始输入（便于回溯 `/d/proj` 这类写法）。
- **FR-2.3** 项目列表端点：返回宿主项目 + 每项目的任务数与未完成数（供界面筛选）。
- **FR-2.4** 列表可按项目筛选；`projectId` 非法（≤0 且非"全部"哨兵）⇒ 400。
- **FR-2.5** 关联可解除（回到未关联），**不得**删除宿主项目档案（越权）。

### FR-3 工件作为任务内容（九件套核心文件）

- **FR-3.1** 给定已关联项目的任务，可列出该项目 `docs/ai/pilot/` 下的工件目录（目录名 + 每个 `NN-*.md` 的文件名与字节数）。
- **FR-3.2** 可选择一个目录 + 勾选其中若干文件（默认勾选核心四件 `01-intent`/`02-spec`/`03-plan`/`04-task`），导入后组装为 `Content` 并回填 `ArtifactRef`。
- **FR-3.3** 组装格式固定（见 BR-5），且导入是**覆盖正文**（界面须给「将覆盖现有正文」的确认，见 §交互）。
- **FR-3.4** 组装结果同时驱动 `Acceptance`：若勾选文件含 `04-task`，从中提取 `- [ ]` 行填入（空则填，非空则提示不覆盖，由用户显式选择覆盖）。

### FR-4 执行记录

一条任务 1:N 执行记录，字段（BR-1 `TaskExecution`）：`Actor`（谁）、`Action`（做了什么操作，一句话）、`Detail`（操作明细）、`Result`（什么结果）、`FilesChanged`（改了哪些文件，JSON 数组 `[{path,change}]`）、`Verification`（跑了什么验证 + 结果）、`Risks`（风险）、`Residuals`（遗留/未做）、`Evidence`（证据路径/链接）、`StageFrom`/`StageTo`（本次导致的状态流转，`-1`=不变）、`ElapsedMs`（耗时）、`BlockReason`（阻塞原因）、`NextStep`（下一步回流入口）、`Seq`（任务内递增）、`CreatedAt`。

- **FR-4.1** 三面写入同一形状：REST（agent/外部）、AI 工具函数（宿主 AIAgent / `universal_tool`）、插件界面（人工补记）。
- **FR-4.2** 记录只增不改不删（append-only 台账）；列表按 `Seq` 升序分页返回。
- **FR-4.3** 记录带 `StageTo` 时，状态流转与记录写入同一次调用完成（避免"改了状态没留痕"）。

### FR-5 Agent 侧领取与回报

- **FR-5.1** `by-key` 读取：给定 `TaskKey` 返回任务全文（正文/判据/命令/项目根/已有记录摘要）。
- **FR-5.2** 领取：`next` 返回一条 `Stage=Dispatched` 的任务并原子置 `Running`（并写执行记录 `Action=领取`）；无可领时返回明确空态而非 404 报错。
- **FR-5.3** 改状态：按 BR-4 合法集流转，非法流转 409 + 列出可达目标。

### FR-6 一键交给 AgentHub 执行

- **FR-6.1** 任务（`Stage` ∈ {Ready, Dispatched}）可一键提交给 AgentHub：经 **`ForgeSelf.Abstractions` 新增能力接缝 `IAgentDelegation`**（提供方 = AgentHub，在 `Apply` 里 `ctx.Register<IAgentDelegation>()`；消费方 = TodoTracker 每次 `ctx.Get<>()`），**禁止**插件间直连 HTTP（architecture-design 铁律 2/3）。
- **FR-6.2** 提交内容 = FR-1.1 生成的 prompt + `cwd`=任务 `ProjectRoot` + `permissionMode`（默认 `read-only`，可选 `workspace-write`/`accept-edits`；黑名单值一律拒绝）+ `createdBy="todo-tracker"`。
- **FR-6.3** 成功后回填 `AgentTaskKey`/`AgentId`、置 `Stage=Running`、写一条执行记录（`Actor=agent-hub`，`Detail` 含 taskKey）。
- **FR-6.4** 接缝缺席（未装/未启用 agent-hub）⇒ **503** + 文案「未检测到 agent-hub 委派能力」，不得静默或伪装成功；AgentHub 的业务拒绝（cwd 白名单、agent 停用等）以 400 原文透传。
- **FR-6.5** 状态回读：`GET .../agent-status` 经接缝查 `taskKey` 当前状态（`Queued/Running/…/Succeeded/Failed/…` + `resultText` 摘要）；`POST .../agent-status/record` 把该结果落成一条执行记录（`Result`/`FilesChanged` 从 `artifactsJson` 尽量解析，解不出则原样放 `Detail`）。

### FR-7 界面（插件自带 `web/`）

- **FR-7.1** 列表：标题/优先级/阶段/项目名/到期/记录数；筛选 = 阶段 + 项目 + 关键字；分页保持 `total` 正确。
- **FR-7.2** 详情（抽屉或分栏）：工作单元各字段可编辑；「下发预览」面板显示将发出的 prompt 与 JSON，一键复制；操作区 = 下发 / 一键交给 AgentHub / 改状态 / 导入工件。
- **FR-7.3** 执行记录时间线倒序展示，含补记表单；`Stage` 变化有视觉区分。
- **FR-7.4** 版本徽标（铁律 13）；破坏性/状态突变动作二次确认（§3.2 约定，确认编排写成可单测纯函数）。
- **FR-7.5** 迁移：宿主 `src/` 不再承载该页面（`plugin.json.frontend.entry = "web/dist/index.js"`，`route` 保持 `/todo`）。

### FR-8 安全与规范闭合

- **FR-8.1** `api/todos` 全部控制器加类级 `[Authorize("ApiKeyPolicy")]`，并加反射守卫用例（照 `McpAdminAuthTests` 形状：断言每个控制器类型都带该策略）。
- **FR-8.2** 插件自行建表：新增 `Data/TodoTrackerTables.cs`（`EntityFactory.InitConnection(ConnName)` + `DAL.Create(ConnName).Db.ServerVersion` 探活，失败 `XTrace.Log.Warn` 不静默）。
- **FR-8.3** 菜单只声明一处：撤销 `TodoTrackerPlugin.cs:35-49` 的 `IMenuExtension`（与 `plugin.json.frontend` 并存即违铁律 19②）。

## Input

| 入口 | 形状 | 依据/约束 |
| --- | --- | --- |
| `POST /api/todos` | 原 `{title,remark?,dueDate?}` **不变**（Home 插件在用：`Plugins/Home/web/src/homeStore.ts:145`）+ 新增可选 `{projectId?,projectPath?,objective?,content?,priority?,assignee?,allowedScope?,forbiddenScope?,acceptance?,verification?,stage?}` | 向后兼容见 Compatibility |
| `PUT /api/todos/{id}` | 原 `{title?,remark?,dueDate?}` + 同上可选字段（部分更新：谁变传谁） | 铁律：新增防覆盖与改值不得同通道（plugin-development §C） |
| `GET /api/todos` | `?status=&projectId=&stage=&q=&page=&pageSize=` | 分页 `RetrieveTotalCount=true`（`TodoService.cs:37` 既有做法） |
| `POST /api/todos/projects/resolve` | `{path:"/d/project"}` | 归一见 BR-3 |
| `GET /api/todos/projects` | — | 宿主注册表 + 任务计数 |
| `PUT /api/todos/{id}/project` | `{path}` 或 `{projectId}` | 二者其一，都给则校验一致 |
| `DELETE /api/todos/{id}/project` | — | 只解除关联 |
| `GET /api/todos/artifact-sets?projectId=&dir=` | 无 dir = 列目录；带 dir = 列该目录 `.md` 文件 | 仅 `<root>/docs/ai/pilot/` 下 |
| `POST /api/todos/{id}/artifacts/import` | `{dir:"2026-10-07-todo-agent-dispatch",files:["01-intent.md","04-task.md"],overwrite:true}` | 越界/非 `.md`/超限 ⇒ 400（BR-5） |
| `GET /api/todos/{id}/dispatch` | — | 下发预览 |
| `POST /api/todos/{id}/dispatch` | `{assignee?}` | 必填齐备才 200 |
| `POST /api/todos/{id}/dispatch-to-agent` | `{agentId?,permissionMode?}` | 接缝缺席 503（FR-6.4） |
| `GET /api/todos/{id}/agent-status` / `POST .../agent-status/record` | — | FR-6.5 |
| `GET /api/todos/agent/next?assignee=&projectId=` | — | FR-5.2（原子领取） |
| `GET /api/todos/by-key/{taskKey}` | — | FR-5.1 |
| `POST /api/todos/by-key/{taskKey}/records` | `{actor,action,detail?,result?,filesChanged?,verification?,risks?,residuals?,evidence?,stageTo?,elapsedMs?,blockReason?,nextStep?}` | FR-4.1 |
| `POST /api/todos/by-key/{taskKey}/stage` \| `POST /api/todos/{id}/stage` | `{stage,reason?}` | BR-4 合法集 |
| `GET /api/todos/{id}/records?page=&pageSize=` | — | FR-4.2 |
| AI 工具函数入参 | JSON 字符串参数（同 `ToolExtensions.cs:50-65` 现有解析方式） | 见 Output-2 |

**真实样例（铁律 21：外部文本/输入必须由真实产物驱动，不得只测我设想过的形状）**：
- 工件导入夹具用**本仓库真实目录** `docs/ai/pilot/2026-10-07-todo-agent-dispatch/`（00-07 八件真实存在）与 `docs/ai/pilot/sems-selfcontained-mcp-tools/`（无日期前缀的历史目录，验证目录名列举不挑格式）；
- 路径归一金样表来自本机真实存在目录（含本 worktree 路径的 4 种写法）+ 反例；
- agent 回报样例 = 由 e2e/单测按 `POST by-key/../records` 契约真实写入再读回；对外部 CLI agent 的实际回报文本暂无样例 ⇒ `Unknown` U-1。

## Output

### Output-1 REST（统一 `ApiResponse<T>` 封套，`ForgeSelf.Abstractions/ApiResponse.cs:32-57`）

```jsonc
// GET /api/todos/{id}
{ "code":0, "success":true, "message":"获取待办详情成功",
  "data": {
    "id":12, "taskKey":"3f2b…", "title":"…", "remark":null,
    "status":"Pending", "stage":"Dispatched", "priority":1, "assignee":"qoder",
    "objective":"…", "content":"## 来源工件：…", "acceptance":"- [ ] …", "verification":"dotnet test …",
    "allowedScope":"…", "forbiddenScope":"…",
    "projectId":3, "projectRoot":"D:\\project", "projectPathRaw":"/d/project",
    "artifactRef":"docs/ai/pilot/2026-10-07-todo-agent-dispatch",
    "dueDate":null, "dispatchedAt":"2026-10-07T10:00:00",
    "agentTaskKey":null, "agentId":0, "permissionMode":"read-only",
    "createdAt":"…", "updatedAt":"…", "completedAt":null,
    "recordCount":4
  } }
// GET /api/todos/{id}/dispatch → { promptMarkdown, payloadJson, missing:[“verification”] }
// POST …/dispatch-to-agent → { taskKey, agentId, agentName, status:"Queued", cwd }
// GET …/agent-status → { taskKey, status, exitCode, elapsedMs, resultSummary, terminal:bool }
```
所有出参键 **camelCase**（默认 ASP.NET Core Web JSON 选项，与 `DelegationTaskDto` 实测一致）。

### Output-2 AI 工具函数（`{success,data|error}` 封套，同 `ToolExtensions.cs:69,106`）

新增 6 个（保留 3 个既有）：`create_agent_task`、`list_agent_tasks`、`get_agent_task`、`claim_agent_task`、`append_task_execution`、`update_task_stage`。每个 description 必须写明必填/可选与取值枚举（铁律 18：调用方据 description 即可知道怎么传参）。

### Output-3 下发提示词（`promptMarkdown`，人复制给任何 agent 即可开工）

```
# 任务 <taskKey>：<title>
项目路径：<projectRoot>（可用写法示例：<projectPathRaw>）
## Objective …
## 任务内容（来自工件 …）…
## 允许改动范围 … / ## 禁止改动范围 …
## 验收判据（逐条勾选）… - [ ] …
## 验证命令 …
## 完成后必须回报 …
  POST http://<host>/api/todos/by-key/<taskKey>/records   Authorization: Bearer <token>
  body: {"actor":"<agent>","action":"…","result":"…","filesChanged":[{"path":"…","change":"M"}],"verification":"…","risks":"…","residuals":"…","stageTo":"Review"}
  或（已装 AIAgent 时）工具函数 append_task_execution {…}
```

## Business Rules

### BR-1 数据模型（`Plugins/TodoTracker/Data/Model.xml` 为唯一真源，铁律 9）

`Todo` 在现有 8 列（`Model.xml:27-34`）后追加 18 列（长度/默认值写全，SQLite 实际不裁长但数据字典要诚实）：

| 列 | DataType | Length | Default | 说明 |
| --- | --- | --- | --- | --- |
| TaskKey | String | 32 | "" | GUID("N")，插入时补 |
| ProjectId | Int32 | | 0 | 宿主项目 Id，0=未关联 |
| ProjectRoot | String | 500 | "" | 归一后的项目根 |
| ProjectPathRaw | String | 500 | "" | 用户原始写法 |
| Objective | String | 500 | "" | 可验证目标 |
| Content | String | 262144 | "" | 任务正文（工件组装） |
| AllowedScope | String | 2000 | "" | |
| ForbiddenScope | String | 2000 | "" | |
| Acceptance | String | 4000 | "" | 每行一条 |
| Verification | String | 2000 | "" | 每行一条 |
| Priority | Int32 | | 1 | 1=P1 2=P2 3=P3 |
| Assignee | String | 100 | "" | agent 名 / manual |
| Stage | Int32 | | 0 | 见 BR-4 |
| ArtifactRef | String | 500 | "" | 相对项目根 |
| DispatchedAt | DateTime | | | MinValue=未下发 |
| AgentTaskKey | String | 64 | "" | AgentHub taskKey |
| AgentId | Int32 | | 0 | 0=自动选路 |
| PermissionMode | String | 32 | read-only | |

索引：`Status`、`CreatedAt`（既有）+ `ProjectId`、`Stage`、`TaskKey`。

新表 `TaskExecution`（待办执行记录）：`Id`(identity PK)、`TodoId`(Int32, 非空, 索引)、`Seq`(Int32)、`Actor`(String 100)、`Action`(String 300 非空)、`Detail`(String 8000)、`Result`(String 8000)、`FilesChanged`(String 8000 JSON)、`Verification`(String 4000)、`Risks`(String 2000)、`Residuals`(String 2000)、`Evidence`(String 1000)、`StageFrom`(Int32, -1=不变)、`StageTo`(Int32, -1=不变)、`ElapsedMs`(Int32)、`BlockReason`(String 1000)、`NextStep`(String 1000)、`CreatedAt`(DateTime, 索引)。索引：`(TodoId,Seq)`、`CreatedAt`。

长文本列形状沿用本仓既有写法（`Plugins/AgentHub/Data/Model.xml:101,108` `Length="8000"`；`Plugins/MemorySystem/Data/Model.xml:48` `Length="4000"`），`Content` 因要装整套工件而放大到 262144，并由单测证明不被裁（AC-3）。

### BR-2 项目身份与关联

- 项目身份 = 宿主 `IProjectRegistry` 的 `ProjectInfo.Id`（**不在插件内再造一张项目表**，避免第二份真相）。
- `ProjectRoot` 是关联时刻的归一根快照：宿主项目被删后仍可用于解释"任务发生在哪"，但 `ProjectId` 匹配不到 ⇒ 界面显示「项目档案已移除」并允许重新关联。
- 一任务至多一项目；一项目至多 N 任务。

### BR-3 路径归一规则（插件内实现，按序执行；每条都有对账用例）

| # | 规则 | 例 |
| --- | --- | --- |
| 1 | 去首尾空白、去包裹的 `"` / `'` / `` ` ``；连续分隔符折叠 | `"D:\\proj\\"` → `D:\proj\` |
| 2 | 前导 `~/` 或 `/~/` → `Environment.GetFolderPath(UserProfile)` | `~/code/x` → `C:\Users\me\code\x` |
| 3 | MSYS/Git-Bash 盘符式 `/<字母>/…` → `<字母>:\…` | `/d/project` → `D:\project` |
| 4 | WSL 式 `/mnt/<字母>/…` → `<字母>:\…` | `/mnt/d/project` → `D:\project` |
| 5 | UNC `\\server\share…` 原样保留，不做 3/4 | `\\nas\work\x` |
| 6 | 分隔符统一为平台分隔符（Windows 下 `/`→`\`），再 `Path.GetFullPath` 折叠 `.`/`..` | `D:\a\.\b\..\c` → `D:\a\c` |
| 7 | 去掉 `\\?\` 扩展前缀；去尾部分隔符（盘符根 `D:\` 保留反斜杠） | `D:\proj\` → `D:\proj` |
| 8 | 盘符大写 | `d:\proj` → `D:\proj` |
| 9 | canonical key = 归一根 `ToUpperInvariant`（Windows 大小写不敏感）⇒ **key 相等即同一项目** | `D:\PROJ` == `D:\proj` |
| 10 | 与宿主比对：对 `GetAll()` 每条 `Root` 跑规则 1-9 后比 key（不改宿主、不做迁移） | — |
| 11 | 相对路径（既不带盘符也不以 `/` 开头）⇒ 拒绝 400，绝不按进程 CWD 猜 | `proj\x` ⇒ 400 |
| 12 | 目录必须存在（与宿主 `Register` 同口径），否则 400「目录不存在：<归一根>」 | — |
| 13 | 归一后长度 > 500 ⇒ 400 | — |

不在本轮解决（写入 Known Limitations）：符号链接/junction/8.3 短名/映射网络驱动器的同一性（需 `GetFinalPathNameByHandle`，属 OS 互操作，代价与收益不对称）。

### BR-4 状态机（`Stage`）

```
0 Draft（草稿）→ 1 Ready（就绪）→ 2 Dispatched（已下发）→ 3 Running（执行中）
                                          ↑  ↓                ↓  ↓
                                    4 Blocked（阻塞，需 BlockReason）
                                                             3 → 5 Review（待验收）→ 6 Done
                                       任一非终态 → 7 Cancelled
```
- 合法边：Draft→Ready；Ready→Dispatched|Cancelled；Dispatched→Running|Blocked|Ready（撤回）|Cancelled；Running→Review|Blocked|Cancelled|Done；Blocked→Ready|Running|Cancelled（解冻需清 `BlockReason`）；Review→Done|Running（打回）；**Done/Cancelled 是终态**，只允许 →Running/Review（重开，AGENTS 口径下 reopen）。
- `Status`（0/1，Home 面板与既有 e2e 在用）派生同步：`Stage>=6 ⇒ Completed`，否则 `Pending`；`POST /{id}/complete` ⇒ `Stage=6`+`CompletedAt`；`POST /{id}/reopen` ⇒ `Stage=0`（草稿）+`CompletedAt=MinValue`，两者行为与旧语义保持（不破坏兼容）。
- 非法流转 ⇒ 409 + `message` 列出该状态的可达目标集合。
- 「领取」只作用于 `Stage=Dispatched`；并发领取用「先查后条件更新 + 复核影响行数」保证至多一人拿到（SQLite 单写者下足够；不允许依赖实体缓存判断，铁律 11）。

### BR-5 工件导入与组装

- 允许集：`<ProjectRoot>/docs/ai/pilot/<dir>/<NN-xxx.md>`；`dir` 必须是 `docs/ai/pilot` 的**直接子目录名**（不含 `/`、`\`、`..`），文件必须匹配 `^\d{2}-.+\.md$`。
- 防穿越：拼接后 `Path.GetFullPath`，必须 `StartsWith(root + sep, OrdinalIgnoreCase)`，否则 400。
- 限额：单文件 ≤ 200 KB；单次导入合计 ≤ 200,000 字符（超限 400 + 提示勾选更少文件）。
- 组装格式（`Content`）：
  ```
  ## 来源工件：<dir>（项目 <projectRoot>）
  导入时间：<now>；文件：01-intent.md、04-task.md

  ---
  ### 01-intent.md
  <原文>

  ---
  ### 04-task.md
  <原文>
  ```
- 缺文件/目录不存在 ⇒ 400 并把服务端 `reason` 原文带出（plugin-development §C：空态文案要能自证成因）。
- 读盘异常 ⇒ **不伪装 400**，冒泡 500（铁律 §C）。

### BR-6 一键交给 AgentHub

- 提供方 = AgentHub：`Apply` 内 eager 构造 `AgentDelegationProvider` 并 `ctx.Register<IAgentDelegation>(provider)`（同 `AIAgentPlugin.cs:68` 的形状；`ctx.Register` 的 effect 随插件卸载自动摘除，`IContext.cs` 注释）。
- 消费方 = TodoTracker：**每次** `ctx.Get<IAgentDelegation>()`，禁止缓存为字段（architecture-design 铁律 4 + `IProjectRegistry.cs:11` 同一告诫）。
- `permissionMode` 只接受 `read-only|workspace-write|accept-edits`，其余（含 `yolo`/`danger-full-access`/`dangerously-skip-permissions`）在**进接缝之前**由本插件拒绝 400（与 AgentHub `AgentPolicy.cs:56` 黑名单同口径，双层不互相替代）。
- 提交失败（含 AgentHub 的 400 原因）⇒ 本任务 `Stage` 不变、**不写**成功记录，错误原文回给用户。
- 提交成功但回填写库失败 ⇒ 返回体必须带 `taskKey` 并明确告警（宁可让用户看到"已入队但台账未回填"，不可吞）。

### BR-7 安全

- 所有本插件控制器类级 `[Authorize("ApiKeyPolicy")]`（宿主无全局鉴权中间件，逐控制器显式 —— 铁律 17 依据）。
- 执行记录与任务正文**不得**写入 API token、密钥明文；下发 prompt 里 token 一律写成占位 `<token>`。

## Boundary Conditions

| 场景 | 期望 |
| --- | --- |
| `TaskKey` 为空的历史行（本次改动前插入） | 插件启动一次性补键（只改空值行，幂等，`XTrace` 记数）；读路径遇空键不报错 |
| 未关联项目的任务点「导入工件」 | 400「任务尚未关联项目」，界面按钮 disable + 提示 |
| 项目根当前不可达（磁盘没挂/目录被移走） | 工件列表与导入 ⇒ 400「项目目录不可达：<root>」；任务本身仍可读可改 |
| `pageSize>100` / `<1` | 沿用 `TodoService.cs:20-22`：钳到 `[1,100]`，默认 20 |
| `stage` 传中文名与数字（`Running` / `3`） | 两者都接受；显式传空串 ⇒ 400（口径同 plugin-development §C「未传可默认，显式空是客户端错误」） |
| 同一目录并发导入 | 覆盖语义：最后一次成功导入即最终 `Content`，`ArtifactRef` 同步 |
| 执行记录 `Seq` 竞争 | 直查 `TaskExecution.FindAll(TodoId==x)` 取 max+1（禁读实体缓存，铁律 11） |
| `Content` 长度 = 0 | 允许保存（草稿态），但下发/一键执行被拒 |
| `docs/ai/pilot/` 在项目里不存在 | 目录列表返回空集合 + 文案，不是 404 |
| 单个 `.md` 读失败（占用/权限） | 500 冒泡，`message` 带文件名 |
| AgentHub 存在但一个 agent 都没注册 | 400 原文透传（AgentHub 实测文案「没有可用的 agent 候选（检查启用状态与交互口）」，`DelegationRuntime.cs:249`） |
| `IAgentDelegation` 缺席 | 503 + FR-6.4 文案；界面按钮置灰并说明「需启用 agent-hub」 |
| 无 token 调 `api/todos` | 401（FR-8.1 生效的判据） |
| Home 插件旧调用 `POST /api/todos {title,remark}` | 200 + 正常建行（兼容性守卫用例） |

## Error Handling

- 服务层：入参/业务校验一律抛 `ArgumentException`/`ArgumentOutOfRangeException` ⇒ 控制器 400（沿用 `TodosController.cs:103-107` 现有映射）。
- 未找到 ⇒ 404（`ApiResponse.Error(...,404)`，形状同 `:61`）。
- 状态机非法 ⇒ 409；接缝缺席 ⇒ 503；越权/鉴权交给策略（401/403）。
- **其它异常不捕获**（冒泡 500），禁止把数据访问故障伪装成"参数错"（plugin-development §C）。
- 建表失败 ⇒ `XTrace.Log.Warn` 且**绝不静默吞**（铁律 12）。
- 每个失败响应必须带可定位原因（含服务端 reason 原文），界面空态文案自证成因。

## Compatibility

| 既有消费方 | 兼容策略 |
| --- | --- |
| `Plugins/Home/web/src/homeStore.ts:145,156,175`（`POST /api/todos`、`GET ?status=Pending&page=1&pageSize=20`、`/{id}/complete`、`/{id}/reopen`） | **不改 Home**；出参新增字段只增不减，`TodoItem` 既有键（`ForgeSelf.Web/src/types/todo.ts:7-16` 同款）语义不变 |
| 应用层 e2e `ForgeSelf.Web/e2e/todo.spec.ts`（370 行：`/todo` CRUD/过滤 + 首页面板 + `:327` `toHaveURL(/\/todo$/)`）、`e2e/app.spec.ts:51-53`（`.todo-panel`）、`e2e/spa-fallback.spec.ts:38,85-89` | 迁界面后 `/todo` 仍可达（`plugin.json.frontend.route` 保持）；`todo.spec.ts` 页面部分整体替换为 `e2e/plugins/todo-tracker/`，首页面板部分保留在应用层 |
| `ForgeSelf.Web/src/router/__tests__/dynamicPlugins.test.ts:62-82,139-153`（TodoView 回退分支与 manifest 夹具） | 删除 `case 'TodoView'` 后同步改为「entry 型插件」断言 |
| `ForgeSelf.Web/scripts/check-features.mjs:92-120`（幽灵页/孤儿门禁） | `data/features.ts:305-318` 去掉 `signals.views:['TodoView']`，保留条目并注释「视图已迁移到插件」（§3.3 步骤 5） |
| `ForgeSelf.Api.Tests/XCodeConfigTests.cs:147,151,194-200`（钉 `todo-tracker` id、扫 `Plugins/**/plugin.json`） | 只加 `entry`，`Id`/`ConnName` 不动 ⇒ 断言不受影响 |
| AgentHub（提供方） | 只**新增** `ctx.Register<IAgentDelegation>` + 适配器类；不改其控制器、状态机、DTO |
| 数据库（插件库 `TodoTracker.db`） | `Todo` 新列全部带默认值，`Meta.CreateTable` 幂等补列；新表由 `TodoTrackerTables.EnsureCreated()` 建；**不做破坏性变更、不删列、不删数据**（铁律 10：测试库只建不删） |

## Non-functional Requirements

- **性能**：列表查询不得逐行读工件或逐行查 `TaskExecution` 计数；`recordCount` 用一次分组查询（`GroupBy TodoId`）批量补齐。
- **真跑**：所有 AC 由 `dotnet test` / `pnpm run check` / `pnpm run test` / `npx vue-tsc -b` / 插件 `pnpm build` / 插件层 e2e 判定；结论标 Verified/Inferred/Unknown（AGENTS §10.1）。
- **门禁档位**：因碰 `ForgeSelf.Abstractions` 与 `Plugins/AgentHub` ⇒ **中档**全量 `dotnet test`；`plugin.json.frontend` 变更 ⇒ e2e **中档**（含 `menu-route-consistency.spec.ts`）；未碰 `global-setup.ts`/config/fixtures ⇒ 不跑深档（跑前对基线，新增红才是我的）。
- **可回滚**：单 commit 原子；变更前确认 HEAD 可恢复点（AGENTS §4.1）；新表新列不动老列 ⇒ 回滚代码即回滚行为（多余列留存但无人读写）。
- **发布链**：`Plugins/TodoTracker/web/` 必须带 `pnpm-lock.yaml` + `pnpm-workspace.yaml`（铁律 20，CI `build-frontend.ps1` 用 `--frozen-lockfile` 发现不了本地"自建目录能过"的假绿）。

## Acceptance Criteria

> 闸门1 用户确认的就是这一节。每条都可由命令输出判定。

- [ ] AC-1 `Data/Model.xml` 为唯一真源：`Todo` 新增 BR-1 的 18 列 + `TaskExecution` 表；`xcode Model.xml` 重新生成后 `BindColumn` 集合与 Model.xml 逐列对账无漂移；`*.Biz.cs` 未被覆写。
- [ ] AC-2 `Data/TodoTrackerTables.cs` 在场（`ConnName` + `EntityTypes` + `EnsureCreated()`），`Apply` 调用并在失败时 `Warn`；`dotnet build` 后宿主产物 `bin/Debug/net10.0-windows/Plugins/TodoTracker/TodoTracker.dll` 存在且与插件产物 md5 相同（铁律 12b 判据）。
- [ ] AC-3 后端单测：写入 `Content` 12,000 字符 ⇒ 读回等长不截断；`TaskExecution` 追加 3 条后 `Seq` = 1/2/3 且直查库不依赖缓存。
- [ ] AC-4 路径归一金样表测试：`/d/p`、`D:\p`、`D:/p/`、`/mnt/d/p`、`D:\P`（大小写）→ 同一 canonical key；`/d/p` vs `/d/p2`、UNC、`~/x`、`D:\a\.\b\..\c`、相对路径拒绝、>500 拒绝各一条 ⇒ 断言逐条通过。
- [ ] AC-5 同一路径的两种写法关联同一任务 ⇒ `ProjectId` 相同，且宿主项目表**未新增第二行**（`GetAll()` 计数不变）。
- [ ] AC-6 工件导入用真实目录 `docs/ai/pilot/2026-10-07-todo-agent-dispatch/`（≥2 个文件）⇒ `Content` 含两个 `### <file>` 段且字节可回查；`dir="../Windows"`、`files:["00-x.txt"]`、`files:["../../../../etc/passwd"]` 各 ⇒ 400。
- [ ] AC-7 下发预览：必填齐备的任务返回 `promptMarkdown`（含项目根、taskKey、验收判据、回报契约）；缺 `verification` 的任务 ⇒ `POST /dispatch` 400 且 `missing` 点名该字段。
- [ ] AC-8 一键执行：`ctx.Get<IAgentDelegation>()` 提交成功 ⇒ 回填 `AgentTaskKey` 且 `Stage=Running`，接缝缺席路径单测断言 503；断言本插件源码内**不存在** `agent-hub` 字面 URL 直连（静态守卫用例，剥注释后匹配）。
- [ ] AC-9 权限模式：`permissionMode:"yolo"` ⇒ 400（进接缝前拒绝）；`read-only`/`workspace-write`/`accept-edits` ⇒ 通过本层校验。
- [ ] AC-10 状态机：合法边逐条通过；`Done→Ready` 等非法边 ⇒ 409 且响应列出可达目标；`complete`/`reopen` 旧语义保持（`Status` 与 `Stage` 一致）。
- [ ] AC-11 领取原子性：两次并发 `next` 不得返回同一任务（用例断言拿到的是不同 taskKey 或第二条为空态）。
- [ ] AC-12 兼容：`POST /api/todos {title,remark}`（Home 形状）⇒ 200 且 `GET ?status=Pending&pageSize=20` 的 `total` 正确；`types/todo.ts` 既有键仍全部在出参中。
- [ ] AC-13 鉴权：`api/todos` 全部控制器带 `[Authorize("ApiKeyPolicy")]`（反射守卫用例在场且经 MUTATION 探针证明它会变红）。
- [ ] AC-14 前端归位：`plugin.json.frontend.entry="web/dist/index.js"` 且 `views[0]` == 导出名；`dist/index.js`+`style.css` 存在；`grep 'from "vue-router"' dist/index.js` 命中（external 未漏声明）；`pnpm install --frozen-lockfile && pnpm build` 在插件目录内按 CI 同参数通过。
- [ ] AC-15 宿主清理：`ForgeSelf.Web/src/**` 无 `TodoView`/`stores/todo`/`services/todoApi`/`components/todo` 残留；`dynamicPlugins.ts` 无 `case 'TodoView'`；`pnpm run check`、`pnpm run test`、`npx vue-tsc -b` 三项绿；`node ForgeSelf.Web/scripts/check-features.mjs` 绿。
- [ ] AC-16 插件层 e2e：`e2e/plugins/todo-tracker/todo-tracker.spec.ts` 在隔离宿主跑绿（零 mock，真实后端），覆盖：新建任务→关联项目（用 `/d/…` 写法）→导入工件→下发预览→人工补记执行记录→状态流转→版本徽标；删除/改状态类动作各测「取消 + 确认」两条路径。
- [ ] AC-17 视觉：Level 3 截图读图清单逐项核（图标/间距/颜色/留白/对齐/遮挡/溢出/数值自洽/版本徽标），截图落 `ForgeSelf.Web/screenshots/e2e/todo-tracker/`，**读图后**在 Evidence 记录结论。
- [ ] AC-18 全量后端：`dotnet test` 与基线对账 —— 通过数 += 我新增用例数，失败数不增加。
- [ ] AC-19 文档：`docs/02-features/005-todo-tracker.md` 更新（新端点/契约/状态机/版本）；ADR 或 `docs/01-architecture/host-capability-seams.md` 增补 `IAgentDelegation` 条目；`docs/07-decisions/not-taken-decisions.md` 登记不做项；`plugin.json.Version` 升级（1.0.0 → 1.1.0）。
- [ ] AC-20 五步闭环：门禁 → 插件层 e2e → 发布（打 tag 或本地目录更新源，须用户批准提交）→ 隔离实例走查 → 运行实例只读复验（用户启用新版本后）。缺一不得报完成。

## Unknown

| 不确定点 | 影响 | 处理方式 |
| --- | --- | --- |
| U-1 外部 CLI agent 实际回报的执行记录文本长什么样（是否含围栏、是否整段散文） | 只影响「解析回报文本填字段」这类便利功能 | **保守假设并标注**：本批只做**结构化 JSON 回报**（REST/工具函数），不做自由文本解析；铁律 21 的"裸发"风险因此不在本批路径上。若要加解析，先向用户要一段真实回报原文 |
| U-2 用户运行实例（:51888）里 AgentHub 是否已注册并启用至少一个 agent | 「一键交给 AgentHub 执行」在真实实例上可能必然 400 | 第⑤步运行实例只读复验时实测（只读：`GET /api/agent-hub/agents?enabledOnly=true`）；不为此改判据，实测结果写进 Evidence |
| U-3 「九件套核心内容文件」到底哪几件算核心 | 决定默认勾选集合 | **默认 01-intent/02-spec/03-plan/04-task**（下发即"为什么/做什么/怎么做/工作单元"），界面可任意改勾；待用户在闸门1 一并拍板 |
| U-4 宿主 `Project` 表里可能已存在 `/d/...` 被错误登记成的 `C:\d\...` 历史脏行 | 旧脏行不会被自动合并（本批不改宿主、不做数据迁移） | 记录为 Known Limitation；界面按 canonical key 匹配时，脏行不会命中 ⇒ 用户重新关联即归位；不做删除（铁律 10） |
| U-5 待办与 AgentHub 任务的成本关联（`ITurnTelemetryQuery` 已有会话遥测接缝） | 影响"这条任务的 token 成本"这类延伸能力 | 本批**不做**，登记 not-taken-decisions（触发条件：需要按任务算成本时） |
