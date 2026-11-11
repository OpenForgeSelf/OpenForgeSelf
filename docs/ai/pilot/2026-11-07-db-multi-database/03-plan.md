# Plan — DbAdmin 连接级多库（文件级）

> Task ID：PILOT-db-multi-database ｜ 顺序即实施顺序；每步后可跑对应检查。实体零改动（无 xcode）。

## S1 后端连接层（多库机制核心）

1. `Plugins/DbAdmin/Services/DbConnectionService.cs`
   - `Resolve(String? id)` → `Resolve(String? id, String? database = null)`；新增派生注册名 `DeriveConnName(id, database)` + 按库连接串重写 `BuildConnStrFor(e, database)`（form：改 `Database=`/`Initial Catalog=`/PG 空默认 `postgres`/mysql 空省键；connstr：DbConnectionStringBuilder 改写/补键）。
   - `ApplyForm` :201 与 connstr→form 路径 :446 按 FR1 矩阵分支化（sqlite/oracle 必填文案不变）。
   - 新增 `ListDatabases(String id)`（FR2 矩阵 SQL，经 `DAL.Create(rc.ConnName).Query` + `Wrap`），返回 `DatabaseList` 模型。
   - `Test` 复制路径校验 :446 同步分支化。
2. `Plugins/DbAdmin/Models/DbAdminModels.cs`
   - 新增 `record DatabaseInfo(String Name, String Kind, Boolean IsDefault, Boolean IsSystem)` + `record DatabaseList(String ConnectionId, String Provider, String? DefaultDatabase, IReadOnlyList<DatabaseInfo> Databases)`。
   - `QueryRequest` + `public String? Database`；`ApprovalView` + 可空 `Database`。

## S2 执行与审批链

3. `Plugins/DbAdmin/Services/DbQueryService.cs`
   - `ExecuteCore` :67 `Resolve(cid, req.Database)`；`PendingWrite` + `Database`（:26），`Enqueue`/`Resolve`（审批）重放透传；审计 Detail 追加 `db=<名>`（BR4）。
   - sqlite/oracle 的 database 合法性检查（FR4：oracle 拒、sqlite 仅 `main`）放 `Resolve` 或执行前一处集中实现。
4. `Plugins/DbAdmin/Controllers/DbAdminController.cs`
   - `GET connections/{id}/databases`；`Tables`/`Table` 加 `[FromQuery] String? database`；`POST query` body 已带（S1.3）。

## S3 MCP 工具契约

5. `Plugins/DbAdmin/Services/DbAdminToolFunctions.cs`
   - 4 个既有工具（:390/:415/:446/:483）：删 `"type":"string"`（connectionId，输入4）+ 加可选 `database`（无 type，`OptString` 取值，description「可选；默认为连接的默认库；Oracle/SQLite 不支持切换」）+ connectionId description 改双收文案。
   - 新增 `db_list_databases` 工具（:576-588 工具清单 + Invoke 分发 → `_connections.ListDatabases`）。
   - 工具 → HTTP 等价：`db_list_tables/describe/query/execute` 内部改传 database 到既有服务方法（ListTables/DescribeTable/Execute 链路加参）。

## S4 前端

6. `Plugins/DbAdmin/web/src/connForm.ts` + `connForm.test.ts`：validateForm 分支化（FR1）、buildConnStr mysql 空库省 `Database=`、placeholder/label 矩阵导出 `databaseFieldMeta(provider)`。
7. `Plugins/DbAdmin/web/src/types.ts` + `dbAdminApi.ts`（或现有 service 文件）：`DbConnectionInput` 不变（database 本就可空）；新增 `listDatabases(id)`、`tables(id, database?)`、`describe(id, table, database?)`、`query` body + database。
8. `Plugins/DbAdmin/web/src/DbAdminView.vue`：库选择器（FR/Interaction 规格：仅 mysql/sqlserver/pg、五态、竞态防护复用既有请求序号模式）、切库重载表、查询携带所选库；oracle/sqlite/系统连接不渲染选择器。
9. 新增纯函数模块 `web/src/databaseSelector.ts`（选项排序/系统库置底/默认选中/切库状态机）+ `databaseSelector.test.ts`；`contract.test.ts` 补 database 字段契约样例。
10. CT：`ForgeSelf.Web/ct/` 下 DbAdmin 壳若已有 `DbAdmin.story.ts` → 加「库选择器」story + 行为断言；无壳则记 TODO 不新建（范围控制）。

## S5 测试与守卫

11. `ForgeSelf.Api.Tests/Plugins/DbAdminTests/DbConnectionServiceTests.cs`：必填矩阵、BuildConnStr 空库分支、派生注册名（含特殊字符/超长）、connstr 补键、`ListDatabases`（sqlite 实测 + 其余 SQL 断言）。
12. `DbQueryServiceTests.cs`：query 带 database（sqlite main）、oracle 拒、**审批重放保库**（AC4）。
13. `DbAdminToolFunctionTests.cs`：4 工具 database 参数用例 + connectionId 数字直传（输入4）+ `db_list_databases`。
14. `DbAdminContractTests.cs`：ApprovalView/QueryRequest 字段清单同步（预期红一次后更新）。
15. `ForgeSelf.Api.Tests/Unit/ToolRegistryTests.cs`：无 type 属性收 Number 放行（输入4 网关锁）。
16. `ForgeSelf.Web/e2e/plugins/db-admin/db-admin.spec.ts`：冒烟主链路补「多库连接→选择器→切库」段（用 sqlite 连接验证选择器隐藏 + 多库段用 mock route 断言选择器交互——e2e 零 mock 铁律说明：既有 spec :96 已用 page.route 造表数据属既有形态，沿用其模式仅限选择器交互段，主链路仍真实）。

## S6 文档与收尾

17. `docs/02-features/044-db-admin.md`：§2.1 连接字段必填矩阵 + 库枚举/切换 + 工具契约（含输入4 段落合并改写）。
18. `Plugins/DbAdmin/docs/03-架构与接口.md`（HTTP/工具契约）、`04-权限与审批.md`（工具表 + db_list_databases）、`ui-design-brief.md`（选择器交互）、`07-Agent工具评测.md` + `agent-tool-eval-cases.json`（加 db_list_databases 用例）。
19. 日记/TODO 收口（§7.5）。
