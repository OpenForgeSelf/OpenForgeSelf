# Evidence

> Task ID：`2026-11-19-oar-remaining-close`

## Acceptance Criteria → Evidence

| AC | 结果 | 证据 |
| --- | --- | --- |
| AC1 Auth + 401 | ✅ Verified | `AgentHubAuthTests`；`oar-todo-verify/v11-agents-noauth.json` / `v11-tasks-noauth.json` |
| AC2 install 闸门 | ✅ Verified | `api-install-unconfirmed.json` HTTP 400；`api-install-confirmed.json` HTTP 200；`w3-install-confirm.png` |
| AC3 Session UI | ✅ Verified | `http.ts` 客户端；`w1b-session-detail.png` / `w4-session-input.png` / `w5-session-records.png`；`v4-*.json` |
| AC4 Runtime probe/models UI | ✅ Verified | `w2-runtime-probe.png`（installation/auth 分列）；`w2b-runtime-models.png` |
| AC5 Steer 能力禁用 | ✅ Verified | Runtime caps 展示 + `canSteer`/`canAbort`；scripted 含 session 时可发 |
| AC6 V1–V8/V11–V12 | ✅ Verified | `oar-todo-verify/` + 单测过滤集 17 Passed；V9/V10 ⏸ |
| AC7 W1–W8 | ✅ Verified | `oar-todo-walkthrough/INDEX.md`；W9/W10 ⏸ |
| AC8 文档/TODO | ✅ Verified | 032 / 架构 / gaps / TODO ⏸ 队列；本目录 00–07 |

## Commands Run (Verified)

| 命令 | 结果 |
| --- | --- |
| `dotnet build Plugins/AgentHub` | 0 Error |
| `cd RuntimeWorker && pnpm run build` | tsc 绿 |
| `cd Plugins/AgentHub/web && pnpm run build` | vite 绿 |
| `dotnet test --filter AgentHubAuth\|SessionKeyAsk\|…` | **17 Passed** |
| `dotnet test --filter FullyQualifiedName~AgentHub` | 106 Passed / 4 Failed（基线 Linux：ProbeSettings Windows 路径 + CliTransport powershell；非本批引入） |
| HTTP probes → `/opt/cursor/artifacts/oar-todo-verify/` | V1–V5/V11 + install 闸门 |
| Playwright walkthrough → `/opt/cursor/artifacts/oar-todo-walkthrough/` | W0–W8 截图 |
| `pwsh scripts/dev-stack.ps1` | ❌ Linux `Start-Process -WindowStyle`（D11 ⏸）；改用 net10.0 手工起 :7301+:7399 |

## Functional Evidence

| ID | 等级 | 证据 |
| --- | --- | --- |
| F1 | Verified | AuthTests + `v11-agents-noauth.json` HTTP 401 / auth 200 |
| F2 | Verified | `api-install-unconfirmed.json` 400；`api-install-confirmed.json` 200；`w3-install-confirm.png` |
| F3 | Verified | http.ts 客户端 + `w1b`/`w4`/`w5`；`v4-input/close` JSON |
| F4 | Verified | `w2-runtime-probe.png` installation/auth 分列；`w2b` models |
| F5 | Verified | Runtime caps 展示；Steer/Abort 按 caps 禁用逻辑（scripted 含 session→可发） |
| V1 | Verified | `v1-abort.json` / `v1-abort-404.json` |
| V2 | Verified | `v2-records.json` |
| V3 | Verified | `v3-stream.sse` |
| V4 | Verified | `v4-get-session.json` / `v4-input.json` / `v4-close.json` |
| V5 | Verified | `v5-runtime-detail.json` |
| V6 | Verified | Provider Watch/SendInput/Close 单测 |
| V7 | Verified | EventStore `prompt路径` 测 |
| V8 | Verified | `AgentHubSessionKeyAskTests` |
| V11 | Verified | agents/tasks 401 |
| V12 | Verified | AgentHub 过滤集跑过（见上；基线 4 红） |
| V9/V10 | ⏸ / 部分 ✅ | V9 仍 ⏸（无 ForgeSelf.exe）；V10 **Grok Build 真会话已 Verified**（见下节）；Claude/Codex/OpenCode 仍 ⏸ |
| W1–W8 | Verified | walkthrough INDEX |
| W9–W10 | ⏸ | 无用户 publish 实例 |

## Known Limitations

- scripted installPlan=`not_supported`（预期）。
- 短会话很快 Closed；Steer 仍可点但 Worker 可能拒。
- 正式 playwright globalSetup exe 未跑（V9）。
- Host `OarTransport` 在 prompt *accepted* 后约 50ms 收束 → 任务可标 Succeeded，但 EventStore 可能尚未收到 vendor `text_delta`；真 Grok 助手正文以 Worker 等待 `turn_ended` 为准（见下节）。

## OAR×Grok 真实验收（2026-11-19 追加 · Verified）

> 诚实结论：本 pilot 原先 W1–W8 / `oar-todo-walkthrough` **是真宿主 UI**，但 Session/prompt 走的是 **`scriptedRuntime`（echo）**，不是 vendor Grok。完整溯源见 Cursor artifacts `oar-screenshot-provenance.md`。

| 项 | 结果 | 证据（Cursor artifacts `oar-grok-acceptance/`） |
| --- | --- | --- |
| Grok 二进制 | ✅ `grok 1.0.89` stable；OAR `installation.via=executable` | `g0-worker-probe.log` / `g27-grok-probe.png` |
| Auth / Models | ✅ `logged_in`（grok.com）；models 含 grok-4.7 等 | `g0-api-probe.json` / `g0-api-models.json` / `g28-grok-models.png` |
| Worker 全流程 | ✅ open→prompt→`text_delta=GROK_ACCEPTANCE_OK`→records→dispose | `g0d-worker-grok-session.json` |
| AccessPoint | ✅ Transport=`Oar` RuntimeId=`grok`（agent `grok-build-live`） | `g0c-create-agent.json` |
| UI 可点击清单 G0–G44 | ✅ **48/48 pass**（零 scripted 作验收 prompt） | `REPORT.md` / `results-merged.json` / `g01`–`g41` PNG |
| 起源码栈 | ✅ `:7301` / `:7399`（未动 `:51888`） | `g43` |

Steer UI：新登记 Oar agent 若未写 `F1_Driving`/`F2_Sessions`，caps 仅 `abort,oar` → Steer 禁用；本批已补 facets 后 `g35-session-steer.png` 通过。
