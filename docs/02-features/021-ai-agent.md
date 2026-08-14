# 021 · AI 智能体（AI Agent）

> 状态：已实现（代码中已落地）
> 最后更新：2026-08-13

## 概述

AI 智能体编排框架：多 Agent 协调与执行（`AgentsController`）、智能规划建议（`PlanningController`）、AI 辅助工作流（`AIWorkflowController`，见 013）、脚本生成与错误分析（`AIScriptController`）、AI 聊天（`AIChatController`）。是连接「统一 AI 网关」与「上层工具」的协调层。

## 代码落点

| 层 | 文件 |
|----|------|
| 控制器 | `Plugins/AIAgent/Controllers/`：`AgentsController.cs`（`[Route("api/agents")]`）、`PlanningController.cs`（`[Route("api/planning")]`）、`AIWorkflowController.cs`（`[Route("api/ai-agent/workflow")]`）、`AIScriptController.cs`（`[Route("api/ai-agent/script")]`）、`AIChatController.cs`（`[Route("api/ai-agent/chat")]`） |
| 服务 | `Plugins/AIAgent/Services/`：`AgentExecutorService`、`AgentCoordinatorService`、`WorkflowPlannerService`、`AIWorkflowAssistant`、`AIAgentService` |
| 模型 | `Plugins/AIAgent/Models/SkillDefinition.cs` |
| 数据 | `Plugins/AIAgent/Data/AIAgentDbContext.cs` |
| 前端视图 | `src/views/AgentsManageView.vue`、`src/views/AgentView.vue` |
| 前端服务 | `src/services/agentApi.ts`、`src/services/planningApi.ts` |

## 核心 API

**`api/agents`**：列表 / 详情 / 按类型 / `find` / `best-match` / `coordinate` / `execute` / `handle` / `execute-task`；实例管理 `instances[/{instanceId}]`。

**`api/planning`**：`suggestions`、`suggestions/pending`、`suggestions/{id}/action|dismiss`、`patterns`、`profile`、`skills`、`preferences`、`event`。

**`api/ai-agent/workflow`**：`plan`、`plan/prompt`、`execute`、`{executionId}/status`、`tools/match`（详见 013）。

**`api/ai-agent/script`**：`generate`、`analyze-error`、`suggest-fix`、`templates[/{templateId}]`、`templates/categories`。

**`api/ai-agent/chat`**：`POST /`、`POST /stream`、`history/{sessionId}`、`DELETE session/{sessionId}`、`tools`。

## 使用要点

- Agent 协调依赖 `AgentCoordinatorService`，可编排多个子 Agent 完成任务。
- `PlanningController` 基于历史行为给出主动性建议（见 `preferences`/`patterns`）。
- 聊天会话复用统一 `ChatSession` 聚合（见 010）。
