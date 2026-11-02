---
name: component-visual-loop
description: 前端组件可视化开发闭环（画廊预览 → 像素基线 → 防回归）。当用户要求「调试组件」「组件单独看效果」「看这个组件长什么样」「加组件视觉回归」「story」「CT」「截图对比组件」「新组件怎么测」时使用。规范真源 docs/04-standards/frontend-component-development.md，画廊在 ForgeSelf.Web/ct/，CT 配置 playwright-ct.config.ts。
version: 1.0.0
---

# 组件可视化开发闭环（component-visual-loop）

> 规范：`docs/04-standards/frontend-component-development.md`（分层/命名/测试分工，唯一真源）
> 入口：`ForgeSelf.Web/ct/`（画廊）｜`ForgeSelf.Web/playwright-ct.config.ts`（CT）

## 何时用

- 要**单独看一个组件**长什么样（不想起整个 dev-stack、不想登录点进页面）
- 组件有多个状态（默认/空/失败/只读/长文本/流式中）需要逐个人眼看
- 改样式后要**防止以后被改坏**（像素基线卡点）
- 新增组件需要走「逻辑分层 + 故事 + CT」准入清单

不适用：纯逻辑改动（写 vitest 纯函数单测即可）、跨页主链路（走 `e2e-testing`）。

## 三阶段（开发期 / 留证据 / 防回归）

| 阶段 | 命令 | 基线 | 用途 |
| --- | --- | --- | --- |
| 正在开发 | `pnpm run ct:server` → `http://127.0.0.1:5190/` | 不需要 | 点侧栏看组件，改 `.vue` 即 HMR |
| 要留证据 | `pnpm run test:ct:update` | 生成 | 把当前形态锁成基线（视觉有意变更时） |
| 防回归 | `pnpm run test:ct` | 对比 | 任何像素漂移即失败 |

深链：`/?story=<id>`（不渲染侧栏，供用例与分享）。未知 id 会列出全部可用 story。

## 加一个组件的故事（6 步）

1. **先分层**：组件是否纯展示？取数/算法抽到 `*.logic.ts` 或 `use*.ts`（规范 §1/§2）。
2. **列状态**：默认 / 空 / 失败 / 只读 / 长文本 / 边界值 —— 每个状态一条变体，写在组件旁 `<组件名>.story.ts`。
3. **写变体**：`{ id: 'Component/Variant', component, props?, slots? }`；每条上方 `// 测试状态：…`；
   颜色只用 token（story 文件在 `src/` 下，受设计守卫扫描）。
4. **注册**：在 `ct/stories.ts` 追加 `import` 与展开项（守卫 `check-component-conventions.mjs` 会拦漏注册）。
5. **写 CT**：`<组件名>.ct.spec.ts`，`openStory(page, id)` → **先行为断言**（文本/类/计数）→ `toHaveScreenshot()`。
6. **锁基线**：`pnpm run test:ct:update` → `pnpm run test:ct` 复查必须全绿。

## 跨包（插件前端）组件怎么挂

插件目录（`Plugins/<X>/web`）**没有也不该新增** `@playwright/test`，因此：

1. 在 `ForgeSelf.Web/src/components/` 建薄宿主壳 `<Xxx>StoryHost.vue`，import 插件组件；
2. 壳里补上组件需要的外层容器与样式（如 AIAgent 的 `.aiw-root` + `widget.css/blocks.css`）；
3. story 与 ct.spec 都放宿主 `src/components/`（用例必须能解析 `@playwright/test`）；
4. 壳内固定画布尺寸（如 `width: 560px`），否则基线随视口漂移。

样例：`src/components/AIAgentMessageStoryHost.vue`（AIAgent 聊天消息，含工具卡与正文共存的回归守卫）。

## 常见坑（都已实测）

| 坑 | 症状 | 处置 |
| --- | --- | --- |
| `import.meta.glob` 聚合故事 | 偶发「未知 story」、HMR 下漏注册 | 一律显式 import（`ct/stories.ts` 已禁用 glob） |
| 挂载动画 | 截图随机半透明/位移 | 用例 `emulateMedia({ reducedMotion: 'reduce' })`，`openStory` 已封装 |
| Element Plus 浮层残留 | 下个变体截图被弹窗盖住 | `unmountStory()` 清理 `body > .el-overlay/.el-dialog__wrapper/.v-modal` |
| 非确定性数据 | 每次截图都 diff | 固定时间字符串、data URL 图片、`currentColor` SVG、内存数据 |
| 组件依赖 router/store | 挂载即报错或空白 | 画廊只提供 Pinia；router 依赖改用 props 注入 |
| 侧栏占位 | 深链下点击超时 | 深链不渲染侧栏；`openStory` 再兜底摘一次 |
| 裸色值写进 story | `design:check` 棘轮上涨 | 用 `var(--el-*)` / `var(--gold-400)` 等 token |
| 5190 陈旧 vite | 改了源码截图不变 | `CT_FRESH=1 pnpm run test:ct`（换 5193 且不复用 server） |

## 收口检查（缺一不可）

```bash
cd ForgeSelf.Web
pnpm run check        # 类型 + lint + 组件规范守卫（story 注册/配对/标注）
pnpm run test         # vitest 不收集 *.ct.spec（输出里不应出现 .ct.spec）
pnpm run test:ct      # 像素回归全绿
pnpm run design:check # 设计 token 棘轮未上涨
```

再补两件人工确认：

- **读图**：打开 `src/components/*.ct.spec.ts-snapshots/*.png` 逐张看（不只是「测试绿了」）；
- **走查**：画廊本身按 `ui-ux-design` 核对（侧栏层级、选中态、挂载区不被遮挡），组件按 `DESIGN.md` 核对 token 生效。

## 与其它技能的分工

| 场景 | 用哪个 |
| --- | --- |
| 组件开发/预览/视觉回归 | 本技能 |
| 真实前后端主链路验收 | `e2e-testing` |
| 设计规范审查（CRAP/配色/字体） | `ui-ux-design` |
| 插件从立项到发布 | `plugin-development`（本技能服务其「前端界面」环节） |
