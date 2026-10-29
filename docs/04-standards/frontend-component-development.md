# 前端组件开发规范（分层 · 可测试 · 视觉回归）

> 状态：已实现（2026-10-26，用户输入12「组件开发按照 Cube.Vue 的逻辑来，页面组件跟逻辑分离，方便分开测试」）
> 最后更新：2026-10-26
> 真源地位：**组件分层、story/CT 组织、页面与逻辑分离**的唯一真源。AGENTS.md 只登记技能与命令入口，不复制本文件细节。
> 参考实现：`D:\src\os-proj\X\NewLife.Cube\NewLife.Cube.Vue\web`（`ct/`、`playwright-ct.config.ts`、`docs/standards/frontend-testable-development.md`）

---

## 1. 四层职责（先分层，再写码）

| 层 | 位置 | 只做什么 | 绝不做什么 | 测法 |
| --- | --- | --- | --- | --- |
| **展示组件** | `src/components/**/*.vue` | 接 `props`、抛 `emits`、渲染 | 请求接口、读路由、`new Date()` 格式化、复杂 `filter/map` 拼装 | vitest（props/slots/emits）+ CT 故事 |
| **页面容器** | `src/views/**/*.vue`、`Plugins/<X>/web/src/*View.vue` | 组合组件、调用逻辑层与组合式、编排加载/空态/错误态 | 内联业务算法、直接拼 URL、把展示细节写进模板判断链 | vitest（挂载 + 桩）或 e2e |
| **纯逻辑** | 同目录 `*.logic.ts`（如 `user.logic.ts`） | 纯函数：入参 → 出参，数据结构转换、校验、格式化 | `ref/computed/watch`、DOM、`fetch`、`import` 组件 | **vitest 纯函数单测（首选）** |
| **组合式** | `src/composables/use*.ts` | 响应式编排：状态、生命周期、加载与重试 | 直接渲染、隐藏 UI 状态语义（状态必须能被组件读到） | vitest + `@vue/test-utils` 挂载宿主组件 |

判据一句话：**能写成纯函数的，就不许写进 `.vue`**；**能靠 props 表达的，就不许组件自己取数**。

## 2. 硬约束（新代码强制，存量棘轮）

1. **页面 `<script setup>` ≤ 50 行**（新增/重构时）。超了说明逻辑该下沉到 `*.logic.ts` 或组合式。
2. **模板里不写业务逻辑**：禁止 `new Date()` 格式化、`fetch(...)`、多层 `filter/map` 拼装、内联正则。模板只允许布尔判断与事件绑定。
3. **逻辑层无框架依赖**：`*.logic.ts` 不 import `vue`、不碰 `document`、不发请求（请求留在 `services/*Api.ts`）。
4. **展示组件不取数**：需要数据时由父级传入 props；确实要请求的组件必须先抽 `use*.ts`，并在测试里桩掉网络。
5. **样式**：颜色只用 Element Plus `--el-*` 与主题 token（`DESIGN.md`），禁裸色值；组件禁显式 `import { ElXxx }`（见 `docs/04-standards/engineering.md` §1）。
6. **类型**：新代码全 TS，不用 `any`；props/emits 必须有类型或运行时声明。
7. **存量豁免**：现有大文件（`src/views/` 下 15–32 KB 的页面）不要求立刻拆；**下次改到哪个文件，就把它按本规范拆到哪**，并在任务工件里记录拆分范围。

## 3. 命名与文件布局

```text
src/components/
├── ForgeCard.vue
├── ForgeCard.story.ts        # 故事：画廊里看到的状态变体
├── ForgeCard.ct.spec.ts      # CT：行为断言 + 像素基线
├── ForgeCard.test.ts         # vitest：渲染契约/交互
└── ct-support.ts             # CT 用例共享助手（只服务 *.ct.spec.ts）
```

| 文件 | 后缀 | 谁收集 | 说明 |
| --- | --- | --- | --- |
| 组件 | `.vue`（PascalCase） | vite | 一个文件一个组件 |
| 纯逻辑 | `*.logic.ts`（小驼峰） | 被组件/测试 import | 纯函数，禁止副作用 |
| 故事 | `*.story.ts` | `ct/stories.ts` 显式注册 | 每条变体必须标 `// 测试状态：…` |
| 组件视觉回归 | `*.ct.spec.ts` | `playwright-ct.config.ts` | 先断言、后 `toHaveScreenshot()` |
| 单测/组件测试 | `*.test.ts` | `vitest.config.ts` | `<script setup>` 之外的行为契约 |
| 端到端 | `e2e/**/*.spec.ts` | `playwright.config.ts` | 真实前后端主链路（零 mock） |

> `*.ct.spec.ts` 以 `.spec.ts` 结尾，`vitest.config.ts` 的 `exclude: ['**/*.spec.ts']` 已把它排除——两套工具互不抓取。

## 4. 测试分工矩阵（按「测什么」选工具，不按习惯选）

