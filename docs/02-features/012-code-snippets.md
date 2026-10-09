---
feature_key: F012
feature_no: 012
status: implemented
last_updated: 2026-10-06
aliases: ["012-code-snippets"]
---

# 012 · 代码片段（Code Snippets）

> 状态：已实现（代码中已落地）
> 最后更新：2026-08-13

## 概述

代码片段收藏与管理功能，允许用户保存常用代码段、按语言/分类组织、标记收藏，并能从已有脚本反向提取片段。该能力归属 **ScriptRunner 插件**（与脚本库共用插件目录）。

## 代码落点

| 层 | 文件 |
|----|------|
| 控制器 | `Plugins/ScriptRunner/Controllers/CodeSnippetController.cs`（`[Route("api/codesnippets")]`） |
| 服务 | `Plugins/ScriptRunner/Services/CodeSnippetService.cs`（接口 `ICodeSnippetService`） |
| 模型 | `Plugins/ScriptRunner/Models/CodeSnippet.cs`、`CodeSnippetDtos.cs` |
| 实体 | `Plugins/ScriptRunner/Data/Entities/CodeSnippet.cs`（含 `.Biz.cs`，XCode 实体） |
| 前端视图 | `src/views/CodeSnippetsView.vue` |
| 前端服务 | `src/services/codeSnippetApi.ts` |

## 核心 API（`api/codesnippets`）

| 方法 | 路由 | 说明 |
|------|------|------|
| GET | `/` | 列表（支持分类/语言筛选） |
| GET | `/{id}` | 详情 |
| POST | `/` | 新建 |
| PUT | `/{id}` | 更新 |
| DELETE | `/{id}` | 删除 |
| POST | `/{id}/favorite` | 切换收藏 |
| POST | `/from-script/{scriptId}` | 从脚本提取片段 |
| GET | `/languages` | 支持语言枚举 |
| GET | `/categories` | 分类列表 |

## 使用要点

- 与 ScriptRunner 插件共享数据上下文（`ScriptRunnerDbContext`）。
- 前端 `CodeSnippetsView` 支持语言高亮与分类筛选。
