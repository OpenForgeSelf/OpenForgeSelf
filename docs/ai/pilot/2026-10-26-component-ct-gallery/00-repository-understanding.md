# Repository Understanding

> 阶段：Stage 0（动手写代码之前必须完成）｜规范：docs/04-standards/ai-native-engineering-workflow.md §2
> Task ID：2026-10-26-component-ct-gallery ｜ 日期：2026-10-26
> 原则：所有条目必须来自**真实仓库内容**，禁止凭常识推测。本文件所有事实均已在本回合读文件/列目录确认（Verified）。

## 项目结构

| 位置 | 内容（已确认） |
| --- | --- |
| `ForgeSelf.slnx` + `ForgeSelf.Api/` + `ForgeSelf.Web/` | 单体宿主 + SPA 前端 |
| `ForgeSelf.Api.Tests/` | 后端 xUnit 测试 |
| `Plugins/<X>/`（含 `<X>/web/` 独立前端包） | 插件：后端程序集 + 自带界面 |
| `e2e/`（`ForgeSelf.Web/e2e/`） | Playwright e2e（真实宿主 + 真实前端） |
| `scripts/`（根）与 `ForgeSelf.Web/scripts/` | 发布链与设计守卫脚本 |
| `docs/`、`docs/ai/pilot/` | 知识库与 AI-Native 工件 |

## 技术栈

| 层 | 技术 | 依据（文件/配置） |
| --- | --- | --- |
| 前端 | Vue 3.5 + Vite 6 + TS 5.7 + Element Plus 2.14 + Tailwind 4 + Pinia + vue-router | `ForgeSelf.Web/package.json`、`vite.config.ts` |
| 前端测试 | vitest 3 + @vue/test-utils + jsdom + @playwright/test 1.61 | `ForgeSelf.Web/vitest.config.ts`、`playwright.config.ts` |
| 后端 | .NET 10 + SQLite + NewLife.XCode + 插件架构 | `ForgeSelf.Api/`、`AGENTS.md` §2.1 |

## 架构特点

- 前端为单 SPA：`src/components/`（展示组件，按域分子目录）＋ `src/views/`（页面容器）＋ `src/services/*Api.ts`（请求封装）＋ `src/stores/`（Pinia）。
- 组件模板用 `<ElXxx>`，由 `unplugin-vue-components` + `ElementPlusResolver` 按需解析并注入样式；`eslint.config.mjs` 明文禁止显式 `import { ElXxx }`（`ElMessage/ElMessageBox/ElNotification/ElLoading` 白名单除外）。
- 全局样式装配顺序固定：`element-plus/theme-chalk/dark/css-vars.css` → `el-message-box.css` / `el-message.css` → `styles/themes/workshop-forge.css` → `styles/tailwind.css` → `styles/themes/bg-image-mode.css`（`src/main.ts:21-32`）。
- 主题 token（`--gold-400`/`--fg-primary`/`--dur-base` 等）定义在 `src/styles/themes/workshop-forge.css` 的 `:root`；`.glass` 全局类亦在同文件 `:297`。暗色由 `html.dark` 激活（`src/stores/theme.ts:86`）。

## 测试方式

| 层 | 命令 | 依据 |
| --- | --- | --- |
| 前端类型+lint | `pnpm run check` | `package.json:19` |
| 前端单测/组件测试 | `pnpm run test`（vitest） | `vitest.config.ts`：include `src/**/*.{test,spec}.{ts,tsx}` + `../Plugins/*/web/src/**/*.test.{ts,tsx}`，exclude `**/*.spec.ts` |
| 前端 e2e | `pnpm run test:e2e`（真实后端 7102 + 前端 7002，动态端口） | `playwright.config.ts`（globalSetup + webServer） |
| 设计守卫 | `pnpm run design:check` | `scripts/check-style-tokens.mjs`、`scripts/check-design-compliance.mjs`，扫描范围 = `ForgeSelf.Web/src` + `Plugins/*/web/src` |
| 后端 | `dotnet build` / `dotnet test` | `AGENTS.md` §5.2 |

## 构建命令

```bash
cd ForgeSelf.Web && pnpm run check
cd ForgeSelf.Web && pnpm run test
cd ForgeSelf.Web && pnpm run design:check
cd ForgeSelf.Web && pnpm run test:e2e
```

## 主要目录职责

| 目录 | 职责 |
| --- | --- |
| `ForgeSelf.Web/src/components/` | 展示组件（含 `__tests__/` 少量 vitest 用例） |
| `ForgeSelf.Web/src/views/` | 页面容器（15 个 `.vue`，最大 32 KB） |
| `ForgeSelf.Web/src/composables/` | 组合式函数（当前仅 `useOpenPage.ts`） |
| `ForgeSelf.Web/e2e/` | Playwright e2e + 共享基建（global-setup、dynamic port） |

## 代码组织方式（本任务相关的事实）

