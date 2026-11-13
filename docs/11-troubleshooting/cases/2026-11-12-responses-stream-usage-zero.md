---
id: 2026-11-12-responses-stream-usage-zero
title: 流式 Responses 的 usage 三个字段没有接住上游 token
pc: PC-031
severity: P1
evidence: Verified
---
# Case：2026-11-12 流式 Responses usage 三字段为 0

## 1 现象

用户原文：openai 响应式流式的 usage 里面三个字段没有承接好 openai 兼容格式的上游返回的 token 统计信息。

随后指定对照 `D:\src\responses-proxy\responses-proxy`（Responses → Chat Completions）。

## 2 复现红灯

`ResponsesToChatStream_RequestsUsage_AndMapsThreeTokenFields` 在补上 `include_usage` 之前失败：`LlmProxyPipelineTests.cs` 第 217 行 `sent["stream_options"]` 为空，`NullReferenceException`。日志 `.temp/tmp/responses-usage-red.log`。

上游样本是 usage-only 块：`choices: []`，`prompt_tokens=18`，`completion_tokens=3`，`total_tokens=21`。映射本身在要到 usage 之后已经能填回三个字段。缺的是出站没有向兼容上游要这块。

## 3 根因（5 Whys + 第 6 问）

1. 为什么 `response.completed.usage` 是 0？→ 累计器没看见 token。
2. 为什么没看见？→ 兼容上游的流里没有 usage 块。
3. 为什么上游不给？→ Chat Completions 流默认不给，除非 `stream_options.include_usage=true`。
4. 为什么出站没有这项？→ `ToChatRequest` 只复制了 `stream`。
5. 为什么映射看起来写过还是 0？→ `ReadChat` 和 `Completed` 只负责抄数，不负责开口要数。
- **第 6 问：** 参考实现 `proxy.py` 在流式转换后固定写 `include_usage`，再把三个 Chat 字段填进 Responses usage。本仓只搬了后半段。

## 4 同类扫查命中表

| 位置 | 同根因？ | 处置 |
|------|----------|------|
| `ChatResponsesBridge.ToChatRequest` 流式 | 是 | 已写 `include_usage` |
| `WireStreamTranslator.ReadChat` + `Completed` | 同一条链，抄写已在 | 不改 |
| 同协议 Chat 转发 | 否 | 只改 `model`，客户端自己带 `stream_options` |
| Chat→Responses 丢掉 `stream_options` | 否 | 反方向，保持丢掉 |
| 非流式 Responses→Chat | 否 | 不带 `stream_options`；整包 usage 原有映射 |
| Anthropic 流式 | 否 | 另一条写出路径，记 TODO |
| `reasoning_tokens` → `output_tokens_details` | 否 | 参考实现有，用户要的是三个字段，记 TODO |

## 5 修复与被否方案

修法见 PC-031。被否：整包移植 `proxy.py` 的事件词表。
