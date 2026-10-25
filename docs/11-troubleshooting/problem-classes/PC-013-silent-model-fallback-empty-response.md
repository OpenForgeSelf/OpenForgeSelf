---
id: PC-013
title: 模型选择静默降级 + 上游空响应被当作成功（失败被误报为「Agent 不会执行」）
tags: [ai-agent, model-resolution, silent-fallback, empty-response, run-status]
invariant: 任何「调用方不给模型」的执行入口必须走统一的有序解析链，解析不到时明确失败（Fail / error 事件），禁止回退到硬编码模型或静默换 provider；上游返回空响应（无正文 + 无工具调用 + 0 补全 token + 非 max-tokens）必须判步骤失败，不得当作成功收束。
status: guarded
first_seen: 2026-10-25
recurrences: []
cases: [2026-10-25-builtin-agent-silent-model-fallback]
signature:
  - {id: S1, kind: test, path: ForgeSelf.Api.Tests/Plugins/AIAgent/ChatModelResolverTests.cs}
  - {id: S2, kind: test, path: ForgeSelf.Api.Tests/Plugins/AIAgent/ReactLoopAgentTests.cs}
allowlist: []
guards:
  - {level: G1, path: ForgeSelf.Api.Tests/Plugins/AIAgent/ChatModelResolverTests.cs, status: active}
  - {level: G2, path: ForgeSelf.Api.Tests/Plugins/AIAgent/ReactLoopAgentTests.cs, status: active}
lens:
  - 执行入口里有没有硬编码模型串 / `DefaultChatModelId` 这类常量（应走 `IChatModelResolver`）？
  - 解析不到模型时是明确失败，还是回退到宿主默认 provider / 目录首个 / 什么都不说？
  - `ResumeAsync` 及一切「恢复/重放」路径是否也显式解析模型（不能传 null 退化）？
  - 上游 200 但空正文、无工具调用、0 completion token 的分支，落到 Completed 还是 Failed？
  - 依赖的宿主契约（如 `ITurnTelemetryQuery`）是否已进 `PluginManager.HostProvidedServiceContracts` 清单（否则 ctx.Get 恒空）？
---
# PC-013 模型选择静默降级 + 空响应当成功

> 事件经过见 `../cases/2026-10-25-builtin-agent-silent-model-fallback.md`。本条与 PC-001/PC-005 相邻但**不变量不同**：PC-001 管「契约没进共享表」，PC-005 管「解析时机/容器错误」，本条管「解析不到时该失败却静默降级 + 空响应被当成功」。

## 1 不变量与诱因

两条不变量：

1. **模型来源单一且有序**：调用方显式指定（原样尊重）→ 宿主默认模型且能在启用目录中匹配 → 目录首个启用 → 无则明确失败。任何一环都不得凭常量猜。
2. **空响应不是成功**：上游 200 且「无正文 + 无工具调用 + 0 completion token + 非 length」意味着请求实际没有可用产出，必须判 `StepEndReason.Failed` 并带模型名，否则上层（029 计划驱动）只能看到「没调 `complete_step`」，把模型问题误报成 Agent 能力问题。

诱因：内置执行入口曾以 `public const string DefaultChatModelId` 指定模型；`AIAgentService.ResolveProvider/ResolveModelId` 不校验存在性/启用态（主聊天既有语义）；状态机门禁只覆盖有内容/有工具调用的路径。

## 2 特征签名与扫描方法

入口签名：`Plugins/AIAgent/Services/ChatModelResolver.cs` 必须是唯一模型解析点；`Plugins/AIAgent/Services/**` 下不得再出现形如 `default:xxx` / 具体模型名的常量（`grep -n "DefaultChatModelId\|ornith\|qwythos" Plugins/AIAgent` 仅允许注释与静态原型）。

G1 覆盖：显式指定原样返回、宿主默认命中（大小写不敏感 + UpstreamModelId 匹配）、宿主默认不在目录改首个启用、目录空返回 null。
G2 覆盖：空响应 ⇒ 无 `assistant/message`、`StepEndReason.Failed`、单个 `TurnFailed` 且 Error 含「空响应」。

## 3 Canonical fix

- `IChatModelResolver.ResolveAsync(requested, ct)`：四级有序链；`ITurnTelemetryQuery.GetModelCatalogAsync()` 只返回 `AIModel.Enabled == true`；日志逐级说明「命中来源」或「为何未命中」。
- 消费方：`BuiltInAgentExecutionService.StartAsync`、`RunOrchestratorService.RunAsync/ResumeAsync`。解析不到 ⇒ `BuiltInAgentOutcome.Fail` / `error` 事件，**不进入规划或步骤执行**。
- `ReactLoopAgent`：在「无工具调用」分支前先判空响应，命中走 `StepEndReason.Failed` + `TurnFailed`。
- 契约可见性：新增依赖的宿主契约必须登记 `PluginManager.HostProvidedServiceContracts`（本次补 `ITurnTelemetryQuery`）。

被否方案：给 `DelegateToAgentRequest` 增加 `ChatModelId`（把选模型推给调用方/前端，且兜不住 resume）；改 `AIAgentService.ResolveModelId` 既有语义（会破坏主聊天现状）。

## 4 守卫台账

| 等级 | 载体 | 何时触发 | 覆盖盲区 |
|------|------|----------|----------|
| G1 | `ChatModelResolverTests.cs` | dotnet test 全量 / CI | 只覆盖解析链四条；目录读取异常（DB 故障）未覆盖 |
| G2 | `ReactLoopAgentTests.cs::EmptyUpstreamResponse_FailsInsteadOfCompleting` | dotnet test 全量 / CI | 「有极短正文但语义为空」不在守卫内（仍按成功收束） |

## 5 案例列表

| case | 日期 | 现象一句话 | 是否逃逸 |
|------|------|------------|----------|
| 2026-10-25-builtin-agent-silent-model-fallback | 2026-10-25 | 待办一键交给内置 Agent 后 Run 立即 Stuck，实为硬编码模型不存在 + 空响应当成功 | 是（041 B5 门禁只覆盖有内容路径，未覆盖空流；2026-10-24 被误判为「Agent 不会执行」） |
