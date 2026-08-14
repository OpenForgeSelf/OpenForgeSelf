# 019 · 脚本运行器（Script Runner）

> 状态：已实现（代码中已落地）
> 最后更新：2026-08-13

## 概述

脚本管理与执行环境，支持多语言运行时（由 `RuntimeDetector` 探测）、脚本收藏与分类、模板库、以及通过 WebSocket（SignalR `ScriptExecutionHub`）实时回传执行日志。代码片段（012）同属此插件。

## 代码落点

| 层 | 文件 |
|----|------|
| 控制器 | `Plugins/ScriptRunner/Controllers/ScriptRunnerController.cs`（`[Route("api/scripts")`）；`CodeSnippetController.cs`（`api/codesnippets`，见 012） |
| 实时通道 | `Plugins/ScriptRunner/Hubs/ScriptExecutionHub.cs`（SignalR） |
| 服务 | `Plugins/ScriptRunner/Services/`：`ScriptService`(IScriptService)、`ScriptExecutor`(IScriptExecutor)、`RuntimeDetector`(IRuntimeDetector)、`ScriptTemplateService`(IScriptTemplateService)、`ScriptRunnerToolFunctions.cs` |
| 模型 | `Plugins/ScriptRunner/Models/`：`Script.cs`、`ScriptDtos.cs`、`ScriptEnums.cs`、`ScriptExecution.cs`、`ScriptTemplate.cs` |
| 实体 | `Plugins/ScriptRunner/Data/Entities/Script.cs`（含 `.Biz.cs`）；`CodeSnippet.cs`（见 012） |
| 数据上下文 | `Plugins/ScriptRunner/Data/ScriptRunnerDbContext.cs`、`ScriptRunnerEntities.cs` |
| 前端视图 | `src/views/ScriptLibrary.vue` |
| 前端服务 | `src/services/scriptRunnerApi.ts` |

## 核心 API（`api/scripts`）

| 方法 | 路由 | 说明 |
|------|------|------|
| GET/POST/PUT/DELETE | `/`、`/{id}` | 脚本 CRUD |
| POST | `/{id}/favorite` | 收藏 |
| GET | `/categories`、`/tags`、`/runtimes` | 分类/标签/运行时 |
| POST | `/{id}/execute`、`/execute-code` | 执行（实时日志走 Hub） |
| GET/POST cancel | `/executions`、`/executions/{id}`、`/executions/{id}/cancel` | 执行记录与取消 |
| GET | `/templates`、`/templates/categories` | 模板库 |

## 使用要点

- 执行过程通过 `ScriptExecutionHub` 实时推送，前端订阅展示流式输出。
- `RuntimeDetector` 在启动时探测本机可用运行时（如 node/python/powershell）。
