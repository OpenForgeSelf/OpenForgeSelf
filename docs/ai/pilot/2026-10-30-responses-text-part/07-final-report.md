# 07 Final Report

## Validation

- 用户现场：Verified。更新后的 `:51888` 报 `text part msg_04269d8eff564d54920c70aa6ba392b7 not found`，id 形态等于我们的 `MessageItemId`。
- AI SDK 映射：Verified。`text-start` 来自 `response.output_item.added` 的 message item，不是 `content_part.added`。
- 单元测试：Verified。`OpenAIResponsesOutboundSchemaTests` 4/4，222 ms。
- `:51888` 含本次修复：Unknown。未发布。

## Review

Final Decision: APPROVED（见 06-review.md）。范围限于出站文本事件的登记顺序。

## 问题类归档

PC-016 复发。新 case `2026-10-30-responses-text-part-not-registered` 写入 `recurrences`。索引已更新 `problem-classes/README.md`、`cases/README.md`、`docs/11-troubleshooting/index.md`。逃逸：上一轮 G2 只锁 `item_id` 字段，没锁 text-start 事件序；本轮把事件序补进同一测试。

## 重构提案

R0。事件仍在已有的 `OpenAIResponsesStreamEvents`，没有新类型层次。不把整份官方 Responses 事件面一次补齐。

## 悬空教训

无。工具调用出站翻译、截断应回 incomplete，都已在 `TODO.md`。`:51888` 尚未吃到本修复，仍挂在「发布 Responses item_id 修复并在 :51888 复验」那条，本批把待发布内容改成含 text part 登记。
