# Plan

> 阶段：Stage 3｜**必须具体到真实文件路径**，禁止只写「修改 Service、增加测试」。
> Task ID：`2026-11-06-db-admin-query-tabs`

## Files To Change

- file: `Plugins/DbAdmin/web/src/tabs.ts`（新增）
  reason: 多标签纯逻辑（key 生成、表标签去重聚焦、上限、关闭后邻居激活、标题口径），可在 vitest 直接测。
- file: `Plugins/DbAdmin/web/src/tabs.test.ts`（新增）
  reason: 锁 BR-1/BR-2/BR-4/BR-5/BR-6/FR-11 与关闭激活规则。
- file: `Plugins/DbAdmin/web/src/sqlSplit.ts`（新增）
  reason: 镜像 `SqlGuard.Strip/SplitStatements` 的客户端切分器（跳过字符串/引号标识符/注释/美元引号/TRIGGER 块），
    输出 `{text, hasCode}` 并区分"无有效语句"与"未闭合引号"两类错误。
- file: `Plugins/DbAdmin/web/src/sqlSplit.test.ts`（新增）
  reason: 锁 BR-13/BR-14/AC-20（含分号在字符串/注释/触发器中、未闭合引号、注释-only 片段）。
- file: `Plugins/DbAdmin/web/src/results.ts`（新增）
  reason: 结果标签模型（`ResultSet`：sql/status/rows/error/page/pageSize/running），
    纯函数 `buildResultSets` / `markSkippedFrom` / `statusLabel` / `activeResult`，供视图编排与单测复用。
- file: `Plugins/DbAdmin/web/src/results.test.ts`（新增）
  reason: 锁 BR-15/BR-17/BR-19（结果标签顺序、失败即停标未执行、分页归属）。
- file: `Plugins/DbAdmin/web/src/exportCsv.ts`（新增）
  reason: RFC 4180 转义 + 表头 + UTF-8 BOM + 文件名（BR-21），纯字符串处理可单测，避免视图内手写 CSV。
- file: `Plugins/DbAdmin/web/src/exportCsv.test.ts`（新增）
  reason: 锁 AC-23（逗号/引号/换行/NULL/文件名页码）。
- file: `Plugins/DbAdmin/web/src/restore.ts`
  reason: 快照从「按连接草稿」升级为「标签 + 每标签 SQL + 活动标签 + 各连接默认表」，含旧快照迁移与 64KB 预算裁剪。
- file: `Plugins/DbAdmin/web/src/restore.test.ts`
  reason: 新增迁移/往返/预算裁剪/脱敏/默认表用例，防止旧快照丢草稿。
- file: `Plugins/DbAdmin/web/src/types.ts`
  reason: `DbAdminFixture` 增 `tabs?: Array<{key,connectionId,table?,sql?,reply?}>`、`activeTabKey?`、`defaultTables?`、`lastConnectionId?`；
  `DbConnectionInput` 增可选 `defaultTable`（本机偏好，不进请求体）。
- file: `Plugins/DbAdmin/web/src/connForm.ts`
  reason: `toSubmitBody` 显式剔除 `defaultTable`（保证请求体与后端契约不变）；表单默认值/编辑回填允许携带该字段。
- file: `Plugins/DbAdmin/web/src/connForm.test.ts`
  reason: 新增断言：`toSubmitBody` 结果不含 `defaultTable`（AC-12 机械守卫）。
- file: `Plugins/DbAdmin/web/src/DbAdminView.vue`
  reason: 视图改造——标签条渲染、活动标签派生（编辑器/结果/分页/错误/运行态）、执行与审批回填按标签作用域、恢复逻辑切换、
  关闭/上限/空态、连接联动、样式（仅 `--el-*` token）。
- file: `ForgeSelf.Web/src/components/DbAdmin.story.ts`
  reason: 新增 `DbAdmin/QueryTabs` story（两个表标签 + 各自结果/错误），供 CT 像素与行为断言。
- file: `ForgeSelf.Web/src/components/DbAdmin.ct.spec.ts`
  reason: 新增 CT 用例：标签条渲染、切换标签内容互换、重复开表不新增、关闭后邻居激活。
- file: `ForgeSelf.Web/e2e/plugins/db-admin/db-admin.spec.ts`
  reason: 在既有**唯一**冒烟主链路内扩断言：双击/查询按钮开表标签、重复开表不新增、两个标签切换内容不丢、
  活动标签亮/暗对比度（真实宿主，零 mock）。
- file: `Plugins/DbAdmin/docs/ui-design-brief.md`
  reason: 面向设计师的交互摘要补「多标签查询」功能/旅程/五态。
- file: `docs/02-features/044-db-admin.md`
  reason: 功能真源补多标签工作区行为与恢复口径。
