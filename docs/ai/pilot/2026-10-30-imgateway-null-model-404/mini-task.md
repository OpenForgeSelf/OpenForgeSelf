# Mini-Task：IM 网关收消息后 AI 补全 404（未绑定模型 → 陈旧默认模型名直发上游）

> 日期：2026-10-30｜级别：轻量（≤3 业务文件缺陷修复，Intent/Spec/Plan/Task 合并单文件）
> 闸门1：用户指令原文「IM网关 收到消息后报错，请用测试复现，然后修复，再测」= 复现→修复→复测的显式授权，范围以本文件为界。
> **适用问题类**：命中 **PC-013**（模型选择静默降级 + 上游错误被吞）——同类复发（逃逸），见 §PC-013 节与 05/06。

## Intent（为什么/做什么/到什么程度）

- **Problem**：:51888 实例（2.4.22）IM 网关收企微消息后 100% 报 `AI 补全失败 … 404 (Not Found)`（Session aabc4389…，21:16:45 / 21:17:18 两条）。
- **Why（实证根因链，全链有证据）**：
  1. 企微通道未绑定模型：`ImGateway.db.ImGatewayChannel.BoundChatModelId=null`、`ImGatewaySession.ModelId=null`（直查 DB，Verified）。
  2. → `ImGatewayRouter` 以 `ChatModelId=null` 调 `IChatCompletion` → `AIAgentService.RunAgentLoopAsync`。
  3. → `ResolveModelId(null)` 回退 `AiConfig.ModelName`；该值来自实例 `appsettings.json` 的 `AI:ModelName=qwythos-9b-v2`（模板残留，日志证实请求发出：`[gpustack] 发送流式 Chat 请求（SDK），模型: qwythos-9b-v2`）。
  4. → provider 路由按 null 走默认 provider=gpustack（`ForgeSelf.db.AIProvider`：gpustack IsDefault=1，Endpoint `http://gpustack.luxvisions-inno.com/v1/chat/completions`）。
  5. → 上游不存在 `qwythos-9b-v2`（该模型也不在启用模型表 AIModel，28 条中无此名）→ **404**。
  6. 本应兜底的 `ChatModelResolver`（PC-013 canonical fix，2026-10-25 建）只接了 `BuiltInAgentExecutionService` / `RunOrchestratorService`，**漏了 `RunAgentLoopAsync`（IChatCompletion/IM 路径）与 `RunAdHocCoreAsync`（ad-hoc 路径）** —— PC-013 逃逸。
- **Expected Outcome**：调用方不给模型时，会话/ad-hoc 两条入口一律走 `IChatModelResolver` 有序链（目录内匹配→首个启用→无则**明确失败**）；不再把陈旧 `AI:ModelName` 直发上游。
- **Constraints**：不改 `ResolveModelId` 既有语义（PC-013 已否决项）；不改 IM 网关会话映射语义；不动运行实例（修复随下次发布生效）。

## Spec（要点）

- FR-1 `RunAgentLoopAsync(sessionId, chatModelId=null,…)`：解析链补全，产出合法 `provider:model` 后再 `CreateAgent`；解析不到 → yield `error` 事件（文案含「无可用模型」），不发起上游请求。
- FR-2 `RunAdHocCoreAsync(…, chatModelId=null,…)`：同 FR-1（error 事件，`ChatAsync`/`ChatWithToolsAsync` 消费方按既有 error 事件语义收到失败）。
- FR-3 `AIChatController` 两聊天端点：请求未带模型时先经 `IChatModelResolver` 解析再 `BuildAgentOptions`；解析不到返回明确错误响应（resolver 缺席时保持旧行为，仅测试装配场景）。
- 非目标：企微通道绑定模型 UI、会话映射陈旧快照语义、`ResolveModelId` 本体。
- **无界面交互（2026-10-30 规则声明，2026-10-31 补）**：本批纯后端修复（模型解析链接入），无 UI 变化；调用方经既有 error 事件/错误响应感知失败（解析不到 =「无可用模型」明确报错 + 零上游请求），无新增界面交互点。
- **Failure Modes（PC-013 lens 逐条）**：
  - 入口硬编码/陈旧回退 → 本批三类入口全部改走解析链（S1 复现测试 + 既有 G1 ChatModelResolverTests）。
  - 解析不到静默换 provider → 解析不到即明确 error/错误响应（测试断言：零上游请求）。
  - 恢复路径 null 退化 → RunOrchestratorService 既有（未触碰，复跑守卫确认）。
  - 上游空响应当成功 → 既有 G2 `ReactLoopAgentTests.EmptyUpstreamResponse_FailsInsteadOfCompleting`（复跑）。
  - 宿主契约未登记 → `ITurnTelemetryQuery` 已在 `PluginManager.HostProvidedServiceContracts`（既有，Verified by code）。

