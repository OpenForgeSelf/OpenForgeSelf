# Evidence — MAF Agent 循环 + 目标驱动执行（PILOT-051）

> 生成于 2026-10-29 02:40（Asia/Shanghai）。全部证据为当日实测日志原文摘录，标注 Verified/Inferred/Unknown。
> 日志文件：`.temp/test-*.log`、`.temp/plugin-web-build.log`（会话临时目录，原文摘录于此持久化）。

## Task

- 需求1（FR-1/FR-2）：AI Agent 发需求→调工具→回文本即停 → 改为任务循环（task loop）持续执行直到 `task_complete` 显式宣告完成；MAF 1.6.1 运行时与 ReactLoop 同语义双引擎。
- 需求2（FR-3）：目标模式——计划完成后由评估 Agent 对照验收标准判定，未达成追加纠正步骤续跑（plan-and-extend），预算耗尽转 Stuck。

## Changed Files

**生产代码（Plugins/AIAgent + 宿主接线）**
- `Plugins/AIAgent/MafLoop/ForgeChatClientAdapter.cs` — MAF IChatClient → 宿主 IAIChatClient 翻译桥
- `Plugins/AIAgent/MafLoop/TaskLoopPrompts.cs` — 任务循环协议/续跑/评估/纠正共享提示词
- `Plugins/AIAgent/MafLoop/MafTaskLoopAgent.cs` — MAF 管线任务循环引擎（IAgent 契约同 ReactLoop）
- `Plugins/AIAgent/Services/ReactLoopAgent.cs` — 后备引擎获得同语义任务循环
- `Plugins/AIAgent/Services/GoalPursuitService.cs` — 目标外环（评估 + plan-and-extend + 预算）
- `Plugins/AIAgent/Services/RunOrchestratorService.cs` — 目标环接线 + goal_* SSE 事件 + PlanJson 落标准
- `Plugins/AIAgent/Models/AgentRunModels.cs` — RunRequest.SuccessCriteria/MaxGoalRounds + 事件 payload
- `Plugins/AIAgent/AIAgentPlugin.cs` / `AIAgentService.cs` — AgentOptions.UseMafRuntime 工厂切换（回滚点）

**测试（ForgeSelf.Api.Tests）**
- 新增 `Integration/GoalPursuitTests.cs`（5 例：扩展收束/预算耗尽/评估故障安全/非目标模式不变/解析容错）
- 新增 `Plugins/AIAgent/MafTaskLoopAgentTests.cs`、`ReactLoopTaskLoopTests.cs`、`ForgeChatClientAdapterTests.cs`
- 修改 `XCodeTestFixture.cs`（进程级单次绑定 + Database 实例级 Migration=Off，见「测试基建根因」）
- 同类修复：`AgentRunPersistenceTests.cs` / `AgentRunsControllerTests.cs` / `RunOrchestratorTests.cs` / `Plugins/AIAgentProjectionAdversarialTests.cs` / `Plugins/AIAgentProjectionQaIndependentTests.cs`（删除 ctor 自建连接串，改夹具注入）

**前端（Plugins/AIAgent/web）**
- `src/types.ts` — RunRequest.successCriteria/maxGoalRounds + GoalRoundStartedPayload/GoalEvaluatedPayload
- `src/http.ts` — goal_round_started / goal_evaluated 事件分发 + RunStreamHandlers 两回调
- `src/agentBridge.ts` — goal_evaluated 评估结论紧凑行进正文（新建/resume 两路接线）
- `src/components/RunRecordPanel.vue` — 「新建执行」表单：目标模式开关 → 验收标准多行 + 最大轮数（1–10 默认 3，0 条=退化普通执行并提示）+ 运行事件行
- `src/AiAgentView.vue` — 传 activeAgentId

## Build

- `dotnet build ForgeSelf.Api.Tests`（连带宿主/插件）：**已成功生成**（仅既有 CS8618/CS8614 警告，与本批无关）— Verified（`.temp/build-tests2.log`）
- `cd Plugins/AIAgent/web && pnpm run build`：`✓ 98 modules transformed. ✓ built in 1.10s` — Verified（`.temp/plugin-web-build.log`）

# 关键输出原文（通过数/失败数/错误摘要）

```
test-fix14-1..3.log（六类联跑 ×3）：已通过! - 失败: 0，通过: 43
test-fix16-1..2.log（八类联跑 ×2）：已通过! - 失败: 0，通过: 57
test-full-1.log（中档全量第一轮）：失败! - 失败: 74，通过: 3181，总计: 3255
test-full-2.log（中档全量第二轮，同类修复后）：见「Integration Test」节更新
plugin-web-build.log：✓ 98 modules transformed. ✓ built in 1.10s
```

