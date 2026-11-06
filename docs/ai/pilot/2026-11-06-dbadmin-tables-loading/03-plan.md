# Plan

> Task ID：`2026-11-06-dbadmin-tables-loading`

## Files To Change

- `Plugins/DbAdmin/web/src/DbAdminView.vue`
  reason: 增加 `tablesLoading` + `tableLoadSeq`，`loadTables` 置 loading/去重/竞态；左栏系统/用户连接展开处渲染 loading 行；新增 spinner CSS。
- `Plugins/DbAdmin/web/src/types.ts`
  reason: `DbAdminFixture.tablesLoading`，供 CT 定格加载状态。
- `ForgeSelf.Web/src/components/DbAdmin.story.ts`
  reason: 新增 `DbAdmin/TablesLoading` story（展开连接 + loading，不显示无表）。
- `ForgeSelf.Web/src/components/DbAdmin.ct.spec.ts`
  reason: 新增 loading 行为断言 + 截图基线。
- `AGENTS.md`
  reason: §8 设计门禁第 3 步补异步 loading/空态/竞态规则。
- `DESIGN.md`
  reason: Do's 补异步动作局部 loading 规则。
- `.agents/skills/ui-ux-design/SKILL.md`
  reason: G 组件四态补「异步加载不抢先空态 + 防竞态 + 失败可重试」。
- `.agents/skills/plugin-development/SKILL.md`
  reason: §3.4 设计/验证清单补异步 loading 与 CT 状态回归。
- `docs/18-templates/ai-pilot/02-spec.tpl.md`
  reason: 交互验收标准补异步 loading 条款。
- `Plugins/DbAdmin/docs/ui-design-brief.md`
  reason: 五态补「表加载」。
- `Plugins/DbAdmin/docs/06-UI交互评审稿.md`
  reason: S2 验收补 loading 不抢先空态。
- `docs/ai/pilot/2026-11-06-dbadmin-tables-loading/00-07`
  reason: 工件链。
- `TODO.md` + `.forgeself/memory/2026-11-06.md`
  reason: 队列与日志。

## Implementation Steps

1. `DbAdminView.vue`：加 `tablesLoading`/`tableLoadSeq`；`applyFixture` 读取；`loadTables` 置 loading + 去重 + 序号；模板两处连接块加 loading 行；CSS 加 spinner。
2. `types.ts`：fixture 增 `tablesLoading`。
3. story/CT：新增 `DbAdmin/TablesLoading`；CT 断言 text/spinner/无「（无表）」+ screenshot。
4. 规范 5 处 + DbAdmin 2 处文档补条目。
5. `pnpm run test:ct:update` 生成基线 → `pnpm run test:ct` 复查；读图。
6. 插件 build + 前端 test/design/check（check 若被并行文件拦红则单独 ESLint + 归因）+ DbAdmin e2e。
7. 补齐 05-07、日记、TODO 移除。

## Test Plan

1. CT：`CT_FRESH=1 pnpm run test:ct:update` → `CT_FRESH=1 pnpm run test:ct`，确认 `DbAdmin/TablesLoading` 新基线生成且复查绿。
2. 插件 build：`cd Plugins/DbAdmin/web && pnpm run build`。
3. 前端单测：`cd ForgeSelf.Web && pnpm run test`（至少过滤 DbAdmin/CT 相关）。
4. 设计守卫：`pnpm run design:check`。
5. e2e：`CI=1 E2E_CLEAN=1 pnpm exec playwright test --config=playwright.config.ts e2e/plugins/db-admin`。
6. 读图：新基线 PNG 与运行截图。

## Verification

```bash
cd ForgeSelf.Web && CT_FRESH=1 pnpm run test:ct:update
cd ForgeSelf.Web && CT_FRESH=1 pnpm run test:ct
cd Plugins/DbAdmin/web && pnpm run build
cd ForgeSelf.Web && pnpm run test
cd ForgeSelf.Web && pnpm run design:check
cd ForgeSelf.Web && CI=1 E2E_CLEAN=1 pnpm exec playwright test --config=playwright.config.ts e2e/plugins/db-admin
```

## Plan 偏差记录

| 时间 | 偏差点 | 原 Plan | 修正后 |
| --- | --- | --- | --- |
| — | — | — | — |
