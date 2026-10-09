# Specification

> 阶段：Stage 2｜从 00-repository-understanding 与 Intent 推导；所有内容与实际仓库一致；不确定点记 `Unknown`。
> Task ID：2026-10-08-ux-close

## Functional Requirements

### FR-1 项目选择（新建预选 + 详情选择 + 自动带出地址）
- FR-1.1 新建任务（TodoView 头部输入框旁）提供「项目」下拉，选项来自 `GET /api/todos/projects`（label = `项目名 · 地址短显（N 任务）`，option `title` 承载完整地址；实现已按此收口）；可留空（创建后补）。创建时把 `projectId` 传给 `POST /api/todos`（后端 CreateTodoRequest 已支持 ProjectId）。
- FR-1.2 详情页项目区增加「选择项目」模式：下拉列出宿主已知项目（name + root 短显 + 任务数/进行中数），选中即 `POST /api/todos/{id}/project`（传 projectId，后端 `LinkProjectAsync` 已支持 ProjectId→known.Root）；保存后显示项目名 + 完整地址（root）+ 原始写法回显（既有逻辑）。
- FR-1.3 「输入路径」模式保留（现有 input + 关联），两种模式可切换；已关联项目时显示 name/root + 「改 / 解除」。
- FR-1.4 项目下拉空态：宿主无项目档案时显示「还没有项目档案，可输入路径创建」，不静默空。

### FR-2 委派按钮禁用原因可见
- FR-2.1 委派按钮下方**常驻可见**一行说明（复用现 `delegateHint` 四态逻辑，从 title 提升为页面文案；title 保留作补充）：
  - 可委派 → 不显示（按钮可点）。
  - 未生成预览 → 「先生成提示词（委派前要先据它判断）」。
  - 接缝缺席（delegationAvailable=false）→ 后端 `delegationError` 原文（如「未检测到 agent 委派能力（agent-hub 插件未安装或未启用），任务仍可手工下发」）。
  - 四栏不齐 → 「四栏齐备（目标/正文/判据/验证命令）后才能一键委派」。
- FR-2.2 agents 空态：`delegationAvailable=true` 但 `preview.agents.length===0` → 文案「AgentHub 已就绪，但还没有可用 agent——去 Agent 中枢登记本机 agent（opencode/claude 等）」+「去登记」链接（导航桥跳 `/agent-hub`）。
- FR-2.3 按钮 `disabled` 逻辑不变（canDelegate = 四栏齐 + 接缝在）。

### FR-3 委派进度可见（闸门1 决策：用户选 **B** = 列表也实时，新增批量状态接口）
- FR-3.0 新增批量委派状态接口：`GET /api/todos/agent-statuses/batch?ids=1,2,3`（ids ≤ 当前页条数，逗号分隔）→ `List<AgentStatusDto>`（仅返回有 agentTaskKey 的任务；无委派任务不返回）。实现：对每个有委派的任务调 gateway 单查（并行 Task.WhenAll；本地快照查询，快），填充 AgentName。
- FR-3.1 列表行：`task.agentTaskKey` 非空时显示**实时委派状态徽标**（数据 = 批量接口轮询结果）：Queued=「排队中」、Running=「执行中」（带 agent 名）、Succeeded=「已成功」、Failed/Timeout/Cancelled=「失败/超时/已取消」、AwaitingPermission=「待授权」；批量结果未到/缺失时按 stage 推断兜底（Dispatched/Running=执行中、Review=待验收、Done=已完成、Failed/Cancelled/Blocked=已结束）；同时显示 taskKey 短显（title 全文）。
- FR-3.2 列表轮询：TodoView 挂载后对已委派任务每 15s 调批量接口刷新徽标（复用 `same()` 防闪）；过滤/翻页/刷新后重拉；组件卸载（onUnmounted）停止定时器。
- FR-3.3 详情委派状态区增强（保留）：
  - 显示 agent 名（后端 AgentStatusDto 新增 `AgentName`，由 `todo.AgentId` 从 gateway 可用 agent 清单解析；未知时显示「agent#id」）。
  - 委派状态行常驻显示（taskKey + agent 名 + 状态 + 耗时 + 结果摘要/错误）。
  - **自动轮询**：详情打开且 `agentTaskKey` 非空且状态非终态时，每 15s 调 `GET /api/todos/{id}/agent-status`；到达终态（terminal=true）停止轮询并 toast「委派已结束：{status}」；关闭详情/切换任务停止轮询；手动「刷新」保留。
- FR-3.4 执行记录区成为进度入口：委派状态区加「查看执行记录」锚点（滚动/聚焦到 ExecutionTimeline）；「记为执行记录」成功 toast 后自动滚动到记录区。

