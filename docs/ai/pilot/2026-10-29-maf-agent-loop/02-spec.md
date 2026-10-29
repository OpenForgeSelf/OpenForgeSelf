# Specification

> 阶段：Stage 2｜从真实 Repository Understanding 与 Intent 推导。Task ID：PILOT-051

## Functional Requirements

### FR-1 任务循环语义（TaskLoop，两引擎共享）

- FR-1.1 新增 `AgentOptions.TaskLoopMode`（bool，默认 false）与 `AgentOptions.TaskCompleteToolName`（常量 `"task_complete"`）。开启时：挂载 `task_complete` 出口工具（走既有 `ExtraTools` 通道，不进注册表过滤），并把它加入 `ExitToolNames`（复用 B7 声明即出口语义：合成 tool/call + tool/result(Ok) + step/end(ExitTool)，产出 `ExitToolInvoked` 帧）。
- FR-1.2 **纯问答豁免**：本回合模型尚未发起任何工具调用时，纯文本回复照旧结束回合（不续跑、不强制工具）。
- FR-1.3 **续跑（continuation）**：本回合已发生 ≥1 次工具调用，且模型随后产出纯文本（无工具调用）时：正常落 `assistant/message`（用户可见中间汇报），然后**不结束回合**，直接进入下一步；下一步请求注入续跑指令（系统提示追加），并携带 `ToolChoice="required"` 强制模型行动（调工作工具或 `task_complete`）。连续续跑步数受 `MaxStepsPerTurn` 兜底（触顶以 `Completed`+原因说明收束，现状语义）。
- FR-1.4 `task_complete` 被调用 → 回合以 `ExitToolInvoked("task_complete", args)` 帧收束（`TurnEndReason.Completed`）；聊天链路把 `args.summary` 作为终局说明落 `turn/end` 事件 payload（现状 turn/end 已带 reason/payload 通道则复用，不新增事件类型）。
- FR-1.5 系统提示协议注入（TaskLoopMode 开启时）：回合系统提示追加「任务循环协议」块（持续工作直至完成、不要以纯文本结束、完成后调用 task_complete、summary 写终局结果）。
- FR-1.6 触达面：聊天会话路径（`AIChatController` 的 chat/chat-stream 与 `AIAgentService.RunAgentLoopAsync`）默认开启 TaskLoopMode；计划驱动路径（RunOrchestrator）**不开启**（其出口语义已是 complete_step/request_help）；遗留门面 L3/L5 不开启。

### FR-2 MAF 运行时（用户指定实现路径）

