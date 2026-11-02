# Plan — B2 结果硬化（filesChanged / Verify 闸门 / 结果收据）

> Task ID：`2026-11-02-maf-runtime-hardening/01-b2-fileschanged-verify`｜推导自 02-spec｜TDD 顺序：每步先红后绿

## 步骤分解（改什么 → 预期什么 → 怎么验）

### S1 B2① 复现红灯（缺陷类，先红）
- 改：新增 `ForgeSelf.Api.Tests/Plugins/TodoTracker/BuiltInFilesChangedPersistenceTests.cs`
  - T1（快照提取）：真库构造 Run+StepRun（ToolCallsJson=含 `write_file` 双编码轨迹，现网形态）→ `BuiltInAgentExecutionService.FindAsync` → 断言 `FilesChanged` 非空（现红：正则不命中）。
  - T2（回写落库）：假 `IBuiltInAgentExecution` 返回带 FilesChanged 的快照 → `ReadStatusAsync` 触发 auto-advance → `GET records` 回读 `FilesChanged` 非空（现红：AppendSystemAsync 无参可传）。
- 验：`dotnet test --filter BuiltInFilesChangedPersistence`（环境前置 §0）→ 记红灯原文进 05-evidence。

### S2 B2① 绿灯修复
- 改：`Plugins/TodoTracker/Services/TaskExecutionService.cs`（AppendSystemAsync 增可选 filesChanged）；`Plugins/TodoTracker/Services/TodoDispatchService.cs`（AppendAdvanceRecordAsync 透传 status.FilesChanged）；`Plugins/AIAgent/Services/BuiltInAgentExecutionService.cs`（ExtractFilePaths 结构化解析优先+正则兜底）。
- 同类扫查：按签名「结构化字段序列化落库/读回丢失」扫 TodoTracker/AIAgent 写入口 → 命中表进 case §4。
- 验：S1 过滤集绿 + 相邻 `dotnet test --filter "FullyQualifiedName~TodoTracker|AIAgent"` 对表基线。

### S3 B2② 实体与契约（先测试后实现）
- 改：`Plugins/AIAgent/Data/Model.xml`（AgentRun 四列 + AgentStepRun.EvidenceJson）→ `xcode Model.xml` 生成 → git diff 复核 Biz 未动。
- 改：`Plugins/AIAgent/Models/AgentRunModels.cs`（RunExitCriterion/RunVerifyReport/RunResultReceipt 模型 + RunRequest.ExitCriteria + DTO 透出）、`ForgeSelf.Abstractions/Agents.cs`（AgentOptions.ExitCriteria）。
- 新：`Plugins/AIAgent/MafLoop/RunVerifyService.cs`（确定性校验器：file_exists/output_contains/files_changed_not_empty/未知=失败；workspace 解析+FilePathGuard）。
- 新：`ForgeSelf.Abstractions/SessionEvents.cs`（RunVerifyEvent + RunReceiptEvent）+ `SessionEventMap.cs` 注册 + `PersistentSessionStore.cs`/`InMemorySessionStore.cs` DeriveMessages case（不入模型历史）。

### S4 B2② 闸门接线（TDD）
- 改：`Plugins/AIAgent/Services/RunOrchestratorService.cs`：
  - `RunAsync` 落 ExitCriteria 快照进 PlanJson+AgentRun.ExitCriteria；
  - `ExecuteStepsCoreAsync` 收束处置 Completed 前插 Verify 阶段（RunVerifyService 校验 → VerifyReport/EvidenceJson/VerifyStatus 落库 → run/verify 事件 → 全过才 Completed，否则 Stuck）；
  - 步骤完成时写 `AgentStepRun.EvidenceJson`（文件清单+工具轨迹摘要）。
- 新：`ForgeSelf.Api.Tests/Plugins/AIAgent/RunVerifyGateTests.cs`（AC-3/4/6 真库）。

### S5 B2③ 收据冻结（TDD）
- 改：`RunOrchestratorService`（Completed 处聚合收据写 ResultReceipt，终态一次性写=冻结）；`Plugins/AIAgent/MafLoop/MafTaskLoopAgent.cs`（出口处聚合回合 tool 事件 → run/receipt 事件冻结）。
- 新：`ForgeSelf.Api.Tests/Plugins/AIAgent/RunReceiptTests.cs`（AC-5：计划任务收据+冻结不可覆盖；MafFixture 开放任务 run/receipt）。

### S6 B2③ UI 收据区块
- 改：`Plugins/AIAgent/web/src/components/RunRecordPanel.vue`（收据区块 + 五态中的空态=不渲染）+ `web/src/types.ts`（可选字段）。
- 验：`cd Plugins/AIAgent/web && pnpm run build`（+宿主侧 `pnpm run design:check` 若无窗口冲突）。

### S7 问题类闭环归档 + 工件
- 新：case（`docs/11-troubleshooting/cases/2026-11-02-fileschanged-persistence-loss.md`）+ PC（按模板）+ 两处 README/index 索引；05/06/07 齐含「问题类归档/重构提案/悬空教训」三节。
- 验：`pwsh scripts/check-problem-classes.ps1`（看日志正文 PASS）。

### S8 门禁复跑 + 汇报
- `dotnet test --filter "FullyQualifiedName~BuiltInFilesChangedPersistence|RunVerifyGate|RunReceipt|MafTaskLoopAgent|TodoDispatch|TodoBuiltIn"` 快档集（日志正文判据）；插件 web build；TODO 移除 + 日记收尾；向 lead 发改动清单/测试原文/工件路径/风险。

## 影响评估

- 公共契约面：`AgentOptions`（Abstractions，增 init 可空属性=向后兼容）、SessionEvent 两新 record（增面不改面）、RunRequest（可空字段）。无 DB 迁移风险（XCode Migration 补列 + 旧行默认值）。
- 测试基建：新增测试文件走既有 Collection/夹具模式，不改共享夹具 → 快档即可（不触发 §5.6 中档触发条件——未碰宿主源码/脚本/测试基建本体；SessionEvents 属 Abstractions 契约增面，RunOrchestrator/AIAgentService 属插件）。

## 风险分级

- 中：实体列变更（Model.xml+xcode）——Migration 自动补列、旧行默认值兜底；git diff 复核生成面。
- 低：TodoTracker 留痕参数扩展（可选参数，调用点兼容）。
- 低：MafTaskLoopAgent 出口收据（append-only 事件，不碰既有帧序——以 MafTaskLoopAgentTests 既有断言守序）。
