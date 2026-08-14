# 013 · 工作流引擎（Workflow Engine）

> 状态：已实现（代码中已落地）
> 最后更新：2026-08-13

## 概述

可视化工作流编排与执行。用户定义多步骤工作流（步骤见 `WorkflowStep`），手动或经 AI 辅助规划后执行，支持执行暂停/恢复/取消、执行历史查看。AI 辅助规划由 AIAgent 插件的 `WorkflowPlannerService` 提供。

## 代码落点

| 层 | 文件 |
|----|------|
| 控制器 | `Plugins/WorkflowEngine/Controllers/WorkflowController.cs`（`[Route("api/workflows")]`）；AI 辅助 `Plugins/AIAgent/Controllers/AIWorkflowController.cs`（`[Route("api/ai-agent/workflow")]`） |
| 服务 | `Plugins/WorkflowEngine/Services/WorkflowService.cs`、`WorkflowExecutor.cs` |
| 模型 | `Plugins/WorkflowEngine/Models/WorkflowStep.cs` |
| AI 规划 | `Plugins/AIAgent/Services/WorkflowPlannerService.cs`、`AIWorkflowAssistant.cs` |
| 前端视图 | `src/views/WorkflowLibrary.vue` |
| 前端服务 | `src/services/workflowApi.ts` |

## 核心 API

**`api/workflows`**：CRUD（`/` `/{id}`）、`{id}/favorite`、`{id}/execute`、`executions`（列表/详情）、`executions/{id}/pause|resume|cancel`、`templates`。

**`api/ai-agent/workflow`**：`plan`、`plan/prompt`、`execute`、`{executionId}/status`、`tools/match`（AI 辅助规划与工具匹配）。

## 使用要点

- 工作流执行支持断点控制（暂停/恢复/取消）。
- AI 规划依赖 AIAgent 插件（`api/ai-agent/workflow`），需该插件启用。
