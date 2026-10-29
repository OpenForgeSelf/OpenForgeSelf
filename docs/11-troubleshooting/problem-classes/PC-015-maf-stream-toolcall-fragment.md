---
id: PC-015
title: 流式工具调用分片被逐片当完整调用（空名调用毒化会话 → 同会话每条消息 1~2ms 必败）
tags: [ai-agent, maf-loop, streaming, tool-call, session-history]
invariant: 流式协议的工具调用分片（OpenAI：首片带 id/name、续片只带参数增量）必须按调用聚合为完整调用后才允许产出 FunctionCallContent 或落库 ToolCallRef；任何消费点不得把单个分片当完整调用；name 为空白的工具调用不得进入模型请求或会话日志。
status: guarded
first_seen: 2026-10-29
recurrences: []
cases: [2026-10-29-aiagent-maf-empty-function-name]
signature:
  - {id: S1, kind: grep, glob: "Plugins/AIAgent/**/*.cs", regex: "new FunctionCallContent.call.Id", expect: no-match}
  - {id: S2, kind: test, path: ForgeSelf.Api.Tests/Plugins/AIAgent/MafTaskLoopAgentTests.cs}
allowlist: []
guards:
  - {level: G1, path: ForgeSelf.Api.Tests/Plugins/AIAgent/MafTaskLoopAgentTests.cs, status: active}
  - {level: G3, path: scripts/check-problem-classes.ps1, status: active}
lens:
  - 消费 UnifiedStreamChunk.DeltaToolCall 的代码是逐片产出调用，还是按 Id 聚合后再产出（分片 ≠ 调用，OpenAI 续片无 id/name）？
  - 聚合/映射工具调用时是否过滤 name 为空白的调用（空名不得出门）？
  - 会话历史派生（DeriveMessages → 模型消息）路径是否同样过滤空名（旧数据自愈，防永久毒化）？
  - 新增流式上游适配器（OpenAI Responses / vLLM / Ollama…）的增量→UnifiedToolCall 映射是否保持「续片只带参数增量」契约、消费侧是否有对应聚合？
---
# PC-015 流式工具调用分片被逐片当完整调用

> 事件经过见 `../cases/2026-10-29-aiagent-maf-empty-function-name.md`。与 PC-013 相邻但不变量不同：PC-013 管「模型解析降级与空响应当成功」，本条管「流式工具调用分片的聚合与空名过滤」。

## 1 不变量与诱因

**不变量（front-matter 展开）**：`UnifiedStreamChunk.DeltaToolCall` 是**分片**不是**调用**——OpenAI 流式协议下只有首片携带 `id`/`name`，续片只携带参数增量（`arguments` 前缀）。消费侧必须：① 按 `id` 聚合（首片新建、续片向末位追加 name/args）；② 流结束时整条产出完整 `FunctionCallContent`；③ 聚合后仍无名的畸形调用丢弃；④ 会话历史派生出站时再过滤一次空名（旧数据自愈）。

**诱因（认知缺口）**：MAF 适配层（2.4.18 新增）把 `UnifiedStreamChunk` 当「完整事件」假设——单个 chunk 的 `DeltaToolCall` 被直接映射为一条完整调用。而 provider 侧（`OpenAISdkAdapter.ChatStreamAsync`）是逐 SDK 增量透传的。两侧契约假设错位，且错误是**延迟爆炸**型：落库当下无症状，下一次请求序列化空名调用时才炸，且炸完会话永久毒化（每次发言 1~2ms 必败），表象与根因相距一个请求周期。

## 2 特征签名与扫描方法

