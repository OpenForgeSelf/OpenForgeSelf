---
id: 2026-10-30-im-gateway-unbound-model-404
title: IM 网关未绑定模型 → 陈旧 AiConfig.ModelName 直发上游 404（PC-013 入口级逃逸）
pc: PC-013
severity: P1
evidence: Verified
---
# Case：2026-10-30 IM 网关收消息后 AI 补全 404

## 1 现象

:51888 运行实例（2.4.22.2610301421）企微通道收到消息后 AI 回复全失败（21:16:45 / 21:17:18 两条 100% 复现）：

```
[MafTaskLoopAgent] MAF Run 失败 SessionId=aabc4389797c4163a4970927ca6a7cfa: Service request failed. Status: 404 (Not Found)
[AIAgentChatCompletion] 补全失败 SessionId=aabc4389…: Service request failed. Status: 404 (Not Found)
[IM网关] AI 补全失败 Channel=wecom Session=aabc4389… Err=Service request failed. Status: 404 (Not Found)
```

上游请求日志（同日志文件 21:16:36）：`[gpustack] 发送流式 Chat 请求（SDK），模型: qwythos-9b-v2`——该模型不存在于上游与启用模型表。

## 2 复现红灯

- **真实边界（运行日志，Verified）**：`C:\Users\12504\.forgeself\log\2026_10_30.log` L2484-2511（两条消息 → 404）。
- **可重复红灯（已绿）**：`ForgeSelf.Api.Tests/Plugins/AIAgent/ChatCompletionWritePathTests.cs`
  - `CompleteAsync_NoModelBound_UsesEnabledCatalogModel_NotStaleConfiguredName`：修复前 `Assert.Equal` 红（Expected `qwen3_8_vision`，实际把陈旧配置名发给上游）；
  - `CompleteAsync_NoModelBound_EmptyCatalog_FailsExplicitlyWithoutUpstreamCall`：修复前 `Assert.False` 红（目录为空仍静默成功调上游）。
- 红灯输出存 `.temp/tmp/red-run2.log`（21:33，2 失败 / 2 通过）→ 修复后 4/4 绿。

## 3 根因（5 Whys + 第 6 问）

1. 为什么 404？→ 上游不存在模型 `qwythos-9b-v2`（启用模型表 AIModel 28 条中无此名，gpustack 上亦无）。
2. 为什么请求带这个模型名？→ IM 网关 `ChatModelId=null`（企微通道未绑定模型 + 会话映射亦空，直查 ImGateway.db 证实）→ `AIAgentService.RunAgentLoopAsync` → `ResolveModelId(null)` 回退 `AiConfig.ModelName`；:51888 实例 `appsettings.json` 残留模板值 `AI:ModelName=qwythos-9b-v2`。
3. 为什么 provider 与模型不一致？→ `ResolveProvider(null)` 走默认 provider（gpustack，DB `AIProvider.IsDefault=1`），模型名却来自 appsettings 旧默认——两个「默认」来源不同源。
4. 为什么解析链没兜住？→ PC-013 的 canonical fix（2026-10-25）只把 `IChatModelResolver` 接进 `BuiltInAgentExecutionService` 与 `RunOrchestratorService`，**漏了 `RunAgentLoopAsync`（IChatCompletion/IM 路径）、`RunAdHocCoreAsync`（ad-hoc）与 `AIChatController` 两端点**。
5. 为什么 2026-10-25 扫查没发现？→ 当时扫查签名是「入口里有没有**硬编码**模型串」，本例入口不硬编码（传 null 交给底层回退），签名未覆盖「null → 底层陈旧回退」形态。
- **第 6 问：为什么流程没让我一开始就写对？**→ PC-013 不变量写的是「任何『调用方不给模型』的执行入口必须走统一有序解析链」，但守卫只有解析链单测（G1）与空响应守卫（G2），**没有任何「入口接链」的类级断言**——新增执行入口（IM 网关 B5/041、ad-hoc）绕链不会被任何测试拦截。

## 4 同类扫查命中表（2026-10-30）

| 位置（文件:行） | 同根因？ | 处置 |
|---|---|---|
| `Plugins/AIAgent/Services/AIAgentService.cs` `RunAgentLoopAsync`（null → 陈旧回退） | **是（本例）** | 入口接解析链；解析不到 yield error 事件收束 |
| `Plugins/AIAgent/Services/AIAgentService.cs` `RunAdHocCoreAsync`（同上） | 是 | 同上（ChatAsync/ChatWithToolsAsync 消费方按既有 error 语义收失败） |
| `Plugins/AIAgent/Controllers/AIChatController.cs` SendMessage/流式端点（null → BuildAgentOptions 直通） | 是 | 端点先经 resolver 解析；解析不到明确错误响应（503/SSE error） |
| `Plugins/AIAgent/Services/AIAgentService.cs` `CreateAgent` 内 `ResolveModelId(null)` 回退 | 异（保留项：sync 契约无法 await 解析链；生产 CreateAgent 调用方均先解析或显式传模型） | 不改（PC-013 已否决改 ResolveModelId 语义）；依赖入口接链兜住 |
| `Plugins/ImGateway/Services/ImGatewayDbStores.cs` `GetOrCreateSession` 存快照 ModelId | 异（会话连续性设计；且当前值为 null 非本例根因） | 不改；如绑定模型改名会以「显式请求不存在模型」复现，属显式优先级语义 |
| `Plugins/AIAgent/Services/RunOrchestratorService.cs` | 否（2026-10-25 已接链） | 复跑守卫确认绿 |
| `Plugins/AIAgent/Services/PlanGeneratorService.cs` | 否（chatModelId 继承 orchestrator 已解析值） | 无需改 |

