# Evidence — B2 结果硬化（filesChanged 落库 / Verify 闸门 / 结果收据）

> 证据分级：**Verified** = 本次会话实际执行并读取日志正文；Inferred = 代码推断未实跑。环境前置（NO_PROXY + TEMP/TMP=.temp\tmp）每轮已设。构建窗口由 lead 统一调度（2026-11-02 21:37-21:50），dev-stack 起停未触碰。

## 1 变更清单（实现面）

| # | 文件 | 变更 | 对应 FR |
|---|------|------|---------|
| 1 | `ForgeSelf.Abstractions/SessionEvents.cs` | 新增 `RunVerifyEvent`(run/verify)、`RunReceiptEvent`(run/receipt)、`RunExitCriterion`、`RunVerifyStatus`、`RunVerifyItem`、`RunVerifyInputs`、`RunVerifyOutcome`、`RunReceiptFile`、`RunResultReceipt` | FR-2.5/2.7, FR-3.1 |
| 2 | `ForgeSelf.Abstractions/SessionEventMap.cs` | 注册 `run/verify`、`run/receipt` | FR-2.5, BR-5 |
| 3 | `ForgeSelf.Api/Services/SessionEventProjection.cs` | 两类事件进不可见分支（不入模型历史） | BR-5 |
| 4 | `ForgeSelf.Abstractions/Agents.cs` | `AgentOptions.ExitCriteria` | FR-2.6 |
| 5 | `Plugins/AIAgent/Data/Model.xml` + xcode 生成 | AgentRun += ExitCriteria/VerifyStatus/VerifyReport/ResultReceipt；AgentStepRun += EvidenceJson（Migration=On 自动加列，.Biz.cs 零 diff） | FR-2.2 |
| 6 | `Plugins/AIAgent/Models/AgentRunModels.cs` | RunRequest.ExitCriteria；AgentRunDto/AgentStepRunDto 新字段；补 `using ForgeSelf.Abstractions`（编译断点修复） | FR-2.1, FR-3.4 |
| 7 | `Plugins/AIAgent/Services/ToolCallFileExtractor.cs`（新） | 结构化提取器：轨迹反序列化 → ArgumentsSummary 解析 path → 反转义正则兜底；ChangeOf 变更标记 | FR-1.3, FR-3.1 |
| 8 | `Plugins/AIAgent/MafLoop/RunVerifyService.cs`（新） | IRunVerifyService + 确定性断言 V1（file_exists/output_contains/files_changed_not_empty）+ fail-safe | FR-2.4/2.7, BR-2 |
| 9 | `Plugins/AIAgent/Services/AIAgentService.cs` + `ReactLoopAgent.cs` | AgentTurnRuntime.RunVerify 注入 + ResolveVerifyService 懒解析 | FR-2.6 |
| 10 | `Plugins/AIAgent/MafLoop/MafTaskLoopAgent.cs` | 回合 Completed → 冻结 run/receipt（RunId=0）+ 配置判据时 run/verify；校验器缺席 = VerifierAbsent fail-safe | FR-2.6, FR-3.3, BR-2 |
| 11 | `Plugins/AIAgent/Services/RunOrchestratorService.cs` | ExitCriteria 快照落列；三路收束共用 FinalizeRunAsync（Verify→Completed/Stuck）；FreezeReceipt write-once；CancelAsync 冻结；MarkStep* 证据；ToDto/ToStepDto 透出；ctor + sessionStore/runVerify 注入 | FR-2.3, FR-3.2, BR-1/2/3 |
| 12 | `Plugins/TodoTracker/Services/ITaskExecutionService.cs` + `TaskExecutionService.cs` | AppendSystemAsync += filesChanged（接口+实现对齐），经 FileChangeList.ToStorage 落库 | FR-1.1, BR-1 |
| 13 | `Plugins/TodoTracker/Services/TodoDispatchService.cs` | 回写留痕透传 status.FilesChanged | FR-1.2 |
| 14 | `Plugins/AIAgent/Services/BuiltInAgentExecutionService.cs` | 快照提取换 ToolCallFileExtractor（旧裸正则删除） | FR-1.3 |
| 15 | `Plugins/AIAgent/web/src/types.ts` | AgentRunDto/AgentStepRunDto 新字段 + RunResultReceipt/RunVerifyReport/VERIFY_STATUS_NAMES | FR-3.4, AC-UI |
| 16 | `Plugins/AIAgent/web/src/components/RunRecordPanel.vue` | 结果收据区块（状态徽标 + Verify 徽标 + 判据 ✓/✗ + 文件 chips + 工具计数/冻结时间），空收据不渲染 | FR-3.4, AC-UI-1/2 |

测试面（新增/改动）：`BuiltInFilesChangedPersistenceTests.cs`(新,2)、`RunVerifyGateTests.cs`(新,10)、`RunReceiptTests.cs`(新,5)、`MafFixture` RunVerify 注入口 + create_file 定义、`SessionStoreContractTests` fake 收录新事件类型。

## 2 TDD 红灯原文（先红后绿，AC1）

