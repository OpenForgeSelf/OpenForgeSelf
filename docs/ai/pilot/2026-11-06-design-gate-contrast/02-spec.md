# Specification

> 阶段：Stage 2｜Task ID：PILOT-design-gate-contrast

## Functional Requirements

1. **规则 E（静态）**：`color:` 绑定 `--el-color-{primary|success|warning|danger|info}-light-{5,6,7,8,9}` 视为违规（背景级 token 不得当文字色）。豁免 `border-color` / `background(-color)` / `outline-color` / `fill` / `stroke`。
2. **规则 F（主题对比度，脚本化）**：解析 `ForgeSelf.Web/src/styles/themes/*.css` 的 `:root` 与 `html.dark`，校验：
   - 暗色 `--el-color-primary-light-{3,5,7,8,9}` 相对亮度逐级递减（不递增）；
   - 暗色 `light-8` 与 `--el-text-color-primary` ≥4.5；`light-9` 与 `--el-color-primary` ≥4.5；
   - 浅色选中底 `light-8` 与 `--el-text-color-primary` ≥4.5。
3. **入口整合**：`pnpm run design:check` = 现有两脚本 + 规则 E/F（F 由新脚本 `scripts/check-theme-contrast.mjs` 承担）。
4. **CI 接线**：`.github/workflows/frontend-check.yml` 在 `pnpm run check` 之后增加 `pnpm run design:check`。
5. **AGENTS 瘦身**：§8 第 3 步改为三步门禁 + 引用（细节留 `DESIGN.md` §Colors 使用规则与 `.agents/skills/ui-ux-design` 走查清单）。

## Input

- 主题 CSS 文本、组件样式文本、棘轮基线 JSON。

## Output

- stdout：`[design-compliance] ...`、`[theme-contrast] ...`；违规逐条列出 `文件:行`。
- 进程退出码：0 通过 / 1 违规。
- `scripts/design-compliance-baseline.json` 新增 `e` 列（当前 0）。

## Business Rules

- 提供者/禁色规则沿用现有 A–D；E/F 与它们同语义（新增即失败，历史存量入基线）。
- 主题目录（`**/styles/themes/`）继续豁免 A 与「裸色值」规则（那是 token 定义层），但**不豁免 F**。

## Boundary Conditions

- 主题文件缺失 / 未定义某 token → F 输出 `SKIP` 而非失败（避免误红）。
- 颜色值支持 `#rgb/#rrggbb`、`rgb()/rgba()`；不支持的颜色格式 → 该对跳过并计入 `Unknown`。
- 玻璃/半透明色（`rgba(...,0.x)`）按最坏情况合成到 `--el-bg-color` 后计算。

## Error Handling

- 解析失败（CSS 结构异常）→ 打印原因并退出 1（不静默放过）。
- 基线缺 `e` 键 → 视为 0（向后兼容旧基线）。

## Compatibility

- 现有 `--update` 语义保留：`node scripts/check-design-compliance.mjs --update` 重写基线（含 e 列）。
- 旧的 `workshop-forge.contrast.test.ts` 保留（开发期快速反馈），但**判定权威 = 提交链上的脚本**。

## Non-functional Requirements

- 脚本总耗时 < 2s（纯文本解析，不启动浏览器、不跑 vitest）。
- 输出中文可读，含修复提示（应改用 `--el-color-primary` 或 `--el-text-color-primary`）。

## Interaction Design（交互设计 · 必写节）

**无界面交互**：本批不改任何用户可见界面，只改仓库级守卫脚本、pre-commit、CI 与规范文档。交互面为命令行：

- `pnpm run design:check` → 通过打印 `PASS`；违规打印 `[theme-contrast] FAIL ... 文件:行` 并以退出码 1 结束。
- 提交时由 `scripts/hooks/pre-commit:69` 调用同一入口，失败即拒绝提交并打印原因。
- 错误提示必须包含「哪条规则、哪一行、建议改法」，不得只报数字。

## Acceptance Criteria

- [ ] 规则 E 在现状代码上命中 0；人造违规（`color: var(--el-color-primary-light-7)`）必红。
- [ ] 规则 F 在现状主题上 PASS；把暗色 `light-8` 改回 `#FDE68A` 必红且报 `1.1x:1`。
- [ ] `pnpm run design:check` 单次 < 2s。
- [ ] CI 与 pre-commit 都调用到该入口（可 grep 验证）。
- [ ] `AGENTS.md` §8 三步门禁区体积显著缩小且均为引用。

## Unknown

- CI 是否愿意接受多一步 node 脚本（预计 <2s）——按「是」设计，若不接受回退为仅 pre-commit。
- 是否把 `workshop-forge.contrast.test.ts` 改为 import 同一模块（避免逻辑漂移）——本批**不做**，记为后续重构提案。
