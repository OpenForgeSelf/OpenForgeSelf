---
id: 2026-10-25-builtin-agent-silent-model-fallback
title: 待办一键交给内置 Agent 静默换模型 + 空响应被当成功，误报「Agent 不会执行」
pc: PC-013
severity: P1
evidence: Verified
---
# Case：2026-10-25 待办 builtin 下发静默降级与空响应当成功

## 1 现象

待办插件「一键交给本工具 AI Agent」（`POST /api/todos/{id}/dispatch-to-agent {"engine":"builtin"}`）下发成功（返回 `taskKey=run:<id>`、待办进 Running），但 Run 很快以 Stuck 收束，`stuckReason` 为「步骤回合未调用 `complete_step` 声明完成即收束」，步骤没有任何产出。表象像是「Agent 不会按计划执行」，实际是模型层根本没返回内容。

承接工件：`docs/ai/pilot/2026-10-24-todotracker-interaction/07-final-report.md` 遗留项 **R3**。

## 2 复现红灯

历史红灯（2026-10-24，隔离实例）：builtin 下发后 Run 判 Stuck，步骤 `outputJson` 为空，`stuckReason` 见上。原始证据保留在 `docs/ai/pilot/2026-10-24-todotracker-interaction/05-evidence.md`。

本次新增可重复红灯（已绿）：

- `ForgeSelf.Api.Tests/Plugins/AIAgent/ReactLoopAgentTests.cs::EmptyUpstreamResponse_FailsInsteadOfCompleting` —— 脚本化上游返回「空正文 + 无工具调用 + 0 补全 token + finish=stop」时，旧实现落 `StepEndReason.Completed`（绿灯即回归）；修复后断言 `StepEndReason.Failed` + 单个 `TurnFailed` 且 `Error` 含「空响应」。
- `ForgeSelf.Api.Tests/Plugins/AIAgent/ChatModelResolverTests.cs` —— 目录为空时解析返回 `null`（调用方据此明确 Fail），不再回退到硬编码模型。

## 3 根因（5 Whys + 第 6 问）

1. 为什么 Run 立刻 Stuck？→ 规划/步骤回合拿到的模型回空流，回合没有任何工具调用可执行。
2. 为什么拿到空流？→ 请求里的模型 id 是 `default:ornith-1.0-9b`，该模型不在宿主模型表中；`AIAgentService.ResolveModelId/ResolveProvider` 只剥前缀、不校验存在性/启用态，未命中时静默回退默认 provider。
3. 为什么模型 id 是硬编码？→ `BuiltInAgentExecutionService` 里写着 `public const string DefaultChatModelId = "default:ornith-1.0-9b"`，且待办侧 `DelegateToBuiltInAsync` 不传模型（本就不该让调用方拼模型串）。
4. 为什么空流没被发现？→ `ReactLoopAgent` 的「无工具调用」分支把「空正文 + 0 token」当作本步最终答复成功收束，于是问题从「模型不可用」伪装成「Agent 不会执行」。
5. 为什么恢复路径也没兜住？→ `RunOrchestratorService.ResumeAsync` 传 `ChatModelId=null`，退化到宿主 `AI:ModelName`（本机实测只回 `delta:{role}` + `stop` 的模型），恢复必然再次空跑。
- **第 6 问：为什么流程没让我一开始就写对？**→ 内置执行入口的「模型从哪来」此前没有任何文档/契约约束，既没有单测覆盖「目录为空」这一分支，也没有任何断言禁止硬编码模型名；空响应分支同理，B5 门禁只覆盖了有内容/有工具调用的路径。

## 4 同类扫查命中表

| 位置（文件:行） | 同根因？ | 处置 |
|---|---|---|
| `Plugins/AIAgent/Services/BuiltInAgentExecutionService.cs` 硬编码 `DefaultChatModelId` | 是 | 删除常量，改走统一解析链；解析不到直接 Fail |
| `Plugins/AIAgent/Services/RunOrchestratorService.cs` `ResumeAsync` 传 `null` | 是 | 改走同一解析链；解析不到发 `error` 事件收束，不进入步骤执行 |
| `Plugins/AIAgent/Services/RunOrchestratorService.cs` `RunAsync` 不传模型（无 resolver 时） | 是 | 注入 resolver 后同样先解析，空则 `error` 事件 |
| `Plugins/AIAgent/Services/ReactLoopAgent.cs` 「无工具调用 = 最终答复」分支 | 是 | 前置空响应守卫（无正文 + 无工具调用 + 0 completion tokens + 非 length ⇒ Failed） |
| `Plugins/AIAgent/Services/AIAgentService.cs` `ResolveProvider/ResolveModelId` | 否（主聊天既有语义，宿主默认模型可能不在表） | 本次不改，避免影响主聊天；由解析链在入口侧兜住 |
| `Plugins/AIAgent/docs/workbench-prototype.html:447` 模型名 | 否（静态原型示例） | 不动 |
| `Plugins/TodoTracker/Services/TodoDispatchService.cs` 不传 `ChatModelId` | 否（本就是设计：调用方不该拼模型） | 扩契约被否，靠解析链解决 |

