# OpenForgeSelf LLM 可观测性能力盘点与增强方案

> 日期：2026-10-03
> 前置研究：`llm-observability-research-2026.md`（行业通用方案基线）
> 目标：盘点本项目已有的「接入 LLM → 落库 → 分析 → 可视化」能力，对照行业方案找缺口，给出在**现有地基上最小改动、最高 ROI** 的增强路线图。

---

## 0. 结论摘要（设计目标）

OpenForgeSelf 在「**请求/响应落库**」和「**实时流通道**」两块已经做到了业界领先水平——`ChatTurn` 把每次调用的请求体、响应体、头、状态、模型、token、延迟、错误全部落库，WebSocket 实时流也已成体系。真正的缺口集中在四件事：**① 没有成本换算（无价目表）；② 没有分布式 trace 关联（ChatTurn 与 AgentRun 之间无外键，看不出一次 Agent 运行下的多步调用树）；③ 没有 LLM 专项可视化 dashboard（成本/延迟分布/错误率/模型分布）；④ 没有质量评估**。

结合行业方案（Langfuse/LangSmith/Helicone/LiteLLM + DeepEval/RAGAS），本方案给出**四阶段增强路线图**：复用现有 `ChatTurn` / `AgentRun` / `ChatTurnStreamRecorder` / WebSocket 地基，**不改业务埋点**（只在录制器与 Agent 执行器统一注入），零供应商锁定、数据自控（自托管）。MVP = 阶段 0 成本 + 阶段 1 trace 关联 + 阶段 2 最小 dashboard。

---

## 1. 本项目现有能力盘点（已落地，按可观测性四层）

### 1.1 请求/响应落库（强 — 业界对齐 Langfuse「保存每次请求与回复」）

| 实体/组件 | 路径 | 作用 |
|---|---|---|
| `ChatTurn` | `ForgeSelf.Api/Entities/ChatTurn.cs` | 单轮请求-响应主表。`RequestBody`/`ResponseBody`/`RequestHeaders`/`ResponseHeaders`/`ResponseStatus`/`Model`/`Style` 全字段持久化；已带 `PromptTokens`/`CompletionTokens`/`TotalTokens`/`FirstTokenMs`/`DurationMs`/`ErrorMessage`/`Temperature`/`MaxTokens`/`MessageCount`/`ToolCallCount`/`HasReasoning`/`RequestId` |
| `ChatSession` | `ForgeSelf.Api/Entities/ChatSession.cs` | 会话聚合，`TotalPromptTokens`/`TotalCompletionTokens` 累计 |
| `SessionEventEntity` | `ForgeSelf.Api/Entities/SessionEventEntity.cs` | append-only 事件日志，`PayloadJson` 存 record 完整序列化，文档称「真相源唯一」 |
| `AgentRun` + `AgentStepRun` | `Plugins/AIAgent/Data/Entities/` | 多步 Agent 执行轨迹：Run 含 PlanJson/状态/TotalTokens；StepRun 含 InputJson/OutputJson/ToolCallsJson/TokensUsed/DurationMs/StartedAt/CompletedAt |
| `ChatTurnStreamRecorder` | `ForgeSelf.Api/Services/ChatTurnStreamRecorder.cs` | 流式响应边回传边节流落库（满 1000 字符或 500ms），定稿写完整 `ResponseBody`；`RequestId = Guid.NewGuid()` 作单次请求关联 ID |

> **关键事实**：Provider 已在流式/非流式强制 `stream_options.include_usage=true`（`OpenAICompatibleProvider.cs`），token 由 Provider 回填后写入 `ChatTurn`（`OpenAIChatController`/`OpenAIResponsesController`/`AnthropicMessagesController`）。落库链路已成熟、已测试（`ChatTurnStreamRecorderTests`/`ChatRecordsControllerIntegrationTests`）。

### 1.2 实时通道（强 — 对标 LLM Glass 的 WebSocket 实时流）

