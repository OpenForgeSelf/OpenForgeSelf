# Repository Understanding

> 阶段：Stage 0（动手写代码之前必须完成）｜规范：docs/04-standards/ai-native-engineering-workflow.md §2
> 原则：所有条目必须来自**真实仓库内容**，禁止凭常识推测。Task ID：PILOT-051（2026-10-29 maf-agent-loop）

## 项目结构

- 解决方案：`ForgeSelf.slnx`；宿主 `ForgeSelf.Api/`（net10.0-windows，WinForms 托盘 + ASP.NET Core）、前端 `ForgeSelf.Web/`（Vue3.5+Vite6+TS5.7+ElementPlus+pnpm）、测试 `ForgeSelf.Api.Tests/`（xUnit+Moq+FluentAssertions）。
- 插件在**仓库根 `Plugins/`**（非 ForgeSelf.Api/Plugins）：`AIAgent`（本任务主战场）等 20 个插件，`plugin.json` 注册，独立 csproj 引用 `ForgeSelf.Core` + `ForgeSelf.Abstractions`。
- 共享契约层 `ForgeSelf.Abstractions/`（IAgent/ISessionStore/IToolRegistry/IAIProvider/Unified* 模型）；宿主实现经 Cordis `IContext` 运行期注入插件。

## 技术栈

| 层 | 技术 | 依据（文件/配置） |
| --- | --- | --- |
| 宿主 | .NET 10 + SQLite + NewLife.XCode | `ForgeSelf.Api/ForgeSelf.Api.csproj` |
| Agent 循环库 | **Microsoft.Agents.AI 1.6.1（MAF）+ Microsoft.Extensions.AI 10.5.1**（已在用） | `ForgeSelf.Api.csproj:73`（Microsoft.Agents.AI.OpenAI 1.6.1）；NuGet 缓存 nuspec 依赖 M.E.AI 10.5.1 |
| 插件 ORM | NewLife.XCode 12.0 | `Plugins/AIAgent/AIAgent.csproj` |
| 前端 | Vue3 + pnpm | `ForgeSelf.Web/package.json`；插件前端 `Plugins/AIAgent/web/` |

## 架构特点：Agent 循环现状盘点（「每个Agent的每个循环」全景）

| # | 循环 | 入口/消费方 | 现状 | 用户痛点相关性 |
| --- | --- | --- | --- | --- |
| L1 | **主聊天 FreeLoop**（`ReactLoopAgent`，turn/step 状态机） | `AIChatController` POST /api/ai-agent/chat[/stream] → `AgentRuntimeRegistry`/`AIAgentService.CreateAgent` | 多步循环：模型带工具调用则喂回结果继续；**模型一旦产出纯文本（无工具调用）回合即结束** | ⭐ 用户抱怨主路径 |
| L2 | 计划驱动 Run（`RunOrchestratorService.ExecuteStepsCoreAsync` 每步一个 `ReactLoopAgent` turn） | `AgentRunsController` POST /api/ai-agent/runs | 出口工具 `complete_step/request_help` + `ToolChoice=required`（B7 语义），步骤做完即 Run Completed；**无「目标达成度」评估与续轮** | ⭐ 需求2 的载体 |
| L3 | 遗留 ad-hoc 循环（`AIAgentService.RunAdHocCoreAsync`，for<10） | `IChatCompletion` 门面（脚本生成/错误分析/IM 网关/AgentExecutorService） | 喂回工具结果最多 10 轮，纯文本即停 | B7 合法保留面，不在本批 |
| L4 | MAF 实验网关（`AgentChatController` /v1/agent/chat/completions） | 宿主 UnifiedAI | MAF `AsAIAgent`+`RunStreamingAsync` 自动执行**演示工具**（AgentDemoTools），与真实工具注册表/会话日志不连通 | ⭐ 证明 MAF 自动工具循环已验证可用 |
| L5 | `AgentExecutorService.ExecuteAgentWorkAsync` | 协调者/编排 | 单次 `ChatAsync`（iterations 恒 1） | 遗留编排实验面，不在本批 |

**关键不变量（改动必须保持）**：
1. 持久事实唯一写路径 = 宿主 `ISessionStore`（`user/message`、`request/header`、`assistant/message`、`tool/call`、`tool/result`、`turn/*`、`step/*`，见 `ForgeSelf.Abstractions/SessionEvents.cs`）；`TurnFrame` 是运行期 live 帧不落盘。
2. 模型输入 = `ISessionStore.DeriveMessages` 纯派生（「请求是日志的纯函数」）。
3. 出口工具（`ExitToolNames`）**声明即出口，不真实执行**：合成 `tool/call + tool/result(Ok) + step/end(ExitTool)`，经 `ExitToolInvoked` 帧交驱动方（`ReactLoopAgent` 内已实现）。
4. 单回合步数上限 `AgentOptions.MaxStepsPerTurn=10` 兜底收束；步级看门狗 `StepTimeout=45s`（无进展挂起 `Suspended`）。
5. MAF 程序集目前只在宿主默认 ALC 加载；插件加载有自己的 PluginLoadContext（`StageAllPlugins` 全量拷贝插件 bin，排除 ForgeSelf.*/NewLife.*）。

## 测试方式

