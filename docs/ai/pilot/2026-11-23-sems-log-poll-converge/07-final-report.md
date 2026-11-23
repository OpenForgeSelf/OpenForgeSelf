# Final Report — sems 终态任务被选中态轮询拖住修复（输入4 2026-11-23）

## 结论

**COMPLETED** — 终态任务退出常驻轮询面（收敛后最多补拉一针），`/api/smart-terminal/inbound` 判定为宿主设计内心跳非缺陷；vitest 55/55、构建 exit=0、插件包 2611231800 已投递。

## 需求回执

- 用户输入4 原文：「那样吗一直发出上面的两个请求怎么整，运行命令都停止了，异常退出了，还在轮询，这两个请求有什么用」。
- 请求1（`GET /api/commands/{id}/logs`）：**已修**——AC-F09-8（AC-T4）红→绿；不变量「终态后不进常驻轮询集合」入 PC-024。
- 请求2（`GET /api/smart-terminal/inbound`）：**不修（有据）**——宿主主壳收件箱设计内心跳（真源 `Plugins/SmartTerminal/docs/03-架构与接口.md`）；优化候选（空闲退避/WS/SSE）记 TODO。

## 验证（全部 Verified）

| 项 | 结果 |
|---|---|
| 红灯（修复前） | 2 failed / 53 passed（终态选中任务 5 tick 发 6 次请求） |
| 绿灯（修复后） | vitest **55/55**（3.6s） |
| 插件构建 | exit=0，dist/index.js 171051B（+81B） |
| 真源回写 | AC-F09-8 / T-F09-S7 / PC-024 / case 追加节 |
| 机检 | check-problem-classes OK；hygiene -Staged PASS |
| 交付 | `软件工程管理系统-1.3.0.2611231800.zip` → OpenForgeSelfPluginUpdates |

## 问题类归档 / 重构提案 / 悬空教训（三节）

- **问题类归档**：PC-024 第 4 发复发已记（case `2026-11-23-sems-stop-no-feedback.md` 追加节）；本轮同时清理了该 PC 前置数据的悬空引用（`cases:`/`recurrences:` 归一）。
- **重构提案**：无（轮询面条件最小修复，tailPull 集合 15 行内，无结构性债务）。
- **悬空教训**：G2 守卫落点是「404+运行中」与停止四路，**选中态循环条件**是逃逸面——第 5 发若再现，优先给 `ensureTaskPolling` 循环条件直接上状态机守卫而非再补一例。

## Validation / Review（验证复核）

Review（06-review.md）Final Decision = **APPROVED**（自审 2026-11-23）：八问全过；本节上表为 Validation 汇总，全部条目为 Verified 等级；残余风险（补拉一针可吃一次 404）已在 06 显式记录并接受。

## 下一步 / 遗留

- 宿主 `useTerminalInbox` 2s 常驻轮询优化（空闲退避/WS/SSE）→ TODO（P3，另立批，涉及宿主源码）。
