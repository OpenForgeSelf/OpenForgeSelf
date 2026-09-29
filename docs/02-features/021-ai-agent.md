# 021 · AI 智能体（AI Agent）

> 状态：已实现（代码中已落地）
> 最后更新：2026-09-21

## 概述

AI 智能体编排框架：多 Agent 协调与执行（`AgentsController`）、智能规划建议（`PlanningController`）、AI 辅助工作流（`AIWorkflowController`，见 013）、脚本生成与错误分析（`AIScriptController`）、AI 聊天（`AIChatController`）。是连接「统一 AI 网关」与「上层工具」的协调层。

## 代码落点

| 层 | 文件 |
|----|------|
| 控制器 | `Plugins/AIAgent/Controllers/`：`AgentsController.cs`（`[Route("api/agents")]`）、`PlanningController.cs`（`[Route("api/planning")]`）、`AIWorkflowController.cs`（`[Route("api/ai-agent/workflow")]`）、`AIScriptController.cs`（`[Route("api/ai-agent/script")]`）、`AIChatController.cs`（`[Route("api/ai-agent/chat")]`）、`ProjectController.cs`（`[Route("api/project")]`） |
| 服务 | `Plugins/AIAgent/Services/`：`AIAgentService`（Agent 工具循环 `RunAgentLoopAsync`）、`AgentRegistryService`（Agent 定义加载 + 空表 seed 内置 Agent）、`AgentExecutorService`、`AgentCoordinatorService`、`WorkflowPlannerService`、`AIWorkflowAssistant`、`ProactivePlanningService`、`ProjectWorkspaceService` 等 |
| 模型 | `Plugins/AIAgent/Models/AgentModels.cs`（`AgentDefinition` / `AgentWorkflowRef` / 任务与实例模型）、`SkillDefinition.cs` |
| 数据 | `Plugins/AIAgent/Data/`：XCode 实体 `AgentDefinition`（含 `ConfigJson` 持久化人格/工具/工作流关联）、`AIChatMessage`（连接名 `AIAgent`，`Model.xml` 生成模式） |
| 前端视图 | 插件自带界面 `Plugins/AIAgent/web/`（`plugin.json.frontend.route = /ai-agent`）：`AiAgentView` + `SessionPanel`（会话/Agent 列表）/ `ChatPanel`（对话 + composer）/ `ContextPanel`（AI 上下文）/ `AgentEditDialog`（Agent 编辑）；宿主残留 `src/views/AgentsManageView.vue`（管理视图） |
| 前端服务 | 插件 `web/src/http.ts`（自带 token 直连后端）、`web/src/types.ts` |

## 核心 API

**`api/agents`**：列表 / 详情 / 按类型 / `find` / `best-match` / `coordinate` / `execute` / `handle` / `execute-task`；实例管理 `instances[/{instanceId}]`；CRUD `POST /`、`PUT {agentId}`、`DELETE {agentId}`（v1.6.5 起，AgentDefinition 入库后开放）。

**`api/planning`**：`suggestions`、`suggestions/pending`、`suggestions/{id}/action|dismiss`、`patterns`、`profile`、`skills`、`preferences`、`event`。

**`api/ai-agent/workflow`**：`plan`、`plan/prompt`、`execute`、`{executionId}/status`、`tools/match`（详见 013）。

**`api/ai-agent/script`**：`generate`、`analyze-error`、`suggest-fix`、`templates[/{templateId}]`、`templates/categories`。

**`api/ai-agent/chat`**：`POST /`、`POST /stream`（SSE：content / **turn** / tool_call / tool_result / usage / done）、`history/{sessionId}`、`DELETE session/{sessionId}`、`tools`、`GET /sessions`（按 `SessionId` 聚合的历史会话列表：`sessionId`/`title`/`messageCount`/`lastTime`，标题取首条 user 消息前 20 字，直查库不走缓存）。

## 会话管理（T2，2026-09-21）

