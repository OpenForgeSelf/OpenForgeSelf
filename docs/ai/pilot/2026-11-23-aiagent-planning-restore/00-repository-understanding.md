# 00 Repository Understanding（灾后重建件 · 2026-11-23）

- 技术栈/架构确认来源：本 clone 实读（.NET 10 宿主 + NewLife.XCode + 插件架构；AIAgent 插件 `Plugins/AIAgent/`，前端 `Plugins/AIAgent/web` Vue3+vitest）。
- 关键事实（实读行号为准）：
  - `RunOrchestratorService` 原生运行路径已具备 LLM 规划（`_planGenerator.GeneratePlanAsync`，DI 装配），但 UI 会话面（`AgentFacadeService`）的 planned 模式只发静态 `BuildDraftPlan` 草稿，**规划器从未接到 facade 路径**。
  - `AgentFacadeRunExecutor` 构造签名（本批前）= facade/ctx/agentService/orchestrator/modelResolver/registry，无 planGenerator 形参 —— PC-040 指认的组合根漏装配面。
  - `IPlanGeneratorService.GeneratePlanAsync(AgentRun, workflowId, chatModelId, ct)` 契约完整可用（submit_plan 闭环 + 20s 无进展超时 + 草稿/单步回退，PlanGeneratorService.cs）。
  - `AgentPlanDto`（facade 计划 DTO）无 note 字段；`plan.proposed` 事件载荷即该 DTO；前端 session.logic 归一化 plan 卡（无 note 透传）。
- 灾后边界：丢失批次（1.10.1/1.10.2）的行为规格以 plugin.json 描述与 05 §8 存证为唯一依据，本批按意图重实现，不逐字节还原。
