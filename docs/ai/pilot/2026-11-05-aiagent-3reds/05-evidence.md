# 05 Evidence（证据，只记实际发生 · Verified/Inferred/Unknown 分级）

## 红原文（隔离复跑，Verified）
1. widget-contract.test.ts › 动效棘轮：「硬编码 animation 7 处 > 棘轮上限 6；循环动画时长不属于交互档位，需单独立项。样本：animation: aiw-pulse 2.6s | aiw-bounce 1.2s | aiw-runstrip-in 0.15s | aiw-burnin-drift 240s | aiw-burnin-breathe 120s: expected 7 to be less than or equal to 6」
2. widget-contract.test.ts › 按钮 hover：「这些可点元素没有任何 hover 反馈：aiw-runstrip__btn: expected [ 'aiw-runstrip__btn' ] to deeply equal []」
3. agent-core.test.ts › free 分流：「expected [] to deeply equal [ '你', '好' ]」（onDelta 一次未调）
- 隔离复跑 ×2（vitest agent-core 过滤集）同红 ⇒ 非类序/非污染，判类：真实漂移/违例，独立于 PC-014（不依赖 qa task-12）。

## 红2 修复后（Verified，2026-11-05）
- 改动：widget.css :1198-1199 新增 `.aiw-runstrip__btn:hover { border-color: var(--aiw-primary); background: var(--aiw-primary-16); }` + `.aiw-runstrip__btn:focus-visible { outline: 2px solid var(--aiw-primary); outline-offset: 1px; }`（全 token，零裸色值，对齐 .aiw-todoentry__btn 房式惯例）。
- 过滤集：`node node_modules/vitest/vitest.mjs run agent-core`（ForgeSelf.Web 归口）⇒ **2 failed | 147 passed**（修前 3 failed | 146 passed），hover 红消失，无新增红。
- 红1/红3：仍在（属后续 commit 范围）。
