# 05 Evidence

## 现场红灯（Verified）

用户原文：已更新 `:51888`，客户端仍报 `发生未知错误 (text part msg_04269d8eff564d54920c70aa6ba392b7 not found)`。

`msg_` + 32 位十六进制与 `Accumulator.MessageItemId = $"msg_{Guid:N}"` 同形。说明已上线的包在发我们的 `item_id`，客户端用这个 id 查找 text part，没有登记记录。

对照 vercel/ai main `packages/openai/src/responses/openai-responses-language-model.ts`（本会话拉取）：`text-start` 只在 `response.output_item.added` 且 `item.type === 'message'` 时发出，id 取 `item.id`。`response.output_text.delta` 随后 enqueue `text-delta`，id 为 `item_id`（若该 output_index 尚未登记）。修复前控制器在 `response.created` 之后直接发 delta。

## 单元测试（Verified）

`dotnet test ForgeSelf.Api.Tests.csproj --filter FullyQualifiedName~OpenAIResponsesOutboundSchemaTests -p:UseArtifactsOutput=true -p:ArtifactsPath=.temp\artifacts-responses`

日志 `.temp/responses-textpart-test.log` 尾部：

`已通过! - 失败: 0，通过: 4，已跳过: 0，总计: 4，持续时间: 222 ms`

覆盖：空增量不发事件；第一条「你」之前是 `output_item.added`（sequence 1，item.type=message，item.id 与 delta.item_id 相同，content 空数组）；两条 delta 的 sequence 为 2、3；结束是 `output_item.done`（sequence 4，正文「你好」）再 `response.completed`（sequence 5，usage 18/3/21）；无正文时只有 completed、sequence 1。发射该 delta 字符串的文件必须同时有 added/done，且没有 `content_part.added`。

## 未验证

`:51888` 仍是更新前的包（2.4.21/2.4.22，含 item_id、不含本次 added/done）。本次没有发布，也没有停宿主。运行实例复验 = Unknown，要等用户同意发布并点更新。
