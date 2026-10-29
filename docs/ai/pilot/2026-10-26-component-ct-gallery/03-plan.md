# Plan

> 阶段：Stage 3｜**必须具体到真实文件路径**，禁止只写「修改 Service、增加测试」。
> Task ID：2026-10-26-component-ct-gallery

## Files To Change

### 新增（画廊与 CT 基建）

- file: `ForgeSelf.Web/ct/index.html`
  reason: 画廊壳。含 `#gallery-nav` 侧栏 + `.gal-preview` 挂载区；`html class="dark" data-theme="dark"` 对齐宿主默认暗色主题；样式只用于工具界面（不进 `src/`，不受设计守卫扫描）。
- file: `ForgeSelf.Web/ct/main.ts`
  reason: 注册表 + `window.mountStory/setStoryProps/unmountStory` 契约 + 侧栏渲染（分组/折叠/选中）+ `?story=` 深链 + 重复 id 校验 + Teleport 残留清理；装配与 `src/main.ts` 同序的全局样式，`app.use(createPinia())`，不注册 router。
- file: `ForgeSelf.Web/ct/stories.ts`
  reason: **显式静态 import** 聚合三个组件的故事（禁止 glob）；新增故事在此登记（守卫机械校验）。
- file: `ForgeSelf.Web/ct/types.ts`
  reason: `ComponentStory` 类型（`id` / `component` / `props`），供 `stories.ts` 与各 story 共享，避免各写一份。
- file: `ForgeSelf.Web/ct/vite.config.ts`
  reason: root = `ct/`；port 5190 / strictPort / host 127.0.0.1；别名 `@` → `../src`；`publicDir` → `../public`（`AppLogo` 这类 `/logo/*` 资源可用）；插件与主应用一致（`vue` + `tailwindcss` + `unplugin-auto-import` + `unplugin-vue-components` 的 `ElementPlusResolver`）；两个 unplugin 均 `dts: false`（避免在 `ct/` 生成 `.d.ts` 污染仓库）；`server.fs.allow` 放行 `ct/`、`src/`、`public/`、`node_modules`。
- file: `ForgeSelf.Web/ct/README.md`
  reason: 3 条命令 + 深链语法 + 归档证据的三阶段（不复制规范细节，指向技能与标准文档）。
- file: `ForgeSelf.Web/playwright-ct.config.ts`
  reason: testDir `./src/components`、testMatch `**/*.ct.spec.ts`、`channel: 'chrome'`、`webServer` 起 ct vite、`CT_FRESH` 换端口；与 `playwright.config.ts`（e2e）完全解耦。
- file: `ForgeSelf.Web/tsconfig.ct.json`
  reason: 让 `ct/**/*.ts` 与 `playwright-ct.config.ts` 进 `vue-tsc -b` 类型检查（DOM lib + node types）；避免它们成为「只靠运行时才发现错」的盲区。

### 新增（示例组件接入）

- file: `ForgeSelf.Web/src/components/ForgeCard.story.ts`
  reason: 3 个变体（Default / Interactive / Padding20），覆盖布尔 prop 与数值 prop 的样式分支；每个变体带 `// 测试状态：`。
- file: `ForgeSelf.Web/src/components/PageHead.story.ts`
  reason: 2 个变体（WithEyebrow / TitleOnly），覆盖可选 prop 的显隐分支。
- file: `ForgeSelf.Web/src/components/EmptyState.story.ts`
  reason: 2 个变体（Full / TitleOnly），覆盖 icon/title/sub 三可选 prop 的组合；icon 用 `fill="currentColor"`（零裸色值）。
- file: `ForgeSelf.Web/src/components/ForgeCard.ct.spec.ts`
  reason: 3 个用例。每个用例：打开故事 → 行为断言（`.forge-card` 类 / `interactive` 类 / 内联 padding）→ `toHaveScreenshot()`。
- file: `ForgeSelf.Web/src/components/EmptyState.ct.spec.ts`
  reason: 2 个用例。断言「Full 三行都在」与「TitleOnly 只有标题，无 icon/sub」→ 截图。

### 新增（规范、技能、守卫）