## Plan

1. `Plugins/AIAgent/Services/AIAgentService.cs`：+`_modelResolver` 字段与可选 ctor 参数；`ModelResolver` 惰性属性（DI 注入优先，兜底 `new ChatModelResolver(_ctx)`）；两个入口接入解析链。
2. `Plugins/AIAgent/Controllers/AIChatController.cs`：ctor 可选 `IChatModelResolver?`；两端点解析后再建 options。
3. `ForgeSelf.Api.Tests/Plugins/AIAgent/ChatCompletionWritePathTests.cs`：CompletionFixture 支持 mock 配置/目录；新增 2 个红灯测试（见 Test Plan）。
4. PC-013 档案 + case 归档 + 索引回写。

## Task

- **Allowed**：上述 3 个业务文件 + 1 测试文件 + `docs/11-troubleshooting/`（PC-013/case/README 索引）+ `docs/ai/pilot/2026-10-30-imgateway-null-model-404/` + TODO/日记。
- **Forbidden**：IM 网关插件文件、`ResolveModelId` 语义、`ChatModelResolver` 本体、启停任何宿主进程、:51888 实例写操作、发布。
- **验收标准（AC）**：
  - [x] 红：`CompleteAsync` 不带模型 + 陈旧配置名 + 目录无该名 → 现实现把 `qwythos-9b-v2` 发给上游（测试断言失败）。（21:33 red-run2.log：`Assert.Equal` Expected `qwen3_8_vision` 红）
  - [x] 绿：同测试修复后断言上游收到目录启用模型（`fake:qwen3_8_vision` → 上游 `qwen3_8_vision`）。（21:47 过滤集 4/4 绿）
  - [x] 绿：目录为空 → `Success=false` + 明确错误文案 + **零**上游请求。（同上）
  - [x] 回归：ChatModelResolverTests（G1）+ ReactLoopAgentTests（G2）+ ChatCompletionWritePathTests 全绿；本域过滤集 dotnet test 绿。（AIAgent 域 120 例：105 绿 + 15 红均为 68df669 既有基线，0 条归本批；Orchestrator/GoalPursuit/ChatControllerInvariant/ChatModelResolver 29/29 绿）

## Test Plan（含 PC 守卫）

- 新增守卫（红灯→绿灯）：`ChatCompletionWritePathTests.CompleteAsync_NoModelBound_UsesEnabledCatalogModel_NotStaleConfiguredName`、`…_EmptyCatalog_FailsExplicitlyWithoutUpstreamCall`（IChatCompletion 入口级 = PC-013 类守卫补位，G2）。
- 复跑既有守卫：`ChatModelResolverTests`（G1）、`ReactLoopAgentTests`（G2）、`ChatCompletionWritePathTests` 既有两用例。
- 真实边界红证：改以**运行日志**为准（`2026_10_30.log` L2476-2511：请求发出模型 `qwythos-9b-v2` → SDK 404）；上游直连探测作废——DB `AIProvider.ApiKey` 为 v2 密文（写时加密边界），宿主外不解密、不伪造探测（两次尝试均 401，恰好证明密文不可直用）。
- 验证命令：`dotnet test --filter`（AIAgent 域）→ 快档；全量不对表（未触宿主源码树外的共享面，走快档 + 域过滤集，偏差见 05）。

## PC-013 记录（复发）

- 复发 #1（2026-10-30）：IM 网关路径逃逸——2026-10-25 修复只接了 Run/内置执行两入口，扫查未覆盖 `RunAgentLoopAsync`/`RunAdHocCoreAsync`/控制器。守卫缺口 = 「入口接链」无类级断言。本批补 IChatCompletion 入口级守卫（G2）并扩 lens。
