---
task_id: 2026-11-19-dev-stack-linux-windowstyle
feature: none
task_kind: chore
risk: { level: L0, triggers: ["scripts/dev-stack.ps1"], raised_by_agent: true }
gate1: { mode: agent, at: "2026-11-19T12:00:00Z", ref: "Linux WindowStyle unblock for UI evidence" }
gate2: { mode: agent, at: "2026-11-19T12:05:00Z", decision: APPROVED }
gate3: { mode: agent, at: null }
expected_files:
  - scripts/dev-stack.ps1
  - docs/ai/pilot/2026-11-19-dev-stack-linux-windowstyle/
rollback: "git revert <commit>"
requirements: []
writeback: { feature_doc: none, reason: "infra-only Start-Process compatibility" }
---

# Task — dev-stack Linux WindowStyle

## Objective

Allow `scripts/dev-stack.ps1` to start on Linux/cloud pwsh by omitting unsupported `-WindowStyle`.

## Scope

### Allowed

expected_files 所列。

### Forbidden

Product UI/API; stopping/killing publish hosts (e.g. `:51888`).

## Acceptance Criteria

- [x] AC1 Linux `pwsh scripts/dev-stack.ps1 -SkipBuild` reaches backend/frontend ready
- [x] AC2 Windows path still sets WindowStyle when `$IsWindows`

## Verify

```bash
export NO_PROXY=localhost,127.0.0.1,::1
pwsh -NoProfile -File scripts/dev-stack.ps1 -SkipBuild
```
