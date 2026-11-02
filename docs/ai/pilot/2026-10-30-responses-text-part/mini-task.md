---
risk:
  level: L1
task_kind: defect
feature: responses-text-part
expected_files:
  - ForgeSelf.Api/Controllers/UnifiedAI/OpenAIResponsesController.cs
  - ForgeSelf.Api/Controllers/UnifiedAI/OpenAIResponsesStreamEvents.cs
  - ForgeSelf.Api.Tests/Unit/OpenAIResponsesOutboundSchemaTests.cs
rollback: 还原上述三个代码文件即可回到「只发 delta + completed」的出站顺序
---

# 轻量任务：Responses 文本增量前登记 text part

## 目标

更新后的客户端不再报 `text part msg_… not found`。第一条真实 `response.output_text.delta` 之前发出 `response.output_item.added`，`item.id` 与 `item_id` 相同。

## 改动

- `OpenAIResponsesStreamEvents.TextDeltaEvents`：非空增量前先发 message 的 `output_item.added`（只发一次）。
- `FinishEvents`：开过文本项则先 `output_item.done`，再 `response.completed`。
- 控制器流式循环改调这两个方法。
- `OpenAIResponsesOutboundSchemaTests`：锁事件顺序，并禁止生产代码写出 `content_part.added`。

## 不改

- 不发 `content_part.*` / `output_text.done` / `response.in_progress`（该客户端 zod union 没有这些 type）。
- 不翻译工具调用，不把截断改成 incomplete。
- 不发布、不停 `:51888`。

## 验证

`dotnet test --filter OpenAIResponsesOutboundSchemaTests` 4 通过。现场红灯是用户在已更新的 `:51888` 上的 `text part msg_04269d8eff564d54920c70aa6ba392b7 not found`。

## Interaction Design（无界面交互声明）

**无界面交互**：本批纯宿主 `/v1/responses` 出站事件顺序修复（首条 delta 前补 `output_item.added`、收尾补 `output_item.done`），无 UI 改动；客户端经 SSE 事件流感知（text part 先登记后增量），无新增界面交互点。

## 代价与风险

正在跑的 `:51888`（2.4.21 / 2.4.22）没有这次事件。源码绿不等于该进程已变。回滚点是上述三个文件。

## Allowed

只改出站 Responses 文本事件的组装、对应测试、PC-016 复发档案。

## Forbidden

不改上游 chat/completions 解析，不翻译工具调用，不停 `:51888`，不本地发布。
