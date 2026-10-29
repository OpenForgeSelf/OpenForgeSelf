---
id: 2026-10-29-test-ctor-rebind-connstr
pc: PC-014
date: 2026-10-29
pilot: docs/ai/pilot/2026-10-29-maf-agent-loop/
title: PILOT-051 中档全量 74 红归因——五个测试类 ctor 换库引发跨类竞态
---
# Case：PILOT-051 中档全量 74 红的测试基建根因闭环（2026-10-29）

## 现象

PILOT-051（MAF Agent 循环）六类过滤跑三连绿（43/43），中档全量 3255 例爆 74 红：
GoalPursuit 4 + PlannedRunUnifiedLoopGates 4 + OrchestratorBehaviorSnapshot 3（本批直接相关），
`BudgetExhausted` 报 `XSqlException: SQL logic error no such table: AgentRun`。

## 归因链（三层，全部实证）

1. **换库不干净**：`AgentRunPersistenceTests` / `AgentRunsControllerTests` / `RunOrchestratorTests` / `AIAgentProjectionAdversarialTests` / `AIAgentProjectionQaIndependentTests` 五类 ctor 各自建临时目录并 `DAL.AddConnStr("AIAgent", 自建目录)` → 换串触发 `DAL.Reset()` 换 Database（新实例 Migration 回 On）+ 旧 ThreadLocal/AsyncLocal 会话残留 → 后续类写入随机落新旧两库（配对联跑 + 逐库 dump：AgentRun 落旧库、AgentStepRun 交替、s2 行 Id=1 证明 s1 插入发生在重建 DROP 之后）。
2. **表重建竞态**：Migration=On 的 Database 每类重跑 CheckTables，SQLite「备份-重建-回填」与并发写竞态 → 吞行/吞表。
3. **首绑漏建表**（修复迭代期发现）：全量首绑夹具 ctor 可能早于插件实体程序集加载，`EntityFactory.InitConnection` 的 `GetAllSubclasses` 只扫已加载程序集 → 静默漏建（`no such table: AIChatMessage/WorkflowDefinition/ScriptExecution`）；`ClearXCodeDataAttribute` 为从未应用的死代码（无程序集级注册），Per-Test 建表从未发生。

## 修复（最终形态）

- `XCodeTestFixture`：进程级单次绑定（随机目录 + 全部连接串 + 建表 + 预载插件程序集）；后续 ctor 仅 `ClearAllData()`；**Migration 保持 On**（Off 会堵死 CheckTableWhenFirstUse 实体惰性建表）。
- 七个测试类（上列五类 + SessionManagementTests + AIAgentProjectionLateResultsTests）：删自建目录/换串，改 `IClassFixture` 注入 + ctor `EntityFactory.InitConnection(connName)` 补建（不换串无竞态）。
- `AgentRunsControllerTests` 两个计数断言改按唯一 sessionId 限定（共享库残留免疫）。
- 新增 G2 守卫 `XCodeTestFixtureIronRuleTests`（连接串唯一入口铁律 + 豁免清单）。

## 验证

十类联跑 ×2 = 73/73、75/75 绿；中档全量第七轮 **3203/3255，52 红全基线**（TodoTracker 32 / TodosController 7 / WorkflowPlanning 6 / ImGateway 4 / ScriptRunnerDi 1 / BOM 1 / AIProviderRegistry 1），我域 7 类 0 红、新增 0 红、**净修复 22 红（74→52）**。日志 `.temp/test-full-1..7.log`（会话临时目录，摘录见 05-evidence）。

## 逃逸分析

守卫此前不存在（首见即建类，非逃逸）。次级发现（死代码 `ClearXCodeDataAttribute`、40 处存量手搓点）已记 TODO 专项批与 PC-014 豁免清单（expires 2026-12-31）。
