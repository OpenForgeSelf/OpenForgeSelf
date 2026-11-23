# Specification

> Task ID：PILOT-087｜插件任务（sems）：运行「停止」无反馈缺陷修复

## Functional Requirements

- FR1 后端停止结果可区分：`IRunnerService.Stop/StopExternal` 返回结构化结果 `StopResult { Outcome, Pid, Reason }`，Outcome ∈ `Stopped / AlreadyStopped / Failed`。会话不存在、进程已自行退出、宿主/插件重启后调用、`taskkill` 后进程已消失 → `AlreadyStopped`；成功杀除进程树 → `Stopped`；进程仍在但无法终止 → `Failed`（带原因）。
- FR2 停止端点契约：`POST /api/commands/{id}/stop` 与 `POST /api/runs/{pid}/stop` → Stopped/AlreadyStopped 返回 `200 {success:true, outcome:"stopped"|"already-stopped", message}`；Failed 返回 `409 {success:false, outcome:"failed", message}`。
- FR3 前端三条停止路径（工作台详情 stopTask、RunPanel 会话停止、外部进程停止）对三种 outcome 均有可见反馈（toast + 日志留痕），已不在运行时同步收敛任务状态并刷新列表/计数。
- FR4 详情区停止按钮不再无声禁用：命令已结束/未运行时点击停止 → 明确提示「当前没有运行中的进程，无需停止」并刷新运行数据（而非静默）。
- FR5 运行中任务日志轮询遇 404 时，向 `/api/runs` 核对一次：会话确实不在 → 收敛为已结束并留痕一次（PC-024 lens 应用）；会话仍在（瞬态）→ 维持既有继续轮询语义。
- FR6 MCP 工具 `sems_stop_command`/`sems_stop_run` 对外语义不变（F25）：「无存活会话/无权限」仍返回既有 Fail 文案；仅「进程在但杀不掉」的失败路径给出带原因的 Fail。
- FR7 需求文档 F09 增补验收标准 AC-F09-1…7；测试计划 F09 同步。

## Input

用户输入1（2026-11-23）缺陷报告 + :51888 运行实例观察（`GET /api/commands/6/logs?after=0` 404 佐证状态脱节）。

## Output

见 03-plan 的文件级改动清单。对外 API 新增 `outcome` 字段（向后兼容：`success` 字段语义不变，老前端忽略新字段仍工作）。

## Business Rules

- BR1（F25）不改变任何 `sems_*` 工具的名称、权限与既有语义。
- BR2 停止是二次确认操作，取消不发任何请求（现状保持）。
- BR3 「已不在运行」不写失败文案、不弹 error 级反馈、不反复重试。
- BR4 运行会话/日志缓冲仍只存在宿主内存（不落盘），重启后丢失属已知 F12 语义，本批不改存储设计；收敛提示必须如实说明「宿主可能重启过，如进程仍在本机运行可用『检查』重新捕获」。

## Boundary Conditions

- 同一命令重复点击停止（防抖：进行中禁用）。
- External 会话 pid 已消失 → AlreadyStopped。
- pid≤0 / 极大 pid → 不抛异常，Failed 或 AlreadyStopped（带原因）。
- demo 模式工作台：`stopTask` 走本地状态收敛，不发网络请求（保持现状模式语义）。

## Error Handling

- 后端 kill 失败：`KillTree` 捕获异常并回传 `(bool, string? error)`，Failed.message 携带 taskkill 退出码/异常摘要。
- 前端停止请求异常：toast「停止失败：原因」+ 日志区留痕 + 状态保持 running（保留重试）。
- 日志核对请求本身失败（网络/5xx）：不计入收敛依据，维持现状轮询/提示行为。

## Compatibility

- 老前端对新响应：只读 `success`（200 布尔仍成立）——stopped/already-stopped 都会走「已停止」旧文案，不破坏。
- 新前端对旧后端（无 outcome 字段）：`outcome` 缺省按 `stopped` 处理，行为等同现状。
- 工具 JSON 输出键（`stopped/commandId/pid`）与 Fail 文案保持逐字不变（有测试锚定）。

## Non-functional Requirements

- 日志轮询收敛核对每次 404 至多触发一次 `/api/runs` 请求（不引入秒级放大）。
- 所有新增前端代码 TS 类型齐全，无 `any`；沿用 sems 插件既有 http/toast/confirmOps 设施，不引新依赖。

## Interaction Design（交互设计 · 插件任务必写节）

### 交互规格（点什么出现什么）

