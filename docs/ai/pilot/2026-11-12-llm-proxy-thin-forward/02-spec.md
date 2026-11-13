# Specification

> 阶段：Stage 2｜Task ID：2026-11-12-llm-proxy-thin-forward

## Functional Requirements

- 入站实例只携带协议枚举和原始 JSON。算法只在 `LlmProxyPipeline.ExecuteAsync`。
- 缺 `model` 或解析不出提供方：按入站协议返回 400，不发出站。
- 同协议：`body.model` 换成上游 id，其余节点保持。响应与 SSE（含 `[DONE]`）原样写回。不补空 role 分片，不合成 finish。
- 跨协议 Chat ↔ Responses：`messages` ↔ `input`；文本 ↔ `input_text`/`output_text`；`image_url` ↔ 字符串 `input_image.image_url`；system/developer 文本拼进 `instructions`；`max_completion_tokens` 优先于 `max_tokens`；`reasoning_effort` → `reasoning.effort`；`response_format` json_object/json_schema → `text.format`。工具与 tool 消息按两侧标准形状互转。
- Chat→Responses 明确丢弃：`n`、`stop`、`presence_penalty`、`frequency_penalty`、`logit_bias`、`stream_options`、`logprobs`、`top_logprobs`、`seed`。
- Chat 上游流翻译成 Responses 事件时，文本增量走 `OpenAIResponsesStreamEvents.TextDeltaEvents`，`item_id` 稳定。
- Anthropic 入站只搬已支持字段和标准图片块（base64 或 url → Chat `image_url` data URL），不解码、不调视觉模型。
- 内置 agent 组 Chat JSON 时保留调用方原始模型 id（含别名或 `provider:model`）。测试替身无管道时仍走 `IAIProvider`。
- API 与设置页去掉 `visionModel`、`enableMultimodal`、`visionPromptTemplate`。更新提供方不覆盖这三列。

## Input

HTTP：请求体为 JSON 对象，`model` 为非空字符串。进程内：`LlmProxyRequest.Protocol` + `BodyJson`。

## Output

同协议：上游状态码与 body。跨协议且 2xx：翻译回入站协议。跨协议且状态 ≥ 400：上游 body 原样。网关 400：入站协议的错误 JSON。

## Business Rules

出站鉴权只用提供方 ApiKey（空则 `EMPTY`），不转发客户端 `Authorization`。`ChatTurn` 记录失败只打日志，不改变 HTTP 结果，也不改写出站 body。

## Boundary Conditions

`messages` / `input` 为空仍转发，由上游判定。无冒号且对不上的模型名走既有 `GetProviderByModel`（精确、前缀、默认提供方）。禁用模型得到 null 提供方 → 400。

## Error Handling

JSON 不是对象、本地模型门闩拒绝、缺少传输：400。其它非取消异常：502。取消不包装。

## Compatibility

库列保留，旧行不迁移。System.Text.Json 忽略未知入站属性，旧客户端仍传 `visionModel` 不会 400，服务端不读它。`IAIProvider` 保留给测试和 `AIService` 罐头回复。

## Non-functional Requirements

快档测试即可。不跑全量 e2e。不新增 NuGet 包。

## Interaction Design（交互设计）

设置页是本批唯一用户可见表面。三个 HTTP 网关没有新页面。

### 交互规格（点什么出现什么）

| 交互点 | 触发 | 结果（点什么出现什么） | 状态模型 | 反馈 | 空态 | 边界 |
| --- | --- | --- | --- | --- | --- | --- |
| 提供方表单 | 打开新增或编辑 | 不再出现「启用多模态」、视觉模型输入、「多模态」徽标 | 正常 | 保存请求体不含那三个字段 | 提供方列表仍可为空 | 旧数据里的三列不被这次保存抹掉 |
| HTTP Chat / Responses / Anthropic | 客户端 POST JSON | 无页面。同协议除 `model` 外原样到上游；错误时状态码与上游 body 回到客户端 | 200 / 4xx / 5xx / 网关 400 | HTTP 状态与 body | 无页面空态 | 空 model 为 400，不发出站 |

### 视觉规格

本批只删除设置项，不新增色、字号、间距。亮色和暗色都是「这些控件不存在」，没有新的选中行或标签底。对比度不因本批产生新表面。

### 交互验收标准

- AC10：设置页单测与类型里不再出现那三个字段名和「多模态」徽标用例。
- 网关无页面，验收走 AC1–AC9 的 HTTP 断言，不走浏览器走查。

### 走查符合性

批准的计划写明：只删设置字段，不新起 dev-stack，不做浏览器走查。前端以 `pnpm run check`、`pnpm run test`、`pnpm run design:check` 为准。

## Acceptance Criteria

- AC1 同协议：未知字段、`seed`、`reasoning_effort`、图片块出现在上游 body，`model` 只被改写一次。
- AC2 源码守卫：`ResolveUpstreamModel(` 只出现在管道；四个控制器不含该调用；HTTP 控制器走 `LlmProxyHttp.ForwardAsync`；实验 agent 走 `LlmProxyChatClient`。
- AC3 请求路径无 `MultimodalProcessor`、无图片缓存类型。
- AC4 空 `model` 为 400，捕获的 HTTP 调用次数为 0。
- AC5 同协议流：响应体与上游 SSE 字节一致，含 `[DONE]`。
- AC6 上游 429：状态与 body 原样，不补 `data: [DONE]`。
- AC7 Chat→Responses：`max_output_tokens` 取 `max_completion_tokens`，`reasoning.effort`、`instructions`、字符串 `image_url`、工具名就位，丢弃字段为 null，响应翻回 `chat.completion`。
- AC8 Responses 入站、Chat 上游的流：两条 `response.output_text.delta` 的 `item_id` 相同，且含 `response.output_item.added`。
- AC9 Anthropic 图片块变成一条 Chat data URL，只打一次上游，响应翻回 Anthropic `message`。
- AC10 前端类型、设置页、相关单测与 e2e 夹具去掉三个字段；`pnpm run check` / `test` / `design:check` 通过。
- AC11 `AIProviderRequest`/`AIProviderResponse` 不含三字段；仓储 `Update` 不给这三列赋值。

## Unknown

| 不确定点 | 影响 | 处理方式 |
| --- | --- | --- |
| 跨协议 usage-only 分片是否带上 `finish_reason` | agent 累加器不靠 finish 才发出工具调用 | 本批不改。现有快档未要求该分片带 finish |
| 无冒号未知模型是否落到默认提供方 | 会打到 XCode | 单测用 `test:gpt-4`，不碰别名表 |
