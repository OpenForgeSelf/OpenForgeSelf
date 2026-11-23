---
id: PC-024
title: 前端轮询面错误处理缺失——静默吞 404（终态错误继续轮询）+ 无 catch 裸抛反复 toast
tags: [frontend, polling, error-handling, 404, session-restore]
invariant: 轮询型请求的 catch 必须区分终态与瞬时错误：404 等终态必须终结该资源后续轮询、登记失效并仅提示一次；async 请求链禁止无 catch 裸抛；恢复型快照（sessionStorage/localStorage）注入的 id 首次终态失败必须标记失效，不得长期留在活跃轮询面。终态判定若依赖另一数据源（如运行列表）核对时，核对失败按「仍在」处理，不误收敛。**轮询面成员资格必须随资源生命周期收敛**：资源进终态（进程退出/会话收割）后不得再因「被选中/被展示」留在固定间隔轮询集合；如需终态后补采尾部数据，登记一次性补拉，用完即清。
status: guarded
first_seen: 2026-11-05
recurrences: ["2026-11-05", "2026-11-23"]
cases: [2026-11-05-smart-terminal-dead-session-poll-404, 2026-11-23-sems-stop-no-feedback]
allowlist: []
signature:
  - {id: S1, kind: test, path: "ForgeSelf.Web/e2e/plugins/smart-terminal/smart-terminal.spec.ts"}
  - {id: S2, kind: test, path: "Plugins/Sems/web/src/workbench/stop-feedback.spec.ts"}
guards:
  - {level: G2, path: "ForgeSelf.Web/e2e/plugins/smart-terminal/smart-terminal.spec.ts", status: active}
  - {level: G2, path: "Plugins/Sems/web/src/workbench/stop-feedback.spec.ts", status: active}
lens:
  - 新增轮询/定时刷新点时：catch 是否区分终态（404/410 资源消失）与瞬时（网络/5xx）？终态是否终结轮询并登记 UI 失效？
  - async 请求是否全链有 catch 兜底（裸抛经 onMounted 链会变成反复 toast / unhandled rejection）？
  - 恢复型数据源（快照/缓存 id）在首次终态失败时是否标记失效（置灰/提示一次），而非静默保留在轮询面？
  - 轮询面 catch 中「静默 return」是否等于无限吞错？运行中任务的 404 不得只 return——必须向权威数据源核对一次后收敛（状态/留痕/登记失效）或明确维持轮询，二选一，禁止第三种「什么都不做」。
  - （2026-11-23 输入4 增补）轮询面「入选条件」是否含资源状态？终态资源（任务已结束/进程已退出）不得因选中态、展示态留在固定间隔轮询集合；终态后补采尾部数据必须是一次性登记（用完即清），不是持续轮询的理由。
  - （2026-11-23 输入4 增补）审查轮询面时同时问一句：这个请求「为谁服务」——若答案随资源终态而消失（如已结束任务的日志增量），轮询条件必须同样消失；跨插件/宿主级的常驻心跳（如主壳收件箱）另按其协议语义单独评审，不得互相类推。
---

# PC-024 前端轮询面错误处理缺失

## 1 不变量与诱因

轮询型请求的错误处理与一次性请求不同：终态错误（资源已不存在，如 404）若被静默吞掉或仅记录，定时器会以秒级周期无限重试（网络面板 404 每秒重复），错误提示在 onMounted 调用链上反复弹出。诱因有四：① catch 写成条件分支（`if (reset) noteError(e)`），非 reset 路径静默吞错；② 部分请求有 catch、部分裸抛的混合态，裸抛者把「单次失败」放大成「反复 toast」；③ 恢复型快照把服务端已消失的 id 塞回活跃轮询面，前端无失效标记机制；④ **「防启动瞬态误收敛」的守卫写过头**——为避免把「刚启动、缓冲区未建」误判为死资源而对 404+运行中直接 `return`，把「继续轮询」的善意变成了「永不收敛」的死循环（2026-11-23 sems 复发即此形态，见 §5）。

