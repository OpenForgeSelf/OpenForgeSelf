# Evidence — 2026-11-20-oar-prod-honesty

## Commands (Verified)

| 命令 | 结果 | 日志 |
|------|------|------|
| `Plugins/AgentHub/RuntimeWorker pnpm build` | PASS | worktree |
| `pnpm run smoke` | 12/12 ok | `/opt/cursor/artifacts/oar-prod-honesty-worker-smoke.log` |
| `Plugins/AgentHub dotnet build` | 0 Error | `/opt/cursor/artifacts/oar-prod-honesty-agenthub-build.log` |
| `dotnet test` filter Oar*/ListAgents scripted | **17 Passed / 0 Failed** | `/opt/cursor/artifacts/oar-prod-honesty-dotnet-test.log` |

## Before / After

| 行为 | Before | After |
|------|--------|-------|
| prompt accept 后 | `Delay(50)` → Exit(0) | `WaitUntilSettledAsync`（status idle+turn 证据 / `turn_ended`）+ timeout/cancel |
| `!rt.installation` | `{available, via:bundled}` | `{kind:not_found}` / requireAvailable 抛 not_found |
| 产品 ListAgents | omit scripted | 仍 omit（测试绿） |

## Source grades

- Turn settle unit + hold 集成：Verified
- handlers honesty G2：Verified
- Worker smoke：Verified
