# Intent — db-admin 表名与结果表对齐修复

> 阶段：Stage 1｜Task ID：`2026-11-06-db-admin-table-alignment`｜日期：2026-11-06｜类型：缺陷修复

## Problem

用户输入3 反馈两处 UI 缺陷：
1. schema 树中的表名显示为「表 e2e_items」，「表」字多余，应直接显示表名。
2. 查询结果表格的列头（thead）与数据行（tbody）逐列错位；运行实例实测 `name` 列表头 left=318.8、数据 left=507.6（错位 188.8px）。

## Why

- 表名前缀是纯视觉噪音，用户明确要求去掉。
- 列错位不是「轻微不齐」：`<table>` 被宿主全局 Tailwind `.grid{display:grid}` 命中，thead/tbody 变成独立 grid 项，列宽各自计算，用户无法按列读取数据（数据库工具的核心功能受损）。

## Expected Outcome

1. schema 树中真实表直接显示表名；视图仍保留「视图」标识（避免丢失类型信息）。
2. 结果表格恢复原生 `display: table` 布局；`thead th` 与 `tbody td` 逐列左边界对齐（有真实数据行时误差 ≤1px）。
3. 同类问题有机器守卫：所有插件 `<table>` 不得使用宿主 Tailwind 的 display 工具类名。
4. 运行态截图验证（真实宿主 CSS + 有数据行）。

## Constraints

- 不改后端契约、不改数据库结构、不改鉴权逻辑。
- 不引入依赖、不改宿主 Tailwind 配置。
- 范围控制：只改 DbAdmin 视图 + e2e + 一个前端级静态守卫 + PC/case 台账；不顺手重构其他插件。
- 发布到用户 `:51888` 需另行授权；本次不停/启/杀用户宿主（只做只读复验）。

## Success Criteria

- 插件前端 `pnpm run test` + `pnpm run build` 绿；宿主 `pnpm run check` + `pnpm run test` + `design:check` 绿。
- DbAdmin e2e：表名 `.name` 文本严格等于 `e2e_items`；`getComputedStyle(table).display === 'table'`；有 1 行数据时表头/数据列左边界差 ≤1px。
- G2 守卫：mutation（把 `dba-grid` 改回 `grid`）转红，还原后转绿。
- PC-026/case 三处索引闭合；`check-problem-classes.ps1` 与 `ProblemClassRegistryTests` 绿。
