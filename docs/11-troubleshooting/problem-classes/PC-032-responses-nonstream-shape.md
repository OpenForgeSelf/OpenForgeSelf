---
id: PC-032
title: 非流式 Responses 回包缺 message.id 与 output_text.annotations，AI SDK 判格式错误
tags: [openai-responses, gateway, ai-sdk]
invariant: Chat Completions 转成非流式 Responses 时，output 里的 message 必须有非空 id，output_text 必须带 annotations 数组；function_call 必须同时有 id 与 call_id。
status: guarded
first_seen: 2026-11-13
recurrences: []
cases: [2026-11-13-responses-nonstream-format]
signature:
  - {id: S1, kind: test, path: "ForgeSelf.Api.Tests/Unit/LlmProxyPipelineTests.cs"}
allowlist: []
guards:
  - {level: G1, path: "ForgeSelf.Api.Tests/Unit/LlmProxyPipelineTests.cs", status: active}
lens:
  - 非流式 Responses 的 message 有没有 id？output_text 有没有 annotations 数组？
  - function_call 是否同时写出 id 和 call_id？
  - 这和流式 usage、流式 item_id 是不是同一条写出路径？
---
# PC-032 非流式 Responses 回包形状

> 事件经过见 `../cases/2026-11-13-responses-nonstream-format.md`。

## 1 不变量与诱因

AI SDK 6（`ai/6.0.185`，`provider-utils/4.0.58`）用 zod 校验非流式 `POST /v1/responses`。`output` 里的 `message.id` 必须是字符串，`output_text.annotations` 必须是数组。缺了就抛 Invalid JSON response。网关仍返回 200，聊天记录里能看到正文。

`ToResponsesResponse` 以前只写了 type/role/text。流式完成事件已经带 id 和 annotations，非流式没有对齐。参考实现 `to_responses_format` 有 id 和 status，没有 annotations；annotations 是这个客户端多出来的必填项。

## 2 特征签名与扫描方法

S1 指向 `LlmProxyPipelineTests`。锁行为的是 `ResponsesNonStream_MessageHasIdStatusAndAnnotations`。

盲区：不覆盖推理条目、不覆盖流式事件词表。

## 3 Canonical fix

`ChatResponsesBridge.ToResponsesResponse`：消息写 `id`、`status`、`annotations: []`；工具调用写 `id` 与 `call_id`；顶层写 `created_at`；`finish_reason=length` 时 status 为 incomplete。

被否：只改流式 usage。那条修的是另一条路径，挡不住这次非流式校验。

## 4 守卫台账

| 等级 | 载体 | 何时触发 | 覆盖盲区 |
|------|------|----------|----------|
| G1 | `LlmProxyPipelineTests.ResponsesNonStream_MessageHasIdStatusAndAnnotations` | `dotnet test` 过滤该类 | 不覆盖推理条目 |

## 5 案例列表

| case | 日期 | 现象一句话 | 是否逃逸 |
|------|------|------------|----------|
| 2026-11-13-responses-nonstream-format | 2026-11-13 | 非流式 hi 有回复，客户端格式错误 | 否（首次） |
