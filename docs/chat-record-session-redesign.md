# 聊天记录会话化重构 — 重设计方案（v2 · 最佳拆分 / 无迁移）

> 分析对象：`ForgeSelf.Api/bin/Debug/net10.0-windows/Data/ForgeSelf.db`
> 设计时间：2026-08-11　数据规模：607 行 ChatRecord（仅代理录制）
> 范围：根因已确认，按"最佳拆分 + 旧数据不迁移 + app 聊天也纳入会话"重设计。

## ⚠️ 落地状态（2026-08-12 反向更新）

本方案**已落地**，实现对齐见 [`02-features/010-chat-session-aggregation.md`](02-features/010-chat-session-aggregation.md)。与 v2 草案的一处出入（已确认以代码为准）：

- **草案设想**：`ChatSession (1) ──< (N) ChatMessage` 作为外键子表。
- **实际实现**：`ChatMessage` 仍保留独立 `SessionId` 字符串字段（app 历史路径未动），通过 `SessionId` 与 `ChatSession` **软关联**（非外键子表）；代理录制改为 `ChatTurn` 实体（原 `ChatRecord` 改名）带 `ChatSessionId` 外键 + `TurnIndex` + `SessionKey` 冗余。
- **聚合目标达成**：`ChatController` 收发消息时 `UpsertSessionAsync(sessionId, Source.App, ...)` 补写会话行；代理录制经 `ResolveConversationKey` 解析 `x-interaction-id` 稳定键 → upsert `ChatSession` → 写 `ChatTurn`。旧 607 行 `ChatRecord` 不迁移。
- 后端代码已**随 `9d84e48` 提交**；前端会话维度改造已完成（输入 32 门禁 408/408）。

---

## 0. 关键事实（决定设计走向）

1. **`ChatRecord` 只录代理流量**：所有走 `OpenAIChatController`（`/v1/chat/completions`）等统一 AI 入口的请求被录制为"每请求一行"。
2. **app 自有聊天不经过代理**：`ChatController` → `AIService` 用 `HttpClient` **直连** `_aiConfig.ApiEndpoint`（见 `Services/AIService.cs:69/134`），`ChatRecord` 完全录不到它。
3. **app 聊天本就有会话 id**：`ChatMessage.SessionId` + `ChatController` 用 `request.SessionId ?? Guid.NewGuid()` 管理会话，只是它**不写 `ChatSession`**，所以"聊天记录"页面看不到 app 会话。
4. **代理录制缺会话键**：`OpenAIChatController.cs:88` 在无 `X-Session-Id` 头时每请求 `Guid.NewGuid()` → 607 行全不同 SessionId。真正的会话键是 Copilot 透传的 `x-interaction-id`（541 行→仅 52 个会话，已验证）。

> 结论：要让"聊天记录"按会话聚合并包含 app 聊天，需要一个**统一的 `ChatSession` 聚合表**，app 聊天与代理录制都向它归属；代理录制靠 `ResolveConversationKey` 解析稳定键，app 聊天直接用自己的 `SessionId` 作为键。

---

## 1. 新数据库结构（最佳拆分 · 3 张角色清晰）

```
ChatSession (1) ──< (N) ChatTurn       // 统一会话聚合 ← 代理录制的每一轮
ChatSession (1) ──< (N) ChatMessage     // 统一会话聚合 ← app 自有聊天的逐条消息
                     （两子表通过同一 ChatSession 关联，app/代理互不混写内容）
```

### 1.1 `ChatSession`（**新增**，统一会话聚合，app + 代理共用）
| 列 | 类型 | 说明 |
|----|------|------|
| Id | INTEGER PK AUTOINCREMENT | — |
| SessionKey | NVARCHAR(64) NOT NULL NOCASE | **统一稳定会话键**：app=其 SessionId；代理=`ResolveConversationKey` 结果 |
| Source | NVARCHAR(16) NOT NULL | `App` / `Proxy`（来自哪套聊天） |
| Title | NVARCHAR(200) NULL | 首条用户消息预览 |
| Model | NVARCHAR(100) NULL NOCASE | 主模型 |
| Provider | NVARCHAR(100) NULL NOCASE | 代理：上游提供方 |
| Style | NVARCHAR(30) NULL NOCASE | OpenAI_Chat / Anthropic / Responses / AppChat |
| ClientKind | NVARCHAR(30) NULL NOCASE | Copilot / App / Curl / Unknown（UA 指纹；app 固定 `App`） |
| RequestCount | INTEGER NOT NULL DEFAULT 0 | 轮次数量（代理=请求数；app=消息对数） |
| MessageCount | INTEGER NOT NULL DEFAULT 0 | 该会话最新完整消息数 |
| FirstUserMsg | NVARCHAR(500) NULL | 首条 user 消息预览 |
| TotalPromptTokens | BIGINT NOT NULL DEFAULT 0 | 累计 |
| TotalCompletionTokens | BIGINT NOT NULL DEFAULT 0 | 累计 |
| LastStatus | INTEGER NOT NULL DEFAULT 0 | 末轮 HTTP 状态 |
| CreatedTime | DATETIME NULL | — |
| UpdatedTime | DATETIME NULL | — |