## 2 特征签名与扫描方法

网络面板：同 URL 4xx 每周期重复 + 错误 UI 反复出现 + 对应元素永不变更状态。代码面：grep 轮询入口（setInterval 回调链上的 api 调用）的 catch 形态，找「条件吞错」「无 catch」「终态分支只 return 不收敛」三类。修复采用登记-终止模式（deadSessions Set + 各入口早退 + UI 置灰 + 提示一次），无 404 路径的周边轮询点补防御 catch。终态判定需要二次核实的（如 404 可能是启动瞬态）：向权威数据源核对一次，核对失败按「仍在」处理。

## 3 canonical fix 与守卫

见事件档案与 SmartTerminalView.vue 实现（deadSessions/isSessionDead404/markSessionDead）。守卫 G2 = e2e「dead session restored」：S-R 注入不存在 id → 断言 banner 一次 + 标签 dead + 3s 零新增 404（红/绿双验证：无修复红灯 banner-dead 不可见，修复后 2/2 绿）。2026-11-23 增补 Sems 形态守卫：`Plugins/Sems/web/src/workbench/stop-feedback.spec.ts` —— 运行中 404 + 运行列表无会话 → 必须收敛（状态 done + 留痕一次 + 登记失效 + 再选中零请求）；运行列表仍有会话（启动瞬态）→ 维持轮询不误收敛。

## 4 事件档案

- [2026-11-05 SmartTerminal 死会话轮询 404](../cases/2026-11-05-smart-terminal-dead-session-poll-404.md)
- [2026-11-23 sems 停止无反馈（日志 404 吞错轮询 → 任务卡运行中 → 停止 404 无反馈）](../cases/2026-11-23-sems-stop-no-feedback.md)

## 5 复发记录

- **2026-11-23（sems 工作台）**：任务页日志轮询对「404 + 运行中」直接 return（原意为容忍启动瞬态），宿主/插件重启后日志缓冲随之消失 → 命令永远停在「运行中」→ 停止按钮可点 → 停止接口 404 → 前端只弹「停止失败」且状态不收敛，整链无有效反馈。修复：404+运行中改为一律向 `/api/runs` 核对一次，会话不在则收敛（markTaskDone + 留痕一次 + logUnavailable 登记失效），会话在则维持轮询；同时停止链路改为可区分三态（stopped/already-stopped/failed），每个结果都有反馈（详见 case 档案）。
- **2026-11-23 输入4（sems 工作台，同日第 2 发=守卫逃逸）**：任务收敛为终态（停止/异常退出）后，「选中任务轮询面」仍按 1.2s 永续轮询其日志（`ensureTaskPolling` 入选条件只看失效登记、不看状态，workbenchStore.ts 原 :748/:758）——终态日志缓冲已定，轮询产物为零；用户在网络面板看到日志请求不断发出而质疑。**逃逸分析**：本类 G2 守卫（stop-feedback.spec.ts）断言了「404+运行中收敛」与「停止四路反馈」，但未断言「终态后轮询面成员资格收敛」——守卫只测了**收敛动作**，没测**收敛之后的循环条件**。修复：`tailPull` 一次性补拉登记（终态后恰一针抓后端尾部留痕，用完即清）+ 选中面条件加「非终态或待补拉」；新 G2 用例「终态任务必须退出常驻轮询面」双验证（修复前终态选中任务 5 tick 6 次请求，修复后 ≤1 次即停）。case：`docs/11-troubleshooting/cases/2026-11-23-sems-stop-no-feedback.md` §走查发现后追加节 + mini-task `docs/ai/pilot/2026-11-23-sems-log-poll-converge/`。同场澄清：`GET /api/smart-terminal/inbound`（2s）为宿主主壳收件箱（跨进程「开终端」信箱，空=正常），属设计内常驻轮询，非本类缺陷。
