# Evidence

> 阶段：Stage 7｜**只记录实际发生的事情**，不得根据代码推测测试结果。
> 每个验证项标注来源等级：Verified（亲自跑过，有真实输出）/ Inferred（凭代码推断）/ Unknown（未验证）。禁止混用。

## Task

`2026-11-06-db-admin-query-tabs`（044-db-admin 多标签查询工作台）

## Changed Files

新增（插件前端纯逻辑 + 单测）：

- `Plugins/DbAdmin/web/src/tabs.ts` / `tabs.test.ts`
- `Plugins/DbAdmin/web/src/sqlSplit.ts` / `sqlSplit.test.ts`
- `Plugins/DbAdmin/web/src/results.ts` / `results.test.ts`
- `Plugins/DbAdmin/web/src/exportCsv.ts` / `exportCsv.test.ts`

修改：

- `Plugins/DbAdmin/web/src/DbAdminView.vue`
- `Plugins/DbAdmin/web/src/types.ts` / `connForm.ts` / `connForm.test.ts` / `restore.ts` / `restore.test.ts`
- `ForgeSelf.Web/src/components/DbAdmin.story.ts` / `DbAdmin.ct.spec.ts`
- `ForgeSelf.Web/e2e/plugins/db-admin/db-admin.spec.ts`
- `Plugins/DbAdmin/plugin.json`（0.1.4 → 0.2.0）
- `Plugins/DbAdmin/docs/ui-design-brief.md`
- `docs/02-features/044-db-admin.md`
- `docs/07-decisions/not-taken-decisions.md`（056 · 不纳入清单）

## Build

Command:

```bash
cd Plugins/DbAdmin/web && pnpm run build
```

Result: PASS（Verified）

```text
dist/style.css   16.70 kB │ gzip:  2.83 kB
dist/index.js   102.61 kB │ gzip: 26.06 kB
✓ built in 375ms
```
日志：`.temp/dbadmin-web-build.log`，`BUILD_EXIT=0`。

Command:

```bash
cd ForgeSelf.Api && dotnet build
```

Result: PASS（Verified）

```text
    1461 个警告
    0 个错误
已用时间 00:00:10.11
```
日志：`.temp/api-build-qtabs.log`，`BUILD_EXIT=0`。

## Unit Test

Command:

```bash
cd Plugins/DbAdmin/web && pnpm run test
```

Result: PASS（Verified）

```text
 ✓ src/sqlSplit.test.ts (12 tests)
 ✓ src/tabs.test.ts (10 tests)
 ✓ src/results.test.ts (8 tests)
 ✓ src/exportCsv.test.ts (8 tests)
 ✓ src/restore.test.ts (9 tests)
 ✓ src/connForm.test.ts (8 tests)
 Test Files  13 passed (13)
      Tests  103 passed (103)
```
日志：`.temp/dbadmin-web-test.log`，`TEST_EXIT=0`。

Command:

```bash
cd ForgeSelf.Web && pnpm run test
```

Result: PASS（Verified）

```text
 Test Files  112 passed (112)
      Tests  1202 passed (1202)
```
日志：`.temp/web-test-3.log`，`TEST_EXIT=0`。

## Integration Test

N/A（依据：本批零后端改动、零 API 契约改动，后端行为未变；`dotnet build` 已验证宿主源码引用链不破。）

## E2E

Command:

```bash
cd ForgeSelf.Web
CI=1 E2E_CLEAN=1 pnpm exec playwright test --config=playwright.config.ts e2e/plugins/db-admin
```

Result: PASS（Verified）

```text
1 passed (1.3m)
[db-admin] 选中行对比度 light=10.41:1 dark=7.98:1
```

覆盖（同一冒烟用例内扩展，未铺全流程）：

- ③ 连接留空默认表 → `workspace-empty` → 点 `empty-new-query` 才出现编辑器
- ④.0 多标签：两个标签（手写「新查询」+ 表标签 `e2e_items`）切换不覆盖、关闭手写标签
- ④.1 表过滤：`table-filter` 命中、`table-filter-empty` 无匹配空态
- ④.2 默认表：`cf-default-table` 填 `e2e_items` → 关全部标签 → 收起/再展开 → 自动带出默认表标签
- ④.3 原有 INSERT 审批拒绝流程不回归
- ⑤.1 多语句 → 结果标签条；⑤.2 选区 → `selection-hint` 含「仅执行选中」+ 只 1 个结果；⑤.3 单元格详情浮层 + `export-csv` 下载名 `/^e2e_items-r1-p1-\d{10}\.csv$/`

日志：`.temp/e2e-dbadmin-4.log`。

## Static Analysis

Command:

```bash
cd ForgeSelf.Web && pnpm run check
```

Result: PASS（Verified）

```text
✖ 166 problems (0 errors, 166 warnings)
[component-conventions] PASS（story 6 个 / ct.spec 6 个，均已注册、配对、标注）
```
日志：`.temp/web-check-6.log`，`CHECK_EXIT=0`。

Command:

```bash
cd ForgeSelf.Web && pnpm run design:check
```

Result: PASS（Verified）

```text
[style-tokens] 硬编码色值合计 1761（基线 2501）
[style-tokens] PASS：无新增硬编码色值，较基线减少 740 处
[design-compliance] PASS：无新增设计规范违反（DESIGN.md 门禁）
```
日志：`.temp/design-check-4.log`，`DESIGN_EXIT=0`。

Command（CT 像素回归，**不带** `--update-snapshots`，验证可重复性）：

