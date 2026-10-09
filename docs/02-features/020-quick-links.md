---
feature_key: F020
feature_no: 020
status: implemented
last_updated: 2026-10-06
aliases: ["020-quick-links"]
---

# 020 · 快捷链接（Quick Links）

> 状态：已实现（代码中已落地）
> 最后更新：2026-08-13

## 概述

常用链接书签管理，支持分类、点击计数、拖拽排序、导入/导出。适合把常用内部系统、文档、工具入口集中管理。

## 代码落点

| 层 | 文件 |
|----|------|
| 控制器 | `Plugins/QuickLinks/Controllers/QuickLinksController.cs`（`[Route("api/quicklinks")]`） |
| 服务 | `Plugins/QuickLinks/Services/QuickLinkService.cs`（接口 `IQuickLinkService`） |
| 实体 | `Plugins/QuickLinks/Data/Entities/QuickLink.cs`、`QuickLinkCategory.cs`（含 `.Biz.cs`） |
| 数据上下文 | `Plugins/QuickLinks/Data/QuickLinksDbContext.cs`、`QuickLinksEntities.cs` |
| 前端视图 | `src/views/QuickLinksView.vue` |
| 前端服务 | `src/services/quickLinksApi.ts` |

## 核心 API（`api/quicklinks`）

| 方法 | 路由 | 说明 |
|------|------|------|
| GET/POST/PUT/DELETE | `/`、`/{id}` | 链接 CRUD |
| POST | `/{id}/click` | 点击计数 |
| POST | `/reorder` | 拖拽排序 |
| GET/POST/PUT/DELETE | `/categories[/{id}]` | 分类管理 |
| POST | `/import` | 导入 |
| GET | `/export` | 导出 |

## 使用要点

- 点击 `click` 自动累加使用次数，可用于排序/常用置顶。
- 支持 `QuickLinks.htm` 静态页导出（见 `Data/QuickLinks.htm`）。
