---
id: 2026-11-23-sems-stop-no-feedback
title: sems 停止无反馈——运行中日志 404 被无限吞错轮询致任务卡「运行中」，停止链路无可区分反馈
pc: PC-024
severity: P2
evidence: Verified
---

# case：sems 停止无反馈（2026-11-23，PILOT-087）

## 现象
sems 工作台「任务」页点击「停止」按钮无反应、页面无任何提示——即使目标进程早已停止。用户输入1原话：进程早就停止了的，点击停止没反应，页面没提示；要求在对应需求增加验收标准、增加测试复现，并完善该功能：点击停止命令，无论什么结果都要有反馈。

## 时间线
- 2026-11-23 输入1 报缺陷（停止点击无反馈）。
- 现场取证（输入2，:51888 运行实例）：`GET /api/commands/6/logs?after=0` 返回 404（宿主/插件重启过，日志缓冲随之消失）；`GET /api/runs` items 为空。任务页该命令仍显示「运行中」。
- 根因链（读码定位，PILOT-087 00 工件）：任务页日志轮询 `pullTaskLogs` 对「404 + 状态运行中」直接 return（原意是容忍「刚启动、缓冲未建」的启动瞬态）→ 日志缓冲已随宿主重启消失时永不收敛，任务永远「运行中」→ 停止按钮可点 → `POST /api/commands/{id}/stop` 后端因会话表（宿主内存）为空返回 404 → 前端只弹「停止失败」且状态不收敛，不解释、不收敛、难重试。另一侧：停止接口把「会话不存在」「进程已自行退出」「真失败」折叠成一个布尔 + 404，前端无从区分。
- 修复（红→绿）：后端 `Stop()`/`StopExternal()` 改为三态 `StopResult`（Stopped / AlreadyStopped / Failed 带原因），控制器 200/200/409 且响应体带 `outcome`；工具层既有文案逐字不变（F25）。前端 `stopTask` 按结果分支（每种结果 toast + 日志留痕 + 状态收敛），未运行时点停止不发请求直接提示；`pullTaskLogs` 404+运行中改为向 `/api/runs` 核对一次，会话不在则收敛（留痕一次 + 登记失效终结轮询），会话在（启动瞬态）维持轮询。

## 特征签名
- 网络面板：`/api/commands/{id}/logs?after=…` 404 每 1.2s 重复（任务选中期间），任务状态徽标一直「运行中」。
- UI：点「停止」后要么「停止失败」一闪而过、要么毫无变化；任务永不进入「已完成」。
- 触发前提：宿主或插件重启过（会话表/日志缓冲在宿主内存，清空），前端本地仍记该任务为运行中。

## 修复
见 [PC-024](../problem-classes/PC-024-frontend-poll-error-handling.md) §3/§5。实现：`Plugins/Sems/Services/RunnerService.cs`（StopOutcome/StopResult 三态 + KillTree 回传原因）、`ProjectCommandsController.Stop` / `RunsController.StopExternal`（200/200/409 + outcome 字段）、`Plugins/Sems/web/src/workbench/workbenchStore.ts`（stopTask 四路分支 + sessionStillLive 核对收敛）、`RunPanel.vue`（already-stopped → info 提示）、`ProjectTasksView.vue`（停止按钮保持可点 + 进行中去重）。

## 守卫
- `Plugins/Sems/web/src/workbench/stop-feedback.spec.ts`（G2，6 例）：404+运行中收敛/瞬态维持轮询、stopped/already-stopped/failed/not-running 四分支（红→绿已验证：修复前 5 红全命中）。
- 后端三态契约：`SemsControllerTests`（200/200/409 + outcome 断言）、`RunnerServiceTests`（第二次停止 → AlreadyStopped 等）、`SemsToolExtensionTests`（F25 文案逐字守卫）。
- 需求/测试计划同步：`Plugins/Sems/docs/01-requirements.md` F09 验收标准 AC-F09-1…7（v1.0.2）、`Plugins/Sems/docs/02-test-plan.md` T-F09-S1…S6。

## 走查发现（同场景伴生缺陷，2026-11-23 随批修复）

起源码本地走查（PILOT-087 六步③）发现：workbench toast 反馈通道本身**不可见**——`.wb__toast-host` 被覆盖为 `position:absolute;bottom:48px`，锚在 `min-height:100vh` 的 `.wb` 容器文档底部，长页面里 toast 渲染在首屏之外（实测 rect y=936 > 视口 716）。用户点停止虽会弹 toast，但滚动位置在上方时**看不见**＝"点了没反应"体感复刻，且该缺陷自 workbench 重构（ca1ccc3，2026-11-19）起影响**所有** workbench toast（启动完成、错误提示等）。

- 根因：容器内定位覆盖（交接包 fixed→sems 容器内）时把 toast-host 从 fixed 改为 absolute，未意识到 `.wb` 是整页高度容器——锚定对象错位。
- 与本 case 主根因（终态分支不收敛/不反馈）**不同根因**，但同属"停止点击后无可见反馈"这一用户可见症状的必要环节；02-spec 交互设计（行 60/68）承诺 toast 为任务详情路径反馈通道，故随批修复（闸门1 expected_files 追加 `workbench.css` 并留痕）。
- 修复：`workbench.css` toast-host 改 `position:sticky;bottom:48px`（与 statusbar 同机制，空态不占布局，弹出时贴滚动口底部居中）；重建后实测 toast 可见（y=610 / vh=716），AC-F09-2/5 截图以修后状态重采。design:check 复跑 PASS（无新增违规）。

## 复发追加（同 PC-024 选中态逃逸面，2026-11-23 输入4）

主缺陷修复发布（1.3.0.2611231715）后，用户仍看到 `GET /api/commands/{id}/logs?after=…` 在命令已停止/异常退出后**持续**发出（另一请求 `GET /api/smart-terminal/inbound` 经核实为主壳收件箱，设计内，非本 case）。定位：`ensureTaskPolling` 的选中任务轮询面只看失效登记、不看任务状态（workbenchStore.ts 原 :748/:758）——终态（done）任务被选中即永续轮询，与主根因「终态分支不收敛」同类（PC-024 第 4 发；守卫只测了收敛动作、未测收敛后的循环条件）。

- 修复（红→绿）：`tailPull` 一次性补拉登记——终态后恰补拉一针抓后端尾部留痕行（如「进程已退出（PID …）」，保留原轮询才有的停止后退出留痕），用完即清；选中面条件加「非终态或在补拉集」；选中已完成历史任务仅选中时拉一针。修复前终态选中任务 5 tick 发 6 次请求（vitest 实测红），修复后 ≤1 次即停（55/55 绿）。
- 同步：`01-requirements.md` AC-F09-8（v1.0.3）、`02-test-plan.md` T-F09-S7、PC-024 复发记录+lens 增补、G2 守卫新例两组；工件 `docs/ai/pilot/2026-11-23-sems-log-poll-converge/mini-task.md`。