- **S1（grep 反模式绊线）**：`Plugins/AIAgent/**/*.cs` 中不得出现 `new FunctionCallContent(call.Id` 形态的直接分片产出（签名用 `new FunctionCallContent.call.Id`：首 `.` 吞括号、中 `.` 吞点号，精确命中缺陷形态；大小写不敏感扫描下不误伤 `tc.CallId` 等完整调用点；`call` 变量名源自缺陷现场，改名换形属盲区，靠 lens 兜底）。合法产出点均为完整调用：`MafTaskLoopAgent.cs:768`（`tc.CallId`）、`ForgeChatClientAdapter.cs:358`（聚合后多行构造，`call.Id` 与产出点相隔多字符不命中）、测试文件。
- **S2（G1 回归测试）**：`MafTaskLoopAgentTests.cs::ForgeChatClientAdapterTests` 三测——分片聚合为单调用、平行调用按序合并、空名历史调用出站过滤。
- **已知盲区**：动态反射/表达式构造的调用点扫不到；非 `call.Id` 变量名的同形缺陷靠 §1 lens 在 Design/Plan 阶段人工过闸；宿主侧（`ForgeSelf.Api/Services/AI/Providers/OpenAISdkAdapter.cs:183`）的 `CreateFunctionToolCall` 空名抛异常是**故意保留的 fail-fast 背板**（见 §3 被否方案），不属扫查目标。

## 3 Canonical fix

**聚合（治本）**：`ForgeChatClientAdapter.GetStreamingResponseAsync` 用与 `ReactLoopAgent.AccumulateToolCall`（生产验证版）同语义的累积器——首片带 `Id` 新建调用，续片（`Id` 空）向末位追加 `name`/`arguments`；流结束后每调用产出一条完整 `FunctionCallContent`（args 为分片拼接后的完整 JSON，空 → `{}`，无 id → 合成 id）。`ToChatResponseUpdates` 移除逐片产出分支。

**自愈（出站过滤）**：`ForgeChatMessageMapper.ToUnifiedMessages` 跳过空名 `FunctionCallContent`；`MafTaskLoopAgent.ToChatMessage` 跳过空名 `ToolCalls`。双层防护使已毒化会话（历史落了空壳）立即恢复可用。

参考实现：`Plugins/AIAgent/MafLoop/ForgeChatClientAdapter.cs:324-384`（聚合 + 产出）、`:53-63`（出站过滤）、`Plugins/AIAgent/MafLoop/MafTaskLoopAgent.cs:757-773`（出站过滤）。

**被否**：
- 改 `OpenAISdkAdapter.BuildChatMessages` 静默跳过空名调用——**否**。该处抛 `ArgumentException(functionName)` 是 fail-fast 报警器：空名能到达 SDK 说明上游防线已失效，应炸响而非吞掉（吞掉会掩盖防线漏洞）。空名过滤职责在上游边界（本 PC ①② 层）。
- 给 `UnifiedToolCall` 加 `Index` 字段以支持平行调用分片交叉归并——**否（降为 R2 候选）**。动公共契约（Abstractions），当前无「交叉到达」的实测上游（gpustack/vLLM 按调用顺序发分片），等真实需求再立项。

## 4 守卫台账

| 等级 | 载体 | 何时触发 | 覆盖盲区 |
|------|------|----------|----------|
| G1 | `ForgeSelf.Api.Tests/Plugins/AIAgent/MafTaskLoopAgentTests.cs`（ForgeChatClientAdapterTests 三测） | dotnet test（全量/过滤集）/ CI | 只覆盖适配层三形态；其它假想消费点不在内 |
| G3 | `scripts/check-problem-classes.ps1` 执行 front-matter S1 签名（pre-commit/CI） | git commit / CI | 变量改名换形扫不到（lens 兜底） |

## 5 案例列表

| case | 日期 | 现象一句话 | 是否逃逸 |
|------|------|------------|----------|
| 2026-10-29-aiagent-maf-empty-function-name | 2026-10-29 | 2.4.18 用户会话两轮工具后 Run 失败「empty string functionName」，此后同会话每条消息 1~2ms 必败 | 否（首见；2.4.18 发布前测试无「续片无 id/name」形态用例，属测试形状缺口非守卫失效） |
