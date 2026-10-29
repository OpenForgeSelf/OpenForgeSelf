# Specification

> 阶段：Stage 2｜必须从真实 Repository Understanding 与 Intent 推导。
> 规则：① 所有内容与实际项目一致；② 不得发明不存在的接口、类、模块；③ 不确定点显式记录为 `Unknown`，不得自行假定。
> Task ID：2026-10-26-component-ct-gallery

## Functional Requirements

### FR1 组件画廊（Gallery）

1. 新增 `ForgeSelf.Web/ct/`，含 `index.html`、`main.ts`、`stories.ts`、`vite.config.ts`。
2. `pnpm run ct:server` = `vite --config ct/vite.config.ts`，监听 `127.0.0.1:5190`，`strictPort: true`。
3. 无 `?story=` 参数：渲染左侧故事侧栏（`#gallery-nav`）＋默认选中并挂载第一个故事；侧栏按 story id 的 `/` 前缀分组，故事总数 > 25 时分组默认折叠。
4. `?story=<id>`：人类浏览时保留侧栏并高亮当前故事；`&chrome=0` 时不生成侧栏、只挂载该故事（CT 截图用）；未知 id 时在页面内列出全部可用 id（可诊断，不白屏）。（2026-10-26 输入15 调整，见 `docs/ai/pilot/2026-10-26-ct-nav-config/`）
5. 浏览器侧契约（供 Playwright 使用，与 Cube.Vue 一致）：
   - `window.mountStory(id, props?)`：卸载当前故事 → 新建挂载点 `.gal-preview` → `createApp({ render: () => h(component, reactiveProps) })` → `mount`。
   - `window.setStoryProps(patch)`：浅合并到当前 props（`Object.assign`）。
   - `window.unmountStory()`：卸载 app、移除挂载点、清理 Teleport 到 `body` 的 `.el-overlay` / `.el-dialog__wrapper` / `.v-modal` 残留。
6. 注册表重复 id 必须显式报错（`registry.size !== stories.length` 时抛错），不静默覆盖。
7. Gallery 必须装配与 `src/main.ts` 一致的全局样式链（顺序同主应用：Element Plus 暗色变量 → `el-message-box.css` / `el-message.css` → `workshop-forge.css` → `tailwind.css` → `bg-image-mode.css`），并创建 Pinia 实例（`app.use(createPinia())`）。**不注册 vue-router**。

### FR2 故事（Story）注册与命名

1. 故事文件与被测组件同目录，命名 `<组件名>.story.ts`，导出 `stories` 数组，元素形状 `{ id: string; component: Component; props?: Record<string, unknown> }`。
2. story id 规范：`<组件名>/<变体名>`（如 `ForgeCard/Default`），全库唯一。
3. 每个变体上方必须有 `// 测试状态：…` 注释，说明该变体验证的运行时状态。
4. `ct/stories.ts` 用**显式静态 import** 聚合（禁止 `import.meta.glob`，理由：HMR 下模块图不稳、偶发漏注册）。
5. 新增故事必须同步在 `ct/stories.ts` 追加 import 与展开项（由 FR6 守卫机械校验）。

### FR3 组件视觉回归（CT）

1. 新增 `ForgeSelf.Web/playwright-ct.config.ts`：`testDir: './src/components'`、`testMatch: '**/*.ct.spec.ts'`、`baseURL` = `http://127.0.0.1:<port>/`、`channel: 'chrome'`、`webServer` 起 FR1 的 vite。
2. 端口策略照 Cube.Vue：默认 5190；`CT_FRESH=1` 时改用 5193 且 `reuseExistingServer: false`（绕开陈旧 vite 假象）。
3. 每个测试必须**先行为断言**（文本/类名/元素计数），再 `toHaveScreenshot()`；禁止只有截图没有断言的用例。
4. 打开故事前必须 `page.emulateMedia({ reducedMotion: 'reduce' })` 并移除 `#gallery-nav`（它在深链下是空壳但样式仍占位，会拦截指针事件），等待状态稳定（组件挂载动画结束）后再截图。
5. 截图基线目录 `<spec 文件名>-snapshots/` 由 Playwright 生成，**不进版本控制**（根 `.gitignore` 增加 `*-snapshots/` 规则）。
6. 命令：`pnpm run test:ct`（对比）、`pnpm run test:ct:update`（更新基线）。

