# Plan

> 阶段：Stage 3｜Task ID：PILOT-design-gate-contrast｜日期 2026-11-06

## Files To Change

| 文件 | 动作 |
|---|---|
| `scripts/check-design-compliance.mjs` | 新增规则 E（背景级 token 当文字色）+ baseline 增 `e` 列 |
| `scripts/design-compliance-baseline.json` | `--update` 重建（`e` = 0） |
| `scripts/check-theme-contrast.mjs` | 新建：规则 F（主题暗色 ramp 与关键配对对比度） |
| `ForgeSelf.Web/package.json` | `design:check` 链追加 `node ../scripts/check-theme-contrast.mjs` |
| `scripts/hooks/pre-commit` | 第 69 行链改为跑同一 `design:check`（三脚本），保持单入口 |
| `.github/workflows/frontend-check.yml` | `pnpm run check` 之后加 `pnpm run design:check` |
| `AGENTS.md` §8 | 第 3 步压成三步门禁 + 引用，删细节长句 |
| `DESIGN.md` §门禁化说明 | 加一行脚本入口（E/F 与 F 的判据） |
| `docs/ai/pilot/2026-11-06-design-gate-contrast/` | 05/06/07 |

## Implementation Steps

1. 在 `check-design-compliance.mjs` 增规则 E：正则匹配 `color:`（排除 `border-color`/`background-color`/`outline-color`/`-webkit-text-fill-color` 等以非 `{`/`;`/空白 前导的形式）绑定 `--el-color-*-light-{5..9}`；计数进 `result.e`，与 A–D 同构落基线。
2. 新建 `scripts/check-theme-contrast.mjs`：读取 `ForgeSelf.Web/src/styles/themes/*.css`，抽取 `:root` 与 `html.dark` 块内的 `--el-color-*` / `--el-text-color-primary` / `--el-bg-color`；实现 WCAG 相对亮度 + 对比度；按 02-spec 规则 F 断言；缺失 token 打 `SKIP`。
3. `package.json` 与 `pre-commit` 接线（保持 `design:check` 为唯一入口，pre-commit 不再硬编码两脚本）。
4. CI 增加一步 `pnpm run design:check`。
5. `AGENTS.md` §8 瘦身；`DESIGN.md` 门禁表补入口。
6. 跑验证 + 红灯实验，补 05-07。

## Test Plan

- **绿灯**：现状仓库 `pnpm run design:check` PASS（`e=0`，F 全通过）。
- **红灯 1（E）**：临时在 `Plugins/DbAdmin/web/src/DbAdminView.vue` 写 `color: var(--el-color-primary-light-7)` → `design:check` 必红；还原。
- **红灯 2（F）**：临时把暗色 `--el-color-primary-light-8` 改回 `#FDE68A` → 必红并报 ~1.1:1；还原。
- **不回归**：`pnpm run check`、`pnpm run test` 保持绿；CT 不受影响（本批不动组件）。

## Verification

| 项 | 命令 | 判定 |
|---|---|---|
| 设计门禁 | `cd ForgeSelf.Web && pnpm run design:check` | PASS，日志含 `[theme-contrast]` |
| 前端快档 | `cd ForgeSelf.Web && pnpm run check && pnpm run test` | 0 error / 全绿 |
| 中档（碰 `scripts/**`） | `cd ForgeSelf.Api.Tests && dotnet test` | 与基线红对表，新增红=本批引入 |
| hook 生效 | `grep -n "design" scripts/hooks/pre-commit` | 三脚本/单入口 |
| CI 生效 | `grep -n "design:check" .github/workflows/frontend-check.yml` | 存在该步骤 |
| 工件 | `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/verify-pilot-artifacts.ps1 -TaskId 2026-11-06-design-gate-contrast -EnforceHeader -EnforceAcCoverage -EnforceInteraction` | PASS |

## Plan 偏差记录

（执行时逐条追加：原计划 → 实际 → 原因）
