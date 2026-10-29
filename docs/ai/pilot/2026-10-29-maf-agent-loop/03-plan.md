# Plan

> 阶段：Stage 3｜具体到真实文件。Task ID：PILOT-051

## Files To Change

**S1-A 契约与出口工具（Abstractions + 插件 Models/Tools）**
- `ForgeSelf.Abstractions/Agents.cs` — reason: AgentOptions 增 `TaskLoopMode`/`UseMafRuntime`（可选属性，默认 false，兼容）。
- `Plugins/AIAgent/Services/ToolFunctions/TaskLoopToolFunctions.cs`（新）— reason: `TaskCompleteToolFunction : IToolFunctionExtension`（声明式出口工具，schema: summary）。

**S1-B ReactLoopAgent 后备语义**
- `Plugins/AIAgent/Services/ReactLoopAgent.cs` — reason: TaskLoopMode 三处最小改动：①ToolChoice 策略（续跑步 required，首步 auto；非 TaskLoop 保持现状）②纯文本且有工具调用的分支改为「落 assistant/message→continue（续跑指令）」③RenderSystemPromptAsync 注入任务循环协议块。计划路径（ExitToolNames 且 !TaskLoopMode）行为逐字节不变。

**S1-C MAF 运行时（新目录 Plugins/AIAgent/MafLoop/）**
- `Plugins/AIAgent/MafLoop/ForgeChatClientAdapter.cs`（新）— reason: FR-2.2 IChatClient↔UnifiedChat 双向翻译。
- `Plugins/AIAgent/MafLoop/MafTaskLoopAgent.cs`（新）— reason: FR-2.3/2.4/2.5/2.7 MAF 驱动的 IAgent。
- `Plugins/AIAgent/MafLoop/RegistryToolFunction.cs`（新）— reason: 注册表工具→AIFunction 包装 + task_complete 短路。
- `Plugins/AIAgent/AIAgent.csproj` — reason: 引用 Microsoft.Agents.AI 1.6.1。
- `Plugins/AIAgent/Services/AIAgentService.cs` — reason: `CreateAgent` 按选项选运行时；聊天路径 BuildAgentOptions 开 TaskLoopMode/UseMafRuntime；`ResolveOwnToolDefinitions` 排除表加 task_complete。

**S2 目标模式**
- `Plugins/AIAgent/Models/AgentRunModels.cs` — reason: RunRequest/AgentPlan 增 successCriteria/maxGoalRounds。
- `Plugins/AIAgent/Services/GoalPursuitService.cs`（新）— reason: FR-3 评估 Agent（MAF 结构化输出）+ 补救步骤生成（MAF 结构化输出）。
- `Plugins/AIAgent/Services/RunOrchestratorService.cs` — reason: 目标外环接线（步骤完成后评估/追加/续跑/Stuck）。
- `Plugins/AIAgent/Services/PlanGeneratorService.cs` — reason: 验收标准注入提示词 + 计划落标准。
- `Plugins/AIAgent/AIAgentPlugin.cs` — reason: 注册 GoalPursuitService。
- `Plugins/AIAgent/Models/AgentLoopEvent 扩展（ForgeSelf.Abstractions/AgentLoopEvent.cs）` — reason: `goal_round_started`/`goal_evaluated` 事件类型。

**S2 前端（最小可用）**
- `Plugins/AIAgent/web/src/types.ts` — reason: RunRequest 增字段 + 事件类型。
- `Plugins/AIAgent/web/src/components/RunRecordPanel.vue` — reason: 目标模式表单 + 事件渲染。
- `Plugins/AIAgent/web/src/agentBridge.ts`（如需）— reason: 透传字段。

**测试**
- `ForgeSelf.Api.Tests/Plugins/AIAgent/MafTaskLoopAgentTests.cs`（新）— reason: AC1/2/4（复用 ScriptedProvider/InMemorySessionStore/FakeToolRegistry 基建）。
- `ForgeSelf.Api.Tests/Plugins/AIAgent/ReactLoopTaskLoopTests.cs`（新）— reason: AC3。
- `ForgeSelf.Api.Tests/Plugins/AIAgent/GoalPursuitTests.cs`（新）— reason: AC5/6。

## Implementation Steps

1. AgentOptions/工具/模型字段（S1-A + S2 模型）→ `dotnet build` 编译通过。
2. ForgeChatClientAdapter + 单测（纯翻译层先行，AC4）。
3. MafTaskLoopAgent（工具 invoker/日志包装/帧翻译/看门狗/续跑）+ 单测（AC1/2）。
4. ReactLoopAgent 后备语义 + 单测（AC3）。
5. AIAgentService 接线（CreateAgent 选择 + 聊天路径开关）。
6. GoalPursuitService + RunOrchestrator 目标外环 + 事件（AC5/6）。
7. 前端表单/事件渲染（AC8）+ 插件前端 build。
8. 全量验证（§5.6 中档：宿主源码被触）+ e2e 冒烟 + 05/06 工件。

