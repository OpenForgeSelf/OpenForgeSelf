# Repository Understanding — DbAdmin 连接级多库（2026-11-07）

> Task ID：PILOT-db-multi-database ｜ 全部结论带 `文件:行`，均为今日实读（Verified），未实测处标 Unknown。

## 1. 插件现状（与本次改动相关的真实形状）

- 实体真源 `Plugins/DbAdmin/Data/Model.xml`：`DbConnection.Provider` 注释枚举 `sqlite/mysql/postgresql/sqlserver`（:48，另有 oracle 已于 2026-11-06 加入，见 `connForm.ts:5-10`、`DbConnectionService.cs:275-279`）；**`Database` 列本就可空**（:52，String 无 Nullable=False），语义="数据库名或 SQLite 文件路径"；oracle 下实为**服务名**（EZConnect `//host:port/service`，:277）。
- 「数据库必填」的实际落点共 4 处（后端 2 + 前端 2）：
  - `DbConnectionService.cs:201`（form 模式校验：sqlite 要文件路径、其余要库名）；`:446`（connstr→form 改模式路径同样校验）；
  - `connForm.ts:54`（sqlite 必填）、`:59`（其余必填「请填写数据库名」）。
- 连接串组装 `DbConnectionService.BuildConnStr`（:252-284）：mode=connstr 原文解密返回（:254）；form 模式按 provider 拼——mysql/postgresql 用 `Database=`、sqlserver 用 `Initial Catalog=`、oracle 用服务名、sqlite 用文件路径（:259-279）。
- 连接解析与注册：`Resolve(id)` :290-308 → 用户连接 `ConnName = DbAdminU_{Id}`（:305），`Register(connName, connStr, def)` 锁内 `DAL.AddConnStr`（:318-326，连接串变更才重登记）；系统连接 `sys:<宿主连接名>` 单库，只读。
- 执行链：`DbQueryService.ExecuteCore` :58-87（SqlGuard → Resolve → 只读拒 → 审批/权限）→ `RunRead` :89+ 用 `DAL.Create(rc.ConnName)` 查询（:97）；`RunWrite` 同。**库上下文完全由注册的连接串决定**。
- Schema 浏览：`ListTables`/`DescribeTable` :457-474 = `DAL.Create(rc.ConnName).Db.CreateMetaData().GetTables()`——同受连接串里的库限定。
- HTTP 面 `Controllers/DbAdminController.cs`（前缀 `api/db-admin`，类级 ApiKeyPolicy :14）：connections CRUD/test（:43-64）、`connections/{id}/tables`（:66）、`tables/{table}`（:69）、`POST query`（:76-87，body=`QueryRequest{ConnectionId,Sql,Params,Page,PageSize}`，Models:114-124）、approvals/audit/permission-mode。
- Agent 工具 `DbAdminToolFunctions.cs`：7 个 db_* 工具（:576-588）；connectionId 在 4 个 schema 声明 `"type":"string"`（:390/:415/:446/:483）；db_resolve_approval 的 approvalId 已是 `"type":"integer"`（:546）。
- 前端：`web/src/DbAdminView.vue`（连接树 + 表列表 + 查询面板）、`connForm.ts`（表单纯函数：validateForm/buildConnStr/toSubmitBody，已有单测 `connForm.test.ts`）、`contract.test.ts`、类型 `types.ts`。连接点击→加载表已有局部 loading + 竞态防护（2026-11-06 批落地）。
- e2e 冒烟：`ForgeSelf.Web/e2e/plugins/db-admin/db-admin.spec.ts`（单条主链路，含建 sqlite 连接→列表→对比度双态断言）。
- 后端测试：`ForgeSelf.Api.Tests/Plugins/DbAdminTests/`（10 文件，含 `DbConnectionServiceTests` / `DbAdminToolFunctionTests` / `DbAdminContractTests` / 评测回放）。
- 审批：**仅内存**（`DbQueryService._pending` ConcurrentDictionary :24，PendingWrite 记录 ConnectionId/Sql/Params/… :26；TTL 30min :19）——不落库 ⇒ 加字段不需动 Model.xml。

## 2. 对本次需求的直接影响

- 实体层**零改动**（Database 已可空，无 xcode 需要）。
- 「一连接多库」的机制 = **同一宿主连接按库派生多个 DAL 注册名**（如 `DbAdminU_{id}__D<规范化库名>`，按库懒注册），执行/表/描述全部经 `Resolve` 换名即可，查询 SQL 本身无需改写。
- 枚举库的 SQL 按 provider 差异（mysql `SHOW DATABASES`；sqlserver `sys.databases`；postgresql `pg_database`；oracle `all_users`=schema；sqlite 单文件无枚举）。
- **已知隐患（本次必须一并处理）**：`PendingWrite` 未记录 database——若写操作带库，审批通过后重放会落回默认库（数据写错位置的静默缺陷）。
- 2026-11-07 输入4 的 connectionId 类型契约（去掉 `"type":"string"`、数字/字符串双收，mini-task `2026-11-07-db-connectionid-type/`）与本批改同一批 schema，**建议同批合并**，闸门1 一次批。

## 3. 测试与环境约束

- 本机可真实验证：SQLite（全流程）；Oracle（用户运行实例已有连接 5，枚举 schema 可实测）。
- Unknown：本机无 mysql/sqlserver/postgresql 服务实例——这三类 provider 的真实枚举/切库只能做到「连接串与枚举 SQL 生成断言 + 单测」，真机行为留给用户实例走查（Spec Unknown 表登记）。
