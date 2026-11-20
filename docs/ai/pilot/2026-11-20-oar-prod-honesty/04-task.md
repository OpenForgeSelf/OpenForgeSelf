---
task_id: 2026-11-20-oar-prod-honesty
feature: 032
risk: { level: L2, triggers: ["Plugins/AgentHub/Services/", "Plugins/AgentHub/RuntimeWorker/"], raised_by_agent: true }
gate1: { mode: authorized-by-user, at: "2026-11-20T03:49Z", ref: "user:Critical product bugs to FIX now" }
gate2: { mode: reviewer, at: null, decision: null }
gate3: { mode: agent, at: null }
expected_files:
  - docs/02-features/032-agent-hub.md
  - Plugins/AgentHub/Services/OarTransport.cs
  - Plugins/AgentHub/Services/OarPromptTurnGate.cs
  - Plugins/AgentHub/Services/OarWorkerClient.cs
  - Plugins/AgentHub/RuntimeWorker/src/handlers.ts
  - ForgeSelf.Api.Tests/Plugins/AgentHub/OarPromptTurnGateTests.cs
  - ForgeSelf.Api.Tests/Plugins/AgentHub/OarTransportTurnSettleTests.cs
  - ForgeSelf.Api.Tests/Plugins/AgentHub/OarHandlersHonestyGuardTests.cs
  - ForgeSelf.Api.Tests/Plugins/AgentHub/OarWorkerClientTests.cs
  - docs/11-troubleshooting/problem-classes/PC-037-oar-honesty-without-evidence.md
  - docs/11-troubleshooting/cases/2026-11-20-oar-early-exit-and-bundled-fake.md
  - docs/ai/pilot/2026-11-20-oar-prod-honesty/
rollback: "git revert <commit>"
writeback: { feature_doc: 032-agent-hub, reason: "OAR honesty" }
---

# Agent Task

## Objective
OAR×AgentHub 生产诚实性：prompt 等真实 turn 结算；无 installation 诚实 not_found；产品面 omit scripted。

## Scope

### Allowed
expected_files 所列。

### Forbidden
停杀宿主；假 Succeeded；产品面展示 scripted；改 csproj 版本。

## Acceptance Criteria
- [x] 无短 sleep 假 Exit
- [x] handlers 无 !installation → available/bundled
- [x] 相关测试 + Worker smoke + AgentHub build 绿
- [x] PC-037 + case；07 含三节

## Verification Commands
见 mini-task / 05-evidence。
