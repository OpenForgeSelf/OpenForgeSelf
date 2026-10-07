# 005 Todo Tracker（待办追踪插件）— 功能需求与设计

> 功能编号：005 ｜ 插件：`Plugins/TodoTracker`（id `todo-tracker`）｜ 当前版本 **1.1.0**
> 状态：已实现（v1.0.0 便签形态 → v1.1.0 「可下发给 agent 的任务台账」，PILOT-054，2026-10-07）
> 关联：插件体系（01-architecture §3.3）、能力接缝 [`host-capability-seams.md` §7](../01-architecture/host-capability-seams.md)（`IAgentDelegation`）、工件 [`docs/ai/pilot/2026-10-07-todo-agent-dispatch/`](../ai/pilot/2026-10-07-todo-agent-dispatch/)

## 1. 功能需求

### 1.1 背景与目标

v1.0.0 的待办是「给人看的便签」（标题 + 备注 + 待处理/已完成）。v1.1.0 把它改造成 **人与 agent 之间的工作交接面**：

1. 一条任务 = 一个 **agent 可直接开工的工作单元**（目标 / 正文 / 允许·禁止范围 / 验收判据 / 验证命令 / 优先级 / 下发对象 / 阶段）；
2. 任务**关联项目**，且路径写法随意（`/d/project` ≡ `D:\project` ≡ `D:/project/` ≡ `/mnt/d/project`）——**一致的路径认为是同一个项目**；
3. 任务正文可由该项目 `docs/ai/pilot/<task-id>/` 的**九件套工件**组装，不必人肉复制粘贴；
4. agent 干完有地方**回写执行记录**：做了什么操作、什么结果、改了哪些文件、验证、风险、遗留、证据、状态流转、耗时、下一步；
5. **一键交给 AgentHub 执行**（经 `IAgentDelegation` 能力接缝，非插件间 HTTP 直连）。

## 2. 设计

### 2.1 实现位置

| 文件 | 职责 |
|------|------|
| `Plugins/TodoTracker/Data/Model.xml` | 列与索引唯一真源（`Todo` 扩 18 列、新增 `TaskExecution` 表） |
| `Plugins/TodoTracker/Data/TodoTrackerTables.cs` | 插件自行建表（铁律 12；`Apply` 内调用，失败 `Warn`） |
| `Plugins/TodoTracker/Data/Entities/*.Biz.cs` | 人工维护的校验与直查库辅助（`Todo.FindByKey`、`TaskExecution.MaxSeqOf/CountByTasks`） |
| `Services/ProjectPathCanonicalizer.cs` | **路径归一 13 条规则**（BR-3）+ `ResolveInside` 防穿越 |
| `Services/TodoStage.cs` | 下发阶段状态机（唯一流转表） |
| `Services/DispatchPayloadBuilder.cs` | 下发提示词 + AgentHub 兼容 JSON 的唯一组装点（纯函数） |
| `Services/FileChangeList.cs` | 「改了哪些文件」归一（camelCase 落库） |
| `Services/TodoProjectService.cs` | 经宿主 `IProjectRegistry` 归一-匹配-登记（每次 `ctx.Get`，不缓存） |
| `Services/ArtifactImportService.cs` | 工件目录列举与正文组装（真实文件读取） |
| `Services/TaskExecutionService.cs` | 执行记录 append-only（状态与记录同批落库） |
| `Services/TodoDispatchService.cs` | 下发 / 领取 / 委派 / 状态回读与回写 |
| `Services/AgentTaskGateway.cs` | `IAgentDelegation` 消费侧（缺席 ⇒ 503 原文） |
| `Services/AgentToolFunctions.cs` | 5 个 agent 工具函数 + `TodoTrackerTables`/回填 |
| `Controllers/{Todos,TodoProjects,TodoArtifacts,TaskExecutions,TodoDispatch}Controller.cs` | 全部带类级 `[Authorize("ApiKeyPolicy")]` |
| `Plugins/TodoTracker/web/**` | **插件自带界面**（v1.1.0 起；宿主 `src/views/TodoView.vue` 等已删除） |

### 2.2 实体（XCode，插件库 `{数据根}/Plugins/todo-tracker/TodoTracker.db`）