| 组件 | 路径 | 作用 |
|---|---|---|
| `WebSocketBroadcaster` | `ForgeSelf.Api/Services/WebSocketBroadcaster.cs` | 维护所有 `/ws` 连接，信封 `{ type, data }` 广播 |
| 事件词表 | `ChatTurnStreamRecorder.cs` | `chat_record_chunk`（`{requestId,sessionId,text,isDone:false}`）、`chat_record_completed`（`{requestId,sessionId,recordId,isDone:true,error?}`） |
| 前端消费 | `ForgeSelf.Web/src/views/ChatRecordsView.vue`、`src/services/websocket.ts` | 过滤 `chat_record_chunk/completed`，实时追加/定稿 |

### 1.3 Token / 延迟（已落库，部分）

- `ChatTurn` 已持久化 token 三件套、首 token 延迟、总耗时、错误原因、消息数、工具调用数。
- ⚠️ **`UsageRecord`（`ForgeSelf.Api/Entities/UsageRecord.Biz.cs`）是「工具/插件调用」维度**（`PluginId`/`ToolId`/`ActionType`/`DurationMs`/`MetadataJson`），**不区分模型、不计算 token 成本**——它服务于 UsageStats 面板，不是 LLM 可观测。

### 1.4 可视化（局部）

| 组件 | 路径 | 现状 |
|---|---|---|
| `ChatRecordDetail.vue` | `ForgeSelf.Web/src/components/chatrecords/ChatRecordDetail.vue` | 单轮详情：模型、`tokens: input/output/total`、耗时、MaxTokens、请求/响应 JSON 树、reasoning 思维链。**看不到成本、看不到延迟分布** |
| UsageStats dashboard | `ForgeSelf.Web/src/stores/usageStats.ts` + `UsageStatsController` | 工具调用维度的趋势/Top 工具/成长曲线，**非 LLM token/成本** |
| SystemMonitor | `Plugins/SystemMonitor` | 主机监控（CPU/内存/磁盘），与 LLM 无关 |
| **Session Activity Timeline** | `dsh-ui-bundle/my-dsh-activity-timeline-client/src/panel.tsx` | **真实 overlay 面板已建，但当前 `MOCK_ENTRIES` 占位**（注释明确 "wire to ctx.sessions for live data"）。schema 已定：`{time, kind: todo/decision/action/result, text}`——是接入真实可观测数据的现成钩子 |

### 1.5 配置 / 单一真源（SSOT）

- `ForgeSelf.Web/src/data/features.ts`：功能列表 SSOT（`chat-records`/`chat`/`ai-agent`/`usage` 已登记），CI 校验。
- `ToolRegistry`（`ForgeSelf.Api/Services/ToolRegistry.cs`）：工具注册/执行管线，在 `FinalizeAsync` 写使用统计、`EmitAsync("tools/result")` 广播。
- 抽象接缝：`ILlmRuntime`/`IAIProvider`/`IChatCompletion`/`SessionEvents`——LLM 运行时与事件的可观测接口面已存在。

---

## 2. 行业通用方案（摘要，详见前置研究）

- **全栈平台**：Langfuse（MIT/自托管/OTel）、LangSmith（eval 最强/闭源）、Helicone（代理/成本最佳）、Arize Phoenix（eval）、Portkey（网关+可观测）。
- **网关/代理**：LiteLLM（51k★，改 base_url 零埋点，写 Postgres + UI，一行 YAML 转发 Langfuse/OTel）。
- **本地最漂亮实时面板**：LLM Glass（代理 + WebSocket 实时流 + SQLite）。
- **质量评估**：DeepEval（CI 门禁）、RAGAS（RAG 忠实度）、Promptfoo（红队）。
- **可观测性四层**：Trace 级可见性 + 成本归因 + 输出质量评分 + 交互可视化。

---

## 3. 差距分析（项目 vs 行业）