### FR4 组件开发规范（唯一真源文档）

新增 `docs/04-standards/frontend-component-development.md`，至少含：

1. **分层职责**：展示组件（`src/components/**`，props in / emits out，不取数不路由）／页面容器（`src/views/**`，组合与编排）／纯逻辑（`*.logic.ts`，纯函数、无 Vue/DOM 依赖）／组合式（`composables/use*.ts`，响应式编排）。
2. **硬约束**：新增/重构的页面 `<script setup>` ≤ 50 行；模板与脚本内禁业务逻辑（`new Date()` 格式化、`fetch`、复杂 `filter/map` 拼装）；存量文件按棘轮豁免、随重构逐步迁移。
3. **命名**：`*.story.ts`（故事）、`*.ct.spec.ts`（Playwright CT）、`*.test.ts`（vitest）、`*.logic.ts`（纯逻辑）；组件文件 PascalCase。
4. **测试分工矩阵**：纯逻辑 → vitest 纯函数；组件渲染/交互 → vitest + `@vue/test-utils`（props/slots/emits，禁真实网络，用 `vi.mock`/依赖注入，**禁 MSW**）；视觉 → CT story + `toHaveScreenshot`；主链路 → e2e（零 mock）。
5. **CT 三条约定**：故事标注测试状态、每个被 CT 覆盖的组件需「功能 ↔ 故事 ↔ 测试」对应表、CT 先断言后截图。
6. **准入检查单（新建组件）**：组件纯展示？逻辑在 `*.logic.ts`？有 `*.test.ts`？有 story？有 ct.spec？更新了 `ct/stories.ts`？跑过 `pnpm run check` / `test` / `test:ct` / `design:check`？
7. 与 `DESIGN.md`、`docs/10-testing/strategy.md` 的关系：本文件只新增「组件分层 + CT」内容，不复制既有 token/测试铁律，用链接引用。

### FR5 技能流程

新增 `.agents/skills/component-visual-loop/SKILL.md`：触发条件、三阶段（`ct:server` 看效果 → `test:ct:update` 锁基线 → `test:ct` 防回归）、加故事步骤、常见坑（`markRaw`、Teleport 残留、非确定性数据/时间、`import.meta.glob`、`./scripts` 禁裸色值、reducedMotion）。并在 `AGENTS.md` §2.4 技能表登记一行。

### FR6 机器守卫

新增 `ForgeSelf.Web/scripts/check-component-conventions.mjs`，纯 Node（无新依赖），检查：

1. `src/**/*.story.ts` 中每个文件都必须被 `ct/stories.ts` 文本引用（按文件名匹配 import 路径）。
2. 每个 `src/**/*.ct.spec.ts` 必须存在同名 `.story.ts`（同目录）。
3. 每个 `src/**/*.story.ts` 至少含一处 `// 测试状态：`。
4. `ct/stories.ts` 不得出现 `import.meta.glob`。
5. 违规时打印 `[FAIL] 文件: 原因` 并列全部问题，`exit 1`；通过则打印统计并 `exit 0`。
6. 接入 `pnpm run check`（`vue-tsc -b && eslint … && node scripts/check-component-conventions.mjs`）。

## Input

| 输入 | 来源 |
| --- | --- |
| 用户指令（仿 Cube.Vue `web/ct` 搭建调试框架 + 加规范 + 页面组件与逻辑分离） | 用户消息 2026-10-26（日记 输入 12） |
| 参考实现 | `D:\src\os-proj\X\NewLife.Cube\NewLife.Cube.Vue\web\{ct,playwright-ct.config.ts,docs/guides/component-visual-dev.md,docs/standards/frontend-testable-development.md,AGENTS.md}` |
| 现有前端装配与守卫 | `ForgeSelf.Web/vite.config.ts`、`src/main.ts`、`eslint.config.mjs`、`tsconfig*.json`、`scripts/check-style-tokens.mjs`、`check-design-compliance.mjs` |
| 被测组件（只读） | `src/components/ForgeCard.vue`、`PageHead.vue`、`EmptyState.vue` |