聊天界面 `SessionPanel` 新增「历史会话」分组：拉 `GET /sessions` 渲染列表，点击切换原样回传**全 id**（`sessionId`）调 `history/{sessionId}` 重载上下文；删除走 `DELETE session/{sessionId}`（删当前则新建会话）。切换一律用全 id，**规避早期「切回历史会话用短 id 查询得 0 条」的断裂**（T3 根因：两段式 id 仅在存储侧一致，前端统一传全 id 即可消除回归）。

## 自治循环（Agent Loop，v1.6.19）

**背景（2026-09-21 根因修复）**：`RunAgentLoopAsync` 原先把「本轮无 tool_call」等同于「任务完成」→ 立即 `done` + `yield break`。弱模型常先输出一段计划文本，于是**每轮都在第一次纯文本输出处熔断**，用户被迫手动连发「继续」。

**修复后的循环契约**：

| 概念 | 说明 |
|------|------|
| 自治模式判定 | `enableTools && maxTurns > 1`（`ChatRequest.MaxTurns` 未传 → `DefaultAutonomousMaxTurns = 20`；传 `1` → 传统单次问答，行为不变） |
| 完成出口 | 模型调用 **`finish`** 工具（`Id = aiagent.finish`，schema `{ summary }`）→ `stopReason = "finish"` 并结束 |
| 强制挂载 | 自治模式下 `finish` **不受 `enabledToolNames` 白名单过滤**（循环控制工具而非能力工具；被过滤会导致模型永远无法声明完成，必然跑满上限） |
| 自动续跑 | 本轮无 tool_call 且未调 `finish` → 注入 `AutoContinuePrompt` 续跑指令，进入下一轮 |
| 硬边界 | 达到 `maxTurns` → `stopReason = "max_turns"`，如实回报轮次 |
| 系统提示词 | 自治模式追加 `AutonomousLoopContract`，告知模型「只有 finish 才结束，否则自动续跑」 |

**可观测字段**（`AgentLoopEvent`）：`Turns`（当前轮次）/ `MaxTurns`（上限）/ `StopReason`（`finish` \| `max_turns` \| `completed` \| `max_iterations`）。

`stopReason` 语义：`finish` = 模型主动声明完成；`max_turns` = 自治模式跑满轮次上限；`completed` = 非自治模式无工具调用即终答；`max_iterations` = 非自治模式达工具迭代上限。

**SSE `turn` 事件**：`{ type: "turn", content: "第 N/M 轮未声明完成，自动续跑" | "第 N/M 轮结束", turns, maxTurns }`，供前端展示循环进度。

**前端展示**：`AiAgentView` 的 `loopProgress` 驱动 `ChatPanel` 状态位显示「第 N/M 轮」；循环结束显示徽标「共 N/M 轮」；`stopReason === "max_turns"` 时提示「已达自主循环上限…任务可能尚未完成——可再发消息继续」。

**计划驱动循环不受影响**：`PlanGeneratorService` / `RunOrchestratorService`（B7 升格后为编排薄壳，步骤执行由统一 `ReactLoopAgent` 状态机承担，原 `StepRunLoopService` 已删除）走 `maxTurns = 1` + `extraTools` 显式挂载 `submit_plan` / `complete_step` / `request_help`，行为与修复前完全一致。

**ReactLoopAgent（dsh B5，循环运行时现状）**：Agent 循环由 AIAgent 插件 `ReactLoopAgent`（实现 `IAgent`，经 `IAgentRegistry` 按会话解析）驱动——九态 turn/step 帧联合推进；工具调度走宿主 `IToolRegistry.ExecuteBatchAsync`（model-ordered commit：N 个 call 必有 N 个 result，取消合成 Skipped）；出口工具（complete_step/request_help）分区直达；step 超时看门狗将 turn 挂起（`TurnEndReason.Suspended`），steer 后恢复；followup/steer/inject 三语义经 `IInbox` 注入。详见 dsh 三部曲（`specs/040..042` 与 `01-architecture/dsh-alignment-施工总览.md`）。

