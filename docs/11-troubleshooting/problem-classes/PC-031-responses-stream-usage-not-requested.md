---
id: PC-031
title: 流式 Responses 转 Chat 未请求 usage，三个 token 字段落成 0
tags: [openai-responses, streaming, usage, gateway]
invariant: 流式 Responses 入站翻译成 OpenAI Chat Completions 时，出站 body 必须带 stream_options.include_usage=true；response.completed.usage 必须把上游 prompt_tokens、completion_tokens、total_tokens 写成 input_tokens、output_tokens、total_tokens。
status: guarded
first_seen: 2026-11-12
recurrences: []
cases: [2026-11-12-responses-stream-usage-zero]
signature:
  - {id: S1, kind: test, path: "ForgeSelf.Api.Tests/Unit/LlmProxyPipelineTests.cs"}
allowlist: []
guards:
  - {level: G1, path: "ForgeSelf.Api.Tests/Unit/LlmProxyPipelineTests.cs", status: active}
lens:
  - 这条跨协议流式出站有没有向 OpenAI 兼容上游要 usage？没要的话末块不会带 token。
  - response.completed.usage 的三个字段是从 prompt_tokens / completion_tokens / total_tokens 抄来的，还是停在 0？
  - 同协议转发、Chat→Responses 丢 stream_options、非流式，有没有被这次「流式才注入」带歪？
---
# PC-031 流式 Responses 转 Chat 未请求 usage

> 事件经过见 `../cases/2026-11-12-responses-stream-usage-zero.md`。

## 1 不变量与诱因

OpenAI 兼容的 Chat Completions 只有收到 `stream_options.include_usage=true` 才会在流末尾给 token。字段名也不一样：上游是 `prompt_tokens` / `completion_tokens` / `total_tokens`，Responses 是 `input_tokens` / `output_tokens` / `total_tokens`。

本仓翻译回路已经会抄这三个数，但 `ToChatRequest` 没有向流式上游要 usage。上游不发末块时，累计器停在 0，`response.completed.usage` 三个字段都是 0。正文可以是正常的。

## 2 特征签名与扫描方法

S1 指向 `LlmProxyPipelineTests`。锁行为的是 `ResponsesToChatStream_RequestsUsage_AndMapsThreeTokenFields`：出站必须有 `include_usage`，完成事件的三个字段必须等于上游 18 / 3 / 21。

盲区：签名不扫描「行是否还在」。删掉赋值、测试还在，才会红。Anthropic 流、`output_tokens_details.reasoning_tokens` 不在本类。

## 3 Canonical fix

`ChatResponsesBridge.ToChatRequest`：入站 `stream` 为 true 时写入 `stream_options.include_usage=true`。三个字段沿用已有抄写，不新写一套事件。

被否：把 responses-proxy 的整套流事件（`content_part.*`、`output_text.done`、reasoning 条目）搬过来。本仓故意不发 AI SDK zod 不接受的类型。

被否：同协议 Chat 也注入 `include_usage`。同协议只改 `model`，字节其余原样。

## 4 守卫台账

| 等级 | 载体 | 何时触发 | 覆盖盲区 |
|------|------|----------|----------|
| G1 | `LlmProxyPipelineTests.ResponsesToChatStream_RequestsUsage_AndMapsThreeTokenFields` | `dotnet test` 过滤该类 | 不覆盖 Anthropic，不覆盖 reasoning_tokens |

## 5 案例列表

| case | 日期 | 现象一句话 | 是否逃逸 |
|------|------|------------|----------|
| 2026-11-12-responses-stream-usage-zero | 2026-11-12 | 流式 Responses 的 usage 三字段是 0 | 否（首次） |
