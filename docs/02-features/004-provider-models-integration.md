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

## 3. 使用指南
LLM 客户端 base URL 指向 `http://localhost:<端口>`，model 填 `ChatModelId`（见 002）。代理录制会自动归并到 `ChatSession`（见 chat 会话化）。

## 4. 测试覆盖
| 测试 | 覆盖点 |
|------|--------|
| `UnifiedAIGatewayIntegrationTests` | 三风格路由、usage 透传（`ChatCompletions_Streaming_PropagatesUsageField`） |
| `ChatCompletions_Streaming_PropagatesUsageField` | 流式 usage 字段（10/10 网关测试绿） |