## 5 修复与被否方案

- `AIAgentService`：+`ModelResolver`（DI 注入优先，兜底 `new ChatModelResolver(_ctx)`，均惰性）+ `ResolveChatModelOrFallbackAsync`；`RunAgentLoopAsync` / `RunAdHocCoreAsync` 入口 null → 解析链，null 结果 → 明确 error 事件（不发起上游请求）。
- `AIChatController`：ctor 可选注入 `IChatModelResolver?`；两端点先解析再 `BuildAgentOptions`；解析不到 → 同步端点 503 / 流式端点 SSE error，文案同 RunOrchestrator。
- 被否：改 `ResolveModelId` 既有语义（PC-013 §3 已否决，破坏主聊天现状）；在 IM 网关侧拼模型串（调用方不该拼模型）。

## 6 重构提案与结论

R1（提案，非本批执行）：`CreateAgent` 的 `AgentOptions.ModelId` 为空时禁止静默回退——把「空 ModelId」变成编译期不可表达（required + 入口解析），消除「新增入口忘接链」这一类。动公共契约（IAgentRegistry/CreateAgent 签名链），走 R2 评审后另立 pilot；本批以入口级守卫（§7）止血。

## 7 守卫落地

- **G2（新，本批）**：`ChatCompletionWritePathTests.CompleteAsync_NoModelBound_UsesEnabledCatalogModel_NotStaleConfiguredName` + `…_EmptyCatalog_FailsExplicitlyWithoutUpstreamCall`——IChatCompletion 入口级类断言（目录解析 + 空目录零上游请求），堵住「入口不接链」的回归。
- G1（既有，复跑绿）：`ChatModelResolverTests` 4 条。
- G2（既有，复跑绿）：`ReactLoopAgentTests::EmptyUpstreamResponse_FailsInsteadOfCompleting`。

## 8 逃逸分析（二次复发，强制）

- **归因**：流程缺陷——PC-013 修复批（2026-10-25）的同类扫查只覆盖「硬编码形态」，未按不变量全称（「任何不给模型的入口」）枚举入口；守卫停在「解析链自身正确」（G1），没有「每个入口都走链」的类级断言。
- **机制上浮**：① PC-013 lens 增补「新增执行入口是否经 `IChatModelResolver`（含 null 透传形态）」；② 本批补入口级 G2（IChatCompletion 路径）；③ R1 提案（§6）待批。
- **复发记录**：PC-013 recurrences +1（本 case）。

## 9 基建改进

- `docs/ai/pilot/2026-10-30-imgateway-null-model-404/`（mini-task + 05/06）。
- 本 case §4 命中表 = 下次入口改动的检查清单。

## 10 复用赋能

任何「新执行入口 / 新消费 IChatCompletion 或 IAIAgentService 的调用方」合入前：模型从哪来？null 时谁解析？解析不到怎么失败？三条答案必须分别指向 `IChatModelResolver`、入口自身、明确 error——否则闸门2 拒收（PC-013 lens）。

## 11 证据

| 结论 | 等级 | 出处 |
|---|---|---|
| 运行实例两条消息 404 + 请求模型 `qwythos-9b-v2` | Verified | `C:\Users\12504\.forgeself\log\2026_10_30.log` L2476-2511 |
| 企微通道与会话映射 ModelId 均为 null | Verified | 直查 `C:\Users\12504\.forgeself\plugins\im-gateway\ImGateway.db`（ImGatewayChannel.Id=1 / ImGatewaySession.Id=1，ReadOnly 连接） |
| `AI:ModelName=qwythos-9b-v2` 为实例 appsettings 残留 | Verified | `D:\src\tools\OpenForgeSelf\versions\2.4.22.2610301421\appsettings.json` AI 节 |
| gpustack 为默认 provider（IsDefault=1，Endpoint 含 /v1/chat/completions） | Verified | 直查宿主 `ForgeSelf.db` AIProvider 表（ReadOnly） |
| `qwythos-9b-v2` 不在启用模型表 | Verified | 直查宿主 `ForgeSelf.db` AIModel 表（28 条，无此名；DB ApiKey 为 v2 密文，故未做上游直连探测，上游无此模型由运行日志 404 + 目录缺席双证） |
| 红灯 2 失败 / 2 通过 → 修复后 4/4 绿 | Verified | `.temp/tmp/red-run2.log`（红）、`dotnet test --filter ChatCompletionWritePathTests`（绿） |
| AIAgent 域过滤集 105/120 绿，15 红均为 68df669 既有基线（非本批） | Verified | `.temp/tmp/aiagent-regression.log`；对表 `docs/ai/pilot/2026-10-30-release-version-from-csproj/05-evidence.md:68` |
| Orchestrator/GoalPursuit/ChatControllerInvariant/ChatModelResolver 29/29 绿 | Verified | 本轮 `dotnet test --filter` 输出 |
