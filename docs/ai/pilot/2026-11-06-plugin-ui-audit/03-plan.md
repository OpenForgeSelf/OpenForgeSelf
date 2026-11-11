# Plan

> 阶段：Stage 3｜Task ID：PILOT-plugin-ui-audit｜只出工件，不执行修复。

## Files To Change（后续执行时；本批不动）

| 批次 | 文件 | 动作 |
|---|---|---|
| B1 | `ForgeSelf.Web/e2e/plugins/costscope/costscope.spec.ts`（新建） | 补 CostScope 冒烟 e2e（主链路 + 运行态断言） |
| B1 | `Plugins/<X>/web/src/*.vue` | 按 C2 实测结果，把不合规的配对改为 token 组合（逐插件小批） |
| B2 | `ForgeSelf.Web/src/components/<X>.story.ts` + `.ct.spec.ts`（新建） | 为关键状态补 CT story + 行为断言（非仅像素） |
| B2 | `Plugins/<X>/docs/ui-design-brief.md` | 五态（含表加载）与亮/暗实测数值回写 |
| B3 | `docs/11-troubleshooting/` | 逐插件发现的问题按 `problem-class-closure` 归档（同根因并入 PC-025） |

## Implementation Steps

**B0 前置（阻塞项，需闸门1）**：`2026-11-06-design-gate-contrast` 的规则 E/F 落地——否则本批每插件的可读性只能靠人工实测，无法沉淀为守卫。此依赖已在另一工件（00–04 已出）。

**B1 P1（可读性 + 缺失 e2e）**
1. CostScope：先核 C3/C4，再补 `e2e/plugins/costscope` 1 条主链路（含运行态 loading 断言）。
2. DbAdmin：C2 已达标；补 C4 的「重试」路径（当前仅有错误横幅）。
3. 高裸色值插件按风险降序：AIAgent(397) → Sems(178) → DesignSystem(108) → AgentHub(104) → Home(61) → ImGateway(46) → CostScope(31) → FileTools(14) → McpCenter(13)。每批 1–2 个，逐批 `pnpm run design:check` + 该插件 build。
4. 每个插件执行 C2 实测（`dev-stack.ps1` 起源码 → 亮/暗各截图 → `getComputedStyle` 记录数值）。

**B2 P2（状态与基线）**
5. 为 C3/C4 缺失的插件补局部 loading（模式复用 DbAdmin 的 `tablesLoading + 序号防竞态`）。
6. 按使用频次补 CT：AgentHub、AIAgent、Home、Sems（每插件 ≥1 关键状态 story + 行为断言）。
7. 回写各插件 `ui-design-brief.md`：五态 + 亮暗数值。

**B3 收口**
8. `problem-class-closure`：同根因（背景级 token 当文字色 / 缺 loading）并入 PC-025 与 PC-027（新建：异步无反馈），跑 `check-problem-classes.ps1`。
9. 规范回写：`plugin-development` §四 加「逐插件核对表」引用。

## Test Plan

- 每插件：`cd Plugins/<X>/web && pnpm run build` + `pnpm run design:check`。
- 有 CT 的插件：`pnpm exec playwright test -c playwright-ct.config.ts src/components/<X>.ct.spec.ts`。
- 有 e2e 的插件：`pnpm run test:e2e -- e2e/plugins/<id>`（干净启动，避免复用旧 Vite）。
- 全批收口：`pnpm run check` + `pnpm run test` + `pwsh scripts/check-problem-classes.ps1`。

## Verification

| 项 | 命令 | 判定 |
|---|---|---|
| 工件完整性 | `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/verify-pilot-artifacts.ps1 -TaskId 2026-11-06-plugin-ui-audit -EnforceHeader -EnforceAcCoverage -EnforceInteraction` | PASS |
| 本批未改业务文件 | `git status --short` | 仅本目录新增 |
| 覆盖度 | 人工对表 14 插件 | 无遗漏 |

## Plan 偏差记录

- 2026-11-06：用户要求「逐个插件核对排查修复，只出工件先不执行」⇒ 03 仅为执行计划，未落地任何 B1–B3 动作；C2–C5/C8 保持 `Unknown(未核)`。
- 2026-11-06：B0 依赖的 `design-gate-contrast` 尚未获批实施，本批不假设其存在。
