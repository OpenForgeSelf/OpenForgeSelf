# 02-spec — B3 任务中心（跨入口任务总览 + 宿主视图）

> Task ID：`2026-11-02-maf-runtime-hardening/02-b3-taskcenter`｜日期：2026-11-02
> 上游：00-campaign-workflow.md §1 B3｜架构真源：docs/01-architecture/maf-centric-architecture.md（L1 交互工作台：入口/可视化/Run 回放）
> 不确定点标注 `Unknown`；已决策标注「决策」。

## 1. 目标（Why）

三个任务入口（开放任务/计划任务/定时触发）各自有 API 但没有统一视图：
开放任务=会话事件轨迹（`ISessionStore` turn 事件，不写 AgentRun）、计划任务=`AgentRun` 表（`IRunOrchestratorService`）、
定时任务=Scheduler `ScheduledTask`（`api/scheduler/tasks`，其中 TaskType=AgentRun 的到点经 `IScheduledAgentRunTrigger`
触发 MAF Run——其产物也是 AgentRun，SessionId=`scheduler:{taskId}`）。B3 在 L1 补一个跨入口总览：**一个列表看全部、
状态准确、失败可见原因、可一键重试/续跑**。

## 2. 范围（What）

**做**：
- `GET /api/ai-agent/tasks-overview`（新控制器，统一 DTO）。
- 宿主 `/task-center` 视图（列表+徽标+失败原因+重试/续跑+轮询）。
- features.ts / tabs.ts / router 注册入口。

**不做**（NOT-to-do）：
- 不改执行引擎（RunOrchestrator 内部、MafLoop、审批挂起）——B2 写面。
- 不改既有 Controller（含 AgentRunsController/SchedulerController——Scheduler 数据只读复用既有 API）。
- 不做 WebSocket 任务事件总线（既有 WS 仅聊天流；任务推送属后续批次，本批用 REST 轮询）。
- 不做任务创建/编辑（入口在智能体页与既有面）。

## 3. 数据源与契约

### 3.1 后端 GET /api/ai-agent/tasks-overview

查询参数：`source`（`open|planned`，空=全部）、`limit`（开放任务会话数上限，默认 30，钳 1–100）。

响应（camelCase）：
```json
{
  "success": true,
  "generatedAt": "2026-11-02T21:30:00+08:00",
  "sources": [ { "source": "open", "count": 2, "ok": true, "error": null },
               { "source": "planned", "count": 1, "ok": true, "error": null } ],
  "items": [ TaskOverviewDto... ]
}
```

`TaskOverviewDto`（统一 DTO）：
| 字段 | 类型 | 说明 |
|------|------|------|
| `id` | string | 稳定标识：`open:{sessionId}` / `plan:{runId}` |
| `source` | string | `open` \| `planned` |
| `title` | string | 标题（会话标题/任务原文首行截断） |
| `status` | string | 归一状态字典（§3.2） |
| `failureReason` | string? | 失败/卡住原因原文（可空） |
| `startedAt` / `endedAt` | DateTime? | 开始/结束时间 |
| `retryable` | bool | 是否可重试/续跑 |
| `retryUrl` | string? | planned：`/api/ai-agent/runs/{id}/resume`（POST SSE） |
| `sourceRef` | string | sessionId 或 runId 原值 |

### 3.2 状态字典（统一）

`running`（执行中）/ `planning`（规划中）/ `pending`（待执行）/ `completed`（已完成）/
`failed`（失败）/ `stuck`（卡住：等待审批、等待人工、暂停可恢复）/ `cancelled`（已取消）/
`scheduled`（定时任务就绪待触发，前端专用）/ `unknown`（无轨迹，前端可显示「无轨迹」）。

### 3.3 开放任务投影规则（决策 D2）

会话枚举：`IPluginMessageService.GetSessionsAsync(Active)`（插件自有服务，构造注入）→ 按 LastTime 倒序取 `limit` 条；
逐会话 `ISessionStore.Replay(sessionId)`（**宿主契约经 `IContext` 运行期解析**，铁律 12c）扫描事件：