## Unit Test

- 引擎/适配器四件套（MafTaskLoopAgentTests + ForgeChatClientAdapterTests + ReactLoopTaskLoopTests + ReactLoopAgentTests）：**25/25 绿** — Verified（会话早段多轮复跑）
- GoalPursuitTests.ParseEvaluation_ToleratesFencesAndNoise（解析容错单测）：绿 — Verified

## Integration Test

- 六类联跑（goal + 快照 + 门禁 + 编排 + 双持久化类，43 例）×3：**43/43 绿 ×3** — Verified
- 八类联跑（再并入 AIAgentProjectionAdversarial/QaIndependent，57 例）×2：**57/57 绿 ×2** — Verified
- **中档全量第一轮**（3255 例）：3181 绿 / 74 红 — Verified（`.temp/test-full-1.log`）
  - 与本批直接相关的 11 红（GoalPursuit 4 + PlannedRunGates 4 + Snapshot 3）：**根因=测试基建缺陷**（五个 AIAgent 测试类 ctor 自建目录 + `DAL.AddConnStr("AIAgent",…)` 换库 → `DAL.Reset()` 换 Database、Migration 回 On、旧线程会话残留 → 后续类写走别处/吞行/吞表 `no such table: AgentRun`）。错误原文：`XSqlException: SQL logic error no such table: AgentRun`。
  - 同类修复后八类 2×57 绿。
- **中档全量第二至六轮（修复迭代期，留档）**：87/96/84/89/80 红——三轮定位出第二层根因：全量首绑时插件实体程序集尚未加载，`EntityFactory.InitConnection` 的 `GetAllSubclasses` 只扫已加载程序集 → 静默漏建表；`ClearXCodeDataAttribute` 为**从未应用的死代码**（无程序集级注册），Per-Test 建表从未发生。中间态尝试（Migration=Off 实例级开关、临时拨 On 回填、程序集预载）均不可靠，最终采用**每类 ctor 主动 `EntityFactory.InitConnection(connName)`**（类自身实体已 JIT 加载 → 必能扫到；不换连接串 → 无 Reset → 无重建竞态）。错误原文：`no such table: AIChatMessage/WorkflowDefinition/ScriptExecution`。
- **中档全量第七轮（最终对表）**：**3203 绿 / 52 红 / 3255** — Verified（`.temp/test-full-7.log`）
  - **我的域 7 类零红**（GoalPursuit / Snapshot / Gates / RunOrchestrator / Persistence / Controller / 三投影类）。
  - 52 红全部为基线（与本批 git diff 零交集）：Plugins.TodoTracker 32、TodosControllerTests 7、WorkflowPlanningIntegration 6、ImGateway.DbConfigStore 4、ScriptRunnerDi 1、RepositoryScript BOM 1、AIProviderRegistry 1。**净效果：74 → 52，修复 22 红，新增 0 红。**

## E2E

- ⚠️ Unknown（本轮未跑）：插件层冒烟 e2e（`e2e/plugins/ai-agent`）待走查轮补——FR-3 UI 交互（开关→表单→事件行）以 vitest/build + 代码走查覆盖，真机交互留待 §0 六步闭环第②③步。

## Static Analysis

- `pnpm run build`（插件 vite build）绿；插件无 vue-tsc/tsconfig（项目现状），类型正确性经 esbuild 转译 + 消费侧编译间接覆盖 — Verified（能力边界如实记录）

## Screenshots

- ⚠️ 待走查轮补（起源码 dev-stack + 运行实例复验时截图存档）。

## Known Limitations

1. **评估器实现路径偏差**：评估调用经 `ForgeChatClientAdapter.GetResponseAsync` 直调（无工具、纯文本判据 + JSON 解析容错），非 02-spec FR-3.2 设想的 `ChatClientAgent + ChatResponseFormat.ForJsonSchema` 结构化输出。语义等价（BR-4 结论只来自评估输出、失败=未达成），少一层 MAF Agent 封装。已记 03-plan 偏差表。
2. **GoalPursuitTests 步骤行断言降维**：共享库下 XCode 跨类首插存在可见性竞态（见「测试基建根因」），步骤级持久化断言改由事件面（step_completed ×3）+ PlanJson + run.StepCount 承载；行级持久化语义由 AgentRunPersistenceTests 专类覆盖。已记 03-plan 偏差表。
3. **测试基建同型残留**：40 处非 AIAgent 连接名的 ctor 手搓连接串（DesignSystem/CostScope/Sems/ForgeSelf 系）未动——各连接名当前自洽，修复属专项批（已记 TODO P2）。
4. **前端事件行为**：聊天主链路不传 successCriteria（composer 不加目标输入，规格 UI 面仅 RunRecordPanel）；resume 场景若服务端续跑目标环，聊天侧以紧凑行展示评估结论。

