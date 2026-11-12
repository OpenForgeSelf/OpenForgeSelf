# 01 Intent

## 为什么

用户给模型设了对外别名 `default`，实际是 qwen3。OpenAI 兼容接口能换成上游模型 id。OpenAI Responses 接口把 `default` 原样发给 GPUStack，上游 404。两条链路必须同一套解析：要么都成功，要么都失败。

同时，整包请求头 JSON 超过 1000 字时，聊天轮次插入失败。

## 做什么

把模型解析收到 `AIProviderRegistry.ResolveUpstreamModel`，四个网关入口都调用它。`RequestHeaders` / `ResponseHeaders` 改为不限长。

## 到什么程度

别名 `default` 解析结果的上游 id 是 `qwen3_8_vision`。四个控制器源码不再各写一份解析。超过 1000 字的头 JSON 能插入并读回。