- file: `Plugins/DbAdmin/plugin.json`
  reason: 插件行为有实质变化，版本 `0.1.4 → 0.2.0`（仅 `plugin.json`，不动 csproj 版本行）。

## Implementation Steps

1. 新建 `tabs.ts`：定义 `QueryTab`（`key/connectionId/table?/sql/results/activeResultIndex/lastSql/running/runSeq/batch?`）、
   `tableTabKey` / `manualTabKey` / `tabTitle` / `openTableTab(tabs, connId, table)`（已存在只返回 key）、
   `closeTab(tabs, key)`（右邻优先、无右取左、空则 null）、`MAX_TABS = 12`。
2. 写 `tabs.test.ts`：同表去重、不同连接同名表不合并、上限拒绝、关闭激活规则、标题不加「表」字。
3. 新建 `sqlSplit.ts`：`splitStatements(sql)` 扫描器——跳过 `'…'`（`''`/`E'…'` 反斜杠）、`"…"`/`` `…` ``/`[…]`、
   `--`、`/* */`、`$tag$…$tag$`；`CREATE [TEMP] TRIGGER … BEGIN…END` 内分号不切；返回 `{text, hasCode}[]`；
   未闭合引号/注释抛 `SqlSplitError`。写 `sqlSplit.test.ts` 覆盖 AC-20。
4. 新建 `results.ts`：`ResultSet`（sql/status/ok|error|awaiting|skipped/running/result/error/page/pageSize/runSeq）+
   `buildResultSets(statements)` / `markSkippedFrom(sets, from, reason)` / `statusLabel` / `SET_PAGE_SIZE_DEFAULT`。
   写 `results.test.ts` 覆盖 BR-15/BR-17/BR-19。
5. 改 `restore.ts`：`RestoreSnapshot` 增 `tabs`、`activeTabKey`、`defaultTables`；
   `buildSnapshot(connections, tabs, activeTabKey, defaultTables)`；`loadSnapshot` 兼容旧 `drafts`（BR-8）并回填 `defaultTables ?? {}`；
   `fitToBudget` 以 `drafts`（key→sql）处理，先丢非活动标签。
6. 改 `restore.test.ts`：旧快照迁移、往返一致、预算裁剪保留活动标签、`stripSecrets` 仍无密码/连接串、`defaultTables` 往返。
7. 改 `types.ts` + `connForm.ts` / `connForm.test.ts`：`DbConnectionInput.defaultTable?`；
   `toSubmitBody` 显式删除该字段，单测断言请求体不含 `defaultTable`（AC-12）；fixture 增 `tabs`/`replies`/`defaultTables`。
8. 新建 `exportCsv.ts`：`toCsv(columns, rows, page)` / `csvFileName(base, resultIndex, page, now)`；
   RFC 4180 转义（逗号/引号/换行）、NULL → `NULL`、UTF-8 BOM；写 `exportCsv.test.ts`。
9. 改 `DbAdminView.vue`：
   - 状态：`tabs = ref<QueryTab[]>([])`、`activeTabKey`、`defaultTables`、`lastConnectionId`；
     `activeTab`/`sql`（读写活动标签）/`results`/`activeResultIndex` 派生；模板只读。
   - **执行**：`runFromEditor(tab)` = 读 `editorEl.selectionStart/End` → 选区非空取选区，否则全文 →
     `splitStatements` → 空语句红条；`buildResultSets` → `runBatch(tab, sets, 0)`。
   - **批量编排**：`runBatch` 顺序 `POST /query`（每条单语句）；ok/error → 写对应结果标签；error → `markSkippedFrom`；
     `awaiting_approval` → 记 `batch = {tabKey, sets, nextIndex, approvalId}` 并开审批；`resolveApproval` 应用结果后
     按批准/拒绝继续或标未执行；关闭标签时丢弃 `batch`。
   - 审批作用域：`approvalTabKey` + `approvalResultIndex` 记录发起位置（FR-10/FR-18）。
   - 分页：`goResultPage(index, p)` 只重发 `results[index].sql`，页码写回该结果标签（FR-19）。
   - 树：表节点行名**单击** → `openTableQuery` + 展开列；`▸/▾` 单击 `stopPropagation` 只切换展开；`insertSelect` 移除。
     `selectConnection`：展开/收起；展开时若有默认表 → `openTableQuery(默认表)`，无默认表 → 仅加载表；始终更新 `lastConnectionId`。
   - 默认表：表单增输入（`connForm.defaultTable`），保存成功后写 `defaultTables[connId]`；删除连接时清除；节点 `title` 提示。
   - 恢复：`applyRestore` / `persist` 标签口径 + 默认表；`applyFixture` 支持 `tabs`/`replies`；`restoredHint` 改标签口径。
   - 模板：`conn-bar` 下 `.query-tabs`；执行栏按钮随选区改文案（`run` testid）；结果区上方 `.result-tabs`（>1 条时渲染，
     状态点 + `result-tab-i`）；无标签 `workspace-empty`；连接表单「默认表」输入（`cf-default-table`）。
   - 编辑器：模板 `ref="editorEl"`，`@select`/`@keyup` 更新 `selectionLength`（按钮文案用），`Ctrl+Enter`/`F5` 走同一执行入口。
   - 树：加「筛选表名」输入（本地过滤 `tables[connId]`，不发请求）与连接行「刷新」按钮（`loadTables(connId, true)`）。
   - 结果：工具条「复制整行」（制表符分隔，无选中禁用）、「导出 CSV」（`exportCsv` + `Blob` 下载，无结果禁用）；
     双击单元格 → 详情浮层（完整值 + 复制，Esc/遮罩关闭）；状态栏「N 行 · X ms」。
   - 复制：`navigator.clipboard.writeText` 失败 → banner 提示（BR-22）。
   - 样式：`.query-tabs/.qtab/...`、`.result-tabs/.rtab/.rtab.on/.rtab-dot`、`.workspace-empty`、`.table-filter`、
     `.result-tools/.cell-detail`，全部 `--el-*` token。
