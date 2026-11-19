# Evidence

> Task ID：`2026-11-19-oar-runtime-gap-close`

## Commands（Verified）

| 命令 | 结果 | 证据 |
| --- | --- | --- |
| `dotnet build Plugins/AgentHub/AgentHub.csproj -p:EnableWindowsTargeting=true` | 0 Error | `/opt/cursor/artifacts/gap-close-build.log` |
| `dotnet test … --filter "…AgentHubRuntime\|…Session\|…Transport\|…OarWorker\|…Delegation\|…Event\|…Permission\|…Registry"` | **Passed 86 / Failed 0** | `gap-close-dotnet-wide.log` |
| `dotnet test … --filter "…EventStore\|…SessionManager\|…RuntimeProvider\|…Consumer\|…OarWorker\|…TransportRouting"` | **Passed 18 / Failed 0** | `gap-close-dotnet-core2.log` |
| `cd Plugins/AgentHub/web && pnpm run build` | ✓ dist | `gap-close-web-build.log` |
| HTTP：GET runtimes / sessions；POST probe；GET models；无 token → 401 | Verified | `gap-close-api-*.json` |
| Playwright Session 冒烟（grep Session 页签；baseURL `:7499`） | **1 passed** | `gap-close-e2e-session.log` + `gap-close-session-tab.png` |

## AC 对照

| AC | 等级 |
| --- | --- |
| AC1 EventStore 单调序列 | ✅ Verified（`AgentHubRuntimeEventStoreTests`） |
| AC2 五端点 + ApiKeyPolicy | ✅ Verified（HTTP；未知 session abort/records=404；无 token=401） |
| AC3 既有过滤不新增红 | ✅ Verified（86 Passed 宽过滤；ProbeSettings/CliTimeout 等 Linux 基线红不在本过滤） |
| AC4 web build + Session e2e | ✅ Verified（build + 1 passed；正式 `playwright.config` globalSetup 需 ForgeSelf.exe，本 Linux 环境用 live-like 栈跑同一条入库用例） |
| AC5 文档/audit | ✅ 本批写回 |

## Known Limitations

- 正式 e2e globalSetup 仍 spawn `ForgeSelf.exe`（Windows）；本环境用 origin 验证栈 + 同一 `agent-hub.spec.ts` Session 用例。  
- `dev-stack.ps1` `-WindowStyle` 在 Linux pwsh 仍失败 → 属 PR#7，本批未改。  
- 真实 Claude/Codex/OpenCode 会话仍 out of scope。  
- WatchTaskAsync / session stream 仍为轮询最小面（非统一 SSE 重写）。
