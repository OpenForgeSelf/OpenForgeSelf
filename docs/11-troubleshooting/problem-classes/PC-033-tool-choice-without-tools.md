---
id: PC-033
title: 出站请求 tool_choice 无 tools 未丢弃，上游 400（BadRequestError）
tags: [openai-responses, gateway, tool-choice, outbound-contract]
invariant: 任何出站 LLM 请求体（直通或桥接产物）：tools 缺失或空数组 ⇒ 不得携带 tool_choice；tools 非空 ⇒ tool_choice 原样保留（auto/required/function 对象三形态）。
status: guarded
first_seen: 2026-11-13
recurrences: []
cases: [2026-11-13-toolchoice-without-tools]
signature:
  - {id: S1, kind: test, path: "ForgeSelf.Api.Tests/Unit/LlmProxyPipelineTests.cs"}
  - {id: S2, kind: test, path: "ForgeSelf.Api.Tests/Plugins/AIAgent/OpenAIChatJsonTests.cs"}
allowlist: []
guards:
  - {level: G2, path: "ForgeSelf.Api/Services/AI/Proxy/LlmProxyPipeline.cs", status: active}
  - {level: G2, path: "ForgeSelf.Api/Services/AI/Proxy/ChatResponsesBridge.cs", status: active}
  - {level: G2, path: "ForgeSelf.Api/Services/AI/Proxy/LlmProxyChatClient.cs", status: active}
  - {level: G2, path: "Plugins/AIAgent/MafLoop/OpenAIChatStreamReader.cs", status: active}
  - {level: G1, path: "ForgeSelf.Api.Tests/Unit/LlmProxyPipelineTests.cs", status: active}
  - {level: G1, path: "ForgeSelf.Api.Tests/Plugins/AIAgent/OpenAIChatJsonTests.cs", status: active}
lens:
  - 出站请求体里 tool_choice 与 tools 是不是同源判定（源请求缺 tools/空数组时 tool_choice 必须不在）？
  - 同协议直通路径（pipeline 不经过 bridge）有没有被同类扫查覆盖？
  - 新增出站点/转发路径时，是否把「从属字段守卫」放在了类级汇合点而不是各写点？
---
# PC-033 出站 tool_choice 无 tools 未丢弃 → 上游 400

> 事件经过见 `../cases/2026-11-13-toolchoice-without-tools.md`。

## 1 不变量与诱因

`tool_choice` 是 `tools` 的从属字段：上游（OpenAI 系）在校验 `tool_choice` 时要求 `tools` 已设置，否则 400 `When using tool_choice, tools must be set. (parameter=tool_choice)`（type=BadRequestError）。

诱因是「出站写点各自为政」：tool_choice 的写出分散在 5 处（bridge 两向映射、M.E.AI 路径、Unified 路径、pipeline 直通透传），每处都只看「自己这一层要不要写」，没有一处保证「与 tools 的从属关系」。首轮扫查只盯「写出点」，漏判了**直通路径**（body 原样转发，根本没有写出点）——这正是用户 live 400 的实际路径。

## 2 特征签名与方法

S1 指向 `LlmProxyPipelineTests`（T1–T6：直通/桥接/ChatClient 三路 × 无 tools 丢 / 有 tools 留，阳性两条守行为不变）。S2 指向 `OpenAIChatJsonTests`（Unified 请求序列化 3 例）。咽喉点守卫本体（`LlmProxyPipeline.cs`）不入 signature（schema 只收 test/grep，grep 表达不了「守卫必须存在」），由 guards 台账 G2 active 锚定。

扫描方法：grep `tool_choice` / `ToolChoice` / `ToolMode` 的全部**出站触达点**（含直通转发路径，不只是写出赋值点）；每处核对源 tools 计数守卫。

盲区：`tools:[]` + 无 tool_choice 的组合对上游的接受度未实测（T2 只锁 tool_choice 缺失）；Anthropic 协议方向不在本 PC 范围。

## 3 Canonical fix

守卫语义一处定义：源请求 tools 缺失或 count==0 ⇒ 出站体丢弃 tool_choice；tools 非空 ⇒ 原样保留。

落地 5 处：
1. `LlmProxyPipeline.ExecuteAsync`：`LlmJson.Write(outboundNode)` 前咽喉点守卫（直通/桥接两路必经，类级汇合点）——**canonical**。
2. `ChatResponsesBridge.MapToolChoiceToResponses` / `MapToolChoiceToChat`：方法入口源 tools 计数守卫（纵深防御，直接调用 bridge 的旁路）。
3. `LlmProxyChatClient.BuildJson` L176：`RequiredChatToolMode` 写点加 `&& options.Tools is { Count: > 0 }`。
4. `OpenAIChatStreamReader`（`OpenAIChatJson.FromUnified`）L157：`ToolChoice` 写点加 `&& request.Tools is { Count: > 0 }`。

被否：把 tool_choice 加进 `ChatToResponsesDropped` 丢弃清单（有 tools 时 tool_choice 合法，一刀切会破坏功能）；只修 bridge（挡不住直通路径）。

## 4 守卫台账

| 等级 | 载体 | 何时触发 | 覆盖盲区 |
|------|------|----------|----------|
| G2 | `LlmProxyPipeline.cs` 咽喉点守卫（Write 前） | 每次 LLM 代理出站（直通+桥接） | 不覆盖绕开 pipeline 的调用方 |
| G2 | `ChatResponsesBridge` 双向映射入口守卫 | 每次桥接 | 不覆盖直通 |
| G2 | `LlmProxyChatClient.BuildJson` / `OpenAIChatJson.FromUnified` 写点条件 | SDK/Unified 序列化路径 | 仅各自路径 |
| G1 | `LlmProxyPipelineTests` T1–T6 + `OpenAIChatJsonTests` 3 例 | `dotnet test` 过滤集 | 不覆盖真实上游兼容性 |

## 5 案例列表

| case | 日期 | 现象一句话 | 是否逃逸 |
|------|------|------------|----------|
| 2026-11-13-toolchoice-without-tools | 2026-11-13 | Responses 直通带 tool_choice 无 tools，上游 400 | 否（首次；首轮扫查内部漏判直通路径，绿灯期补第 5 处） |
