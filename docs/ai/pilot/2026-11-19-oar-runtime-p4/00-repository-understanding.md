# Repository Understanding

> Task ID：`2026-11-19-oar-runtime-p4`

## 要点

- P0–P3 已落地：Worker、契约、Client、Transport/Session、Provider + SessionKey Surface。
- 缺：消费者经 `ctx.Get<IAgentRuntime>()` 的可回归试点；Worker 不可用时 Cli 降级断言；Node≥24 运维真源写回；阶段表勾 P4。
- 消费铁律：每次 Get、禁止字段缓存；缺席 → null → 降级；Abstractionseds 无 OAR 类型。

## 测试

```bash
# PATH 须含 Node ≥24（环境自备；勿写死绝对路径）
dotnet build Plugins/AgentHub/AgentHub.csproj -p:EnableWindowsTargeting=true
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj -p:EnableWindowsTargeting=true --filter "FullyQualifiedName~AgentHubRuntime|FullyQualifiedName~AgentTransport|FullyQualifiedName~AgentHubSession|FullyQualifiedName~TransportRouting|FullyQualifiedName~AgentHubDelegation|FullyQualifiedName~OarWorker|FullyQualifiedName~AgentRuntime|FullyQualifiedName~ConsumerPilot"
cd Plugins/AgentHub/RuntimeWorker && pnpm run smoke
```
