# 01 Intent — 全项目 token 规范统一

## 为什么做（背景）
用户输入11 直接指出（原文见日记）：项目里很多页面存在不规范行为——直接使用固定 token（如 `--ink-850`）虽然定义在主题文件，但相当于写死；历史上还出现自己写弹窗等造轮子问题。输入9/输入10 两个实测缺陷（UA 焦点描边漏网、SkillsView 弹窗浅色主题文字 1.06:1 不可读）是该结构问题的具体恶果：主题切换语义散落在 338 个自造 token 的使用点上，任何一处漏配都直接呈现为可见缺陷。

## 做什么（目标）
1. **token 单一来源**：全项目页面/组件（宿主 ForgeSelf.Web/src + Plugins/*/web/src）样式**只允许使用 `--el-*` token**；主题文件（`ForgeSelf.Web/src/styles/themes/*.css`）是唯一允许定义/控制具体值的地方。
2. **禁自造 token 外泄**：非 `--el-` 自定义 token（`--gold-400`、`--ink-850`、`--fg-*`、`--ds-*`、`--aiw-*`……）不得出现在主题文件之外的任何页面/组件；主题文件外的自造 token 定义（DesignSystem tokens.css、AIAgent widget.css、各 tailwind.css 桥接块等）全部收敛或登记。
3. **禁硬编码色值**：主题文件外禁止 hex/rgb(a)/命名颜色（含 `var(--x, #fff)` 兜底）。
4. **组件复用**：弹窗/抽屉/浮层一律用 Element Plus 既有组件（`el-dialog` 等），禁止自造 modal——9 个自造弹窗文件迁移。
5. **门禁固化**：本次引入的 `check-theme-tokens` 门禁在修复批收敛后接入常规检查链，防回潮。

## 到什么程度（本批边界）
- **本批（审计批）**：复制+适配门禁脚本 → 全项目检查 → 工件列出**全部违反点与数量**。**不改任何业务代码**。
- **修复批（后续，待用户看完清单拍板）**：按 03-plan 分域收敛全部 6007 处违反；收敛后把 `check:theme` 接入 `check` 链与 pre-commit。
- 明确不做：主题视觉改版（收敛是等值映射，不改观感）；Element Plus 升级；`forgeself-design/` 历史原型（不在扫描范围）。

## 成功判据（修复批）
`node scripts/check-theme-tokens.mjs` 对 `ForgeSelf.Web/src` + `Plugins/*/web/src` 报 **0 处违反**（或仅剩白名单登记项），且亮/暗两态走查无视觉回归（截图对比）、`pnpm run check`+`test` 全绿。
