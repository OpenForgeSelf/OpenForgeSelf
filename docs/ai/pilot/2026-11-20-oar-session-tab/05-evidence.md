# 05 Evidence — oar-session-tab

| 项 | 等级 | 证据 |
| --- | --- | --- |
| AgentHub web build | Verified | `/opt/cursor/artifacts/oar-session-tab-build.log` 0 error |
| 顶栏五页签含「Runtime 检测」「会话」 | Verified | `assert.json` tabs；截图 |
| Runtime 页：一键探测可见、网格可见 | Verified | `rtProbeVis/rtGridVis=true`；`01-runtime-tab.png` |
| 会话页：一键探测隐藏、网格隐藏 | Verified | `sessProbeVis/sessGridVis=false`；`02-sessions-tab.png` |
| 正式 e2e 全量 | Unknown | 本回合以 Playwright 走查断言；CI 跑 agent-hub spec |

截图目录：`/opt/cursor/artifacts/oar-session-tab/`
