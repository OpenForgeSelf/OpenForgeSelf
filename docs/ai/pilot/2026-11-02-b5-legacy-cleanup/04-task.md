---
task_id: 2026-11-02-b5-legacy-cleanup
feature: campaign/2026-11-02-maf-runtime-hardening-B5
risk: { level: L3, triggers: ["Plugins/AIAgent/**", "ForgeSelf.Abstractions/Agents.cs", "ForgeSelf.Api.Tests/**", "e2e/**"], raised_by_agent: false }
gate1: { mode: user, at: "2026-11-02T23:42:00+08:00", decision: "战役 00-campaign-workflow §B5 经 lead seq7 开工令开工（闸门1 视为已批）；③ AgentCoordinator 子项需 lead 拍板后才动" }
gate2: { mode: reviewer, at: null }
gate3: { mode: agent, at: null }
expected_files:
  - Plugins/AIAgent/Services/ReactLoopAgent.cs
  - Plugins/AIAgent/Services/AgentTurnRuntime.cs
  - Plugins/AIAgent/Services/AgentRuntimeRegistry.cs
  - Plugins/AIAgent/Services/AIAgentService.cs
  - Plugins/AIAgent/AIAgentPlugin.cs
  - ForgeSelf.Abstractions/Agents.cs
  - Plugins/AIAgent/MafLoop/MafTaskLoopAgent.cs
  - Plugins/AIAgent/Controllers/AgentsController.cs
  - Plugins/AIAgent/Services/AgentCoordinatorService.cs
  - Plugins/AIAgent/Services/AgentExecutorService.cs
  - Plugins/AIAgent/Models/AgentModels.cs
  - ForgeSelf.Api.Tests/Plugins/AIAgent/ReactLoopAgentTests.cs
  - ForgeSelf.Api.Tests/Plugins/AIAgent/ReactLoopInboxGateTests.cs
  - ForgeSelf.Api.Tests/Plugins/AIAgent/ReactLoopExitAndSuspendTests.cs
  - ForgeSelf.Api.Tests/Plugins/AIAgent/ReactLoopTaskLoopTests.cs
  - ForgeSelf.Api.Tests/Integration/PlannedRunUnifiedLoopGates.cs
  - ForgeSelf.Web/src/services/agentApi.ts
  - ForgeSelf.Web/src/stores/agent.ts
  - docs/07-decisions/not-taken-decisions.md
rollback: "git checkout -- <touched files> + 删除新文件；无数据/对外契约变更（4 端点下线零消费者，恢复=revert）"
writeback: { feature_doc: "docs/02-features/021-ai-agent.md 引擎表 + docs/01-architecture/maf-centric-architecture.md §3.2 目标态落地注记，随 07 回写", reason: "删除面大，真源需同步收敛后的现状" }
---

# Task — B5 旁路收敛（Agent 工作单元）

> Task ID：`2026-11-02-b5-legacy-cleanup`｜执行者：verify-dev（战役批 teammate）｜战役工作流：`docs/ai/campaigns/2026-11-02-maf-runtime-hardening/00-campaign-workflow.md` §B5

## Allowed（本任务写面）

- `Plugins/AIAgent/Services/`：ReactLoopAgent.cs（拆解删除）、AgentTurnRuntime.cs（新，共享契约迁移）、AgentRuntimeRegistry.cs（模式匹配）、AIAgentService.cs（工厂恒 MAF + BuildAgentOptions 删 2 行）
- `Plugins/AIAgent/AIAgentPlugin.cs`（DI：③ 删除面 + ScopedAgent 工厂注释）
- `Plugins/AIAgent/MafLoop/MafTaskLoopAgent.cs`（恒任务循环 + 注释标「MAF Runtime Agent」）
- `ForgeSelf.Abstractions/Agents.cs`（删 UseMafRuntime/TaskLoopMode）
- ③ 拍板后：`Plugins/AIAgent/Controllers/AgentsController.cs`、`Services/AgentCoordinatorService.cs`、`Services/AgentExecutorService.cs`、`Models/AgentModels.cs`（待删模型）、`ForgeSelf.Web/src/services/agentApi.ts`、`stores/agent.ts`、`types/agent.ts`
- 测试：ReactLoop* 4 文件（迁 3 删 1）+ 编排器系 6 文件换装 + ForgeSetting 兼容读测试（新）
- 注释级引用同步：PersistentInbox.cs、IInbox.cs、InMemoryInbox.cs、AppBuilder.cs(:211)、RunOrchestratorService.cs、TaskLoopToolFunctions.cs、AgentLoopAndInboxTests.cs、OrchestratorBehaviorSnapshotTests.cs
- 文档：本 pilot 目录 00-07 + agenthub-adjudication.md、not-taken-decisions.md、TODO.md（本条 + 队列清理）、`.forgeself/memory/2026-11-02.md`

## Forbidden

- ToolRegistry.cs / IApprovalService / IInbox 契约 / AgentTurnRuntime 属性面（只迁不改）
- IChatCompletion 旧面（B7 定性合法例外）；IAgentRegistryService 人设注册表（RunOrchestrator/agents 页在用）
- AgentCoordinator 代码（③ lead 拍板前零触碰——git diff 佐证）
- IM 审批推送（后置）；Scheduler；`/ai-agent` 路由与插件 Id
- `:51888` 停/启/杀/配置；dev-stack :7301/:7399 不停（撞锁报 lead 开窗）
- 生成件手改（xcode `.cs`）

## 验证命令（中档 + 深档）

