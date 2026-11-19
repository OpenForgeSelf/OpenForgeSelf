# Evidence

> Task ID：`2026-11-19-oar-runtime-p1`

## Changed Files

| 路径 | 动作 |
| --- | --- |
| `ForgeSelf.Abstractions/AgentRuntimeContracts.cs` | 新增 `IAgentRuntime` + DTO |
| `Plugins/AgentHub/Services/OarWorkerClient.cs` | 新增 stdio JSON-RPC 客户端 |
| `Plugins/AgentHub/Services/OarWorkerClientOptions.cs` | 新增启动选项 |
| `ForgeSelf.Api.Tests/Plugins/AgentHub/AgentRuntimeContractsTests.cs` | 新增契约守卫 |
| `ForgeSelf.Api.Tests/Plugins/AgentHub/OarWorkerClientTests.cs` | 新增 Worker 协议面测试 |
| `ForgeSelf.Api.Tests/Plugins/AIAgent/AgentRunDispatchTests.cs` | 补 `ExecuteConfirmedPlanAsync` 替身（基线编译阻断） |
| `docs/02-features/032-agent-hub.md` | P1 进度 |
| `docs/ai/pilot/2026-11-19-oar-runtime-p1/*` | 工件链 |
| 架构/ADR/seams/README + design pilot 回写 | 进度勾选 |

## Commands Run

| # | 命令 | 结果 | 等级 |
| --- | --- | --- | --- |
| 1 | `PATH=…/node24… node -v` | v24.11.0 | Verified |
| 2 | `cd Plugins/AgentHub/RuntimeWorker && pnpm run build` | tsc OK | Verified |
| 3 | `dotnet build ForgeSelf.Abstractions` | 0 error | Verified |
| 4 | `dotnet build Plugins/AgentHub` | 0 error | Verified |
| 5 | `dotnet test --filter …AgentRuntime\|OarWorker`（Linux 下去掉 WindowsDesktop framework 后） | **6 Passed** | Verified |
| 6 | `dotnet test --filter …AgentHubCli\|AgentDelegation` | 10 Passed / 1 Failed（`powershell` 不存在于本 Linux 环境——基线环境限制，非本批引入） | Verified |

日志：`/opt/cursor/artifacts/oar-p1-dotnet-test2.log`、`oar-p1-cli-filter.log`、`oar-p1-worker-build.log`

## AC 对照

| AC | 结果 | 证据 |
| --- | --- | --- |
| AC1 | ✅ | Abstractions 可编译；源码守卫无 botiverse |
| AC2 | ✅ | Hello protocol=1 + list 含 scripted |
| AC3 | ✅ | open/prompt/dispose + Dispose |
| AC4 | ✅ | 未改 CliTransport/DelegationRuntime/IAgentDelegation 实现路径；Delegation 过滤 10 绿 |
| AC5 | ✅ | 032 §6 + 本目录 05–07 |

## Known Limitations

- Cloud/Linux 跑 `net10.0-windows` 测试需去掉 `Microsoft.WindowsDesktop.App` runtimeconfig 项（宿主 `UseWindowsForms`）；属环境手法，非产品代码。  
- `AgentHubCliTransportTimeoutTests` 依赖 `powershell` 可执行文件，Linux 上失败为环境基线。  
- P1 **未** `ctx.Register<IAgentRuntime>`（P3）。

## Gate unblock (CI)

- `cfbdbb6` 触发 pilot-gate「越界 diff」：`ForgeSelf.Api.Tests/Plugins/AIAgent/AgentRunDispatchTests.cs` 未列入 `04-task.md` `expected_files`（该文件为编译基线替身，同批改动）。
- 已补入 `expected_files`；本地 `pwsh scripts/verify-pilot-gate.ps1 -Base origin/main` 应对齐 CI「门禁（pilot / risk / style）」中的 pilot 段。
