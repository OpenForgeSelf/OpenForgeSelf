# 00 Repository Understanding — B2 结果硬化（filesChanged / Verify 闸门 / 结果收据）

> Task ID：`2026-11-02-maf-runtime-hardening/01-b2-fileschanged-verify`｜日期：2026-11-02｜状态：执行中（战役批闸门1 已批，见 00-campaign-workflow.md 头注）
> 上游：[00-campaign-workflow.md](../00-campaign-workflow.md)｜真源：[docs/01-architecture/maf-centric-architecture.md](../../../01-architecture/maf-centric-architecture.md)

## 1. 相关代码事实（全部实读核实，`文件:行` 可追溯）

### 1.1 filesChanged 落库链（B2①）

TodoTracker「执行记录」的写入口有三面（REST / AI 工具函数 / 界面），结构化字段 `FilesChanged` 的归一真源是 `Plugins/TodoTracker/Services/FileChangeList.cs`（写紧凑 JSON `[{"path":"…","change":"M"}]`，读回 `FromStorage`）。内置 agent（todo「一键交给本工具 AI Agent」）链路：

```
TodoDispatchService.DelegateAsync（内置分支）
  → IBuiltInAgentExecution.StartAsync（Plugins/AIAgent/Services/BuiltInAgentExecutionService.cs:43，包 IRunOrchestratorService 后台 Run）
  → 轮询 ReadStatusAsync → ReadBuiltInStatusAsync（TodoDispatchService.cs:531，snap.FilesChanged → AgentStatusDto.FilesChanged，:559）
  → 终态 TryAutoAdvanceAsync（:482）→ AppendAdvanceRecordAsync（:511）→ ITaskExecutionService.AppendSystemAsync
  → TaskExecution 落库（Plugins/TodoTracker/Services/TaskExecutionService.cs:120）
```

**命中两处同根因断点（TODO 233 的待定性②与①各中一个）**：

1. **落库漏赋值（主断点）**：`TaskExecutionService.AppendSystemAsync`（TaskExecutionService.cs:120-155）**没有 filesChanged 参数**，实体 `FilesChanged` 恒为空；调用方 `AppendAdvanceRecordAsync`（TodoDispatchService.cs:515-520）手里的 `status.FilesChanged`（:559 已填好）没传进去。⇒ 内置引擎的每一条「agent 执行回写」自动留痕，结构化字段必为 `[]`。
2. **工具轨迹解析失效（次断点）**：快照侧 `BuiltInAgentExecutionService.ExtractFilePaths`（BuiltInAgentExecutionService.cs:201-224）用正则 `path[\"']?\s*[:=]\s*[\"']([^\"',}\s]+)` 扫 `step.ToolCallsJson`。但 `ToolCallsJson` 是 `List<StepToolCallTrace>` 的序列化（RunOrchestratorService.cs:880），`ArgumentsSummary` 是**再套一层 JSON 字符串**（:686 `TruncateForTrace(started.ArgsJson)`，截 200 字符）——内层 JSON 的引号全被转义成 `\"`，形如 `\"path\\\":`，正则在 `path` 后遇到 `\` 而非引号/冒号，**永不命中**。⇒ 即使 Run 步骤真的写了文件，`snap.FilesChanged` 也是空。

### 1.2 Run 级 Verify（B2②）现状

- 计划任务 Run：`RunOrchestratorService.ExecuteStepsCoreAsync`（RunOrchestratorService.cs:616-807）逐步驱动，出口只有 complete_step / request_help / 失败；全部完成直接终局合成 → Completed（:802-806）。目标模式（PILOT-051 SuccessCriteria）走 `ExecuteWithGoalLoopAsync` LLM 评估外循环（:499-607），与确定性 Verify 闸门是两回事。
- 实体：`AgentRun`（Plugins/AIAgent/Data/Entities/AgentRun.cs，列=Model.xml:79-102）与 `AgentStepRun`（Model.xml:103-129）**均无验收标准 / Verify 状态 / 证据列**。实体改动铁律：先改 `Plugins/AIAgent/Data/Model.xml` 再跑 `xcode Model.xml` 生成，禁手改生成件（plugin-development 铁律 9；`xcode.exe` 在 `C:\Users\12504\.dotnet\tools\xcode.exe` 实测存在）。
- 开放任务 Run：不写 AgentRun，事实全在 SessionEvent 日志（`ForgeSelf.Abstractions/SessionEvents.cs`，15 类已注册进 `SessionEventMap.cs`；持久化=SessionEventEntity.PayloadJson 全量 JSON，新增事件**无需 DB 迁移**，但必须「定义 record + 注册 Map + DeriveMessages case」三步）。
- 领导指派中的 `ForgeSelf.Api/Entities/AgentRun*.cs` **实际不存在**——AgentRun/AgentStepRun 生成件在 `Plugins/AIAgent/Data/Entities/`（已在 TODO 🔄 条更正）。

### 1.3 Run 结果收据（B2③）现状

- MafTaskLoopAgent（Plugins/AIAgent/MafLoop/MafTaskLoopAgent.cs）：出口工具 task_complete 经 `MafLoopBridge.SetExit`（:53-59）单一记录，回合收束即止——**无结构化结果物**；工具轨迹只有 tool/call、tool/result 事件，无「本回合改了哪些文件」的聚合。
- 计划任务侧 Run 结束只有 `SynthesisAsync` 文本拼接（RunOrchestratorService.cs:898-913）落 SSE，不落库。
- UI：`Plugins/AIAgent/web/src/components/RunRecordPanel.vue`（1085 行逻辑+样式）已展示 Run 列表/详情/步骤时间线/卡住原因；AgentRunDto（`Plugins/AIAgent/Models/AgentRunModels.cs`）无验收/收据字段。

## 2. 测试基建事实

- XCode 真库守卫：`[Collection("XCode")]` + `IClassFixture<XCodeTestFixture>`（进程级单次绑定 + 每类 ClearAllData），参照 `ForgeSelf.Api.Tests/Plugins/AIAgent/GoalRunSseEvidenceTests.cs:21-31`；AIAgent 实体缓存清零模式同处。
- 步骤循环测试替身：`ScriptedProvider` + `FakeToolRegistry`（ReactLoopAgentTests.cs:399-512），MafFixture（MafTaskLoopAgentTests.cs:195-235）。
- TodoTracker 侧真库测试：`TodoDispatchFlowTests` / `TodoBuiltInDispatchTests`（假 IBuiltInAgentExecution）。

## 3. 不确定点

- `xcode` 重新生成 AgentRun/AgentStepRun 后 `.Biz.cs` 是否受影响：模板只覆写 `.cs`（Biz 人工维护不受影响），执行后以 git diff 复核。
- Verify 的「测试类校验」（跑验证命令）本批做到哪一层：见 02-spec §6 决策。