**验证**：单测 `ForgeSelf.Api.Tests/Plugins/FinishToolTests.cs`（14 用例）+ `web/src/http.test.ts`（5 用例，SSE 分片解析）；e2e `e2e/plugins/ai-agent/agent-loop-autonomous.spec.ts`（4 用例，覆盖 `completed` / `finish` / `max_turns` 三条出口）。


**`api/project`**：工作目录选择 / 文件列表 / 读写（MCP 工具 `aiagent.list_files/read_file/write_file`，见 028）；目录浏览 `GET browse-directories?path=`（磁盘级逐级浏览，供前端目录选择弹窗与「浏览」按钮使用）。

## Agent 可配置项（AgentDefinition，v1.6.5–v1.6.7）

Agent 定义为 XCode 实体（空表时 seed 7 个内置 Agent），`AgentEditDialog` 支持编辑：

- 基本信息：名称 / 头像 / 描述 / 类型 / 排序 / 最大迭代 / 启用开关
- 系统提示词（SystemPrompt）
- 五维能力画像：创造力 / 分析力 / 同理心 / 自信度 / 正式度
- 擅长领域 / 能力 / 工具 / 局限性（TagInput 多值）
- 关联工作流（见下节）

## Agent 关联工作流（v1.6.7）

Agent 可关联多个 WorkflowEngine 工作流，执行时注入提示词，由 LLM 用 `execute_workflow` 工具按需调用。

**数据模型**：`AgentDefinition.Workflows`（`List<AgentWorkflowRef>`：`WorkflowId` + 冗余 `Name`/`Description`），随 `ConfigJson` 持久化（`AgentDefinition.Biz.cs` ToModel/FromModel 透传），零迁移。

**关联 UI**：`AgentEditDialog.vue`「关联工作流」区块 —— 多选下拉（`fetchWorkflows` 拉 `api/workflows?page=1&pageSize=200`）+ 已关联 chips + 空态降级（无工作流时提示）。

**执行期注入**：`AIAgentService.AppendAssociatedWorkflows` 在每轮 agent 循环前把「## 本 Agent 关联的工作流」追加进 system prompt，提示 LLM：任务匹配某工作流时调 `execute_workflow(workflowId, inputVariables)`，不匹配不强行调用。

**执行工具**：`ExecuteWorkflowToolFunction`（Id `aiagent.execute_workflow`，[AIAgentPlugin.cs](../../ForgeSelf.Api/Plugins/AIAgent/AIAgentPlugin.cs)）——经 `IWorkflowService` 启动工作流执行。

```mermaid
flowchart LR
    A[用户选中 Agent] --> B[注入关联工作流<br/>到 system prompt]
    B --> C[LLM 匹配任务]
    C -->|命中| D[execute_workflow<br/>工具调用]
    C -->|不匹配| E[自由应答]
    D --> F[WorkflowEngine<br/>异步启动]
    F --> G[返回「已启动」]
```

**已知限制（记录于 2026-09-09，后续由 spec 029 解决）**：

1. `execute_workflow` 为**异步启动即返回**：不等工作流终态、不校验步骤、不回注结果 → LLM 可在流程未完成时宣称完成，**无走完保证**。
2. 无步骤级完成度校验（多工作流取舍、步骤推进均靠 LLM 自决）。

**回归保护**：`ForgeSelf.Web/e2e/plugins/ai-agent/ai-agent.spec.ts` 含「关联工作流区块渲染」用例（多选/空态二选一 + 无 5xx 护栏）。

## 使用要点