- FR-2.1 新增 `Plugins/AIAgent` 引用 `Microsoft.Agents.AI 1.6.1`（与宿主同版本）。
- FR-2.2 新增 `ForgeChatClientAdapter : IChatClient`（插件内）：把 M.E.AI `ChatMessage/ChatOptions` 翻译为宿主 `UnifiedChatRequest`，调用 `IAIProvider.ChatStreamAsync`，把 `UnifiedStreamChunk` 翻译回 `ChatResponseUpdate`（文本、FunctionCallContent、UsageContent、FinishReason）。工具（AIFunction）→ `UnifiedToolDefinition`；`ChatToolMode` → `ToolChoice`；`ChatOptions.Instructions` → `SystemPrompt`。
- FR-2.3 新增 `MafTaskLoopAgent : IAgent`（插件内）：与 `ReactLoopAgent` 同一公开契约（sessionId、Options、Inbox、Status、RunAsync 帧、Cancel、WhenIdle）。每步经 MAF 管线驱动：`ChatClientBuilder` → `UseFunctionInvocation`（`MaximumIterationsPerRequest` 取 `MaxStepsPerTurn` 同源上限、`FunctionInvoker` 包装）→ `ForgeChatClientAdapter`；`ChatClientAgent`（`UseProvidedChatClientAsIs=true`，会话历史真源仍是宿主日志——每次 Run 传派生消息全集 + 全新 `AgentSession`，不引 MAF 持久历史）。
- FR-2.4 工具执行与日志：`FunctionInvoker` 内逐工具走宿主六闸门管线 `IToolRegistry.ExecuteAsync`（30s 步级预算），落 `tool/call` + `tool/result`（与现状事件字段一致）；`task_complete` 在 invoker 内短路（置 `ctx.Terminate=true` + 合成 Ok 结果）并记录出口。MAF 内层逐模型请求经内层日志 `DelegatingChatClient` 落 `request/header` + `assistant/message`（含 ToolCallRef 与 usage），保持「请求是日志的纯函数」与事件顺序（assistant/message → tool/call → tool/result → 下一请求）。
- FR-2.5 FR-1 的续跑/纯问答豁免/协议注入在 MAF 引擎以同一语义实现（续跑=追加续跑指令消息 + `ChatToolMode.RequireAny` 后再次 `RunStreamingAsync`）。
- FR-2.6 运行时选择：`AgentOptions.UseMafRuntime`（bool，默认 true 当 TaskLoopMode 开启）；`AIAgentService.CreateAgent` 按选项选择 MAF 运行时或 `ReactLoopAgent`（后备开关，回滚点）。
- FR-2.7 超时/看门狗/取消/空响应检测：与 `ReactLoopAgent` 同语义移植（步级 45s 无进展→Suspended；cancel→Aborted 不提交半截消息；空流→Failed）。

### FR-3 目标模式（Goal Run）

- FR-3.1 `RunRequest` 增 `SuccessCriteria`（`List<string>?`）与 `MaxGoalRounds`（`int?`，默认 3，钳制 1–10）；`AgentPlan` 增 `SuccessCriteria`（`List<string>?`，随 PlanJson 持久化，**不改 DB**）。
- FR-3.2 计划生成（PlanGeneratorService）在提供验收标准时把标准写入计划（提示词注入标准清单）。
- FR-3.3 目标环（`RunOrchestratorService` 内新增目标外环）：步骤全部完成后，若计划带验收标准：
  1. 发 `goal_round_started` SSE 事件（round/maxRounds）；
  2. 用 **MAF 评估 Agent**（`ChatResponseFormat.ForJsonSchema` 结构化输出）对照目标+验收标准+各步骤产出判定：`{achieved, confidence, missing[], critique, summary}`；
  3. 达成 → 现有终局合成收束（合成文本附评估结论）；
  4. 未达成且轮次未用尽 → **plan-and-extend**：按 critique/missing 生成补救步骤（MAF 结构化输出步骤数组）**追加**到当前计划尾部（不重建计划，不触 AgentStepRun 冲突），继续执行；
  5. 未达成且轮次用尽 → Run 置 `Stuck`，`StuckReason` = 未达成报告（人工可介入/恢复，恢复后轮次预算重置为 1 次评估）。
- FR-3.4 新 SSE 事件类型：`goal_round_started`、`goal_evaluated`（payload：round、achieved、summary、missing、critique，camelCase）。
- FR-3.5 评估器失败（非法 JSON/上游错误）：重试 1 次；仍失败按「未达成」保守处理（critique=评估失败原因），不中断 Run。

## Input

- 聊天：现有 `ChatRequest`（含 enableTools/enabledToolNames/agentId/chatModelId/skillIds）不变。
- Run：`RunRequest` 新增可选字段 `successCriteria`/`maxGoalRounds`（不传 = 现状行为）。

## Output

- 聊天 SSE 帧序新增：中间 `assistant` 文本（现状已有）→ 续跑步继续 → `ExitToolInvoked` 收束（`task_complete` 摘要进入 turn/end）。
- Run SSE 新增 `goal_round_started`/`goal_evaluated` 事件；Run 详情 plan JSON 增 `successCriteria`。

## Business Rules