- 后端单测：`ForgeSelf.Api.Tests`（`dotnet test`）；既有可复用基建：`ReactLoopAgentTests.Fixture`（`ScriptedProvider:IAIProvider` 脚本化响应 + `InMemorySessionStore` + `FakeToolRegistry` + `InMemoryInbox` + `EventBus`）、`RunOrchestratorTests`、`OrchestratorBehaviorSnapshotTests`。
- e2e：`e2e/plugins/ai-agent/ai-agent-agent-execute.spec.ts`（真实宿主+真实模型）；`e2e/helpers/e2e-env.ts` 管地址。
- 门禁分档：改 `ForgeSelf.Api/**`（宿主源码）→ §5.6「中」档全量 `dotnet test`；改 `Plugins/AIAgent/**` → 快档（过滤集 + check/test）+ 1 冒烟 e2e。

## 构建命令

```bash
cd ForgeSelf.Api && dotnet build
cd ForgeSelf.Api.Tests && dotnet test
cd Plugins/AIAgent/web && pnpm run build      # 插件前端门禁（改 UI 时）
cd ForgeSelf.Web && pnpm run check && pnpm run test   # 仅当触碰宿主前端
```

## 主要目录职责

| 目录 | 职责 |
| --- | --- |
| `Plugins/AIAgent/Services/` | ReactLoopAgent（L1/L2 循环状态机）、AIAgentService（编排+门面）、RunOrchestratorService（计划驱动）、ToolFunctions/RunFlowToolFunctions（submit_plan/complete_step/request_help） |
| `Plugins/AIAgent/Models/` | AgentRunModels（AgentPlan/RunRequest/DTO）、AgentModels |
| `Plugins/AIAgent/Controllers/` | AIChatController（聊天 SSE）、AgentRunsController（Run SSE） |
| `ForgeSelf.Abstractions/` | Agents.cs（IAgent/AgentOptions/TurnFrame）、UnifiedChatModels、IToolRegistry |
| `ForgeSelf.Api/Controllers/UnifiedAI/` | MAF 实验网关（L4）、AgentStreamTranslator（AgentResponseUpdate→SSE 翻译参考实现） |

## 代码组织方式

插件子容器：`AIAgentPlugin.Apply` 注册服务（scoped 为主）；宿主契约经 `ctx.Get<T>()` 运行期软解析（构造期注入宿主契约会 500，e2e 实测）。出口工具经 `IToolFunctionExtension`（ParametersJsonSchema + ExecuteAsync）+ `AgentOptions.ExtraTools` 挂载（不走注册表过滤）。

## 现有工程规范（对本任务有约束力的条目）

- AI-Native 闭环 00-07 工件 + 三闸门（本批闸门1 由用户明文预授权「自行调研、自行决策开发，不用问我」，记录于 01-intent）。
- §1 硬约束：不改 DB 结构（**不新增实体表**，目标配置全部走既有 JSON 列）、不新增大规模依赖（MAF 1.6.1 已在宿主引用，插件侧引用同版本不算新增大规模依赖）、不改鉴权。
- XCode 为唯一 ORM；脚本一律 `pwsh`；测试体系唯一（本批全部走 dotnet test + 既有 e2e）。
- 技能：本任务不新建插件（无需 plugin-feasibility-study）；不改插件目录结构；涉及 AIAgent 插件本体改动 → 收尾须跑插件门禁四步（§11 记录）。

## MAF 1.6.1 API 事实（NuGet 缓存 XML 核实，编译视角）

- `AIAgent` 抽象基类：`RunAsync/RunStreamingAsync(ChatMessage|IEnumerable<ChatMessage>, AgentSession, AgentRunOptions, ct)`；`AgentResponseUpdate{Contents,Text,Role,FinishReason,ResponseId}`。
- `ChatClientAgent(IChatClient, ChatClientAgentOptions, ...)`：`ChatClientAgentOptions{Instructions, ChatOptions, UseProvidedChatClientAsIs, ...}`；`ChatClientAgentRunOptions{ChatOptions}`。
- `Microsoft.Extensions.AI` 10.5.1：`IChatClient`、`ChatMessage{Contents,Role}`、`ChatOptions{Tools,ToolMode,Instructions,ModelId,...}`、`ChatToolMode.Auto/None/RequireAny/RequireSpecific(name)`、`FunctionInvokingChatClient{MaximumIterationsPerRequest, FunctionInvoker, MaximumConsecutiveErrorsPerRequest, IncludeDetailedErrors, TerminateOnUnknownCalls}`、`FunctionInvocationContext{CallContent, Function, Arguments, Terminate, Iteration}`、`FunctionCallContent{CallId,Name,Arguments}`、`FunctionResultContent{CallId,Exception,Result}`、`UsageContent`、`AIFunction`（`AIFunctionFactory.CreateDeclaration(name,description,schema)` 可造声明式工具）。
- MAF 的自动工具循环：ChatClientAgent 默认在客户端管线加 function invocation（宿主 L4 注释「Agent Framework 默认在服务端自动执行注册的工具」实测背书）；`UseProvidedChatClientAsIs=true` 时由我们自装 `UseFunctionInvocation`。

## 候选低风险任务

见 01-intent：两个需求拆为 S1（MAF 运行时 + 任务循环语义，聊天路径）与 S2（目标模式，计划驱动路径追加评估/轮环均不触 DB/鉴权/生产。

## 选择该任务的原因

用户直接指令（最高优先级来源），且痛点路径（L1 停止语义、L2 无目标闭环）与行业标准方案差距明确、可测。