- Agent 协调依赖 `AgentCoordinatorService`，可编排多个子 Agent 完成任务。
- `PlanningController` 基于历史行为给出主动性建议（见 `preferences`/`patterns`）。
- 聊天会话复用统一 `ChatSession` 聚合（见 010）。
- 工具白名单（`enabledToolNames`）与技能注入（`skillIds`）随聊天请求传递（v1.5.5 工作台重构）。
- 执行模式（`executionMode`）：`free`（自由循环，默认）/ `plan`（计划驱动），随 `AgentDefinition.ConfigJson` 持久化（v1.6.7，见下节）。

## 计划驱动执行引擎（v1.6.7–v1.6.9 · spec 029）

Agent 可配置执行模式为「计划驱动 PlanDriven」：任务先规划（Plan DSL）再逐步执行，代码逐步驱动 LLM（`complete_step` 推进 / `request_help` 卡住），全部步骤留痕可复盘，卡住可人工介入（跳过/补位/继续），支持中断后续跑。

### 数据模型（XCode 双表，连接名 `AIAgent`）

| 表 | 字段要点 |
|----|----------|
| `AgentRun` | 执行实例：`AgentId`/`SessionId`/`TaskInput`/`PlanJson`（Plan DSL）/`StepCount`/`CurrentStepIndex`/`Status`（Pending→Planning→Running→Completed/Stuck/Failed/Cancelled）/`StuckReason`/`TotalTokens` |
| `AgentStepRun` | 步骤明细：`RunId`+`StepIndex`（唯一）/`StepId`/`Name`/`Objective`/`InputJson`/`OutputJson`/`ToolCallsJson`/`StuckReason`/`HumanNote`/`HumanOverride`/`RetryCount`/`TokensUsed`/`DurationMs` |

**Plan DSL**（存 `AgentRun.PlanJson`）：`{ goal, steps[] }`，每步 `{ id, name, objective, expectedOutput, allowedTools, mandatory }`；`mandatory` V1 先埋不启用（D5）。

### 执行流程（RunOrchestrator）

```mermaid
flowchart LR
    A[用户选 PlanDriven Agent<br/>发送任务] --> B[建 Run Planning]
    B --> C[规划阶段<br/>LLM submit_plan]
    C -->|Plan DSL| D[PlanJson 落库<br/>plan_created 事件]
    D --> E[逐步骤循环<br/>RunAgentLoopAsync]
    E -->|complete_step| F[step_completed<br/>下一步]
    E -->|request_help / 迭代超限| G[run_stuck 卡住<br/>保留现场]
    G --> H{人工介入}
    H -->|跳过 skip| I[继续下一步]
    H -->|补位 override| I
    H -->|继续 resume| E
    E -->|全部完成| J[终局合成<br/>done 事件]
```

### 模式配置与入口

- **执行模式开关**：`AgentEditDialog.vue`「执行模式」radio（自由循环 FreeLoop / 计划驱动 PlanDriven）→ 写 `AgentDefinition.ConfigJson.ExecutionMode`（`free` 默认，缺省向后兼容）。
- **前端分流**：`AiAgentView.sendMessage` 依据激活 Agent 的 `executionMode`——`plan` 走 `POST /api/ai-agent/runs`（SSE 流），`free` 走既有聊天流。
- **步骤进度卡** `StepProgressCard.vue`：与消息流并列常驻，消费 `plan_created/step_started/step_completed/run_stuck` SSE 事件，折叠展示目标/产出/卡住原因。
- **执行记录面板** `RunRecordPanel.vue`：中栏右上「执行记录」按钮打开——Run 列表（状态徽标）+ 详情（步骤时间线、入参出参、卡住原因）+ 操作（继续/重开/跳过/补位/取消）。

### 核心 API（`api/ai-agent/runs`，`AgentRunsController`）

