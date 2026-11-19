# Repository Understanding

> Task ID：`2026-11-19-oar-runtime-p3`

## 要点

- P0–P2 已落地：Worker、IAgentRuntime 契约、OarWorkerClient、Registry/OarTransport/Session、Delegation 选路。
- 缺：`AgentHubRuntimeProvider` + `ctx.Register`；工具/API/UI SessionKey 面。
- 注册铁律：同实例 MS DI + ctx；消费者每次 Get；Worker 清理由 Effect。

## 测试

```bash
dotnet build Plugins/AgentHub/AgentHub.csproj -p:EnableWindowsTargeting=true
dotnet test --filter "FullyQualifiedName~AgentHubRuntime|FullyQualifiedName~AgentTransport|FullyQualifiedName~AgentHubSession|FullyQualifiedName~TransportRouting|FullyQualifiedName~AgentHubDelegation|FullyQualifiedName~OarWorker|FullyQualifiedName~AgentRuntime"
```
