# mini-task — todo 委派终态自动回写 + 详情委派回显（2026-10-09-todo-auto-advance）

> 级别：轻量（3 文件缺陷修复）。闸门1 裁剪记录：用户 2026-10-09 明确「需要我拍板的就按你推荐的来就好」「你能操作的话自己操作，见面只报结果」（输入6 立规）——按推荐方案直接实施，方案与判据本文件自含。

## 1. Intent

- **Problem（用户输入9 实测）**：运行实例任务 50（内置引擎委派 ok-builtin.txt）执行结果「已成功·程序员 #run:6」（徽标来自 AgentHub/AIAgent 实时快照），但 todo 状态徽章一直「执行中」（`Todo.Stage=Running` 永不翻转）；点详情委派区不回显真实下发对象（角色下拉恒为「默认（程序员）」占位，不按 `task.agentId` 回填）。
- **根因（已读码确认）**：
  1. `TodoDispatchService.cs`：`AgentOutcomeStage(Succeeded)=Review`，但**只有手动按钮**「记为执行记录」（`RecordAgentResultAsync`，Controller 唯一调用方）才推进 `Stage`；内置/外部委派完成后无自动回写 → `Stage` 停 Running。列表徽章 `AgentStatusesAsync` 实时读快照 → 与 `Stage` 打架。
  2. `TaskDetail.vue:45-49`：`agentId`/`agentRoleIndex` 仅初始化为 `undefined`（不按 `task.agentId` 回填）→ 打开旧任务详情，下拉不回显真实下发的角色/agent。
- **Expected Outcome**：①委派终态（Succeeded）自动把 todo 推进到「待验收」并留痕（幂等、防并发双写），列表/详情状态自动一致；失败/取消保持「由人判」语义（不动状态）；②打开详情，引擎/角色/agent 下拉按任务既有委派回显真实对象。
- **Success Criteria**：任务 50 升级后首次读取即自动变为「待验收」+ 新增「agent 执行回写：Succeeded」记录；详情角色下拉显示「程序员」而非占位；新增 3 条单测全绿 + 既有 todo 测试无回归。

## 2. Spec（要点 + 交互设计）

- FR1 自动回写：读委派状态（列表批量/详情单查共用 `ReadStatusAsync`）读到**终态 Succeeded** 且 `todo.Stage==Running` → 推进 `Stage=Review`、写一条「agent 执行回写：Succeeded」记录（actor=agent 名，stageFrom=Running，stageTo=Review）。
- FR2 幂等：推进后 `Stage!=Running` 不再推进；同一 todo 并发读用静态 todoId 粒度锁 + 锁内双检，只写一次。
- FR3 范围：Failed/Cancelled/Stuck 终态不自动推进（保留「失败由人判」语义）；非终态（Queued/Running）不推进；接缝缺席/快照丢失/格式异常不推进。
- FR4 回显：详情打开时 `engine` 按 `task.agentEngine`（已有）、`agentRoleIndex` 按 `task.agentId`（1..7）回填、`agentId` 按 `task.agentId` 回填；切换任务（watch id）同样回填。
- 交互设计（点什么出现什么）：

| 交互点 | 触发 | 结果 | 状态模型 | 反馈 | 空态/边界 |
| --- | --- | --- | --- | --- | --- |
| 委派状态读取（列表/详情） | 详情打开 / 列表 12s 轮询 / 点「刷新」 | agent 终态 Succeeded 且任务 Running → 状态自动转「待验收」+ 记录追加 | Running→Review 仅一次 | 徽章变「待验收」；记录区新增一条回写记录 | 已 Review 不再重复；失败不动（人判） |
| 详情委派区下拉 | 打开任务详情 | 引擎下拉=任务引擎；角色下拉=委派角色名（如「程序员」）；外部 agent 下拉=委派 agent 名 | 按 task.agentEngine/agentId 回填 | 下拉显示真实对象，不再恒为「默认（程序员）」占位 | 未委派过保持默认（外部引擎 + 占位） |
| 「记为执行记录」按钮 | 手动点击 | 兜底回写（用户手动触发） | 与自动回写同路径幂等 | toast | — |

- AC（验收标准，逐条可测）：
  - AC1 单测：内置 Succeeded 终态读状态后 `Stage=Review` + 出现一条「agent 执行回写：Succeeded」（stageTo=Review）。
  - AC2 单测：已 Review 再读不重复写记录（幂等）。
  - AC3 单测：Failed 终态读状态后 `Stage` 仍 Running、无回写记录。
  - AC4 前端 build 过（vue-tsc 类型含 syncDelegation 回填）。
  - AC5 既有 Todo 测试过滤集全绿（无回归）。
  - AC6 运行实例复验：任务 50 首次读取自动变「待验收」+ 记录新增 + 详情角色下拉显示「程序员」（截图存档）。

## 3. Plan（真实文件）

- `Plugins/TodoTracker/Services/TodoDispatchService.cs`
  - 新增 `private static readonly ConcurrentDictionary<int, object> AutoAdvanceLocks`；
  - 新增 `private async Task TryAutoAdvanceAsync(Todo todo, AgentStatusDto status)`：前置（status.Ok && status.Terminal && todo.Stage==Running && AgentOutcomeStage(status.Status)>=0）→ 锁 todoId → 重读 todo 双检 Stage==Running → `Stage=Review; Status=ToLegacyStatus(Review)`、`CompletedAt` 补记、`UpdateAsync` → `_records.AppendSystemAsync(todo.Id, status.AgentName ?? "agent", "agent 执行回写：" + status.Status, detail: taskKey+耗时+工作目录, result: status.ResultSummary, evidence: taskKey, stageFrom: Running, stageTo: Review)`；
  - `ReadStatusAsync` 外部分支成功构造 DTO 后、`ReadBuiltInStatusAsync` 成功构造 DTO 后各调一次 `TryAutoAdvanceAsync(todo, dto)`（await）。
- `Plugins/TodoTracker/web/src/components/TaskDetail.vue`
  - 新增 `syncDelegation(t)`：`engine.value` 按 `t.agentEngine`；builtin → `agentRoleIndex.value = t.agentId in 1..7 ? t.agentId : undefined`；agenthub → `agentId.value = t.agentId > 0 ? t.agentId : undefined`；
  - setup 初始化与 `watch(() => props.task.id)` 回调中调用。
- `ForgeSelf.Api.Tests/Plugins/TodoTracker/TodoBuiltInDispatchTests.cs`
  - 新增 3 用例（AC1/AC2/AC3），复用 `FakeBuiltIn`（Snapshot 可控）。
- 发布附带：`Plugins/TodoTracker/plugin.json` Version 1.1.2 → 1.1.3（插件行为变化）。

## 4. Task（工作单元）

- Allowed：上述 3 文件 + plugin.json 版本号；Todo 插件内部逻辑与详情组件回显。
- Forbidden：不改 AgentHub/AIAgent 宿主；不改状态流转表语义（失败仍由人判）；不重构无关代码；不 git 提交（用户未授权）。
- Verification Commands：
  - 后端：`dotnet build ForgeSelf.Api` + `dotnet test ForgeSelf.Api.Tests --filter FullyQualifiedName~TodoBuiltInDispatchTests`（快档；改动在插件服务）。
  - 前端：`cd Plugins/TodoTracker/web && pnpm run build`（类型检查）。
  - e2e：`e2e/plugins/todo-tracker`（1 worker，防回归）。
  - 发布：`release-local.ps1 -Version 2.3.9 -Sign -UpdateDir updates` → 页面自升级（确认弹窗自点）→ 运行实例复验 AC6。
