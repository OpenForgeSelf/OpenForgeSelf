# 07 Final Report — goal-sse-evidence-fix（D1/D2，并入 PILOT-077）

> 任务：修复目标模式 SSE 事件映射（D1）与步骤工具轨迹证据链（D2）两个验收缺陷。
> 2026-10-30 21:2x 并入全量批次 PILOT-077（`docs/ai/pilot/2026-10-30-aiagent-loop-defect-fix-batch/`），载体提交 e55d2a4；本目录为闸门3 工件链补录。

## 1 交付摘要

- **D1**：`AgentRunsController.WriteSseEventAsync` payload 白名单 +2 case（goal_round_started / goal_evaluated → `{type, payload}` 封套），前端事件流不再 TypeError 断流。
- **D2**：`RunOrchestratorService` 帧循环聚合工具轨迹落库 + `GoalPursuitService` 评估提示词携带步骤产出与工具轨迹摘要，目标评估不再因零证据误判 Stuck。
- 插件 ai-agent 1.9.8 → 1.9.9（随 PILOT-077 批发布）。

## 2 Validation / Review

- **Validation**（详见本目录 05-evidence，来源等级逐条标注）：GoalRunSseEvidenceTests 红 3/9 → 绿 9/9；AIAgent 过滤集 120 通过零新增红；冒烟 e2e 7/7；真机 Run 3/4 走查（goal 事件渲染 + Run 4 轨迹落库）。
- **Review**：PILOT-077 06-review 八问 + Final Decision = APPROVED；本补录 06 同结论（工件链收口，零代码改动）。

## 3 问题类归档（problem-class-closure）

- D1 归「SSE 契约漂移：新增事件类型未同步消费侧封套映射」——**未独立建 PC**：单例缺陷 + 封套守卫测试即档案（PILOT-077 07 §3 同口径），复发再评估建类。
- D2 归 PILOT-077 批档案（工具轨迹断链类，证据链三层守卫：单测 → 评估提示词断言 → 真机 Run 4）；台账现状 `scripts/checkblem-classes.ps1` 19/19 OK。

## 4 重构提案（小范围，未实施，回流 TODO）

- TurnFrame.ToolCompleted 携带结果摘要，评估证据含结果全文（宿主 Abstractions 侧增强，P2/TODO1）。
- MafTaskLoopAgent harness 补工具轨迹聚合单测（P2/TODO2）。

## 5 悬空教训清单

- 无——发现的全部问题均已落档：三条 TODO 均有归属与优先级（PILOT-077 07 §6），本补录零新增悬空。