## Unresolved Issues

- 无（中档全量第七轮对表：我域 0 红 + 新增 0 红；52 基线红已逐类归因）。
- 插件冒烟 e2e + 源码走查 + 运行实例复验（§0 插件六步的第②③⑤步）→ 待发布轮（已登记 07-final-report「下一步」）。

## 附：测试基建根因（XCode 12.0.2026.701 实测，证据级结论）

**缺陷类：测试 ctor 手搓 `DAL.AddConnStr` 换库（2026-10-29 三层实证）**

1. **换库不干净**（配对联跑 + 逐库 dump 实证）：`DAL.AddConnStr` 换串触发 `dal.ConnStr=…; dal.Reset()`，但 `DbBase.CreateSession` 的 ThreadLocal/AsyncLocal 会话缓存交替检出旧会话——同一测试的写操作随机落新旧两库（AgentRun 落旧库、AgentStepRun 交替、行整体消失；dump：目标目录 11 行步骤 vs 快照目录仅 2 行且 s2 的 Id=1）。
2. **表重建竞态**（换库触发 Reset 后 Migration=On 时）：XCode 对 SQLite 改表走「备份-重建-回填」，与并发写竞态时**吞行甚至吞表**（`no such table: AgentRun`；s2 行 Id=1 证明 s1 插入发生在重建的 DROP 之后）。
3. **修复（XCodeTestFixture.cs + 七个测试类，最终形态）**：
   - 进程级单次绑定——首个夹具 ctor 建随机目录 + 注册全部连接串 + `EnsureTablesCreated` 建表 + 预载插件程序集；后续 ctor 复用同目录仅 `ClearAllData()`（纯 DELETE ≈ 换新库语义）。
   - **Migration 保持 On**：不设实例级 Off（实证会堵死 `CheckTableWhenFirstUse` 实体的惰性建表 → 全量大面积 no such table）；无换库 → 无 Reset → CheckTables 全进程一次 → schema 不变 → SetTables 空操作 → 重建竞态消失。
   - **每个用 XCode 连接的测试类 ctor 主动 `EntityFactory.InitConnection(connName)`**：全量首绑可能早于插件实体程序集加载，`GetAllSubclasses` 只扫已加载程序集 → 首绑漏建；类自身引用实体类型 → JIT 已加载 → 必能扫到补建（不换串 → 无 Reset 无竞态）。注意 `ClearXCodeDataAttribute` 是从未应用的死代码（无程序集级注册），Per-Test 建表从未发生过。
   - 七类同类修复（去重绑 + ctor 补建）+ 两个计数断言按唯一 sessionId 限定。验证：十类 ×2 绿、中档全量第七轮 52 红全基线、我域 0 红、净修复 22 红。

**MAF 1.6.1 特性实测**（会话早段逐项验证，.temp 源码证据）：
- 工具必须每次运行时在 `ChatOptions.Tools` 配置；指令放 `ChatOptions.Instructions`
- `RunStreamingAsync` 迭代器内 yield 后再抛异常 → 事件已发出但通道关闭，需 Channel<T> 泵模式（yield+catch 不可靠）
- turn-stopping 事件类型 = `SerialAsync`（非 Realtime/RunStreaming）
- 多模态 `DataContent` 需跳过（翻译桥 try/catch per content）
- `ChatClientAgent.GetService<T>` 对未注册类型返回 null 而非抛错

## 验收编号索引

门禁按无连字符编号检索。等级仍以上文为准。

- AC1：`MafTaskLoopAgentTests.ToolCallThenInterimText_ContinuesUntilTaskComplete`，含在 Unit Test 节四件套 25/25。
- AC2：`MafTaskLoopAgentTests.PureQA_TextOnly_CompletesNormally`，同上。
- AC3：`ReactLoopTaskLoopTests`（续跑、纯问答、计划路径 ToolChoice），同上。
- AC4：`MapsRequest_MessagesSystemToolsToolMode` 与 `MapsStreamChunks_TextToolCallUsageFinish`，同上。
- AC5：`GoalPursuitTests` 达标续跑 / 预算 Stuck / 评估失败 fail-safe，Integration Test 节联跑全绿。
- AC6：`GoalPursuitTests.NonGoalMode_NoGoalEvents`，加上八类联跑里的 RunOrchestrator。
- AC7：Build 节与中档全量第七轮 3203/3255，我域 0 红。
- AC8：插件 `pnpm run build` 绿（Build 节）。冒烟 e2e 与真机走查在 E2E 节标 Unknown，未当作通过。
- AC9：本文件与 `06-review.md` 已落盘。
