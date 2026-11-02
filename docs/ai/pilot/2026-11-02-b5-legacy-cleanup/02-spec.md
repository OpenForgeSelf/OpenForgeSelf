# Spec — B5 旁路收敛

> 关联：架构真源 `docs/01-architecture/maf-centric-architecture.md` §2/§3.2/§7；战役 `00-campaign-workflow.md` §B5。
> 不确定点标 Unknown（00 §3 U1/U2）。

## 1. 背景与目标

三处旁路（ReactLoopAgent / UseMafRuntime+TaskLoopMode 开关 / AgentCoordinator 启发式旁路）按架构 §7 授权收敛：删引擎、删开关、旁路裁决。删除面大，门禁 = 中档全量 + 深档全量 e2e。

## 2. 范围

**改**：`Plugins/AIAgent/Services/ReactLoopAgent.cs`（拆解删除）、`MafLoop/MafTaskLoopAgent.cs`（无开关化）、`AIAgentService.cs`（工厂恒 MAF）、`AgentRuntimeRegistry.cs`（模式匹配换 MAF）、`ForgeSelf.Abstractions/Agents.cs`（删 UseMafRuntime/TaskLoopMode 属性）、注释级引用 8 文件、测试 10 文件迁移/删除；③ 若 lead 拍板删除：AgentCoordinatorService/AgentExecutorService/AgentsController 4 端点/AIAgentPlugin DI/AgentModels 待删模型 + UI agentApi/store/types 死代码。
**不改**：ToolRegistry 六闸门、IApprovalService、IInbox 契约、AgentTurnRuntime 属性面、IChatCompletion 旧面、人设注册表（IAgentRegistryService + BuiltInAgentDefinitions）、路由 `/ai-agent`、插件 Id。

## 3. 契约（对外行为零变化）

- 会话事件流（turn/step/tool/receipt）与 TurnFrame 集合：MAF 引擎既有输出即唯一行为（B7 已统一，RunReceiptTests 既有断言不动）。
- `AgentOptions` 删 `UseMafRuntime`（bool 属性删除 = 编译期破坏，绿场允许；对外 HTTP/存储契约不变——AgentOptions 不落盘不出网）。
- `TaskLoopMode`：从 AgentOptions 删除 + MafTaskLoopAgent 恒任务循环（原 true 分支语义）；`AgentOptions.TaskCompleteToolName` 常量保留。
- ForgeSetting.config 兼容：两开关从未进 ForgeSetting 属性表（00 F9/F11），旧配置文件残留任意键由强类型绑定天然忽略——补一条兼容读测试锁行为。
- ③ 删除面契约：`api/ai-agent/agents/plan|execute|execute-task|handle` 4 端点下线（零 UI/插件消费者，00 F14/F15；绿场无迁移承诺）。

## 4. 引擎删除语义（①）

- 先迁共享件：AgentTurnRuntime + PreStepContext/PreStepDecision + AgentRequestContext/AgentRequestDecision + TurnStoppingContext → 新文件 `Plugins/AIAgent/Services/AgentTurnRuntime.cs`（同 namespace，纯移动）。
- 删 ReactLoopAgent 类（132-943 行）+ `AIAgentService:420-422` 三元 → `return new MafTaskLoopAgent(...)`。
- `AgentRuntimeRegistry.ResolveLoopAgent` 改匹配 MafTaskLoopAgent（ScopedAgent.Inner 同步）。
- 注释引用改词（F5 清单 8 文件）。
- 测试迁移（F6/F7/F8）：
  - 删：ReactLoopTaskLoopTests（同判据 MAF 版已在 MafTaskLoopAgentTests）。
  - 迁：ReactLoopAgentTests/ReactLoopInboxGateTests/ReactLoopExitAndSuspendTests 驱动类改 MafTaskLoopAgent（U1：inbox 确认接线逐条验证）。
  - 换装配：PlannedRunUnifiedLoopGates/RunVerifyGateTests/GoalPursuitTests/PlanGeneratorScratchSessionQaTests/GoalRunSseEvidenceTests/InboxR2QaAdversarialTests 的 mock CreateAgent 返回体换 MafTaskLoopAgent（断言走共享事件契约）。

