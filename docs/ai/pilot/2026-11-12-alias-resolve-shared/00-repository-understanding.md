# 00 Repository Understanding

- 网关入口：`OpenAIChatController`（`/v1/chat/completions`）、`OpenAIResponsesController`（`/v1/responses`）、`AgentChatController`、`AnthropicMessagesController`。
- 别名数据：`AIModel.PublicAlias` / `UpstreamModelId`，查询 `AIModel.FindByPublicAlias`。本机库 Id=16：`default` → `gpustack` / `qwen3_8_vision`，Enabled=1。
- 提供方解析：`AIProviderRegistry.GetProviderByModel` 能用别名找到提供方，返回值只有 `IAIProvider`，没有上游模型 id。
- 出站：`OpenAISdkAdapter` 用 `request.Model` 调 `GetChatClient`，GPUStack 对未知模型名返回 HTTP 404。
- 轮次：`ChatTurn.RequestHeaders` 原 `DataObjectField` 长度 1000；`ChatTurnService.UpsertTurnAsync` 吞掉插入异常。
- 测试：`ForgeSelf.Api.Tests`，xUnit + FluentAssertions，夹具 `XCodeTestFixture`。
