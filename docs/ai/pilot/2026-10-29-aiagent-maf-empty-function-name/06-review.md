# 06 Review — AIAgent MAF 空名工具调用毒化会话（PC-015 / mini-task 闭环）

> 日期 2026-10-29。Review 方式：自查（本会话）+ 机器门禁（05-evidence V1-V5 全 Verified）。状态：**APPROVED（自查）**；闸门2 用户验收与发布动作待用户拍板（见「待拍板」）。

## 八问

1. **问题解决了吗？** 是。根因链（分片逐片成调用 → 空壳落库 → 出站空名抛参异常）三层全断：聚合（治本）+ 出站双层过滤（自愈）。G1 三测红→绿，28/28 过滤集绿，build 0 错。
2. **改了什么、影响面？** `ForgeChatClientAdapter.cs`（聚合 + 移除逐片分支 + ToUnifiedMessages 空名过滤 + ParseArguments 转 public）、`MafTaskLoopAgent.cs`（ToChatMessage 空名过滤）、`MafTaskLoopAgentTests.cs`（+3 测）、PC-015/case/三索引文档。影响面 = MAF 聊天路径的流式工具调用消费；老路径（ReactLoop/AIAgentService）与协议透传未动且有回归测试护航。
3. **有没有改出口径？** 无。`UnifiedStreamChunk` 契约未动；对外 API/SSE 契约未动；唯一可见行为变化 = 修复本身（工具调用不再产生空名分片；工具调用参数历史恢复完整——修复前 ArgsJson 落库为 `{}`）。
4. **同类风险清了吗？** 全仓 `DeltaToolCall` 消费点 4 处逐一核对（case §4）：2 处老路径已有聚合器、1 处协议透传为正确语义、1 处即本例已修；投影响层为忠实投影（过滤在出站层）。S1 签名绊线 + lens 四问已入 PC-015 台账。
5. **测试是真的吗？** Verified（05 §2/§3 逐字输出）；红灯在修复前实测 3 FAIL，非摆拍。测试形状含「续片无 id/name」关键形态（本次缺陷的形状缺口已闭合）。
6. **代价与收益？** 代码净增约 40 行（聚合器 + 过滤 + 注释）；无新依赖；无迁移。收益 = 会话永久毒化（用户级不可恢复症状）根除 + 工具调用参数历史完整。
7. **遗留？** ① 平行调用分片交叉到达场景依赖上游按序发送（与老路径同限，`UnifiedToolCall.Index` 为 R2 候选，无实测需求不立项）；② 聚合器与 ReactLoopAgent.AccumulateToolCall 约 10 行同语义重复（R0，合并条件记 case §6）。均落 PC-015/case，无悬空教训。
8. **要不要别人再审？** 建议用户验收时重点看两处：`ForgeChatClientAdapter.cs:324-384`（聚合 + 产出时机：流结束后统一产出，finish 更新先于调用更新，M.E.AI 累积序无关）与被否方案取舍（宿主 fail-fast 保留）。

## Final Decision

**APPROVED**（自查；闸门2 待用户验收后按 §8.5 走发布请示——不自行提交/发布）。

## 待拍板（提交用户）

1. 是否发布修复版（本地 zip `release-local.ps1 -Version 2.4.18 -Sign`，或等下一版本合并发布）——**默认推荐：发布**（用户 :51888 实例的会话毒化属用户可感缺陷，修复含旧会话自愈）。
2. 闸门2 验收后是否 git 提交——**默认推荐：提交**（工件链已齐，pre-commit 工件门禁可过）。
