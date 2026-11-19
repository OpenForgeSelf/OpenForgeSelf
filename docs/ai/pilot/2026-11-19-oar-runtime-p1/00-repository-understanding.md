# Repository Understanding

> Task ID：`2026-11-19-oar-runtime-p1`

## 项目结构

- 解决方案 `ForgeSelf.slnx`；宿主 `ForgeSelf.Api/`；契约 `ForgeSelf.Abstractions/`；插件 `Plugins/AgentHub/`；测试 `ForgeSelf.Api.Tests/`。
- OAR Worker 已落地：`Plugins/AgentHub/RuntimeWorker/`（P0，`@botiverse/oar@0.109.0`，stdio JSON-RPC）。

## 技术栈

| 层 | 技术 | 依据 |
| --- | --- | --- |
| 后端 | .NET 10 + XCode | `ForgeSelf.Api.csproj` |
| 契约 | ForgeSelf.Abstractions | `AgentDelegationContracts.cs` 先例 |
| Worker | Node≥24 + TS | `RuntimeWorker/package.json` engines |
| 测试 | xUnit + FluentAssertions | `ForgeSelf.Api.Tests` |

## 架构特点

- L1 接缝：`ctx.Register` / `ctx.Get`；同实例铁律（AgentHub `IAgentDelegation` 先例）。
- Transport 现状：仅 `CliTransport` 实现 `IAgentTransport`（P2 才做 Registry）。
- P1 范围 = 契约落码 + Worker 客户端，**不** Register Provider、不改 Delegation。

## 测试方式

```bash
dotnet build Plugins/AgentHub/AgentHub.csproj
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~OarWorker|FullyQualifiedName~AgentRuntime"
# Worker（可选回归）
export PATH="/tmp/node24/node-v24.11.0-linux-x64/bin:$PATH"
cd Plugins/AgentHub/RuntimeWorker && pnpm run smoke
```

## 适用问题类

无（能力落地，非缺陷）。相邻：seams 同实例 / JSON 非 ASCII 转义守卫（Serialize 须 `ForgeJson`）。

## 候选低风险任务

本批选定：Abstractions `IAgentRuntime` + `OarWorkerClient`（依赖 P0 ✅）。
