---
id: PC-042
title: 悬浮显形图标未预留槽位，页签 hover 宽度跳变且相邻页签位移
tags: [frontend, css, hover-reveal, layout-shift, topnavbar]
invariant: 行内流式布局中 hover/focus 显形的图标与操作钮必须常驻占位（visibility/opacity 显隐或绝对浮层），禁止 display:none ↔ display:inline-flex 切换挤占布局
status: guarded
first_seen: 2026-11-23
recurrences: []
cases: [2026-11-23-hover-reveal-width-jump]
signature:
  - {id: S1, kind: test, path: "ForgeSelf.Web/src/__tests__/TopNavbar.test.ts", note: "「页签图标槽常驻预留（悬浮零位移守卫）」4 条：无 display:none、hover/focus 态无 display 切换、槽位规格、DOM 恒渲染"}
allowlist: []
guards:
  - {level: G1, path: "ForgeSelf.Web/src/__tests__/TopNavbar.test.ts", status: active, note: "组件级红→绿实证（2026-11-23 3 failed → 8 passed）"}
lens:
  - 这个 hover/focus 显形元素占不占布局？占位是常驻预留（visibility/opacity）还是绝对浮层？
  - 显形/收起的过渡是否用动效 token（--dur-fast/--ease-out），移出时 visibility 延迟翻转是否保证淡出完整？
---

# PC-042 — 悬浮显形图标未预留槽位，页签 hover 宽度跳变且相邻页签位移

- **date**: 2026-11-23
- **severity**: P1（用户可感的持续性布局抖动，非崩溃）
- **category**: 前端样式 / 布局稳定性（CLS）

## 1 不变量与行内流式布局

**不变量**：行内流式布局（flex/grid 行内）中，靠 hover/focus 才显形的图标、徽标、操作钮，
**显形前后占用的布局空间必须恒定**——要么常驻占位（`visibility:hidden + opacity:0`，仍在盒模型内），
要么绝对定位浮层（不参与流式布局）。**禁止** `display:none ↔ display:flex/inline-flex` 切换：
`display:none` 把元素从盒模型摘除，显形时行内宽度突增，同容器内后续元素整体位移（layout shift）。

Agent/人为什么会犯：把「显隐」直接映射成 `display` 是 CSS 最顺手写法；`display` 切换在
**非流式容器**（弹层、下拉、遮罩）里完全正确，认知模型被正向强化；只有「行内元素挤占同排兄弟」
场景才会爆炸，且静态看代码/看截图都不可见，必须悬浮对比量测才暴露。

## 2 特征签名与扫描方法

- 显式守卫（S1）：TopNavbar 组件守卫测试，锁 `.close-x` 的槽位规格与显隐语义（见 §4）。
- 类级扫查（人工/检索）：grep `display:\s*none` 所在规则的选择器，与含 `:hover`/`:focus-within`
  的规则交叉；命中后判断「显形后是否挤占行内布局」——媒体查询隐藏、弹层/下拉初始隐藏、
  `hidden` 属性、可访问性隐藏（clip/absolute）均**非**本类。
- 已知盲区：JS 动态 `style.display` 赋值与 Vue `v-show`（本质同型）不经 CSS 文本扫描；
  v-show 场景靠 lens 问题 + code review 拦截。

## 3 Canonical fix

```css
/* 常驻占位：隐而占位，显隐只走 visibility/opacity */
.close-x {
  visibility: hidden;
  opacity: 0;
  flex: none;            /* 防 flex 压缩槽位 */
  width: 16px;
  height: 16px;
  transition:
    opacity var(--dur-fast) var(--ease-out),
    visibility 0s linear var(--dur-fast); /* 移出延迟翻转，淡出完整 */
}
.tab-item:hover .close-x,
.tab-item:focus-within .close-x {
  visibility: visible;
  opacity: 1;
  transition:
    opacity var(--dur-fast) var(--ease-out),
    visibility 0s;       /* 悬入立即显现 */
}
```

参考实现：`ForgeSelf.Web/src/components/TopNavbar.vue` `.close-x`（2026-11-23 起）。
要点：① 槽位元素 DOM 恒渲染（条件区除外）；② 悬入零延迟、移出 160ms 延迟翻转 visibility；
③ 首页签等无操作语义的位置不渲染槽位（不白占空间）。

被否方案见 case §5（预显邻位 / 绝对定位浮层 / min-width 兜底）。

## 4 守卫台账

| 等级 | 载体 | 何时触发 | 覆盖盲区 |
|------|------|----------|----------|
| G1 | `ForgeSelf.Web/src/__tests__/TopNavbar.test.ts`「页签图标槽常驻预留（悬浮零位移守卫）」4 条 | `pnpm run test` / vitest run | 仅锁 TopNavbar `.close-x`；其他组件/未来的同型交互靠 lens + PC 检索 |

## 5 案例列表

| case | 日期 | 现象一句话 | 是否逃逸（守卫已在但没拦住） |
|------|------|------------|------------------------------|
| 2026-11-23-hover-reveal-width-jump | 2026-11-23 | 页签悬浮显形 ×，本页签 +22px、后续页签整体 +22px 位移 | 否（首次，无既有守卫） |
