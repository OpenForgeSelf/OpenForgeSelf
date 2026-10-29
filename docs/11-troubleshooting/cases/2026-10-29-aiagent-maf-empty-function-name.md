---
id: 2026-10-29-aiagent-maf-empty-function-name
title: 2.4.18 MAF 聊天路径空名工具调用毒化会话（同会话每条消息 1~2ms 必败）
pc: PC-015
severity: P1
evidence: Verified
---
# Case：2026-10-29 2.4.18 MAF 空名工具调用毒化会话

> Case 只讲「这一次发生了什么」；规则/修法/扫描方法一律引用 PC-015，不复述。

## 1 现象

用户（输入3，2026-10-29 13:15「你试试下面的请求 报错了」）在 :51888 宿主（2.4.18.2610290828，AIAgent 1.9.7 MAF 聊天路径，今晨发布）会话 `mvt30ipk-quraxi` 发「查看当前项目结构」：agent 跑完两轮工具调用后 13:08:12.948 报 `[MafTaskLoopAgent] MAF Run 失败: Value cannot be an empty string. (Parameter 'functionName')`（实例日志 `Log/2026_10_29.log`）；此后 13:08:58、13:09:42 两条「报错了」均在 1~2ms 内同错、响应长度 0。用户复制的 curl 中 message 被脱敏成 3 个空格，该形态只命中后端空消息守卫（`AIChatController.cs:217`「消息不能为空」，设计内行为），不是用户真实报错。

## 2 复现红灯

单元层红灯（TDD Red，2026-10-29 14:20）：

```
dotnet test --filter FullyQualifiedName~ForgeChatClientAdapterTests
失败! - 失败: 3，通过: 3，总计: 6
  StreamingToolCallFragments_MergeIntoSingleCompleteCall [FAIL]   （修复前逐片产出 3 条 FunctionCallContent，其中 2 条空名）
  StreamingToolCallFragments_ParallelCallsMergedInOrder  [FAIL]   （4 条）
  MapsRequest_SkipsEmptyNameToolCallsFromHistory         [FAIL]   （空名调用进了出站请求）
```

生产现象侧复现 = 会话事件表证据：`C:\Users\12504\.forgeself\ForgeSelf.db` SessionEvent `mvt30ipk-quraxi`：#1098（2 条真实调用）、#1104（真实调用 + 数十条 `{CallId:"",ToolName:"",ArgsJson:"{}"}` 空壳）、#1099-#1110（真实 tool/call+result 对）、#1120/#1128（失败 run）。

## 3 根因（5 Whys + 第 6 问）

1. 为什么抛 `functionName` 空？→ `OpenAISdkAdapter.BuildChatMessages:183` 构造 `ChatToolCall.CreateFunctionToolCall(id, "")`，OpenAI SDK 校验非空即抛。
2. 为什么请求里有空名调用？→ 会话事件 #1104 的 ToolCalls 落了空名空壳（`DeriveMessages` 原样投影回模型消息）。
3. 为什么落了空壳？→ `ForgeModelCallLogger`（MafTaskLoopAgent.cs:853-857）逐 `FunctionCallContent` 落 `ToolCallRef`，而 fcc 本身是分片。
4. 为什么 fcc 是分片？→ `ForgeChatClientAdapter.ToChatResponseUpdates:140-149` 逐 `DeltaToolCall` 产出 `FunctionCallContent`，而 OpenAI 流式续片无 id/name（参数还是 JSON 前缀，解析失败变 `{}`）。
5. 为什么会写成逐片产出？→ MAF 适配层（2.4.18 新增）把 `UnifiedStreamChunk` 当「完整事件」假设，未对齐 provider 的增量契约（老路径 `ReactLoopAgent`/`AIAgentService` 均有 `AccumulateToolCall` 聚合器）。
- **第 6 问：为什么现有流程/守卫没让我一开始就写对？** → 2.4.18 发布前测试只造了「单完整调用分片」形态用例（`ScriptedStreamProvider` 既有用例无「续片无 id/name」形态），且无「空名调用出站」断言——**测试形状缺口**，非守卫失效（此前无该域守卫）。本次以 G1 三测补齐（含续片形态与空名过滤），技能 lens 已上浮（PC-015）。

## 4 同类扫查命中表

