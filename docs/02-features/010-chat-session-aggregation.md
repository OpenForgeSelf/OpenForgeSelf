---
feature_key: F010
feature_no: 010
status: implemented
last_updated: 2026-10-06
aliases: ["010-chat-session-aggregation"]
---

# 010 聊天会话聚合（ChatSession / ChatTurn）— 功能需求与设计

> 功能编号：010（无独立 spec，源于 ChatRecord 会话聚合重构）
> 状态：已实现（已随 9d84e48 提交）；前端会话维度改造已完成（输入 32 门禁 408/408）
> 关联：docs/chat-record-session-redesign.md（根因分析）；04 统一网关
> 最后更新：2026-08-12

## 1. 功能需求

### 1.1 背景
`ChatRecord` 原是 LLM 代理录制器（走 `/v1/chat/completions`），而 app 自有聊天是 `ChatController`（走 `/api/chat`），**两套会话空间互不相同**。客户端不传 `X-Session-Id` 时每请求随机 `SessionId` → 607 行全互异、无法聚合。详见 `chat-record-session-redesign.md` 根因。

### 1.2 目标
1. 统一会话聚合根 `ChatSession`（app 自有聊天 Source='App' + 代理录制 Source='Proxy'）；
2. 单次请求录制改为 `ChatTurn`（原 `ChatRecord` 改名），带 `ChatSessionId`/`TurnIndex`/`SessionKey` 外键关联；
3. 代理录制用透传头（`x-interaction-id` 等）解析真会话键 → upsert 会话；
4. 提供会话维度 REST（`api/chat-sessions`）。

## 2. 设计

### 2.1 实体（`Entities/`）
| 实体 | 职责 | 关键字段 |
|------|------|----------|
| `ChatSession` | 聚合根 | `SessionKey`（稳定键，UX 唯一索引）、`Source`、`Title`、`Model`、`Provider`、`Style`、`ClientKind`、`MessageCount`、`Total*Tokens` |
| `ChatTurn` | 单轮录制（原 ChatRecord） | `ChatSessionId`（FK）、`TurnIndex`、`SessionKey`（冗余免 JOIN）、`Style`、`RequestBody/Response`、`Tokens` |
| `ChatEnums` | 枚举（Style/Source 等） | — |
| `ChatSessionService` / `ChatTurnService` / `ChatTurnStreamRecorder` | 业务 | 重写以适配改名与关联 |

### 2.2 会话键解析（`ResolveConversationKey` 优先级链，见 `Services/ChatSessionResolver.cs`）
按优先级取第一个非空（命中即返回）：
1. 头 `X-Session-Id` / `X-Conversation-Id`（自定义/标准客户端主动传）；
2. 头 `x-interaction-id`（GitHub Copilot 透传会话键，已验证稳定可用）；
3. 头 `x-conversation-id`（Anthropic/Responses 风格兼容）；
4. body 字段 `conversationId` / `sessionId`（今后 app 直接打代理时）；
5. 兜底 `Guid.NewGuid()`（仅无会话标识的裸客户端，自成单轮会话）。
- app 自有聊天：`ChatController` 自行 `UpsertSessionAsync(sessionId, Source='App')` 补写会话行。
- 注：旧实现曾误用 `x-agent-task-id` / `x-request-id`（每请求唯一）作会话键致 607 行散列，该逻辑已在重构中移除，代码中不再存在这两个头的解析。

### 2.3 实现位置
| 文件 | 职责 |
|------|------|
| `Entities/ChatSession*.cs` `ChatTurn*.cs` `ChatEnums.cs` | 实体/枚举 |
| `Services/ChatSessionService.cs` `ChatTurnService.cs` `ChatTurnStreamRecorder.cs` | 业务 |
| `Controllers/ChatController.cs` | app 聊天：`api/chat`(POST 收发)、`api/chat/stream`(流式)、`api/chat/history/{sessionId}`(GET 历史)、`api/chat/session/{sessionId}`(DELETE 删除)；**会话列表/详情由 `ChatRecordsController`(`api/chat-sessions`) 提供，非 ChatController** |
| `Controllers/ChatRecordsController.cs` | 会话视图（`api/chat-sessions` GET 列表 + `{id}` 详情） |
| `Controllers/UnifiedAI/*` | 代理录制 → 解析键 → upsert 会话 → 写 Turn |
| `AppBuilder.cs` | DI 注册 IChatTurnService/ChatSessionService 等 |

## 3. 使用指南
前端聊天记录面板（`ChatRecordsView`）按会话列表展示，点进看 `ChatTurn` 详情（JSON 树 + 中文说明 + 展开/收起，见 working memory）。

## 4. 注意事项
- 旧 `ChatRecord` 607 行**不迁移**（历史散列数据，无聚合价值）；
- 运行实例无权限停止时，缓存/重构生效需**重启后端**；
- 集成测试 `ChatRecordRealLLMTests` 全量跑偶发红，疑测试间状态干扰（TODO 已知）。

## 5. 测试覆盖
| 测试 | 覆盖点 |
|------|--------|
| `ChatSessionServiceTests` `ChatTurnServiceTests` `ChatTurnStreamRecorderTests` | 改名后服务逻辑（拆分单测） |
| `ChatRecordsControllerIntegrationTests` `UnifiedAIGatewayIntegrationTests` | 会话视图/网关集成（3 集成测试适配） |
| 前端 `ChatRecordsView` `ChatRecordDetail` 测试 | 会话维度渲染、WS 推送 |
