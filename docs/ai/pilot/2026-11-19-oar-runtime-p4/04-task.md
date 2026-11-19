---
task_id: 2026-11-19-oar-runtime-p4
feature: 032
risk: { level: L1, triggers: ["ForgeSelf.Api.Tests/Plugins/AgentHub/AgentHubRuntimeConsumerPilotTests.cs"], raised_by_agent: true }
gate1: { mode: user, at: "2026-11-19T18:40+08:00", ref: "P3 checkpoint accepted a63c40d → Implement P4" }
gate2: { mode: reviewer, at: null, decision: null }
gate3: { mode: agent, at: null }
expected_files:
  - ForgeSelf.Api.Tests/Plugins/AgentHub/
  - Plugins/AgentHub/RuntimeWorker/README.md
  - docs/ai/pilot/2026-11-19-oar-runtime-p4/
  - docs/ai/pilot/2026-11-19-oar-agenthub-runtime/concurrency-plan.md
  - docs/02-features/032-agent-hub.md
  - docs/01-architecture/agenthub-oar-runtime.md
  - docs/01-architecture/host-capability-seams.md
  - docs/07-decisions/007-oar-agenthub-runtime.md
rollback: "git revert <commit>"
writeback: { feature_doc: 032-agent-hub, reason: "OAR P4 ConsumerQA + Node≥24 ops" }
---

# Agent Task

## Objective

P4：消费者试点 + Cli fallback 回归 + Node≥24 运维说明；勾选 P0–P4。

## Scope

### Allowed

expected_files 所列。

### Forbidden

P5；Watch SSE 重写；独立 SessionKey 映射表大实现；停宿主；自动 install；OAR 类型进 Abstractionseds。

## Acceptance Criteria

- [x] AC1 Consumer Get/降级/Cli fallback 测绿
- [x] AC2 Session DTO / SessionKey 解耦守卫绿
- [x] AC3 既有 Runtime 过滤集不新增红
- [x] AC4 Worker smoke 12/12
- [x] AC5 032/架构/concurrency/seams P4 ✅；05–07 齐

## Expected Files

见 front-matter `expected_files`。

## Verification Commands

```bash
# PATH 须含 Node ≥24（环境自备；勿写死绝对路径）
dotnet build Plugins/AgentHub/AgentHub.csproj -p:EnableWindowsTargeting=true
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj -p:EnableWindowsTargeting=true --filter "FullyQualifiedName~AgentHubRuntime|FullyQualifiedName~AgentTransport|FullyQualifiedName~AgentHubSession|FullyQualifiedName~TransportRouting|FullyQualifiedName~AgentHubDelegation|FullyQualifiedName~OarWorker|FullyQualifiedName~AgentRuntime|FullyQualifiedName~ConsumerPilot"
cd Plugins/AgentHub/RuntimeWorker && pnpm run smoke
```