### FR-4 流程完善（交互设计成为工件硬性环节）
- FR-4.1 `docs/18-templates/ai-pilot/02-spec.tpl.md` 新增「交互设计」节（模板：状态模型/反馈/空态/动线/边界，每个用户可见功能点）。
- FR-4.2 `docs/04-standards/ai-native-engineering-workflow.md` Spec 定义处声明「交互设计」为必写节（与 §4 裁剪联动：轻量 mini-task 亦须含交互要点）。
- FR-4.3 `.agents/skills/plugin-development/SKILL.md` §3.4 强化：交互设计**落进 Spec 工件**（不只技能文档要求）；走查按交互清单逐项验证 + 截图；e2e 覆盖交互路径。
- FR-4.4 `AGENTS.md` §11 九阶段描述补一句：Spec 必含交互设计节（一句话级）。

## Input

- 宿主项目档案（`GET /api/todos/projects` → TodoProjectDto[]）；创建请求体（含 projectId）；关联请求体（{projectId} 或 {path}）；agent-status 响应（AgentStatusDto，新增 AgentName）。

## Output

- 前端：TodoView（新建项目下拉 + 列表徽标）、TaskDetail（选择项目 + 委派可见文案 + agents 空态引导 + 委派区 agent 名/轮询/锚点）、store（createTodo 传 projectId、轮询生命周期）、types（TodoSaveRequest.projectId、AgentStatus.agentName）、actions（徽标/状态映射纯函数）。
- 后端：AgentStatusDto.AgentName 填充（TodoDispatchService.AgentStatusAsync）。
- 文档：模板/规范/技能/AGENTS.md 交互设计声明。

## Business Rules

- 新建传 projectId 时走 `ApplyProjectFields(registerIfMissing:false)`：**只有宿主档案内项目才登记**，档案外路径需走详情「关联」（显式动作 registerIfMissing:true）——沿用既有语义，不改。
- 列表徽标按 stage 推断，**不代表实时 agent 状态**；实时状态以详情 agent-status 为准（避免引入批量状态接口）。
- 轮询只在详情页打开且非终态时进行；15s 间隔；关闭详情/切换任务即清除定时器；单任务单定时器。
- agent 名解析失败不阻断（显示 agent#id 兜底）。
- AgentStatusDto 扩字段 → `TodoTrackerContractTests` 字段清单同步（该用例红是正确提醒）。

## Boundary Conditions

- **新增** 批量委派状态接口（列表实时徽标的数据源），仅返回有委派任务、不改变任何写路径语义。
- **不改** AgentHub 插件自身 UI / 其任务列表页（todo 视角进度可见是本批边界）。
- **不改** 委派与接缝后端逻辑（IsAvailable/SeamNotAvailableMessage/CanDelegate 语义不变）。
- **不改** 宿主代码（导航桥宿主已提供 `forgeOpenPage`，插件间跳转直接消费）。
- **不新增** 前端测试基建（todo-tracker web 无 vitest，交互断言走 e2e + 走查截图）。

## Error Handling

- 项目选择保存失败：`showFailure` 端后端 reason 原文（既有通道），下拉保持选中但不落库。
- agent 名解析失败：显示 `agent#<id>`，不 toast 报错。
- agent-status 请求失败：保留上次状态 + toast 失败原因；轮询继续（直至成功或手动停止）。
- 轮询期网络中断：不终止轮询（下次 tick 重试）。

## Compatibility

- 契约：AgentStatusDto 加 `AgentName`（string?）——向后兼容（旧客户端忽略未知字段）；TS 类型同步；契约测试字段清单同步。
- 插件版本：todo-tracker plugin.json 已 bump 1.1.1（本批再改 → 版本定稿时确认是否需再 bump，见 Unknown）。

## Non-functional Requirements

- 轮询负载：列表批量 15s（页内已委派条数）、详情单条 15s（非终态才轮询）；关闭即停。
- 不闪：复用 `same()` 比对，状态未变不重渲染（既有机制）。
- 版本徽标照旧（铁律 13）。

## Interaction Design（交互设计 · 示范节）

> 本节为必写节（2026-10-08 用户立），审查用 `ui-ux-design` 技能。每个用户可见功能点写「点什么出现什么」。

### 交互规格

