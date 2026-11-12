---
id: PC-024
title: 前端轮询面错误处理缺失——静默吞 404（终态错误继续轮询）+ 无 catch 裸抛反复 toast
tags: [frontend, polling, error-handling, 404, session-restore]
invariant: 轮询型请求的 catch 必须区分终态与瞬时错误：404 等终态必须终结该资源后续轮询、登记失效并仅提示一次；async 请求链禁止无 catch 裸抛；恢复型快照（sessionStorage/localStorage）注入的 id 首次终态失败必须标记失效，不得长期留在活跃轮询面。
status: guarded
first_seen: 2026-11-05
recurrences: []
cases: [2026-11-05-smart-terminal-dead-session-poll-404]
allowlist: []
signature:
  - {id: S1, kind: test, path: "ForgeSelf.Web/e2e/plugins/smart-terminal/smart-terminal.spec.ts"}
guards:
  - {level: G2, path: "ForgeSelf.Web/e2e/plugins/smart-terminal/smart-terminal.spec.ts", status: active}
lens:
  - 新增轮询/定时刷新点时：catch 是否区分终态（404/410 资源消失）与瞬时（网络/5xx）？终态是否终结轮询并登记 UI 失效？
  - async 请求是否全链有 catch 兜底（裸抛经 onMounted 链会变成反复 toast / unhandled rejection）？
  - 恢复型数据源（快照/缓存 id）在首次终态失败时是否标记失效（置灰/提示一次），而非静默保留在轮询面？
---

# PC-024 前端轮询面错误处理缺失

## 1 不变量与诱因

轮询型请求的错误处理与一次性请求不同：终态错误（资源已不存在，如 404）若被静默吞掉或仅记录，定时器会以秒级周期无限重试（网络面板 404 每秒重复），错误提示在 onMounted 调用链上反复弹出。诱因有三：① catch 写成条件分支（`if (reset) noteError(e)`），非 reset 路径静默吞错；② 部分请求有 catch、部分裸抛的混合态，裸抛者把「单次失败」放大成「反复 toast」；③ 恢复型快照把服务端已消失的 id 塞回活跃轮询面，前端无失效标记机制。

## 2 特征签名与扫描方法

网络面板：同 URL 4xx 每周期重复 + 错误 UI 反复出现 + 对应元素永不变更状态。代码面：grep 轮询入口（setInterval 回调链上的 api 调用）的 catch 形态，找「条件吞错」与「无 catch」两类。修复采用登记-终止模式（deadSessions Set + 各入口早退 + UI 置灰 + 提示一次），无 404 路径的周边轮询点补防御 catch。

## 3 canonical fix 与守卫

见事件档案与 SmartTerminalView.vue 实现（deadSessions/isSessionDead404/markSessionDead）。守卫 G2 = e2e「dead session restored」：S-R 注入不存在 id → 断言 banner 一次 + 标签 dead + 3s 零新增 404（红/绿双验证：无修复红灯 banner-dead 不可见，修复后 2/2 绿）。

## 4 事件档案

- [2026-11-05 SmartTerminal 死会话轮询 404](../cases/2026-11-05-smart-terminal-dead-session-poll-404.md)