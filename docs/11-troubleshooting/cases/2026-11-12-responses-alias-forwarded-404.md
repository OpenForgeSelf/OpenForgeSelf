---
id: 2026-11-12-responses-alias-forwarded-404
title: Responses 把对外别名 default 原样发给 GPUStack，上游 404
pc: PC-028
severity: P1
evidence: Verified
---
# Case：2026-11-12 Responses 别名未换成上游模型 id

## 1 现象

`POST /v1/responses`，model=`default`（PublicAlias，对应 gpustack / `qwen3_8_vision`）。日志：`Responses API 请求 - 模型: default, 提供者: gpustack`，随后 `POST http://gpustack.luxvisions-inno.com/v1/chat/completions` 返回 404。同一别名走 `/v1/chat/completions` 能换成上游模型 id。出处：用户日志 2026-11-12 13:51 与 14:07；库内 `AIModel` Id=16，`PublicAlias=default`，`UpstreamModelId=qwen3_8_vision`。

## 2 复现红灯

生产日志即红灯：出站模型名是 `default`，HTTP 404。修后守卫 `ResolveUpstreamModel_PublicAlias_SubstitutesUpstreamId` 断言别名换成 `qwen3_8_vision`。

## 3 根因（5 Whys + 第 6 问）

1. 上游 404 → GPUStack 没有名为 default 的模型。
2. 为什么发出 default → Responses 控制器把解析结果里的模型名留成客户端原字符串。
3. 为什么解析不出上游 id → 私有 `ResolveProviderAndModel` 没有 `FindByPublicAlias` 分支。
4. 为什么 Chat 可以 → Chat 的同名私有方法有这段，两份拷贝已经分叉。
5. 为什么提供方仍是 gpustack → `GetProviderByModel` 用别名找到了提供方，但不返回 `UpstreamModelId`。
- **第 6 问：** 别名反查写在控制器私有方法，没有共用函数，也没有「四个入口都必须调用」的源码守卫。后加的 Responses 抄漏了。

## 4 同类扫查命中表

| 位置 | 同根因？ | 处置 |
|------|----------|------|
| `OpenAIChatController` 私有解析 | 有别名分支（正确副本） | 已删，改调用共用方法 |
| `OpenAIResponsesController` 私有解析 | 同根因，缺别名分支 | 已删，改调用共用方法 |
| `AgentChatController` 私有解析 | 同根因，缺别名分支 | 已删，改调用共用方法 |
| `AnthropicMessagesController` 只用 `GetProviderByModel` | 同根因，不替换模型名 | 已改为共用方法并写回 `request.Model` |

## 5 修复与被否方案

修法见 PC-028。否决「只改 Responses 私有方法」：Agent / Anthropic 是同一处漏抄。

## 6 重构提案与结论

R1：解析收到 `AIProviderRegistry.ResolveUpstreamModel`。已在本任务完成。不再升 R2。

## 7 守卫落地

G1 行为测试 + G2 四控制器源码必须调用共用方法、不得再出现 `ResolveProviderAndModel`。首次发生，不是逃逸。