唯一索引 `UX_ChatSession_SessionKey(SessionKey)`；`IX_ChatSession_UpdatedTime(UpdatedTime DESC)`；`IX_ChatSession_Source(Source)`。

### 1.2 `ChatTurn`（**由 `ChatRecord` 改名/改造**，代理轮次明细 · 分析友好）
- 语义从"一条聊天记录"变为"**会话内的一轮请求-响应**"。
- 新增 `ChatSessionId INTEGER NOT NULL`（→ `ChatSession.Id` 外键）。
- 新增 `TurnIndex INTEGER NOT NULL DEFAULT 0`（会话内第几轮，从 1 递增）。
- 新增 `UserPreview` / `AssistantPreview`（NVARCHAR(500)，列表速览，免展开读大字段）。
- **废弃并删除原 `SessionId` 列**（旧随机值无语义）。
- 保留全部原始请求/响应字段（调试价值）：RequestHeaders/RequestBody/ResponseStatus/ResponseHeaders/ResponseBody/ResponseText/RequestId/Style/Model/Temperature/MaxTokens/MessageCount/ToolCallCount/HasReasoning/DurationMs/CreatedTime 等。

**分析友好字段（为后续"分析会话记录"刻意冗余，便于聚合查询，不返解析 JSON）：**
| 列 | 类型 | 用途 |
|----|------|------|
| PromptTokens | INTEGER NOT NULL DEFAULT 0 | 本轮输入 tokens（喂 `ChatSession.TotalPromptTokens` 累加） |
| CompletionTokens | INTEGER NOT NULL DEFAULT 0 | 本轮输出 tokens |
| TotalTokens | INTEGER NOT NULL DEFAULT 0 | 本轮总 tokens |
| DurationMs | INTEGER NOT NULL DEFAULT 0 | 本轮耗时（已有，保留） |
| FirstTokenMs | INTEGER NULL | 首 token 延迟（流式场景，分析响应速度） |
| ErrorMessage | NVARCHAR(1000) NULL | 失败原因（便于失败率分析） |
| ToolCallCount | INTEGER NOT NULL DEFAULT 0 | 工具调用次数（已有，保留） |
| HasReasoning | BIT NOT NULL DEFAULT 0 | 是否含推理内容（已有，保留） |

> 设计原则（用户确认）：会话记录表要为"后续分析对话记录"服务，故轮次级指标**直接落列**而非每次从 JSON 解析；`ChatSession` 再汇总成会话级总量，列表/统计页可直接 `GROUP BY` 或读冗余列，零 JOIN 重算。

索引：`IX_ChatTurn_ChatSessionId(ChatSessionId)`、`IX_ChatTurn_ChatSessionId_Turn(ChatSessionId, TurnIndex)`、`IX_ChatTurn_CreatedTime(CreatedTime DESC)`、`IX_ChatTurn_Model(Model)`（按模型分析）。

### 1.3 `ChatMessage`（**既有**，app 聊天逐条消息，基本不动）
- 已有 `SessionId`。改造点：在 `ChatController` 每次收发消息时，用同一 `SessionId` **upsert 一条 `ChatSession`（Source='App'）**，使 app 会话进入统一列表。
- 不新增外键列（字符串 `SessionId` 即等于 `ChatSession.SessionKey`，关联已成立）。
- 可选 Phase2：把 `ChatMessage` 并入统一消息表（见 §5）。

---

## 2. 写入流程（改造点）

### 2.1 代理入口 `OpenAIChatController`
```
请求到达
  └─ key = ResolveConversationKey(req, body)     // 见 §2.3
  └─ session = ChatSessionService.UpsertByKey(key, source:'Proxy', model, clientKind:UA指纹)
  └─ turn.ChatSessionId = session.Id; turn.TurnIndex = session.RequestCount(自增)
  └─ 流式/非流式落库逻辑不变，仅多写外键 + TurnIndex + Preview
```