## 5. 开关收敛语义（②）

- AgentOptions 删 UseMafRuntime + TaskLoopMode 两属性（Agents.cs:74/84）。
- MafTaskLoopAgent :225 区 `_options.TaskLoopMode` 判断移除（恒真路径）；ReactLoop 相关代码随 ① 已消失。
- AIAgentService BuildAgentOptions :333-334 两行删除。
- 兼容读测试：ForgeSetting 反序列化含未知键（模拟旧配置带 useMafRuntime/taskLoopMode 残留）不抛不崩。

## 6. AgentCoordinator 裁决（③，拍板前不动）

裁决报告单独呈报 lead（见本批 send_message），要点：链路实锤（00 F13-F16）、UI 零消费、接入 MAF = 重建多智能体编排（>1 天，与 RunOrchestrator 职责重叠）→ 按判据推荐**删除** + not-taken 记录；拍板原文归档本目录 `agenthub-adjudication.md`。

## 7. Interaction Design（无界面变更声明）

本批无新增/修改用户可见界面。③ 若拍板删除，`/agents` 页不受影响（仅列表/编辑人设，走 agents 列表端点保留）；已死代码（agentApi 3 方法等）删除对用户不可见。深档 e2e 走查覆盖聊天/计划任务主链路回归。

## 8. Unknown

- U1 inbox R2 MAF 侧确认接线完整度（迁移时验证；缺 = 缺陷记 TODO 修复后再删）。
- U2 A3 零事件语义 MAF 驱动复验结果。
- U3 lead 对 ③ 的拍板（删除 / 接入）。

## 9. 验收标准（AC1..ACn，05 映射表逐字含同 token）

- **AC1**：共享契约先行迁移——AgentTurnRuntime + PreStep*/AgentRequest*/TurnStopping 迁独立文件，编译绿且 MafTaskLoopAgent 既有过滤集全绿（零行为变化）。
- **AC2**：ReactLoopAgent 删除——类/工厂分支/注册表模式匹配/类型引用全仓 grep 零残留（文档历史记载除外），测试 ReactLoopTaskLoopTests 删除。
- **AC3**：引擎语义门禁迁移——ReactLoopAgentTests/ReactLoopInboxGateTests/ReactLoopExitAndSuspendTests 核心判据由 MafTaskLoopAgent 驱动全绿（inbox R2/exit/suspend/cancel/状态机）。
- **AC4**：编排器系测试换装——PlannedRunUnifiedLoopGates/RunVerifyGateTests/GoalPursuitTests/PlanGeneratorScratchSessionQaTests/GoalRunSseEvidenceTests/InboxR2QaAdversarialTests 换 MafTaskLoopAgent 装配全绿（A3 零事件/run verify/goal/规划 QA 断言不回退）。
- **AC5**：开关删除——UseMafRuntime/TaskLoopMode 属性与全部赋值/消费点删除，CreateAgent 恒 MafTaskLoopAgent，全仓 grep 零残留。
- **AC6**：ForgeSetting.config 兼容读——含 useMafRuntime/taskLoopMode 残留键的配置反序列化不崩不生效（测试锁行为）。
- **AC7**：AgentCoordinator 裁决留痕——推荐 + 理由 + lead 拍板原文归档 `agenthub-adjudication.md`；拍板前 AgentCoordinator 代码零改动（git diff 佐证）。
- **AC8**：③ 执行（拍板=删除时）——Coordinator/Executor/4 端点/DI/待删模型 + UI 死代码删除，全仓 grep 零残留，not-taken 记录落盘；拍板=接入时按拍板方案另立任务。
- **AC9**：门禁——中档全量 dotnet test 对表零新增红；深档全量 e2e（e2e-testing SOP，4 worker，空输出目录）通过；构建 0 错误。
