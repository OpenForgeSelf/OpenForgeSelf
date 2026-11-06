# Evidence — db-admin 表名与结果表对齐修复

> 阶段：Stage 7｜Task ID：`2026-11-06-db-admin-table-alignment`｜日期：2026-11-06
> 来源等级：Verified（亲自跑过/真实测量）/ Inferred（凭代码推断）/ Unknown（未验证）。

## Task

`2026-11-06-db-admin-table-alignment`

## Changed Files

- `Plugins/DbAdmin/web/src/DbAdminView.vue`：表名渲染去「表」前缀；结果表 `grid` → `dba-grid`。
- `ForgeSelf.Web/e2e/plugins/db-admin/db-admin.spec.ts`：表名/display/列对齐断言 + 批准插入 1 行。
- `ForgeSelf.Web/src/styles/pluginTableClassGuard.test.ts`（新）：G2 静态守卫。
- `Plugins/DbAdmin/docs/02-交互说明.md`：布局示意同步。
- `docs/11-troubleshooting/problem-classes/PC-026-plugin-class-tailwind-collision.md`（新）。
- `docs/11-troubleshooting/cases/2026-11-06-dbadmin-table-header-misalign.md`（新）。
- `docs/11-troubleshooting/problem-classes/README.md`、`cases/README.md`、`index.md`（索引）。
- 本 pilot 工件目录 00-07。

## Reproduction（红灯）

### 运行实例 DOM 实测（Verified）

在 `:51888`（db-admin v0.1.2）对 ForgeSelf 系统库执行 `SELECT name, type, sql FROM sqlite_master LIMIT 20`：

```text
table.grid 数量 = 1
computed display = grid（非 table）
thead th left = 282.80 / 318.80 / 410.00 / 501.20
tbody 第一行 td left = 282.80 / 318.80 / 598.80 / 650.80
列 left 差值：name 0；type ≈ -188.8；sql ≈ -149.6
```

截图：`c:\Users\12504\AppData\Local\Temp\trae\screenshots\db-admin-result.png`（读图确认表头 `name String` 与数据 `AIProvider` 不同列起点）。

### 仓库 e2e 红灯（Verified）

1. 表名断言：`.temp/tmp/dbadmin-ui-red.log` → `locator resolved to <span class="name">表 e2e_items</span>`，`toHaveText('e2e_items')` 失败。
2. display 断言：`.temp/tmp/dbadmin-ui-red2.log` → `expect(getComputedStyle(table).display).toBe('table')`，Received: `"grid"`。

## Fix Verification（绿灯）

### 插件 web

```bash
cd Plugins/DbAdmin/web && pnpm run test && pnpm run build
```

Result: PASS（Verified）

```text
Test Files 9 passed (9)；Tests 61 passed (61)；WEB_TEST_EXIT=0
build: dist/index.js 68.65 kB / dist/style.css 12.43 kB；BUILD_EXIT=0
```

### 插件冒烟 e2e（真实宿主 CSS）

```bash
cd ForgeSelf.Web && pnpm exec playwright test --config=playwright.config.ts e2e/plugins/db-admin
```

Result: PASS（Verified，最终一轮；此前一轮因插入未落库被判 flaky，已加 `result-stats` 等待）

```text
1 passed (1.4m)（.temp/tmp/dbadmin-ui-green3.log）
断言内含：表名 .name === 'e2e_items'；display === 'table'；thead th 与 tbody td 左边界差 ≤1px
截图：ForgeSelf.Web/screenshots/e2e/db-admin/04-schema.png（表名无「表」）、05-select.png（有 1 行数据且列对齐）
```

### 宿主前端

```bash
cd ForgeSelf.Web && pnpm run check && pnpm run test && pnpm run design:check
```

Result: PASS（Verified）

```text
check：0 errors（166 warnings 既有）；CHECK_EXIT=0
test：Test Files 108 passed (108)；Tests 1160 passed (1160)；HOST_TEST_EXIT=0
design:check：PASS（无新增硬编码色值/规范违反）
```

### G2 守卫（含 mutation 验证）

```bash
cd ForgeSelf.Web && pnpm exec vitest run src/styles/pluginTableClassGuard.test.ts
```

Result: PASS（Verified）

```text
还原后：GUARD_EXIT=0
mutation（把 dba-grid 改回 grid）：GUARD_MUTATION_EXIT=1（守卫转红）
再次还原：守卫绿
```

### 问题类台账

```bash
pwsh scripts/check-problem-classes.ps1          # 全量
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj --filter "FullyQualifiedName~ProblemClassRegistryTests"
```

Result: PASS（Verified）

```text
check-problem-classes OK：26 个 PC 签名/allowlist/guards 全部通过（PC-026 scan 完成）
ProblemClassRegistryTests：PC_TEST_EXIT=0（PC-026↔case↔三处 README 闭合）
```

## Integration Test

N/A：本批未改后端契约、实体、DB；DbAdmin 后端 167 项既有测试不在本批改动面（仅前端模板/CSS/断言/台账）。

## Screenshots

- `ForgeSelf.Web/screenshots/e2e/db-admin/04-schema.png`：左栏表名 `e2e_items`（无「表」前缀）。
- `ForgeSelf.Web/screenshots/e2e/db-admin/05-select.png`：结果表 1 行数据，表头 `# / id Int64 / name String` 与行 `1 / 1 / e2e_row` 对齐。
- 运行实例只读复验截图 `c:\Users\12504\AppData\Local\Temp\trae\screenshots\db-admin-result.png`（修复前错误态证据）。

## Acceptance Criteria 对照

- AC1：e2e `.name` === `e2e_items`（Verified）；`04-schema.png` 读图（Verified）。
- AC2：e2e `display === 'table'` + 列左边界差 ≤1px（Verified）；`05-select.png` 读图（Verified）。
- AC3：空结果表头仍显示（e2e SELECT 步骤；既有行为未改，Verified by e2e）。
- AC4：插件 web 61/61 + build；宿主 1160/1160 + check 0 error + design:check PASS（Verified）。
- AC5：G2 mutation 红→绿；`check-problem-classes` 26 PC OK；Registry 测试绿（Verified）。
- AC6：运行实例 `:51888` 仍为旧插件 0.1.2，修复未发布；发布后需按 `plugin-publish-verify` 做只读复验（Unknown→待发布）。

## Known Limitations

- 本批未发布到 `:51888`（发布需用户授权；用户当前看到的仍是旧插件 0.1.2）。
- 并行会话在 10:03 打的本地 zip 早于本批修复（10:1x 完成），**不含本次表名/对齐修复**；需重新打包后才可见（打包/发布须用户授权）。
- e2e 首轮曾因 SELECT 抢跑（插入审批未落库）判 flaky，已通过等待 `result-stats` 修掉，最终一轮 1 passed。
- G2 守卫只覆盖 `<table>` 的静态 class；`:class` 动态拼接与非 display 工具类不在覆盖范围（PC-026 已知盲区）。

## Unresolved Issues

无（本任务范围内）。