| 能力 | 行业标杆 | 本项目现状 | 缺口 |
|---|---|---|---|
| 请求/响应落库 | Langfuse：完整保存 | ✅ **强**（ChatTurn 全字段） | ⚠️ legacy 路径 `Usage` 可能 null（`AIServiceLlmRuntime` 注释） |
| 实时流通道 | LLM Glass：WebSocket 实时 | ✅ **强**（chat_record_*） | ⚠️ 仅文本，未流 token/延迟/成本结构化指标 |
| Token 计数 | 各行业默认 | ✅ 已落库 | — |
| **成本换算** | Langfuse/LiteLLM 核心 | ❌ 全仓 `cost` 零匹配 | ❌ 无价目表、无 cost 列 |
| **分布式 trace** | Langfuse 瀑布（run 挂多 span） | 🟡 粗粒度（AgentRun/StepRun + RequestId） | ❌ 无 SpanId/Parent；ChatTurn↔AgentRun 无外键；无瀑布图 |
| **LLM 专项可视化** | 成本/延迟 P50-P95/错误率/模型分布 | 🟡 仅工具维度（UsageStats） | ❌ 无 LLM token/成本/延迟分布 dashboard；dsh timeline mock |
| **质量评估** | DeepEval/RAGAS/faithfulness | ❌ 基本无 | ❌ 无 eval / LLM-as-judge / 人工标注 |
| 重放 / Playground | Langfuse Playground | ❌ | ❌ 无 |

**最有价值的下一步（基于现有地基）**：① 加成本（复用已落库 Model+Tokens）；② 加 TraceId 把 ChatTurn 挂到 AgentRun 出瀑布；③ 把 dsh activity-timeline 接真实数据 + 新建 LLM dashboard。

---

## 4. 增强方案（更完善的方案）— 四阶段路线图

**设计原则**
1. 复用优先：成本来自已有 `Model`+`Tokens`；trace 来自已有 `AgentRun`；实时来自已有 WebSocket。
2. 最小侵入：不在业务代码加埋点，统一在 `ChatTurnStreamRecorder`（定稿时算成本）与 Agent 执行器（注入 `AgentRunId`）两处注入。
3. 数据自控：价目表本地 JSON / `DbConfigProvider`，不依赖外部 SaaS。
4. 零自定义 token：前端严格遵循 `--el-*` + `color-mix()` 约束。

### 阶段 0｜补齐「成本」维度（最小改动，最高 ROI）

- 新增 **`ModelPricing`** 配置：本地 JSON 或 NewLife `DbConfigProvider`（`ConfigData` 表）。结构：
  ```json
  { "gpt-4o": { "inputPer1K": 0.0025, "outputPer1K": 0.01 },
    "claude-sonnet-4-6": { "inputPer1K": 0.003, "outputPer1K": 0.015 } }
  ```
- `ChatTurn` 增加 `Cost` 列（decimal）；定稿时按 `Model + PromptTokens/CompletionTokens × 单价` 计算写入（零额外 IO，复用录制器定稿点）。
- 新增 `ILlmUsageService` + `/api/llm-usage`：按 模型 / 日 / 会话 / 标签 聚合成本与 token。
- **验证**：`ChatTurn.Cost` 单测；e2e 发一次真实调用后接口返回非零成本。

### 阶段 1｜建立「分布式 trace」关联（让 ChatTurn 挂到 AgentRun）

- `ChatTurn` 增加 `TraceId` / `AgentRunId`（= 根）/`ParentStepId` 外键（不加 ParentSpanId 也行，先用 AgentRunId 两级即可）。
- Agent 执行时在 `ReactLoopAgent` / `AIAgentChatCompletion` 调 LLM 处透传 `AgentRunId`（`AgentStepRun` 已知 StepRun 上下文），由 `ChatTurnStreamRecorder` 写入。
- 前端 `ChatRecordsView` 增加 **Trace 瀑布视图**：以 `AgentRun` 为根，下挂若干 `AgentStepRun`（含工具调用）与 `ChatTurn` generations，按 `DurationMs`/`StartedAt` 画甘特瀑布。
  ```
  AgentRun #12  (Plan → 执行 → 收尾)             |████████████████|
    ├ Step: 读文件        (ToolCall)             |██|
    ├ Step: 调 LLM 规划    ChatTurn #340          |████|
    ├ Step: 写文件        (ToolCall)             |█|
    └ Step: 调 LLM 生成    ChatTurn #341          |██████|
  ```
- 复用 `dsh-ui-bundle` activity-timeline 的 `{time,kind,text}` schema，把 MOCK 换成真实 `ChatTurn`/`AgentStepRun` 流。

### 阶段 2｜LLM 专项可视化 dashboard（成本 / 延迟 / 错误率 / 模型分布）

