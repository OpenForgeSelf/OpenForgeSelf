---
id: PC-046
title: Element Plus 组件自定义 class 落在组件根元素，后代选择器写法死规则不生效
tags: [Element-Plus, 弹窗, CSS 选择器, 插件前端, 壳层覆盖]
invariant: 给 EP 组件传自定义 class（如 class="x-dialog"）时，样式覆盖必须用复合选择器 `.el-dialog.x-dialog`（同一元素双类）；`.x-dialog .el-dialog` 后代选择器永远匹配不到
status: guarded
first_seen: 2026-11-23
recurrences: []
cases: [2026-11-23-plugin-dialog-elimport-missing]
signature:
  - {id: S1, kind: grep, glob: "Plugins/*/web/src/**/*.vue", regex: "\\.[a-z-]+dialog\\s+\\.el-dialog", expect: no-match}
  - {id: S2, kind: grep, glob: "ForgeSelf.Web/src/**/*.{vue,css,scss}", regex: "\\.[a-z-]+dialog\\s+\\.el-dialog", expect: no-match}
allowlist: []
guards:
  - {level: G3, path: "scripts/check-problem-classes.ps1", status: active}
lens:
  - 写 `.something .el-dialog` 覆盖样式之前，先确认自定义 class 实际落在哪个元素——EP 2.14.3 的 dialog 自定义 class 与 .el-dialog 同根。
  - 覆盖不生效时先在 DevTools 查真实 DOM 类名序列，再调选择器；不要靠加 !important 硬怼。
  - 需要弹窗内部（header/body/footer）样式时，用 `:deep()` 从根元素下钻，而不是造不存在的外层容器。
---

# PC-046 Element Plus 组件自定义 class 落在组件根元素，后代选择器写法死规则不生效

> 本文件是「这一类问题」的唯一真源：规则、扫描方法、修法。事件经过在 case，不在这里。

## 1 不变量与诱因

**不变量**：Element Plus 2.14.3 中，`<el-dialog class="x-dialog">` 的自定义 class 与 `.el-dialog` 落在**同一个根元素**（`<div class="el-dialog x-dialog">`）。要覆盖壳层样式（宽度/圆角/背景），选择器必须写复合形式 `.el-dialog.x-dialog`；写 `.x-dialog .el-dialog`（后代）是死规则——DOM 里根本不存在这个祖先-后代关系。

**为什么会犯**：
- 旧版 EP / 其他组件库的版本记忆（custom class 曾落在 wrapper/外层）；
- 直觉上「给 dialog 加类、再从外面选里面」，没有实查 DOM 结构。

## 2 特征签名与扫描方法

- **S1**：全前端源码 grep 「`.x-dialog .el-dialog`」式后代写法（类名含 dialog + 后代 .el-dialog）即红——这类规则 100% 是写错的。
- 命中后的正确改法：改为 `.el-dialog.x-dialog { … }`；内部元素用 `.el-dialog.x-dialog .el-dialog__header`（根复合 + 内部后代，这条是合法的）。

## 3 修法

复合选择器改写（本批 9 个弹窗迁移文件全部按此定式）；需要穿透 scoped 时配 `:deep()`。

## 4 关联

- case: `docs/11-troubleshooting/cases/2026-11-23-plugin-dialog-elimport-missing.md`
- 与 PC-045 同批同源：弹窗迁移（FR5）在插件预编译产物语境下的两个必守点。
