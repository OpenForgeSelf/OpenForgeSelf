---
id: 2026-11-21-composition-root-arg-omission
title: planned 模式恒回退：组合根漏传 planGenerator 静默降级
pc: PC-040
severity: P1
evidence: Verified
---
# Case：2026-11-21 组合根省参漏装配（PC-040）

## 1 现象
planned 模式所有会话恒「规划失败，已回退计划」；界面无解释；规划器装配路径零日志（静默降级）。

## 2 复现红灯
组合根守卫测试（real-Apply 装配图断言 + 显式 arity 枚举）：修复前红——装配图缺 planGenerator。运行实例侧证：规划事件存在但规划器装配路径零日志（2026-11-22 实测见 pilot 05 §8）。

## 3 根因（5 Whys + 第 6 问）
1. 规划恒回退 → executor 拿到的 planGenerator=null。
2. null → 组合根未传。
3. 未传 → 新增可选参数后调用点未同步（C# 省参合法零警告）。
4. 无警告无日志 → 静默降级不可见。
5. 不变量：组合根构造必须全显式命名实参；可选参 null 降级必须留痕。
- 第 6 问：无类级守卫强制 arity 显式——G1 灾前已立；2026-11-23 随码丢失待重建。

## 4 同类扫查命中表
| 位置 | 同根因？ | 处置 |
|------|----------|------|
| AIAgentPlugin → AgentFacadeRunExecutor | 是 | 灾前已修（码随批丢失） |
| DbAdminPlugin → DbConnectionService | 是 | 灾前已显式化（码随批丢失） |
| 其余组合根 new 调用点 | 否（已显式） | — |

## 5 修复与被否方案
组合根 GetService 解析 + 全显式命名实参；执行器 Warn 兜底。否决：「实参数 ∈ 构造参数数集合」守卫规则（对本形态直接放行，见 not-taken 067②）。

## 6 重构提案与结论
R1：跨插件组合根显式 arity 守卫（G2），随代码级还原批落地。

## 7 守卫落地
G1 组合根守卫测试（灾前 active；2026-11-23 随码丢失，status: open；重建 todo「PC-040 守卫重建」）。