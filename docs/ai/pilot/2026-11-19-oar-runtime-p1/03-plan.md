# Plan

> Task ID：`2026-11-19-oar-runtime-p1`

## Files To Change

- file: `ForgeSelf.Abstractions/AgentRuntimeContracts.cs`  
  reason: `IAgentRuntime` + DTO  
- file: `Plugins/AgentHub/Services/OarWorkerClient.cs`  
  reason: stdio JSON-RPC 客户端 + 生命周期  
- file: `Plugins/AgentHub/Services/OarWorkerClientOptions.cs`  
  reason: NodePath / WorkerEntry / Timeout  
- file: `ForgeSelf.Api.Tests/Plugins/AgentHub/OarWorkerClientTests.cs`  
  reason: 真实 Worker 协议面（可 Skip）  
- file: `ForgeSelf.Api.Tests/Plugins/AgentHub/AgentRuntimeContractsTests.cs`  
  reason: 契约形状 + 无 OAR 泄漏  
- file: `docs/02-features/032-agent-hub.md`  
  reason: P1 进度回写  
- file: `docs/ai/pilot/2026-11-19-oar-runtime-p1/*`  
  reason: 工件链  
- file: 架构/ADR/seams 进度句（与审计同批时可一并勾 P1）

## Files Forbidden

- `Plugins/AgentHub/Data/Model.xml`、DelegationRuntime 选路、Controllers/web  
- `AgentHubPlugin` 内 `ctx.Register<IAgentRuntime>`（P3）  
- 停宿主 / 自动 install

## Implementation Steps

1. 落 Abstractions 契约（对齐真源 §5）。  
2. 实现 `OarWorkerClient`（Start/Request/Notify/Dispose）。  
3. 写契约单测 + Client 集成测（Node24 + Worker dist）。  
4. 回写 032 / Evidence / Review。

## Test Plan

1. `dotnet build` Abstractions + AgentHub + Api.Tests  
2. `dotnet test --filter "FullyQualifiedName~AgentRuntime|FullyQualifiedName~OarWorker"`  
3. 可选：Worker `pnpm run smoke` 回归

## Verification

见 04-task Verification Commands。

## Risks

| 风险 | 缓解 |
| --- | --- |
| CI Node&lt;24 | 测试 Skip + 本地/Cloud 用 `/tmp/node24` |
| 进程泄漏 | Dispose + 测试 finally |
