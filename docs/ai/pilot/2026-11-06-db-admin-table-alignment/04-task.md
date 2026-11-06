---
task_id: 2026-11-06-db-admin-table-alignment
task_kind: defect
feature: 044-db-admin
risk: { level: L2, triggers: ["Plugins/DbAdmin/web/src/DbAdminView.vue", "ForgeSelf.Web/e2e/plugins/db-admin/**", "ForgeSelf.Web/src/styles/pluginTableClassGuard.test.ts"], raised_by_agent: false }
gate1: { mode: user, at: "2026-11-06T10:00+08:00", decision: "用户输入3 直接指令『1、表名不用加表字…2、查询结果的列表头和内容没对齐』，视为闸门1 已批" }
gate2: { mode: reviewer, at: null, decision: null }
gate3: { mode: agent, at: null }
expected_files:
  - Plugins/DbAdmin/web/src/DbAdminView.vue
  - ForgeSelf.Web/e2e/plugins/db-admin/db-admin.spec.ts
  - ForgeSelf.Web/src/styles/pluginTableClassGuard.test.ts
  - Plugins/DbAdmin/docs/02-交互说明.md
  - docs/11-troubleshooting/problem-classes/PC-026-plugin-class-tailwind-collision.md
  - docs/11-troubleshooting/cases/2026-11-06-dbadmin-table-header-misalign.md
  - docs/11-troubleshooting/problem-classes/README.md
  - docs/11-troubleshooting/cases/README.md
  - docs/11-troubleshooting/index.md
  - docs/ai/pilot/2026-11-06-db-admin-table-alignment/**
rollback: "git checkout -- Plugins/DbAdmin/web/src/DbAdminView.vue ForgeSelf.Web/e2e/plugins/db-admin/db-admin.spec.ts ForgeSelf.Web/src/styles/pluginTableClassGuard.test.ts docs/11-troubleshooting/（或 git revert <commit>）"
writeback: { feature_doc: done, reason: "插件 docs/02 与功能文档路径无需新增条目；PC-026/case 已入台账" }
---

# Agent Task

## Task ID

`2026-11-06-db-admin-table-alignment`

## Objective

修复 DbAdmin 两处 UI 缺陷：表名不再显示「表」前缀；结果表格恢复原生 table 布局，表头与数据列逐列对齐，并补机器守卫防同类复发。

## Scope

### Allowed

- `Plugins/DbAdmin/web/src/DbAdminView.vue`：表名文案 + 结果表类名/CSS。
- `ForgeSelf.Web/e2e/plugins/db-admin/db-admin.spec.ts`：冒烟断言与截图。
- `ForgeSelf.Web/src/styles/pluginTableClassGuard.test.ts`：新增 G2 守卫。
- `docs/11-troubleshooting/`：PC-026、case、三处索引。
- `Plugins/DbAdmin/docs/02-交互说明.md`：布局示意同步。
- 本 pilot 工件目录。

### Forbidden

- 不改后端代码、后端契约、数据库结构、鉴权逻辑。
- 不改宿主 Tailwind 配置 / 主题 token / 其他插件。
- 不新增依赖、不新增第二条插件 e2e（保持 1 条冒烟）。
- 不停/启/杀 `:51888` 用户宿主；不执行 git commit/push/tag/发布（发布需另行授权）。
- 不用 `!important` / 局部 `display:table` 覆盖来掩盖类名冲突。

## Acceptance Criteria

- [ ] AC1 表名 `.name` 严格等于 `e2e_items`（无「表」前缀）；视图保留「视图」标识。
- [ ] AC2 结果表 computed `display === 'table'`；有 1 行数据时 `thead th` 与 `tbody td` 左边界差 ≤1px。
- [ ] AC3 空结果（0 行）表头仍显示，既有 SELECT 用例通过。
- [ ] AC4 插件 web test/build、宿主 check/test/design:check 全绿。
- [ ] AC5 G2 守卫 mutation 红→还原绿；PC-026/case 三索引闭合；台账检查绿。
- [ ] AC6 运行实例只读复验（发布后）：:51888 查询截图对齐（本批源码验证以 e2e 为准）。

## Expected Files

- Plugins/DbAdmin/web/src/DbAdminView.vue
- ForgeSelf.Web/e2e/plugins/db-admin/db-admin.spec.ts
- ForgeSelf.Web/src/styles/pluginTableClassGuard.test.ts
- Plugins/DbAdmin/docs/02-交互说明.md
- docs/11-troubleshooting/problem-classes/PC-026-plugin-class-tailwind-collision.md
- docs/11-troubleshooting/cases/2026-11-06-dbadmin-table-header-misalign.md
- docs/11-troubleshooting/{problem-classes, cases}/README.md、index.md

## Verification Commands

```bash
cd Plugins/DbAdmin/web && pnpm run test && pnpm run build
cd ForgeSelf.Web && pnpm run check && pnpm run test && pnpm run design:check
cd ForgeSelf.Web && pnpm exec playwright test --config=playwright.config.ts e2e/plugins/db-admin
pwsh scripts/check-problem-classes.ps1 -PcId PC-026
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj --filter "FullyQualifiedName~ProblemClassRegistryTests"
pwsh scripts/verify-pilot-artifacts.ps1 -TaskId 2026-11-06-db-admin-table-alignment -EnforceHeader -EnforceAcCoverage -EnforceInteraction
```