1. **没有任何 `*.logic.ts`**（`Get-ChildItem -Filter '*.logic.ts'` 在 `src/` 全树 0 命中）——逻辑目前内嵌在 `.vue` 的 `<script setup>` 或 `services/*Api.ts`。
2. **没有组件故事/CT 基建**：`src/` 内无 `*.story.ts`、无 `*.ct.spec.ts`、无 `playwright-ct.config.ts`、无 `ct/` 目录（列目录 + 命名检索确认）。
3. 已有少量组件单测：`src/components/__tests__/ForgeCard.test.ts`、`PageHead.test.ts`、`settings/__tests__/*.test.ts`、`chatrecords/*.test.ts`。
4. `tsconfig` 分三项目：`tsconfig.app.json`（`src/**`，`types` 仅 `vitest/globals`）、`tsconfig.node.json`（仅 `vite.config.ts`）、`tsconfig.e2e.json`；`tsconfig.json` 用 references 聚合。
5. `eslint.config.mjs` 的 `ignores` 含 `*.config.*`；`ct/**` 与 `ForgeSelf.Web/scripts/**` 均无独立 globals 覆盖（`scripts/**/*.mjs` 已有 node globals 块）。
6. 根 `.gitignore` 已有 `screenshots/`、`playwright-report/`、`test-results/`、`dist/` 等；无 `*-snapshots/` 规则。

## 参考实现（用户指定）

`D:\src\os-proj\X\NewLife.Cube\NewLife.Cube.Vue\web\` 的 CT 体系（已逐文件读取）：

| 文件 | 作用 |
| --- | --- |
| `ct/index.html` | 画廊壳 + 侧栏样式（`#gallery-nav`） |
| `ct/main.ts` | 注册表 + `window.mountStory/setStoryProps/unmountStory` + 侧栏（按 id 前缀分组、>25 默认折叠）+ `?story=` 深链 |
| `ct/stories.ts` | **显式静态 import** 聚合故事（注释说明 `import.meta.glob` 在 HMR 下会出现模块图不稳定、偶发注册失败） |
| `ct/vite.config.ts` | 独立端口 5190、别名桩（`mocks/request.ts`、`mocks/lov-api.ts`）、虚拟模块桩 |
| `playwright-ct.config.ts` | `testDir: ./core/components`、`testMatch: **/*.ct.spec.ts`、`channel: chrome`、`CT_FRESH` 换端口与不复用 server |
| `<Component>/<Name>.story.ts` | story 变体：props + 桩 deps（内存数据、`markRaw` 上下文） |
| `<Component>/<Name>.ct.spec.ts` | 每变体：先行为断言再 `toHaveScreenshot()`；基线目录 `*-snapshots/` 被 gitignore |
| `web/docs/guides/component-visual-dev.md` | 规范：Story 定义、`*.story.ts`/`*.ct.spec.ts`/`*.spec.ts` 后缀隔离、故事标注「测试状态：」、README 功能↔故事↔测试对应表、CT 不截图无断言的禁令 |
| `web/docs/standards/frontend-testable-development.md` | 规范总纲：`index.vue` `<script setup>` ≤50 行、业务逻辑抽 `*.logic.ts` 纯函数、L1–L4 渐进式页面开发、测试范围矩阵、不使用 MSW（用 `vi.mock`/DI） |
| `web/AGENTS.md` | 硬约束 5 条（关注点分离 / CSS token / CubeTable 插槽 / 测试策略 / 命名约定） |

## 现有工程规范（对本任务有约束力）

- `AGENTS.md` §2.4：新增技能必须登记到技能表；§0 第4条：技能清单无对应流程时先创建技能。
- `AGENTS.md` §5.6：门禁分档（本任务触及 `playwright.*.config.ts` 与 `package.json`，需说明档位与理由）。
- `AGENTS.md` §8：设计门禁三步（`design:check` + Spec 交互设计节 + 走查符合性）。
- `docs/04-standards/engineering.md` §1：样式 token 单一来源、组件禁显式 import、全 TS 类型。
- `scripts/check-style-tokens.mjs` / `check-design-compliance.mjs`：扫描 `ForgeSelf.Web/src` 的 `.ts` 文件——**新增 story/spec 文件同样被扫**，禁裸色值与自定义 token 族。
- `docs/04-standards/risk-policy.md` §3：`AGENTS.md` = L4，`.agents/skills/**` = L2，`package.json`/`playwright.*.config.ts` = L3 ⇒ 本任务取最高 L4。

## 候选低风险任务

1. 为 `ForgeCard`/`PageHead` 补 CT 故事（纯展示组件、已有单测、零副作用）。
2. 组件开发规范文档化（`docs/` 级，L0）。
3. 抽取某个视图的纯函数到 `*.logic.ts`（存量视图 15–32 KB，改动面大、回归风险高）。

## 选择该任务的原因

用户明确指令：搭建组件调试框架 + 加规范，使「页面组件与逻辑分离、方便分开测试」。候选 1/2 是该指令的组成部分；候选 3 属存量重构，超出本次范围（记入 TODO 后续单项推进）。本次以「基建 + 规范 + 最小可验证样例（ForgeCard/PageHead/EmptyState）」为范围，避免一次性搬动大视图。
