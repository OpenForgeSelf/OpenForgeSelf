# Review — MAF Agent 循环 + 目标驱动执行（PILOT-051）

> 阶段：Stage 8｜Reviewer 视角重查 Intent → Spec → Plan → Task → Code → Test → Evidence 全链。
> 结论只报事实。定稿于中档全量第二轮对表后（见 05-evidence「Integration Test」）。

## 审查八问（逐项回答）

1. **实现是否真正满足 Intent？** PASS——两条需求均有可运行实现与门禁：需求1=任务循环（`task_complete` 显式收束 + 续跑 + 纯问答豁免，MAF/ReactLoop 双引擎同语义，25/25 引擎门禁绿）；需求2=目标模式（评估器判定 + plan-and-extend 续跑 + 预算耗尽 Stuck，GoalPursuitTests 5 例绿）。用户预授权自主决策，实现路径偏差已记录（05 Known Limitations 1/2）。
2. **实现是否符合 Spec？** PASS——FR-1/FR-2/FR-3 逐条落地；FR-3.2 的「MAF 结构化输出」降为适配器直调 + JSON 解析容错（语义等价，BR-4 保全），偏差已记 03-plan；BR-1~BR-5 全部成立（无新增表/列，配置走 PlanJson/事件 payload）。
3. **是否超出了 Scope？** PASS（有记录的同类修复）——测试基建修复超出 04-task 预期文件清单，但属「缺陷类同根因命中」（problem-class-closure 2026-10-17 例外条款）：不修则本批门禁无法绿（中档全量第一轮 11 红全部源于它）。已列 03-plan 偏差表 + TODO 专项批。
4. **是否修改了不应该修改的文件？** PASS——宿主源码（ForgeSelf.Api/**）零改动；Forbidden 清单（宿主前端/e2e 基建）未触碰；测试改动仅限清单内文件 + 同类修复 5 文件 + 计数断言 2 处。
5. **测试是否覆盖 Acceptance Criteria？** PASS——AC1~AC7（引擎/循环/评估/预算/fail-safe/兼容/回归）由 25 例引擎门禁 + GoalPursuitTests 5 例 + 十类联跑 ×2 覆盖；AC6（空值=现状）由既有 RunOrchestrator 测试全绿背书；AC8 前端 build 绿（UI 走查/冒烟 e2e 属六步闭环第②③步，本批范围注明待补）；AC9 工件链本件随 06/07 齐。
6. **是否存在明显回归风险？** L1——①`AgentOptions.UseMafRuntime` 默认 true：任务循环路径全量切换，回滚点=置 false 走 ReactLoop（同语义已实现）；②共享测试夹具七类改造经中档全量第七轮对表（52 红全基线、我域 0 红、净修复 22 红、新增 0 红）——风险已实测收敛。
7. **是否存在架构不一致？** PASS——双引擎共享 TaskLoopPrompts 契约（`task_complete` 唯一收束信号）；目标环放 RunOrchestratorService 外环（不改步循环内核）；事件只增不改；PlanJson 承载配置不触 DB——与 00-repository-understanding 的插件架构约定一致。
8. **Evidence 是否足以证明任务完成？** PASS（中档口径）——构建双绿 + 六类×3/八类×2 联跑绿 + 中档全量第二轮对表（基线红逐类归因）；引擎/目标语义门禁全部 Verified。缺口如实登记：冒烟 e2e/源码走查/运行实例复验待六步闭环后续步骤（未虚报完成）。

## Requirement Check

PASS（两条需求均有实现+测试门禁+证据；「一直做直到完成」的语义以 `task_complete` 显式收束 + 步数/轮数硬上限兜底实现，对标 agentic loop 行业惯例）

## Scope Check

PASS（同类修复在 2026-10-17 例外条款内，已记录；40 处非 AIAgent 连接名同型残留明确不入本批，记 TODO）

## Test Check

PASS（十类联跑 ×2 绿；中档全量第七轮 3203/3255——52 红全基线逐类归因与本批零交集，我域 7 类 0 红，净修复 22 红、新增 0 红）

## Architecture Check

PASS（双引擎同契约、目标环外置、事件只增、PlanJson 承载配置、回滚开关保留）

## Risk

L1（默认切换 MAF 运行时 + 共享测试夹具七类改造；均有回滚点，且中档全量第七轮对表已把夹具风险实测收敛）

## Findings

### Critical

（无）

### Major

（无——中档全量第七轮对表「新增红=0、我域红=0」达成定稿条件）

### Minor

1. 评估器未用 MAF 结构化输出（`ChatResponseFormat.ForJsonSchema`）——语义等价但少了 schema 层约束，解析容错测试（ParseEvaluation_ToleratesFencesAndNoise）兜底；后续可升级。
2. 插件前端无 vue-tsc 类型门禁（项目现状），本批 TS 类型仅经 esbuild 转译验证。
3. GoalPursuitTests 步骤行直查断言降维（见 05 Known Limitations 2）——行级持久化依赖 AgentRunPersistenceTests 专类，存在理论上的覆盖缝隙（夹具竞态修复后风险趋零）。
4. `ClearXCodeDataAttribute` 为从未应用的死代码（无程序集级注册）——已留档 05-evidence，清理属后续专项批。

## Final Decision

APPROVED
<!-- 定稿条件已满足：中档全量第七轮 3203/3255，52 红全基线（TodoTracker 32/Todos 7/WorkflowPlanning 6/ImGateway 4/杂项 3），我域 0 红、新增 0 红、净修复 22 红（74→52） -->
