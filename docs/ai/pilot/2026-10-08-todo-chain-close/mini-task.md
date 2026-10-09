# PILOT-055｜todo → AI Agent 委派链收尾：P1/P2 缺陷修复 + 真实委派端到端验证（mini-task）

> 任务协调人：MainAgent（本会话）｜ 裁剪裁定：**轻量**（源文件 3 + 配套测试 2；规范 §4 允许 ≤3 文件缺陷修复合并为 mini-task，本任务协调人裁定并记录于此）
> 状态：**闸门1 待用户批准**（批准前不改任何业务代码）
> 基线：GitHub 同步已确认（github-origin/main=a678979 ⊆ 本地 HEAD a4ae03b，behind=0，无新提交可拉）；委派链代码由 PILOT-054 实现入库并在上会话隔离实例走查全链路通过（当时 0 agent，委派按钮禁用路径已验）。

---

## 0. 对外方案（六段 · AGENTS.md §3.1 硬约束）

### 1) 目标
基于最新代码把「todo 下发 → 委托 AI Agent 执行 → 结果回流留痕」闭环真正打通并验收：

- 修复 2 个走查缺陷：完成/重开不留执行记录（P1）、删除确认弹窗记录数恒 0（P2）；
- 用真实 agent CLI（**opencode**，经 pwsh 桥接登记）完成一次真实委派端到端（只读任务），留事件流 + 记录回流 + 截图证据；
- **成功判据**：后端 `dotnet test --filter TodoTracker` 全绿且新增 P1 留痕用例 ≥1；todo e2e ≥9/9（增 P1 留痕断言 + P2 弹窗计数断言）；真实委派走到「opencode 进程真实启动 → 事件流 → Succeeded → todo 自动新增执行记录」并截图。

### 2) 改动（源文件 3，≤5 条）

| 文件:行 | 动作 |
|---|---|
| `Plugins/TodoTracker/Services/TodoService.cs:230-268` | `CompleteTodoAsync` / `ReopenTodoAsync` 加 `string actor` 参数；状态实际变更成功时 `_records.AppendSystemAsync` 留痕（对齐 `ChangeStageInternalAsync:312-316` 模式：动作 + stageFrom/stageTo + actor；**幂等**：已是目标态则不改不记） |
| `Plugins/TodoTracker/Controllers/TodosController.cs:215-248` | `/complete`、`/reopen` 两处调用传 `ActorHint()` |
| `Plugins/TodoTracker/web/src/store.ts:171-188` + `:207-230` | `removeTodo` 弹窗记录数改用真实来源（`state.selectedId === task.id` 时用 `state.recordsTotal` 兜底，不再用变更接口返回 DTO 的恒 0 字段）；`completeOrReopen` 成功路径补 `await loadRecords()`（P1 新记录须即时反映进记录区与删除弹窗） |
| 测试 | 后端留痕用例（扩展 `ForgeSelf.Api.Tests/Plugins/TodoTracker/` 现有文件或新增小节）；e2e `todo-tracker.spec.ts` B2 增补「标记完成」留痕断言 + 新增 C2 弹窗计数断言 |

### 3) 不改
- AgentHub / ForgeSelf.Abstractions / 宿主代码与委派链契约（PILOT-054 已验证，不重开）；
- 既有 e2e 8 条语义（只增不删）；用户实例（:51888/7102/7002 均无监听，不启动、不停启、不改配置）；
- **不 push / 不 commit / 不发布**（属待授权项，本次不动）。

### 4) 验证（快档 §5.6 + 真实委派走查）
- 后端：`dotnet build` + `dotnet test --filter "FullyQualifiedName~TodoTracker"`（判据：日志正文 PASS 计数，不看 exit code）
- 前端：`cd ForgeSelf.Web && pnpm run check && pnpm run test`；插件前端 `cd Plugins/TodoTracker/web && pnpm run build`
- e2e：`cd ForgeSelf.Web && npx playwright test e2e/plugins/todo-tracker --workers=1`（零 mock 隔离实例）
- 真实委派走查：隔离实例（PATH 合并用户真实 PATH；`NO_PROXY`/`TEMP` 前置 §5.0）→ 登记 opencode（pwsh 桥接）→ probe → 建 todo → 关联 scratch 项目目录 → 生成提示词 → 一键委派（**只读任务**，如「列出当前目录文件」）→ 轮询状态/事件 → 执行记录回流 → 浏览器走查截图存 `ForgeSelf.Web/screenshots/e2e/todo-tracker/`

