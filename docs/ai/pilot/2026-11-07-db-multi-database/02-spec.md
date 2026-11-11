# Specification — DbAdmin 连接级多库

> Task ID：PILOT-db-multi-database ｜ 依据：00-repository-understanding + 01-intent。不确定点在文末 Unknown 表，不猜测。

## Functional Requirements

- **FR1 数据库非必填（按 provider 分档）**：
  | provider | Database 字段语义 | 是否必填 | 说明 |
  | --- | --- | --- | --- |
  | sqlite | 文件路径 | **必填**（现状不变） | 文件即库 |
  | mysql | 默认库 | 可空 | 连接串省略 `Database=` |
  | sqlserver | 默认库 | 可空 | 连接串省略 `Initial Catalog=`（驱动落到登录默认库，通常 master） |
  | postgresql | 默认库 | 可空 | 留空时后端默认填 `postgres`（PG 协议必须指定库；维护库恒存在，Unknown 假设见文末） |
  | oracle | 服务名 | **必填**（现状不变） | EZConnect `//host:port/service` 结构必需；「库」概念对应 schema，不在此字段 |
  - 生效点：后端 `ApplyForm`（:201 分支化）与 connstr→form 改模式路径（:446）；前端 `validateForm`（:54/:59 分支化）。
- **FR2 枚举库**：新能力 `ListDatabases(id)` 按 provider 返回 `{databases:[{name, kind, isDefault, isSystem}]}`：
  | provider | 枚举方式 | kind | isDefault 判定 |
  | --- | --- | --- | --- |
  | mysql | `SHOW DATABASES` | database | 连接串 Database（可空） |
  | sqlserver | `SELECT name FROM sys.databases WHERE state=0 AND HAS_DBACCESS(name)=1 ORDER BY name` | database | 同上 |
  | postgresql | `SELECT datname FROM pg_database WHERE datallowconn AND NOT datistemplate ORDER BY datname` | database | 同上（空=null） |
  | oracle | `SELECT username FROM all_users ORDER BY username` | **schema** | null |
  | sqlite | 不发 SQL，返回 `[main]` | main | true |
  - `isSystem`：mysql 的 information_schema/mysql/performance_schema/sys 标 true（不隐藏，UI 排序置底）。
- **FR3 按库执行（连接名派生）**：`Resolve(id, database?)`——database 为空 = 现行为（`DbAdminU_{id}`）；非空且 ≠ 默认库 → 派生注册名 `DbAdminU_{id}__D{规范化}`（规范化：仅保留 `[A-Za-z0-9_]`，其余替换 `_`，超 40 字符或含替换时追加 8 位稳定 hash），连接串按 FR1 语义重写（form：改 `Database=`/`Initial Catalog=`；connstr：`DbConnectionStringBuilder` 改写同键，无该键则补），复用 `Register` 锁内懒注册（:318-326 同模式）。系统连接/带 database → 报 `invalid_request`「系统连接不支持指定数据库」。
- **FR4 既有面追加可选 `database`**：
  - HTTP：`GET connections/{id}/tables?database=`、`GET connections/{id}/tables/{table}?database=`、`POST query` body 加 `Database`（Models.QueryRequest）；审批 resolve 链路经 PendingWrite 补 `Database` 字段重放（见 BR3）。
  - MCP 工具：`db_list_tables` / `db_describe_table` / `db_query_readonly` / `db_execute_write` 各加可选 `database` 参数；新增工具 `db_list_databases`（入参仅 connectionId）。
  - `database` 在 sqlite/oracle 上：oracle 报 `invalid_request`「Oracle 不支持切换数据库（枚举的是 schema），查询请显式 schema.table」；sqlite 非 main 值报 `invalid_request`，`main` 视同不传。
- **FR5 connectionId 类型契约（并入输入4 mini-task）**：4 处 schema 删 `"type":"string"` + description 改「用户连接 id 传数字或数字串均可；系统连接传 `sys:<连接名>`」；网关无 type 即放行（ToolRegistry.cs:227-230），插件 `OptString` 双收。
- **FR6 新工具 schema**：`db_list_databases` 的 connectionId 同 FR5（无 type）；description 说明「返回该连接可见的数据库/schema 列表，oracle 返回 schema」。

## Input

- HTTP 请求体/查询串（database 字符串，trim 后空 = 未传）；MCP 工具 JSON args（同 044 §2.1 封套）。

## Output

