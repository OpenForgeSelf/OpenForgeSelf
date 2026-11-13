---
id: 2026-11-13-toolchoice-without-tools
title: OpenAI Responses 直通请求带 tool_choice 无 tools，上游 400
pc: PC-033
severity: P1
evidence: Verified
---
# Case：2026-11-13 tool_choice 无 tools → 上游 400

## 1 现象

用户原文：「如图 openai响应式接口报错，解决以下」。截图：JSON 数据检查器里 error 对象 message=`When using tool_choice, tools must be set. (parameter=tool_choice)`，type=`BadRequestError`，param=`tool_choice`，code=400。前端配置的 Open Responses 协议提供方，请求经 LLM 网关代理转发到上游即被拒。

## 2 复现

TDD 复现（真实管线，非 mock 断言面）：`ForgeSelf.Api.Tests/Unit/LlmProxyPipelineTests.cs` 新增 T1–T6——`LlmProxyChatClient` 组装 → `LlmProxyPipeline.ExecuteAsync` → capture handler 收真实转发体 → 断言 `capture.Body`。红灯运行2：`失败: 6，通过: 12，总计: 18`（`ForgeSelf.Api.Tests/.temp/toolchoice-red-run2.log`），T1 断言 `Expected sent["tool_choice"] to be <null>, but found auto.`。

上游：`tool_choice` 校验依赖 `tools` 已设置；网关出站体携带 `tool_choice` 而 `tools` 缺失/为空 ⇒ 400。

## 3 根因（5 Whys + 第 6 问）

1. 为什么上游 400？→ 出站体带了 tool_choice 但没带 tools。
2. 为什么带？→ 入站体里的 tool_choice 原样透传。
3. 为什么透传没人拦？→ 首轮扫查只查「tool_choice 写出点」，补了 4 处守卫。
4. 为什么修完首轮绿跑 T1/T2 还红？→ T1/T2 走 Responses 入站 → Responses 上游**同协议直通**（`LlmProxyPipeline` L42–44：`request.Protocol == outboundProtocol ? body : ToOutbound(...)`），body 原样转发，根本不经过 bridge——写出点扫查对直通路径天然失明。
5. 为什么直通路径没有防线？→ 守卫散在各写点，没有类级汇合点。
- **第 6 问：** 用户 live 400 走的恰是直通路径——首轮扫查的盲区不是边角，是主路径。教训：同类扫查的枚举单位应是「出站报文的全部路径」（写出点 + 透传路径），不是「写出点」。

## 4 同类扫查命中表

| 位置 | 同根因？ | 处置 |
|------|----------|------|
| `LlmProxyPipeline.ExecuteAsync` 直通/桥接汇合出站 | 是 | 咽喉点守卫（canonical） |
| `ChatResponsesBridge.MapToolChoiceToResponses` | 是 | 入口源 tools 守卫 |
| `ChatResponsesBridge.MapToolChoiceToChat` | 是 | 入口源 tools 守卫 |
| `LlmProxyChatClient.BuildJson`（RequiredChatToolMode） | 是 | `&& Tools is { Count: > 0 }` |
| `OpenAIChatStreamReader` / `OpenAIChatJson.FromUnified` | 是 | `&& request.Tools is { Count: > 0 }` |
| `OpenAISdkAdapter`（L226–246/L552–573） | 否 | 已有守卫 |
| `ChatToResponsesDropped` 丢弃清单 | 否 | 有 tools 时 tool_choice 合法，不加条目 |

## 5 修复与被否方案

修法见 PC-033（5 处守卫 + 9 条测试）。绿跑：`失败: 0，通过: 18，总计: 18`（toolchoice-green-run2.log）。

被否：① tool_choice 进 `ChatToResponsesDropped`（破坏有 tools 场景）；② 只修 bridge（直通路径不在其上，T1/T2 仍红）；③ 入站剥离 tool_choice（改变入站语义，出站守卫才是契约边界）。
