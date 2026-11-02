# 01 Intent — B2 结果硬化（filesChanged / Verify 闸门 / 结果收据）

> 上游：[00-campaign-workflow.md](../00-campaign-workflow.md) B2 节（闸门1 已批：用户已令开工，本批承接战役级授权）

## 为什么做（背景）

1. **AI 自述成功 ≠ 可验证结果**。TODO 233 实证：todo id=56 五条执行记录 `filesChanged` 全空，而 agent 明明新建过文件——「做了什么」的证据在结构化字段里丢失，台账只能靠自由文本脑补。
2. **Run 没有出口验收**。计划任务跑完步骤即 Completed，无人回答「验收标准达成了吗、证据在哪」；开放任务回合结束连「本回合改了哪些文件」都没有结构化沉淀。架构真源链 `Permission→Approval→Execute→Verify→Result` 的 Verify/Result 两段在 Run 级是空的。
3. **无结果报告**。Run 结束没有「改了什么/验了什么/证据在哪」的机器可读收据，B3 任务中心与后续自动化消费都缺这块数据。

## 做什么（一句话）

把 Run 的「结果侧」补成结构化：文件清单落库不丢、出口验收（exitCriteria→Verify 阶段→证据）有记录、Run 结束有冻结收据且 UI 可读。

## 到什么程度（验收边界）

- B2①：复现测试红（带文件清单的执行记录回读 filesChanged 非空）→ 修复 → 绿；问题类闭环全套（case + PC + 守卫）。
- B2②：`RunRequest.ExitCriteria` 配了验收标准的 Run，Execute 完成后记录里可见 Verify 阶段（状态+每条判据结论+证据），SessionEvent 有 Verify 轨迹事件。
- B2③：Run 结束生成结构化收据（改了哪些文件/跑了什么验证/证据链接）并冻结（终态后不可变）；RunRecordPanel 展示收据区块。
- 门禁：§5.6 快档全绿 + 新增真库守卫；不碰 ToolRegistry.cs / ForgeSelf.Web/src / features.ts / :51888。

## 不做什么

- 不改 ToolRegistry 六闸门本体（B4）；不做 RunOrchestrator 换引擎/删 ReactLoopAgent（B5）；不做跨入口任务总览（B3）。
- 不做收据的签名/防篡改密码学冻结（「冻结」= 终态后写路径拒绝覆盖，见 02-spec §7）。
- 不做文件清单的 git 状态回读（`change` 类型沿用回报方口径，本批不引入 git 依赖）。
