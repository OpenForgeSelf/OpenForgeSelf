---
feature_key: F004
feature_no: 004
status: implemented
last_updated: 2026-11-12
aliases: ["004-provider-models-integration"]
---

# 004 Provider 与模型集成（统一网关路由）— 功能需求与设计

> 功能编号：004
> 状态：已实现
> 关联：001/002；统一 AI 网关（01-architecture §3.1）（历史 specs/004 已弃用）
> 最后更新：2026-08-12

## 1. 功能需求

### 1.1 背景
LLM 客户端（GitHub Copilot 等）按 OpenAI / Anthropic / OpenAI Responses 协议直连后端，后端需把请求按 `model` 路由到正确的 provider 上游，并归一三种风格。

### 1.2 目标
1. `model` 支持公开别名与 `provider:upstream_model_id`。改写只发生在 `LlmProxyPipeline`，经 `AIProviderRegistry.ResolveUpstreamModel`；
2. 同协议原样转发 JSON（只改 `model`）。Chat 与 Responses 跨协议只做标准字段映射。Anthropic 入站先落到 Chat；
3. 流式同协议原样回写上游字节，不强制 `include_usage`，不补 `[DONE]`。

## 2. 设计

### 2.1 网关控制器（`Controllers/UnifiedAI/`）
| 控制器 | 路由 | 风格 |
|--------|------|------|
| `OpenAIChatController` | `v1/chat/completions` | OpenAI Chat |
| `OpenAIResponsesController` | `v1/responses` | OpenAI Responses |
| `AnthropicMessagesController` | `v1/anthropic/messages` | Anthropic Messages |
| `ModelsController` | `v1/models` | 模型列表（见 §2.6） |
| `AgentChatController` | `v1/agent/chat/completions` | Agent Framework 实验接口（服务端工具样本） |

### 2.2 路由铁律（`AIProviderRegistry.GetProviderByModel`）
- 支持 `provider:` 前缀与无冒号公开别名。生产路径不要在调用方剥前缀，交给 `ResolveUpstreamModel`；
- 图片块不再本地识别。同协议原样转发；Chat→Responses 时 `image_url` 变成 Responses 的字符串 `input_image.image_url`。

### 2.3 实现位置
`Controllers/UnifiedAI/*`（薄壳）+ `Services/AI/Proxy/LlmProxyPipeline.cs` + `Services/AI/AIProviderRegistry.cs`。公开代理不再经 `CompleteChatAsync`。同协议 POST 复用已配置的 `OpenAIClient` 传输（地址、ApiKey、重试 0），body 用原始 JSON。

### 2.4 出站协议现状（2026-10-28 输入9 定标：全类型统一 SDK，现成优先+正确能用）

- **入站（使用侧）多格式**：三风格控制器（§2.1）各自转成统一内部表示 `UnifiedChatRequest` 后出站——「客户端多种格式 + 中间自动转化」成立。
- **出站（配置侧）三协议、单一 SDK 接缝（PILOT-074 落地 + 输入6→9 定标全量统一）**：`AIProviderType` = `OpenAI/OpenAIResponses/Anthropic/Custom`（`ForgeSelf.Abstractions/AIConfigModels.cs`）；`AIProviderRegistry.ReloadAsync` 按 ProviderType 分派——`OpenAIResponses` → `OpenAIResponsesProvider`（Responses 协议：`POST {base}/v1/responses`，`store:false`、不转发 `previous_response_id`、Responses-only 内建工具不转发），其余 → `OpenAICompatibleProvider`。**全部四类型的聊天/流式出站与 AIService 回退路径统一经 openai-dotnet SDK**（`OpenAISdkAdapter`，`OpenAI/2.10.0` 传递依赖零新增包；wire 细节归 SDK，适配器只做类型映射）；**BC-3：SDK 对 MaxTokens 固发 `max_completion_tokens`**（OpenAI 兼容上游与 Anthropic 兼容层均支持）。
- **Anthropic = 官方 OpenAI 兼容层（输入6→9 终态）**：Anthropic 型聊天/流式经 `DeriveAnthropicCompatBaseUrl` 归一（…/v1/messages 剥后缀 → …/v1；SDK 追加 /chat/completions），鉴权 Bearer、`max_completion_tokens`/`stream`/`tools`/system-hoist 均被兼容层支持（官方文档实证 2026-10-28）；手写 x-api-key 出站已删。**已知限制**：① Claude 4.7+ 仅接受默认 temperature/top_p（其它值 400，测试连接先暴露——上游模型约束，与实现路线无关）；② 非 workspace 域 key 需 `anthropic-workspace-id` 头（加头=手写 policy，未做）；③ thinking/缓存等原生特性不暴露。模型列表保留手写原生 `GET /v1/models`（x-api-key + anthropic-version；兼容层不含该端点）。
- **推论（已解决）**：~~仅暴露 `/v1/responses` 的上游（Azure `/openai/v1/`、vLLM 等）当前接不进来~~ → 配置 ProviderType=`OpenAIResponses` 即接入；~~Anthropic 出站语义不通~~（旧手写路径以 OpenAI 格式直发原生 /v1/messages）→ 兼容层路线真通 api.anthropic.com。测试连接探针与真实链路同 wire（全类型）。工件：`docs/ai/pilot/2026-10-27-openai-responses-upstream/`（00-07 齐，06=APPROVED）。

