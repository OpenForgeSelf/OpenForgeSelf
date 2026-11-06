# Plan — db-admin 表名与结果表对齐修复

> 阶段：Stage 3｜Task ID：`2026-11-06-db-admin-table-alignment`｜日期：2026-11-06

## Files To Change

- file: `Plugins/DbAdmin/web/src/DbAdminView.vue`
  reason: 两处表名渲染去掉「表」前缀；结果表 `class="grid"` → `dba-grid`，CSS 选择器同步。
- file: `ForgeSelf.Web/e2e/plugins/db-admin/db-admin.spec.ts`
  reason: 增加表名文案断言、display=table 断言、列左边界对齐断言（批准插入 1 行后测）。
- file: `ForgeSelf.Web/src/styles/pluginTableClassGuard.test.ts`（新建）
  reason: G2 静态守卫，枚举全部插件 `<table>` 的 display 工具类冲突。
- file: `docs/11-troubleshooting/problem-classes/PC-026-plugin-class-tailwind-collision.md`（新建）
- file: `docs/11-troubleshooting/cases/2026-11-06-dbadmin-table-header-misalign.md`（新建）
- file: `docs/11-troubleshooting/problem-classes/README.md`、`cases/README.md`、`index.md`
  reason: PC/case 三处索引闭合。
- file: `Plugins/DbAdmin/docs/02-交互说明.md`
  reason: 布局示意中的「▸ 表 users」改为「▸ users」。
- file: `docs/ai/pilot/2026-11-06-db-admin-table-alignment/`（00-07）
  reason: 本任务工件链。

## Implementation Steps

1. 先复现红灯：e2e 加表名/display 断言 → 运行得到两条红灯（表名「表 e2e_items」、display="grid"）。
2. 修 1：模板两处 `{{ t.type === 'view' ? '视图' : '表' }} {{ t.name }}` → `{{ t.type === 'view' ? '视图 ' : '' }}{{ t.name }}`。
3. 修 2：`<table class="grid">` → `<table class="dba-grid">`；CSS `.grid*` → `.dba-grid*`。
4. 重建插件 web dist；e2e 补「批准插入 1 行 + 列左边界断言」；跑绿。
5. 新增 G2 守卫并做 mutation 验证（改回 `grid` → 红；还原 → 绿）。
6. 写 PC-026 + case + 三处 README；跑 `check-problem-classes.ps1` + `ProblemClassRegistryTests`。
7. 跑门禁：插件 web test/build、宿主 check/test/design:check、e2e。
8. 更新插件文档与日记/TODO。

## Test Plan

1. G1 e2e：`e2e/plugins/db-admin/db-admin.spec.ts`（表名文本 / display=table / 列左边界 ≤1px）。
2. G2 静态守卫：`ForgeSelf.Web/src/styles/pluginTableClassGuard.test.ts`（含 mutation 验证）。
3. 既有回归：插件 web 61 项、宿主前端 1160 项、DbAdmin 后端 167 项（契约未变，跑定向即可）。
4. 问题类台账：`ProblemClassRegistryTests` + `check-problem-classes.ps1`。

## Verification

### Build

```bash
cd Plugins/DbAdmin/web && pnpm run build
```

### Unit Test

```bash
cd Plugins/DbAdmin/web && pnpm run test
cd ForgeSelf.Web && pnpm run check && pnpm run test
```

### Integration Test

N/A：无独立集成测试工程；DbAdmin 后端契约未改。

### E2E

```bash
cd ForgeSelf.Web && pnpm exec playwright test --config=playwright.config.ts e2e/plugins/db-admin
```

### Other Checks

```bash
cd ForgeSelf.Web && pnpm run design:check
pwsh scripts/check-problem-classes.ps1 -PcId PC-026
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj --filter "FullyQualifiedName~ProblemClassRegistryTests"
pwsh scripts/verify-pilot-artifacts.ps1 -TaskId 2026-11-06-db-admin-table-alignment -EnforceHeader -EnforceAcCoverage -EnforceInteraction
```

## Plan 偏差记录

| 时间 | 偏差点 | 原 Plan | 修正后 |
| --- | --- | --- | --- |
| 10:1x | 首版列对齐断言在 SELECT 后立刻执行，插入审批尚未落库 → 0 行无 td，断言偶发红（Playwright 判 flaky） | SELECT 后直接断言 | 批准 INSERT 后先等 `result-stats` 出现「影响 1 行」，再 SELECT 并断言 |
| 10:1x | G2 守卫首版阻塞注释含 `web/src/**/*.vue`（`*/` 提前闭合注释）导致 esbuild 语法错误 | 注释照写路径 | 改为「web/src 目录下的 .vue 文件」文字描述，避免注释闭合 |
