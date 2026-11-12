# Repository Understanding

> 阶段：Stage 0｜事实全部来自当前仓库。

## 项目结构

- 宿主前端 `ForgeSelf.Web/`（Vue 3 + Vite + Element Plus + Tailwind；CT 画廊 `ct/`，CT 配置 `playwright-ct.config.ts`）。
- 插件 `Plugins/DbAdmin/web/`（独立 Vite lib 构建，`web/dist/index.js` 远程加载）。
- DbAdmin 视图 `Plugins/DbAdmin/web/src/DbAdminView.vue`：左栏连接树 + 右区 SQL 编辑器/结果。
- CT 宿主壳 `ForgeSelf.Web/src/components/DbAdminStoryHost.vue` + story `DbAdmin.story.ts` + `DbAdmin.ct.spec.ts`（已在 `ct/stories.ts:19` 注册）。

## 技术栈

| 层 | 技术 | 依据 |
| --- | --- | --- |
| 插件 UI | Vue 3.5 SFC + scoped CSS + `--el-*` token | `Plugins/DbAdmin/web/src/DbAdminView.vue` |
| 单测/CT | vitest + Playwright CT | `package.json` scripts `test` / `test:ct` |
| 走查规范 | `ui-ux-design` 清单 + `plugin-development` §3.4 + `DESIGN.md` | 技能与规范文件 |

## 现状与缺口

- `selectConnection(c)` 在 `DbAdminView.vue:325-334` 切换 `activeId`、展开连接并调用 `loadTables(c.id)`。
- `loadTables` 在 `:262-277` 只 `await dbAdminApi.tables(id)`，**无 loading 状态**；用户点击后到响应返回前，左栏会立刻显示「（无表）」（`:730`），造成「没有表 / 没反应」的错觉。
- 初次整页加载有 `tree-skeleton`（`:650`），但**按连接加载表**没有局部反馈。
- CT 画廊已有 `DbAdmin/Loading`（整页加载）与 `S2 SchemaTree`，但**没有表加载中状态**的 story/断言。

## 测试与构建命令

```bash
cd ForgeSelf.Web && pnpm run check && pnpm run test && pnpm run design:check
cd ForgeSelf.Web && CT_FRESH=1 pnpm run test:ct:update && CT_FRESH=1 pnpm run test:ct
cd Plugins/DbAdmin/web && pnpm run build
cd ForgeSelf.Web && CI=1 E2E_CLEAN=1 pnpm exec playwright test --config=playwright.config.ts e2e/plugins/db-admin
```

## 现有规范（对本任务有约束力）

- `AGENTS.md` §8 设计门禁三步：Spec 交互节、走查符合性、静态守卫。
- `DESIGN.md` Do's：列表/面板必须有加载态/空态/错误态。
- `ui-ux-design` G「组件四态」：加载态有 spinner/skeleton（本批补「异步加载不抢先空态 + 防竞态」）。
- `plugin-development` §3.4 设计/验证清单（本批补异步 loading 与 CT 状态回归）。
- `docs/04-standards/frontend-component-development.md`：组件 story/CT 准入与 DoD。

## 适用问题类

- PC-025（暗色 token 对比度）不适用（本批不改颜色）；PC-019（flex 压缩）lens：loading 行是普通 flex 子项，父容器 `.side > * { flex-shrink:0 }` 已有，不触发压缩。
- 本批为交互状态补强，非缺陷类，不新建 PC。
