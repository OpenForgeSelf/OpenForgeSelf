---
id: 2026-11-13-responses-nonstream-format
title: 非流式 Responses 有回复，AI SDK 仍报格式错误
pc: PC-032
severity: P1
evidence: Verified
---
# Case：2026-11-13 非流式 Responses 格式错误

## 1 现象

用户原文：刚发本地 zip。客户端发非流式请求，界面显示格式错误。宿主库记录 id 12795，聊天记录详情 #42349。发送的是 hi，记录正常、有回复，客户端失败。

## 2 复现

只读 `~/.forgeself/ForgeSelf.db`。ChatTurn 42349 属于会话 12795。`Style=OpenAI_Responses`，`POST /v1/responses`，`ResponseStatus=200`。User-Agent：`ai/6.0.185 ai-sdk/provider-utils/4.0.58`。

落库响应是 `object=response`，`output[0]` 只有 `type/role/content[].type/text`，没有 `id`，`output_text` 没有 `annotations`。正文是正常的助手回复。

AI SDK 6 非流式 schema 要求 `message.id` 为字符串、`annotations` 为数组。缺任一即 Invalid JSON response。

## 3 根因（5 Whys + 第 6 问）

1. 为什么客户端失败？→ 回包过不了 zod。
2. 为什么过不了？→ message 没有 id，output_text 没有 annotations。
3. 为什么库里却有回复？→ 网关返回了 200 和正文，记录照存。
4. 为什么回包缺这两项？→ `ToResponsesResponse` 只抄了文本。
5. 为什么流式 usage 那次没挡住？→ 那次只给流式出站加了 `include_usage`，不经过这个非流式组装。
- **第 6 问：** 参考实现非流式已经写了 message.id 和 status。本仓非流式没跟。annotations 是 AI SDK 6 比参考实现多要的一项。

## 4 同类扫查命中表

| 位置 | 同根因？ | 处置 |
|------|----------|------|
| `ToResponsesResponse` 消息 | 是 | 补 id、status、annotations |
| `ToResponsesResponse` 工具调用 | 是 | 补 id，与 call_id 相同 |
| 流式 `OpenAIResponsesStreamEvents.Completed` | 否 | 已有 id 与 annotations |
| 流式 `include_usage` | 否 | PC-031，已修 |

## 5 修复与被否方案

修法见 PC-032。被否：再改流式 usage。
