# Evidence

> Task ID：`2026-11-19-oar-runtime-p4`

## Changed Files

| 路径 | 动作 |
| --- | --- |
| `ForgeSelf.Api.Tests/Plugins/AgentHub/AgentHubRuntimeConsumerPilotTests.cs` | 新增消费者试点 + Session 守卫 |
| `docs/01-architecture/agenthub-oar-runtime.md` | §7.1 映射注；§11 P4 ✅；§13 Node≥24 |
| `Plugins/AgentHub/RuntimeWorker/README.md` | 运维/CI 表 |
| `docs/02-features/032-agent-hub.md` | §6 P4 |
| `docs/ai/pilot/2026-11-19-oar-agenthub-runtime/concurrency-plan.md` | P0–P4 ✅ |
| `docs/01-architecture/host-capability-seams.md` | 进度行 |
| `docs/07-decisions/007-oar-agenthub-runtime.md` | 后果：P0–P4 已落地 |
| `docs/ai/pilot/2026-11-19-oar-runtime-p4/*` | 工件链 |

## Commands Run

| # | 命令 | 结果 | 等级 |
| --- | --- | --- | --- |
| 1 | `dotnet build Plugins/AgentHub` | 0 Error | Verified |
| 2 | filter AgentHubRuntime\|Transport\|Session\|Routing\|Delegation\|OarWorker\|AgentRuntime\|ConsumerPilot | **39 Passed** | Verified |
| 3 | RuntimeWorker `pnpm run smoke` | 12/12 | Verified |

日志：环境 artifacts 下 `p4-dotnet-test.log` / `p4-worker-smoke.log` / `p4-build.log`（不入库）。

## AC 对照

| AC | 结果 | 证据 |
| --- | --- | --- |
| AC1 | ✅ | Consumer Get/降级/Cli fallback 测绿（ConsumerPilot） |
| AC2 | ✅ | SessionDto 无 OarSessionId；SessionKey≠OarSessionId 库内可解耦守卫绿 |
| AC3 | ✅ | 过滤 39/39（含既有 Runtime/Transport/Delegation） |
| AC4 | ✅ | Worker smoke 12/12 |
| AC5 | ✅ | 032/架构/concurrency/seams P4 ✅；05–07 齐 |

## Known Limitations

- 未跑全量 `dotnet test` / 全量 e2e（未碰宿主脚本与 e2e 基建）。  
- 未改 AgentHub `web/` → 未跑前端 check。  
- SessionKey 独立 GUID 映射表未落地（仅文档 + 守卫）。  
- ManagePullRequest 工具不可用 → PR 草稿落环境 artifacts。
