---
task_id: 2026-11-19-frontend-check-plugin-deps
feature: 032
risk: { level: L1, triggers: [".github/workflows/frontend-check.yml"], raised_by_agent: true }
gate1: { mode: authorized-by-task, at: "2026-11-19T12:45Z", ref: "CI FAIL Frontend Check on 549b08b" }
gate2: { mode: reviewer, at: null, decision: null }
gate3: { mode: agent, at: null }
expected_files:
  - .github/workflows/frontend-check.yml
  - docs/ai/pilot/2026-11-19-frontend-check-plugin-deps/
rollback: "git revert <commit>"
writeback: { feature_doc: 032-agent-hub, reason: "optional; CI gate only — note in gap-close evidence" }
---

# Mini-task：Frontend Check 缺插件 node_modules

## Why

`ForgeSelf.Web` CT story hosts 静态 import `Plugins/{DbAdmin,SmartTerminal}/web` SFC；`vue-tsc -b` 跟随进插件源码。CI 只 `pnpm install` 宿主前端 → 插件无 `vue` → 上千 TS 错。本分支改 `ForgeSelf.Web/e2e/**` 触发该 workflow。

## What

`frontend-check.yml` 在 `pnpm run check` 前安装 story 链接的插件前端依赖（frozen-lockfile）。

## Done when

- 隐藏插件 `node_modules` 后：装插件 deps → `pnpm run check` 绿（或至少 vue-tsc 绿）
- CI tip 再跑 Frontend Check 通过

## Out of scope

不改 SmartTerminal/DbAdmin 业务代码；不合并 PR#7。

## Interaction Design

**无界面交互**。仅改 GitHub Actions workflow（装依赖）；无用户可见 UI / 工具出参变更。