| 被测对象 | 工具 | 位置 | 关键要求 |
| --- | --- | --- | --- |
| 纯函数/格式化/拼装 | vitest | 与被测逻辑同目录 `*.test.ts` | AAA 结构；禁 `if/for` 造数据，用 `it.each` |
| 组件渲染与交互 | vitest + `@vue/test-utils` | 同上 | props/slots/emits 断言；网络用 `vi.mock`/依赖注入，**禁 MSW** |
| 视觉形态（多状态） | Playwright CT | `<组件名>.ct.spec.ts` | story 驱动；先行为断言再截图；基线本地生成 |
| 主链路/跨页 | Playwright e2e | `e2e/**` | 真实后端与 token，禁 mock |
| 后端契约 | xUnit | `ForgeSelf.Api.Tests` | 见 `docs/10-testing/strategy.md` |

**选层口诀**：能算清的写纯函数测试；能挂载的写组件测试；只能用眼睛判断的才写 CT；只有跨系统的才写 e2e。

## 5. CT（组件视觉回归）三条约定

1. **故事标注测试状态**：每个变体上方 `// 测试状态：…`，与用例名对应，禁止「Default1/Default2」这种无语义命名。
2. **功能 ↔ 故事 ↔ 测试 可追溯**：被 CT 覆盖的组件，其 story 的变体清单要能对上一句「用户能做成什么」的行为断言；缺断言的截图＝基线会固化错误状态。
3. **确定性**：story 里禁止真实网络、随机数、`Date.now()`、相对时间；图片用 data URL 或 `currentColor` SVG；CT 打开时 `reducedMotion: 'reduce'` 消除动画中间帧。

命令与踩坑见技能 `.agents/skills/component-visual-loop/SKILL.md`；画廊入口见 `ForgeSelf.Web/ct/README.md`。

## 6. 新建组件准入清单（DoR / DoD）

**开工前（Definition of Ready）**
- [ ] 明确它属于「展示组件」还是「页面容器」；若两者混在一起，先拆。
- [ ] 列出所有状态变体（默认/空/失败/只读/长文本/边界值），每个变体一句话。
- [ ] 确认数据来源：props 注入 / `use*.ts` / 页面容器传入（不允许多份来源）。

**交付前（Definition of Done）**
- [ ] 组件源码里没有取数、没有格式化算法（在 `*.logic.ts`）。
- [ ] 有 `*.test.ts`（渲染 + 关键交互），或说明为何免测（纯样式且有 CT）。
- [ ] 有 `*.story.ts` 且已在 `ct/stories.ts` 注册；每个变体标了「测试状态」。
- [ ] 有 `*.ct.spec.ts`（先断言后截图）；基线已用 `test:ct:update` 生成，`test:ct` 二次运行全绿。
- [ ] `pnpm run check`（含 `check-component-conventions.mjs`）、`pnpm run test`、`pnpm run design:check` 全绿。
- [ ] 用户可见界面按 `ui-ux-design` 走查清单核对（CRAP + `DESIGN.md` 组件规格 + 品牌语汇）。

## 7. 与其它规范的关系

| 文档 | 关系 |
| --- | --- |
| `DESIGN.md` | 视觉唯一真源（色板/组件规格/动效/三禁红线）；本规范不重复，只要求「story 里也用 token」 |
| `docs/04-standards/engineering.md` §1 | 样式与导入硬规则；本规范 §2 引用它，不另立一套 |
| `docs/10-testing/strategy.md` | 测试分层与命令；本规范新增「CT 层」，与 vitest/e2e 并列 |
| `docs/04-standards/ai-native-engineering-workflow.md` | 任务流程；组件任务的 Spec 必含「Interaction Design」节，由本规范提供交互规格写法来源 |
| `AGENTS.md` §5.6 | 门禁分档；本规范的第 6 节清单属「快档」必跑项 |

## 8. 命令速查

```bash
cd ForgeSelf.Web
pnpm run ct:server          # 组件画廊（127.0.0.1:5190，HMR）
pnpm run test:ct            # 像素回归
pnpm run test:ct:update     # 更新基线（视觉有意变更）
pnpm run check              # 类型 + lint + 组件规范守卫
pnpm run test               # vitest（组件/纯逻辑）
pnpm run design:check       # 设计 token 守卫
```

## 9. 常见问题

| 现象 | 原因 | 处置 |
| --- | --- | --- |
| 画廊里组件白屏 | story 漏注册 / id 重合 / 依赖 router | 看页面 `Unknown story` 列表；router 依赖改用 props |
| 截图每次都不一样 | 动画中间帧、时间、随机数、真实图片 404 | `reducedMotion: 'reduce'` + 固定数据 + data URL |
| 弹窗残留盖住下个变体 | Element Plus Teleport 到 body | `unmountStory()` 已清理；自定义 Teleport 需在 story 里处理 |
| `pnpm run check` 报「未在 ct/stories.ts 注册」 | 新增 story 未登记 | 在 `ct/stories.ts` 追加 import 与展开项 |
| 插件组件挂不上 | 插件目录没有 `@playwright/test` | 在 `src/components` 建薄宿主壳（见 `AIAgentMessageStoryHost.vue`） |
