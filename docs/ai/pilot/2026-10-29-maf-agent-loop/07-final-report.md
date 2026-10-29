# AI-Native Pilot Result（最终汇报）— MAF Agent 循环 + 目标驱动执行（PILOT-051）

> 任务结束强制格式｜状态只报事实。2026-10-29，中档全量第七轮对表后定稿。

## 1. Repository Understanding

我确认了：ForgeSelf.Api 为 .NET 10 + NewLife.XCode（唯一 ORM）+ 插件架构（`Plugins/` + plugin.json）；AIAgent 插件已有 ReactLoop 引擎（工具循环、SSE 事件、Run/Step 持久化、六闸门工具管线）；MAF 1.6.1（`Microsoft.Agents.AI`）可用但未接运行时；测试基建为 xUnit + `XCodeTestFixture`。详见 00-repository-understanding.md。

## 2. Selected Task

输入1 两需求：① AI Agent 发需求→调工具→回文本即停 → 改为任务循环持续执行直到完成（对标 agentic loop 行业惯例）；② 新增设置目标功能，agent 持续执行直到达成目标/满足目标要求。用户预授权「自行调研、自行决策开发，不用问我」。工件目录 `docs/ai/pilot/2026-10-29-maf-agent-loop/`。

## 3. Changed Files

生产代码（全部在 Plugins/AIAgent 插件内 + 宿主零改动）：`MafLoop/ForgeChatClientAdapter.cs`、`MafLoop/TaskLoopPrompts.cs`、`MafLoop/MafTaskLoopAgent.cs`、`Services/ReactLoopAgent.cs`（同语义任务循环）、`Services/GoalPursuitService.cs`、`Services/RunOrchestratorService.cs`（目标外环 + goal_* SSE）、`Models/AgentRunModels.cs`、`AIAgentPlugin.cs`/`AIAgentService.cs`（UseMafRuntime 工厂切换，回滚点）。前端：`web/src/types.ts`、`http.ts`、`agentBridge.ts`、`components/RunRecordPanel.vue`（新建执行表单：目标模式开关 + 验收标准 + 最大轮数 + 运行事件行）、`AiAgentView.vue`。测试：新增 GoalPursuitTests 5 例 + 引擎门禁三件套 25 例 + `XCodeTestFixtureIronRuleTests`（PC-014 G2 守卫）；`XCodeTestFixture` 升级进程级单次绑定；七类同类修复（去重绑 + ctor 补建）。**变更未 git 提交（等用户验收，闸门2）。**

## 4. Validation

Build: `dotnet build ForgeSelf.Api.Tests`（连带宿主/插件）成功（仅既有警告）✅ Verified；插件前端 `pnpm run build` = `✓ 98 modules transformed. ✓ built in 1.10s` ✅ Verified。
Unit Test: 引擎/适配器门禁 25/25 ✅；GoalPursuit 5/5 ✅；守卫+台账 6/6 ✅（Verified）。
Integration: 十类联跑 73/73、75/75 ✅ ×2（Verified）；**中档全量第七轮 3203/3255——52 红全基线（TodoTracker 32/Todos 7/WorkflowPlanning 6/ImGateway 4/杂项 3），我域 7 类 0 红，新增 0 红，净修复 22 红（修复前 74 红）** ✅ Verified（`.temp/test-full-7.log`）。
E2E: ⚠️ Unknown（本轮未跑插件冒烟 e2e——属 §0 插件六步第②步，待发布轮；已列入下方「下一步」）。

## 5. Evidence

`05-evidence.md`（含全部日志原文摘录 + 三层根因链 + MAF 1.6.1 特性实测五条）：任务循环以 `task_complete` 显式收束（BR-1）；目标环 = 评估器（ForgeChatClientAdapter 无工具直调）对照验收标准判定 + plan-and-extend 追加纠正步骤 + 预算耗尽 Stuck + 评估失败 fail-safe；事件只增（goal_round_started/goal_evaluated）；配置走 PlanJson 不触 DB。测试基建根因（XCode 换库竞态 + 首绑漏建表）三层实证全录。

## 6. Review

`06-review.md` 八问全 PASS，Final Decision = **APPROVED**（定稿条件：全量第七轮新增红=0、我域红=0）。Minor 4 条已逐条落 TODO/PC（见 §12）。

## 7. Risk

L1——UseMafRuntime 默认切换（回滚点=置 false 走 ReactLoop 同语义实现）；共享夹具七类改造经全量对表实测收敛。残余风险：UI 交互未走真机走查（Unknown 已登记）。

## 8. Problems Found

1. 测试基建缺陷类（PC-014，新建）：测试 ctor 手搓 `DAL.AddConnStr` 换库 → 全量吞行/吞表/写走别处（74 红归因，净修复 22 红）。
2. `ClearXCodeDataAttribute` 为从未应用的死代码（无程序集级注册）。
3. 同型残留 40 处（DesignSystem 18/CostScope 5/Sems 4/ForgeSelf 系 10 等）——各连接名当前自洽，专项批迁移。
4. MAF 特性实测五条（工具须运行时 ChatOptions 配置、迭代器 yield 后异常致通道关闭需 Channel 泵、turn-stopping=SerialAsync、DataContent 跳过、GetService null）。

## 9. Process Evaluation

| 环节 | 评价 |
| --- | --- |
| Repository Understanding | PASS |
| Intent → Spec | PASS |
| Spec → Plan | PASS（评估器实现路径偏差 + 夹具同类修复均记 03-plan 偏差表） |
| Plan → Code | PASS |
| Code → Test | PASS（6 轮全量迭代按 §6 升级规则推进：每轮新根因新方案，未原地打转） |
| Test → Evidence | PASS |
| Evidence → Review | PASS |

## 10. 问题类归档

PC-014（新建，首见）+ case：`docs/11-troubleshooting/problem-classes/PC-014-test-ctor-rebind-connstr.md`、`docs/11-troubleshooting/cases/2026-10-29-test-ctor-rebind-connstr.md`；G2 守卫 `ForgeSelf.Api.Tests/XCodeTestFixtureIronRuleTests.cs` active；`check-problem-classes.ps1` 14 个 PC 全通过（实测）。

## 11. 重构提案

R1（局部，07 披露）：测试基建专项批——39 处存量手搓连接串（PC-014 豁免清单）迁移到夹具唯一入口 + 删除死代码 `ClearXCodeDataAttribute`；已记 TODO P2（expires 2026-12-31），不在本批执行（范围控制）。

## 12. 悬空教训清单（必须为空）

（空——全部落位：①40 处专项批 → TODO.md「测试基建缺陷类」条目逐字含「40 处」；②ClearXCodeDataAttribute 死代码 → 同一条目逐字含；③MAF 结构化输出升级 → TODO.md「AIAgent 评估器结构化输出」条目；④插件冒烟 e2e/源码走查/运行实例复验 → TODO.md 主条目「插件六步 ②③⑤」逐字含；⑤vue-tsc 门禁缺失 → TODO.md「AIAgent 插件前端 vue-tsc」条目。）
