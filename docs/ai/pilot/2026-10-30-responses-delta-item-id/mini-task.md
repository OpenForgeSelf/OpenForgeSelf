---
risk:
  level: L1
task_kind: defect
feature: responses-outbound-delta
expected_files:
  - ForgeSelf.Api/Controllers/UnifiedAI/OpenAIResponsesController.cs
  - ForgeSelf.Api/Controllers/UnifiedAI/OpenAIResponsesStreamEvents.cs
  - ForgeSelf.Api.Tests/Unit/OpenAIResponsesOutboundSchemaTests.cs
rollback: 还原上述三个代码文件即可恢复 2.4.19 的出站流行为
---

# 轻量任务：出站 Responses 文本增量补 item_id

## 目标

`POST /v1/responses` 的流式文本增量能过 Open Responses / AI SDK 校验：不再发送无 `item_id` 的空 delta，完成事件带回累计正文和 usage。

## 改动

- `OpenAIResponsesController.HandleStreamAsync`：删掉空占位 delta，增量与完成事件改走 `OpenAIResponsesStreamEvents`。
- 新文件 `OpenAIResponsesStreamEvents.cs`：非空 delta 带稳定 `item_id` 与 `sequence_number`；完成事件复用该 id，text/usage 来自累计。
- `OpenAIResponsesOutboundSchemaTests`：源码窗口必须含 `item_id`，并断言序列化形状。

## 不改

- 非流式 `ConvertToResponsesResponse`。
- 入站 `OpenAISdkAdapter` 对上游事件的解析。
- 工具调用没有翻译成 `response.function_call_arguments.*`（另记 TODO）。
- 不发布、不重启 `:51888`。

## 验证

`dotnet test --filter OpenAIResponsesOutboundSchemaTests` 3 通过。现场红灯是 `:51888` 上抓到的空 delta SSE。

## Interaction Design（无界面交互声明）

**无界面交互**：本批纯宿主 `/v1/responses` 出站 SSE 事件组装修复，无 UI 改动；客户端经 SSE 事件流感知（增量事件补 `item_id`/`sequence_number`，完成事件带回累计正文与 usage），无新增界面交互点。

## 代价与风险

已发布的 `2.4.19.2610291518` 仍会发出旧事件，直到含本次源码的包装上并更新。回滚点是上述三个文件。

## Allowed

只改出站 Responses 文本事件的组装与对应测试、问题类档案。

## Forbidden

不改上游 chat/completions 解析，不翻译工具调用，不停 `:51888` 进程，不本地发布。