### 2.5 薄转发（2026-11-12，取代公开代理上的封闭 DTO 路径）

公开入站（Chat / Responses / Anthropic）和内置 agent 的模型回合都进 `ILlmProxyPipeline.ExecuteAsync`。同协议只改 `model`。跨协议只做标准映射，不做识图、缓存或其它加工。`OpenAISdkAdapter` 仍留给 `AIService` 无提供方时的罐头回复和既有测试，本批不删。设置页不再提供「启用多模态 / 视觉模型」。

流式 Responses 入站、出站是 OpenAI Chat Completions 时，`ChatResponsesBridge.ToChatRequest` 固定写 `stream_options.include_usage=true`。上游末块的 `prompt_tokens` / `completion_tokens` / `total_tokens` 填进 `response.completed.usage` 的 `input_tokens` / `output_tokens` / `total_tokens`（`WireStreamTranslator.ReadChat` → `OpenAIResponsesStreamEvents.Completed`）。同协议不注入这一项。Chat→Responses 仍丢掉客户端的 `stream_options`。非流式不带 `stream_options`。

非流式 Responses 回包（`ToResponsesResponse`）对照 `responses-proxy` 的 `to_responses_format`：消息带 `id` 与 `status`，工具调用同时带 `id` 和 `call_id`，顶层有 `created_at`。`output_text.annotations` 必须是数组（可为空）。AI SDK 6 非流式 schema 缺 `id` 或缺 `annotations` 会判 Invalid JSON response，库里仍能看到正文。

应用内聊天 `POST /api/chat`（`ChatController`）不在这条管道上。它仍调用 `IAIService.ChatAsync` / `ChatStreamAsync`：带 `chatModelId` 时走 `IAIProvider` 的封闭 `UnifiedChatRequest`，否则走 `OpenAISdkAdapter` 直连 `ForgeSetting` 里的旧端点。这里的「内置 agent」是 AIAgent 模型回合（`AIAgentService.ReadPipelineChunks` 组 `LlmProxyRequest`）和实验页 `AgentChatController` 的 `LlmProxyChatClient`。薄转发约束写明不改会话日志，并保留 `OpenAISdkAdapter`（`docs/ai/pilot/2026-11-12-llm-proxy-thin-forward/01-intent.md`）。所以应用内聊天没有把请求交给出站管道，记录、别名和协议也没有复用到这一条。这是当时范围留下的缺口。会话投影是否在返回前等待，不决定这条路由。

### 2.6 `GET /v1/models` 的 `data[].id` 语义（2026-11-03 实测对照源码；**2026-11-14 修订：别名模型额外返回原名**，工件 `docs/ai/pilot/2026-11-14-models-alias-raw/`）

实现：`ForgeSelf.Api/Controllers/UnifiedAI/ModelsController.cs`（`ListModels` / `GetModel`）；id 投影真源 = `AIModel.GetGatewayModelListIds`（`AIModel.Biz.cs`）。

