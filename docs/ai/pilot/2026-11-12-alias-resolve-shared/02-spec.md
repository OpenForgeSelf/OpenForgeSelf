# 02 Spec

## 背景

`/v1/models` 在配置了 PublicAlias 时把别名当作对外 id。客户端会拿这个 id 调聊天接口。

## 行为

启用中的别名：提供方取别名所属提供方，出站模型名取 `UpstreamModelId`。
禁用的别名：不路由（provider 为 null），四个入口都拒绝。
`提供商:上游id`：拆开后使用，与改前 Chat 一致。
没有别名、也没有前缀：回退 `GetProviderByModel`，模型名保持原字符串。

## 验收标准

- AC1 别名 `default` 经 `ResolveUpstreamModel` 得到提供方 gpustack、上游 id `qwen3_8_vision`；禁用后 provider 为 null。
- AC2 Chat、Responses、Agent、Anthropic 四个控制器都调用 `ResolveUpstreamModel`，源码中不再有 `ResolveProviderAndModel`。
- AC3 `RequestHeaders` 与 `ResponseHeaders` 超过 1000 字可以 `SaveTurnAsync` 并按原文读回。

## 范围外

不改 GPUStack 地址拼接。不改 `ErrorMessage` 的 1000 字上限（写入前已截断）。不重启用户正在跑的宿主。

## Interaction Design

无新页面。客户端对 `/v1/chat/completions` 与 `/v1/responses` 发送同一个 `model`（别名或 `提供商:上游id`）时，两条接口选中的提供方和发给上游的模型 id 相同。别名禁用时两条都返回「找不到提供方」，都不会把别名发给上游。

## 无界面交互

操作面是上述 HTTP 入口，入参 `model`。错误仍是现有 400（无提供方）或上游错误体。本变更不新增端点。