- file: `docs/04-standards/frontend-component-development.md`
  reason: 规范唯一真源（分层职责 / 硬约束 / 命名 / 测试分工矩阵 / CT 三约定 / 准入检查单 / 与 DESIGN.md、10-testing 的关系）。
- file: `.agents/skills/component-visual-loop/SKILL.md`
  reason: 可重复执行的技能流程（何时用 / 三阶段命令 / 加故事 6 步 / 常见坑 / 收口检查）。
- file: `ForgeSelf.Web/scripts/check-component-conventions.mjs`
  reason: 机械守卫（FR6 五条检查），接入 `pnpm run check`。

### 改动（配置与索引）

- file: `ForgeSelf.Web/package.json`
  reason: 增 `ct:server` / `test:ct` / `test:ct:update`；`check` 末尾追加守卫调用。依赖不变。
- file: `ForgeSelf.Web/eslint.config.mjs`
  reason: 增 `ct/**/*.{ts}` 的 browser+node globals 覆盖（否则 `window`/`document`/`process` 触发 `no-undef`）。
- file: `ForgeSelf.Web/tsconfig.json`
  reason: references 追加 `./tsconfig.ct.json`。
- file: `docs/README.md`
  reason: §一「30 秒定位速查」加一行（组件开发规范 + CT 入口）。
- file: `AGENTS.md`
  reason: §2.4 技能表加 `component-visual-loop` 一行（技能不登记 = 等于不存在）。
- file: `.gitignore`
  reason: 增 `*-snapshots/`（CT 像素基线不入库，照 Cube.Vue）。

### 不改（明确边界）

- `ForgeSelf.Web/src/components/{ForgeCard,PageHead,EmptyState}.vue`（组件源码不动）
- `ForgeSelf.Web/vitest.config.ts`（`exclude: ['**/*.spec.ts']` 已排除 `.ct.spec.ts`）
- `ForgeSelf.Web/playwright.config.ts`、`ForgeSelf.Web/e2e/**`、`ForgeSelf.Web/e2e/global-setup.ts`
- `Plugins/**`、`ForgeSelf.Api/**`、版本号、`scripts/`（根）

## Implementation Steps

1. **画廊骨架**：写 `ct/index.html`、`ct/types.ts`、`ct/main.ts`、`ct/vite.config.ts`、`ct/stories.ts`（stories 先空数组）。
2. **跑通空画廊**：`pnpm run ct:server` 起 5190，确认页面不白屏、侧栏显示「0 个故事」（先证明壳可用，再挂组件）。
3. **示例故事**：写三个 `*.story.ts`，在 `ct/stories.ts` 登记；浏览器逐个点开确认渲染与样式 token 生效。
4. **CT 配置**：写 `playwright-ct.config.ts`、`tsconfig.ct.json`，改 `tsconfig.json` 引用。
5. **CT 用例**：写 `ForgeCard.ct.spec.ts`、`EmptyState.ct.spec.ts`；`pnpm run test:ct:update` 生成基线，再 `pnpm run test:ct` 复跑全绿。
6. **守卫**：写 `check-component-conventions.mjs`；改 `package.json`、`eslint.config.mjs`、`.gitignore`。
7. **规范与技能**：写两份文档；改 `docs/README.md`、`AGENTS.md`。
8. **门禁**：`pnpm run check` / `pnpm run test` / `pnpm run test:ct` / `pnpm run design:check` 四条实跑，记录日志正文。
9. **工件收口**：补 `05-evidence.md` / `06-review.md` / `07-final-report.md`。

## Test Plan

| 验证项 | 覆盖 | 命令 |
| --- | --- | --- |
| 类型 + lint + 守卫 | AC4、AC8（顺带） | `cd ForgeSelf.Web && pnpm run check` |
| 单测未被打扰 | AC5（vitest 不收集 `.ct.spec`） | `cd ForgeSelf.Web && pnpm run test` |
| 视觉回归 | AC1、AC3、AC9 | `pnpm run test:ct:update` → `pnpm run test:ct` |
| 设计守卫棘轮 | AC6 | `cd ForgeSelf.Web && pnpm run design:check` |
| 守卫红/绿对照 | AC8 | 临时制造三类违规（探针文件用完即删）→ 期望 `[FAIL]` + exit 1；恢复 → exit 0 |
| 画廊人工浏览 | AC2（侧栏点击） | 浏览器打开 `/`，点第二个条目，核 active 与组件切换；截图存 `ForgeSelf.Web/screenshots/ct-gallery/` |

