---
id: 2026-11-20-oar-early-exit-and-bundled-fake
title: OarTransport 50ms 假 Exit + handlers 缺 installation 假 bundled
pc: PC-037
severity: P1
evidence: Verified
---
# Case：2026-11-20 OAR early Exit + bundled fake

## 1 现象
Host 任务可在 vendor text_delta 入 EventStore 前标 Succeeded；runtime.installation 在无 probe 时返回 available/bundled。

## 2 复现红灯
旧源码 Delay(50)+Exit(0)；handlers !installation→bundled。守卫测试未修前红。

## 3 根因（5 Whys + 第 6 问）
1. Succeeded 过早 → Exit 在 accept 后短 sleep。
2. sleep 当冲刷完成。
3. 未接 session.status / turn_ended。
4. 缺 installation 当 scripted 特例捏造 bundled。
5. 不变量：无证据不得 available/Succeeded。
- 第 6 问：Known Limitation 无守卫强制。

## 4 同类扫查命中表
| 位置 | 同根因？ | 处置 |
|------|----------|------|
| OarTransport Delay(50) | 是 | 已修 |
| handlers bundled fallback | 是 | 已修 |
| 产品 scripted omit | 异 | 保持 |

## 5 修复与被否方案
Host WaitUntilSettled；handlers not_found。否决：加长 sleep；Worker promptAndWait（堵 abort）。

## 6 重构提案与结论
R0。

## 7 守卫落地
G1 TurnGate + hold；G2 handlers/OarTransport 源码守卫；PC-037。
