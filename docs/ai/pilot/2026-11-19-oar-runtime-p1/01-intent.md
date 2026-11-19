# Intent

> Task ID：`2026-11-19-oar-runtime-p1`

## Problem

P0 Worker 可探针，但 .NET 侧无公共 `IAgentRuntime` 契约，也无稳定的 stdio JSON-RPC 客户端与进程生命周期，P2 Transport / P3 Provider 无法接线。

## Why

ADR-007 / 架构真源要求：契约进 Abstractions、OAR 不泄漏、Worker 延迟启停与崩溃可观测；并发计划 P1 = T-Contract ∥ T-WorkerClient。

## Expected Outcome

1. `ForgeSelf.Abstractions` 可编译的 `IAgentRuntime` + DTO（无 OAR/TS 类型）。  
2. `OarWorkerClient` 可对 P0 Worker：`hello` / `runtime.list` / scripted `session.open`→`prompt`→`dispose`，并正确 Dispose 进程。  
3. 既有 Cli / `IAgentDelegation` 行为不变（本批不改其代码路径）。

## Constraints

- 不实现 `AgentHubRuntimeProvider` / `ctx.Register<IAgentRuntime>`（P3）。  
- 不改 Model.xml / DelegationRuntime 选路（P2）。  
- 不自动 install 第三方 Agent；不停宿主。  
- JSON 写出走 `ForgeJson`。

## Success Criteria

- `dotnet build` 含 Abstractions + AgentHub 绿。  
- 过滤测试覆盖 Client 协议面（Node≥24 可用时跑真实 Worker；否则标 Skip 并有探测）。  
- 032 §6 写明 P1 进度。