## Output

| 产出 | 类型 |
| --- | --- |
| `ForgeSelf.Web/ct/{index.html,main.ts,stories.ts,vite.config.ts,types.ts,README.md}` | 新文件（画廊） |
| `ForgeSelf.Web/playwright-ct.config.ts`、`ForgeSelf.Web/tsconfig.ct.json` | 新文件（CT 运行与类型检查） |
| `ForgeSelf.Web/src/components/{ForgeCard,PageHead,EmptyState}.story.ts` | 新文件（故事） |
| `ForgeSelf.Web/src/components/{ForgeCard,EmptyState}.ct.spec.ts` | 新文件（视觉回归） |
| `ForgeSelf.Web/scripts/check-component-conventions.mjs` | 新文件（守卫） |
| `ForgeSelf.Web/package.json`（scripts：`ct:server`/`test:ct`/`test:ct:update`/`check` 追加守卫） | 改动 |
| `ForgeSelf.Web/eslint.config.mjs`（`ct/**/*.ts` browser globals 覆盖） | 改动 |
| `ForgeSelf.Web/tsconfig.json`（追加 `tsconfig.ct.json` 引用） | 改动 |
| `docs/04-standards/frontend-component-development.md` | 新文件（规范真源） |
| `.agents/skills/component-visual-loop/SKILL.md` | 新文件（技能） |
| `docs/README.md`（§一 加一行）、`AGENTS.md`（§2.4 加一行） | 改动 |
| `.gitignore`（`*-snapshots/`） | 改动 |

## Business Rules

1. **单一真源**：组件分层与 CT 规范只在 `docs/04-standards/frontend-component-development.md` 承载；AGENTS.md 只登记技能名，不复制细节。
2. **后缀隔离**：`*.test.ts` → vitest；`*.ct.spec.ts` → Playwright CT；`*.story.ts` → 画廊。任一工具不得抓取其它后缀（`vitest.config.ts` 已排除 `**/*.spec.ts`，CT 配置只抓 `.ct.spec.ts`）。
3. **所见即所测**：画廊与 CT 共用同一份 story 与同一份 vite 配置；画廊除「挂载壳」外不得给组件注入额外样式/包装（避免基线偏离真实页面）。
4. **组件源码不动**：story 只能通过 props/slots 表达状态；组件本身不足时先记为 TODO，不在本任务改。
5. **确定性**：story 中禁止 `new Date()`、随机数、真实网络、相对时间；图片用 data URL 或 `currentColor` SVG。
6. **零新增依赖**：复用现有 `@playwright/test`、`vite`、`@vue/test-utils`、`unplugin-*`。

## Boundary Conditions

| 边界 | 期望行为 |
| --- | --- |
| 5190 端口被占用 | vite `strictPort` 直接失败并提示端口占用；`CT_FRESH=1` 换 5193 |
| 未知 story id | 页面显示 `Unknown story: <id>` + 可用 id 列表（不白屏） |
| 重复 story id | `main.ts` 启动即抛错（不静默覆盖） |
| 故事数为 0 | 侧栏显示标题与空列表；不抛错 |
| 故事数 > 25 | 分组默认折叠，首个故事所在组自动展开并选中 |
| 组件依赖 router/store | 画廊只提供 Pinia，不提供 router；此类组件需 story 内以 props/桩注入，或记为暂不接入 |
| 组件含异步首查 | story 用 `markRaw` 传上下文 + 固定数据；CT 打开后等待稳定态再截图 |
| 元素含 `ElDialog` 等 Teleport | `unmountStory()` 清理 `body` 下 overlay，避免污染下一个故事截图 |
| Reduced motion | CT 强制 `reducedMotion: 'reduce'`，组件动画关闭，避免中间帧 |

## Error Handling

