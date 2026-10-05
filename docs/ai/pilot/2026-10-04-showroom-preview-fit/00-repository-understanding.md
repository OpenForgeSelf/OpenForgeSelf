# Repository Understanding

> 阶段：Stage 0｜规范：docs/04-standards/ai-native-engineering-workflow.md
> 原则：条目全部来自**本仓库真实内容**（现读现核，行号为 2026-10-05 实测）。

## 涉及模块与真实文件

设计系统插件自带前端（`Plugins/DesignSystem/web/`，Vue SFC，构建为 ESM 由宿主远程加载）：

- `src/showroom/Showroom.vue`：展厅容器。三列网格 `grid-template-columns: 240px minmax(0, 1fr) 240px`（`@media (max-width:1100px)` 才塌单列）；持有 `sceneId / pageId / device / theme / density` 状态并向下传；深链 `#/showroom/<page>?outfit=&theme=&device=` 在此还原。
- `src/showroom/Stage.vue`：舞台。控制条三组 chips（`aria-label="明暗" / "疏密" / "设备"`）+ 两组 `role="tablist"`（场景 / 页面）+ 画布容器 `.ds-stage__viewport`（改前只有 `width:100%; min-width:0; overflow-x:auto`，**无任何缩放**）。
- `src/showroom/DeviceFrame.vue`：设备框，框宽取 `scenes.ts` 的 `DEVICES`（`desktop 1280 / tablet 820 / mobile 390`），**唯一真源**，组件里不写第二份数字。
- `src/showroom/OutfitScope.vue`：把**后端交付文本**的 CSS 收窄到本件衣服作用域（"看到的 == 导出的"同源纪律）。
- `src/showroom/scenes.ts`：`DEVICES` / `deviceById()` / `SCENES`（五类场景 × 页面）。
- `src/shell/mode.ts`：既有 localStorage 惯用法（`ds.mode` 键、读写包 `try/catch` 静默回落）——本次视图档持久化沿用同一写法与命名族。

## 技术栈

| 层 | 技术 | 依据 |
| --- | --- | --- |
| 插件前端 | Vue 3.5 + Vite 6 + TS 5.7（无独立依赖，复用宿主 `node_modules`） | `Plugins/DesignSystem/web/package.json`（scripts 全部 `pnpm -C ../../../ForgeSelf.Web exec ...`） |
| 单测 | Vitest 3.2.6，`environment: 'jsdom'` | `ForgeSelf.Web/vitest.config.ts:28` |
| e2e | Playwright，单配置，`projects: [{ use: { ...devices['Desktop Chrome'], channel: 'chrome' } }]` | `ForgeSelf.Web/playwright.config.ts:58-66` |

## 测试方式（本任务可用的正规入口）

```bash
cd Plugins/DesignSystem/web && pnpm run check && pnpm run test && pnpm run build
cd ForgeSelf.Web && node node_modules/@playwright/test/cli.js test --config=playwright.config.ts e2e/plugins/design-system --workers=1 --output=<空目录>
```

- 插件层 e2e 走**真实宿主 + 真实后端**（globalSetup 自动构建并起 publish 宿主、起前端 dev 7002、注入真实令牌），零 mock；证据固定落 `ForgeSelf.Web/screenshots/e2e/design-system/m2/`（`shot`/`shotOf` + `dumpEvidence`，见 `design-system-helpers.ts`）。
- 展厅既有 e2e 分片：A 片（向导/同源）、B 片（设备框/对比）、C 片（交付/深链/可达性）、D 片（AC24 视觉 QA 矩阵，`test.use({ viewport: 2200x1000 })`）。

## 现状与缺陷位置（实测，非推断）

`:51888` 只读走查（输入21，design-system 3.1.0，视口 1372x768）：画布容器 `ds-stage` = 648x1101（宽占视口 47%），`.ds-stage__viewport` 从 y=334 起高 997 ⇒ 窗口内只剩约 434px 可见；无 `iframe`、无 `transform: scale`、`zoom: 1` ⇒ 1280 的桌面稿按 1:1 塞进 648px 槽，右侧卡片被裁一半。截图 `ForgeSelf.Web/screenshots/live-51888/design-system-3.1.0-showroom-viewport.png`。

## 既有工程约束（必须遵守）

- AGENTS.md §0 插件五步门禁；`design-system-verify` 技能的四层门禁 + 假能力自查表；`e2e-testing` 技能「禁止一次性 temp 脚本充当验证」。
- 插件层样式只走 `--ds-*` 令牌与既有 shell 变量（`--ds-space-* / --ds-fg-2 / --ds-border-1 / --ds-radius-md`），不新增色值。
- DOM 契约（§U）：判据挂在 `data-*` / `role` / `aria-label` 上，e2e 名即判据 ⇒ 新增控件必须同时给 e2e 可寻址的钩子。