| 交互点 | 触发 | 结果（点什么出现什么） | 状态模型 | 反馈 | 空态 | 边界 |
| --- | --- | --- | --- | --- | --- | --- |
| 详情区「停止」 | 点击（任务选中、状态 running） | 确认?→否；直接 POST stop → 按结果 toast + 日志行 + 状态收敛 | running→done | toast「已停止：X」/「X 已不在运行，无需停止」/「停止失败：原因」+ 日志区留痕 | 无 | 进行中按钮禁用防重复提交 |
| 详情区「停止」 | 点击（状态 idle/done，旧版无声禁用） | 不发请求 → toast「X 当前没有运行中的进程，无需停止」+ 刷新运行计数 | 状态不变 | info toast | — | 选中命令为空时按钮保持禁用（无可操作对象） |
| 会话面板「停止」（Launched） | 确认弹窗→确认 | POST stop → outcome 三分支 toast + 列表刷新 | 会话条目→ended | success/info/error ElMessage | 列表空态不变 | 已结束条目不显示停止按钮 |
| 会话面板「停止」（外部捕获） | 确认弹窗（含杀树警示）→确认 | POST /api/runs/{pid}/stop → 同上 | 条目→ended | 同上 | — | pid 已消失 → info「已不在运行」 |
| 日志轮询（后台） | 404 且本地状态 running | 核对 /api/runs 一次；会话不在 → 状态收敛 done + 日志区一行「后端已无该会话…」 | running→done | 日志留痕（无弹窗打扰） | — | 核对失败不收敛 |

### 视觉规格（目标态）

- 本批不新增视觉表面；沿用既有 toast（WorkbenchView `.toast`）与 ElMessage 体系。文案分级：成功=成功色，已不在运行=中性提示（非红），失败=错误色（含原因）。
- 主题矩阵：不触及任何 token/颜色定义，light/dark 均沿用既有 toast 样式（本批不改动面，标注「不触及」）。

### 交互验收标准（并入 AC）

- AC-F09-1～7（见下）逐条可测；每条交互点至少一条 AC 锚定真实接口返回（outcome 字段），不只看回显。

### 走查符合性（DoD 绑定）

- 走查按 ui-ux-design「走查 UI 符合性清单」逐项核对停止三分支反馈；dev-stack 本地实例实测截图（05-evidence 留痕）。

## Acceptance Criteria

- **AC-F09-1** 点击「停止」后，无论结果如何都必须有可见反馈，禁止静默无响应；结果分三类：已停止 / 已不在运行 / 停止失败（含原因）。
- **AC-F09-2** 「已不在运行」（进程已自行退出、会话已不存在或宿主/插件重启过）不算失败：界面提示「已不在运行，无需停止」，任务/会话状态收敛为已结束，停止按钮回到不可用态，列表与运行计数刷新；不得停留在「运行中」或反复报「停止失败」。
- **AC-F09-3** 停止失败（进程仍在但无法终止）必须提示失败原因，且保留可重试入口（按钮仍可点、状态不收敛）。
- **AC-F09-4** 停止请求发出到结果返回期间，停止按钮处于进行中禁用态，不得重复提交。
- **AC-F09-5** 任务日志区对停止的每种结果留痕一行（已停止 / 已不在运行已同步 / 停止失败原因）。
- **AC-F09-6** 后端停止接口对三种结果返回可区分的 `outcome`（stopped / already-stopped / failed），前端不把「已不在运行」当异常错误处理。
- **AC-F09-7** 运行中任务日志轮询遇 404 必须与会话状态核对一次并收敛，不得无限轮询 404、状态卡「运行中」（PC-024 lens）。
- **AC-T1（复现）** 修复前：对无存活会话的命令调 `Stop` → 旧契约不可区分「已不在」与「失败」；前端 stopTask 对 404 仅报「停止失败」且状态不收敛（vitest 红灯）。
- **AC-T2（守卫）** 修复后：RunnerService 对真实进程（启动→停→再停）返回 Stopped→AlreadyStopped；控制器三态状态码矩阵 200/200/409；工具文案契约逐字锚定（xUnit 绿）。
- **AC-T3（守卫）** 前端 vitest：stopTask 三 outcome 反馈与收敛、404+running 核对收敛、confirmOps data 透传（绿）。

## Unknown

| 不确定点 | 影响 | 处理方式（询问/搁置/保守假设并标注） |
| --- | --- | --- |
| :51888 实例的实际部署版本与当前 worktree 是否一致 | 影响用户现场复现细节 | 搁置：本批以源码 + 本地 dev-stack 走查为准，运行实例复验待用户更新后执行（05/07 标注） |
| e2e 冒烟「停止后 .run__item 计数=0」与 D2 合并快照（ended 条目保留）的既有关系 | 跑 e2e 时可能暴露既有红 | 保守假设并标注：先跑基线判定是否既有红；若本批改动静音路径导致则修复断言 |
