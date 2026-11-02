---
task_id: 2026-11-02-b2-fileschanged-verify
feature: campaign/2026-11-02-maf-runtime-hardening-B2
risk: { level: L2, triggers: ["Plugins/AIAgent/**", "Plugins/TodoTracker/**", "ForgeSelf.Abstractions/**", "docs/11-troubleshooting/**"], raised_by_agent: false }
gate1: { mode: user, at: "2026-11-02T21:05:00+08:00", decision: "战役 00-campaign-workflow 经用户令开工（闸门1 视为已批）" }
gate2: { mode: reviewer, at: "2026-11-02T22:30:00+08:00", decision: "lead 走查证据复核通过，APPROVED（COMPLETED_WITH_RISK 收敛至低/信息级）" }
gate3: { mode: agent, at: null }
expected_files:
  - Plugins/AIAgent/MafLoop/RunVerifyService.cs
  - Plugins/AIAgent/Services/ToolCallFileExtractor.cs
  - Plugins/AIAgent/Models/AgentRunModels.cs
  - Plugins/TodoTracker/Services/TaskExecutionService.cs
  - ForgeSelf.Api.Tests/Plugins/AIAgent/RunVerifyGateTests.cs
  - docs/11-troubleshooting/problem-classes/PC-023-todo-fileschanged-persistence-loss.md
rollback: "git checkout -- <touched AIAgent/TodoTracker/Abstractions files>；Run#5-#8 测试数据留库无副作用"
writeback: { feature_doc: pending, reason: "021 执行记录节随本批收据/Verify 语义回写" }
---

# Task — B2 结果硬化（Agent 工作单元）

> Task ID：`2026-11-02-b2-fileschanged-verify`｜执行者：verify-dev（战役批 teammate）｜战役工作流：`docs/ai/campaigns/2026-11-02-maf-runtime-hardening/00-campaign-workflow.md`

## Allowed（本任务写面）

- `Plugins/AIAgent/MafLoop/`（RunVerifyService 新增；MafTaskLoopAgent 收据；不碰 ToolRegistry.cs/RegistryToolFunction 六闸门语义）
- `Plugins/AIAgent/Services/AIAgentService.cs`（CreateAgent 透传 ExitCriteria 等最小接线）
- `Plugins/AIAgent/Services/BuiltInAgentExecutionService.cs`（B2① 快照提取修复）
- `Plugins/AIAgent/Services/RunOrchestratorService.cs`（Verify 阶段 + 收据冻结 + EvidenceJson）
- `Plugins/AIAgent/Models/AgentRunModels.cs`（契约模型/DTO）
- `Plugins/AIAgent/Data/Model.xml` + `Data/Entities/AgentRun*.cs`（xcode 生成，禁手改）
- `ForgeSelf.Abstractions/`：`Agents.cs`（AgentOptions.ExitCriteria）、`SessionEvents.cs`、`SessionEventMap.cs`
- `ForgeSelf.Api/Services/PersistentSessionStore.cs` + `ForgeSelf.Abstractions.Tests/SessionStoreContractTests.cs` 的 InMemory 替身（DeriveMessages case，仅增 case 不改既有断言）
- `Plugins/TodoTracker/Services/TaskExecutionService.cs`、`TodoDispatchService.cs`（B2① 同根因命中项，§0 例外）
- `Plugins/AIAgent/web/src/components/RunRecordPanel.vue` + `web/src/types.ts`
- 测试：`ForgeSelf.Api.Tests/Plugins/TodoTracker/BuiltInFilesChangedPersistenceTests.cs`、`ForgeSelf.Api.Tests/Plugins/AIAgent/{RunVerifyGateTests,RunReceiptTests}.cs`
- 文档：本 pilot 目录 00-07、`docs/11-troubleshooting/`（case/PC/索引）、TODO.md（本条）、`.forgeself/memory/2026-11-02.md`

## Forbidden

- `ForgeSelf.Api/Services/ToolRegistry.cs`（B4 独占）及其闸门语义
- `ForgeSelf.Web/src/**`、`features.ts`（B3 独占）
- `:51888` 实例停/启/杀/配置（铁律）；dev-stack :7301/:7399 不停（DLL 锁先报 lead）
- 生成件手改（`.Biz.cs`/xcode 生成 `.cs` 手编辑）、宿主共享测试夹具重构、全量 e2e（未触发深档条件）

## 验证命令（§5.6 快档）

```powershell
$env:NO_PROXY='localhost,127.0.0.1,::1'; $env:TEMP="$PWD\.temp\tmp"; $env:TMP="$PWD\.temp\tmp"
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~BuiltInFilesChangedPersistence|FullyQualifiedName~RunVerifyGate|FullyQualifiedName~RunReceipt|FullyQualifiedName~MafTaskLoopAgent|FullyQualifiedName~TodoDispatch|FullyQualifiedName~TodoBuiltIn|FullyQualifiedName~SessionStoreContract"
cd Plugins/AIAgent/web; pnpm run build
pwsh -NoProfile -File scripts/check-problem-classes.ps1
```

判据：全部看日志正文（passed/failed 计数、PASS 字样），不拿 exit code 当证据。

## 完成定义

AC-1..8 全绿（02-spec）+ 问题类闭环出口清单全勾 + 05/06/07 齐 + 向 lead 报告（不自行 commit）。
