# Evidence — OAR A-bucket honesty（#22/#28/#25/#78）

- **日期**：2026-11-20
- **分支**：`cursor/oar-a-bucket-honesty-3d6d`
- **闸门1**：父代理 A-bucket 明文授权

## Changed Files

| 文件 | 变更 |
| --- | --- |
| `docs/02-features/032-agent-hub.md` | §6.2 Watch/stream = 轮询最小面（诚实） |
| `docs/ai/pilot/2026-11-19-oar-agenthub-runtime/02-spec.md` | AC-R3 诚实标注强化 |
| `ForgeSelf.Api.Tests/.../AgentHubRuntimeProviderTests.cs` | AC-R3 poll/Cancel/续接 + 源码 Delay 守卫 |
| `ForgeSelf.Api.Tests/.../AgentHubInterruptFixtureTests.cs` | #25 Interrupted ≠ Succeeded；resume ≠ revive |
| `Plugins/AgentHub/web/src/probeAllRuntimes.ts` (+ spec + vitest) | 并发池纯函数 |
| `Plugins/AgentHub/web/src/AgentHubView.vue` | 复用 mapPool / formatProbeAllMessage |
| `ForgeSelf.Web/e2e/plugins/agent-hub/agent-hub.spec.ts` | #78 一键探测 e2e |

**未改（sibling 范围）**：`OarTransport` 50ms、`handlers.ts` bundled。

## Verification

| ID | Command | Result | Grade |
| --- | --- | --- | --- |
| V1 | `dotnet test … --filter AgentHubInterruptFixtureTests\|AgentHubRuntimeProviderTests` | **Passed 15/15**；日志 `/opt/cursor/artifacts/oar-a-bucket-honesty/dotnet-agenthub-2.log` | Verified |
| V2 | `cd Plugins/AgentHub/web && pnpm run test` | **4/4** probeAllRuntimes | Verified |
| V3 | `pnpm run build`（插件 web） | PASS；`dist/index.js` | Verified |
| V4 | Playwright `agent-hub.spec.ts` -g 一键探测 | **1 passed**；`probeHits=13`；refresh≠probe；日志+截图 artifacts | Verified |

## Screenshots / logs

- `/opt/cursor/artifacts/oar-a-bucket-honesty/e2e-probe-all.log`
- `/opt/cursor/artifacts/oar-a-bucket-honesty/agent-hub-probe-all.png`（若复制成功）
- `/opt/cursor/artifacts/oar-a-bucket-honesty/agent-hub-probe-all.log`

## Best-effort / Known Limitations

- **#54 partial-fail 截图**：本环境一键探测 13/13 全成功，未注入失败 Runtime；未捕获「m 失败」聚合 UI。记 TODO/D 侧可选。
- **#73 bundled badge**：未单独采 close-up；产品面 `installBadge(via=bundled)` 仍在 `AgentHubView.vue`（Inferred from code）。
- **#63 SSE**：明确不实现（C deferred）。
- Sibling `oar-prod-honesty`（#20/#39/#50/#70）未合入本 PR。

## Unresolved

无阻塞本 A-bucket 交付项。
