# 05 Evidence

## 现场红灯（Verified）

2026-10-30 对 `http://localhost:51888/v1/responses` 发流式请求（模型 `gpustack:deepseek-v4-flash`，input「只回复一个字：好」）。监听进程是 `D:\src\tools\OpenForgeSelf\versions\2.4.19.2610291518\ForgeSelf.exe`。

抓到的事件顺序：

- `response.created`
- `{"type":"response.output_text.delta","output_index":0,"content_index":0,"delta":""}`（无 `item_id`，与客户端报错值一致）
- `{"type":"response.output_text.delta",...,"delta":"\u597D"}`（仍无 `item_id`）
- `response.completed` 的 `output_text.text` 为 `""`，usage 为 0/0/0

同一接口非流式返回 `text:"好"`，usage 18/65/83。Verified。

## 单元测试（Verified）

修复后：

`dotnet test ForgeSelf.Api.Tests.dll --filter FullyQualifiedName~OpenAIResponsesOutboundSchemaTests`

结果：`已通过! - 失败: 0，通过: 3，已跳过: 0，总计: 3，持续时间: 176 ms`

输出目录：`.temp/artifacts-responses`（源码 `bin` 被 pid 103608 的 .NET Host 锁住，不能写默认输出目录）。

断言覆盖：空增量不发事件；两条 delta 的 `item_id` 相同且非空；`sequence_number` 为 1、2；完成事件正文为「你好」，usage 为 18/3/21，output item id 与 delta 的 `item_id` 相同。

## 未验证

`:51888` 仍是 2.4.19，本次源码没有打进该进程。运行实例复验 = Unknown，要等发布并更新。