```bash
cd ForgeSelf.Web && pnpm exec playwright test --config playwright-ct.config.ts src/components/DbAdmin.ct.spec.ts
```

Result: PASS（Verified）

```text
  32 passed (11.0s)
```
日志：`.temp/ct-verify.log`，`CT_EXIT=0`。

## Screenshots

CT 基线（组件级，含亮色态）：`ForgeSelf.Web/src/components/__screenshots__/` 下 DbAdmin 系列（本批新增/更新 4 个 story：
`QueryTabs` / `QueryTabsMultiResult` / `QueryRunBatch` / `QueryRunApproval`）。

e2e 真实宿主截图（`ForgeSelf.Web/screenshots/e2e/db-admin/`）：本批入口截图由既有用例产出（`01-loaded.png` 等），
多标签/选区/多语句/导出分支在同一用例内断言（未额外截图）。

已读图复核的 CT 快照：`DbAdmin · 多语句与选区执行 … 批量遇写审批：暂停-→-批准续跑；拒绝则其余「未执行」`，
确认审批弹窗字段（写操作审批 / 目标连接 demo.db / 类型「写」/ 来源「UI 编辑器」/ 预估风险「中：修改数据」/
脱敏 SQL `UPDATE users SET name = '****' WHERE id = 1` / 待审批 1 项 / 稍后·拒绝·批准）、左侧树
（users / orders / 视图 v_active_users）、右上「复制整行 / 导出 CSV」、底部历史与「Agent 工具回显」均正常。

## Known Limitations

1. **起源码本地前后端（dev-stack）直觉走查未单跑**（Unknown）。已起 `dev-stack.ps1`（:7301/:7399）并确认后端
   鉴权生效（无 token 探 `/api/plugin/frontend-manifest`、`/api/db-admin/providers` 均 401），随即 `-Stop` 清理；
   插件页需 `forge_api_token` 才能渲染数据，本会话未对 dev-stack 实例做浏览器截图走查。
   本批 UI 验收依据为：CT 32 用例（含四态）+ e2e 真实宿主（真实后端、真实审批、亮/暗对比度实测）。该缺口在发布后
   （用户把运行实例更新到 0.2.0 时）按 `plugin-publish-verify`「运行实例只读复验」补做。
2. **未发布**：`plugin.json` 已升 0.2.0，但未跑 `release-local.ps1`、未提交 git（闸门2 未过）。
3. **未验证场景**：MySQL/PostgreSQL 连接下的多语句/审批链路（本批只用 SQLite 实测，其余 provider 逻辑同源但
   无运行证据，标 Inferred）。

## Acceptance Criteria Coverage

| AC | 证据 | 等级 |
|----|------|------|
| AC1 / AC2 / AC3 | CT「两标签互不覆盖；切换保留内容；重复开同表不新建不重置」；e2e ④.0 | Verified |
| AC4 | CT「关闭标签：右邻激活；关到 0 显示工作区空态」 | Verified |
| AC5 | `tabs.test.ts`（12 上限 + banner）；`openTableTab.full` 分支 | Verified（单测） |
| AC6 | 删除连接时清其标签 + 默认表（`DbAdminView.vue`）+ `restore.test.ts` 快照剔除 | Verified（单测）+ Inferred（UI 路径） |
| AC7 | CT S-R「刷新前/刷新后」；`restore.test.ts` 9 用例（含旧 `drafts` 迁移） | Verified |
| AC8 | CT「批量遇写审批：暂停 → 批准续跑；拒绝则其余未执行」 | Verified |
| AC9 | `runSeq` + `tabExists(key, seq)` 丢弃迟到响应；关闭标签清 `approvalCtx` | Inferred（无专属用例） |
| AC10 | 历史按连接分流（既有逻辑未改）+ 活动标签派生连接 | Inferred |
| AC11 | e2e 实测 `light=10.41:1 dark=7.98:1` | Verified |
| AC12 | `cf-default-table` e2e 走查 + `connForm.test.ts`（`toSubmitBody` 剔除 `defaultTable`） | Verified |
| AC13 | e2e ④.1 `▸/▾` 只展开；CT 同路径断言 | Verified |
| AC14 | e2e ⑤.2 选区 + `selection-hint`「仅执行选中」+ 只 1 结果 | Verified |
| AC15 / AC16 | CT「多语句 → 结果标签条」+「失败即停，后续标未执行」 | Verified |
| AC17 | CT 审批暂停 / 批准续跑 / 拒绝未执行 | Verified |
| AC18 / AC19 | CT「单语句不渲染标签条」；分页归属每个 `ResultSet` | Verified |
| AC20 | `sqlSplit.test.ts` 12 用例（注释/字符串/TRIGGER/未闭合） | Verified |
| AC21 | e2e ④.1 `table-filter` / `table-filter-empty`；CT 同路径 | Verified |
| AC22 | CT 双击单元格详情 / 整行复制 / Esc；`exportCsv.test.ts` `rowText` | Verified |
| AC23 | CT 导出 CSV 文件名断言；`exportCsv.test.ts` 8 用例（转义/BOM/NULL） | Verified |
| AC24 | CT 状态栏「120 行 · 4 ms」+ `result-range`「第 101–120 行 / 共 120」 | Verified |
| AC25 | 本文件全部门禁输出（插件 build/test、宿主 check/test/design:check、CT 32、e2e 1 passed） | Verified（除 Known Limitations） |

## Unresolved Issues

无 FAIL 项。
