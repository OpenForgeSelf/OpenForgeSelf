---
feature_key: F002
feature_no: 002
status: implemented
last_updated: 2026-10-06
aliases: ["002-ai-models-list"]
---

# 002 AI 模型列表 — 功能需求与设计

> 功能编号：002
> 状态：已实现
> 关联：001 AI Provider；004 集成（历史 specs/002 已弃用）
> 最后更新：2026-08-12

## 1. 功能需求

### 1.1 背景
Provider 配置后，需把上游模型清单固化到本地（`AIModel`），支撑聊天模型选择、能力标签（vision/stream）、`ChatModelId` 拼接（用于 LLM 客户端配置）。

### 1.2 目标
1. 从上游 `/models` 拉取模型，按 `(Provider, UpstreamModelId)` upsert 持久化；
2. 支持用户编辑别名、能力标签、最大上下文；
3. 生成 `ChatModelId = provider:upstream_model_id`（复制按钮输出，供客户端直接粘贴）。

## 2. 设计

### 2.1 实体关键字段（`Entities/AIModel.cs`）
| 字段 | 含义 |
|------|------|
| `ProviderId` / `ProviderName` | 归属供应商（Name 锁定不可编辑） |
| `UpstreamModelId` | 上游原始模型标识（锁定） |
| `ChatModelId` | 聊天模型 id：`提供商:原始模型id`（复制输出） |
| `Alias` | 显示别名（可编辑） |
| `Capabilities` | 能力标签（逗号分隔，如 vision,stream） |
| `MaxContext` | 最大上下文长度（token，0=未设置） |
| `Enabled` | 是否启用（默认启用） |
| `Owner` | 上游返回的 owner/owned_by |
| `LastSyncTime` | 最近同步（拉取）时间 |

### 2.2 实现位置
| 文件 | 职责 |
|------|------|
| `Entities/AIModel*.cs` | 实体 |
| `Controllers/AIModelController.cs` | REST（`api/ai-models`） |
| `Controllers/AIProviderController.cs` `fetch-models` | 触发上游拉取 → upsert |

## 3. 使用指南
设置面板 → 模型列表，点"拉取"从已配 Provider 同步模型；编辑别名/能力；复制 `ChatModelId` 到 LLM 客户端（如 GitHub Copilot 的模型字段）。

## 4. 测试覆盖
| 测试 | 覆盖点 |
|------|--------|
| `AIModel` 相关单测/集成 | upsert 幂等、ChatModelId 拼接、Capabilities 解析 |
