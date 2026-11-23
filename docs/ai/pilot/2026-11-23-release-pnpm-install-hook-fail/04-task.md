---
task_id: 2026-11-23-release-pnpm-install-hook-fail
feature: none                      # 仓库基建脚本缺陷修复，不对应 docs/02-features 编号；行为真源 = scripts/install-git-hooks.ps1 + scripts/postinstall-install-hooks.cjs（AGENTS.md §0 门禁节）
risk: { level: L2, triggers: ["scripts/install-git-hooks.ps1（发布链 install 段必经）", "ForgeSelf.Api.Tests/RepositoryScriptTests.cs（仓库级守卫）"], raised_by_agent: false }
gate1: { mode: auto-granted, at: 2026-11-23, ref: "§8.5 自动迭代授权 + 用户输入5 直接指令（报错实录=修复指令）+ 本会话审批策略 never-ask；决策留痕见 07 与工作日记" }
gate2: { mode: merged-to-final-report, at: null, decision: null }
gate3: { mode: user-approved, at: 2026-11-23, ref: "用户输入6 明示「提交，合并回主分支然后推送github」" }
expected_files:
  - scripts/install-git-hooks.ps1
  - ForgeSelf.Api.Tests/RepositoryScriptTests.cs
  - docs/11-troubleshooting/problem-classes/PC-043-crossplatform-shortcircuit-before-posix.md
  - docs/11-troubleshooting/problem-classes/README.md
  - docs/11-troubleshooting/problem-classes/PC-024-frontend-poll-error-handling.md
  - docs/11-troubleshooting/cases/2026-11-23-release-pnpm-install-hook-fail.md
  - docs/11-troubleshooting/cases/README.md
  - docs/11-troubleshooting/index.md
rollback: revert 本提交即回滚（Windows 短路前置与守卫随提交移除；主仓 checkout 同 patch 工作区改动可单独 git checkout -- 还原）
---

# Agent Task

> 阶段：Stage 4 — Task｜规范：`docs/04-standards/ai-native-engineering-workflow.md`（轻量批精简 04：五要素已合并于 mini-task.md，本件承载门禁机械校验所需的头部元数据）

## Task ID

> Task ID：2026-11-23-release-pnpm-install-hook-fail（输入5，PC-043 首发）

## Scope

### Allowed
见 mini-task.md（2 代码文件 + 守卫 + 归档五件 + PC-024 前置数据去悬空）。

### Forbidden
不动 `scripts/postinstall-install-hooks.cjs`（行为正确）；不动 CI workflow；不碰主仓 checkout 他人未提交改动（package.json check:theme 行、TopNavbar/DESIGN.md WIP）。

## 验证

中档（scripts/** 触发）：全量 `dotnet test ForgeSelf.Api.Tests`（3985/37，3 处本批自引入红当批修复转绿）+ 主仓原位复跑 `pnpm install --frozen-lockfile` exit=0 + `check-problem-classes.ps1` OK（42 PC）+ hygiene -Staged PASS。
