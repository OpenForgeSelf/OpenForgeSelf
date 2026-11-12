---
id: PC-025
title: 暗色主题 primary light-N 明暗倒置，主色浅底上的文字不可见
tags: [UI, 主题, 暗色, 对比度, token, 插件前端]
invariant: 暗色下 --el-color-primary-light-N 必须逐级更暗（背景级 token），light-8/9 作底色时文字对比度 ≥4.5:1；light-N 不得当文字色
status: guarded
first_seen: 2026-11-06
recurrences: []
cases: [2026-11-06-dbadmin-dark-connection-contrast]
signature:
  - {id: S1, kind: grep, glob: "ForgeSelf.Web/src/styles/themes/*.css", regex: "--el-color-primary-light-8:\s*#FDE68A", expect: no-match}
  - {id: S2, kind: grep, glob: "ForgeSelf.Web/src/styles/themes/*.css", regex: "--el-color-primary-light-9:\s*#FEF3C7", expect: no-match}
  - {id: S3, kind: test, path: "ForgeSelf.Web/src/styles/themes/workshop-forge.contrast.test.ts"}
allowlist: []
guards:
  - {level: G2, path: "ForgeSelf.Web/src/styles/themes/workshop-forge.contrast.test.ts", status: active}
  - {level: G1, path: "ForgeSelf.Web/e2e/plugins/db-admin/db-admin.spec.ts", status: active}
lens:
  - 新增/修改 primary-light-8/9 底色时，文字用的是哪个 token？亮/暗两态对比度各是多少？
  - e2e/走查是否显式切了 dark，而不是依赖默认主题（system 在 CI/Playwright 解析为 light）？
  - 是否把 light-N 当文字色用了（亮/暗下明暗相反，跨主题不可控）？
---

# PC-025 暗色主题 primary light-N 明暗倒置，主色浅底上的文字不可见

> 本文件是「这一类问题」的唯一真源：规则、扫描方法、修法。事件经过在 case，不在这里。

## 1 不变量与诱因

**不变量**：`--el-color-primary-light-N` 是 Element Plus 的**背景级** token。暗色下 `N` 越大必须越暗（与主色混合黑色），light-8/9 作底色时正文/标签字 ≥4.5:1；`light-N` 不得作为文字色。

**为什么会犯**：
- 主题重写时把「浅色模式的亮金 ramp」直接复制进 `html.dark`（`git blame` 显示 2026-10-11 a99f5e2 design overhaul 引入），只检查了浅色效果；
- 设计稿真源 `forgeself-design/themes/workshop-forge.css:112-116` 明确定义暗色 ramp 为 `#D97706 → #451A03`，但实现层漂移没有被 `design:check` 拦（该守卫只拦裸色值/自定义 token 族，不校验 token 语义）；
- 走查清单有对比度条款，但没有点名「列表/树选中行、标签底、横幅底」这些 primary-light-8/9 表面；
- e2e 默认主题 `system` 在 Playwright 下解析为 light，暗色从未进入自动化。

## 2 特征签名与扫描方法

- **S1/S2**：主题文件中重新出现暗色倒置的亮金值 `light-8 #FDE68A` / `light-9 #FEF3C7`。
- **S3**：类级守卫直接解析主题文件，枚举 light-3/5/7/8/9 亮度与对比度（覆盖所有使用该 token 的表面，不限于 DbAdmin）。
- 已知盲区：组件内联写死 `#FDE68A` 不扫（由 `check-style-tokens` 棘轮 + 本类 vitest 的对比度断言兜底）；`color-mix` 动态混色需人工按几何取证 SOP 实测。
- 扫描范围矩阵：宿主 `ForgeSelf.Web/src/**`、两棵插件树 `Plugins/*/web/src/**`、主题 `ForgeSelf.Web/src/styles/themes/**`、e2e `ForgeSelf.Web/e2e/plugins/**`。

## 3 Canonical fix

1. 对齐设计稿真源，修 `ForgeSelf.Web/src/styles/themes/workshop-forge.css` 暗色段：
   ```css
   --el-color-primary-light-3: #D97706;
   --el-color-primary-light-5: #B45309;
   --el-color-primary-light-7: #92400E;
   --el-color-primary-light-8: #78350F;
   --el-color-primary-light-9: #451A03;
   ```
2. 全仓扫 `--el-color-primary-light-(3|5|7|8|9)` 的 `color:` 用法，改为 `--el-color-primary`（文字强调）。
3. 跑 `pnpm run test` 的 `workshop-forge.contrast.test.ts`；在 DbAdmin e2e 冒烟内实测亮/暗选中行对比度。
4. 被否方案：只改 `.node.on` 背景（点状补丁，标签/横幅/按钮 hover 仍坏）；用 `!important` 或 `color-mix` 在组件里局部调（绕开 token 单一来源，新增漂移点）。

## 4 守卫台账

| 等级 | 载体 | 何时触发 | 覆盖盲区 |
|------|------|----------|----------|
| G2 | `ForgeSelf.Web/src/styles/themes/workshop-forge.contrast.test.ts` | 快档 `pnpm run test` / pre-commit 前端单测 | 主题 token 明暗语义与对比度，枚举 light-N |
| G1 | `ForgeSelf.Web/e2e/plugins/db-admin/db-admin.spec.ts` | 插件冒烟 e2e | 真实渲染选中行的 light/dark 对比度（防 CSS 特异性/运行时 token 漂移） |
| G3 | `scripts/check-problem-classes.ps1` signature S1/S2 | pre-commit 问题类扫描 | 主题文件重新写入倒置亮金值 |

## 5 案例列表

| case | 日期 | 现象一句话 | 是否逃逸（守卫已在但没拦住） |
|------|------|------------|------------------------------|
| [2026-11-06-dbadmin-dark-connection-contrast](../cases/2026-11-06-dbadmin-dark-connection-contrast.md) | 2026-11-06 | 暗色 DbAdmin 选中连接名 #FDE68A 底 + #F4F0E7 字，对比度 1.10:1 | 是（前案 2026-10-15 IM 网关暗色对比度修复未立 PC；本批按二次复发补 G2） |
