# AI-Native Pilot Result（最终汇报）— MAF 空名工具调用毒化会话（PC-015）

> 轻量缺陷闭环。日期 2026-10-29。状态只报 05-evidence 里实际跑过的结果。

## 1. Repository Understanding

我确认了：AIAgent 聊天路径在 `UseMafRuntime` 下由 `MafTaskLoopAgent` 经 `ForgeChatClientAdapter` 消费 OpenAI 流式增量；老路径 `ReactLoopAgent` 已有 `AccumulateToolCall` 聚合器。本缺陷是 MAF 适配层把每个工具调用分片当成一条完整调用。详见 `mini-task.md` §1。

## 2. Selected Task

修复 MAF 流式工具调用的分片聚合，并过滤历史空名调用，使毒化会话可以继续对话。工件目录 `docs/ai/pilot/2026-10-29-aiagent-maf-empty-function-name/`。

## 3. Changed Files

- `Plugins/AIAgent/MafLoop/ForgeChatClientAdapter.cs`：分片聚合后每调用产出一条完整 `FunctionCallContent`；出站跳过空名调用。
- `Plugins/AIAgent/MafLoop/MafTaskLoopAgent.cs`：`ToChatMessage` 跳过空 `ToolName`。
- `ForgeSelf.Api.Tests/Plugins/AIAgent/MafTaskLoopAgentTests.cs`：分片聚合与空名过滤回归。
- 问题类：`docs/11-troubleshooting/problem-classes/PC-015-maf-stream-toolcall-fragment.md`、`docs/11-troubleshooting/cases/2026-10-29-aiagent-maf-empty-function-name.md` 及三处索引。

## 4. Validation

Build: `dotnet build`（ForgeSelf.Api）0 错误（337 警告，既有基线）✅ Verified（05 V3）。

Unit Test: 修复前 `ForgeChatClientAdapterTests` 失败 3 / 通过 3 ✅ Verified（05 V1）；修复后 MAF 域四类过滤集失败 0 / 通过 28 / 总计 28 ✅ Verified（05 V2）。

问题类扫描: `check-problem-classes.ps1` 15 个 PC 通过 ✅ Verified（05 V4）；`ProblemClassRegistryTests` 5/5 ✅ Verified（05 V5）。

E2E: 无 UI 变更，未跑。

## 5. Evidence

`05-evidence.md`。生产侧：2.4.18 日志 `functionName` 空字符串，会话 `mvt30ipk-quraxi` 的 SessionEvent #1104 含数十条空壳工具调用。

## 6. Review

`06-review.md` Final Decision = **APPROVED**（自查）。2026-10-29 用户指令「能提交的先提交」，本目录随该批入库。

## 7. Risk

L1。平行分片交叉到达仍与老路径相同，依赖上游按序发送。`:51888` 只读复验未在本任务重跑，仍挂在 TODO「MAF Agent 循环」条目，以 2.4.19 / ai-agent 1.9.8 为准。

## 8. Problems Found

1. 空名分片毒化会话（本缺陷，PC-015）。
2. 聚合器与 `ReactLoopAgent.AccumulateToolCall` 有约 10 行同语义重复（R0，记在 PC-015 case §6）。
3. 平行调用分片交叉到达未单独立项（R2 候选，无实测需求，记在 PC-015）。

## 9. Process Evaluation

| 环节 | 评价 |
| --- | --- |
| Repository Understanding | PASS |
| Intent → Spec | PASS |
| Spec → Plan | PASS |
| Plan → Code | PASS |
| Code → Test | PASS（先红后绿） |
| Test → Evidence | PASS |
| Evidence → Review | PASS |

## 10. 问题类归档

PC-015（新建，首见）+ case：`docs/11-troubleshooting/problem-classes/PC-015-maf-stream-toolcall-fragment.md`、`docs/11-troubleshooting/cases/2026-10-29-aiagent-maf-empty-function-name.md`。

## 11. 重构提案

R0：聚合器与老路径重复约 10 行，第二条传输或第二次复制出现前不合并。R2 候选 `UnifiedToolCall.Index` 无实测需求，不在本批立项。两条都写在 PC-015，不另开实现。

## 12. 悬空教训清单（必须为空）

（空。运行实例复验已在 TODO.md「MAF Agent 循环」条目中，复验口径以 2.4.19 / ai-agent 1.9.8 为准。）
