---
id: 2026-11-23-hover-reveal-width-jump
title: 悬浮显形图标未预留槽位，页签 hover 宽度跳变且相邻页签位移
pc: PC-042
severity: P1
evidence: Verified         # 本 case 主证据等级：Verified | Inferred | Unknown
---
# Case：2026-11-23 顶部栏页签悬浮时宽度跳变、相邻页签位移

> Case 只讲「这一次发生了什么」；规则/修法/扫描方法一律引用 PC，不复述。

## 1 现象

用户指令（输入22 原文）：「顶部栏页签增加宽度，预留关闭、固定图标等位置，避免悬浮展示图标的时候，位置变宽，所有页签都要产生位移」。

浏览器实测复现（Playwright，修复前，`ForgeSelf.Web/screenshots/dev/topbar-tab-slot/before-hovered.png`）：
悬浮「智能体」页签时，该页签 width **67.8 → 89.8（+22.0px，16px 图标 + 6px gap）**，其后三个页签
x 坐标整体 **+22.0px 位移**（技能管理 254.0→276.0、系统监控 337.1→359.1、任务中心 420.1→442.1）。
鼠标扫过页签栏时整排页签持续抖动。

## 2 复现红灯

`cd ForgeSelf.Web && pnpm exec vitest run src/__tests__/TopNavbar.test.ts`（2026-11-23 18:00，`.temp/red-test.log`）：
3 failed | 5 passed —
① `.close-x 不得含 display:none` → 失败（`display: none` 在场）；
② hover/focus 态规则不得用 display 切换 → 失败（`display: inline-flex`）；
③ 槽位规格 `visibility:hidden + width:16px + flex:none` → 失败。

## 3 根因（5 Whys + 第 6 问）

1. 为什么悬浮时页签变宽？→ `.close-x` 用 `display: none` ↔ `:hover { display: inline-flex }` 切换，`display:none` 元素**不占布局**。
2. 为什么用 display 切换？→ 写样式时把「显隐」直接映射成了 `display`，没有区分「占位显隐」与「不占位显隐」两种语义。
3. 为什么后续页签会跟着动？→ 页签是 flex 行内布局，前一个元素宽度 +22px，后续元素 x 全部顺移（layout shift）。
4. 为什么没有在写代码时被拦住？→ `design:check`/`check` 均不检测「hover 显形元素是否预留槽位」，几何类缺陷只有跑起来才可见。
- **第 6 问：为什么现有流程/守卫没让我一开始就写对？** → 归因：无守卫强制。hover 显形（hover-reveal）类交互缺一条「显形前必须有常驻占位」的项目不变量；本批以 PC-042 + G1 守卫补齐。

## 4 同类扫查命中表

| 位置（文件:行） | 同根因？ | 处置（已修 / 已守卫 / TODO#） |
|-----------------|----------|-------------------------------|
| `ForgeSelf.Web/src/components/TopNavbar.vue` `.close-x`（display:none ↔ hover display:inline-flex） | 是（本例） | 已修 + G1 守卫 |
| 仓内其余 `display: none` ↔ `:hover` 切换命中 | 否（逐一核对：媒体查询隐藏、弹层/下拉初始隐藏、`hidden` 属性、表单可访问性隐藏，均非「行内元素 hover 显形挤占布局」同型） | 无需处置 |

扫查方法：grep `display:\s*none` 与 `:hover` 规则交叉，逐条判断「显形后是否挤占行内布局」；细节见 05-evidence.md。

## 5 修复与被否方案

最终修法 = PC-042 canonical fix：`.close-x` 常驻渲染 + `visibility:hidden; opacity:0; flex:none; width:16px; height:16px`（隐而占位），`:hover` / `:focus-within` 时 `visibility:visible; opacity:1`；过渡只用 `opacity var(--dur-fast) var(--ease-out)` + `visibility 0s linear var(--dur-fast)`（移出延迟翻转保证淡出完整）。位移从机制上归零。