- `GET connections/{id}/databases` → `200 {databases:[…], provider, defaultDatabase}`（无显式默认库时 defaultDatabase=null）。
- tables/describe/query 的既有响应形状**不变**（多库只影响执行上下文）。
- `db_list_databases` 工具回包沿用插件封套 `{success, tool, status, data, hint?}`。

## Business Rules

- BR1 权限/审批/SqlGuard/上限逻辑**逐字不变**——多库只改 `Resolve` 出的 ConnName，写审批、只读拒、脱敏、ResultLimiter 全部沿用。
- BR2 `isDefault` 与连接的默认库一致性：`database` 参数值大小写敏感性交由各数据库引擎判定（不在此归一）。
- BR3 **审批重放保库**：`PendingWrite` 增加 `Database` 字段（:26 record），`Enqueue` 传入、`Resolve`（审批处理）重放时原样传回 `ExecuteCore`；`ApprovalView` 增加可空 `Database` 供 UI 展示。
- BR4 审计：query/write 的审计 `Detail` 追加 `db=<库名>`（未指定时省略），历史表结构不动。

## Boundary Conditions

- 库名不存在/无权限：交由引擎报错，经 `Wrap`（:477-490）映射 `sql_error`，原文脱敏回显。
- connstr 模式连接串里没有 Database/Initial Catalog 键（如 sqlite 文件串被误当 server 串）：按 provider 补键；改写失败（不可解析）→ `invalid_request` 带原因。
- 派生注册名上限：同连接枚举过的库数量无硬限（每个库一条注册，进程内字典承载；宿主重启自然清空）。
- 默认库为空的连接：`db_list_tables` 等未传 database 时 mysql/sqlserver 照常执行（服务器默认上下文）；postgresql 用 `postgres`；未传 database 的 `db_execute_write` 在「无默认库」连接上允许（与现状语义一致——连接串就是执行上下文）。

## Error Handling

- 复用 `DbErrorCodes`：参数问题 `invalid_request`（400）、连接不存在 `not_found`（404）、引擎错误 `sql_error`（400）、驱动缺失 `driver_missing`（424）。无新错误码。
- 工具层错误沿用插件封套 `{success:false, tool, status:"error", hint}` 形态（044 §2.1）。

## Compatibility

- 不传 `database` = 现行为，既有 HTTP 调用方、评测集（`agent-tool-eval-cases.json`）、既有单测零改动即绿（新增字段均可选）。
- `QueryRequest`/`ConnectionView` 只增不改；`ApprovalView` 增可空字段（契约测试字段清单同步——DbAdminContractTests 的字段集断言会红一次，属预期提醒）。
- 输入4 的 connectionId 双收：字符串传法兼容（`db_list_connections` 出参 Id 本就是字符串，`DbConnectionService.cs:94/:82`）。

## Non-functional Requirements

- 枚举库结果不缓存（每次实查，库列表可能变化）；工具侧沿用 ResultLimiter 不适用（列表天然小，≤1000 兜底截断同现有风格）。
- 派生注册并发安全：复用 `_regLock`（:320），不改锁粒度。
- 枚举 SQL 全部为只读系统视图/语句，SqlGuard 无需放行（`SHOW DATABASES` 属 read 类，现分类已覆盖；pg_database/sys.databases 为 SELECT）。

## Interaction Design（交互设计 · 必写节）

> 改动面 = 连接表单（Database 必填性）+ 连接/表面板（库选择器）。工具/HTTP 交互见 FR4-FR6。

### 交互规格（点什么出现什么）

| 交互点 | 触发 | 结果 | 状态模型 | 反馈 | 空态 | 边界 |
| --- | --- | --- | --- | --- | --- | --- |
| 表单 Database 字段 | 切 provider | label 改「默认数据库」；sqlite=「数据库文件路径」、oracle=「服务名」placeholder；mysql/sqlserver/pg placeholder「可留空」 | per-provider 必填矩阵（FR1） | 校验失败红字（沿用现有 form error 行） | — | sqlite/oracle 留空提交 → 阻止 + 现有文案 |
| 库选择器（表列表头部） | 点击连接 | 仅 mysql/sqlserver/postgresql 显示；加载库列表（局部 loading「加载数据库…」≤100ms 出现）→ 下拉，默认选中连接默认库（空=null→选第一项并标记「默认」），isSystem 项置底加「系统」标签 | 所选库 = 会话级 UI 状态，切连接重置 | 切库 → 表列表重载（既有 loading + 竞态防护沿用） | 库列表为空（罕见）→「未枚举到数据库」+ 重试 | 加载失败 → 错误原文 + 重试按钮；oracle/sqlite/系统连接不显示选择器 |
| 查询面板 | 执行查询 | 请求自动携带当前选中库（无选择器的连接不带 database） | 跟随库选择器 | — | — | 切库后旧查询结果不清空（用户自见 SQL），但新查询必带新库 |
| Oracle 连接 | 点击 | 不显示库选择器；连接详情的服务名照旧展示 | — | — | — | 用户可自行 schema.table 查询（文档提示） |

