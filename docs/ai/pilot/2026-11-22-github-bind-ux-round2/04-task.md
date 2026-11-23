---
task: 2026-11-22-github-bind-ux-round2
task_kind: feature
feature: 101
risk: { level: L1, triggers: [安装主流程加前置分支（未绑定时零网络请求）、主题守卫用例扩展], raised_by_agent: false }
expected_files: [ForgeSelf.Web/src/views/PluginStore.vue, ForgeSelf.Web/src/styles/themes/workshop-forge.contrast.test.ts, ForgeSelf.Web/src/components/github/GithubBindPanel.vue]
rollback: revert 本提交即回滚（前置钩子随提交移除，安装行为还原为一轮直发）
---

# Agent Task

## Scope

### Allowed
见 mini-task.md Plan 表（3 文件）。

### Forbidden
不改安装后端与主流程实现；不改 workshop-forge.css 色值；不动一轮四态结构。

## 验证
过滤集 vitest（contrast + github 组件）全绿；`design:check`；`verify-pilot-feature-writeback`（feature: 101）。
