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
writeback: { feature_doc: 032-agent-hub, reason: "CI gate only; evidence in this pilot + gap-close 05" }
---

# Agent Task

细节见 `mini-task.md`。

## Objective

Frontend Check 在 `pnpm run check` 前安装 CT StoryHost 链接的插件前端依赖。

## Scope

### Allowed

- `.github/workflows/frontend-check.yml`
- 本 pilot 目录

### Forbidden

- 改 DbAdmin/SmartTerminal 业务源码
- 改 OAR/AgentHub 产品行为
- 混入 PR#7 WindowStyle

## Acceptance Criteria

- [x] workflow 含 DbAdmin/SmartTerminal web `pnpm install --frozen-lockfile`
- [x] 本地模拟 CI 后 `pnpm run check` 绿

## Verification Commands

```bash
rm -rf Plugins/DbAdmin/web/node_modules Plugins/SmartTerminal/web/node_modules
(cd Plugins/DbAdmin/web && pnpm install --frozen-lockfile)
(cd Plugins/SmartTerminal/web && pnpm install --frozen-lockfile)
cd ForgeSelf.Web && pnpm run check
```