| 位置（文件:行） | 同根因？ | 处置（已修 / 已守卫 / TODO#） |
|-----------------|----------|-------------------------------|
| Plugins/AIAgent/MafLoop/ForgeChatClientAdapter.cs:140-149（旧） | 是（本例） | 已修：聚合 + 移除逐片分支 |
| Plugins/AIAgent/Services/ReactLoopAgent.cs:423-425 | 否（已有 AccumulateToolCall:917 聚合） | 已守卫（生产验证版语义，本次对齐复用） |
| Plugins/AIAgent/Services/AIAgentService.cs:497-498 | 否（同上，AccumulateToolCall） | 已守卫（既有） |
| ForgeSelf.Api/Controllers/UnifiedAI/OpenAIChatController.cs:215-247 | 否（对外 OpenAI 协议透传：流式→流式逐片重发是正确语义） | 无需改 |
| ForgeSelf.Api/Services/AI/Providers/OpenAISdkAdapter.cs:182-184 | 否（同根因下游受害点/ fail-fast 背板） | 保留抛异常语义（PC-015 §3 被否方案），上游过滤保证空名不再到达 |
| ForgeSelf.Api/Services/SessionEventProjection.cs:52-60 | 否（忠实投影，空名过滤在出站映射层） | 无需改 |

## 5 修复与被否方案

最终修法 = PC-015 §3 canonical fix（聚合 + 双层出站过滤），参考实现 `ForgeChatClientAdapter.cs:324-384` / `:53-63`、`MafTaskLoopAgent.cs:757-773`。被否方案两件见 PC-015 §3（宿主 fail-fast 保留；`UnifiedToolCall.Index` 降 R2 候选）。

## 6 重构提案与结论

**R0（不重构）**：聚合语义统一后，`ReactLoopAgent.AccumulateToolCall` 与适配层累积器存在约 10 行同语义重复（跨插件项目树，抽共享件需动公共库归属，属 R2 级）。触发条件：出现第二处消费点或「平行调用分片交叉到达」的真实上游时，合并为共享累积器 + `UnifiedToolCall.Index` 字段（已记 PC-015 §3 候选）。无需闸门。

## 7 守卫落地

- G1：`ForgeSelf.Api.Tests/Plugins/AIAgent/MafTaskLoopAgentTests.cs` 新增三测（分片聚合、平行调用合并、空名出站过滤），status active。
- G3：PC-015 front-matter S1 签名（`scripts/check-problem-classes.ps1` 执行，pre-commit/CI 触发）：`Plugins/AIAgent/**/*.cs` 禁现 `new FunctionCallContent(call.Id` 逐片产出形态。

## 8 逃逸分析

N/A 首次。PC-013（2026-10-25）与本例同域（AIAgent 执行链）但不变量不同，非复发。

## 9 基建改进

- G1 测试形状规范沉淀：流式消费侧用例必须包含「续片无 id/name」形态（已入 PC-015 lens 第 1/4 问，设计/修复前 tags 表必查）。
- 问题类台账 +1（PC-015），`check-problem-classes.ps1` 新增一条可执行签名。

## 10 复用赋能

后续必须引用本 case/PC 的工作：① 新增流式 provider 适配器（OpenAI Responses / Ollama 等）；② MAF 循环改造/换 runtime；③ 任何新写「消费 UnifiedStreamChunk」的代码（先过 PC-015 lens 四问）。

## 11 证据

| 结论 | 等级 | 出处（文件:行 / 命令输出 / 截图） |
|------|------|-----------------------------------|
| 抛错时序与报错原文 | Verified | 实例日志 `D:\src\tools\OpenForgeSelf\versions\2.4.18.2610290828\Log\2026_10_29.log`（13:07:50 / 13:08:12.948 / 13:08:58 / 13:09:42 行） |
| 会话事件空壳落库 | Verified | `C:\Users\12504\.forgeself\ForgeSelf.db` SessionEvent #1098/#1104（只读探针；事件载荷关键行收录于 pilot `05-evidence.md` §4） |
| 红灯 3 条 | Verified | pilot `05-evidence.md` §2（逐字输出；探针日志已按 §5.0 用后即删） |
| 绿灯 28/28 | Verified | pilot `05-evidence.md` §3（ForgeSelf.Api.Tests，MAF 域四类过滤集） |
| 后端构建 0 错误 | Verified | pilot `05-evidence.md` §1 V3（337 警告=基线水位） |
| 同类扫查命中表 | Verified | 本 case §4（grep 全仓 `DeltaToolCall`/`new FunctionCallContent(` 逐点核对） |
