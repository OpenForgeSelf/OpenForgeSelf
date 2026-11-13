# Intent

> 阶段：Stage 1｜Task ID：2026-11-12-llm-proxy-thin-forward｜日期：2026-11-12

## Problem

各入站各自改模型别名、各自组封闭请求。同协议字段（`reasoning_effort`、`seed`、图片块、未知字段）在中间被丢掉。请求路径还会按提供方配置调用视觉模型并写图片缓存。内置 agent 另走一套发送栈。

## Why

后续公共逻辑要加在一处。同协议的客户端和上游说同一种 JSON，中间加工会破坏兼容。图片识别交给上游。

## Expected Outcome

一条 `LlmProxyPipeline`。HTTP Chat、HTTP Responses、HTTP Anthropic、内置 agent 的模型回合、实验 agent 都只组入站实例并调用它。同协议只改 `model`。跨协议只做标准字段映射。设置页和 API 不再暴露多模态三字段。运行时不调用视觉模型、不读写图片缓存。

## Constraints

不改工具循环、会话日志、技能、系统提示、收件箱状态机。不改 `GET /v1/models` 与 `ResolveUpstreamModel` 的规则。不删库列，不做迁移。不删 `OpenAISdkAdapter`（`AIService` 回退和测试仍用）。不新增依赖。不停宿主，不发布，不提交。

## Success Criteria

1. 同协议请求里，图片块和未知字段出现在上游 HTTP body，只有 `model` 被改写。
2. `/v1/chat/completions` 与内置 agent 的模型回合都进同一次 `ExecuteAsync`，别名只改写一次。
3. 请求路径不调用视觉模型，不读写图片缓存。
4. 空 `model` 返回 400 且不发出站。上游 4xx/5xx 的状态和 body 原样回到客户端。