窗口内先把三处 B2① 修复临时还原至缺陷形态（接口/提取器/透传三撤，带「TDD 红灯临时还原点」标记），构建 0 错误后跑过滤集：

```
命令：dotnet test ForgeSelf.Api.Tests --no-build --filter "FullyQualifiedName~BuiltInFilesChangedPersistence"
日志：.temp/tmp/b2-red-evidence.log
  失败 …BuiltInFilesChangedPersistenceTests.步骤工具轨迹含write_file时快照应提取出目标路径 [69 ms]
  错误消息:
   Expected snapshot.FilesChanged not to be empty because Run 步骤确实调用了 write_file 写文件，
   结构化文件清单不得在快照段丢失（TODO 233 第 4 条回报形态）.
  失败 …BuiltInFilesChangedPersistenceTests.内置成功自动回写留痕应携带结构化文件清单 [115 ms]
  错误消息:
   Expected advance!.FilesChanged not to be empty because 回写留痕时状态快照已带文件清单，
   落库不得丢弃（TODO 233：5 条记录 filesChanged 全空）.
失败!  - 失败: 2，通过: 0，已跳过: 0，总计: 2
```

红灯与 TODO 233 用户回报形态一致（两段断点各红一条）→ 恢复三处修复 → 复测绿。

## 3 门禁实测（Verified）

| 门禁 | 命令/日志 | 结果 |
|------|-----------|------|
| 后端构建 | `dotnet build ForgeSelf.Api.Tests` → .temp/tmp/b2-build-green.log | **0 错误**（462 警告均为既有基线警告面） |
| B2① 过滤集 | `--filter BuiltInFilesChangedPersistence` → b2-green-fileschanged.log | **2/2 通过** |
| 核心新测试 | `--filter RunReceipt|RunVerifyGate|MafTaskLoopAgent` → b2-filter-core.log | **23/23 通过** |
| 全量过滤集 | 15 个过滤子（含 TodoDispatch/TodoBuiltIn/SessionStoreContract/RunOrchestratorTests/PlannedRunUnifiedLoopGates/GoalRunSseEvidence/AgentRunsControllerTests/GoalPursuitTests/OrchestratorBehaviorSnapshot/TaskExecutionService/ReactLoopAgent）→ b2-filter-full.log | **121/121 通过，0 失败（10s）** |
| 基线对表 | qa-gate 基线 89 红（qa-baseline-dotnet.log） | 过滤集零红，**无新增回归** |
| 插件前端 | `cd Plugins/AIAgent/web && pnpm run build` → b2-web-build.log | **绿**（99 modules，dist/index.js 290.04KB） |
| 守卫的守卫 | `--filter ProblemClassRegistryTests` → b2-registry.log | **5/5 通过** |
| 问题类签名扫描 | `pwsh scripts/check-problem-classes.ps1` → b2-checkpc.log | **OK：23 个 PC 全部通过** |

## 4 测试对 AC 映射

| AC | 证据 | 结论 |
|----|------|------|
| AC1 红→绿 | §2 红灯原文 + b2-green-fileschanged.log 2/2 | ✅ Verified |
| AC2 守卫（带/不带） | T1/T2 覆盖带值落库；不带 = 既有 19 调用点零变化（过滤集全绿） | ✅ Verified |
| AC3 Verify 闸门 | RunVerifyGateTests.VerifyGate_AllPassed（Passed+run/verify 事件+收据）/ VerifyGate_Failed（Stuck+明细）；file_exists 命中/未命中/工作区缺席在 RunVerifyService 单测 3 例 | ✅ Verified |
| AC4 断言类型 | RunVerifyServiceTests：output_contains 大小写命中/未命中、files_changed 空/非空、未知类型 fail-safe、混合判据、VerifierAbsent | ✅ Verified |
| AC5 收据 | RunVerifyGate 两例（Completed/Stuck 冻结）+ RunReceiptTests（开放任务 run/receipt + run/verify Passed/Failed/VerifierAbsent + 纯问答不产收据）；**cancel 冻结路径代码级复用同一 FreezeReceipt（write-once），无独立用例** | ✅ Verified（cancel 无专测，见风险） |
| AC6 兼容 | RunVerifyGateTests.NoExitCriteria（无 Verify 阶段、无事件、收据仍冻结）+ 既有过滤子（MafTaskLoopAgent 8 例含纯问答精确序列、ReactLoopAgent 等）零回归 | ✅ Verified |
| AC7 UI | 插件 web build 绿（AC-UI-3）；**AC-UI-1/2 真实宿主走查 Verified**（22:17-22:19，dev-stack :7301/:7399 含 B2 bin）：RunRecordPanel 收据区块两态齐——Run#7 PASS（结果收据+已完成+验收通过徽标+✓判据行+工具 0/0+冻结时间）、Run#8 FAIL（已卡住+验收未通过徽标+✗判据行带未过原因+卡住告警条）；截图 `ForgeSelf.Web/screenshots/dev/b2-receipt-20261102/`（receipt-pass-run7 / receipt-fail-run8 / panel-overview-run8，已读图核对） | ✅ Verified |
| AC8 问题类闭环 | case + PC-023 双档、三处索引、Registry 5/5、签名扫描 OK | ✅ Verified |

