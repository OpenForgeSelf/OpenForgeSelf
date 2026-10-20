---
id: PC-004
title: Agent 委派「成功回报」≠ 进程真实退出/终态一致（候选，待定性）
tags: [agent-delegation, state-sync, process-lifecycle]
invariant: 委派链路的任何「成功」状态必须由可证事实推出（进程退出码 + 回报内容 + 终态落库三者闭合）；回报优先标记非终态、终态保护不得被后到事件覆盖——是否升格为通用状态机不变量**待读码确认**
status: open
first_seen: 2026-10-08
recurrences: []
cases: []
signature: []
allowlist: []
guards:
  - {level: none, todo: "todo↔AgentHub 状态不同步", expires: 2026-12-31, status: planned}
lens:
  - 本次链路里的「成功」是谁说了算？回报方自证还是宿主核验（退出码/存活探测）？
  - 回报与进程退出乱序到达时，状态机的终态保护（IsTerminal）还成立吗？
---
# PC-004 Agent 成功回报 ≠ 进程真实退出（候选）

> **候选档位说明**：本条源自 2026-10-08-todo-chain-close 07 报告 Problems Found「AgentHub 成功回报 ≠ 进程退出（转 todo-ux-state 批次）」。用户反馈「AgentHub 踩的坑在 todo 又发生」存在两个候选（本条 与 PC-001/PC-005），**尚未定论哪次复发对应本条**；PILOT-057B 的修复（回报优先 + finally IsTerminal 保护）是否属通用状态机问题，需读 AgentHub/TodoTracker 两侧状态机代码确认后升格或并入。出处等级：Inferred（报告文字），未按本类模板归档事件。

## 1 不变量与诱因

已知症状：opencode 子进程静默挂起时 CliTransport 超时不触发（已修）；回报 stageTo=Review 时任务被标 Completed 与真实终态竞态（PILOT-057B 已加 IsTerminal 保护）。疑点：同类「乐观回报即成功」的判定是否还散布在委派/工作流其他节点（WorkflowEngine 执行状态、AIAgent run 卡片）。

## 2 特征签名与扫描方法

待定性感性结论后再写签名（防止把猜测当事实——规范 §6.2）。候选签名：扫 `Completed|Succeeded` 赋值点是否全部经终态保护函数。

## 3 Canonical fix

参考 PILOT-057B：回报回调只标非终态（Review/Running），`RunAsync` finally 仅在非终态时落 Failed/Timeout 终态。

## 4 守卫台账

| 等级 | 载体 | 何时触发 | 覆盖盲区 |
|------|------|----------|----------|
| none（带期限） | TODO「todo↔AgentHub 状态不同步」残留①（真机成功回报复验未触达竞态） | — | 全量 |

## 5 案例列表

| case | 日期 | 现象一句话 | 是否逃逸 |
|------|------|------------|----------|
| （待归档：定性后补） | — | — | — |