10. `DbAdmin.story.ts` 加 `DbAdmin/QueryTabs`（双表标签 + 每标签多结果）与 `DbAdmin/QueryTabsMultiResult`（单标签两结果）。
11. `DbAdmin.ct.spec.ts`：多标签切换/关闭；多结果标签切换与失败态；表过滤；`CT_FRESH=1 pnpm run test:ct:update` 生基线后读图。
12. 扩展 `db-admin.spec.ts` 冒烟：默认表开关、单击表开标签/去重、`▸` 不建标签；表过滤不发请求；选中执行只发选中；
    两条 SELECT → 两个结果标签；切回查询标签内容不丢；复制/导出 CSV（断言下载文件名与内容首行表头）；
    活动标签与结果标签 light/dark 对比度。
13. 更新 `ui-design-brief.md`、`044-db-admin.md`、`plugin.json` 版本（0.1.4 → 0.2.0）；按 §7.5 回写日记与 TODO，
    并把「明确不纳入本批」清单同步到 `docs/07-decisions/not-taken-decisions.md`。

## Test Plan

1. 纯逻辑单测（`tabs.test.ts` / `sqlSplit.test.ts` / `results.test.ts` / `exportCsv.test.ts` / `restore.test.ts` / `connForm.test.ts`）：
   去重、上限、关闭激活、切分边界、失败即停、分页归属、CSV 转义、旧快照迁移、预算裁剪、脱敏、默认表不进请求体。
2. 组件测试（`DbAdmin.ct.spec.ts`）：多标签切换、多结果标签切换/失败态、表过滤、关闭激活、空态；CT 像素基线（新 story）。
3. 冒烟 e2e（`e2e/plugins/db-admin/db-admin.spec.ts`，**单条用例内扩展**）：真实宿主 + 真实 SQLite 库，
   覆盖默认表、开标签/去重/切换不丢、表过滤、选中执行、多结果标签、CSV 下载、对比度；不新增第二条 e2e（铁律 24）。
4. 插件前端构建 + 宿主全量前端门禁 + 设计门禁（`design:check`），确认无裸色值与 token 违规。
5. 起源码本地前后端走查（`pwsh scripts/dev-stack.ps1`）：按 §3.4 清单逐项核对标签交互、空态、上限提示、亮/暗截图。

## Verification

### Build

```bash
cd Plugins/DbAdmin/web && pnpm run build
cd ForgeSelf.Web && pnpm run check
```

### Unit Test

```bash
cd Plugins/DbAdmin/web && pnpm run test
cd ForgeSelf.Web && pnpm run test
```

### Integration Test

N/A —— 本批不改后端、不改 API 契约，无集成层变更（后端仅 `dotnet build` 作为回归前置，非必需）。

### E2E

```bash
cd ForgeSelf.Web && pnpm exec playwright test --config=playwright.config.ts e2e/plugins/db-admin
```

### Other Checks

```bash
cd ForgeSelf.Web && pnpm run design:check
cd ForgeSelf.Web && pnpm run test:ct          # 新增 story 基线：先 CT_FRESH=1 pnpm run test:ct:update 再复查
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts\dev-stack.ps1        # 起源码本地前后端走查
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts\dev-stack.ps1 -Stop
```

## Plan 偏差记录

> 实现中发现 Plan 与仓库实际不符时，先在此记录偏差，再修正 Plan，不得直接绕过。

| 时间 | 偏差点 | 原 Plan | 修正后 |
| --- | --- | --- | --- |
| — | — | — | — |