## 5 修复与被否方案

- 新增 `Plugins/AIAgent/Services/ChatModelResolver.cs`：四级有序解析（调用方显式 → 宿主默认模型且在启用目录内 → 目录首个启用 → `null`），命中/未命中逐级落日志，禁止静默降级；`RunOrchestratorService` / `BuiltInAgentExecutionService` 共用。
- `AIAgentPlugin` 注册 `IChatModelResolver`；`ForgeSelf.Api/Plugins/PluginManager.cs` 的宿主 seed 清单补 `ITurnTelemetryQuery`（此前插件侧 `ctx.Get` 拿不到，模型目录不可见）。
- `ReactLoopAgent` 空响应守卫。
- 被否方案：① 给 `DelegateToAgentRequest` 新增 `ChatModelId` 字段（把选模型的复杂度推给调用方/前端，且仍无法兜住 resume）；② 改 `AIAgentService.ResolveModelId` 的既有语义去做校验（会破坏主聊天「宿主默认模型可能不在表」的现状）。

## 6 重构提案与结论

R0：不重构。三个缺陷都在同一入口链路上，用「统一解析链 + 空响应守卫」两处集中收口即可；把模型解析下沉到宿主层属于架构级改动，当前收益不足以支撑。

## 7 守卫落地

- G1 `ForgeSelf.Api.Tests/Plugins/AIAgent/ChatModelResolverTests.cs`（4 条：显式原样 / 宿主默认命中 / 目录首个 / 目录空返回 null）。
- G2 `ForgeSelf.Api.Tests/Plugins/AIAgent/ReactLoopAgentTests.cs::EmptyUpstreamResponse_FailsInsteadOfCompleting`（空响应必须 Failed）。

## 8 逃逸分析

首见建档。但属**逃逸暴露**：B5 状态机门禁（041）建立时只覆盖了「正常答复 / 工具调用 / max-tokens / 取消 / 请求失败」，未覆盖「上游 200 + 空流」，因此该缺陷从 041 到 2026-10-24 一直存在且被判成「Agent 能力问题」。本次把空响应补进状态机用例集合，并对「默认模型来源」建立解析链单测。

## 9 基建改进

- 插件可见的宿主契约必须在 `PluginManager.HostProvidedServiceContracts` 清单里有名字，否则 `ctx.Get` 恒空——本 case 暴露「契约已实现但未 seed」的典型缺口（与 PC-001/PC-005 相邻但不同根因）。
- 内置执行入口禁止出现硬编码模型串（PC-013 lens 第 1 问）。

## 10 复用赋能

以后改 AIAgent 执行入口/恢复路径时先查 PC-013：模型从哪来？解析不到会怎样？上游空响应会被判成什么？关联待办委派或 Run 恢复的任务，验收必须包含「真实下发到 Completed + 失败路径明确报错」两条。

## 11 证据

| 结论 | 等级 | 出处 |
|---|---|---|
| 2026-10-24 builtin 下发 Run 判 Stuck、步骤无产出 | Verified | `docs/ai/pilot/2026-10-24-todotracker-interaction/05-evidence.md` |
| `DefaultChatModelId` 硬编码 `default:ornith-1.0-9b`（已删除） | Verified | 修复前 `Plugins/AIAgent/Services/BuiltInAgentExecutionService.cs:30`；`git diff` |
| 解析链在隔离实例命中「目录首个启用模型 `default:qwen3_8_vision`」 | Verified | `.temp/dev-stack-backend.log` 01:51:26 `[ChatModelResolver] 使用模型目录首个启用模型：default:qwen3_8_vision` |
| 目录为空时拒绝下发 | Verified | 同上 01:54:23 `启用模型目录为空` + `[BuiltInAgentExecution] 未解析到可用对话模型，拒绝下发`；HTTP 400 返回同一文案 |
| 真实下发 Run 1 两步 Completed，Agent 经 todo 工具回报并流转 Review | Verified | `GET /api/ai-agent/runs/1`（status=3）与 `/api/todos/10/records`（seq1-4） |
| 定向测试 37 通过 / 1 失败（既有基线红 `ExitToolTurn_RequiresToolChoice`） | Verified | 本轮 `dotnet test --filter` 输出 |
