---
task_id: 2026-11-20-oar-session-tab
feature: 032
risk: { level: L1, triggers: ["Plugins/AgentHub/web/src/AgentHubView.vue"], raised_by_agent: false }
gate1: { mode: user, at: 2026-11-20T05:45:00Z, ref: "user:单独一个页签展示会话" }
gate2: { mode: agent, at: 2026-11-20T06:05:00Z, decision: APPROVED }
gate3: { mode: agent, at: null }
expected_files:
  - Plugins/AgentHub/web/src/AgentHubView.vue
  - ForgeSelf.Web/e2e/plugins/agent-hub/agent-hub.spec.ts
  - docs/02-features/032-agent-hub.md
  - Plugins/AgentHub/docs/ui-design-brief.md
  - docs/ai/pilot/2026-11-20-oar-session-tab/mini-task.md
  - docs/ai/pilot/2026-11-20-oar-session-tab/04-task.md
  - docs/ai/pilot/2026-11-20-oar-session-tab/05-evidence.md
  - docs/ai/pilot/2026-11-20-oar-session-tab/06-review.md
  - docs/ai/pilot/2026-11-20-oar-session-tab/07-final-report.md
rollback: "git revert <commit>"
writeback: { feature_doc: done, reason: "032 §6.1 页签分区" }
---

# 04 Task — oar-session-tab

## Objective

AgentHub 顶栏将「Runtime 检测」与「会话」拆成独立页签；会话页不含 Runtime 网格与一键探测。

## Scope

### Allowed

见 YAML `expected_files`。

### Forbidden

- OAR Worker / Transport
- 停启用户宿主
- 引入模拟数据

## Acceptance Criteria

- [x] 顶栏有「Runtime 检测」「会话」
- [x] 会话页一键探测不可见；Runtime 页可见
- [x] 032 / ui-design-brief 回写
- [x] AgentHub web build 绿

## Verify

```bash
cd Plugins/AgentHub/web && pnpm run build
```
