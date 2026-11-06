# Repository Understanding

> 阶段：Stage 0（动手写代码之前必须完成）｜规范：`docs/04-standards/ai-native-engineering-workflow.md` §2
> 原则：所有条目必须来自**真实仓库内容**，禁止凭常识推测。
> Task ID：`2026-11-06-db-admin-query-tabs`

## 项目结构

- 仓库根：`d:\src\my-proj\OpenForgeSelf\OpenForgeSelf`（`ForgeSelf.slnx`）
- 宿主后端：`ForgeSelf.Api/`（.NET 10 + SQLite + NewLife.XCode + 插件架构）
- 宿主前端：`ForgeSelf.Web/`（Vue 3.5 + Vite 6 + TS 5.7 + Element Plus + Pinia + pnpm）
- 插件：`Plugins/<PascalCase>/`（后端 `plugin.json` + 自带 `web/`，本次目标为 `Plugins/DbAdmin/`）
- 测试：`ForgeSelf.Api.Tests/`（xUnit）、宿主前端 `ForgeSelf.Web/src/**/*.spec.ts`（vitest）、
  组件测试 `ForgeSelf.Web/src/components/DbAdmin.ct.spec.ts`（Playwright CT）、
  插件层 e2e `ForgeSelf.Web/e2e/plugins/db-admin/db-admin.spec.ts`
- 工件：`docs/ai/pilot/YYYY-MM-DD-<task-id>/`；功能文档 `docs/02-features/044-db-admin.md`

## 技术栈

| 层 | 技术 | 依据（文件/配置） |
| --- | --- | --- |
| 插件前端 | Vue 3 SFC + `<script setup lang="ts">` | `Plugins/DbAdmin/web/src/DbAdminView.vue:1-12` |
| 样式 | Element Plus `--el-*` token + scoped CSS（禁裸色值） | `DbAdminView.vue:1014-1174`、`DESIGN.md` |
| 构建 | vite lib 模式，产物 `web/dist/index.js` | `Plugins/DbAdmin/web/vite.config.ts`、`plugin.json` |
| 单测 | vitest（插件纯逻辑模块与 `.test.ts` 同目录） | `Plugins/DbAdmin/web/src/*.test.ts` |
| 组件测试 | 宿主 CT 画廊 + `DbAdminStoryHost.vue` 注入 fixture | `ForgeSelf.Web/src/components/DbAdmin.story.ts`、`DbAdmin.ct.spec.ts` |
| 插件 e2e | Playwright + 真实 publish 宿主，零 mock | `ForgeSelf.Web/e2e/plugins/db-admin/db-admin.spec.ts` |

## 架构特点

- 插件前端是**独立预编译产物**，`vue`/`vue-router`/`element-plus` 由宿主 import map 解析为同一实例；
  插件内不能 `import` 宿主 `@/…` 模块（`.agents/skills/plugin-development/SKILL.md` 铁律 4）。
- DbAdmin 视图内聚：`DbAdminView.vue` 持有连接、schema 树、编辑器、结果格、历史/Agent 回显、审批浮层；
  纯逻辑拆到同目录模块（`pager.ts` / `restore.ts` / `normalize.ts` / `approvalFlow.ts`），可在 vitest 直接测。
- **当前查询态是"单实例"**（真实仓库事实，grep 行号）：
  - 每连接一份编辑器草稿：`drafts = ref<Record<string,string>>({})`（`DbAdminView.vue:49`），当前 SQL 由 `sql` computed 读写该连接草稿（`:80`）
  - 全局单份结果：`result` / `resultError` / `running` / `page` / `pageSize` / `lastSql`（`:50-55`）
  - 点表：`toggleTable` 只展开列；`insertSelect` 把 `SELECT * FROM <表>` 写进**当前连接草稿**（`:353-356`，双击触发，模板 `:688/:733`）
  - 切换连接：`selectConnection` 直接清空 `result` / `resultError` / 复位页码（`:338-347`）⇒ 换连接/换表必丢上一份查询结果
  - 执行与分页均以全局单份结果为目标：`execute`（`:359`）、`handleQueryResponse`（`:384`）、`goPage`（`:406`）、`changePageSize`（`:415`）
  - 审批回填也写全局结果：`resolveApproval`（`:452`）→ `handleQueryResponse`（`:473`）
- 刷新恢复（S-R）：`restore.ts` 以 `activeConnectionId + drafts: Record<connectionId, sql>` 落 localStorage（≤64KB），
  不恢复结果集（`restore.ts:24-30`、`buildSnapshot:106`、`loadSnapshot:139`）。