| 端点 | 说明 |
|------|------|
| `POST /` | 创建并驱动执行（SSE 流：`plan_created`/`step_started`/`step_completed`/`run_stuck`/`content`/`tool_call`/`tool_result`/`usage`/`error`/`done`） |
| `GET /` | Run 列表（分页 + `sessionId`/`status` 过滤，`RetrieveTotalCount=true`） |
| `GET /{id}` / `GET /{id}/detail` | Run 摘要 / 详情（含步骤列表） |
| `POST /{id}/resume` | 恢复执行（仅 Stuck/Failed，从 `CurrentStepIndex` 续跑，SSE 流） |
| `POST /{id}/restart` | 同 Plan 新建 Run 从头执行 |
| `POST /{id}/cancel` | 取消（终态拒绝） |
| `PATCH /{id}/steps/{index}` | 人工介入（`skip` 跳过 / `override` 补位，`HumanNote` 批注） |

**特殊工具**（仅 PlanDriven 步骤循环挂载，不进 FreeLoop）：`submit_plan`（规划器提交 Plan）、`complete_step`（步骤完成声明）、`request_help`（卡住求助）。

> **status 字段契约**（v1.6.9 实测对齐）：`AgentRunDto.status` / `AgentStepRunDto.status` 经 System.Text.Json **默认序列化为 int**（枚举序号，非字符串名）。前端 `web/src/types.ts` 的 `runStatusName`/`stepStatusName` 统一做 int→小写语义映射；`status` 过滤查询参数同样传 int。详见 `specs/029-agent-plan-driven-execution/contracts/agent-runs-api.md` §8。

### 回归保护

`ForgeSelf.Web/e2e/plugins/ai-agent/ai-agent.spec.ts` 含「执行模式开关持久化」「计划驱动执行全链路」（runs SSE + 步骤卡 + 执行记录面板）用例（真实后端，零 mock）。

## 万能工具网关与命令执行工具（v1.6.10 · spec 031）

Agent 侧新增两个工具（实现在 `Plugins/AIAgent/Services/ToolFunctions/`，经 `AIAgentPlugin.RegisterToolFunctionExtensions` 注册进宿主 ToolRegistry）：

| 工具 | 形态 | 说明 |
|------|------|------|
| `universal_tool` | 分发透传壳 | 入参 `{tool, parameters}` → 经宿主 `IToolRegistry.ExecuteAsync(new ToolExecution{...})` 六闸门执行面分发到真实工具，结果原样透传；防自引用（拒绝转发自身）、unknown 带已注册数量提示。FreeLoop 白名单外的宿主全量工具可经它触达，事件链（pre-execute 三态/守卫/execute/post-execute）/使用统计全部照走，不扩权 |
| `run_terminal_command` | 独立命令工具 | 三道安全门：可执行名白名单（仅放行 `dotnet/pnpm/node/git/ssh/pwsh`）；管道/链式拒绝（`\|` `;` `&&` 换行混淆）；CWD 限登记项目根内。破坏性命令红线（删除/格式化/联网类）拒绝路径零子进程。stdout/stderr/exitCode 回传 50KB 截断，默认 30s 超时 |

- 设计依据：`specs/031-universal-tool-gateway/`（design §2：复用 ToolRegistry 分发核，宿主零修改）+ 决策台账 ADR-002（`docs/07-decisions/002-universal-tool-gateway.md`）。
- 模型侧可见性：两工具随插件注册自动进入 FreeLoop（历史教训：77 工具全挂上游 400，故只挂 13+2 个定义，其余经 `universal_tool` 转发）。
- 单测：`ForgeSelf.Api.Tests/Plugins/` 下 `UniversalToolTests`（12 用例：双形态解析/透传/自引用/unknown 计数/软依赖兜底）+ `TerminalCommandGuardTests`（21 用例：白名单/链式注入/越界/红线）。

## 后续演进

- **步骤完成度强校验**（V2，`mandatory` 出口校验）：触发条件 = V1 实测出现「谎报完成」案例（not-to-do 台账 006.1）。
- **`execute_workflow` 同步等终态**：异步启动即返回、无走完保证，已记 not-to-do 006.2，可独立小修先行。
- **Run 记录供给其他插件**（sems 看板等）：触发条件 = 第二消费方出现（not-to-do 006.5）。