| 场景 | 列表中返回的条目 | 是否含 `提供商:` 前缀 |
|------|-------------|----------------------|
| 未配置 `PublicAlias` | 单条：`提供商:上游模型id`（与 `ChatModelId` 一致） | **是**（例：`OpenAI:gpt-4o`；2026-11-03 PILOT 落地） |
| 已配置 `PublicAlias` | **两条**：别名条目 + 原名条目 `提供商:上游模型id`，别名在前（2026-11-14 需求调整：原名不再被别名遮蔽；列表不按 `Enabled` 过滤） | 别名条**否**、原名条**是**（例：`default` 与 `gpustack:qwen3_8_vision` 并存） |
| 同名字段 | 两条条目 `owned_by` = 提供方显示名、`created` 相同 | 前缀在 **owned_by**，不在 **id** |

与聊天路由的关系：`POST /v1/chat/completions`、`POST /v1/responses`、Agent 测试接口、Anthropic Messages 的 `model` 都走 `AIProviderRegistry.ResolveUpstreamModel`（2026-11-12 收口；此前只有 Chat 会把别名换成上游 id，Responses 把别名原样出站，GPUStack 对未知模型名返回 404）。带前缀的 `提供商:上游模型id` 仍可显式路由。客户端从 `/v1/models` 原样取 `id` 再发起对话时，无别名模型用 `provider:` 前缀；有别名模型**两个 id 都可回填**——别名条目经 `ResolveUpstreamModel` 出站前换成 `UpstreamModelId`，原名条目走 `provider:` 拆分路由。`GET /v1/models/{model}` 支持按 `提供商:上游id` 或别名查询，行为未随 2026-11-14 调整变化。

### 2.5 AIService 回退路径（PILOT-074 FR-8）

注册表未命中 ChatModelId 时，`AIService` 走 `ForgeSetting.config` 旧配置直连——出站已切 `OpenAISdkAdapter`（10s 超时、空回复→`reasoning_content` 往返提取回放（deepseek 系，仅回退路径付 roundtrip 成本）、失败/空→`GetFallbackResponse` 降级、流式失败 log+静默停语义保留）。

### 2.6 上游 HTTP 错误全量透传（2026-11-03 落地）

共用 `ForgeSelf.Api/Services/AI/UnifiedUpstreamErrors.cs`：**凡上游 HTTP 4xx/5xx**（`ClientResultException.Status` / `HttpRequestException.StatusCode`，含 **InnerException 解包**）均原样 `StatusCode`，不再压成 500。

| 模式 | 行为 |
|------|------|
| **非流式**（Chat / Responses / Anthropic / Agent 实验路由） | `StatusCode(upstream)`；body 优先回传上游 JSON `error` 对象（SDK `Error` 或 Message 内嵌 JSON），否则 `{ error: { message, type, code } }` |
| **流式 Chat / Responses / Agent** | `data: {"error":…}` + `data: [DONE]` |
| **流式 Anthropic** | `event: error` + snake_case |

**type 映射**：Message 内 JSON 的 `error.type` 优先；否则 429→`rate_limit_error`、401/403→`authentication_error`、其余 4xx→`invalid_request_error`、5xx→`upstream_error`。

**限制**：流式在首个 SSE 分片已发出后 HTTP 常为 200；客户端读 SSE 内 `error`。`!Response.HasStarted` 时设 HTTP 状态码。

工件：`docs/ai/pilot/2026-11-03-gateway-stream-upstream-passthrough/mini-task.md`。

## 3. 使用指南
LLM 客户端 base URL 指向 `http://localhost:<端口>`，model 填 `ChatModelId`（见 002）。代理录制会自动归并到 `ChatSession`（见 chat 会话化）。

## 4. 测试覆盖
| 测试 | 覆盖点 |
|------|--------|
| `UnifiedAIGatewayIntegrationTests` | 三风格路由、usage 透传（`ChatCompletions_Streaming_PropagatesUsageField`） |
| `ChatCompletions_Streaming_PropagatesUsageField` | 流式 usage 字段（10/10 网关测试绿） |
