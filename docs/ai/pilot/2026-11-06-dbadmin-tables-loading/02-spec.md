# Spec — DbAdmin 表加载 loading + 交互规范 + CT

> Task ID：`2026-11-06-dbadmin-tables-loading`｜AC 编号无连字符

## 1 范围

**In**：`Plugins/DbAdmin/web/src/DbAdminView.vue`（局部 loading + 竞态防护）、`types.ts`（fixture 字段）、`DbAdmin.story.ts` + `DbAdmin.ct.spec.ts`（CT loading 状态）、规范 5 处、DbAdmin 两处文档、pilot 工件。

**Out**：后端/接口/数据库；其它插件行为；整页 loading 改造；发布。

## 2 功能需求

- **FR1** 每个连接独立 `tablesLoading` 状态；`loadTables` 开始时置 true、结束（成功/失败）置 false。
- **FR2** 左栏在连接展开且加载中时，节点下显示 `spinner + 加载表…`（`data-testid="tables-loading-<connId>"`，`role=status`）；加载中不渲染「（无表）」/旧表。
- **FR3** 同一连接加载中非 force 调用直接返回（去重）；force 重拉用请求序号，旧响应丢弃。
- **FR4** fixture 支持 `tablesLoading`，供 CT 定格 loading 状态。
- **FR5** 规范补强：异步 loading 位置/文案/时机、空态不抢先、去重/竞态、失败可重试；新插件 UI 状态变更须补 CT story + 像素断言。
- **FR6** CT 增加 `DbAdmin/TablesLoading` 行为断言 + 截图基线。

## 3 Interaction Design（有 UI）

| 交互点 | 触发 | 结果（点什么出现什么） | 状态模型 | 反馈 | 空态 | 边界 |
| --- | --- | --- | --- | --- | --- | --- |
| 展开连接加载表 | 点击连接节点 | 节点下出现 spinner「加载表…」；完成后表列表替换 loading | 未加载/加载中/有表/无表/失败 | 局部 spinner + 文本 | 加载结束后才显示「（无表）」 | 加载中重复点击不重复发；force 重拉旧响应丢弃 |
| 加载失败 | 请求失败 | loading 结束，顶部 error banner 显示「读取表失败：…」 | 失败 | 红横幅 | — | 可再次点击连接重试（缓存未写入） |
| 系统库展开 | 点击系统只读连接 | 同样显示局部 loading（不因只读而静默） | 未加载/加载中/有表 | 局部 spinner | — | 系统库无表时显示空，不显示锁提示 |

### 视觉规格（目标态）

| 表面 | light | dark | 判据 |
| --- | --- | --- | --- |
| 加载行 `.node-loading` | fg `--el-text-color-secondary`；spinner 边 `--el-border-color` / 顶边 `--el-color-primary` | 同 token（暗色自动深底） | 12px、左缩进 20px、flex gap 6px |
| 加载行位置 | 连接节点下、表列表位置 | 同 | 不占整页、不遮挡编辑器 |
| 动效 | `dba-spin 0.8s linear infinite`；`prefers-reduced-motion` 关闭动画 | 同 | 无持续高频装饰；仅 loading 期 |

## 4 Failure Modes

- PC-019（flex 压缩）：`.side > * { flex-shrink: 0 }` 已覆盖 loading 行 → 不适用（lens 已答）。
- PC-025（暗色 token）：spinner 用 `--el-*`，不引入裸色值；CT 在默认主题截基线。
- 竞态：force 重拉时旧响应覆盖新结果 → 请求序号丢弃。
- 失败：`handleError` 已写 banner；loading 必须在 finally 结束，不能卡住。

## 5 Acceptance Criteria

- **AC1** `loadTables` 有独立 loading 状态，成功/失败/竞态均正确结束。
- **AC2** 左栏连接展开后加载中显示 `tables-loading-<id>`（含 spinner + 文案），且不显示「（无表）」。
- **AC3** CT story `DbAdmin/TablesLoading` 行为断言通过 + 基线生成；`test:ct` 绿。
- **AC4** `Plugins/DbAdmin/web pnpm run build` 通过。
- **AC5** 规范 5 处补强落地（AGENTS/DESIGN/ui-ux/plugin-development/02-spec）。
- **AC6** DbAdmin ui-design-brief/06 评审稿补 loading 验收项。
- **AC7** pilot 00-07 齐，05 记真实命令与结果，07 汇报。

## 6 Unknown

- CT 基线首次生成是否受宿主 CT server/浏览器环境影响，以实际 `test:ct:update` 输出为准。
- 共享工作区存在并行会话未提交改动，`pnpm run check` 可能被其未完成文件拦红；本批单独跑 ESLint/CT/插件 build 并记录归因。
