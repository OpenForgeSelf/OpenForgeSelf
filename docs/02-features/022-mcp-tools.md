---
feature_key: F022
feature_no: 022
status: implemented
last_updated: 2026-10-06
aliases: ["022-mcp-tools"]
---

# 022 · MCP 工具（MCP Tools）

> 状态：已实现（代码中已落地）
> 最后更新：2026-08-13

## 概述

MCP（Model Context Protocol）服务器与工具管理。后端以**核心控制器**形式提供（非插件），负责注册 MCP 服务器、列出其工具、开关/测试单个工具，供 AI 网关在对话中调用外部能力。

## 代码落点

| 层 | 文件 |
|----|------|
| 控制器 | `Controllers/McpController.cs`（`[Route("api/mcp")]`） |
| 服务 | `Services/Mcp/McpService.cs`（接口 `IMcpService`） |
| 模型 | `Models/Mcp/McpToolDto.cs` |
| 前端视图 | `src/views/McpToolsView.vue` |
| 前端服务 | `src/services/mcpApi.ts` |

## 核心 API（`api/mcp`）

| 方法 | 路由 | 说明 |
|------|------|------|
| GET | `/servers` | MCP 服务器列表 |
| GET | `/servers/{serverId}/tools` | 某服务器下工具 |
| POST | `/tools/{toolId}/toggle` | 工具开关 |
| POST | `/tools/{toolId}/test` | 测试工具 |
| GET | `/servers/{serverId}/test` | 测试服务器连通性 |

## 使用要点

- MCP 工具可被统一 AI 网关（004）在对话中动态调用，扩展 agent 能力边界。
- 开关状态持久化，决定是否对 AI 可见。