被否方案：
- **悬浮其他页签时预显本页签图标**（预显邻位）— 会造成未交互区域闪烁，且仍需 display/宽度过渡，复杂度高、体验差。
- **绝对定位图标浮在页签上**（AllFeaturesView `.pin-btn` 同款）— 不挤占布局，但会遮挡页签文字/需要额外留白兜底，且与本例「页签内固定槽位」语义不符；本例需要的是常驻槽位而非浮层。
- **给页签设 min-width 而图标仍 display 切换** — 数值上等价于常驻槽位但图标仍重排页签内容（文字位置跳），不彻底。

## 6 重构提案与结论

- R0（本次已做）：TopNavbar `.close-x` 常驻槽位化。
- R1（提案，未排期）：页签 × 图标改为 `<button>` 并加 `aria-label`，键盘可达（现存缺口：span 非 focusable，仅 `:focus-within` 借父级焦点显形）；记 TODO 待排。
- R2/R3：无。

## 7 守卫落地

- **G1**（首见）：`ForgeSelf.Web/src/__tests__/TopNavbar.test.ts`「页签图标槽常驻预留（悬浮零位移守卫）」4 条 —— ① `.close-x` 无 `display:none`；② hover/focus 态规则无 `display` 切换；③ 槽位规格（`visibility:hidden` + `width:16px` + `flex:none`）；④ DOM 非首页签恒渲染 1 个 `.close-x`（首页签 0 个）。status: active（2026-11-23 红→绿实证）。
- 逃逸场景说明：本守卫只锁 TopNavbar 的 `.close-x`；「hover-reveal 必须常驻占位」的类级不变量尚未全仓机械化（见 PC-042 signature 留位），新页面/新组件引入同型交互时靠 lens 问题 + PC 检索拦截。

## 8 逃逸分析（二次复发必做，首见可写「N/A 首次」）

N/A 首次。

## 9 基建改进

- 新增 PC-042 问题类（invariant/signature/lens），problem-classes 台账、cases 索引、index.md 现象速查同步登记。
- TopNavbar 守卫测试补齐组件级几何不变量的 vitest 先例（SFC 源解析 + CSS 规则断言 + DOM 断言三层）。
- 过程中实证 dev-stack 端口占用「假就绪」缺陷（readiness 探测无法区分自家实例与并行 worktree 实例），记 TODO 待修（PC-007 邻域）。

## 10 复用赋能

- 后续任何「hover/focus 显形图标、徽标、操作钮」出现在**行内流式布局**（页签、面包屑、列表行操作列、表格行 hover 工具条）时，必须先查 PC-042：显形元素要么常驻占位（visibility/opacity），要么绝对浮层不挤布局；禁止 `display:none ↔ display:flex` 切换。
- 新写组件 Spec 的「Interaction Design」节需自问 lens：这个显形元素占不占布局？不占则标注浮层方案。

## 11 证据

| 结论 | 等级 | 出处（文件:行 / 命令输出 / 截图） |
|------|------|-----------------------------------|
| 修复前 hover 页签 +22px、后续页签 +22px 位移 | Verified | Playwright 量测 2026-11-23 18:01（技能前实例）＋ `ForgeSelf.Web/screenshots/dev/topbar-tab-slot/before-hovered.png` |
| 守卫红灯 3 条（display:none / hover display / 槽位规格缺失） | Verified | `.temp/red-test.log`（2026-11-23 18:00，3 failed \| 5 passed） |
| 修复后 hover/移开全程零位移（所有页签 width/x Δ0） | Verified | Playwright 量测 2026-11-23 18:07（本 checkout :7002 实例）＋ `after-base.png` / `after-hovered.png` |
| 暗色主题下显隐正常、无新增裸色值 | Verified | `after-hovered-dark.png`（html.dark 显式切换）；`gate-design.log` design:check 绿 |
| 键盘聚焦页签时图标显形（focus-within）、移开 160ms 后回隐 | Verified | Playwright evaluate 2026-11-23 18:07–18:09（idle hidden → focus visible → blur 延迟翻转 hidden） |
| 守卫绿灯 8/8 | Verified | `.temp/green-test2.log`（2026-11-23 18:06，8 passed） |
