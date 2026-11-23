---
id: 2026-11-20-mock-provider-silent-divert
title: Mock 配置 400 无感 / resume 被默认模型接管
pc: PC-039
severity: P1
evidence: Verified
---
# Case：2026-11-20 Mock 提供方静默改道族（PC-039）

## 1 现象
Mock 配置下对话请求被转发管道吃掉报 400 无感；会话 resume 后被宿主默认模型静默接管真 LLM。

## 2 复现红灯
双接缝单决策点断言 + ChatModelId 快照贯通测试红（灾前实跑；随码丢失）。

## 3 根因（5 Whys + 第 6 问）
1. Mock 请求 400 → 被 ForgeChatClientAdapter 转发管道处理。
2. 管道不该接 Mock → Create 缺单一决策点。
3. resume 接管 → AgentRun 未落 ChatModelId 快照。
4. Create/Resume/Restart 三处读宿主默认模型 → 快照缺列。
5. 不变量：Mock 直连/管道单一决策点；会话模型身份必须快照贯通。
- 第 6 问：无守卫强制决策点唯一——G1 灾前已立，灾后随码丢失。

## 4 同类扫查命中表
| 位置 | 同根因？ | 处置 |
|------|----------|------|
| ForgeChatClientAdapter.Create | 是 | 灾前已修（码随批丢失） |
| AgentRun Create/Resume/Restart | 是 | 灾前已贯通（码随批丢失） |

## 5 修复与被否方案
Create 统一「Mock 直连 / 非 Mock 走管道」；AgentRun 落 ChatModelId 列三处贯通。否决：各调用点各自判 Mock（多点决策重蹈）。

## 6 重构提案与结论
R0（单一决策点即结构修复）；守卫重建随代码级还原批。

## 7 守卫落地
G1 双接缝/快照测试（灾前 active；2026-11-23 随码丢失，status: open；重建见 PC-039 guards todo）。