## Test Plan

1. 翻译层单测：M.E.AI→Unified 消息/工具/工具模式映射；Unified 流→ChatResponseUpdate（文本、FCC 增量、usage、finish）。
2. MAF 引擎帧序/日志序单测（脚本化 provider）：工具→纯文本→续跑→task_complete；纯问答直通；空流失败；取消。
3. ReactLoop 后备同语义 + 既有 ReactLoop 测试全绿回归。
4. 目标环单测：达标/未达标续轮/预算耗尽/评估失败保守回退。
5. 既有测试对表：`dotnet test` 全量（中档），比对基线无新增红。

## Verification

### Build

```bash
cd ForgeSelf.Api && dotnet build
```

### Unit Test

```bash
cd ForgeSelf.Api.Tests && dotnet test --filter "FullyQualifiedName~MafTaskLoopAgent|FullyQualifiedName~ReactLoopTaskLoop|FullyQualifiedName~GoalPursuit|FullyQualifiedName~ForgeChatClientAdapter"
cd ForgeSelf.Api.Tests && dotnet test   # 中档全量（宿主源码被触）
```

### Integration Test

```bash
# N/A（后端集成由既有 RunOrchestrator 测试覆盖，归入上面过滤集/全量）
```

### E2E

```bash
# 冒烟（快档 1 条主链路）：目标模式创建 Run → SSE 事件可见（e2e/plugins/ai-agent 定向）
cd ForgeSelf.Web && npx playwright test e2e/plugins/ai-agent --config playwright.aiagent.config.ts --grep 目标 2>/dev/null || npx playwright test e2e/plugins/ai-agent/ai-agent-agent-execute.spec.ts
```

### Other Checks

```bash
cd Plugins/AIAgent/web && pnpm run build   # 插件前端门禁
# 宿主前端未触碰，不跑 pnpm run check（记录：本批不涉 ForgeSelf.Web）
```

## Plan 偏差记录

| 时间 | 偏差点 | 原 Plan | 修正后 |
| --- | --- | --- | --- |
| 02:18 | XCode 测试夹具跨类换库竞态（既有基建缺陷，被本批 GoalPursuitTests 暴露：换库后旧 Database 的线程会话残留 + SQLite 表重建「备份-重建-回填」与并发写竞态，吞行/吞表） | 未预见（00-04 未涉及） | XCodeTestFixture 改进程级单次绑定 + 后续类 ClearAllData 等价换新库 + Database 实例级 Migration=Off；属解锁本批门禁的最小修复，中档全量对表 |
| 02:18 | GoalPursuitTests 步骤行直查断言在共享库下有首插可见性竞态 | 原计划断言 AgentStepRun 行三步齐 | 降为 Run 行（FindById）+ 事件面（step_completed ×3）+ PlanJson + 请求序断言；步骤级持久化语义由 AgentRunPersistenceTests 专类覆盖 |
| 02:18 | 评估器经 ForgeChatClientAdapter 直调（无工具、纯文本判据），非 ChatClientAgent 封装 | 03-plan 原 ADR 候选为 ChatClientAgent | 与 02-spec FR-3 语义一致，记录为已选实现路径 |
| 02:42 | 中档全量暴露第二层同类缺陷：五个测试类 ctor 自建目录 + `DAL.AddConnStr("AIAgent", …)` 换库（connStr 变更 → DAL.Reset 换 Database、Migration 回 On、旧线程会话残留）→ 全量类序下后续类写走别处/吞行/吞表 | 04-task 未预见（既有测试基建缺陷） | 同类修复（problem-class-closure 例外条款）：五类改 `IClassFixture<XCodeTestFixture>` + `fixture.DbDir`，删除自建连接串；AgentRunsControllerTests 两个计数断言改按唯一 sessionId 限定 |
| 02:42 | UI 落点：RunRecordPanel 原无「新建执行」表单 | 02-spec 设想的「新建执行」表单 | 按规格补建于面板左栏顶部：任务输入 + 目标模式开关（验收标准多行 + 最大轮数 1–10 默认 3，0 条=退化普通执行并提示）+ 运行事件行（goal_round_started/goal_evaluated/step/done/stuck）；聊天侧 agentBridge 对 goal_evaluated 以紧凑行进正文（resume 可见） |