```powershell
$env:NO_PROXY='localhost,127.0.0.1,::1'; $env:no_proxy='localhost,127.0.0.1,::1'; $env:TEMP="$PWD\.temp\tmp"; $env:TMP="$PWD\.temp\tmp"
# 中档全量（OutDir 旁路换号，防自撞；红须 --no-build 标准 bin 隔离复跑定性）
dotnet test ForgeSelf.Api.Tests\ForgeSelf.Api.Tests.csproj -p:OutDir="$PWD\.temp\bin-r4\\"
# 深档全量 e2e（e2e-testing 技能 SOP：4 worker + 空输出目录；先读技能再跑）
```

判据：全量对表 qa-baseline（零新增红；OutDir 伪红隔离复跑定性）+ e2e 报告；看日志正文，不拿 exit code 当证据。

## 完成定义

AC1..AC9 全绿（02-spec §9）+ 05/06/07 + agenthub-adjudication.md 裁决留痕 + 深档 e2e 报告 + 回报 lead（不自行 commit）。

## 偏差记录（实现期实证，均测试侧机械适配，生产行为零变化）

1. **引擎语义衍生（重要，写入 05 证据）**：`TaskLoopMode` 开关删除的语义等价映射 = **TaskLoopMode ≡ (ExitToolNames 为空)**——`MafTaskLoopAgent.cs:196` `var taskLoop = _options.ExitToolNames is not { Count: > 0 };`。开放任务（ExitToolNames 空）恒任务循环（原 TaskLoopMode=true 逐字节等价，task_complete 自动挂载）；计划路径（RunOrchestratorService/PlanGeneratorService 设 ExitToolNames）无 task_complete 挂载（原 false 等价）。已注明 MafTaskLoopAgent 代码注释 + 05。
2. **MAF 内层函数循环**：一步 = 一次 MAF Run，内层 function-invocation 自动推进——「工具调用→模型文本」在**同一 step 内**吃掉，纯文本响应才断步（ReactLoop 一请求一步）。TwoSteps 类测试脚本形态按 MafTaskLoopAgentTests AC1b 模式（工具对 + 中间文本 + 出口）。
3. **MAF 显式工具声明通道**：ToolDefinitions 为 null 时脚本化工具调用打「未声明」错误结果（不执行、不记 tool/call 三件套）——迁移测试的 mock 装配必须显式声明所用注册表工具（共享 Fixture 声明 get_current_time；RunVerifyGate/GoalRunSse 声明 create_file）。
4. **write_file→create_file（编排器系脚本）**：write_file 属高风险名单（HighRiskToolPolicy），MAF 会包 ApprovalRequiredAIFunction 挂起回合——ReactLoop 时代无此包装。编排器系测试脚本改用 create_file（非高风险），断言工具名同步；与 B4 期 MafTaskLoopAgentTests/RunReceiptTests 同因同解。
5. **编排器 mock 装配补 runFlowTools**：`RunOrchestratorService` 的 `runFlowTools` 是可选参数，旧测试省略后 complete_step/request_help 无声明通道（MAF 空 响应门禁触发步骤失败）。测试装配补 `runFlowTools: new RunFlowToolSet("aiagent")`（生产 DI 同款）。
6. **ScriptedProvider 推进语义修复（测试基建缺陷）**：旧「仅在存在后继时推进」在「回合间追加脚本」模式下回放脚本 0（QA 对抗用例两次同名错误实锤）。改「无条件推进 + 入口回夹」，耗尽重复最后一段语义不变（RepeatLast 等价）。
7. **QA 文件机械适配（实现方不得改写本文件的例外，逐条留痕）**：
   - InboxR2QaAdversarialTests 取消用例：MAF 无 agent/request 拦截点 → CancelAsync 取景 + HangThenTextProvider 确定性挂起（有限快速流有「取消前已 Completed」竞态）+ 显式 `keepInbox:true`（MAF 默认 false=显式取消丢弃，QA remarks 自记分叉；用例钉的 R2 不变量「取消不确认消费」语义原样保留）。
   - PlanGeneratorScratchSessionQaTests 规划轮事件序：MAF 含工具调用的 Completed 回合按 B2②③ 追加 run/receipt（turn/end 前）；MEAI 重序列化 args 非 ASCII 转 \uXXXX → tool/call 参数断言改键名/步骤 id（`"plan"`/`"s1"`）。
   - MafExitAndSuspendTests 出口用例：预期序补 run/receipt + ArgsJson 断言改 `output` 键名（同转义）。
8. **MafEngineGateTests TwoSteps 顺序断言**：ReactLoop 断言「assistant/message 先于 tool/call」在 MAF 不成立（ForgeModelCallLogger 落盘时点不同），改钉配对不变量（tool/call < tool/result）+ 计数断言（AC4 判据核心不回退）。
9. **死于引擎的门禁（ReactLoop 特有判据，随引擎消亡，不迁移）**：ToolChoice required 强制（ReactLoop 出口续跑机制，MAF 由内层循环/续跑步 ToolMode 替代）、MaxTokens_Sticky、RequestError retry（MAF fail-fast 无同步骤重试，B4 已定性）。QA 对抗文件的「请求失败重试」用例标题按 MAF fail-fast 语义保留（输入不丢断言不受影响）。
10. **S7③ UI 删除连带 AIChatMessage**：types/agent.ts 批准清单 6 类型外连带删除 AIChatMessage（唯一引用方 AgentInstance 同批删除）；store 其余无消费死成员（getTaskPriorityLabel/getTaskStatusLabel/getAgentTypeIcon/getAgentStatusLabel/getStatusColor/filteredAgents/agentsByType/filterType/searchKeyword、agentApi 的 getAgent/getAgentsByType/findAgentsByCapability/getBestAgentForTask、types 的 FindAgentRequest/BestMatchRequest/TaskPriority/AgentStatus）**不越权删**，报 lead 备案（后端对应端点仍在，非 ③ 死代码范畴）。
