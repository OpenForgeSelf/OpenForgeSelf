# Specification — db-admin 表名与结果表对齐修复

> 阶段：Stage 2｜Task ID：`2026-11-06-db-admin-table-alignment`｜日期：2026-11-06

## Functional Requirements

- FR1 schema 树中的真实表只显示表名，不显示「表」前缀；视图（`t.type === 'view'`）仍显示「视图 <name>」。
- FR2 DbAdmin 结果表格使用插件前缀类名（`dba-grid`），不得使用宿主 Tailwind 的 display 工具类名。
- FR3 结果表为原生 table 布局：`display: table`；`thead th` 与 `tbody td` 列左边界对齐（有数据行时误差 ≤1px）。
- FR4 插件冒烟 e2e 增加两条运行态断言：表名文本、结果表 display + 列对齐（插入一行并批准后断言）。
- FR5 新增 G2 前端静态守卫：扫描全部插件 `web/src/**/*.vue` 的 `<table>` 静态 class，禁止 `grid/flex/block/inline/inline-block/inline-flex/contents/hidden`。
- FR6 问题类归档：PC-026 + case + 三处索引闭合。

## Input

- 用户输入3 原文（两处 UI 反馈）。
- 运行实例 `:51888` db-admin v0.1.2 的实际查询结果（`SELECT name, type, sql FROM sqlite_master LIMIT 20`）。

## Output

- 修复后的 `DbAdminView.vue`（模板类名/表名文案）。
- e2e 断言与截图（`05-select.png`、`04-schema.png`）。
- G2 守卫测试文件与 PC-026/case。

## Business Rules

- 表名展示只影响文案，不改变 schema 数据的请求/缓存/展开逻辑。
- 结果表格的对齐是表格布局语义，不得用 `!important` / 局部 `display: table` 覆盖来掩盖类名冲突。
- 插件自定义类名必须带插件前缀（DbAdmin = `dba-`），与宿主 Tailwind 工具类隔离。

## Boundary Conditions

- 空结果集（0 行）：表头仍按列显示；不因无数据行而隐藏表头。
- 只有表头无数据时，列宽由表头内容决定；有数据行时按内容最宽值分布，表头与数据同行对齐。
- 视图类型：保留「视图」文字标识；真实表无前缀。
- 系统连接只读、用户连接、审批/拒绝链路行为不变。

## Error Handling

- 本批不新增错误路径；SQL 错误、401、审批拒绝等既有错误呈现不变。
- e2e 若拿不到 SELECT 结果表或数据行，按超时失败（不静默跳过）。

## Compatibility

- 后端契约（columns/columnTypes/rows）不变；前端 `normalize.ts` 不变。
- CT 故事 `DbAdmin/ReadResult` / `RestoreBefore` 仍按 `[data-testid="result-grid"] table` 定位，类名改动不影响。
- 其他插件表格类名（`rank-table`/`dv-table`/`aiw-table`）不受影响。

## Non-functional Requirements

- 无新增依赖；改动只涉及模板文案、CSS 类名、测试与文档。
- G2 守卫为静态文件扫描，耗时毫秒级；G1 e2e 复用既有冒烟用例，不新增第二条 e2e。

## Failure Modes（PC-026 lens 预填）

| PC-026 lens 问题 | 本任务如何满足 |
| --- | --- |
| 插件自定义类名是否与宿主 Tailwind 工具类同名？ | 结果表类名从 `grid` 改为 `dba-grid`；守卫枚举全部插件 `<table>` class token |
| 承载布局语义的元素 display 是否被宿主全局 CSS 改写？ | e2e 断言 `getComputedStyle(table).display === 'table'`（真实宿主 CSS） |
| e2e 是否断言列左边界对齐（有真实数据行）？ | e2e 增加「批准插入 1 行」步骤，随后断言 `thead th` 与 `tbody td` 左边界差 ≤1px |

## Interaction Design（交互设计 · 必写节）

### 交互规格（点什么出现什么）

| 交互点 | 触发 | 结果（点什么出现什么） | 状态模型 | 反馈 | 空态 | 边界 |
| --- | --- | --- | --- | --- | --- | --- |
| schema 树表节点 | 展开连接 | 显示 `▸ users` / `▸ 视图 v_active_users`，真实表无「表」前缀 | 展开/折叠 | 无 | 「（无表）」 | 长表名 ellipsis |
| 查询结果表格 | 执行 SELECT 成功 | 表头 `# / 列名 类型` 与数据行逐列对齐；上下滚动时表头 sticky | 加载/成功/失败/空 | 耗时/行数统计 | 0 行仍显示表头 | 长值 ellipsis（max-width 280px） |
| 结果表横向溢出 | 列宽超过容器 | `.grid-wrap` 内横向滚动；表头与数据同表滚动，不脱节 | 溢出 | 滚动条 | 不适用 | 不产生页面级横向滚动 |

### 视觉规格（目标态）

- 本批不新增颜色/间距；只改类名与文案。表格样式 token 保持 `--el-border-color` / `--el-bg-color-overlay` / `--el-text-color-secondary`。
- 对齐线：`thead th` 与 `tbody td` 同列 left 边界差 ≤1px（目标 0）；`#` 行号列宽 36px。
- 主题矩阵：本批不触及颜色/背景/边框 token，亮/暗两态沿用既有值（PC-025 的对比度守卫继续覆盖）。

### 交互验收标准

- AC1 表名 `.name` 文本严格等于表名（无「表」前缀）；视图保留「视图」。
- AC2 结果表 display=table；表头与数据列左边界差 ≤1px。
- AC3 0 行结果仍显示表头（既有行为，不回归）。

### 走查符合性（DoD 绑定）

- 走查用真实运行实例（只读 SELECT）截图 + 读图；按 `ui-ux-design` 走查清单核对表格层级/对齐/长文本/暗色可读性。本批证据见 05。

## Acceptance Criteria

- AC1 schema 树真实表名直接显示（无「表」前缀），视图保留「视图」标识；e2e 断言 `.name` === `e2e_items`。
- AC2 结果表 `getComputedStyle(table).display === 'table'`；有 1 行数据时逐列左边界差 ≤1px；e2e 断言通过。
- AC3 空结果（0 行）表头仍显示；既有 `SELECT` 用例通过。
- AC4 插件前端 `pnpm run test` + `pnpm run build` 绿；宿主 `pnpm run check` + `pnpm run test` + `design:check` 绿。
- AC5 G2 守卫有效（mutation 红 → 还原绿）；PC-026/case 三索引闭合；`check-problem-classes.ps1` + `ProblemClassRegistryTests` 绿。
- AC6 运行实例只读复验：:51888 查询截图表头/数据对齐（发布后生效；本批源码验证以 e2e 真实宿主 CSS 为准）。

## Unknown

| 不确定点 | 影响 | 处理方式 |
| --- | --- | --- |
| 运行实例 `:51888` 当前插件为 0.1.2（未含本批修复） | 用户看到的现象仍会持续到发布新版本 | 本批先交付源码 + 证据；发布需用户授权后走本地目录更新源，届时只读复验 |
| 其他插件未来是否会在 `<table>` 上用其他 Tailwind 工具类 | 当前守卫覆盖 display 类，非 display 类（如 `w-full`）不阻止 | 本批按 PC-026 不变量覆盖 display 类；如出现新形态再扩守卫 |