### 2.2 app 入口 `ChatController`
```
sessionId = request.SessionId ?? Guid.NewGuid("N")   // 现有逻辑保留
ChatSessionService.UpsertByKey(sessionId, source:'App', title:首条user消息, clientKind:'App')
_messageService.SaveMessageAsync(sessionId, role, content)   // 现有逻辑保留
返回 sessionId（现有逻辑保留）
```
→ app 聊天"带会话 id"即它天然成为一个 `ChatSession`，出现在统一"聊天记录"里。

### 2.3 `ResolveConversationKey(HttpRequest, body)` 优先级（解决来源不统一）
1. 头 `X-Session-Id` / `X-Conversation-Id`（自定义/标准客户端主动传）
2. 头 `x-interaction-id`（Copilot 会话键，已验证 541 行可用）
3. 头 `x-conversation-id`（Anthropic/Responses 风格兼容）
4. body `conversationId` / `sessionId`
5. 兜底 `Guid.NewGuid()`（仅对裸 curl 等无标识客户端，自成单轮会话，符合预期）

---

## 3. 旧数据

**不迁移**（用户确认：当前未投入生产，旧 607 行无意义）。直接按新结构建表；运行时新写入即走新结构。旧 `ChatRecord` 表可保留为 `ChatRecord_Legacy` 备查或直接 DROP（新库从空开始）。

---

## 4. API / 前端影响

### 4.1 后端
- `ChatRecordsController` 更名/扩展为会话视图：
  - `GET /api/chat-sessions`：分页列出会话，支持 `source`(App/Proxy/All)、`clientKind`、`style`、时间过滤。
  - `GET /api/chat-sessions/{id}`：会话 + 其下 `ChatTurn` 轮次列表（app 会话则返回 `ChatMessage` 列表）。
  - 原原始请求日志视图（Debug）保留为会话详情里的"查看原始请求/响应"。
- `IChatSessionService`：新增 `UpsertSessionAsync` / `GetSessionsAsync` / `GetSessionAsync`。
- `ChatController` 注入 `IChatSessionService` 做 upsert。

### 4.2 前端 `ChatRecordsView`
- 列表改为**会话维度**（标题/来源标签 App·Proxy/模型/轮次/消息数/最后活动），点击展开轮次明细。
- 复用 `ChatRecordDetail.vue` 渲染单轮；新增会话卡片 + 来源筛选。
- `chatRecordsApi.ts`：新增 `getSessions()` / `getSession(id)`；原 `getChatRecords()` 转调试接口。
- app 聊天侧：`stores/chat.ts` 已持 `conversationId`/`sessionId`，确保每轮对话线程稳定复用同一 id（勿每次新建），使 `ChatSession` 正确聚合。

---

## 5. 实施步骤（建议顺序）

1. 新建 `ChatSession` 实体 + 注册（XCode）。
2. `ChatRecord` → `ChatTurn` 改名 + 加 `ChatSessionId`/`TurnIndex`/`*Preview`，删旧 `SessionId` 列（无迁移，直接改实体）。
3. 新增 `ChatSessionService` + `ResolveConversationKey`。
4. 改造 `OpenAIChatController`：解析键 → upsert 会话 → 写外键/TurnIndex/Preview。
5. 改造 `ChatController`：收发消息时 upsert `ChatSession`(Source='App')。
6. 改造 `ChatRecordsController` → 会话视图 API；前端 `ChatRecordsView` + api。
7. 门禁：`dotnet build` + 后端 `dotnet test` + 前端 `pnpm run check` + `pnpm run test`。

### 已确认决策（用户拍板 · 2026-08-11）
- **A. 统一 `ChatSession`**：app 自有聊天（`ChatController`）与代理录制（`OpenAIChatController`）都向同一个 `ChatSession` 归属；app 聊天保持直连上游、不改路由，仅补写会话行即可出现在统一"聊天记录"。
- **B. 拆分粒度 = 2 级**：`ChatSession` + `ChatTurn`（原 `ChatRecord` 改名），消息仍存 `RequestBody` JSON；Phase2 视分析需要再拆 `ChatTurnMessage` 逐条消息表。
- **命名**：`ChatRecord` 改名 `ChatTurn`（语义贴合"会话内一轮"）。
- **分析友好原则**：`ChatTurn` 轮次级指标（tokens/耗时/首 token 延迟/错误/工具调用）直接落列，`ChatSession` 汇总会话级总量，列表与统计分析零重算。

> 旧 607 行不迁移（未投产），新库从空开始；`ChatRecord` 旧表可保留为 `ChatRecord_Legacy` 备查或直接 DROP。

### 下一步
方案已定稿。是否开始实现？（首次架构改动，按协作约定需你明确"动手"授权范围：仅本次 / 以后可自动）实现将按 §5 七步推进，并以 `dotnet build` + 后端 `dotnet test` + 前端 `pnpm run check`/`test` 门禁验收。
