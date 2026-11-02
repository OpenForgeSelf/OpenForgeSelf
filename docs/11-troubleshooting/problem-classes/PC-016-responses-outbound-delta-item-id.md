---
id: PC-016
title: 出站 Open Responses 文本增量缺 item_id，严格客户端在首个空 delta 上拒绝整段流
tags: [openai-responses, streaming, protocol, gateway]
invariant: 出站 response.output_text.delta 必须带非空字符串 item_id，且不得先发 delta 为空字符串的占位事件；第一条真实增量之前必须已发 response.output_item.added（item.type=message，item.id 与后续 item_id 相同），结束时发同一 id 的 response.output_item.done；不发客户端 zod 不接受的 content_part.*；response.completed 的 output 文本与 usage 必须来自本段流的累计结果。
status: guarded
first_seen: 2026-10-30
recurrences: [2026-10-30-responses-text-part-not-registered]
cases: [2026-10-30-responses-outbound-delta-item-id]
signature:
  - {id: S1, kind: test, path: ForgeSelf.Api.Tests/Unit/OpenAIResponsesOutboundSchemaTests.cs}
allowlist: []
guards:
  - {level: G2, path: ForgeSelf.Api.Tests/Unit/OpenAIResponsesOutboundSchemaTests.cs, status: active}
lens:
  - 新增或改写出站 Responses SSE 时，response.output_text.delta 是否带稳定 item_id，空增量是否被丢掉而不是发成 delta:""？第一条真实增量之前是否已有 response.output_item.added，且 item.id 与 item_id 相同？
  - response.completed 的 output.text 与 usage 是否来自本段累计，而不是写死空串和 0？
  - 上游 chat/completions 的工具调用分片有没有翻译成 response.function_call_arguments.*（本类不覆盖，见 TODO）？
---
# PC-016 出站 Open Responses 文本增量缺 item_id

> 事件经过见 `../cases/2026-10-30-responses-outbound-delta-item-id.md`。

## 1 不变量与诱因

**不变量**：`POST /v1/responses` 的流要同时过两道客户端门。第一道是 zod：`response.output_text.delta` 的 `item_id` 必须是字符串，不能先发 `delta:""`。第二道是 AI SDK 的 part 状态机：`text-start` 只由 `response.output_item.added`（`item.type=message`，id=`item.id`）登记；随后的 `text-delta` 使用 delta 的 `item_id`。没登记就报 `text part {id} not found`。结束用同一 id 的 `response.output_item.done` 做 `text-end`。不发 `content_part.*`（该客户端 union 没有这些 type）。`response.completed` 的正文和 usage 必须是这段流的累计结果。

**诱因**：入站翻译先写了一条没有 `item_id` 的空 delta，用来“打开”文本通道。严格 schema 在这条上就失败，后面的真增量到不了客户端。完成事件另写死了空正文和零 usage，和流里已经收到的字、token 脱节。

## 2 特征签名与扫描方法

- **S1**：`OpenAIResponsesOutboundSchemaTests` 扫 `ForgeSelf.Api/**/*.cs` 里每一处 `"response.output_text.delta"`：同一窗口内必须有 `item_id`，所在文件必须同时写出 `response.output_item.added` 与 `response.output_item.done`，且不得写出 `response.content_part.added`。行为断言：空增量不产出事件；第一条真实增量前的事件是 `output_item.added`（item.type=message，item.id 与 delta.item_id 相同）；结束先 `output_item.done` 再 `response.completed`，正文与 usage 来自累计。无正文时不发 added/done。
- **盲区**：运行中的已发布宿主（`:51888` 的 versions 目录）不在源码扫描范围内。签名只约束仓库源码。

## 3 Canonical fix

文本增量只在 `delta` 非空时发出，并带上本段稳定的 `item_id`。第一条非空增量之前先发 `response.output_item.added`（`item.id` 与该 `item_id` 相同，`type=message`，`content=[]`）。流结束且开过文本项时先发 `response.output_item.done`，再发 `response.completed`。参考实现：`ForgeSelf.Api/Controllers/UnifiedAI/OpenAIResponsesStreamEvents.cs`。

被否方案：补 `content_part.added`。AI SDK 的 `text-start` 不看这个事件；该客户端第一轮 zod union 也没有这个 type，发出去会回到 `invalid_union`。只给空 delta 补 `item_id` 同样被否：空 delta 不是登记 part 的事件。

## 4 守卫台账

| 等级 | 载体 | 何时触发 | 覆盖盲区 |
|------|------|----------|----------|
| G2 | `ForgeSelf.Api.Tests/Unit/OpenAIResponsesOutboundSchemaTests.cs` | `dotnet test --filter OpenAIResponsesOutboundSchemaTests` | 已发布二进制、工具调用事件翻译 |

## 5 案例列表

| case | 日期 | 现象一句话 | 是否逃逸 |
|------|------|------------|----------|
| 2026-10-30-responses-outbound-delta-item-id | 2026-10-30 | 空 delta 无 item_id，客户端 zod 拒绝 | 否（首次） |
| 2026-10-30-responses-text-part-not-registered | 2026-10-30 | item_id 已发出，但 delta 前没有 output_item.added，客户端报 text part not found | 是（守卫只锁了 item_id 字段，没锁 text-start 登记） |

## 6 逃逸

PC-016 第一次守卫把「delta 对象上有 item_id」当成整段流合法。AI SDK 在 zod 之后还有 part 状态机：`text-delta` 的 id 必须先被 `output_item.added` 登记。字段守卫绿了，状态机仍红。本次把「added 在第一条 delta 之前、id 相同、不发 content_part」写进同一 G2 测试。不再加新的 hook。
