---
id: 2026-11-12-llm-audit-blocks-response
title: 记录 ChatTurn 时 SQLite 重建表，网关响应被拖住
pc: PC-030
severity: P1
evidence: Verified
---
# Case：2026-11-12 网关旁路记录挡住响应

## 1 现象

用户原文：记录请求内容报错或记录失败就导致没响应。期望先返回响应，记录失败也不影响主流程，并且不能拖慢。日志：`SQLite需要重建表，因无法执行：Alter Table ChatTurn Alter Column RequestHeaders text NULL COLLATE NOCASE; Alter Table ChatTurn Alter Column ResponseHeaders text NULL COLLATE NOCASE`。

## 2 复现红灯

`ChatCompletions_SlowSessionUpsert_DoesNotHoldResponse`（修复前的测试名）把会话写入故意拖 3 秒。结果：`Expected sw.Elapsed to be less than 800ms, but found 3s, 52ms`。日志 `.temp/tmp/llm-audit-red.log`。

## 3 根因（5 Whys + 第 6 问）

1. 为什么客户端没响应？→ 动作没返回，HTTP 响应没结束。
2. 为什么动作没返回？→ 正文写完后仍在 `await` 旁路落库。
3. 为什么落库这么久？→ XCode 改不了那两列，转而同步重建 `ChatTurn`。
4. 为什么重建跑在请求上？→ 记录写在 `ForwardAsync` 返回之前。
5. 为什么 catch 没挡住？→ 重建是堵住，不一定先抛；抛了也被服务吃掉，但 `await` 要等它吃完。
- **第 6 问：** PC-029 只守了「超长也能写入」。注释里的「不影响接口响应」没有延迟断言，所以 await 一直留在热路径上。

## 4 同类扫查命中表

| 位置 | 同根因？ | 处置 |
|------|----------|------|
| `LlmProxyHttp.ForwardAsync` 原 `TryRecordAsync` | 是 | 已改为入队 |
| 三个 UnifiedAI 控制器 | 是 | 只注入 `ILlmTurnAudit` |
| `ChatController` 投影 / 会话补写 | 否 | 返回前对齐读模型，注释要求不要火后即忘。另记 TODO |
| AIAgent `SyncProjectionSafeAsync` | 否 | 同上 |
| `ChatTurnService` 的 catch | 否 | 只负责不往外抛，解除不了 await 阻塞 |

## 5 修复与被否方案

修法见 PC-030。被否：只加 catch。红灯证明慢保存即使最终成功，响应也被拖住。

## 6 重构提案与结论

R0。队列只服务这一条旁路，不把应用内投影一起改成异步。

## 7 守卫落地

G1 行为测试 + G2 热路径源码扫描。首次发生，不做逃逸分析。
