---
id: 2026-10-30-responses-outbound-delta-item-id
title: Open Responses 流式首事件缺 item_id，客户端判定内容格式无效
pc: PC-016
severity: P1
evidence: Verified
---
# Case：2026-10-30 Open Responses 出站 delta 缺 item_id

> Case 只讲这一次。规则与修法见 PC-016。

## 1 现象

对 `http://localhost:51888/v1/responses`（宿主 `2.4.19.2610291518`）用 Open Responses 流式请求 `gpustack:deepseek-v4-flash`。客户端报「AI 返回的内容格式无效」，zod 拒绝的值正好是：

`{"type":"response.output_text.delta","output_index":0,"content_index":0,"delta":""}`

第一分支只失败在 `item_id`：expected string, received undefined。

## 2 复现红灯

2026-10-30 对运行实例 `POST /v1/responses`（`stream:true`，input「只回复一个字：好」）抓到的 SSE：

1. `response.created` 通过
2. `response.output_text.delta` 且 `delta:""`，无 `item_id`（与报错值一致）
3. 下一条才是 `delta:"\u597D"`，同样无 `item_id`
4. `response.completed` 的 `output_text.text` 为 `""`，usage 三个数都是 0

同一模型非流式 `POST /v1/responses` 返回 `text:"好"`，usage `18/65/83`。说明上游 chat/completions 有正文，坏在出站流的组装。

单元红灯未单独留下：当时 `ForgeSelf.Api\bin` 被本机 .NET Host（pid 103608）锁住，过滤测试在断言前就 MSB3027。现场 SSE 是红灯证据。修复后 `OpenAIResponsesOutboundSchemaTests` 3/3 通过（176 ms，Verified）。

## 3 根因（5 Whys + 第 6 问）

1. 为什么客户端说格式无效？→ 流里的 `response.output_text.delta` 没有 `item_id`。
2. 为什么没有？→ `OpenAIResponsesController.HandleStreamAsync` 在读上游之前先发了一条只含 `output_index`、`content_index`、`delta:""` 的事件，后续真增量同样不写 `item_id`。
3. 为什么要发空 delta？→ 用它充当“文本开始”，但 Open Responses 的文本增量 schema 不接受缺 `item_id` 的事件。
4. 为什么完成事件正文是空的？→ `response.completed` 把 `text` 和 usage 写死，不读累计分片。
5. 为什么非流式是好的？→ 非流式走 `ConvertToResponsesResponse`，会抄上游的 content 和 usage。两条路径没有共用事件组装。
- **第 6 问**：出站 Responses 流没有契约测试，只有入站解析测试。空对象能编译，守卫不存在，所以缺字段能进 2.4.19。

## 4 同类扫查命中表

| 位置 | 同根因？ | 处置 |
|------|----------|------|
| `OpenAIResponsesController.cs` 空 delta 与无 item_id 的文本增量 | 是 | 已删占位，改走 `OpenAIResponsesStreamEvents` |
| `OpenAIResponsesController.cs` completed 写死空正文和零 usage | 是（同一出站组装） | 已改为累计正文与最后一次 usage |
| `OpenAISdkAdapter` 入站解析允许缺 item_id | 否（读上游，不是出站） | 不改 |
| 工具调用未译成 `response.function_call_arguments.*` | 否 | TODO：Responses 出站未转发 function_call |

## 5 修复与被否方案

修法 = PC-016 §3。否决「只给空占位补 item_id 仍发送」：空 delta 不该出现在线上。

## 6 重构提案与结论

**R1 已在本任务内做完**：事件组装抽到 `OpenAIResponsesStreamEvents`，控制器只负责写出。不再抽更大的 Responses 序列化框架（R0 对工具调用翻译：那是另一条缺口，见 TODO）。

## 7 守卫落地

G2：`ForgeSelf.Api.Tests/Unit/OpenAIResponsesOutboundSchemaTests.cs`（源码窗口必须含 `item_id` + 序列化断言）。首次发生，无逃逸。

## 8 教训回流

后续改 `POST /v1/responses` 出站流时先过 PC-016 lens。`:51888` 要等含本次源码的包发布并更新后才变。
