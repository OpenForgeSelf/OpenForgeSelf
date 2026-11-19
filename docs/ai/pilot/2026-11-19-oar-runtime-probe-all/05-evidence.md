# Evidence — Runtime 库存「一键探测」

- **日期**：2026-11-19
- **分支**：`cursor/oar-runtime-probe-all-3d6d`
- **来源等级约定**：Verified = 亲自跑过；Inferred = 代码推断；Unknown = 未验证

## Changed Files

| 文件 | 变更 |
| --- | --- |
| `Plugins/AgentHub/web/src/AgentHubView.vue` | `runProbeAllRuntimes`（并发 3）+ 按钮/卡进度 UI |
| `docs/02-features/032-agent-hub.md` | §6.1 探测粒度回写 |
| `Plugins/AgentHub/docs/ui-design-brief.md` | 交互表增加一键探测 |
| `docs/ai/pilot/2026-11-19-oar-runtime-probe-all/*` | mini-task + 05/06/07 |

## Verification

| # | Command / 动作 | Result | 等级 |
| --- | --- | --- | --- |
| V1 | `cd Plugins/AgentHub/web && pnpm run build` | PASS，`dist/index.js` 含「一键探测」；日志 `/opt/cursor/artifacts/agenthub-web-build.log` | Verified |
| V2 | 宿主已起 `ForgeSelf.dll` `:7305` + FE `:7410`；打开 `/agent-hub` → Runtime / Session | 「一键探测」主按钮可见；与「刷新」分离 | Verified |
| V3 | 点击「一键探测」 | 横幅「一键探测完成：13/13 全部成功」；卡徽标「已探测」；hint「上次：13 成功」 | Verified |
| V4 | `POST …/runtimes/grok/probe`（Bearer） | installation `via=executable` + auth `logged_in` | Verified |
| V5 | 正式插件 e2e `e2e/plugins/agent-hub` | 本环境未跑全量 e2e（Cloud Linux / 时间）；功能以 V2–V3 真机走查替代 | Unknown（阻塞注明） |

## Screenshots

- `/opt/cursor/artifacts/oar-runtime-probe-all/01-runtime-inventory-probe-all-btn.png`
- `/opt/cursor/artifacts/oar-runtime-probe-all/02-runtime-probe-all-after.png`

## Known Limitations

- 无新 batch API；前端循环既有 per-id probe。
- Agent 卡仍仅单条「探测」（本批范围外）。
- 正式 Playwright 插件冒烟 e2e 未在本回合新增用例（既有 session 冒烟仍覆盖库存区存在性；一键按钮断言可后续补）。

## Unresolved Issues

无阻塞实现的未决项。合并主分支取决于栈上 PR CI/权限。
