# Plan

> 阶段：Stage 3｜Task ID：`2026-11-14-db-admin-list-databases`
> 闸门1 已批（2026-11-14「P1 一并做」）。下面两段都要做。实施目录是本工作树。

## Files To Change

- file: `Plugins/DbAdmin/web/src/connForm.ts`
  reason: `validateForm` 按 provider 放宽；oracle 文案改为服务名。`buildConnStr` 在库名为空时不要写出空的 `Database=`。
- file: `Plugins/DbAdmin/web/src/connForm.test.ts`
  reason: 空库名矩阵：mysql/sqlserver/pg 通过，sqlite/oracle 拒绝。
- file: `Plugins/DbAdmin/web/src/types.ts`
  reason: 列库条目类型；`QueryRequest` 增加可选 `database`。`QueryTab` 不放这里。
- file: `Plugins/DbAdmin/web/src/tabs.ts`
  reason: `QueryTab.database?`；`tableTabKey` / `createTableTab` 带库名，避免不同库同名表串标签。
- file: `Plugins/DbAdmin/web/src/normalize.ts`
  reason: 归一 `databases` 响应（name/kind/isDefault/isSystem）。
- file: `Plugins/DbAdmin/web/src/api.ts`
  reason: `databases(id)`；`tables` / `tableDetail` / `query` 传递可选 `database`。
- file: `Plugins/DbAdmin/web/src/DbAdminView.vue`
  reason: 三类 provider 点连接先列库；点库再列表；表单标签与说明；筛选与刷新。
- file: `Plugins/DbAdmin/web/src/api.test.ts`（若已有 tables/query 断言）
  reason: 锁住 `database` 查询参数出现在 URL / body。
- file: `ForgeSelf.Web/src/components/DbAdmin.story.ts`
  reason: `ConnFormInvalid` 改为 oracle 空服务名；新增「库列表加载中 / 已列出」夹具。
- file: `ForgeSelf.Web/src/components/DbAdmin.ct.spec.ts`
  reason: 替换「请填写数据库名」断言；加 AC-I2 库节点。
- file: `docs/02-features/044-db-admin.md`
  reason: 实现后把 §4 的「未接上」改成实现后的行为。缺口句本规划阶段已先写入。
- file: `Plugins/DbAdmin/docs/ui-design-brief.md`
  reason: 给设计师写「点连接先出全部库」的旅程，不写代码路径。
- file: `Plugins/DbAdmin/docs/02-交互说明.md` 或 `06-UI交互评审稿.md`（只改已经描述「点连接加载全部表」的那几句）
  reason: 避免交互真源仍写旧行为。
- file: `Plugins/DbAdmin/web/src/exportCsv.ts` 与新建 `exportResult.ts`（或同级纯函数文件）
  reason: §8.2 的全部页 CSV 拼接、JSON、INSERT 必须是可单测纯函数；下载与翻页循环留在视图。
- file: `Plugins/DbAdmin/web/src/tabs.ts` / `restore.ts`（若快照有标题字段）
  reason: 自由标签 `title?`；快捷键「下一张/上一张/关闭」若能做成纯函数也放 tabs。
- file: `Plugins/DbAdmin/plugin.json`
  reason: 0.2.1 → 0.2.2。

## §8.2 实施要点

- 快捷键加在现有 `onWindowKey`，浮层 `overlay !== null` 时直接 return。
- 列宽用表头内的窄拖拽条，不引入库。状态 `colWidths: Record<string, number>` 按列名。
- 右键菜单一个组件内的绝对定位层即可，Esc 与点击空白关闭。不要用浏览器默认菜单（`preventDefault`）。
- 导出全部：复用 `dbAdminApi.query`，body 带当前标签的 `database` 与该结果语句。上限 50 页。纯函数负责把多页行拼成 CSV。
- INSERT/JSON 只针对当前页已在内存中的行。
- 筛选一个输入框同时滤连接名、库名、表名。先做谓词纯函数再接到模板。

## Implementation Steps

1. 先改 `connForm` 与单测（红：mysql 空库名目前失败；绿：放宽后通过，sqlite/oracle 仍红灯用例保持失败文案）。
2. `api` + `normalize` + 类型：接上已有 GET，不改后端。
3. `tabs.ts` 给表标签加上库名维度；自由查询标签记录「当前从哪一个库打开」。旧快照无该字段时执行不带 `database`。
4. `DbAdminView`：`selectConnection` 对三类 provider 改调 `loadDatabases`。库节点点击调 `loadTables(id, db)`。loading / 序号 / 空态顺序复制表现有 `loadTables`（约 435–449 行）。
5. 表单模板：按 provider 改标签、placeholder、postgresql 一句说明。
6. CT story 与断言。e2e 冒烟只加「sqlite 仍无库层」的断言；server 型不在冒烟里连真实库（环境没有就不要写死会失败的用例）。
7. 回写 044 §4 与 ui-design-brief。版本号：行为变化，`plugin.json` `0.2.1` → `0.2.2`（仅插件补丁，不动宿主 csproj）。

## Test Plan

1. `connForm.test.ts`：五类 provider 的空库名。
2. `api.test.ts` 或新增小测：`tables(id, 'demo')` 的 URL 含 `database=demo`；`databases` 路径为 `/connections/{id}/databases`。
3. CT：夹具连接 provider=mysql，展开后见库名与「加载数据库…」不与空态同时出现。
4. 既有 sqlite CT / e2e 冒烟保持绿。
5. 不新增后端测试（行为未改）。实施时跑一遍 `dotnet test --filter FullyQualifiedName~DbAdmin` 确认没被前端无关改动带红；若只改 web，此后端过滤集仍跑，因为插件版本与文档守卫可能扫到。若守卫不涉及则记录「未改 .cs，过滤集仍绿/未跑及原因」。

## Verification

### Build

`cd Plugins/DbAdmin/web && pnpm run build`

### Unit

`cd Plugins/DbAdmin/web && pnpm run test`  
宿主若改了 story/CT：`cd ForgeSelf.Web && pnpm run check` 与相关 vitest/CT。`pnpm run design:check`。

### Integration

无新后端接口。过滤集 `dotnet test ForgeSelf.Api.Tests --filter FullyQualifiedName~DbConnectionServiceTests` 作为回归，预期与改前相同。

### E2E

`e2e/plugins/db-admin` 冒烟一条：sqlite 主链路仍在，且树中没有库层。server 型无实例则不新增必红用例。

### Other

dev-stack 走查 sqlite 一条。有用户提供的 server 连接再走「空库名 → 点连接见库列表 → 点库见表」。亮/暗各一次。无 server 实例则 05 写 Unknown。

## 偏差预记

2026-11-07 的 02-spec 写的是表头下拉选择器。本批改为树。实施时不要把下拉做回来。
