---
task_id: 2026-11-24-release-netsdk1151-selfcontained
feature: none          # 缺陷修复，非特性开发；对应问题类 PC-049，无 docs/02-features 编号
risk: { level: L1, triggers: ["ForgeSelf.UpdateAgent/ForgeSelf.UpdateAgent.csproj", "ForgeSelf.Api.Tests/ProblemClassEnumerationTests.cs"], raised_by_agent: false }
gate1: { mode: reviewer, at: 2026-11-24T14:30, ref: "用户回复 a 授权方案 A" }
gate2: { mode: agent, at: 2026-11-24T14:45, decision: "verify passed" }
gate3: { mode: agent, at: 2026-11-24T14:50 }
expected_files:
  - ForgeSelf.UpdateAgent/ForgeSelf.UpdateAgent.csproj
  - ForgeSelf.Api.Tests/ProblemClassEnumerationTests.cs
  - docs/11-troubleshooting/problem-classes/PC-049-release-updateagent-selfcontained-inference.md
  - docs/11-troubleshooting/cases/2026-11-24-release-netsdk1151-selfcontained-inference.md
rollback: "git revert <commit>（本任务未提交，git checkout -- 指定文件即可）"
writeback: { feature_doc: none, reason: "缺陷类任务无特性文档；归档落在 PC-049 + case" }
---

# Agent Task

## Task ID
PILOT-2026-11-24-NETSDK1151

## 目标
消除 NETSDK1151，产出可校验的本地 zip，并固化类级守卫。

## T-1 修复 SelfContained 声明
- Objective：UpdateAgent.csproj 的 SelfContained 去 Condition 无条件 false + 中文根因注释。
- Scope Allowed：`ForgeSelf.UpdateAgent/ForgeSelf.UpdateAgent.csproj` 的 SelfContained 属性行及其注释。Scope Forbidden：其他 csproj 属性、publish 命令、SDK/shim、并行会话文件。
- Acceptance：grep `<SelfContained\s+Condition` = 0 命中；存在 `<SelfContained>false</SelfContained>`。
- Verification：`git diff ForgeSelf.UpdateAgent/ForgeSelf.UpdateAgent.csproj` + 最小 publish。

## T-2 新增 G2 守卫
- Objective：ProblemClassEnumerationTests 新增断言无条件 SelfContained=false 的测试，含阳性对照。
- Scope Allowed：`ForgeSelf.Api.Tests/ProblemClassEnumerationTests.cs`。Scope Forbidden：其他测试文件。
- Acceptance：`dotnet test --filter Pc049_…` Passed。
- Verification：`.temp/pc049.trx`。

## T-3 问题类归档
- Objective：PC-049 + case + 三处索引 + pilot 00-07。
- Scope Allowed：`docs/11-troubleshooting/` 与 `docs/ai/pilot/2026-11-24-release-netsdk1151-selfcontained/`。Scope Forbidden：其他文档、并行会话在途文件。
- Acceptance：check-problem-classes 39/39 + Registry 6/6 + pilot 工件门禁全开关绿。
- Verification：见 05-evidence AC3。

## 执行边界
只改 UpdateAgent.csproj + 守卫测试 + 归档文档；不动发布脚本/SDK/shim/并行会话文件。

## 完成判据
AC1–AC4 全部 Verified（见 01-intent / 05-evidence）。
