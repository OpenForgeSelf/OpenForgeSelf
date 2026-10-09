# Repository Understanding

> 阶段：Stage 0｜基于 2026-10-08 实读仓库（HEAD `a4ae03b`，含 PILOT-055 已落盘未提交改动）。
> 任务：UX 三缺陷修复 + 交互设计纳入开发验证流程。

## 技术栈与相关模块

- **todo-tracker 插件**：`Plugins/TodoTracker/`（后端 Controllers/Services/Models + `web/src/` Vue3 独立产物）。
  前端结构：`TodoView.vue`（列表/过滤/新建）、`components/TaskDetail.vue`（详情/四栏/委派/阶段/执行记录）、
  `components/ExecutionTimeline.vue`（执行记录时间线）、`store.ts`（reactive 单例 + 动作）、`http.ts`（写请求串行队列）、
  `types.ts`（与后端 DTO 一一对应，靠 `TodoTrackerContractTests` 钉字段）、`actions.ts`（纯函数）。
- **委派链路**：`TodoDispatchController.cs`（dispatch-to-agent / agent-status / agent-status/record）→
  `TodoDispatchService.cs`（预览含 DelegationAvailable/DelegationError/CanDelegate/Agents）→
  `AgentTaskGateway.cs`（接缝 `Delegation != null` 判定 IsAvailable；`SeamNotAvailableMessage` 固定文案）。
- **宿主项目档案**：`TodoProjectsController`（`/api/todos/projects` 列表、`/projects/resolve` 解析归一）；
  `TodoProjectDto`（ProjectDtos.cs:8，含 id/root/name/pathExists/isGitRepo/taskCount/openTaskCount）。
- **契约事实（实读）**：
  - `CreateTodoRequest`（TodoDtos.cs:102）**已支持 `ProjectId`（int?）**，`CreateAsync` 走
    `ApplyProjectFields(todo, request.ProjectPath, request.ProjectId, registerIfMissing: false)`。
  - `AgentStatusDto`（DispatchDtos.cs:100）字段：Ok/Error/TodoId/TaskKey/Status/Terminal/ExitCode/ErrorCode/ElapsedMs/
    ResultSummary/FilesChanged/Cwd/Verification/NotFound/StatusCode —— **无 AgentName**。
  - `TodoProjection.ToDto`（TodoProjection.cs:47-49）列表 DTO 已带 `AgentTaskKey`/`AgentId`/`PermissionMode`。
  - 契约测试：`TodoTrackerContractTests` 有「字段集一致」用例（扩 DTO 字段必须同步清单，否则红——正确提醒）。

## 三个 UX 缺陷的现状证据（实读代码，非猜测）

### 缺陷① 项目选择不存在 / 新建不自动关联
- `TodoView.vue` 新建任务**只有标题输入框**（L87-91 `create()` 只传 `{title}`）；列表顶部项目下拉是**过滤**用途（L103-106 projectFilter），不是选择器。
- `TaskDetail.vue` 项目区只有**手动输入路径**（L269-272 input + 关联按钮；已有项目时 L253「改」进编辑态），**无「从宿主项目清单选择」UI**。
- `store.ts` `selectedProject` computed（L48-49）存在但**前端无任何地方使用**。
- 后端 `/projects` 列表与 `/projects/resolve` 均已就绪 → **能力在，前端没用**（用户问「项目选择是真的吗」——答案：目前只有手动路径与过滤下拉，没有真正选择器；选了项目也没有「新建任务自动带出项目地址」）。
- 用户期望：能选已知项目（名 + 地址 + 任务数）→ 选中自动关联并显示项目地址 → 新建任务时可预选项目、创建即关联、地址对 agent 下发有效。

### 缺陷② 委派按钮禁用无可见提示
- `TaskDetail.vue` L361-362：`<button :disabled="!canDelegate" :title="delegateHint">`——`delegateHint` 四种文案（可委派/先生成提示词/接缝缺席/四栏不齐，L76-81）**只挂在 hover title**，页面无可见文案。
- agent 下拉 `v-if="s.preview?.agents.length"`（L356）：接缝在场但 **0 个 agent 时下拉消失、无任何提示**（用户在 AgentHub 未登记 agent 时按钮恒禁用且不知道原因/下一步）。
- 「一直禁用也没提示」成立：预览未生成、接缝缺席、agents 为空三种常见态都只有 hover 提示。

### 缺陷③ 进度无入口 / 只能默默等待
- 列表项（TodoView.vue L148-156）：只显示项目/assignee/记录数/时间，**无委派状态、无 taskKey**。
- 详情委派状态区（TaskDetail.vue L378-387）：`task.agentTaskKey` 存在才显示状态/刷新/记为执行记录——但**无自动轮询**（用户必须手动点「刷新」，「默默等待」成立）、**无 agent 名**（AgentStatusDto 无 AgentName）、无「去哪里看」引导。
- 执行记录时间线（ExecutionTimeline.vue）在详情页最底部，与委派状态区**分离**，用户不知道「进度 = 委派状态 + 执行记录」。
- toast 回执（store.ts L349-351）只给 taskKey，无后续进度路径。

## 流程层现状（用户第一诉求）
- AI-Native 闭环工件模板（docs/18-templates/ai-pilot/02-spec.tpl.md）**无「交互设计」节**；`docs/04-standards/ai-native-engineering-workflow.md` Spec 定义未强制交互设计。
- `plugin-development` SKILL.md §3.4 有「交互设计统一要求」（点即保存/成败可见/防闪/空态分级/边界/二次确认/版本展示），但**未与工件链绑定**：实施时未写交互设计节、未按交互清单走查 → PILOT-055 交付后用户体验差。
- 用户诉求：从需求到提交，交互设计/UX 验证成为**硬性环节**（工件必写交互设计节 + DoD 必验交互清单）。
