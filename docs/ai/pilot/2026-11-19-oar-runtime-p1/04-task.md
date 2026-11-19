---
task_id: 2026-11-19-oar-runtime-p1
feature: 032
risk: { level: L2, triggers: ["ForgeSelf.Abstractions/AgentRuntimeContracts.cs", "Plugins/AgentHub/Services/OarWorkerClient.cs"], raised_by_agent: true }
gate1: { mode: user, at: "2026-11-19T17:47+08:00", ref: "design gate1 seq6 + 续作「方案落实了…并发实现」；P1 属已批定案阶段" }
gate2: { mode: reviewer, at: null, decision: null }
gate3: { mode: agent, at: null }
expected_files:
  - ForgeSelf.Abstractions/AgentRuntimeContracts.cs
  - Plugins/AgentHub/Services/OarWorkerClient.cs
  - Plugins/AgentHub/Services/OarWorkerClientOptions.cs
  - ForgeSelf.Api.Tests/Plugins/AgentHub/OarWorkerClientTests.cs
  - ForgeSelf.Api.Tests/Plugins/AgentHub/AgentRuntimeContractsTests.cs
  - docs/02-features/032-agent-hub.md
  - docs/ai/pilot/2026-11-19-oar-runtime-p1/
rollback: "git revert <commit>"
writeback: { feature_doc: 032-agent-hub, reason: "OAR P1 契约+OarWorkerClient" }
---

# Agent Task

## Objective

落地 `IAgentRuntime` 契约与 `OarWorkerClient`（stdio JSON-RPC + 生命周期），验证对接 P0 Worker。

## Scope

### Allowed

- 上表 expected_files + 本 pilot + 必要的架构进度句回写

### Forbidden

- Provider Register、Transport Registry、Session XCode、UI/API、停宿主、自动 install

## Acceptance Criteria

- [x] AC1：Abstractions `IAgentRuntime` 可编译且无 OAR 依赖
- [x] AC2：Client hello + runtime.list（含 scripted）
- [x] AC3：scripted open/prompt/dispose + Client Dispose
- [x] AC4：未改 Cli / IAgentDelegation 行为路径
- [x] AC5：032 §6 P1 进度 + 05–07

## Verification Commands

```bash
export PATH="/tmp/node24/node-v24.11.0-linux-x64/bin:$PATH"
cd Plugins/AgentHub/RuntimeWorker && pnpm run build
dotnet build ForgeSelf.Abstractions/ForgeSelf.Abstractions.csproj
dotnet build Plugins/AgentHub/AgentHub.csproj
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj --filter "FullyQualifiedName~AgentRuntime|FullyQualifiedName~OarWorker"
```