## 5 测试期发现并当场修复的缺陷

1. **ReceiptJsonOptions 缺 `JsonStringEnumConverter`**：VerifyReport.status 序列化为数字（2），前端按字符串约定读不到 → 两处选项补转换器（RunOrchestratorService + MafTaskLoopAgent）。
2. **MAF 路径 write_file 挂审批**：HighRiskToolPolicy 硬名单把 write_file 包成 ApprovalRequiredAIFunction → 回合挂起不收束。**这是正确的审批语义被测试样本踩中**，测试样本改用 create_file（非名单写类工具）。
3. **ToolCallFileExtractor 迭代器形态非法**（yield 在 try-catch 内，CS1626/1631）+ MatchCollection 非泛型枚举（CS1061）→ 改 List 返回 + 显式 `Match` 类型。
4. **AgentRunModels.cs 缺 using**（编译断点，taskcenter-dev 撞上）→ 补 `using ForgeSelf.Abstractions`；`ITaskExecutionService` 接口参数面同步（15 参）。
5. 顺手最小修复 `TasksOverviewController.cs:295` 三元 `DateTimeOffset?` 类型不匹配（taskcenter-dev 文件，一行，已报备 lead 知会对方）。

## 6 走查实证（2026-11-02 22:12-22:19 · dev-stack :7301/:7399，lead 已载入 B2 bin）

**链路全通实证（API 侧）**：`POST /api/ai-agent/runs`（SSE）带 `exitCriteria` → plan_created/step_started/step_completed/goal_evaluated → Verify 阶段 → run_stuck（出口验收未通过，含判据与产出长度）或 done；Run 详情回读 `exitCriteria/verifyStatus/verifyReport/resultReceipt` 全部落库，camelCase JSON + 枚举出字符串（"Failed"/"Passed"），步骤 `evidenceJson` 落库（{"files":[],"toolCalls":0,"toolOk":0}）。旧 Run（无判据）回读 verifyStatus=0/resultReceipt=null（BR-3 兼容 ✓）。

**UI 侧（AC-UI-1/2，截图三张已读图）**：执行记录面板按当前会话过滤（listRuns?sessionId=…），Run#7（判据 text=验收通过）→「结果收据 已完成 验收通过」+ ✓ 行 + 工具 0/0 成功 · 冻结于 22:18:31；Run#8（判据 text=产出包含「验收通过」）→「结果收据 已卡住 验收未通过」+ ✗ 行带未过原因 + 顶部红色「出口验收未通过」告警条 + 步骤时间线/介入按钮组（继续/重开/跳过/补位/取消）。

**走查抓到并当场修复的真缺陷（TDD 红→绿）**：`output_contains` 把判据整句 Text 当匹配串——Run#5 判据 text=「产出包含「验收通过」」、产出「验收通过」却判未过（detail=「产出不包含「产出包含「验收通过」」」）。fail-safe 语义正确（未过→Stuck，绝不假绿），但断言字面量与人类描述未分离。修复：`RunExitCriterion` 增 `Value`（断言字面量，缺省回退 Text，裸词判据不变）；RunVerifyService 的 output_contains/file_exists 取 `LiteralOf(criterion)`。新增 3 用例先红（2 失败/1 通过，构建被运行实例锁 DLL 走 `-p:OutDir` r2 旁路）后绿（3/3），全过滤集 r2 旁路复跑 **124/124 零失败**。**注**：运行实例仍载旧 bin，Value 语义随下次重启/发布生效（源码+测试已齐）。

## 7 未验证 / 待办（Unknown 与遗留）

- **cancel 冻结无专测**：FreezeReceipt write-once 守卫与 Stuck/Completed 共路径，已记 TODO P3。
- **FR-2.1 修订**：ExitCriteria 实际落 `AgentRun.ExitCriteria` 独立列（非 PlanJson 随行）——原因：PlanJson 是计划面缓存、判据是运行面事实，分列后 resume/audit 不依赖 Plan 反序列化。已在 02-spec 补修订记录。
- 外部 AgentHub 委派回写的 filesChanged 语义（Hub 侧清单）未覆盖（PC-023 §4 盲区已登记）。
- **走查测试数据**：Run#5/#6（sessionId=null，面板不可见）、Run#7/#8（绑会话 mvvrf5g1-b9yg4a，面板可见）+ 对应会话事件/run:{id} 会话日志。runs 无 DELETE API，数据留在库中（纯收据/验证语义数据，无文件副作用——两 Run 均零工具调用）；如需清净可由 lead 直接 SQL 清 AgentRun 5-8 行。
- 走查环境注记：构建被运行实例锁 DLL（MSB3027，pid 33776 锁 ForgeSelf.Api\bin）→ 测试构建走 `.temp\bin-r2` OutDir 旁路（输入11 先例）；截图 MCP 相对路径落在 DSH profile cwd（`C:\Users\12504\.dsh\profiles\desktop\ForgeSelf.Web\...`），已拷回仓库。
