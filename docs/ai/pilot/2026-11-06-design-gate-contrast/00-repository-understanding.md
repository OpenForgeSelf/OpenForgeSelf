# Repository Understanding

> 阶段：Stage 0｜本批为「设计门禁缺口」基建批，先确认事实再改门禁（用户输入：先确认插件用的是 token 还是自定义色值）。

## 项目结构

- `ForgeSelf.Web/src`（宿主 SPA）｜`Plugins/<X>/web/src`（插件前端，构建产物 `web/dist/index.js`）
- `scripts/`（仓库级守卫与发布脚本）｜`docs/04-standards/`｜`docs/ai/pilot/`（工件链）
- `Plugins/DbAdmin/web/src`（本次缺陷插件，纯 token 实现）

## 技术栈

Vue 3.5 + Vite 6 + TS 5.7 + Element Plus 2.14 + Tailwind 4 + Pinia；测试 = vitest（`pnpm run test`）/ Playwright CT（`pnpm run test:ct`）/ Playwright e2e（`pnpm run test:e2e`）。

## 架构特点

- 颜色单一来源：`ForgeSelf.Web/src/styles/themes/workshop-forge.css`（`:root` 浅色 / `html.dark` 暗色），插件与宿主共用同一套 `--el-*` token。
- 设计门禁入口：`ForgeSelf.Web/package.json:20` `design:check` = `check-style-tokens.mjs` + `check-design-compliance.mjs`。

## 测试方式

- 主题级对比度：`ForgeSelf.Web/src/styles/themes/workshop-forge.contrast.test.ts`（vitest）。
- 渲染态对比度：`ForgeSelf.Web/e2e/plugins/db-admin/db-admin.spec.ts` 内 `measureContrast()`（仅该插件）。
- 组件状态：`ForgeSelf.Web/src/components/DbAdmin.ct.spec.ts`（CT，仅像素基线）。

## 构建命令

`pnpm run check`（vue-tsc + eslint + 组件规范守卫）｜`pnpm run design:check`｜`pnpm run test`｜`pnpm run test:ct`。

## 主要目录职责

| 路径 | 职责 |
|---|---|
| `scripts/check-style-tokens.mjs` | 禁新增裸色值（棘轮基线 `scripts/style-token-baseline.json`） |
| `scripts/check-design-compliance.mjs` | 规则 A 非白名单 token 族 / B 胶囊按钮 / C 玻璃 blur>40px / D 蓝紫渐变 |
| `scripts/hooks/pre-commit` | 提交门禁：design:check + pilot 工件链 + 问题类台账 |
| `.github/workflows/frontend-check.yml` | CI 只跑 `pnpm run check` |
| `ForgeSelf.Web/ct/index.html:12` | 画廊固定 `class="dark"`（CT 只拍暗色） |

## 代码组织方式

样式写在组件 `<style scoped>` 内，颜色引用 `var(--el-*)`；主题变量集中在 `styles/themes/`（守卫豁免该目录）。

## 现有工程规范

`DESIGN.md` §Colors 使用规则（L178-184，已含 2026-11-06 立的 token 明暗语义与「禁止把 light-N 当文字色」）；`AGENTS.md` §8 设计门禁三步。

## 候选低风险任务

1. AGENTS §8 瘦身为引用（用户明确要求）。
2. 把「背景级 token 当文字色」变成静态规则（当前全仓仅 2 处命中且都在 `Plugins/AIAgent/docs/workbench-prototype.html` 原型 HTML → 零债，可直接硬拦）。
3. 把主题对比度从「只在 vitest」搬到提交/CI 可执行入口。

## 选择该任务的原因（实测证据）

- **DbAdmin 是 100% token 实现**：`scripts/style-token-baseline.json` 的 `dirs` 里没有 `Plugins/DbAdmin/web/src`（= 0 处硬编码色值）。所以「统一 token 就不会黑底看不见」的前提在本例不成立——问题出在**主题 token 的暗色取值**（`light-8` 被定义成亮金）与**token 语义配对**，不是色值来源。
- 仓库仍有 2501 处硬编码色值（宿主 1533 / 插件 968），属历史棘轮基线，新增已被 `check-style-tokens.mjs` 拦截。
- **对比度守卫没进任何提交链**：`scripts/hooks/pre-commit:69` 只跑 two-node 脚本；`.github/workflows/frontend-check.yml:49` 只跑 `pnpm run check`。vitest 与 CT 均不在 CI/pre-commit 内 ⇒ 主题对比度守卫在「提交」这一环等于不存在。
- `check-design-compliance.mjs:61-91` 四条规则全是文本模式（token 族/胶囊/玻璃/蓝紫渐变），**没有任何亮度或对比度计算**，也不校验主题变量取值方向。
