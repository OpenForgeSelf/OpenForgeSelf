# Intent

> 阶段：Stage 1｜只描述「为什么做 / 做什么 / 做到什么程度」，**不提前决定具体代码实现**。
> Task ID：2026-10-26-component-ct-gallery ｜ 日期：2026-10-26

## Problem

1. 前端组件没有「脱离业务页面单独看效果」的手段：改一个卡片/页头只能起整套 dev-stack、登录、点进某个页面才能看到，改样式靠肉眼反复试。
2. 组件与业务逻辑混在同一个 `.vue` 里：`src/views/` 下 15 个页面文件最大 32 KB，全仓 **0 个 `*.logic.ts`**，纯计算、数据拼装、格式转换都写在 `<script setup>` 里，只能整体挂载测试，无法单测纯函数。
3. 视觉回归没有组件级卡点：e2e 只覆盖主链路，样式漂移（字号、间距、token 失效）只能靠人眼或读 e2e 截图发现；`toMatchSnapshot` 这类结构快照对样式无感知。

## Why

- 组件是前端复用的最小单位，必须有「传一组 props 就能看到全部状态变体」的确定性入口，才能把设计走查、像素基线、评审截图从「跑整站」降级为「单组件秒看」。
- 项目已具备 `@playwright/test`、`@vue/test-utils`、`vite`，自建 Gallery（≈100 行）即可获得 Storybook 的核心能力，不需要新依赖。
- 用户要求「组件开发按照 Cube.Vue 的逻辑来、页面组件与逻辑分离、方便分开测试」：Cube.Vue 已被验证的做法（`*.story.ts` / `*.ct.spec.ts` / `*.logic.ts` / 后缀隔离 / `index.vue` ≤50 行）可以直接移植，避免自创一套。

## Expected Outcome

1. `cd ForgeSelf.Web && pnpm run ct:server` 打开 `http://127.0.0.1:5190/`，左侧列出全部已登记故事，点击即渲染；`?story=<id>` 深链只渲染该变体（供 Playwright 截图，不受侧栏干扰）。
2. `pnpm run test:ct` 对每个变体做「行为断言 + 像素基线」双验证；`pnpm run test:ct:update` 更新基线。
3. 至少三个真实组件（`ForgeCard` / `PageHead` / `EmptyState`）接入，形成可复制的样例。
4. 一份唯一真源规范：分层（展示组件 / 页面容器 / 纯逻辑 / 组合式）、命名后缀、测试分工、CT 三条约定、准入检查单；并被 `docs/README.md` 与 `AGENTS.md` 技能表登记。
5. 一个可重复执行的技能流程（`.agents/skills/component-visual-loop/SKILL.md`）+ 一个机器守卫（story 必须登记、spec 必须有同名 story、story 必须标注测试状态）。

## Constraints

| 约束 | 依据 |
| --- | --- |
| 不新增依赖（不引 Storybook、不引 @playwright/experimental-ct-vue） | 规范 §1 第4条；现有 `@playwright/test`/`vite`/`@vue/test-utils` 足够 |
| 不改业务组件实现（本次只在组件旁新增 story/spec，组件源码不动） | 范围控制；避免把「搭框架」变成「改组件」 |
| 不覆盖插件前端（`Plugins/*/web`） | 插件前端是独立包、独立构建；本次只服务宿主 `ForgeSelf.Web` |
| 不改 e2e 基建（`e2e/global-setup.ts`、`playwright.config.ts`、fixtures） | CT 是纯前端静态画廊，不依赖后端 |
| story/spec 文件位于 `src/` ⇒ 受设计守卫扫描 | 禁裸色值（hex/rgb/oklch/hsl）、禁自定义 token 族 |
| 不修改生产/DB/鉴权 | 规范 §1 第1–3条 |
| 文档规范只写一处真源，不在 AGENTS.md 复制细节 | AGENTS.md §7.5；Cube.Vue 的「AGENTS 只做索引」经验 |

## Success Criteria

1. `pnpm run ct:server` 可在 5190 启动，`?story=ForgeCard/Default` 渲染出 `.forge-card`（浏览器实测）。
2. `pnpm run test:ct` 基线生成后二次运行全绿；截图文件落在 `src/components/<Name>.ct.spec.ts-snapshots/`。
3. `pnpm run check`（vue-tsc + eslint + 新守卫）、`pnpm run test`（vitest）、`pnpm run design:check` 三者全绿。
4. 规范文档存在且含：分层职责表、命名约定、测试范围矩阵、CT 三约定、准入清单；`docs/README.md` §一 与 `AGENTS.md` §2.4 各有一行指向它。
5. 故意制造三类违规（story 未登记 / ct.spec 无 story / story 缺测试状态注释）时守卫 `exit 1` 并指出文件；恢复后 `exit 0`。
