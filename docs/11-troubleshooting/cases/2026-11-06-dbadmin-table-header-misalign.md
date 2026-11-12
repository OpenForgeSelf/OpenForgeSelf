---
id: 2026-11-06-dbadmin-table-header-misalign
title: DbAdmin 结果表 class="grid" 命中宿主 Tailwind，表头与内容列错位
pc: PC-026
severity: P2
evidence: Verified
---
# Case：2026-11-06 DbAdmin 结果表 class="grid" 命中宿主 Tailwind，表头与内容列错位

> Case 只讲「这一次发生了什么」；规则/修法/扫描方法一律引用 PC，不复述。

## 1 现象

用户反馈（2026-11-06 输入3）：「1、表名不用加表字，直接显示表名即可。2、查询结果的列表头和内容没对齐。」

运行实例 `:51888`（db-admin v0.1.2）实测：左侧 schema 树显示「表 AIModel」；对 ForgeSelf 系统库执行
`SELECT name, type, sql FROM sqlite_master LIMIT 20` 后，结果表看起来是「# / name String / type String / sql String」，
但数据行从更左侧开始，逐列右移不一致。

## 2 复现红灯

- **运行实例 DOM 实测（Verified）**：`document.querySelectorAll('table.grid').length = 1`；computed `display = grid`（非 table）；
  `thead th` 的 left = 282.8 / 318.8 / 410.0 / 501.2，`tbody` 第一行 td left = 282.8 / 318.8 / 598.8 / 650.8 →
  name 列 diff=0、type 列 diff≈-188.8、sql 列 diff≈-149.6。
- **仓库 e2e 红灯（Verified）**：
  1. 首次加断言 `toHaveText('e2e_items')` → 实际 `<span class="name">表 e2e_items</span>`（表名前缀）；
  2. 修前缀后再跑 → `expect(getComputedStyle(table).display).toBe('table')` 收到 `"grid"`（`.temp/tmp/dbadmin-ui-red2.log`）。
- 修复后同一 e2e 通过（`1 passed`，`.temp/tmp/dbadmin-ui-green3.log`）。

## 3 根因（5 Whys + 第 6 问）

1. 为什么表头与数据列错位？→ `<table>` 的 computed `display` 是 `grid` 而不是 `table`。
2. 为什么 display 变成 grid？→ class 名 `grid` 命中宿主全局 Tailwind 工具类 `.grid{display:grid}`。
3. 为什么用 `grid` 当类名？→ 想表达「数据网格」，选了通用英文名，没有插件前缀。
4. 为什么宿主 CSS 会作用到插件？→ 插件组件挂载在宿主 document 内（非 iframe/Shadow DOM），全局 class 选择器直接命中。
5. 为什么测试没拦住？→ CT 画廊不加载宿主 Tailwind（预览正常）；e2e 只断言结果格可见，没有断言 computed display 与列对齐。

- **第 6 问：为什么现有流程/守卫没让我一开始就写对？**
  归因：没有「插件类名 vs 宿主工具类」的问题类/检查项；`ui-ux-design` 走查清单关注 CRAP 与对比度，
  不检查全局 CSS 类名冲突；e2e 视觉断言也没有「元素 display 语义」这一层。

## 4 同类扫查命中表

| 位置（文件:行） | 同根因？ | 处置（已修 / 已守卫 / TODO#） |
|-----------------|----------|-------------------------------|
| `Plugins/DbAdmin/web/src/DbAdminView.vue:783` `<table class="grid">` | 是 | 已修：改为 `class="dba-grid"`，CSS 同步 |
| `Plugins/CostScope/web/src/components/*.vue` `<table>`（无 class） | 否 | 无命中 |
| `Plugins/FileTools/web/src/components/FoldersPanel.vue` `class="rank-table"` | 否 | 无命中 |
| `Plugins/DesignSystem/web/src/delivery/DeliveryMode.vue` `class="dv-table"` | 否 | 无命中 |
| `Plugins/AIAgent/web/src/.../BlockRenderer.vue` `class="aiw-table"` | 否 | 无命中 |

结论：仅 DbAdmin 1 处同根因；其余插件均已使用带前缀的表类名。

## 5 修复与被否方案

- 修：`<table class="dba-grid">` + CSS `.dba-grid`；同时把表名渲染从 `表 {{ name }}` 改为直接 `{{ name }}`（保留 `视图` 前缀）。
- 被否：在 scoped CSS 加 `display: table` 覆盖（类名仍同名，后续 Tailwind `.grid` 新增属性会再次命中）；
  `!important`；iframe/Shadow DOM 隔离（成本过高）。

## 6 重构提案与结论

R1（局部，≤3 文件、同模块）：DbAdmin 模板/CSS + e2e + 新增 G2 守卫，任务内完成并披露。无跨模块契约变更。

## 7 守卫落地

- **G2**：`ForgeSelf.Web/src/styles/pluginTableClassGuard.test.ts`（扫描全部插件 `<table>` 静态 class token；已做 mutation 验证：把 `dba-grid` 改回 `grid` 时测试转红）。
- **G1**：`ForgeSelf.Web/e2e/plugins/db-admin/db-admin.spec.ts`：真实宿主 CSS 下断言 `display === 'table'`，
  并在批准插入 1 行后断言 `thead th` 与 `tbody td` 逐列左边界差 ≤1px。
- **G3**：PC-026 S1 签名（`scripts/check-problem-classes.ps1`）。

## 8 逃逸分析（二次复发必做，首见可写「N/A 首次」）

N/A 首次。此前无「插件类名与宿主 Tailwind 工具类冲突」问题类；本次首见即补 G2，无需逃逸分析。

## 9 基建改进

- 新增 G2 前端静态守卫，把「插件类名 vs 宿主工具类」从人工走查升级为机器判据；
- e2e 冒烟增加两层运行态断言（computed display + 列左边界），不再只断言「结果格可见」；
- 形成 PC-026 lens，后续新插件/新表格组件设计时必须回答类名前缀与 display 语义。

## 10 复用赋能

- 任何新增插件前端页面（尤其表格、列表、栅格容器）在 Spec/Plan 阶段引用 PC-026 lens；
- 任何修改 `Plugins/*/web/src/**/*.vue` 的批次，`pnpm run test` 会跑 G2 守卫；
- 任何 DbAdmin 结果区/表格改动，e2e 冒烟会跑 G1 断言。

## 11 证据

| 结论 | 等级 | 出处（文件:行 / 命令输出 / 截图） |
|------|------|-----------------------------------|
| 运行实例 table display=grid、列错位 188.8px | Verified | browser_use 在 :51888 的测量输出（见会话记录） |
| 表名带「表」前缀 | Verified | e2e 红灯 `.temp/tmp/dbadmin-ui-red.log`（`<span class="name">表 e2e_items</span>`） |
| display=grid 红灯 | Verified | `.temp/tmp/dbadmin-ui-red2.log`（Received: "grid"） |
| 修复后 e2e 绿 | Verified | `.temp/tmp/dbadmin-ui-green3.log`（1 passed） |
| G2 守卫有效 | Verified | mutation：`.temp/tmp/dbadmin-guard-mutation.log`（exit 1）；还原后 exit 0 |
| 修复后截图对齐 | Verified | `ForgeSelf.Web/screenshots/e2e/db-admin/05-select.png`（读图：表头 # / id / name 与数据行 1 / 1 / e2e_row 对齐） |
