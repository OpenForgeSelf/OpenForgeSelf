# Repository Understanding

> 阶段：Stage 0｜Task ID：PILOT-plugin-ui-audit｜只出工件，不执行修复。

## 项目结构

- 插件后端：`Plugins/<X>/`（含 `plugin.json`）；插件前端：`Plugins/<X>/web/src` → 产物 `web/dist/index.js`（宿主远程加载）。
- 宿主：`ForgeSelf.Web`（Vue 3）；主题 token 单一来源：`ForgeSelf.Web/src/styles/themes/workshop-forge.css`。
- 守卫：`scripts/check-style-tokens.mjs`（棘轮基线 `scripts/style-token-baseline.json`）｜`scripts/check-design-compliance.mjs`（A–D）｜主题对比度 vitest `workshop-forge.contrast.test.ts`。

## 技术栈

Vue 3.5 + Vite 6 + TS 5.7 + Element Plus 2.14 + Tailwind 4；测试 = vitest / Playwright CT（`ForgeSelf.Web/ct`，固定暗色）/ Playwright e2e（真实 publish 宿主，零 mock）。

## 架构特点

插件 UI 与宿主共用 `--el-*` token，插件内联 `<style scoped>`；CT 只覆盖宿主薄壳挂载的少数插件视图；e2e 每插件 1 条冒烟主链路。

## 测试方式

- 纯逻辑：vitest；组件状态/视觉：CT（`ForgeSelf.Web/src/components/*.story.ts` + `*.ct.spec.ts`）；运行态：`e2e/plugins/<id>/<id>.spec.ts`。

## 构建命令

`pnpm run check`｜`pnpm run test`｜`pnpm run design:check`｜插件前端 `cd Plugins/<X>/web && pnpm run build`｜e2e `pnpm run test:e2e`。

## 主要目录职责

| 路径 | 职责 |
|---|---|
| `Plugins/<X>/web/src` | 插件前端源码（含样式） |
| `ForgeSelf.Web/ct/stories.ts` | CT 故事登记表（当前仅 6 组） |
| `ForgeSelf.Web/e2e/plugins/<id>` | 插件冒烟 e2e（当前 13 个） |
| `scripts/style-token-baseline.json` | 各目录硬编码色值棘轮计数 |

## 代码组织方式

插件视图单文件组件 + scoped 样式；颜色应引用 token，禁止裸色值（新增即被 `check-style-tokens.mjs` 拦）。

## 现有工程规范

`DESIGN.md` §Colors 使用规则（暗色 light 语义、禁止把 light-N 当文字色）｜`AGENTS.md` §8 设计门禁三步｜`ui-ux-design` 走查清单（双态、异步 loading、空态不抢先）。

## 候选任务

逐个插件核对 UI 规范（背景/文字/主题配对、五态与 loading），产出修复清单——本批仅出工件。

## 选择该任务的原因（实测盘点）

- 带前端插件 **14 个**：AgentHub、AIAgent、CostScope、DbAdmin、DesignSystem、FileTools、Home、ImGateway、McpCenter、QuickLinks、Sems、SmartTerminal、TodoTracker、ToolBridge。
- e2e 覆盖 **13 个**（`e2e/plugins`）：缺 **CostScope**（有前端但无冒烟 e2e）。
- CT 故事仅覆盖 6 组（ForgeCard / PageHead / EmptyState / AIAgentMessage / SmartTerminal / **DbAdmin**）⇒ 其余 12 个带前端插件无组件状态基线。
- 硬编码色值（`style-token-baseline.json`）：AIAgent 397、Sems 178、DesignSystem 108、AgentHub 104、Home 61、ImGateway 46、CostScope 31、FileTools 14、McpCenter 13、QuickLinks 8、TodoTracker 7、ToolBridge 1、DbAdmin **0**、SmartTerminal 0。
- 结论：出问题的 DbAdmin 恰是唯一 0 硬编码的插件 ⇒ 风险主因不是「有没有裸色值」，而是**token 语义配对 + 主题取值 + 缺渲染态断言**。
