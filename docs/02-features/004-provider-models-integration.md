---
feature_key: F004
feature_no: 004
status: implemented
last_updated: 2026-10-06
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
1. `model` 支持 `provider:upstream_model_id` 前缀路由；无前缀且不在 provider 支持列表时回退默认 provider；
2. 三风格（OpenAI Chat / Anthropic Messages / OpenAI Responses）统一内部表示后转发上游；
3. 流式 SSE 透传 `usage`（强制 `include_usage=true`），修复"已用 token 字段缺失"。

## 2. 设计

### 2.1 网关控制器（`Controllers/UnifiedAI/`）
| 控制器 | 路由 | 风格 |
|--------|------|------|
| `OpenAIChatController` | `v1/chat/completions` | OpenAI Chat |
| `OpenAIResponsesController` | `v1/responses` | OpenAI Responses |
| `AnthropicMessagesController` | `v1/anthropic/messages` | Anthropic Messages |
| `ModelsController` | `v1/models` | 模型列表透传 |
| `AgentChatController` | `v1/agent/chat/completions` | Agent Framework 实验接口（服务端工具样本） |

### 2.2 路由铁律（`AIProviderRegistry.GetProviderByModel`）
- 支持 `provider:` 前缀解析（如 `default:qwen/qwen3-vl-4b`）；
- `MultimodalProcessor.BuildVisionRequest` 转发上游时**必须剥离 `provider:` 前缀**，否则本地 1234 不识别 → 404；
- `OpenAICompatibleProvider` 图片块 `image_url` 内层必须用 `Dictionary<string,object>` 构造（匿名类型 → `InvalidCastException`）。

### 2.3 实现位置
`Controllers/UnifiedAI/*` + `Services/AI/MultimodalProcessor.cs` + `Services/AI/AIProviderRegistry.cs`。

### 2.4 出站协议现状（2026-10-28 输入9 定标：全类型统一 SDK，现成优先+正确能用）

- **入站（使用侧）多格式**：三风格控制器（§2.1）各自转成统一内部表示 `UnifiedChatRequest` 后出站——「客户端多种格式 + 中间自动转化」成立。
- **出站（配置侧）三协议、单一 SDK 接缝（PILOT-074 落地 + 输入6→9 定标全量统一）**：`AIProviderType` = `OpenAI/OpenAIResponses/Anthropic/Custom`（`ForgeSelf.Abstractions/AIConfigModels.cs`）；`AIProviderRegistry.ReloadAsync` 按 ProviderType 分派——`OpenAIResponses` → `OpenAIResponsesProvider`（Responses 协议：`POST {base}/v1/responses`，`store:false`、不转发 `previous_response_id`、Responses-only 内建工具不转发），其余 → `OpenAICompatibleProvider`。**全部四类型的聊天/流式出站与 AIService 回退路径统一经 openai-dotnet SDK**（`OpenAISdkAdapter`，`OpenAI/2.10.0` 传递依赖零新增包；wire 细节归 SDK，适配器只做类型映射）；**BC-3：SDK 对 MaxTokens 固发 `max_completion_tokens`**（OpenAI 兼容上游与 Anthropic 兼容层均支持）。
- **Anthropic = 官方 OpenAI 兼容层（输入6→9 终态）**：Anthropic 型聊天/流式经 `DeriveAnthropicCompatBaseUrl` 归一（…/v1/messages 剥后缀 → …/v1；SDK 追加 /chat/completions），鉴权 Bearer、`max_completion_tokens`/`stream`/`tools`/system-hoist 均被兼容层支持（官方文档实证 2026-10-28）；手写 x-api-key 出站已删。**已知限制**：① Claude 4.7+ 仅接受默认 temperature/top_p（其它值 400，测试连接先暴露——上游模型约束，与实现路线无关）；② 非 workspace 域 key 需 `anthropic-workspace-id` 头（加头=手写 policy，未做）；③ thinking/缓存等原生特性不暴露。模型列表保留手写原生 `GET /v1/models`（x-api-key + anthropic-version；兼容层不含该端点）。
- **推论（已解决）**：~~仅暴露 `/v1/responses` 的上游（Azure `/openai/v1/`、vLLM 等）当前接不进来~~ → 配置 ProviderType=`OpenAIResponses` 即接入；~~Anthropic 出站语义不通~~（旧手写路径以 OpenAI 格式直发原生 /v1/messages）→ 兼容层路线真通 api.anthropic.com。测试连接探针与真实链路同 wire（全类型）。工件：`docs/ai/pilot/2026-10-27-openai-responses-upstream/`（00-07 齐，06=APPROVED）。

### 2.5 AIService 回退路径（PILOT-074 FR-8）

注册表未命中 ChatModelId 时，`AIService` 走 `ForgeSetting.config` 旧配置直连——出站已切 `OpenAISdkAdapter`（10s 超时、空回复→`reasoning_content` 往返提取回放（deepseek 系，仅回退路径付 roundtrip 成本）、失败/空→`GetFallbackResponse` 降级、流式失败 log+静默停语义保留）。

## 3. 使用指南
LLM 客户端 base URL 指向 `http://localhost:<端口>`，model 填 `ChatModelId`（见 002）。代理录制会自动归并到 `ChatSession`（见 chat 会话化）。

## 4. 测试覆盖
| 测试 | 覆盖点 |
|------|--------|
| `UnifiedAIGatewayIntegrationTests` | 三风格路由、usage 透传（`ChatCompletions_Streaming_PropagatesUsageField`） |
| `ChatCompletions_Streaming_PropagatesUsageField` | 流式 usage 字段（10/10 网关测试绿） |