- BR-1 任务完成的唯一显式信号 = `task_complete` 调用（TaskLoop 模式下）；「模型不再调工具」不再隐含完成。
- BR-2 纯问答（零工具调用回合）永不续跑。
- BR-3 步数/轮数上限是硬兜底；触顶不是失败（Completed）但必须留下「未显式宣告完成」的可观测痕迹（turn/end payload 标注）。
- BR-4 目标评估结论只来自评估 Agent 的结构化输出，不根据文本猜测；评估失败=未达成。
- BR-5 不新增表/列：目标配置与轮次全部进既有 JSON（PlanJson / StuckReason / 事件 payload）。

## Boundary Conditions

- 本地小模型不支持 required tool choice：退化为普通续跑（指令注入仍在），步数上限兜底（provider 对 tool_choice 未知值会按各自行为处理，现有计划路径已承受同样风险）。
- `MaxStepsPerTurn` 触顶：FR-1.3 收束。
- 目标模式下 Run 被取消：走现状取消路径（Cancelled，终态拒绝）。
- 会话无可用 provider/model：现状错误路径不变。
- 评估 Agent 与执行 Agent 共用同一 chatModelId（v1 不做单独评估模型配置，记 Unknown）。

## Error Handling

- MAF 管线异常（翻译失败/管线构建失败）：该回合按 `TurnFailed` 处理（现状语义），日志记 `[MafTaskLoopAgent]` 前缀。
- 适配器翻译失败（未知 content 类型）：跳过该 content 并记 warn，不炸整个响应。
- 工具执行异常：走六闸门管线既有失败结果（Error/Timeout outcome 喂回模型）。

## Compatibility

- 未开启新选项的全部路径行为不变（e2e 存量基线不回归）。
- `AgentOptions` 新增可选属性（默认 false），序列化/构造点全部向后兼容。
- RunRequest 新字段可选；旧前端调用不传 = 现状。
- 事件类型新增只增不改（前端未知事件现状按忽略处理，需在 http.ts 确认忽略行为）。

## Non-functional Requirements

- 单回合步数与 MAF 迭代上限同源，防预算放大；日志事件顺序与现状态机一致（快照测试约束）。
- 插件 ALC 双载 MAF 程序集可接受（宿主已载一份）；MAF 类型不跨插件公开边界（公开面仍是 ForgeSelf.Abstractions）。

## Failure Modes

| 问题类/风险 | 本设计如何满足 |
| --- | --- |
| 工具调用被丢（宿主 L4 注释记载的原手写代理 bug） | MAF 管线自动执行工具，FunctionCallContent/FunctionResultContent 全量翻译（AgentStreamTranslator 参考实现已验证该翻译面） |
| 续跑死循环 | MaxStepsPerTurn 硬兜底 + 连续纯文本续跑步数计数（同池） |
| 评估器幻觉「已达成」 | 结构化输出 + confidence/missing 明示 + summary 原文进 SSE 与 StuckReason，可审计 |
| 双引擎语义漂移 | FR-1 语义在两引擎各实现一份，测试断言同一帧序/日志序契约（共享测试助手） |
| required tool_choice 上游不支持 | 计划路径既有风险，同策略承受；步数上限兜底 |

## Interaction Design（交互设计 · 必写节）

> UI 改动面 = AIAgent 插件前端 RunRecordPanel「新建执行」表单 + 运行事件流。风格沿用既有 ForgeCard/EmptyState/tokens，不新增色值（design:check 棘轮）。

### 交互规格（点什么出现什么）

