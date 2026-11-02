# Repository Understanding — B5 旁路收敛

> Task：`2026-11-02-b5-legacy-cleanup`｜日期：2026-11-02｜执行：verify-dev｜闸门1 = lead seq7 开工令（视为已批）

## 1. 事实表（file:line 实证）

### 1.1 ReactLoopAgent（删除目标 ①）

| # | 事实 | 依据 |
|---|------|------|
| F1 | ReactLoopAgent（943 行）与共享契约同文件：AgentTurnRuntime(:15-70)、PreStepContext/PreStepDecision(:77-87)、AgentRequestContext/AgentRequestDecision(:91-103)、TurnStoppingContext(:107)——**删除前必须先迁移共享件** | `Plugins/AIAgent/Services/ReactLoopAgent.cs` |
| F2 | 生产构造点唯一：`AIAgentService.CreateAgent` 工厂三元 `UseMafRuntime ? MafTaskLoopAgent : ReactLoopAgent`（:420-422）；`UseMafRuntime` 生产恒 true（:333-334）→ false 分支 = 死代码 | `Plugins/AIAgent/Services/AIAgentService.cs` |
| F3 | 注册/包装点：`AgentRuntimeRegistry.ResolveLoopAgent` 模式匹配 ReactLoopAgent/ScopedAgent.Inner 做 ApplyOptions（:81-87）；`AIAgentPlugin.cs:95` 工厂 new ScopedAgent(CreateAgent(...))——registry 改指 MafTaskLoopAgent（其 ApplyOptions :142 已有） | `Plugins/AIAgent/Services/AgentRuntimeRegistry.cs`、`Plugins/AIAgent/AIAgentPlugin.cs` |
| F4 | 共享契约消费方：MafTaskLoopAgent 用 PreStep*/TurnStoppingContext（:296-302/:473-475）；测试 InboxR2QaAdversarialTests(:86-171)、ReactLoopInboxGateTests(:61-72)、ReactLoopAgentTests(:287) 挂监听——**契约记录必须保留（迁文件不迁语义）** | 全仓 grep |
| F5 | 注释级引用（删除后需同步改词）：PersistentInbox.cs:16/88、IInbox.cs:82/116、InMemoryInbox.cs:14/17、AppBuilder.cs:211、RunOrchestratorService.cs:89/139/143/704、TaskLoopToolFunctions.cs:10/12、AgentLoopAndInboxTests/OrchestratorBehaviorSnapshotTests/PersistentInboxTests 注释 | 全仓 grep（88 处） |
| F6 | 测试直接构造 ReactLoopAgent 共 10 文件：ReactLoopAgentTests、ReactLoopTaskLoopTests、ReactLoopInboxGateTests、ReactLoopExitAndSuspendTests、PlannedRunUnifiedLoopGates(:101/236)、RunVerifyGateTests(:225)、GoalPursuitTests(:93)、PlanGeneratorScratchSessionQaTests(:52)、GoalRunSseEvidenceTests(:126)、InboxR2QaAdversarialTests(:202) | 全仓 grep |
| F7 | ReactLoopTaskLoopTests 文件头自述「同一判据集，保证两引擎行为一致」= MafTaskLoopAgentTests 已是同判据 MAF 版（PILOT-051 门禁 15 用例：任务循环 5 + cancel + turn-stopping + 映射 5）→ 该文件随引擎删除，语义由 MAF 侧承载 | 两文件头注释对照 |
| F8 | MAF 侧未覆盖的引擎语义门禁需迁移：inbox R2 三通道（ReactLoopInboxGateTests）、exit/suspend（ReactLoopExitAndSuspendTests）、A3 唤醒零事件（OrchestratorBehaviorSnapshotTests 驱动类）、编排器真身门禁（PlannedRunUnifiedLoopGates） | 测试文件对照 |

### 1.2 UseMafRuntime / TaskLoopMode（删除目标 ②）

