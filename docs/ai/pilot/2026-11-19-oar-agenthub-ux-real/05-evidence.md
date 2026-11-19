# Evidence

> Task：oar-agenthub-ux-real｜2026-11-19

## Verification matrix

| AC | Result | Evidence |
| --- | --- | --- |
| AC1 审计 Grok-only | ✅ Verified | `/opt/cursor/artifacts/oar-real-vs-simulated.md`；installation 探测 grok=executable，claude/codex/opencode=not_found |
| AC2 无 scripted 产品面 | ✅ Verified | API `runtimes` `has_scripted=false`；UI Runtime 库存文案声明剔除；Agent 目录过滤 scripted AP |
| AC3 安装态诚实 | ✅ Verified | grok Available+via=executable；claude not_found；pi bundled 不标已装；`grok-probe.json` auth=logged_in |
| AC4 品牌图标 | ✅ Verified | `RuntimeBrandIcon` + OAR brands SVG 内联；截图 Runtime 卡互异图标 |
| AC5 UX 分区 | ✅ Verified | Runtime 库存 / Session 操作两节；探针 JSON 默认折叠；截图 `oar-ux-real/02-*` `03-*` |
| AC6 build/smoke | ✅ Verified | web build 绿；Worker smoke 12/12；Provider 测试含「不含 scripted」 |

## Commands (Verified)

- `pnpm run build` @ `Plugins/AgentHub/web` → ok
- `pnpm run smoke` @ `RuntimeWorker` → 12/12
- `dotnet test --filter AgentHubRuntimeProvider` → Pass
- `pnpm run design:check` → PASS
- API probe grok → available/executable + logged_in
- Screenshots → `/opt/cursor/artifacts/oar-ux-real/`

## Notes

- dig-stack 曾因 Linux `-WindowStyle` 拒启；本批合入 `scripts/dev-stack.ps1` 条件化（与 fa93 同修）。
- 本机 DB 曾有 `remaining-scripted` 登记；已 DELETE；UI 另加过滤。