| 交互点 | 触发 | 结果（点什么出现什么） | 状态模型 | 反馈 | 空态 | 边界 |
| --- | --- | --- | --- | --- | --- | --- |
| 新建任务·项目下拉 | 打开下拉 / 选中项目 | 列出宿主项目（名+地址短显+任务数）；选中后创建任务自动关联该项目 | 无项目档案 / 有项目 | 选中即回显；创建后列表显示项目名 | 「还没有项目档案，可输入路径创建」 | 可留空（创建后详情补）；创建请求带 projectId |
| 详情·选择项目 | 切换「选择项目」模式 → 选中项目 | 下拉列项目 → 点「关联」→ 显示项目名+完整地址+原始路径回显 | 未关联 / 已关联（可改/解除） | 保存成功「已关联项目」；失败端后端原文 | 无项目档案同新建 | 与「输入路径」模式互斥可切换；根归一沿用 |
| 委派按钮 | 悬停 / 点击 | 不可委派时按钮下方常驻可见原因（四态）；可委派点击 → 确认弹窗 → 入队 toast「已交给 agent」 | 可委派 / 未生成预览 / 接缝缺席 / 四栏不齐 / agents 空 | toast + 按钮 loading | agents 空态：「AgentHub 已就绪，但还没有可用 agent——去登记」+「去登记」跳 /agent-hub | title 保留补充；文案不遮挡按钮 |
| 列表委派徽标 | 列表加载 / 每 15s | 已委派行显示实时徽标（排队/执行中+agent 名/成功/失败/超时）+ taskKey 短显 | 未委派（无徽标）/ 委派中 / 终态 | 无 toast（不打扰）；批量数据缺失按阶段兜底 | — | 批量接口接缝缺席返回空→无徽标不报错；15s 轮询卸载停止 |
| 详情委派状态区 | 打开详情 / 每 15s | 显示 taskKey+agent 名+状态+耗时+结果摘要；非终态自动刷新，终态停 + toast「委派已结束：{status}」 | 非终态轮询 / 终态停 | toast（仅终态一次）+ 手动刷新保留 | agent 名未知显示 agent#id | 切换任务/关闭详情停轮询；请求失败保留上次状态 |
| 执行记录锚点 | 点击「查看执行记录」/「记为执行记录」成功 | 滚动并聚焦到执行记录区 | — | 成功后 toast + 滚动 | 无记录空态文案沿用 | 记录区在详情最底部，锚点直达 |

### 走查符合性（DoD 绑定）

- 走查按 `ui-ux-design`「走查 UI 符合性清单」逐项核对：上表每行「触发 → 结果」与实现一致；CRAP 层级/重复/对齐/亲密性；点即保存落盘（刷新/重进仍在）；轮询 12s+ 无闪动；空态分级文案正确；禁用态原因可见；窄屏不破版。
- e2e 覆盖交互路径（新建选项目 / 委派禁用四态文案 / agents 空态引导 / 列表实时徽标 / 详情轮询终态停）；截图读图对照设计基准。

## Acceptance Criteria（逐条可测；闸门1 确认清单）

- [x] AC-1 新建任务：项目下拉可列出宿主项目（含地址与任务数）；选中后创建，`GET /api/todos/{id}` 返回 projectId>0 且 projectRoot 非空。
- [x] AC-2 详情页「选择项目」下拉选中项目后，页面显示项目名 + 完整地址；`GET /api/todos/{id}` 落库一致；「输入路径」模式仍可用。
- [x] AC-3 委派按钮在「未生成预览 / 接缝缺席 / 四栏不齐 / agents 空」四种态下，按钮下方显示对应可见文案（e2e 断言文本，非 title）。
- [x] AC-4 agents 空态显示「去 Agent 中枢登记」引导，点击可跳到 `/agent-hub`（导航桥）。
- [x] AC-5 列表行对已委派任务显示**实时委派状态徽标**（批量接口数据：排队/执行中/成功/失败/超时 + agent 名）与 taskKey 短显；批量数据缺失时按阶段兜底；未委派任务无徽标。
- [x] AC-6 列表打开时对已委派任务 15s 轮询批量接口刷新徽标（卸载停止）；详情委派区显示 agent 名、非终态 15s 自动刷新至终态后停止、手动「刷新」可用。
- [x] AC-7 「记为执行记录」成功后出现「查看执行记录」引导并滚动到记录区。
- [x] AC-8 后端定向测试绿（含契约字段清单 + AgentName 填充断言）。
- [x] AC-9 宿主 check/vitest 绿、插件前端 build 过、todo e2e 全量绿（新增交互用例 + 既有 10 条不回归）。
- [x] AC-10 走查：隔离实例浏览器点一遍交互清单 + 截图读图（项目选择/委派禁用/空态/徽标/轮询/记录锚点）。
- [x] AC-11 流程落盘：02-spec.tpl.md 加交互设计节；ai-native-engineering-workflow.md / plugin-development §3.4 / AGENTS.md §11 同步声明；本批 02-spec 含交互设计节。

## Unknown

| 不确定点 | 影响 | 处理方式 |
| --- | --- | --- |
| 批量接口对无接缝（agent-hub 未装）时返回什么 | 列表徽标 | 接缝缺席时批量接口返回空（不报错）；列表此时无委派任务（委派按钮本就禁用），徽标不出现 |
| 批量查询 N 次单查的耗时（页 20 条） | 轮询间隔内是否完成 | 本地快照查询（FindAsync 查任务表不拉起进程），Task.WhenAll 并行；e2e/走查实测耗时，超 15s 再调间隔 |
| 列表实时徽标与详情实时状态双轮询的叠加 | 请求量 | 列表 15s 批量（页内已委派条数）、详情 15s 单条；详情打开时列表轮询不停止（互不干扰，量小） |
| todo-tracker 插件版本是否再 bump（本批交互改动实质变更） | 插件版本语义 | 实施收口时定：本批已 1.1.1 未发布，1.1.1 即覆盖本批（含 UX）全部改动，不再单独 bump |
| 导航桥跳 agent-hub 在隔离预览/e2e 环境的可用性 | AC-4 断言 | e2e 只断言引导文案与链接存在；实际跳转在走查截图验证 |
