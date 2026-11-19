---
task_id: 2026-11-19-oar-runtime-p2
feature: 032
risk: { level: L2, triggers: ["Plugins/AgentHub/Data/Model.xml", "Plugins/AgentHub/Services/DelegationRuntime.cs"], raised_by_agent: true }
gate1: { mode: user, at: "2026-11-19T17:47+08:00", ref: "design gate1 + 续作 Implement P2" }
gate2: { mode: reviewer, at: null, decision: null }
gate3: { mode: agent, at: null }
expected_files:
  - Plugins/AgentHub/Data/
  - Plugins/AgentHub/Services/
  - Plugins/AgentHub/Models/
  - Plugins/AgentHub/AgentHubPlugin.cs
  - ForgeSelf.Api.Tests/Plugins/AgentHub/
  - docs/ai/pilot/2026-11-19-oar-runtime-p2/
  - docs/ai/pilot/2026-11-19-oar-agenthub-runtime/concurrency-plan.md
  - docs/02-features/032-agent-hub.md
  - docs/01-architecture/agenthub-oar-runtime.md
rollback: "git revert <commit>"
writeback: { feature_doc: 032-agent-hub, reason: "OAR P2 Transport/Session/Delegation" }
---

# Agent Task

## Objective

P2：Transport 注册表 + OarTransport + RuntimeId + AgentHubSession + Delegation 选路。

## Scope

### Allowed

expected_files 所列。

### Forbidden

P3 Provider/UI；停宿主；自动 install；OAR 类型进 Abstractionseds。

## Acceptance Criteria

- [x] AC1 Registry 选路测绿
- [x] AC2 Session 测绿
- [x] AC3 Oar 路由测绿
- [x] AC4 Delegation 既有过滤不因本批新增红
- [x] AC5 032 + 05–07

## Verification Commands

```bash
export PATH="/tmp/node24/node-v24.11.0-linux-x64/bin:$PATH"
dotnet build Plugins/AgentHub/AgentHub.csproj -p:EnableWindowsTargeting=true
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj -p:EnableWindowsTargeting=true --filter "FullyQualifiedName~AgentTransport|FullyQualifiedName~AgentHubSession|FullyQualifiedName~TransportRouting|FullyQualifiedName~AgentHubDelegation|FullyQualifiedName~OarWorker|FullyQualifiedName~AgentRuntime"
```