### 5) 代价与风险
- opencode 首跑可能缺登录/模型配置 → 委派失败或 Degraded：**如实报告并给证据，不造假**（回滚点：隔离数据根整体可弃，不碰任何真实数据）；
- pwsh 桥接登记（executable=pwsh + args 指向 `C:\nvm4w\nodejs\opencode.ps1`）规避 `.ps1` shim 无法被 CliTransport 的 CreateProcess 直拉——**仍是真实 CLI 委派**（真实进程真实执行）；
- 真实执行只跑只读任务；权限模式不用危险档（`AgentDelegationProvider` 黑名单本就先拒 yolo/danger-full-access/dangerously-skip-permissions）。

### 6) 待你拍板（每项带默认推荐）
- **Q1** 是否按本方案实施（P1/P2 + 真实委派走查）？默认：**是**
- **Q2** 真实委派用哪个 agent？默认：**opencode**（本机可用、有内置 profile 与输出映射）；备选 claude / grok
- （push/发布属另一授权项，本次不动）

---

## 1. Intent（为什么 / 做什么 / 到什么程度）

- **为什么**：用户要求「更新代码后，打通 todo 插件下发任务、委托 AI Agent 执行的链路」。PILOT-054 已实现委派链并全量走查，但留下 2 个走查缺陷未修，且**真实委派端到端从未跑通**（上会话隔离实例 0 agent，只能验证禁用路径）。
- **做什么**：修 P1（完成/重开不留执行记录）、P2（删除确认弹窗记录数恒 0）；用真实 agent CLI 跑通一次完整委派。
- **到什么程度**：代码改动收敛在 todo-tracker 插件内（3 源文件 + 2 测试文件）；真实委派以一次**只读任务**跑通为准；发布/推送不做（待授权，另行请示）。

## 2. Spec（要点；不确定点标 Unknown）

- **P1 行为定义**：`CompleteTodoAsync(id, actor)`——仅当状态确实从非完成态变为完成态（Status=Completed、Stage=Done、CompletedAt=Now）时，`AppendSystemAsync(todo.Id, actor, "标记完成", stageFrom: 原Stage, stageTo: Done)`；已是完成态则不改不记。`ReopenTodoAsync(id, actor)` 对称（action=「重新打开」，stageTo=Draft，CompletedAt=MinValue）。
- **P1 契约影响**：仅服务层签名变化 + 控制器 2 处调用点；HTTP 路径/请求体不变，无契约破坏。
- **P2 行为定义**：删除确认弹窗「该任务的 N 条执行记录会一并删除」的 N = 选中任务真实记录总数（`state.recordsTotal`，各写路径均已刷新；本批补上 completeOrReopen 的刷新缺口）；不再使用 `task.recordCount`（详情对象上被变更接口 DTO 覆盖后恒 0）。
- **验收**：P1 单测断言（actor 透传、stageFrom/To、记录 +1、幂等不重复）；e2e B2 增补「标记完成」后记录区出现留痕；e2e C2 弹窗计数与后端 `listRecords` 总数一致且取消不删。
- **Unknown（如实标注）**：① opencode 首跑的鉴权/模型可用性（本机有 LM Studio/grok 等 provider，能否直接可用实施时验证）；② 经 pwsh 桥接后 probe 的版本断言行为（`pwsh --version` 会过 `version_min 0.1.0`，属合理但不精确，Evidence 中如实说明）；③ `AppendSystemAsync` 的参数全名（id, actor, action, detail?, blockReason?, stageFrom?, stageTo?, result?）以 `ChangeStageInternalAsync:312-316` / `LinkProjectAsync:343` 实参为准，实施时核对签名。

## 3. Plan（分步，具体到文件）

