# mini-task：顶部栏页签预留关闭/固定图标位（悬浮零位移）

- date: 2026-11-23
- task-id: `topbar-tab-icon-slot`
- feature: 壳层（ForgeSelf.Web TopNavbar，无 02-features 单页映射，宿主壳层批）
- type: 轻量缺陷修复（UI 悬浮布局位移）｜裁剪依据：规范 §4 轻量（≤3 文件），Intent/Spec/Plan/Task 合并本文件，05/06 单独产出
- 闸门1 处理：用户直接指令（输入22 原文即需求与验收口径）+ AGENTS §8.5 自动迭代（既有规范覆盖内的常规 UI 修复自主执行，无需逐项请批）。无接口/契约/依赖变更，git 可回滚；尺寸数值沿用既有 close-x 规格（16px 槽 + 6px gap），无新增自由裁量面。

## 一、Intent（为什么 / 做什么 / 到什么程度）

- **Problem**：`ForgeSelf.Web/src/components/TopNavbar.vue` 页签的关闭/取消钉住图标（`.close-x`）用 `display:none` ↔ `:hover` 时 `display:inline-flex` 切换显隐。悬浮瞬间该页签自身变宽（图标 16px + gap 6px = +22px），其后所有页签整体位移，视觉抖动。
- **Why**：用户输入22 明确要求「页签增加宽度，预留关闭、固定图标等位置，避免悬浮展示图标时位置变宽、所有页签都要产生位移」。
- **Expected Outcome**：非首页页签常驻预留图标槽位；悬浮/键盘聚焦只改图标可见性与透明度；任意页签宽度悬浮前后恒定，整体零位移。
- **Constraints**：不动 tabs store 与交互逻辑；不动其他组件/全局样式；颜色与动效只用既有 token；首页签（`/`）无图标、不占槽（维持现状）。
- **Success Criteria**：AC-1~AC-4（见「五、Task」）。

## 二、Spec

### Functional Requirements
- FR-1 非首页页签（可关闭或可取消钉住）图标槽常驻占位：16px×16px + 既有 6px gap，`flex:none` 防 flex 压缩（PC-019 lens）。
- FR-2 图标默认 `visibility:hidden` + `opacity:0`；hover 或 `focus-within` 时 `visible` + `opacity:1`，过渡 `var(--dur-fast) var(--ease-out)`（160ms fade）；隐藏经 `visibility 0s var(--dur-fast)` 延迟翻转保证淡出完整。
- FR-3 隐藏态不参与命中测试与无障碍树（`visibility:hidden` 语义，不用 `display` 出入布局流）。
- FR-4 首页签（`/`）不渲染图标、不预留槽位。

### Interaction Design（ui-ux-design 交互规格模板）

| 交互点 | 触发 | 结果（点什么出现什么） | 状态模型 | 反馈 | 空态 | 边界 |
| --- | --- | --- | --- | --- | --- | --- |
| 页签关闭/钉住图标 | hover 页签；或键盘 Tab 聚焦页签 | 图标在**常驻预留槽内**淡入（160ms）；页签与相邻页签零位移 | hidden（默认）↔ visible（hover / focus-within） | opacity 淡入淡出 | 槽位空置但占位（常驻） | 悬浮任一页签时其他页签位置不变；大量页签时仅 tab-nav 滚动量增加 ~22px/签 |

- 五态：纯静态样式面，加载/空/出错/无权限态不涉及；正常态 = 图标槽常驻。
- 几何验收（并入 AC）：悬浮前后同页签 `getBoundingClientRect().width` 相等；其后相邻页签 `x` 坐标不变（几何取证 SOP，数值入 05）。
- 走查符合性：亮/暗两态各截图核对（本次不涉颜色值变更，走查确认无回归即可）。

### Boundary / Compatibility / Failure Modes
- 兼容：不新增依赖；Element Plus / Tailwind 不受影响；暗色主题无新颜色面。
- Failure Modes（PC.lens 预填，2026-11-23 查 `problem-classes/README.md` tags 检索表）：
  - PC-019（flex/grid 子项尺寸约束双面）：新增槽位必须 `flex:none` 防 shrink 压缩 → 本批 FR-1 满足。
  - 其余 PC 无命中 → 无。
- Unknown：无。

## 三、Plan（真实文件）

| 文件 | 动作 | 理由 |
| --- | --- | --- |
| `ForgeSelf.Web/src/components/TopNavbar.vue` | `.close-x` 规则块：删 `display:none` 与 `:hover` 的 `display` 切换；改 `visibility:hidden + opacity:0` 常驻占位 + `flex:none` + token 过渡；hover/focus-within 显示 | 根因修复：图标显隐不再出入布局流 |
| `ForgeSelf.Web/src/__tests__/TopNavbar.test.ts` | 追加「图标槽常驻预留」守卫测试：源码不变量（`.close-x` 无 `display:none`、无 hover `display` 切换、含 `visibility:hidden`/`width:16px`/`flex:none`）+ DOM 结构断言（非首页签恒渲染 1 个 `.close-x`，首页签无） | G1 守卫，驻快档 vitest，先红后绿 |

- Test Plan：① 守卫先红（修复前实跑，Verified）→ 修复后绿；② 快档：`pnpm run check` + `pnpm exec vitest run` + `pnpm run design:check`；③ 走查：dev-stack 起真实前后端，浏览器量测悬浮前后页签 width/相邻 x（几何取证 SOP），亮/暗两态截图。
- 守卫等级：**G1**（首次发生、单点影响面）；复发才升 G2/建枚举守卫。

## 四、Task

- **Scope Allowed**：上表两文件 + 问题类归档（case/PC/索引）+ pilot 工件。
- **Scope Forbidden**：tabs store、其他组件、全局样式、设计 tokens、e2e 基建、后端。
- **Acceptance Criteria**：
  - [x] AC-1 守卫测试修复前红、修复后绿（vitest 实跑日志，Verified）
  - [x] AC-2 悬浮任一非首页页签：该页签 width 与其后相邻页签 x 悬浮前后差 = 0（走查实测数值）
  - [x] AC-3 快档全绿：`pnpm run check` / `pnpm exec vitest run` / `pnpm run design:check`
  - [x] AC-4 键盘聚焦页签时图标可见（focus-within）
- **Verification Commands**：
  - `pnpm exec vitest run src/__tests__/TopNavbar.test.ts`（ForgeSelf.Web/）
  - `pnpm run check`；`pnpm exec vitest run`；`pnpm run design:check`
  - 走查：dev-stack（根 `pnpm dev`）+ 浏览器量测 + 截图

## 五、适用问题类（v1.2.0 轻量任务栏位）

- 2026-11-23 按 tags 检索 `docs/11-troubleshooting/problem-classes/README.md`：**无同 id 命中**（PC-019 为 shrink/min-width 面，本例为「hover 显隐控件未预留位宽」新根因）。本例按新缺陷实例走闭环：G1 守卫 + case/PC 归档（PC-042，本批建立）；复发即升 G2。