| 交互点 | 触发 | 结果 | 状态模型 | 反馈 | 空态 | 边界 |
| --- | --- | --- | --- | --- | --- | --- |
| 目标模式开关 | 勾选「按目标执行（直到验收达标）」 | 表单出现「验收标准」多行输入 + 「最大轮数」数字输入（1–10，默认 3） | 开/关 | 即时显隐 | 未勾选=现状表单 | 轮数钳制 1–10，越界自动纠正 |
| 验收标准输入 | 每行一条标准 | 提交时非空行去空格后传后端 | 文本 | — | 0 条 = 目标模式退化为普通执行（提交按钮旁提示「未填验收标准，将按普通执行」） | 空行忽略 |
| 新建执行（目标模式） | 点击开始 | SSE 流出现 `goal_round_started`/`goal_evaluated` 事件行（轮次/结论/缺失项），步骤事件照常渲染 | running/evaluating/stuck | 事件行即时追加 | 无 | 评估中保持「执行中」徽标 |
| 未达成收束 | 轮次用尽且未达标 | Run 进 Stuck，卡片显示未达成报告（critique/missing） | stuck | 现状 stuck 面板 | — | 可恢复（恢复=预算重置 1 次评估） |

### 交互验收标准（并入 AC）

- AC-UI1 勾选目标模式出现两个新输入；取消勾选恢复原状。
- AC-UI2 提交后事件流可见 `goal_round_started`（轮次 1/N）与 `goal_evaluated`（达标结论）；未达标时出现缺失项列表。
- AC-UI3 轮数输入非 1–10 被纠正为边界值。

### 走查符合性（DoD 绑定）

- 走查按 ui-ux-design 清单逐项核对（显隐不破版、事件行对齐、长文本换行、空态文案）；e2e 冒烟 1 条主链路（创建目标 Run → 事件可见）。

## Acceptance Criteria

- [ ] AC1 聊天路径 TaskLoop：脚本化「工具→纯文本」序列在 MAF 引擎下自动续跑并最终 `task_complete` 收束；帧序与日志序符合契约（单测）。
- [ ] AC2 纯问答（零工具调用）回合行为与现状一致（不续跑、不强制工具）（单测）。
- [ ] AC3 `ReactLoopAgent` 后备引擎同语义（TaskLoopMode 开启时续跑 + required + 协议注入；计划路径语义不变）（单测）。
- [ ] AC4 适配器翻译：文本/工具调用增量/usage/finishReason/ToolChoice/Instructions 往返正确（单测）。
- [ ] AC5 目标模式：达标 → 终局合成附结论；未达标 → 补步骤续跑；预算耗尽 → Stuck 带报告（单测，评估器用脚本 provider 驱动 MAF 评估 Agent）。
- [ ] AC6 `RunRequest.successCriteria/maxGoalRounds` 空值 = 现状行为（单测回归既有 RunOrchestrator 测试全绿）。
- [ ] AC7 `dotnet build` + 过滤集单测绿；宿主源码被触 → 中档全量 `dotnet test` 与基线对表无新增红。
- [ ] AC8 插件前端 build 绿；AC-UI1~3 走查/冒烟 e2e 通过。
- [ ] AC9 05-evidence/06-review 落盘，全部 AC 标注 Verified 证据。

## Unknown

| 不确定点 | 影响 | 处理方式 |
| --- | --- | --- |
| `FunctionInvocationContext.Terminate` 在 1.6.1 管线中的精确时机（工具结果是否已回填） | 若与预期不符，`task_complete` 后可能多一次模型请求 | 保守处理：出口记录以 `ExitToolInvoked` 为准（外层观察到即收束）；预算受迭代上限保护；实现后以单测实测帧序为准 |
| 个别 provider 对 `ToolChoice="required"` 的差异 |跑步上游报错 计划路径同风险失败按现有请求失败路径处理 |
| MAF 评估 Agent 对本地小模型的结构化输出遵循度 | 评估可能解析失败 | FR-3.5 保守回退（重试 1 次→未达成） |
| MAF `ChatClientAgent` 会话历史与派生消息双轨 | 可能重复历史 | 设计上每次 Run 全新 session + 全量派生消息输入，规避 MAF 持久历史；实现后单测验证请求内容 |
