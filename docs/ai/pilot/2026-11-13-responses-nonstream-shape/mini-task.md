---
feature: responses-nonstream-shape
task_kind: defect
risk:
  level: L1
expected_files:
  - ForgeSelf.Api/Services/AI/Proxy/ChatResponsesBridge.cs
  - ForgeSelf.Api.Tests/Unit/LlmProxyPipelineTests.cs
rollback: 恢复 ToResponsesResponse 里不写 id、status、annotations 的旧对象
---

# 非流式 Responses 回包形状

## Intent

非流式 Responses 客户端收到的 message 带 id，output_text 带 annotations 数组。ChatTurn 42349 那种回包不再缺这两项。

## Spec

对照 `responses-proxy` 的 `to_responses_format`：消息 id、status、created_at，工具调用 id 与 call_id。另加空 `annotations`，因为 AI SDK 6.0.185 的非流式 schema 把它当成必填。

无界面交互。这是网关 JSON。

验收：

- AC1 message.id 以 msg_ 开头，status 为 completed，annotations 为空数组。
- AC2 function_call 的 id 与 call_id 都是上游的 call id。
- AC3 usage 三字段仍从 prompt/completion/total 抄回，reasoning_tokens 从 completion_tokens_details 抄。

## Plan

只改 `ToResponsesResponse`。

## Task

Allowed：`ChatResponsesBridge.ToResponsesResponse` 与一条管道测试。

Forbidden：改流式事件词表；发布或重启宿主。
