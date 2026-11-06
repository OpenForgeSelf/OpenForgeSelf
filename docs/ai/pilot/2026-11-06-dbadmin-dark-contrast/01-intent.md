# Intent

> 阶段：Stage 1｜只描述「为什么做 / 做什么 / 做到什么程度」，不提前锁定具体代码实现。
> Task ID：2026-11-06-dbadmin-dark-contrast ｜ 日期：2026-11-06

## Problem

暗色主题下，DbAdmin 插件左栏「我的连接」选中行（`IOC_TEST`）的连接名几乎不可见：选中背景为亮金 `#FDE68A`，文字继承浅色前景 `#F4F0E7`，实测 WCAG 对比度约 **1.10:1**（截图见用户输入1）。同类结构性问题还包括使用 `--el-color-primary-light-9` 作底色的标签/横幅，以及把 `light-N` 当文字色用的写法。

## Why

- 这是用户可见的可读性缺陷（P0 级「文字不可见」），且不是单个组件调色失误：暗色主题把 Element Plus 的 `light-N`（背景级 token）定义成了比主色更亮的浅色，与 `DESIGN.md`/设计稿 token 语义相反，属于**主题 token 倒置**这一类问题。
- 项目已有对比度规范（`DESIGN.md:181,389`）与走查清单（`ui-ux-design` D/D-1/E），但：
  1. 没有「暗色下 light-N 必须是逐级更暗」的 token 不变量；
  2. 走查清单没有点名「列表/树选中行、标签底、横幅底」这些 primary-light-8/9 表面；
  3. e2e 默认主题是 system → Playwright 解析为 light，暗色从未被自动化覆盖；
  4. `design:check` 只拦新增裸色值/token 族违规，不校验主题 token 的明暗语义。
- 只修 `.node.on` 一处会留下同类隐患（标签/横幅/按钮 hover 等 ~100 处引用），用户要求「本质目标是完善规范和流程，下次不会再发生」。

## Expected Outcome

1. 暗色下 DbAdmin 选中连接名、标签、横幅等 primary 浅底表面的文字对比度 ≥4.5:1（实测数值进 05-evidence）。
2. 暗色主题 `--el-color-primary-light-3/5/7/8/9` 恢复 EP/设计稿语义：逐级更暗，不得比主色更亮。
3. 规范/流程补强：`DESIGN.md` 增加 token 语义不变量；`ui-ux-design` 走查清单点名三类表面与「亮/暗双态实测」；UI 批规则与 Spec 模板要求「选中/激活/标签/横幅 × light/dark」矩阵，并明确默认主题不等于暗色验证。
4. 新增机器守卫：主题 token 对比度 vitest（类级）+ DbAdmin e2e 选中行亮暗对比度断言（实例级），回归会在门禁变红。
5. 问题类归档：新建 PC-025 + case，三处索引齐全，`check-problem-classes.ps1` 绿。

## Constraints

- 不修改生产环境、不停/启/杀用户运行实例（含 `:51888`）。
- 不改数据库结构、不动鉴权/权限核心逻辑。
- 不新增依赖；不顺手重构无关代码；不提交 git（发布动作须用户批准）。
- 主题 token 修改仅限 `workshop-forge.css` 暗色 primary 段；同类 `light-N` 当文字色的用法按扫描结果最小修正。
- 本批为缺陷类任务：走 problem-class-closure（先红后绿、同类扫查、守卫、case/PC 归档）。

## Success Criteria

- `pnpm run test` 中新增 `workshop-forge.contrast.spec.ts` 先红后绿（修复前红灯输出留档，修复后绿）。
- `ForgeSelf.Web/e2e/plugins/db-admin/db-admin.spec.ts` 冒烟用例选中连接后，light 与 dark 两态实测对比度均 ≥4.5:1。
- `pnpm run check`、`pnpm run test`、`pnpm run design:check`、`Plugins/DbAdmin/web pnpm run build` 全绿（本批相关范围）。
- 走查：dev-stack 亮/暗两态截图各一张，DbAdmin 选中行文字肉眼可读，数值与断言一致。
- `pwsh scripts/check-problem-classes.ps1` 绿；PC-025/case/索引齐全；07 三节（问题类归档/重构提案/悬空教训）齐全。
