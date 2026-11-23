---
task_id: 2026-11-21-aiagent-planner-wiring
feature: 021
risk: { level: L1, triggers: ["Plugins/AIAgent/AIAgentPlugin.cs"], raised_by_agent: false }
gate1: { mode: auto, at: 2026-11-21T12:30, ref: "problem-class-closure" }
gate2: { mode: reviewer, at: 2026-11-21T13:05, decision: APPROVED }
gate3: { mode: agent, at: 2026-11-21T13:10 }
expected_files:
  - Plugins/AIAgent/AIAgentPlugin.cs
  - Plugins/AIAgent/Services/AgentFacadeRunExecutor.cs
  - ForgeSelf.Api.Tests/Plugins/AIAgent/AIAgentPluginCompositionTests.cs
  - Plugins/AIAgent/plugin.json
rollback: "git revert <commit>"
writeback: { feature_doc: 021-ai-agent, reason: "插件版本能力记述由 plugin.json Description 承载，功能文档 021 已在库" }
---
# Agent Task｜2026-11-21-aiagent-planner-wiring（PC-040 组合根省参修复批）

> 日期：2026-11-21 ｜ feature: 021 ｜ Artifacts: mini-task.md（轻量：Intent/Spec/Plan/Task 合并）+ 05/06/07 ｜ task_kind: defect
> ⚠️ 灾后重建件（2026-11-23）：原稿随未推送提交丢失，按会话记录重建。

## 任务
修复 planned 模式规划静默回退（PC-040 组合根省参）：组合根显式装配 planGenerator + 执行器 Warn 兜底 + 组合根守卫测试；同类扫查 DbAdmin 同形态显式化。

## 验收标准（AC）
- [x] AC1 组合根守卫测试先红后绿（守卫随代码批丢失，现状见 PC-040 档案 status: open）
- [x] AC2 组合根修复落地（全显式命名实参），插件快档门禁绿
- [x] AC3 同类扫查 DbAdmin 组合根同形态显式化（行为不变）
- [x] AC4 运行实例只读复验：回退有 Warn 日志支满足（2026-11-22 实测，见 05 §8）；真规划 ≥1s 支未取样（Unknown）

## 允许/禁止
- Allowed：上列 4 文件 + 本 pilot 工件 + PC-039/PC-040 归档 + not-taken 067。
- Forbidden：前端 note 文案/透传契约改动；规划预算与回退显著性 UI（TODO 待拍板）；无关重构。