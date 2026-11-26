# mini-task · LLM 代理上游缓存粘滞（xAI）

## Intent
经 ForgeSelf `/v1/chat/completions` 转到 `api.x.ai` 时，不带头 `x-grok-conv-id` / 不写 `prompt_cache_key`，前缀缓存命中会抖。要在出站前按提供方钉粘滞，且别家以后加策略而不是往管道堆 if。

## Spec
- 判定：Endpoint host 为 `x.ai` / `*.x.ai`，或提供方名 `grok|xai|x-ai`，或上游模型 id `grok-*`。不看上一轮响应。
- 入站有 `x-grok-conv-id` → allowlist 转发；不另写 `prompt_cache_key`（xAI 规定 key 优先）。
- body 已有 `prompt_cache_key` → 不覆盖。
- 两样都没有 → SHA-256(system + tools) 写入 `prompt_cache_key`。禁止 Guid / ChatSessionResolver 兜底。
- `Authorization` / `Cookie` / `Host` 等禁止转发。
- AC1：xAI 无客户端提示时写出 64 位 hex key。
- AC2：入站 conv-id 原样到上游且不写 key。
- AC3：非 xAI 提供方不改 body/头。

## Plan
1. `ILlmUpstreamAffinity` + `LlmUpstreamAffinityChain`
2. `XaiGrokAffinity`；`LlmProxyRequest.Headers`；Forwarder 安全头
3. DI 注册；`LlmUpstreamAffinityTests`

## Task
- **Allowed**：`ForgeSelf.Abstractions/LlmProxyContracts.cs`、`ForgeSelf.Api/Services/AI/Proxy/*`、`AppBuilder.cs`、对应单测、本目录工件
- **Forbidden**：改发布脚本、AIAgent 前端、停/启宿主、OAuth 冒充

## Interaction Design
无界面交互（纯 API 代理出站头/体）。