1. **已完成的探查**：TodoService.cs:215-344、store.ts:130-415、TodosController.cs:215-320、CliTransport.cs、AgentProbeService.cs（扩展名含 .ps1）、AgentHubProfiles（opencode.json）、AgentHubAgentsController（POST 登记/GET discover/POST probe）、e2e spec 标题结构——全部实读。
2. 改 `Plugins/TodoTracker/Services/TodoService.cs`：Complete/Reopen 加 actor 参数 + 在现有 `if (状态未变)` 守卫内追加 `AppendSystemAsync`（保留幂等语义）。
3. 改 `Plugins/TodoTracker/Controllers/TodosController.cs`：`:222` `CompleteTodoAsync(id, ActorHint())`、`:242` `ReopenTodoAsync(id, ActorHint())`。
4. 改 `Plugins/TodoTracker/web/src/store.ts`：`removeTodo` 计数来源（选中任务用 `state.recordsTotal`）；`completeOrReopen` finally 补 `await loadRecords()`。
5. 后端测试：`ForgeSelf.Api.Tests/Plugins/TodoTracker/`（扩展 `TodoStageMachineTests` 或新增小节）——complete/reopen 留痕（actor、stageFrom/To、记录数 +1）、幂等（重复 complete 不新增记录）。
6. e2e：`ForgeSelf.Web/e2e/plugins/todo-tracker/todo-tracker.spec.ts`——B2 增补「标记完成」后执行记录区出现留痕条目；新增 C2「删除确认弹窗记录数 = 真实总数，取消不删」。
7. 门禁（快档 §5.6）：后端 build + 定向 test；宿主 check/test；插件前端 build；todo e2e。判据看日志正文。
8. 真实委派走查：起隔离实例（`FORGESELF_DATA_ROOT=<repo>\.temp\preview-data-055` + `FORGESelf_INSTANCE_ID=preview055` + PATH 合并用户真实 PATH + NO_PROXY/TEMP）→ 建 scratch 项目目录（含 2-3 个文件）→ 建 todo（补四栏）→ 关联项目 → 生成提示词 → 登记 opencode（executable=pwsh + argsTemplate 指向 opencode.ps1，vendor=opencode 复用输出映射）→ probe → 一键委派（只读任务）→ 轮询 `agent-status`/事件 → 记录回流 → 截图读图 → 清理自己造的测试数据（不删数据目录）。
9. 补件 `05-evidence.md` / `06-review.md`；按 §10 汇报；待授权项（push/发布）单独列出。

## 4. Task（Allowed / Forbidden + 验证命令）

**Allowed**：修改上述 3 源文件 + 2 测试文件；起/停**自己**的隔离实例；在 `<repo>\.temp\` 建 scratch 数据根与工作目录；登记测试 agent、建测试 todo（只清自己造的，不删任何数据目录）；按 §5.0 设环境变量后跑门禁。

**Forbidden**：碰 AgentHub/Abstractions/宿主代码与契约；停/启/杀用户实例或改其配置；`git commit`/`push`/发布；删任何数据目录/文件；用一次性 temp 脚本代替 e2e/单测（AGENTS §0 红线）；把猜测当证据写进工件。

**验证命令（pwsh 内执行；判据看日志正文）**：

```powershell
# 环境前置（AGENTS §5.0）
$env:NO_PROXY = 'localhost,127.0.0.1,::1'; $env:no_proxy = $env:NO_PROXY
$env:TEMP = $env:TMP = 'D:\src\my-proj\OpenForgeSelf\OpenForgeSelf\.temp\tmp'

# 后端
cd D:\src\my-proj\OpenForgeSelf\OpenForgeSelf\ForgeSelf.Api
dotnet build
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~TodoTracker" --nologo -v q

# 宿主前端
cd D:\src\my-proj\OpenForgeSelf\OpenForgeSelf\ForgeSelf.Web
pnpm run check
pnpm run test

# 插件前端
cd D:\src\my-proj\OpenForgeSelf\OpenForgeSelf\Plugins\TodoTracker\web
pnpm run build

# e2e（零 mock 隔离实例）
cd D:\src\my-proj\OpenForgeSelf\OpenForgeSelf\ForgeSelf.Web
npx playwright test e2e/plugins/todo-tracker --workers=1
```

**真实委派走查环境**（实施时逐项核验）：PATH 前置 `[Environment]::GetEnvironmentVariable('Path','User')` + `('Path','Machine')`；`FORGESELF_DATA_ROOT` / `FORGESelf_INSTANCE_ID` 隔离；起后端前 `netstat` 确认 7102/7002 空闲；收尾只停自己起的进程。