`test:ct` 自身即覆盖 AC1（`?story=` 深链 + 渲染断言）与 AC9（额外加一条未知 id 用例，断言错误页文案）。

## Verification

### Build

```bash
cd ForgeSelf.Web && pnpm run check
```

判定：`vue-tsc -b`（app/node/e2e/ct 四个 project）+ `eslint .` + `check-component-conventions.mjs` 全部 exit 0；日志正文无 `error`。

### Unit Test

```bash
cd ForgeSelf.Web && pnpm run test
```

判定：全部通过；输出中不出现 `*.ct.spec` 文件（证明 vitest 未误收）。

### Integration Test

N/A —— 依据：本任务不碰后端与 API 契约，无集成测试项目参与（后端集成在 `ForgeSelf.Api.Tests`）。

### E2E

```bash
cd ForgeSelf.Web && pnpm run test:ct        # CT（新增，Playwright 驱动）
```

判定：5 个用例全绿；基线文件生成后二次运行不产生 diff。

**不跑全量 e2e 的理由（需用户确认）**：新建的 `playwright-ct.config.ts` 与 e2e 的 `playwright.config.ts` 无共享代码；未改 `e2e/global-setup.ts`、`e2e/fixtures/**`、`playwright.config.ts`；CT 不启后端、不注入 token。按 `AGENTS.md` §5.6 字面「碰 `playwright.*.config.ts` 即深档」，此处按「新增独立配置、与 e2e 零耦合」判为不需深档，作为待拍板项交用户确认。

### Other Checks

```bash
cd ForgeSelf.Web && pnpm run design:check
```

判定：`style-tokens` 与 `design-compliance` 均 exit 0，且基线计数未上涨（脚本输出不含「超出基线」）。

## Plan 偏差记录

> 实现中发现 Plan 与仓库实际不符时，先在此记录偏差，再修正 Plan，不得直接绕过。

| 时间 | 偏差点 | 原 Plan | 修正后 |
| --- | --- | --- | --- |
| 2026-10-26 | 用户追加要求「用 AI Agent 插件的聊天助手测试」 | 计划只覆盖 3 个宿主基础组件 | 增 `AIAgentMessageStoryHost.vue` + `AIAgentMessage.story.ts` + `AIAgentMessage.ct.spec.ts`，把插件真实 MessageItem 挂进画廊（工具卡/失败续跑/用户气泡 4 变体） |
| 2026-10-26 | 插件 story 放插件目录不可行 | 原定 story/spec 与被测组件同目录（照 Cube.Vue） | 插件目录无 `@playwright/test`（不能新增依赖）⇒ story/spec 落宿主 `src/components`，插件组件由薄壳承接；插件目录零改动 |
| 2026-10-26 | 跨包 import 触发 TS7016 | 未预见 | `tsconfig.app.json` 增 `allowJs: true`（只解析插件 JS SFC，不检查） |
| 2026-10-26 | 三个宿主 spec 需要同一段开页逻辑 | 未列共享助手 | 新增 `src/components/ct-support.ts`（`openStory`：关动效 + 摘侧栏 + 等稳定） |
| 2026-10-26 | AC2/AC9 没有承载用例 | 原计划只测组件，不测画廊壳 | 新增 `Gallery.ct.spec.ts`（3 条）；守卫对「无同名 story」显式豁免该类 |
| 2026-10-26 | `ct/stories.ts` 注释里出现 `import.meta.glob` 字样导致守卫误报 | 守卫按字面包含判定 | 守卫改为先剥注释再判 |
| 2026-10-26 | 用户输入15 要求配置侧边栏：原「深链一律不渲染侧栏」不再满足人类浏览 | 深链不生成侧栏 | 深链保留侧栏并高亮；CT 统一用 `&chrome=0` 关闭（`ct-support.ts`），独立工件 `2026-10-26-ct-nav-config` |
