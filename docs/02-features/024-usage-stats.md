---
feature_key: F024
feature_no: 024
status: implemented
last_updated: 2026-10-06
aliases: ["024-usage-stats"]
---

# 024 · 用量统计（Usage Stats）

> 状态：已实现（代码中已落地）
> 最后更新：2026-08-13

## 概述

API 调用与 token 消耗统计、工作流使用记录。后端以**核心服务**聚合数据，落 XCode 实体，前端经 `usageStatsApi` 展示。注意：**无独立 REST 控制器**，统计由 `UsageStatsService` 在内部聚合后供其他接口/页面调用。

## 代码落点

| 层 | 文件 |
|----|------|
| 服务 | `Services/UsageStats/UsageStatsService.cs`、`WorkflowRecommendationService.cs` |
| 实体 | `Entities/UsageRecord.cs`（含 `.Biz.cs`）、`UsageDailySummary.cs`、`WorkflowUsageRecord.cs` |
| 前端服务 | `src/services/usageStatsApi.ts` |

## 数据模型

| 实体 | 用途 |
|------|------|
| `UsageRecord` | 单次调用记录（provider/模型/token/耗时） |
| `UsageDailySummary` | 按日聚合摘要 |
| `WorkflowUsageRecord` | 工作流调用记录（支撑推荐） |

## 使用要点

- 统计数据源来自统一 AI 网关（004）的调用埋点。
- `WorkflowRecommendationService` 基于 `WorkflowUsageRecord` 给出工作流推荐。
- 前端展示入口由 `usageStatsApi` 对接（具体查询端点见源码 `Services/UsageStats`）。