| 观测 | status | retryable | failureReason |
|------|--------|-----------|----------------|
| `turn/start` 无配对 `turn/end` | running | false | — |
| 末 `turn/end` Reason=Completed | completed | false | — |
| Reason=Suspended | stuck | true | 回合暂停（可续跑） |
| Reason=MaxTokens | stuck | true | 回合因 token 上限收束 |
| Reason=Aborted 且末尾有 `assistant/attempt` 错误 | failed | true | attempt ErrorKind/内容摘要 |
| Reason=Aborted（无错误痕迹） | cancelled | false | — |
| Reason=NoInput | completed | false | — |
| 有未闭合 `tool/approval-request`（其后无同 CallId `tool/result`） | stuck | true | 等待工具审批：{工具名} |
| 无任何 turn 事件 | unknown | false | — |

时间：startedAt=首个 `turn/start`（缺则会话 LastTime）；endedAt=末个 `turn/end`（运行中为 null）。

### 3.4 计划任务投影

`IRunOrchestratorService.ListRunsAsync(null, null, 1, 50)`（插件服务，构造注入）：
Pending→pending / Planning→planning / Running→running / Completed→completed / Stuck→stuck（StuckReason）/
Failed→failed（StuckReason 承载错误原文） / Cancelled→cancelled；Stuck|Failed → retryable=true + retryUrl。

### 3.5 Scheduler 桶（决策 D1：前端服务层聚合）

后端控制器**只聚合 open+planned**（AIAgent 插件内数据）。Scheduler 行由 `taskCenterApi.ts` 调既有
`GET /api/scheduler/tasks`（`TaskType=3` AgentRun 过滤）映射为同一 `TaskOverviewItem` 形状：
Enabled→scheduled、Error→failed（FailureCount）、Disabled→cancelled；`FailureCount>0` 行按需拉
`GET /api/scheduler/tasks/{id}/logs` 取最近失败原因（并行、每轮≤5 条）；重试=`POST /api/scheduler/tasks/{id}/run-now`。

**决策 D1 依据**：AIAgent.csproj 仅引用 Core+Abstractions（插件间禁止隐式耦合，架构 §5.4）；
`ScheduledAgentRunContracts.cs` 既有裁决「不走 HTTP 自调」；Abstractions 无读侧契约，新增契约+改 SchedulerPlugin
会把写面扩到并行队友的目录。代价：统一 DTO 的第三源映射在前端（形状同一份 TS 类型，文档 §3.2 为契约真源）。

## 4. Interaction Design（交互设计 · ui-ux-design 模板）

### 4.1 页面五态

| 态 | 触发 | 表现 |
|----|------|------|
| 加载 | 进入页面/手动刷新，首次无数据 | 列表区 `v-loading`，不闪空白 |
| 空 | 三源均 0 条 | EmptyState（icon=check-square，主文案「还没有任务」，副文案说明三个入口）+ 动作按钮「打开智能体发一个任务」→ openPage('/ai-agent') |
| 出错 | 任一源 fetch 抛错 | 列表上方错误条（错误原文）+「重试」按钮；已有数据保留展示（不白屏） |
| 无权限 | 401 | 错误条显示「请求失败(401)…」，引导从设置页检查访问密钥 |
| 正常 | 数据到达 | 任务列表（ForgeCard 容器） |

### 4.2 交互点（点什么出现什么）

| 交互点 | 触发 | 结果 | 反馈 | 边界 |
|--------|------|------|------|------|
| 来源筛选 | 点击「全部/开放任务/计划任务/定时任务」分段 | 列表即时过滤（本地 filter，不重拉） | 选中态高亮 | 空桶显示桶级空文案（「暂无该来源任务」） |
| 「仅看异常」开关 | 点击 checkbox | 只显示 failed/stuck | 开关态 | 与来源筛选叠加 |
| 行点击 | 点击任务行（open/planned） | openPage('/ai-agent')（智能体页处理任务） | 标签栏出现「智能体」 | scheduler 行不跳转（无配置页） |
| 续跑（planned 行，failed/stuck 才有） | 点击「续跑」 | POST retryUrl 消费 SSE 流至 done/error 终止事件 | 按钮 loading→完成 toast「续跑已发起」；error→toast 显示错误原文 | 已终态（completed/cancelled）不显示按钮；重复点击防抖（进行中禁用） |
| 立即运行（scheduler 行，Enabled 才有） | 点击「立即运行」 | POST /api/scheduler/tasks/{id}/run-now | toast 成功→刷新列表 | Disabled/Paused 不显示按钮 |
| 失败原因 | failed/stuck 行 | 行内原因文本（最多 2 行，`title` 悬浮看全文） | — | scheduler 行 FailureCount>0 时按需拉 logs（并行≤5），拉取中显示「查询失败原因…」 |
| 自动刷新 | 每 15s | 静默重拉三源 | 无 loading 打断；**数据指纹未变不重渲染（防闪）** | `document.hidden` 暂停；回前台立即刷一次；组件卸载停止计时器 |
| 手动刷新 | 点击 PageHead「刷新」按钮 | 立即重拉 | 按钮 loading | 轮询计时器重置 |