| # | 事实 | 依据 |
|---|------|------|
| F9 | `UseMafRuntime` 仅 Agents.cs:84 定义 + AIAgentService:334 赋值 true + :420 三元消费 + 测试注释；**无任何 config/JSON 键**（全仓 grep `useMafRuntime` 0 命中） | 全仓 grep |
| F10 | `TaskLoopMode` 生产赋值点唯一：AIAgentService:333 `= true`；消费点 MafTaskLoopAgent(:225 区) / ReactLoopAgent(:244-566)；测试直接赋 true 多处 | 全仓 grep |
| F11 | ForgeSetting.config 兼容读：宿主 ForgeSetting 为强类型属性绑定，未知键天然忽略；两开关不在 ForgeSetting 属性表 → 删属性后旧配置文件残留键不会崩（须加一条兼容读测试佐证） | ForgeSetting 结构 + F9/F10 |
| F12 | 架构真源：`maf-centric-architecture.md` §3.2「UseMafRuntime/TaskLoopMode 收敛为默认常开；开关最终删除｜不改契约」；§2 L2 图「废弃旁路：ReactLoopAgent / ad-hoc 无日志循环（勿扩）」 | 架构文档 :60-71 |

### 1.3 AgentCoordinator 启发式旁路（裁决目标 ③）

| # | 事实 | 依据 |
|---|------|------|
| F13 | 链路：AgentsController 4 端点（plan :66 / execute :73 / execute-task :102 / handle :80）→ AgentCoordinatorService（关键词启发式 IsSimpleRequest :44 + AI 规划 + 兜底计划）→ AgentExecutorService.ExecuteTaskAsync(:32) → **`_aiAgentService.ChatAsync(messages, useTools)`（:278）= ad-hoc 无日志工具循环** | `Plugins/AIAgent/Controllers/AgentsController.cs`、`Services/AgentCoordinatorService.cs`、`Services/AgentExecutorService.cs` |
| F14 | UI 消费 = **零**：store 方法 createPlan/executePlan/handleRequest 定义于 `ForgeSelf.Web/src/stores/agent.ts`(:82-118) 但**无任何视图调用**（AgentsManageView 仅 loadAgents/getAgentTypeLabel）；execute-task/getActiveInstances 同样无视图调用 | 全仓 grep `createPlan|executePlan|handleRequest|executeTask` |
| F15 | 插件/IM/Scheduler 消费 = 零（grep IAgentCoordinatorService 仅 AgentsController + AIAgentPlugin DI :45-46） | 全仓 grep |
| F16 | 安全缺口：ad-hoc 循环无 MAF 审批泵（ApprovalRequiredAIFunction 仅 MAF 包装）；零配置下 B4 registry 层 = default-policy Allow → **Coordinator 路径带 Tools 的任务，高风险工具不经审批直接执行** | AgentExecutorService:278 + B4 闸门语义 |
| F17 | 人设注册表（IAgentRegistryService + BuiltInAgentDefinitions 7 角色）**必须保留**：RunOrchestratorService:1206 步骤人设注入在用 + AgentsManageView 列表在用 | grep 对照 |
| F18 | IChatCompletion 旧面（ChatAsync/ChatStreamAsync/ChatWithToolsAsync）B7 已定性「合法例外」（WorkflowPlannerService:43、AIWorkflowAssistant:70/178 在用，useTools=false 一次性产出）——**不在本批删除** | AIAgentService:156-170 remarks |
| F19 | 待删模型：AgentModels.cs 的 AgentTask(:126)/AgentExecutionResult(:179)/CoordinatorPlan(:195)/AgentInstance 及 UI types/agent.ts 对应 interface——随 ③ 删除（若 lead 拍板删） | grep（仅 Coordinator 链消费） |

## 2. 技术栈与测试方式（从仓库确认）

后端 .NET 10 + NewLife.XCode + MAF（Microsoft Agent Framework）经 ForgeChatClientAdapter；测试 xUnit + FluentAssertions（ForgeSelf.Api.Tests）；ScriptedProvider 驱动两引擎（MafApprovalLoopTests 范式）；e2e = Playwright 统一配置（e2e-testing 技能 SOP，4 worker 空输出目录）。

## 3. Unknown

- U1：MafTaskLoopAgent 对 inbox R2「确认语义」（step 成功后 IInboxConfirmation）实现完整度——迁移 ReactLoopInboxGateTests 时逐条验证（若 MAF 缺确认接线 = 发现缺陷，按缺陷处理记 TODO）。
- U2（架构性）：OrchestratorBehaviorSnapshotTests 的「ReactLoopAgent 真实驱动」注释与 A3 零事件语义——迁移后由 MafTaskLoopAgent 驱动复验。
