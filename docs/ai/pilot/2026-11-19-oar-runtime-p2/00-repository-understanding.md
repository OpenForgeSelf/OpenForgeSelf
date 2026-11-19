# Repository Understanding

> Task ID：`2026-11-19-oar-runtime-p2`

## 要点

- P0 Worker / P1 `IAgentRuntime`+`OarWorkerClient` 已落地；DelegationRuntime 仍 DI 单一 `IAgentTransport`（仅 Cli）。
- 实体真源 `Plugins/AgentHub/Data/Model.xml`；生成用 `xcode Model.xml`。
- 并发计划：T-Transport ∥ T-Session → T-Delegation 尾随。

## 测试 / 构建

```bash
dotnet build Plugins/AgentHub/AgentHub.csproj -p:EnableWindowsTargeting=true
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~AgentTransport|FullyQualifiedName~AgentHubSession|FullyQualifiedName~TransportRouting|FullyQualifiedName~AgentHubDelegation|FullyQualifiedName~OarWorker|FullyQualifiedName~AgentRuntime"
```

## 适用问题类

无（能力落地）。注意 XCode 实体改动铁律与同实例 DI。
