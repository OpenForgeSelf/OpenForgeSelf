---
feature: responses-stream-usage
task_kind: defect
risk:
  level: L1
expected_files:
  - ForgeSelf.Api/Services/AI/Proxy/ChatResponsesBridge.cs
  - ForgeSelf.Api.Tests/Unit/LlmProxyPipelineTests.cs
rollback: 删掉 ToChatRequest 里对流式 stream_options 的赋值，并删掉 ResponsesToChatStream_RequestsUsage_AndMapsThreeTokenFields
---

# 流式 Responses usage 三字段

## Intent

流式 Responses 转成 OpenAI Chat Completions 之后，`response.completed.usage` 的 `input_tokens` / `output_tokens` / `total_tokens` 要等于上游的 `prompt_tokens` / `completion_tokens` / `total_tokens`。

成功判据：`LlmProxyPipelineTests.ResponsesToChatStream_RequestsUsage_AndMapsThreeTokenFields` 通过。出站 JSON 含 `stream_options.include_usage=true`，完成事件三个数为 18 / 3 / 21。

## Spec

对照 `D:\src\responses-proxy\responses-proxy\proxy.py`：流式转换后写 `stream_options.include_usage=true`（约第 942–944 行）；完成时把三个 Chat 字段填进 Responses usage（约第 595–598 行）。

本仓抄写已在 `WireStreamTranslator.ReadChat` 和 `OpenAIResponsesStreamEvents.Completed`。缺的是开口要 usage。

无界面交互。这是网关 JSON，没有页面。

验收：

- AC1 流式 Responses→Chat 的出站 body 含 `stream_options.include_usage=true`。
- AC2 `response.completed.usage` 的三个字段等于上游 18 / 3 / 21。
- AC3 不移植 `content_part.*`、`output_text.done`、reasoning 条目。不改同协议转发，不改 Chat→Responses 丢掉 `stream_options`。

## Plan

1. `ChatResponsesBridge.ToChatRequest`：`stream` 为 true 时写入 `stream_options`。
2. 测试先红（`stream_options` 为空）再绿。

## Task

Allowed：`ChatResponsesBridge.ToChatRequest` 的流式分支；`LlmProxyPipelineTests` 一条事实。

Forbidden：改 Anthropic 写出；映射 `reasoning_tokens`；同协议注入 `stream_options`；删掉 Chat→Responses 的 `stream_options` 丢弃；发布或重启 `:51888`；去掉请求头 1000 切片。