- `Todo`：原 8 列 + `TaskKey/ProjectId/ProjectRoot/ProjectPathRaw/Objective/Content/AllowedScope/ForbiddenScope/Acceptance/Verification/Priority/Assignee/Stage/ArtifactRef/DispatchedAt/AgentTaskKey/AgentId/PermissionMode`；索引加 `ProjectId`、`Stage`、`TaskKey`。
- `TaskExecution`（1:N，append-only）：`TodoId/Seq/Actor/Action/Detail/Result/FilesChanged/Verification/Risks/Residuals/Evidence/StageFrom/StageTo/ElapsedMs/BlockReason/NextStep/CreatedAt`。
- 兼容：旧二元 `Status(0/1)` 由 `Stage` 派生（`Stage=Done ⇒ Completed`），**旧出参 8 键与旧端点语义不变**（Home 插件在用）。

### 2.3 阶段状态机（`Stage`）

```
0 Draft → 1 Ready → 2 Dispatched → 3 Running → 5 Review → 6 Done
                       ↕                 ↕           ↓(打回 3)
                     4 Blocked ←────────┘      Done/Cancelled 可重开 → 3/5
   任一非终态 → 7 Cancelled；同态幂等放行
```
非法流转 ⇒ **409 + 列出该态可达目标**；进 `Blocked` 必须带 `blockReason`。界面只按出参 `allowedTargets` 渲染按钮（不在前端抄第二份流转表）。

### 2.4 端点（前缀 `api/todos`，全部需 Bearer token）

| 方法 · 路径 | 用途 |
|------|------|
| `GET /` | 列表：`status/stage/projectId/q/page/pageSize`（旧三参形状继续可用） |
| `POST /` · `PUT /{id}` · `DELETE /{id}` | 建（部分更新：null=不改，空串=清空）/删 |
| `GET /{id}` · `GET /by-key/{taskKey}` | 详情（agent 面用 taskKey） |
| `POST /{id}/complete` · `POST /{id}/reopen` | 旧端点，内部同步 Stage |
| `POST /{id}/stage` · `POST /by-key/{taskKey}/stage` | 阶段流转（409 带可达集） |
| `GET /projects` · `POST /projects/resolve` · `GET /projects/{id}` | 项目清单 / 归一匹配（必要时登记，来源记 `todo-tracker`） |
| `POST /{id}/project` · `DELETE /{id}/project` | 关联 / 解除（**不删宿主项目档案**） |
| `GET /artifact-sets?projectId=&projectPath=` | 列 `docs/ai/pilot/*` 目录与 `NN-*.md` 文件 |
| `POST /{id}/artifacts/import` | 组装正文（正文已存在 ⇒ 409，需 `overwrite:true`） |
| `GET /{id}/dispatch` · `POST /{id}/dispatch` | 下发预览（提示词 + JSON + 缺口）/ 执行下发 |
| `POST /{id}/dispatch-to-agent` | 一键委派（接缝缺席 ⇒ **503**，AgentHub 裁决 ⇒ 400 原文透传） |
| `GET /{id}/agent-status` · `POST /{id}/agent-status/record` | 回读委派状态 / 落成执行记录 |
| `GET /agent/next?assignee=&projectId=` | agent 领取（原子置 Running；无可领 ⇒ **204**） |
| `GET /{id}/records` · `POST /{id}/records` · `POST /by-key/{taskKey}/records` | 执行记录读 / 写（三面同形状） |

AI 工具函数（宿主 `universal_tool` / MCP 可调）：`create_todo`、`list_todos`、`complete_todo`、`get_agent_task`、`claim_agent_task`、`append_task_execution`、`update_task_stage`、`dispatch_task`。

### 2.5 路径归一口径（插件内，不改宿主）

