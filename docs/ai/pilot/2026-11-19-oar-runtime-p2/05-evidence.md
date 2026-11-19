# Evidence

> Task ID：`2026-11-19-oar-runtime-p2`

## Changed Files

| 路径 | 动作 |
| --- | --- |
| `Plugins/AgentHub/Data/Model.xml` | AccessPoint.RuntimeId；新表 AgentHubSession |
| `Plugins/AgentHub/Data/Entities/AgentHubSession.{cs,Biz.cs}` | 新增实体 |
| `Plugins/AgentHub/Data/Entities/AgentAccessPoint.*` | RuntimeId + Oar 校验 |
| `Plugins/AgentHub/Data/AgentHubTables.cs` | EntityTypes + Session |
| `Plugins/AgentHub/Services/IAgentTransportRegistry.cs` | 新增 |
| `Plugins/AgentHub/Services/AgentTransportRegistry.cs` | 新增 |
| `Plugins/AgentHub/Services/OarTransport.cs` | 新增 |
| `Plugins/AgentHub/Services/AgentSessionManager.cs` | 新增 |
| `Plugins/AgentHub/Services/DelegationRuntime.cs` | 改 Registry 选路 |
| `Plugins/AgentHub/AgentHubPlugin.cs` | DI Cli+Oar+Registry+Session |
| `Plugins/AgentHub/Models/AgentDtos.cs` / `AgentRegistry.cs` | RuntimeId 映射 |
| `ForgeSelf.Api.Tests/Plugins/AgentHub/AgentTransportRegistryTests.cs` | 新增 |
| `ForgeSelf.Api.Tests/Plugins/AgentHub/AgentHubSessionManagerTests.cs` | 新增 |
| `ForgeSelf.Api.Tests/Plugins/AgentHub/AgentHubTransportRoutingTests.cs` | 新增 |
| `ForgeSelf.Api.Tests/Plugins/AgentHub/AgentHubDelegationRuntimeTests.cs` | Fake→Registry/Kind=Cli |
| `docs/02-features/032-agent-hub.md` | P2 进度 |
| `docs/01-architecture/agenthub-oar-runtime.md` | P2 ✅ |
| `docs/ai/pilot/2026-11-19-oar-runtime-p2/*` | 工件链 |

## Commands Run

| # | 命令 | 结果 | 等级 |
| --- | --- | --- | --- |
| 1 | `dotnet build Plugins/AgentHub -p:EnableWindowsTargeting=true` | 0 Error | Verified |
| 2 | `dotnet test --filter AgentTransport\|AgentHubSession\|TransportRouting\|AgentHubDelegation\|OarWorker\|AgentRuntime` | **29 Passed / 0 Failed** | Verified |
| 3 | `dotnet test --filter CliTransport\|AgentHubCli` | 1 Failed（`powershell` 不存在——Linux 基线，非本批引入） | Verified |
| 4 | `cd Plugins/AgentHub/RuntimeWorker && pnpm run build && pnpm run smoke` | smoke ok，12 步 | Verified |

日志：`/opt/cursor/artifacts/p2-dotnet-test.log`、`p2-cli-regression.log`、`p2-worker-smoke.log`

## AC 对照

| AC | 结果 | 证据 |
| --- | --- | --- |
| AC1 | ✅ | AgentTransportRegistryTests 2/2 |
| AC2 | ✅ | AgentHubSessionManagerTests 2/2 |
| AC3 | ✅ | AgentHubTransportRoutingTests 1/1 |
| AC4 | ✅ | DelegationRuntime 18 + P0/P1 回归共 29 绿；Cli timeout 环境基线红不变 |
| AC5 | ✅ | 032 §6 + 架构阶段表 + 本目录 05–07 |

## Known Limitations

- Cli timeout 用例依赖 `powershell`；Cloud Linux 无此可执行文件（既有基线）。  
- OarTransport 在 Worker 入口缺失时返回可解释错误事件，不阻断 Cli。  
- SessionKey 本批与 Worker sessionKey 对齐；P3 Provider 可换独立 GUID 映射。  
- **未**做 P3：`ctx.Register<IAgentRuntime>` / 工具 SessionKey / API·UI。
