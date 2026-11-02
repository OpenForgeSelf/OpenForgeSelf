# AgentCoordinator 启发式旁路 · 裁决留痕（AC7）

> B5 ③｜呈报：2026-11-02 23:5x（verify-dev → lead）｜拍板：lead seq8（同日）｜状态：**已拍板 = 删除**

## 1. 裁决链路证据（00-repository-understanding §1.3 F13-F19）

- 链路：`POST api/ai-agent/agents/plan|execute|execute-task|handle`（AgentsController :66/:73/:102/:80）→ AgentCoordinatorService（关键词启发式 `IsSimpleRequest` :44 + AI 规划 + 兜底计划）→ AgentExecutorService.ExecuteTaskAsync(:32) → `_aiAgentService.ChatAsync(messages, useTools)`(:278) = **ad-hoc 无日志工具循环**（架构废弃扩面「无会话日志；不得承载新产品能力」）。
- UI 零消费：store 三方法（createPlan/executePlan/handleRequest，stores/agent.ts:82-118）无任何视图调用；/agents 页仅 loadAgents 列人设。execute-task / getActiveInstances 同样零视图调用。
- 插件/IM/Scheduler 零消费（grep 仅 AgentsController + AIAgentPlugin DI :45-46）。
- **安全缺口实锤**：ad-hoc 循环无 MAF 审批泵（ApprovalRequiredAIFunction 仅 MAF 包装挂载）——零配置下 B4 registry 层 = default-policy Allow → Coordinator 带工具任务的高风险工具不经审批直接执行。
- 保留面：人设注册表（IAgentRegistryService + BuiltInAgentDefinitions 7 角色，RunOrchestratorService:1206 步骤人设注入在用 + /agents 页列表在用）；IChatCompletion 旧面（B7 定性合法例外，WorkflowPlanner/AIWorkflowAssistant 在用）。

## 2. 推荐与理由（呈报原文要点）

推荐**删除**：接入 MAF 工程量 >1 天（须在 RunOrchestrator 之上重建多 persona 步骤调度/聚合 + 结果汇总，属新产品能力而非收敛，与 PlanGenerator+RunOrchestrator 职责重叠）；删除收益 = 消审批缺口旁路 + 4 死端点 + 2 服务（808 行）+ AgentModels 待删模型 + UI 死代码；用户可见零变化（4 端点本就无人调）。

## 3. 拍板原文（lead seq8，2026-11-02 23:5x）

> 拍板：**A = 删除**（原文：「AgentCoordinator 启发式旁路裁决采纳删除方案。理由：① 审批缺口旁路实锤——ad-hoc 循环无 MAF 审批泵，registry 层零配置 Allow 下高风险工具直执行，留着即安全暗雷；② 接入工程量 >1 天且与 PlanGenerator+RunOrchestrator 职责重叠，属新产品能力而非收敛，违本批「收敛」定位；③ UI/插件/IM/Scheduler 四路零消费，删除用户可见零变化；④ ADR-006 绿场无迁移承诺 + 架构 §7 已授权）。保留 /agents 页与人设注册表；not-taken 按你拟的条目落。把本拍板原文归档 agenthub-adjudication.md（AC7 留痕）后按 S7 执行面推进。删除完成后的深档 e2e 是你与 qa-gate 的重头，e2e 基建层若要动（globalSetup/config）先报我。」

## 4. 执行面（S7，拍板后）

删除：AgentCoordinatorService.cs（505 行）、AgentExecutorService.cs（303 行）、AgentsController 4 端点（plan/execute/execute-task/handle）、AIAgentPlugin DI 2 行（:45-46）、AgentModels.cs 待删模型（AgentTask/AgentExecutionResult/CoordinatorPlan/AgentInstance 及关联枚举）、UI 死代码（ForgeSelf.Web/src/services/agentApi.ts 3 方法 + stores/agent.ts 3 方法/instances + types/agent.ts 对应 interface）。
not-taken：docs/07-decisions/not-taken-decisions.md 增「多智能体启发式编排不做，能力归计划任务主路径」。
边界：e2e 基建层（globalSetup/config）不动；若删除迫使基建层变更，先报 lead。
