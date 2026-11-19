---
task_id: 2026-11-19-oar-runtime-p3
feature: 032
risk: { level: L2, triggers: ["Plugins/AgentHub/AgentHubPlugin.cs", "Plugins/AgentHub/Services/AgentHubRuntimeProvider.cs"], raised_by_agent: true }
gate1: { mode: user, at: "2026-11-19T18:30+08:00", ref: "P2 checkpoint accepted → Implement P3" }
gate2: { mode: reviewer, at: null, decision: null }
gate3: { mode: agent, at: null }
expected_files:
  - Plugins/AgentHub/
  - ForgeSelf.Api.Tests/Plugins/AgentHub/
  - docs/ai/pilot/2026-11-19-oar-runtime-p3/
  - docs/ai/pilot/2026-11-19-oar-agenthub-runtime/concurrency-plan.md
  - docs/02-features/032-agent-hub.md
  - docs/01-architecture/agenthub-oar-runtime.md
rollback: "git revert <commit>"
writeback: { feature_doc: 032-agent-hub, reason: "OAR P3 Provider + SessionKey Surface" }
---

# Agent Task

## Objective

P3：AgentHubRuntimeProvider + Register + SessionKey Tools/API/UI。

## Scope

### Allowed

expected_files 所列。

### Forbidden

P4 消费者试点/打包正文；停宿主；自动 install；OAR 类型进 Abstractionseds。

## Acceptance Criteria

- [x] AC1 Provider 同实例测绿
- [x] AC2 List/Submit/Cancel 测绿
- [x] AC3 既有 Delegation/Transport 过滤不因本批新增红
- [x] AC4 032/架构 P3 ✅；05–07 齐

## Verification Commands

```bash
export PATH="/tmp/node24/node-v24.11.0-linux-x64/bin:$PATH"
dotnet build Plugins/AgentHub/AgentHub.csproj -p:EnableWindowsTargeting=true
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj -p:EnableWindowsTargeting=true --filter "FullyQualifiedName~AgentHubRuntime|FullyQualifiedName~AgentTransport|FullyQualifiedName~AgentHubSession|FullyQualifiedName~TransportRouting|FullyQualifiedName~AgentHubDelegation|FullyQualifiedName~OarWorker|FullyQualifiedName~AgentRuntime"
cd Plugins/AgentHub/RuntimeWorker && pnpm run smoke
```
