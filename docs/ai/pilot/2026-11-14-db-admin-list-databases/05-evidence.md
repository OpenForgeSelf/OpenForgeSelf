# Evidence

> 阶段：Stage 7｜只记录实际发生的事情。
> 环境：`$env:NO_PROXY='localhost,127.0.0.1,::1'`，`$env:TEMP` / `$env:TMP` 指到工作树 `.temp/tmp`。未改仓库代理或临时目录配置。

## Task

`2026-11-14-db-admin-list-databases`

## Changed Files

- `Plugins/DbAdmin/web/src/DbAdminView.vue`
- `Plugins/DbAdmin/web/src/connForm.ts`、`connForm.test.ts`
- `Plugins/DbAdmin/web/src/api.ts`、`api.test.ts`
- `Plugins/DbAdmin/web/src/normalize.ts`、`normalize.test.ts`
- `Plugins/DbAdmin/web/src/types.ts`
- `Plugins/DbAdmin/web/src/tabs.ts`、`tabs.test.ts`
- `Plugins/DbAdmin/web/src/restore.ts`、`restore.test.ts`
- `Plugins/DbAdmin/web/src/results.ts`、`results.test.ts`
- `Plugins/DbAdmin/web/src/exportCsv.ts`
- `Plugins/DbAdmin/web/src/exportResult.ts`、`exportResult.test.ts`（新）
- `Plugins/DbAdmin/web/src/treeFilter.ts`、`treeFilter.test.ts`（新）
- `Plugins/DbAdmin/plugin.json`（0.2.2）
- `ForgeSelf.Web/src/components/DbAdmin.story.ts`
- `ForgeSelf.Web/src/components/DbAdmin.ct.spec.ts`
- `docs/02-features/044-db-admin.md`
- `Plugins/DbAdmin/docs/ui-design-brief.md`
- `Plugins/DbAdmin/docs/02-交互说明.md`
- `Plugins/DbAdmin/docs/03-架构与接口.md`
- `Plugins/DbAdmin/docs/06-UI交互评审稿.md`

未改 `Plugins/DbAdmin` 下任何 `.cs`。未改主工作区。

## Build

Command:

```bash
cd Plugins/DbAdmin/web
pnpm run build
```

Result: PASS（Verified）。进程退出码 0。日志 `.temp/dbadmin-build.log`：

```text
$ vite build
vite v6.4.5 building for production...
transforming...
19 modules transformed.
rendering chunks...
computing gzip size...
dist/style.css   18.04 kB
dist/index.js   131.58 kB
built in 384ms
```

## Unit Test

Command:

```bash
cd Plugins/DbAdmin/web
pnpm run test
```

Result: PASS（Verified）。进程退出码 0。本次命令先装了插件 web 依赖（pnpm 报告 +69，用的是本机 pnpm store），然后 vitest。日志 `.temp/dbadmin-test.log`：

```text
RUN  v3.2.7 Plugins/DbAdmin/web

src/pager.test.ts (6 tests)
src/redact.test.ts (6 tests)
src/exportCsv.test.ts (8 tests)
src/approvalFlow.test.ts (6 tests)
src/exportResult.test.ts (5 tests)
src/treeFilter.test.ts (4 tests)
src/normalize.test.ts (8 tests)
src/connForm.test.ts (10 tests)
src/tabs.test.ts (11 tests)
src/sqlSplit.test.ts (12 tests)
src/results.test.ts (8 tests)
src/http.test.ts (6 tests)
src/restore.test.ts (10 tests)
src/api.test.ts (4 tests)
src/contract.test.ts (14 tests)

Test Files  15 passed (15)
     Tests  118 passed (118)
  Start at  11:59:56
  Duration  2.55s (transform 882ms, setup 0ms, collect 1.83s, tests 67ms, environment 2ms, prepare 3.28s)
```

失败数：0。没有删既有测试。

## Integration Test

未改后端。`dotnet test` 未跑。Result: N/A（依据：工作树内无 `.cs` 改动；用户要求不必跑全量 dotnet test，并在此写明未改后端）。

## E2E

未跑。Result: Unknown（依据：本任务禁止 e2e / dev-stack / 停启宿主）。

## Static Analysis

Command:

```bash
cd ForgeSelf.Web
pnpm run design:check
```

