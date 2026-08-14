# 014 · 记忆系统（Memory System）

> 状态：已实现（代码中已落地）
> 最后更新：2026-08-13

## 概述

长期记忆存储模块，支持记忆条目 CRUD、分类管理、语义相关检索（`relevant`）、以及导入/导出。后端以独立插件形式存在，数据通过 XCode 实体持久化。

## 代码落点

| 层 | 文件 |
|----|------|
| 控制器 | `Plugins/MemorySystem/Controllers/MemoryController.cs`（`[Route("api/memory")]`） |
| 服务 | `Plugins/MemorySystem/Services/MemoryService.cs`、`MemoryIntegrationService.cs`、`MemoryServiceXCode.cs`（接口 `IMemoryService`） |
| 实体 | `Plugins/MemorySystem/Data/Entities/Memory.cs`、`MemoryCategory.cs`（含 `.Biz.cs`） |
| 数据上下文 | `Plugins/MemorySystem/Data/MemoryDbContext.cs`、`MemoryEntities.cs` |
| 前端视图 | `src/views/MemoryView.vue` |
| 前端服务 | `src/services/memoryApi.ts` |

## 核心 API（`api/memory`）

| 方法 | 路由 | 说明 |
|------|------|------|
| POST | `/search` | 关键词检索 |
| POST | `/relevant` | 语义相关检索 |
| GET/POST/PUT/DELETE | `/{id}` | 条目 CRUD |
| GET/POST/PUT/DELETE | `/categories[/{id}]` | 分类管理 |
| GET | `/stats` | 统计 |
| POST | `/import` | 导入 |
| GET | `/export` | 导出 |

## 使用要点

- 记忆与分类独立成表，支持多级组织。
- `MemoryIntegrationService` 负责与外部（如 AI 上下文）集成。