去引号空白 → 折叠重复分隔符（保住 UNC 前导 `\\`）→ `~` 展开 → `/d/x`、`/mnt/d/x` 翻成 `D:\x` → **先判绝对形式再** `Path.GetFullPath`（否则相对路径会被拼上进程 CWD 而"看起来合法"）→ 去 `\\?\` → 去尾分隔符（盘符根保留）→ 盘符大写；`Key = Root.ToUpperInvariant()`，**Key 相等即同一项目**。相对路径、无盘符根、超 500 字符 ⇒ 400 带原因。
已知不覆盖：符号链接 / junction / 8.3 短名 / 网络盘映射（判同一需要 OS 互操作，代价与收益不对称）。

## 3. 使用指南

侧栏「待办事项」→ `/todo`（界面由插件自带 `web/dist/index.js` 远程加载）。
典型链路：新建 → 补四栏 → 填项目路径（任意写法）→「列目录 → 勾工件 → 导入为正文」→「生成提示词」（可复制给任意 agent，或直接「交给 AgentHub 执行」）→「下发」→ agent 用 `by-key/{taskKey}/records` 回报 → 时间线查看 → 阶段推到「待验收」→「标记完成」。

界面口径两条（都是实测出来的）：

- **点即保存，但写请求串行**。文本栏失焦即回写，且只发变化的那一栏（部分更新，`null=不改`、`空串=清空`）。插件前端把**非 GET 请求排成一条队列**（`web/src/http.ts`）：实测连续失焦连发 4 个 PUT 时，四个都落库了、界面却仍显示「还缺：验收判据、验证命令」——因为响应乱序回来，`applyUpdated` 把**较早那次写的整行旧快照**盖回了新状态。串行后最后到达的响应必然是最新状态。
- **按钮的禁用原因必须照实说**。「交给 AgentHub 执行」不可点时，`title` 分四种：预览未生成 / 接缝（agent-hub）不在场 / 四栏不齐 / 可委派；预览出参里的 `agents` 同时渲染成「执行 agent」下拉（`data-test="delegate-agent"`），不留"接口给了字段但界面没入口"的死数据。

## 4. 测试覆盖

| 测试 | 覆盖点 |
|------|--------|
| `ForgeSelf.Api.Tests/Plugins/TodoTracker/ProjectPathCanonicalizerTests.cs` | 归一金样表（4 种写法同 Key）、反例（相对/无盘符/超长/越界）、`ResolveInside` 防穿越 |
| `…/TodoStageMachineTests.cs` | 全边遍历、非法边、终态与旧 Status 派生 |
| `…/DispatchPayloadBuilderTests.cs` | 提示词六块内容、回报契约含 `by-key`、token 只留占位、`FileChangeList` 归一 |
| `…/TodoDispatchFlowTests.cs` | 真库真文件：同路径四写法→一个 ProjectId 且宿主只多一条档案、12,000 字正文不裁短、真实工件导入 + 越界/坏名/未确认覆盖三拒、记录 Seq 递增、并发领取不撞车、权限黑名单、历史行回填幂等 |
| `…/TodoAgentDelegationTests.cs` | 接缝缺席 ⇒ `SeamMissing`；业务拒绝原文透传；**静态守卫：插件源码不得出现直连其他插件的 HTTP 路径** |
| `…/TodoTrackerAuthTests.cs` | 程序集内全部控制器带 `[Authorize("ApiKeyPolicy")]`（新增控制器漏登记即红） |
| `…/TodoTrackerWebAssetTests.cs` | `entry`/导出名/vite 产物名/lockfile/宿主别名禁令 |
| `…/Unit/TodoServiceTests.cs`、`…/Integration/TodosControllerTests.cs` | 旧 CRUD/分页 total/完成重开语义不回归 |
| `ForgeSelf.Web/e2e/plugins/todo-tracker/todo-tracker.spec.ts` | 远程加载 + 徽标、下发主链路（Git-Bash 路径→归一）、执行记录补记与流转、删除双路径、401、Home 兼容形状建单 |
| `ForgeSelf.Web/e2e/todo.spec.ts` | 仅剩首页待办面板（应用层跨插件视角） |

## 5. 已知约束

- 其他插件（QuickLinks/Scheduler/WorkflowEngine/ScriptRunner 等）分页 `total=0` 仍未修（与本功能独立）。
- 路径同一性只在**本插件入口**成立：宿主 `Project.Root` 仍按 `Path.GetFullPath` + 精确匹配登记，AIAgent/sems 若写入怪异写法（如 `\\?\` 前缀、8.3 短名）仍可能形成另一条档案；插件比对时会对双方都归一，故**读取侧**不会误判，登记侧新行仍可能重复（决策见 `not-taken-decisions.md` 2026-10-07 第 1 条）。
- 委派执行需 `agent-hub` 在场且其 `agent` 已注册并启用、`cwd` 在其白名单内，否则一键执行按 400/503 如实失败（不影响手工下发）。