### 4.3 视觉规格（DESIGN.md 对齐）

- `PageHead`（eyebrow=`Tasks · 任务中心`，title=`任务中心`，actions=刷新按钮）+ `ForgeCard` 容器 + `EmptyState`。
- 状态徽标：圆角 4px 色块，色板 token——running=`--el-color-primary`、completed=`--el-color-success`、
  failed=`--el-color-danger`、stuck=`--el-color-warning`、pending/planning/scheduled=`--el-color-info`、
  cancelled/unknown=中性（`--el-text-color-secondary` 描边）。**禁裸色值**；字体用 `--font-mono`。
- 来源徽标：开放任务/计划任务/定时任务，mono 10px 金色描边（`--gold-400`）。
- 时间/计数：mono 小字（`--font-mono`，`--el-text-color-secondary`）。
- 动效：仅 ForgeCard 自带挂载进入（--dur-fast）；无循环动画。

### 4.4 验收标准

1. 三源数据同列可见，来源/状态/时间与各真源一致（对照 api/ai-agent/runs、api/scheduler/tasks 抽样）。
2. failed/stuck 行可见失败原因与重试入口；planned 续跑点击后 Run 状态在列表刷新后变化。
3. 轮询 15s 观察无闪动（指纹不变不重渲染）；切后台暂停、回前台即刷。
4. 空态/出错/无权限三态文案正确（走查时清数据或断后端验证空态）。
5. design:check 三项零新增；check-features 通过（无孤儿）。

## 5. Repository Understanding（关键事实，读码确认）

- 插件控制器宿主契约必须 `IContext` 运行期解析（铁律 12c，AIChatController.cs:43-47 实证注释）。
- `IRunOrchestratorService` / `IPluginMessageService` 是插件自有服务，可构造注入（AIAgentPlugin.cs:41,60）。
- `MafTaskLoopAgent` 落 `turn/start|end` 事件（MafTaskLoopAgent.cs:182,491）→ 开放任务投影可行。
- Scheduler 读侧契约在 Abstractions 不存在（仅 `ISchedulerHost` 生命周期 + `IScheduledAgentRunTrigger` 写侧）→ 决策 D1。
- 宿主 request.ts 自动带 token；`[Authorize("ApiKeyPolicy")]` 插件控制器可用（ImGateway PILOT-078 先例）。
- AgentRunDto.StuckReason 承载失败原因（AgentRunsController 失败路径写入 StuckReason）。

## 6. Unknown / 风险

- ~~Scheduler 桶聚合位置~~ → 决策 D1 已定。
- 开放任务「Aborted=用户取消还是失败」只能靠 attempt 错误痕迹启发式区分（会话日志无显式 cancel 事件）→ 已在 §3.3 声明规则，unknown 不强判。
- `AgentRun.StuckReason` 在 Failed 态是否必有值：**Unknown**（Mock 测试覆盖有值/空值两态；空值显示「无失败详情」）。
- dev-stack 宿主重启后的旧 DLL 含旧代码 → 走查前必须确认构建窗口内新 Controller 编译进宿主。

## 7. 测试计划

- 后端 xUnit（Mock 服务层，任务指定）：投影规则表 §3.3 全行覆盖 + source 过滤 + limit + 空数据 + 统一 DTO 字段断言。
- 前端 vitest：scheduler 行映射/状态归一/重试标记纯函数；视图五态关键行为（loading/空/错/按钮可见性）。
- 集成验证：dev-stack :7301 Invoke-RestMethod 打端点验 JSON 结构（sources+items+DTO 字段）。
- 走查：dev-stack 前端 :7399 截图 → `ForgeSelf.Web/screenshots/dev/taskcenter-20261102/` 读图核对 §4.3。
