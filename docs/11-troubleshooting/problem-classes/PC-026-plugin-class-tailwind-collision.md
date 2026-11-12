---
id: PC-026
title: 插件语义类名与宿主 Tailwind 工具类同名，table 被改成 display:grid 致表头/内容错列
tags: [UI, 插件前端, CSS, Tailwind, 类名冲突, 表格]
invariant: 插件 DOM 中承载布局语义的元素（尤其 table）不得使用宿主 Tailwind 的 display 工具类名；插件自定义样式类必须带插件前缀（dba-/aiw-/dv-/rank- 等），避免宿主全局工具类命中插件元素
status: guarded
first_seen: 2026-11-06
recurrences: []
cases: [2026-11-06-dbadmin-table-header-misalign]
signature:
  - {id: S1, kind: grep, glob: "Plugins/**/web/src/**/*.vue", regex: "<table[^>]*class=.[^>]*(?<![-\w])(grid|flex|block|inline|inline-block|inline-flex|contents|hidden)(?![-\w])", expect: no-match}
  - {id: S2, kind: test, path: "ForgeSelf.Web/src/styles/pluginTableClassGuard.test.ts"}
allowlist: []
guards:
  - {level: G2, path: "ForgeSelf.Web/src/styles/pluginTableClassGuard.test.ts", status: active}
  - {level: G1, path: "ForgeSelf.Web/e2e/plugins/db-admin/db-admin.spec.ts", status: active}
lens:
  - 插件自定义类名是否与宿主 Tailwind 工具类同名（grid / flex / block / inline / hidden …）？
  - 承载布局语义的元素（table / thead / tbody）display 是否被宿主全局 CSS 改写？运行态是否实测 computed display？
  - e2e 是否断言了列左边界对齐（有真实数据行），而不只断言「结果格可见」？
---

# PC-026 插件语义类名与宿主 Tailwind 工具类同名，table 被改成 display:grid 致表头/内容错列

> 本文件是「这一类问题」的唯一真源：规则、扫描方法、修法。事件经过在 case，不在这里。

## 1 不变量与诱因

**不变量**：插件组件最终挂在宿主文档里，宿主全局 Tailwind 工具类（如 `.grid{display:grid}`）会命中插件 DOM。承载布局语义的元素（尤其 `<table>`）的 class 不得使用这些工具类名；插件自定义类必须带插件前缀，避免同名。

**为什么会犯**：
- `<table class="grid">` 是很自然的名字（想表达「数据网格」），但 `.grid` 恰好是 Tailwind 的 display 工具类；
- 插件前端虽有独立构建产物，但**不是 iframe/Shadow DOM**，宿主 CSS 仍全局生效；
- CT 画廊不加载宿主 Tailwind，预览时表格正常；发布宿主里才坏（**预览与运行环境 CSS 集不一致**）；
- e2e 冒烟只断言「结果格可见」，没有断言 computed display 与列对齐，双绿不覆盖。

## 2 特征签名与扫描方法

- **S1**：扫全部插件 `web/src/**/*.vue` 的 `<table>` 标签，静态 class 中出现 `grid/flex/block/inline/inline-block/inline-flex/contents/hidden` 即命中（带负向 lookbehind 排除 `dba-grid` 这类带前缀类名）。
- **S2**：vitest 守卫 `ForgeSelf.Web/src/styles/pluginTableClassGuard.test.ts` 枚举全部插件 Vue 文件，按 class token 精确比对（比 S1 更严，零误报）。
- 已知盲区：`:class` 动态拼接、CSS-in-JS、插件内联 `<style>` 里自定义 `.grid` 规则（S2 只查 `<table>` 标签；若自定义类与工具类同名，仍可能被宿主工具类叠加，需要人工审查）。
- 扫描范围矩阵：`Plugins/*/web/src/**`、`ForgeSelf.Web/src/**`、`ForgeSelf.Web/e2e/plugins/**`。

## 3 Canonical fix

1. 插件自定义布局类一律加插件前缀（DbAdmin 用 `.dba-grid`；其他插件已有 `rank-table`/`dv-table`/`aiw-table` 先例）。
2. 改模板 `<table class="grid">` → `<table class="dba-grid">`，CSS 同步改 `.dba-grid th/td`。
3. 运行态验证 `getComputedStyle(table).display === 'table'`，并用真实数据行断言 `thead th` 与 `tbody td` 左边界逐列对齐（误差 ≤1px）。
4. 被否方案：在插件 scoped CSS 里加 `display: table` 覆盖——特异性可以赢，但类名仍与宿主工具类同名，后续任何 `.grid` 新增属性都会再次命中；`!important` 同类问题且污染主题能力；iframe/Shadow DOM 隔离成本过高，不是本类的最小修法。

## 4 守卫台账

| 等级 | 载体 | 何时触发 | 覆盖盲区 |
|------|------|----------|----------|
| G2 | `ForgeSelf.Web/src/styles/pluginTableClassGuard.test.ts` | 快档 `pnpm run test` | 枚举全部插件 `<table>` 静态 class，防新增同名类 |
| G1 | `ForgeSelf.Web/e2e/plugins/db-admin/db-admin.spec.ts` | 插件冒烟 e2e（真实宿主 CSS） | 真实渲染：computed display=table + 有数据行时列左边界对齐 |
| G3 | `scripts/check-problem-classes.ps1` S1 签名 | pre-commit 问题类扫描 | 源码级 `<table class>` 工具类名 |

## 5 案例列表

| case | 日期 | 现象一句话 | 是否逃逸（守卫已在但没拦住） |
|------|------|------------|------------------------------|
| [2026-11-06-dbadmin-table-header-misalign](../cases/2026-11-06-dbadmin-table-header-misalign.md) | 2026-11-06 | DbAdmin 结果表 `class="grid"` 命中宿主 Tailwind `.grid{display:grid}`，thead/tbody 独立成 grid 项，表头与数据列错位 188.8px | 否（首见；此前无「插件类名 vs 宿主工具类」PC/守卫） |