### 视觉规格（目标态）

- 选择器复用 Element Plus `ElSelect`（小尺寸），置于表列表工具行左端，宽 ≤220px；token 全用 `--el-*` 变量，不新增裸色值（design:check 棘轮）；不破坏既有「加载表…」spinner 与选中行对比度守卫（PC-025 系列）。
- 主题矩阵：选择器表面 = `--el-fill-color-blank`（bg）/`--el-text-color-regular`（fg）/`--el-border-color`（border），light/dark 两态均走组件默认 token（本批不自定义色，逐格引用 token；暗色走查显式切 `html.dark`）。

### 交互验收标准（并入 AC）

- AC-I1 选 mysql 连接（多库）→ 选择器出现且首选项=默认库，库列表与引擎枚举一致。
- AC-I2 切库 → 表列表重载且 loading 先于内容；连点不同库不串响应（竞态防护沿用断言）。
- AC-I3 选择器加载失败 → 错误文案 + 重试可用；重试成功恢复下拉。
- AC-I4 sqlite/oracle/系统连接 → 无选择器（不渲染，而非禁用灰置）。
- AC-I5 表单：pg 连接 Database 留空可保存；sqlite/oracle 留空被文案阻止。

### 走查符合性（DoD 绑定）

- 按 ui-ux-design 走查清单全项核对 + 截图读图（dev-stack 起源码实例，`screenshots/dev/db-admin/`）；对比度沿用既有选中行守卫，本批新增 UI 无自定义色。

## Acceptance Criteria

1. AC1 创建 mysql/sqlserver/postgresql 连接不填 Database → 201/200，`GET connections` 回显 `database=null`；sqlite/oracle 不填 → 400 文案不变。（后端单测 + SQLite 实测 HTTP 面）
2. AC2 `databases` 端点按 FR2 矩阵返回；sqlite 实测、其余 provider 为 SQL 生成与分发断言（真机 Unknown，见下表）。
3. AC3 `tables?database=` / `describe?database=` / `query{database}` / `db_execute_write{database}` 在指定库执行：SQLite 双库文件端到端实测（建两个 sqlite 连接场景不适用——单连接多库仅 server 型；SQLite 侧验证 `main` 语义与错误分支）。server 型 = 派生连接串断言 + （走查）用户实例 Oracle schema 枚举实测。
4. AC4 带 database 的写操作 → awaiting_approval → approve → **同库**重放（单测：sqlite `main` + 一个派生库名场景断言 PendingWrite.Database 透传）。
5. AC5 `db_list_databases` 经网关 search_tools/list_tools 可发现、调用成功（sqlite）。
6. AC6 connectionId 数字直传经网关不再 `INVALID_PARAMETERS`（输入4 判据，网关层测试 + 插件测试双收）。
7. AC7 既有全绿：`dotnet test --filter DbAdmin` + ToolRegistry 过滤集 + `pnpm run check/test`（插件 web）+ CT（如触选择器新 story）+ 冒烟 e2e 更新通过。
8. AC-I1…I5 走查逐条留痕。

## Unknown

| 不确定点 | 影响 | 处理方式 |
| --- | --- | --- |
| 本机无 mysql/sqlserver/postgresql 实例 | 三类 provider 的真实枚举/切库无法在本机端到端验证 | 保守假设：连接串/SQL 按 FR2 生成并单测断言；真机行为标 Unknown，交付说明中明确「mysql/sqlserver/pg 多库行为待你实例实测」，Oracle 用你的运行实例连接 5 实测 schema 枚举 |
| PG 维护库 `postgres` 是否恒可连 | 空库连接的初始上下文 | 保守假设成立（PG 标准安装）；若用户实例特殊，错误会以 sql_error 明文回显，可在该连接填入具体库名规避 |
| connstr 模式连接串使用非标准库名键（如 `Database Name=`） | 改写补键可能遗漏 | v1 仅支持 `Database`/`Initial Catalog` 标准键，文档注明；其余场景建议用户改 form 模式 |
| mysql 空默认库下 `SHOW DATABASES` 是否需 USE 权限 | 枚举可能受账号权限限制 | 交由引擎报错脱敏回显（Boundary）；文档注明「枚举结果取决于账号权限」 |
