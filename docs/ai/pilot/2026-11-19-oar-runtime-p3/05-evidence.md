# Evidence

> Task ID：`2026-11-19-oar-runtime-p3`

## Changed Files

| 路径 | 动作 |
| --- | --- |
| `Plugins/AgentHub/Services/AgentHubRuntimeProvider.cs` | 新增 IAgentRuntime 提供方 |
| `Plugins/AgentHub/Services/OarTransport.cs` | 改共享 OarWorkerClient |
| `Plugins/AgentHub/AgentHubPlugin.cs` | DI + ctx.Register + Effect 清理 |
| `Plugins/AgentHub/Controllers/AgentHubRuntimesController.cs` | 新增 |
| `Plugins/AgentHub/Controllers/AgentHubSessionsController.cs` | 新增 |
| `Plugins/AgentHub/Services/DelegationRuntime.cs` | SessionKey 别名 |
| `Plugins/AgentHub/Tools/AgentHubTools.cs` | SessionKey 面 |
| `Plugins/AgentHub/web/**` | Session 页签 + 委派 SessionKey |
| `ForgeSelf.Api.Tests/.../AgentHubRuntimeProviderTests.cs` | 新增 |
| `docs/02-features/032-agent-hub.md` / 架构 / concurrency-plan | P3 进度 |
| `docs/ai/pilot/2026-11-19-oar-runtime-p3/*` | 工件链 |

## Commands Run

| # | 命令 | 结果 | 等级 |
| --- | --- | --- | --- |
| 1 | `dotnet build Plugins/AgentHub` | 0 Error | Verified |
| 2 | filter AgentHubRuntime\|Transport\|Session\|Routing\|Delegation\|OarWorker\|AgentRuntime | **34 Passed** | Verified |
| 3 | RuntimeWorker `pnpm run smoke` | 12/12 | Verified |
| 4 | `Plugins/AgentHub/web && pnpm run build` | vite OK | Verified |

日志：`/opt/cursor/artifacts/p3-dotnet-test3.log`、`p3-worker-smoke.log`、`p3-web-build.log`

## AC 对照

| AC | 结果 | 证据 |
| --- | --- | --- |
| AC1 | ✅ | 同实例测绿 |
| AC2 | ✅ | List/Submit/Cancel Provider 测绿 |
| AC3 | ✅ | 过滤 34/34（含既有 Delegation/Transport） |
| AC4 | ✅ | 032/架构 + 05–07 |

## Known Limitations

- WatchTaskAsync 为轮询最小面（SSE 仍走 tasks 控制器）。  
- SessionKey 仍可与 Worker key 对齐（P2 策略）；独立 GUID 映射未做。  
- P3 e2e 冒烟未跑（本批按用户指令以 filtered tests + Worker smoke 为准；P4/环境可补）。  
- P4 消费者试点 / Node≥24 打包说明未开。