Result: PASS（Verified）。跑了两次，退出码都是 0。第二次日志 `.temp/design-check-utf8.log` 的中文被控制台码页打乱，下面是日志里仍可读的片段：

```text
> forgeself-frontend@0.0.0 design:check ForgeSelf.Web
> node ../scripts/check-style-tokens.mjs && node ../scripts/check-design-compliance.mjs
[style-tokens] ... 1761 ... 2501 ... PASS ... 740
[design-compliance] a(...)=209 b(...)=0 c(...)=0 d(...)=0 （基线同为 209/0/0/0） PASS
```

`Plugins/DbAdmin/web` 没有 `pnpm run check`（`package.json` 只有 `test` 与 `build`）。宿主 `pnpm run check` 未跑。

## Component Test（Playwright CT）

第一次：

```bash
cd ForgeSelf.Web
pnpm exec playwright test --config playwright-ct.config.ts src/components/DbAdmin.ct.spec.ts -g "库节点|校验失败"
```

Result: FAIL（Verified）。`ForgeSelf.Web/node_modules` 不存在。全文：

```text
Error: Cannot find package '@playwright/test' imported from ForgeSelf.Web/playwright-ct.config.ts
code: 'ERR_MODULE_NOT_FOUND'
```

随后在 `ForgeSelf.Web` 执行 `pnpm install --frozen-lockfile`，退出码 0（+355，含 `@playwright/test 1.61.1`）。未改 lockfile。

第二次同一条 playwright 命令，并设 `CT_FRESH=1`。Result: 2 tests，1 passed，1 failed（Verified）。退出码 1。日志 `.temp/dbadmin-ct.log`：

```text
Running 2 tests using 2 workers
  2 [ct-host] src\components\DbAdmin.ct.spec.ts:70:1 S2 MySQL 展开后先见库节点（夹具） (842ms)  passed
  1 [ct-host] src\components\DbAdmin.ct.spec.ts:58:3 校验失败留窗；编辑态密码遮罩、留空不改 (1.2s)  failed

Error: A snapshot doesn't exist at
ForgeSelf.Web\src\components\DbAdmin.ct.spec.ts-snapshots\...校验失败留窗...-1-ct-host-win32.png, writing actual.

  64 |     await tid(page, 'cf-save').click()
  65 |     await expect(tid(page, 'conn-form')).toBeVisible()
> 66 |     await expect(root(page)).toHaveScreenshot()

1 failed
1 passed (6.0s)
```

失败点只有第 66 行截图。同用例里「请填写服务名」和点保存后表单仍在的断言没有报失败。基线目录在 `.gitignore`（`ForgeSelf.Web/**/*-snapshots/`），注释写明基线不入库。Playwright 把当次图写进了该 gitignore 目录和 `test-results/`。

读了失败用例写出的 actual 图：表单 Provider 为 Oracle，字段名「服务名」，端口 1521，红字「请填写服务名」，密码占位「****（留空 = 不改）」。这是读图，不是像素对比通过。

MySQL 库节点用例没有 `toHaveScreenshot`。它通过了：`db-8-app` 含「默认」，`db-8-mysql` 与 `db-8-information_schema` 含「系统」，顺序为 app、mysql、information_schema，「未枚举到数据库」数量为 0，`table-8-app-orders` 可见。

全文件 `DbAdmin.ct.spec.ts` 未整份跑。其余故事的截图：Unknown。

## Screenshots

- 上述 CT actual：`ForgeSelf.Web/test-results/` 下 ConnFormInvalid 的 `*-actual.png`（gitignore，不入库）。
- 走查截图：无。

## Known Limitations

- 没有连真实 MySQL / SQL Server / PostgreSQL。库列表顺序与标记只在夹具 CT 和 `presentDatabases` 单测里看到。
- `toHaveScreenshot` 没有旧基线可比。没有把这次生成的 png 当回归通过。
- 导出全部、快捷键、列宽拖拽、右键菜单没有组件测试覆盖，只有纯函数单测和构建。

## Unresolved Issues

- ConnFormInvalid 的 `toHaveScreenshot` 在干净工作树会因缺少基线失败。这是仓库「基线不入库」的既有行为，不是本批把文案改错。未用 `--update-snapshots` 把该失败改成绿。
- 走查与插件 e2e 留给主会话。
