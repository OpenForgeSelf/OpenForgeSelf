---
task_id: 2026-11-24-update-agent-restart-before-cleanup
feature: none          # 缺陷修复，非特性开发；对应问题类 PC-050，无 docs/02-features 编号
risk: { level: L1, triggers: ["ForgeSelf.UpdateAgent/UpdateAgentRunner.cs", "ForgeSelf.Api.Tests/ProblemClassEnumerationTests.cs"], raised_by_agent: false }
gate1: { mode: reviewer, at: 2026-11-24T17:02, ref: "用户回复 可 授权方案 A（交换步骤8/9）" }
gate2: { mode: agent, at: 2026-11-24T17:07, decision: "verify passed (G2 1/1, Registry 6/6, PC 40/40)" }
gate3: { mode: agent, at: 2026-11-24T17:10 }
expected_files:
  - ForgeSelf.UpdateAgent/UpdateAgentRunner.cs
  - ForgeSelf.Api.Tests/ProblemClassEnumerationTests.cs
  - docs/11-troubleshooting/problem-classes/PC-050-update-agent-cleanup-staged-before-restart.md
  - docs/11-troubleshooting/cases/2026-11-24-update-agent-exe-cleanup-before-restart.md
rollback: "git checkout -- ForgeSelf.UpdateAgent/UpdateAgentRunner.cs ForgeSelf.Api.Tests/ProblemClassEnumerationTests.cs（本任务未提交）"
writeback: { feature_doc: none, reason: "缺陷类任务无特性文档；归档落在 PC-050 + case" }
---

# Agent Task

## Task ID
PILOT-2026-11-24-EXE-AGENT-RESTART

## 目标
消除 exe 轨更新代理「先删 staged 再重启宿主」的自删运行时竞态（AC1–AC4）。

## T-1 修复步骤顺序
文件：`ForgeSelf.UpdateAgent/UpdateAgentRunner.cs`。把 `CleanupStagedAndBackups` 与 `RestartLauncher` 交换；顶部步骤注释同步更新为 8=重启 / 9=清理并写明不可交换理由。
- Verification：见 05-evidence AC1。

## T-2 新增 G2 守卫
文件：`ForgeSelf.Api.Tests/ProblemClassEnumerationTests.cs`。新增 `Pc050_UpdateAgentRunner_MustRestartBeforeCleanupStaged`，用 IndexOf 断言顺序，带阳性对照。
- Verification：见 05-evidence AC3。

## T-3 问题类归档
PC-050 + case + PC README / cases README / index 三处索引。
- Verification：见 05-evidence AC4。

## 执行边界
- Allowed：`UpdateAgentRunner.cs`、`ProblemClassEnumerationTests.cs`、PC-050/case/三处索引、本 pilot 00-07。
- Forbidden：发布脚本、shim、宿主 csproj、SDK/global.json、并行会话在途文件（`b-engineering-daily.md` / `c-changelog.md` / theme worktree）。

## 完成判据
AC1–AC4 全部 Verified（见 01-intent / 05-evidence）。