- 新建 `LlmObservabilityView.vue`（或并入现有 UsageStats 体系）：
  - 成本时序图、按模型成本分布、token 趋势；
  - 延迟 P50/P95/P99（来自 `FirstTokenMs`/`DurationMs`）；
  - 错误率（来自 `ResponseStatus`/`ErrorMessage`）；
  - Top 会话 / 模型 / 标签；
  - 接阶段 1 的 Trace 瀑布入口。
- 数据来自阶段 0 的 `Cost` 列 + `ChatTurn` 既有字段，无需新采集。
- 把 `dsh activity-timeline` 从 mock 接真实 `ctx.sessions` 数据（注释已给切换点）。

### 阶段 3｜质量评估（可选，接行业工具）

- 离线评估管线：复用 **DeepEval / RAGAS** 对存量 `ChatTurn` 数据集跑 faithfulness/相关性，结果回灌 dashboard（或导出报告）。
- 轻量 LLM-as-judge：新增 `Evaluation` 表，对关键对话按 rubric 打分（**注意 judge 的位置/自偏好/冗长偏差，需显式 rubric 缓解**）。
- 人工标注闭环：`ChatRecordDetail` 加「标记问题 / 标注质量」按钮，形成 LangSmith 式 annotate queue 雏形。

### 阶段 4（可选）｜网关化 / 多供应商统一

- 当前已自建 OpenAI 兼容代理（`OpenAICompatibleProvider`），具备 base_url 概念；如需统一路由多家供应商，可在代理层统一采集，无需引入外部网关（避免供应商锁定）。仅在「多供应商 failover/预算」诉求出现时再考虑 LiteLLM 式网关。

---

## 5. 落地影响面与风险

| 项 | 说明 |
|---|---|
| 数据迁移 | `ChatTurn`/`AgentRun` 加列：XCode 自动迁移，向后兼容（新列默认 null/0） |
| 侵入面 | 仅 `ChatTurnStreamRecorder`（算成本）、Agent 执行器（注入 AgentRunId）两处；不改业务埋点 |
| 性能 | 成本在定稿时算一次，零额外 IO；trace 关联仅多写一个外键；不新增请求路径开销 |
| 隐私 | 敏感对话脱敏：复用 `RequestHeaders` 脱敏逻辑，按需对 `RequestBody`/`ResponseBody` 脱敏（参考 LiteLLM `turn_off_message_logging` 思路） |
| 一致性 | `SessionEventEntity` 为真相源，dashboard 一律从 `ChatTurn` 派生，不双写 |

---

## 6. 推荐优先级与 MVP

**MVP（建议先做）= 阶段 0 成本 + 阶段 1 的 `AgentRunId` 关联 + 阶段 2 最小 dashboard（成本+延迟时序）**。
理由：现有地基已强，MVP 只在两处注入 + 一个 dashboard，工作量小、ROI 高，且直接补齐行业四层里最缺的两层（成本、LLM 专项可视化）。

后续按 阶段 1 瀑布图 → 阶段 3 质量评估 递进。

---

## 7. 验证证据（如何证明达标）

- **单元**：`ChatTurn.Cost` 计算、`AgentRunId` 关联写入（扩 `ChatTurnStreamRecorderTests`）。
- **集成**：`ChatRecordsControllerIntegrationTests` 增加成本非空断言。
- **e2e（Playwright，禁 mock、真实登录）**：访问 `LlmObservabilityView` 截图确认成本/延迟图有数据；点开 Trace 瀑布确认 AgentRun 下挂 ChatTurn。
- **CI**：`features.ts` 新增 `llm-observability` 条目 + `scripts/check-features.mjs` 校验（signals 指向新 controller/view）。

---

## 8. 与 features.ts（SSOT）对接

- 新增条目：`llm-observability`（成本归因 / 分布式 trace / LLM 专项 dashboard），`signals` 含 `LlmUsageController` / `LlmObservabilityView.vue`。
- 更新 `chat-records` 条目说明：补充「成本 + trace 瀑布」能力。

> 资料：项目代码（ChatTurn.cs / AgentRun / ChatTurnStreamRecorder / WebSocketBroadcaster / dsh activity-timeline / UsageRecord）+ 前置行业研究 `llm-observability-research-2026.md`。落地前以各实体当前字段与官网价目表复核。