| 失败点 | 处理 |
| --- | --- |
| vite 启动失败（端口/依赖） | 命令非零退出，控制台保留 vite 原始报错；不以「重试」掩盖 |
| story 引用了不存在的组件 | `main.ts` 注册表构建期抛错；CT 首屏即失败失败 |
| 截图基线缺失 | `test:ct` 失败并提示用 `test:ct:update` 生成 |
| 守卫发现违规 | `[FAIL]` 列表 + `exit 1`，阻断 `pnpm run check` |
| 守卫自身异常（读文件失败） | 直接抛出非零退出，不吞异常 |

## Compatibility

1. 不修改 `playwright.config.ts`、`e2e/**`、`vitest.config.ts`：e2e 与单测行为不变。
2. `ct/` 与 `playwright-ct.config.ts` 不参与 `vite build`（生产构建只打包 `src` 中被应用引用的模块），前端产物不变。
3. `src/**/*.story.ts` / `*.ct.spec.ts` 会被 `vue-tsc -b`（`tsconfig.app.json` include `src/**`）类型检查——必须类型安全，不允许 `@ts-ignore`。
4. `src/**/*.ct.spec.ts` 不会被 vitest 收集（现有 `exclude: ['**/*.spec.ts']` 已覆盖）。
5. `scripts/check-style-tokens.mjs` 与 `check-design-compliance.mjs` 会扫描新增 story/spec（`.ts`），故两者零新增违规计数（棘轮基线不涨）。
6. `.gitignore` 新增 `*-snapshots/` 只影响 CT 基线目录，不改变其它路径。

## Non-functional Requirements

1. 启动速度：`ct:server` 冷启动不超过 15 s（仅 vue/tailwind/EP resolver，无重型插件）。
2. 单次 `test:ct` 全量 ≤ 30 s（2 个 spec、5 个用例以内）。
3. 守卫脚本在 1000 个前端文件量级下 ≤ 2 s，只扫描 `src/**` 与 `ct/stories.ts`。
4. 新文件零新增 npm 依赖（`package.json` 的 dependencies/devDependencies 不变）。
5. 文档：中文、与仓库既有风格一致（表格 + 硬约束条目）；所有命令可直接复制运行。

## Interaction Design（交互设计 · 必写节 · 2026-10-08 用户立）

> 本任务产生用户可见界面 = 组件画廊页面（开发者工具界面）。逐项写「点什么出现什么」。

### 交互规格（点什么出现什么）

| 交互点 | 触发 | 结果（点什么出现什么） | 状态模型 | 反馈 | 空态 | 边界 |
| --- | --- | --- | --- | --- | --- | --- |
| 打开画廊 | 浏览器访问根路径 `/` | 左侧侧栏（标题 + 分组列表）+ 右侧默认挂载第一个故事；首个故事高亮 | 已选中 1 个 | 无 toast；组件即时渲染 | 无故事时侧栏仅标题、右侧空白 | 故事 > 25 时分组折叠，仅首组展开 |
| 点击故事条目 | 点击侧栏 `li` | 取消其它高亮 → 当前条目高亮 → 右侧卸载旧故事并挂载新故事 | 单选中 | 条目背景变强调色 | — | 未知 id 时显示错误页并列出可用 id |
| 展开/收起分组 | 点击分组头 | 该组列表显示/隐藏，箭头 `▾/▸` 切换，不影响其它组 | 每组建独立开合 | 箭头即时切换 | — | 组内 0 项时不渲染组 |
| 深链单故事 | 访问 `/?story=<id>` | 人类浏览保留侧栏并高亮当前故事；`&chrome=0` 时只渲染该故事（供截图） | 单故事 | 当前条目高亮 | 未知 id → 错误页列出可用 id | id 含 `/` 需 URL 编码（测试里 `encodeURIComponent`） |
| 无参数浏览 | 直接打开根路径 | 与「打开画廊」同（侧栏模式），支持 HMR | 单选中 | 改 `.vue` 即热更新 | — | — |
| 命令失败 | 5190 被占 / 基线缺失 | 终端输出 vite / Playwright 原始报错，非零退出 | 失败 | 明确错误信息 | — | `CT_FRESH=1` 换端口不复用 |

### 交互验收标准（并入 Acceptance Criteria，逐条可测）

