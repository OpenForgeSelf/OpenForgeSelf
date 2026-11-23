---
task_id: 2026-11-23-sems-log-poll-converge
feature: none                      # 插件缺陷修复，不对应 docs/02-features 编号；需求真源在 Plugins/Sems/docs/01-requirements.md（AC-F09-8）
risk: { level: L1, triggers: ["Plugins/Sems/web/src/workbench/workbenchStore.ts 轮询面条件"], raised_by_agent: false }
gate1: { mode: auto-granted, at: 2026-11-23, ref: "§8.5 自动迭代授权 + 用户输入4 直接指令「怎么整」= 修复指令 + 本会话审批策略 never-ask；决策留痕见 07 与工作日记" }
gate2: { mode: merged-to-final-report, at: null, decision: null }
gate3: { mode: not-executed, at: null, ref: "发布动作（git 提交/发布）须用户批准，本批不执行" }
expected_files:
  - Plugins/Sems/web/src/workbench/workbenchStore.ts
  - Plugins/Sems/web/src/workbench/stop-feedback.spec.ts
  - Plugins/Sems/docs/01-requirements.md
  - Plugins/Sems/docs/02-test-plan.md
  - docs/11-troubleshooting/problem-classes/PC-024-frontend-poll-error-handling.md
  - docs/11-troubleshooting/cases/2026-11-23-sems-stop-no-feedback.md
rollback: revert 本提交即回滚（tailPull 集合与选中面条件随提交移除，轮询面还原为仅看 logUnavailable 的既有行为）
---

# Agent Task

> 阶段：Stage 4 — Task｜规范：`docs/04-standards/ai-native-engineering-workflow.md`（轻量批精简 04：五要素已合并于 mini-task.md，本件承载门禁机械校验所需的头部元数据）

## Task ID

> Task ID：2026-11-23-sems-log-poll-converge（输入4，PC-024 第 4 发复发修复）

## Scope

### Allowed
见 mini-task.md Plan（4 处：store / spec / requirements / test-plan）+ PC-024 与 case 归档文档。

### Forbidden
不动后端（本轮纯前端轮询面）；不动宿主 `useTerminalInbox`（异根因，记 TODO 另立批）；不动 F25 工具语义与 PILOT-087 停止四路反馈。

## 验证

快档：`Plugins/Sems/web` 内 `pnpm run test`（vitest，含新增 2 例红→绿）+ `pnpm run build` + 仓库级 `check-problem-classes.ps1`（PC-024 增补后全绿）+ hygiene -Staged。
