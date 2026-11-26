---
task_id: 2026-11-24-release-local-zip-emptyoutdir
feature: none          # 发布链/构建配置修复，无对应 docs/02-features 功能编号
risk: { level: L1, triggers: ["Plugins/*/web/vite.config.ts", "ForgeSelf.Api.Tests/ProblemClassEnumerationTests.cs"], raised_by_agent: false }
gate1: { mode: user, at: 2026-11-24T12:10, ref: "用户回复 a 批准方案 A" }
gate2: { mode: reviewer, at: 2026-11-24T12:35, decision: APPROVED }
gate3: { mode: agent, at: null }
expected_files:
  - Plugins/AgentHub/web/vite.config.ts
  - Plugins/AIAgent/web/vite.config.ts
  - ForgeSelf.Api.Tests/ProblemClassEnumerationTests.cs
  - docs/11-troubleshooting/problem-classes/PC-047-release-local-zip-emptyoutdir.md
  - docs/11-troubleshooting/cases/2026-11-24-release-local-zip-emptyoutdir.md
rollback: "git checkout -- Plugins/*/web/vite.config.ts ForgeSelf.Api.Tests/ProblemClassEnumerationTests.cs（文档归档保留）"
writeback: { feature_doc: none, reason: "纯缺陷修复，无功能文档需回写" }
---

# 04 Task — 插件 vite emptyOutDir 类级修复

## T-1 批量修配置
- Objective：15 个插件 `vite.config.ts` 的 `emptyOutDir: true` → `false`。
- Scope Allowed：`Plugins/*/web/vite.config.ts` 中的 `emptyOutDir` 行；Forbidden：其他构建项、其他文件。
- Acceptance：grep 命中 0；git diff 仅 15 行。
- Verification：`grep -rn "emptyOutDir: true" Plugins/*/web/vite.config.ts`。

## T-2 新增 G2 守卫
- Objective：ProblemClassEnumerationTests 新增枚举测试断言插件 config 无 `emptyOutDir: true`，含阳性对照。
- Scope Allowed：`ForgeSelf.Api.Tests/ProblemClassEnumerationTests.cs`。
- Acceptance：`dotnet test --filter ProblemClassEnumerationTests` 绿。
- Verification：同上。

## T-3 问题类归档
- Objective：PC-047 + case + 三处索引。
- Acceptance：`check-problem-classes.ps1` 绿，Registry 测试绿。
- Verification：`pwsh scripts/check-problem-classes.ps1`。

## T-4 发布链复验
- Objective：修复后重跑 `release-local.ps1` 产出 zip。
- Acceptance：日志全文无 `SAFE_DELETE_BULK`，产物 zip 存在。
- Verification：命令 + 产物路径（Verified）。
