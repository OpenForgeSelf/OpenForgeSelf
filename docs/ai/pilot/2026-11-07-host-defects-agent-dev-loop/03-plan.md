# Plan

> 全部文件路径相对 repo 根。顺序按「独立可验证」编排；每步先红灯后实现（problem-class-closure P1/P4）。

## 批次 B1 — P1① SmartTerminal DI（PC-005 复发，先做：最小且解锁后续演示）
1. 红灯：`ForgeSelf.Api.Tests/Architecture/PluginRawCreateScopeGuardTests.cs` 新增阳性对照——临时 fixture 文件含 `_sp.CreateScope()`（或直接以 Plugins/SmartTerminal 现状为红灯源）跑 `dotnet test --filter PluginRawCreateScopeGuardTests` 记红。
2. 守卫扩正则：`:28-30` 加 `_sp|_provider|_ctx|_context`；`TreatedAnchors` 增 `("Plugins/SmartTerminal/Services/SmartTerminalToolFunctions.cs", "CreateHostScope()")`。
3. 修复：`Plugins/SmartTerminal/Services/SmartTerminalToolFunctions.cs` 8 处 `_sp.CreateScope()`（:35/:76/:116/:155/:177/:209/:250/:289）→ `_sp.CreateHostScope()`；文件头 `using ForgeSelf.Abstractions;`。
4. 绿灯：同 filter 复跑记绿；`st_list_shells` 语义以单测/后续本地实例实测覆盖。

## 批次 B2 — P2① run_terminal_command（守卫+执行器）
1. 红灯先行（新测试文件 `ForgeSelf.Api.Tests/Plugins/McpCenterTests/RunTerminalCommandRedirectTests.cs` 等，参照既有 `ChainExecutionTests` 夹具风格）：
   - `Redirect_ToFile_WritesFile`（`echo hi > <tmp>\a.txt` 经 pwsh 落盘，PC-018 side-effect 断言）
   - `Redirect_Append_And_2>&1_Merges`（`>>` 与 `2>&1`）
   - `Redirect_Input_And_Backtick_StillRefused`（`<`、反引号保持拒绝，锚定既有文案格式）
   - `Timeout_ClapBounds`（timeoutSeconds=0/5000 → 落 [1,1800]，default 300）
   - `Timeout_ReturnsPartialOutput`（`$t=Start-Sleep 30; 'before'` 类长命令 + timeoutSeconds=2 → success=false 且 stdout 含 "before"）
2. 实现：
   - `TerminalCommandGuard.cs`：`MaxTimeoutSeconds=1800`；新增 `DefaultTimeoutSeconds=300`；`TrySplitSegments` → 新 `TrySplitSegmentsEx`（每段 `{Text, HasRedirect}`，旧签名委托）；引号外 `>` 分支判 `>>`/`1>`/`2>`/`2>&1` 形态放行并置 HasRedirect；`<`/反引号/裸 `&`（非 `&&`/`>&1`）拒绝不变。
   - `RunTerminalCommandTool.cs`：clamp 用 Default/Max 常量；`isChain = segments.Count>1 || segments.Any(HasRedirect)` → pwsh 路由；schema/Description 文案更新（「1-1800，默认300」「支持 > >> 2>&1；不支持 < 与反引号」「超时返回已捕获的部分输出」）。
   - 超时/取消分支：Kill 后 `Task.WhenAll(outTask, errTask).WaitAsync(5s)` 容错收集 → `ProcessOutputText.Decode` → 信封加 `stdout/stderr/stdoutTruncated/stderrTruncated`。
3. 更新 `ChainExecutionTests` 重定向拒绝断言 → 放行断言；跑 McpCenter 全 filter。

## 批次 B3 — P1② 空流 Failed（PC-013 逃逸批）
1. 红灯：`ForgeSelf.Api.Tests/Plugins/AIAgent/MafExitAndSuspendTests.cs` 新增 `ZeroByteTimeout_TurnFailed_NotSuspended`（现有 `RequestStall_TurnSuspended_KeepsScene` 保留为「部分产出」例）。
2. 实现：`MafTaskLoopAgent.cs` TimedOut 分支（:416-423）——`!runState.HasText && !runState.HasToolCall` ⇒ 走 Failed（同 :425-433 的 TurnFailed 帧路径），否则维持 Suspended。
3. 绿灯 + AIAgent filter 全绿。

## 批次 B4 — P2② 审批唤醒
1. 探明 `IAgentRegistry`/`IInbox`/`RunAsync` 在 Controller 可注入面（U1 落点）。
2. 红灯：`ForgeSelf.Api.Tests/Plugins/AIAgent/` 新增 Decide 唤醒集成测试（StubIApprovalService + 假 agent 或 spy Inbox）。
3. 实现：`ApprovalsController.Decide` 成功后按规格 4（P2②）唤醒；`resumed`/`resumeNote` 回填 DTO；幂等（非 Pending 不唤醒）。

## 批次 B5 — P2③ 调度器四子项
1. 红灯：`ForgeSelf.Api.Tests/Plugins/Scheduler/` 新增——`Once_WithoutRunAt_Rejected`、`Once_PastRunAt_Updated_KeepsNextRun`（update 不抹僵尸）、`Skip_DoesNotConsumeOnce`、`DeleteTool_RoundTrip`、UTC 归一用例。
2. 实现：`SchedulerService.cs`（create/update 校验+归一+僵尸守卫）、`TaskScheduler.cs`（Skip 不推进 :159-170）、`SchedulerPlugin.cs`（新工具 delete_scheduled_task，仿 :138-155 pause 模板）。
3. Cron/Interval 回归：Scheduler 既有 filter 全绿。

## 批次 B6 — P3① 上下文裁剪
1. 红灯：`MafExitAndSuspendTests`（或新 `MafContextTrimTests`）——超预算裁剪保留集断言（系统指令/最近N/成对不拆）+ 400 关键字触发裁剪重试成功。
2. 实现：`MafTaskLoopAgent.cs` :364 前 `TrimMessagesForBudget`（估算+显式事件）；400 关键字识别重试一次；溢出终态错误文案用户可读。

## 批次 B7 — P3② 零块降级
1. 红灯：新 `MafFailoverTests`——mock 两 provider 一坏一好 → 断言降级事件+最终成功+两次尝试可追溯；目录单模型行为不变。
2. 实现：`ForgeChatClientAdapter`（或 BuildPipeline 组装点，落点实现时定，05 回填）零 chunk 失败 → 目录下一启用模型重试一次；XTrace.Warn + turn 事件。

## 收口
- `pwsh scripts/check-problem-classes.ps1` 绿；PC-005/PC-013 归档更新（case + 逃逸分析 + 守卫行），P2②/P2③/P3① 定级新 PC（按 07 判定）。
- 中档全量 `dotnet test` 对基线（1516/13 红；新增红=0）；警告 ≤474。
- dev-stack 起本地实例 → 自动审批 → 浏览器驱动 Agent 开发插件迭代（04 的集成验证步骤）。
