---
id: PC-044
title: 主题浮层面 token 只在暗块接管、亮块漏配，亮暗两态供给不对称
tags: [UI, 主题, token, 弹窗, 亮暗一致性, Element-Plus]
invariant: themes/workshop-forge.css 的表面族 token（--el-bg-color / --el-bg-color-page / --el-bg-color-overlay / --el-fill-color / --el-fill-color-light / --el-fill-color-lighter）必须在 :root 与 html.dark 两块同时显式接管且同族同语义；任一块留 EP 默认值即两态不一致
status: guarded
first_seen: 2026-11-23
recurrences: []
cases: [2026-11-23-dialog-overlay-white-mismatch]
signature:
  - {id: S1, kind: test, path: "ForgeSelf.Web/src/styles/themes/workshop-forge.contrast.test.ts"}
  - {id: S2, kind: grep, glob: "ForgeSelf.Web/src/styles/themes/workshop-forge.css", regex: "--el-bg-color-overlay:\\s*#FFFFFF", expect: no-match}
allowlist: []
guards:
  - {level: G2, path: "ForgeSelf.Web/src/styles/themes/workshop-forge.contrast.test.ts", status: active}
  - {level: G3, path: "scripts/check-theme-tokens.mjs", status: active}
lens:
  - 改主题文件时，新增/修改的每个表面 token 是否两块（:root 与 html.dark）都在？只改一块时另一块的默认值是什么？
  - 浮层（dialog/messagebox/dropdown）白底与内容区不同族，是不是 overlay 系 token 漏配而不是组件问题？
  - 单点修组件样式之前，先查主题层供给——主题层改 1 处，全部浮层受益。
---

# PC-044 主题浮层面 token 只在暗块接管、亮块漏配，亮暗两态供给不对称

> 本文件是「这一类问题」的唯一真源：规则、扫描方法、修法。事件经过在 case，不在这里。

## 1 不变量与诱因

**不变量**：`ForgeSelf.Web/src/styles/themes/workshop-forge.css` 亮（`:root`）暗（`html.dark`）两块对「表面族」token 的供给必须成对：一块接管了、另一块也必须接管。`--el-bg-color-overlay`（弹窗/浮层面）亮块留 EP 2.14.3 默认 `#FFFFFF` 而暗块已暖化 `#1B1815`，就是供给不对称——用户看到「弹窗白底与内容区不统一」。

**为什么会犯**：
- 暗块改造先做（2026-10 design overhaul），亮块沿用 EP 默认值当时「看起来能用」，没有一次两块对称性核对；
- 检查器只管 token 存在性与白名单，不管「EP 默认值被主题文件某块继承」这种隐式供给；
- 弹窗壳层自造样式把注意力引向组件层，根因实际在主题层单点；
- 走查清单有「亮暗两态」条款，但只验「暗色对不对」，没验「亮色与暗色是否同语义体系」。

## 2 特征签名与扫描方法

- **S1**（类级守卫，G2）：`workshop-forge.contrast.test.ts`「表面 token 接管」describe——6 个表面 token × 亮/暗两块解析值必须 ≠ EP 2.14.3 默认值。阳性对照已做：临时还原 `#FFFFFF` 缺陷态 → 红（红在 overlay）。
- **S2**（G3 签名）：主题文件 grep 亮块 overlay 回归 `#FFFFFF` 即红。
- 扫描命令：`pnpm run test -- workshop-forge.contrast`（vitest）。

## 3 修法

1. 主题层修（不是组件层）：亮块 `--el-bg-color-overlay: #FFFDF7`（= 本块 `--el-fill-color-lighter` 既有值，暖面家族，零新增色值）；暗块 `#1B1815` 已暖化不动。层级语义对齐：两块都保持「overlay 略亮于 bg」。
2. 类级守卫落地（S1 describe），并保留 `--el-fill-color-blank` 留 EP 白的裁决——输入面（input）与浮层面语义不同，有意对比，不纳入接管清单。

## 4 边界与裁决

- ToastHost `.toast-close` 等非表面 token 的 20px 宽度不属此类。
- 页面壳泛灰（dark + 亮色 appearance 背景）与 vite HMR 主题文件瞬态丢暗块（硬刷新即复）都是环境差异，非此类缺陷——走查前硬刷新 + 查 ForgeSetting 数据根。

## 5 关联

- case: `docs/11-troubleshooting/cases/2026-11-23-dialog-overlay-white-mismatch.md`
- 守卫文件：`ForgeSelf.Web/src/styles/themes/workshop-forge.contrast.test.ts`
- 同批接线：`scripts/check-theme-tokens.mjs`（AC1 全仓 0 + fileExemptions 机制）承担「新表面 token 漏配」的第一道门。
