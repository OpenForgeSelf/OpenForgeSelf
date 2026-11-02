---
id: 2026-10-30-responses-text-part-not-registered
title: Open Responses 流已带 item_id，但客户端报 text part not found
pc: PC-016
severity: P1
evidence: Verified
---
# Case：2026-10-30 text part not found

> Case 只讲这一次。规则与修法见 PC-016。

## 1 现象

用户已把 `:51888` 更新到含 PC-016 item_id 修复的包。某个客户端改报：

`发生未知错误 (text part msg_04269d8eff564d54920c70aa6ba392b7 not found)`

`msg_` + 32 位十六进制与 `OpenAIResponsesStreamEvents` 的 `MessageItemId`（`msg_{Guid:N}`）同形。说明新包已经把这个 id 放进了 delta，客户端拿它去找已登记的 text part，找不到。

## 2 复现红灯

红灯是用户在更新后的 `:51888` 上看到的上述原文（Verified：用户报告 + id 形态与源码生成式一致）。

对照 vercel/ai `openai-responses-language-model.ts`（本会话拉取的 main）：

- `response.output_item.added` 且 `item.type === 'message'` 时 `enqueue({ type: 'text-start', id: item.id })`，并把 `activeOutputItemIds[output_index] = item.id`。
- `response.output_text.delta` 走 `resolveOutputItemId`：有 `output_index` 时优先用已登记 id，否则用事件上的 `item_id`，然后 `enqueue({ type: 'text-delta', id })`。
- 没有 text-start 时，AI SDK 对 text-delta 报 `text part ${id} not found`。

修复前源码顺序是 `response.created` → `response.output_text.delta` → `response.completed`，没有 `output_item.added`。这与报错一致。

## 3 根因（5 Whys + 第 6 问）

1. 为什么报 text part not found？→ text-delta 的 id 不在 `activeTextContent`。
2. 为什么没登记？→ text-start 只从 `response.output_item.added` 的 message item 来，出站没发这个事件。
3. 为什么 item_id 修复后仍失败？→ 上一轮只满足 zod 的 `item_id: string`，没有满足下一层状态机。
4. 为什么不能改发 `content_part.added`？→ 该客户端第一轮 zod union 没有这个 type；AI SDK 的 text-start 也不读它。
5. 为什么 id 正好是我们的 msg_？→ delta 的 `item_id` 被原样当成 text-delta 的 id。
- **第 6 问**：PC-016 的 G2 只扫描 delta 初始化块里有没有 `item_id`。字段在、事件序错，测试仍绿。这是守卫口径窄，不是新的上游故障。

## 4 同类扫查

| 位置 | 判定 |
|------|------|
| `ForgeSelf.Api` 中 `"response.output_text.delta"` | 只在 `OpenAIResponsesStreamEvents.TextDeltaEvents`。同根因，本批修。 |
| `content_part.added` | 生产代码不发。保持不发。 |
| 工具调用 `function_call_arguments` | 仍未翻译。异根因，已在 TODO，本批不修。 |
| 截断应回 incomplete | 异根因，另有待批 pilot，本批不修。 |

## 5 修复

`TextDeltaEvents`：第一条非空增量先发 `response.output_item.added`（item.id = 后续 item_id，type=message，content=[]），再发 delta。`FinishEvents`：开过文本项则先 `response.output_item.done`，再 `response.completed`。无正文不发 added/done。
