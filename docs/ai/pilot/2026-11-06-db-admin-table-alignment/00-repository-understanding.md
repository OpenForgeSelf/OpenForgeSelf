# Repository Understanding — db-admin 表名与结果表对齐修复

> 阶段：Stage 0｜Task ID：`2026-11-06-db-admin-table-alignment`｜日期：2026-11-06｜类型：缺陷修复

## 项目结构

- 解决方案 `ForgeSelf.slnx`；宿主后端 `ForgeSelf.Api/`（.NET 10 + NewLife.XCode）；宿主前端 `ForgeSelf.Web/`（Vue 3 + Vite + pnpm）。
- 插件源码 `Plugins/DbAdmin/`（`plugin.json` Id=`db-admin`，本次发布前版本 0.1.4）；插件前端 `Plugins/DbAdmin/web/`（Vite lib 模式，产物 `dist/index.js`）。
- 插件测试：`ForgeSelf.Api.Tests/Plugins/DbAdminTests/`；插件冒烟 e2e：`ForgeSelf.Web/e2e/plugins/db-admin/db-admin.spec.ts`；CT 画廊：`ForgeSelf.Web/src/components/DbAdmin.story.ts` + `DbAdmin.ct.spec.ts`。
- 功能文档：`docs/02-features/044-db-admin.md`；插件文档 `Plugins/DbAdmin/docs/01~07`。

## 技术栈

| 层 | 技术 | 依据 |
| --- | --- | --- |
| 后端 | .NET 10 + NewLife.XCode 12.0.2026.701 + SQLite | `ForgeSelf.Api/ForgeSelf.Api.csproj` |
| 前端 | Vue 3.5 + Vite 6 + Element Plus + Tailwind（宿主全局工具类） | `ForgeSelf.Web/package.json`、`ForgeSelf.Web/src/styles/*` |
| 插件前端 | Vue SFC + Vite lib 模式（产物由宿主远程加载） | `Plugins/DbAdmin/web/vite.config.ts`、`plugin.json frontend.entry` |
| 测试 | xUnit、vitest、Playwright e2e/CT | `ForgeSelf.Api.Tests/`、`ForgeSelf.Web/src/**/*.test.ts`、`e2e/` |

## 架构特点

- 插件不是 iframe/Shadow DOM：插件组件挂载在宿主 document 内，**宿主全局 Tailwind 工具类会命中插件 DOM**。
- DbAdmin 结果表用原生 `<table>` + 自有 CSS（`.grid` 等）；模板 `DbAdminView.vue` 同时渲染 schema 树与结果表格。
- CT 画廊 `DbAdmin/ReadResult` 用 fixture 渲染 20 行结果，可用于组件级视觉检查；但 CT 不加载宿主 Tailwind，**不能复现宿主全局 CSS 冲突**。

## 测试方式

- 后端定向：`dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~DbAdmin"`
- 插件前端：`cd Plugins/DbAdmin/web && pnpm run test && pnpm run build`
- 宿主前端：`cd ForgeSelf.Web && pnpm run check && pnpm run test && pnpm run design:check`
- 插件 e2e：`pnpm exec playwright test --config=playwright.config.ts e2e/plugins/db-admin`
- 问题类台账：`pwsh scripts/check-problem-classes.ps1`、`ProblemClassRegistryTests`

## 构建命令

```bash
cd Plugins/DbAdmin/web && pnpm run build
cd ForgeSelf.Web && pnpm exec playwright test --config=playwright.config.ts e2e/plugins/db-admin
cd ForgeSelf.Web && pnpm run check && pnpm run test && pnpm run design:check
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj --filter "FullyQualifiedName~ProblemClassRegistryTests"
pwsh scripts/check-problem-classes.ps1
```

## 主要目录职责

| 目录 | 职责 |
| --- | --- |
| `Plugins/DbAdmin/web/src/DbAdminView.vue` | 插件根视图：schema 树、结果表格、表单/审批等全部 UI |
| `Plugins/DbAdmin/web/src/normalize.ts` | 后端契约唯一适配点（columns/rows 归一） |
| `ForgeSelf.Web/src/components/DbAdmin.story.ts` | CT 画廊 fixture（含 `DbAdmin/ReadResult` 有数据行） |
| `ForgeSelf.Web/src/styles/` | 宿主主题与前端级守卫（PC-025 对比度守卫即在此） |
| `docs/11-troubleshooting/` | PC/case 台账与索引 |

## 代码组织方式

- 插件视图为单 SFC，样式 scoped（构建后带 `data-v-*`）；宿主 Tailwind 工具类为全局样式。
- 测试分层：组件逻辑/归一化 → vitest；真实宿主主链路 → Playwright e2e；组件视觉 → CT。

## 现有工程规范

- AGENTS §0/§11：开发任务走 AI-Native 闭环；缺陷类任务叠加 `problem-class-closure`。
- 插件任务维护闭环（`.agents/skills/plugin-development`）：门禁 → 冒烟 e2e → dev-stack 验收 → 文档。
- 问题类台账：`docs/11-troubleshooting/problem-classes/README.md`；Registry 测试要求 PC↔case↔README 三处闭合。

## 适用问题类

- 按 tags 检索：**无既有命中**（PC-019 是 flex/grid 子项尺寸约束，不是类名冲突；PC-025 是主题 token 对比度）。
- 本次新建 **PC-026**（插件语义类名与宿主 Tailwind 工具类同名，table 被改成 display:grid）。

## 选择该任务的原因

用户输入3 直接反馈两处 UI 缺陷（表名前缀、结果表头/内容错列），优先级最高；且实测根因是宿主全局 CSS 类名冲突。

## 候选低风险任务

- 本任务：DbAdmin 模板/CSS + e2e 断言 + 前端 G2 守卫，低风险、可单测/可 e2e 验证，不涉数据/鉴权/架构。
