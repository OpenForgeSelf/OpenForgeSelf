# Evidence

## AC1 / AC2

Verified。`ResponsesToChatStream_RequestsUsage_AndMapsThreeTokenFields`。

红灯（补赋值之前）：`NullReferenceException`，`LlmProxyPipelineTests.cs:217`，`sent["stream_options"]` 为空。日志 `.temp/tmp/responses-usage-red.log`。

绿灯：同一测试连同 `LlmProxyPipelineTests` 全类。本回合 `.temp/tmp/responses-usage-close.log`：失败 0，通过 8，总计 8，63 ms。上一回合 `.temp/tmp/responses-usage-green.log` 同为 8/8。

上游样本：`choices: []`，`prompt_tokens=18`，`completion_tokens=3`，`total_tokens=21`。完成事件读回同样三个数。

## AC3

Verified（读码）。改动只在 `ToChatRequest` 的 `if (LlmJson.Bool(responses["stream"]))`。`ChatToResponsesDropped` 仍含 `stream_options`。未改 `WireStreamTranslator` 的事件种类。

## 未验证

`:51888` 上的 `2.4.22.2611122107` 不含这次源码。未发布，未重启。运行实例上的流仍会是旧行为。
