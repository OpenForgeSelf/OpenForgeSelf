---
task: 2026-11-22-github-bind-ux
task_kind: feature
feature: 101
risk: { level: L1, triggers: [新增设置页面板与弹窗组件、自动化剪贴板与 window.open 时序（fake timers 锁定）], raised_by_agent: false }
expected_files: [ForgeSelf.Web/src/types/githubAccount.ts, ForgeSelf.Web/src/services/githubAccountApi.ts, ForgeSelf.Web/src/utils/clipboard.ts, ForgeSelf.Web/src/components/github/GithubBindPanel.vue, ForgeSelf.Web/src/components/github/GithubBindDialog.vue, ForgeSelf.Web/src/components/settings/GithubAccountPanel.vue, ForgeSelf.Web/src/views/SettingsView.vue]
rollback: revert 本提交即回滚（组件与测试随提交移除；SettingsView 集成行还原）
---

# Agent Task

## Scope

### Allowed
见 mini-task.md Plan 表（7 文件 + 3 测试）。

### Forbidden
不改后端与 PluginStore；不引入裸色值；不显式 `import { ElXxx } from 'element-plus'`。

## 验证
过滤集 vitest 26/26；`pnpm run check` 本批文件零红；`verify-pilot-feature-writeback`（feature: 101）。