- AC1/AC2 覆盖「打开画廊 / 点击条目 / 深链」三条路径，断言锚在真实 DOM（`.gal-preview`、`.forge-card`、`#gallery-nav` 移除后无拦截）。
- 空态与边界：故事数 0 与未知 id 各有可测表现（错误页含可用 id 列表）。
- 失败必须可见原因（vite/Playwright 原始报错），不静默。

### 走查符合性（DoD 绑定）

- 画廊本身是开发者工具界面，按 `ui-ux-design` 走查清单核：侧栏层级清晰（分组/选中态可辨）、点击反馈即时、无遮挡（挂载区不与侧栏重叠）、窄屏不破版（侧栏固定 250px + 挂载区左内边距）。
- 被测组件按 `DESIGN.md` 规格：截图读图核对 token 生效（`--gold-400`、`--fg-primary`、玻璃卡边框/圆角），不以「能显示」为通过。

## Acceptance Criteria

- **AC1**：`pnpm run ct:server` 在 `127.0.0.1:5190` 起服务；访问 `/?story=ForgeCard/Default` 后 `.gal-preview .forge-card` 可见且侧栏高亮该条目；追加 `&chrome=0` 时页面不生成 `#gallery-nav`（2026-10-26 输入15 调整，见 `docs/ai/pilot/2026-10-26-ct-nav-config/`）。
- **AC2**：访问根路径 `/` 时 `#gallery-nav` 存在且列出全部已登记 story（数量 = `stories.ts` 聚合长度）；点击第二个条目后 `.gal-preview` 内组件类型随之切换、该条目获得 active 态。
- **AC3**：`pnpm run test:ct:update` 生成基线后，`pnpm run test:ct` 全绿；每个用例同时含行为断言与 `toHaveScreenshot()`。
- **AC4**：`pnpm run check` 通过（vue-tsc 全 4 个 tsconfig 项目 + eslint + `check-component-conventions.mjs`）。
- **AC5**：`pnpm run test` 全绿，且 vitest 未收集任何 `*.ct.spec.ts`（输出中不出现 `.ct.spec`）。
- **AC6**：`pnpm run design:check` 通过且棘轮基线计数**未上涨**（story/spec 零裸色值、零自定义 token）。
- **AC7**：`docs/04-standards/frontend-component-development.md` 存在并含 FR4 的 7 项内容；`docs/README.md` §一 与 `AGENTS.md` §2.4 各有一行指向它/技能。
- **AC8**：守卫三类违规各造一次（story 未登记 / ct.spec 无 story / story 缺「测试状态：」）均 `exit 1` 且指名文件；恢复后 `exit 0`（探针文件用完即删、不入库）。
- **AC9**：未知 story id（`?story=NotExist/X`）页面显示 `Unknown story` 与可用 id 列表，不白屏。

## Unknown

| 不确定点 | 影响 | 处理方式（询问/搁置/保守假设并标注） |
| --- | --- | --- |
| U1 本机 5190 是否被占用 | `ct:server` 与 `test:ct` 能否直接跑 | 实现后实测；被占则用 `CT_FRESH=1`，最终以实测端口写 Evidence |
| U2 是否需要为组件提供 Pinia/router | 决定能接入哪些组件 | 保守假设：提供 Pinia、不提供 router；需要 router 的组件暂不接入并记 TODO |
| U3 截图基线是否入库 | 跨机器/CI 稳定性 | 保守假设：不入库（照 Cube.Vue），`test:ct:update` 本地生成；待用户拍板 |
| U4 CT 是否接入 CI | 门禁强度 | 保守假设：不接入（CI 无 Chrome 保证），本次只做本地开发卡点 |
| U5 「≤50 行」是否对存量视图强制 | 存量 15 个大视图 | 保守假设：仅新增/重构强制，存量按棘轮随重构迁移，不在本任务改视图 |
| U6 tsconfig.ct 与 tsconfig.app 的 `.vue` 共享编译 | 类型检查是否重复报错 | 保守假设：允许同一文件进两个 program（均 noEmit），实测 `pnpm run check` 全绿即成立 |