- fixture（CT 画廊）与真实请求共用 normalize：`applyFixture`（`DbAdminView.vue:159-206`），fixture 的 `sql` 只生成一份当前连接草稿。

## 测试方式

| 层 | 命令 | 说明 |
| --- | --- | --- |
| 插件前端构建 | `cd Plugins/DbAdmin/web && pnpm run build` | 产物 `web/dist/index.js` + `style.css` |
| 插件单测 | `cd Plugins/DbAdmin/web && pnpm run test` | vitest（`*.test.ts`） |
| 宿主前端 | `cd ForgeSelf.Web && pnpm run check && pnpm run test` | vue-tsc + eslint / vitest |
| 宿主设计门禁 | `cd ForgeSelf.Web && pnpm run design:check` | 禁裸色值 / token 族违规 |
| 组件测试 | `cd ForgeSelf.Web && pnpm run test:ct`（更新基线 `test:ct:update`） | DbAdmin story 与像素基线 |
| 插件冒烟 e2e | `cd ForgeSelf.Web && pnpm exec playwright test --config=playwright.config.ts e2e/plugins/db-admin` | 真实宿主，零 mock |
| 后端 | `cd ForgeSelf.Api && dotnet build`（本任务不改后端，定向测试可免） | — |

环境前置（`AGENTS.md` §5.0）：`$env:NO_PROXY='localhost,127.0.0.1,::1'`、`$env:TEMP=$env:TMP='<repo>\.temp\tmp'`；脚本一律 `pwsh`。

## 构建命令

```bash
cd Plugins/DbAdmin/web && pnpm run build
cd ForgeSelf.Web && pnpm run check && pnpm run test && pnpm run design:check
cd ForgeSelf.Web && pnpm run test:ct
cd ForgeSelf.Web && pnpm exec playwright test --config=playwright.config.ts e2e/plugins/db-admin
```

## 主要目录职责

| 目录 | 职责 |
| --- | --- |
| `Plugins/DbAdmin/web/src/` | 视图 + 纯逻辑模块 + 单测 + fixture 样本 |
| `Plugins/DbAdmin/docs/` | 产品需求 / 交互说明 / UI 评审稿 / `ui-design-brief.md` |
| `ForgeSelf.Web/src/components/` | 宿主薄壳 `DbAdminStoryHost.vue` + story + CT 用例 |
| `ForgeSelf.Web/e2e/plugins/db-admin/` | 插件层冒烟 e2e（恰 1 条主链路） |
| `docs/02-features/044-db-admin.md` | 功能真源（AI 入口、契约、交互） |

## 代码组织方式

- 视图组件只管编排与渲染；可测逻辑（分页、恢复、归一、脱敏、审批编排）在独立纯 TS 模块并配 `*.test.ts`。
- CT fixture 用后端原始 JSON 形态（`__fixtures__/backend-samples.json` 同源），视图走真实 normalize。

## 现有工程规范

- `AGENTS.md` §11：开发任务走 AI-Native 九阶段，闸门1（Intent/Spec/Plan/Task）批准后才 Implement。
- `AGENTS.md` §8 设计门禁三步：`design:check` + Spec「Interaction Design」必写 + 走查符合性（亮/暗两态）。
- `.agents/skills/plugin-development/SKILL.md` 铁律 7/24/25：插件改动 = 门禁 + 1 条冒烟 e2e + 起源码本地前后端走查；
  §3.4 交互设计统一要求（点即保存/成败可见/防闪/空态分级/边界/禁用原因/异步局部 loading ≤100ms）。
- `DESIGN.md`：token 引用、禁裸色值、组件规格、三禁红线。

## 候选低风险任务

| 候选 | 风险 | 说明 |
| --- | --- | --- |
| DbAdmin 多标签查询页（本任务） | L2 | 仅插件前端 + 纯逻辑模块 + fixture/story/e2e/文档；不碰后端与 DB |
| 历史记录导出 | L1 | 纯新增，但用户未提出 |
| 结果格列宽拖拽 | L1 | 交互面扩大，非用户当前诉求 |

## 选择该任务的原因

用户直接指令（最高优先级）：切换不同表查询时，另一表的编辑器内容与结果会丢失；要求"不同表各开一个查询页，
已打开的表则聚焦原标签"。改动集中在 `Plugins/DbAdmin/web/src/`（视图 + 新纯逻辑模块 + 恢复格式迁移），
可用 vitest 锁纯逻辑、CT 锁渲染态、单条冒烟 e2e 锁真实链路，风险可控且不触碰后端契约。
