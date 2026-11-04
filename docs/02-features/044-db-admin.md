# 044 · 数据库管理（db-admin）—— 可审批的 SQL 工作台

> 状态：已实现（feat/db-admin），随 2026-11-05 收口并入 main｜插件 Id `db-admin`｜目录 `Plugins/DbAdmin`｜路由 `/db-admin`｜菜单「数据库管理」｜工具前缀 `db`
> 功能规格真源：`Plugins/DbAdmin/docs/01~07`（炳宏定稿）；契约真源：`Plugins/DbAdmin/docs/03` §3 + §3.1。本文件承载宿主侧功能登记与速查。

## 1. 定位与范围

「本地 + Agent-first 的轻量数据库管理台」：人在页面连库、看表、跑 SQL；Agent 经 `db_*` 工具在可审批、可审计前提下查库与改库。MVP：连接管理（SQLite 实测；MySQL/PostgreSQL/SQL Server 可配置但「未实测」、驱动缺失明确提示）、系统只读挂出（宿主库 + XCodeConfig.PluginDbs）、Schema 浏览、SQL 执行与分页、写审批（AskWrite 默认 + 硬拒清单兜底 + 破坏性二次确认）、全量审计脱敏、刷新恢复。

**非目标**：表设计器、ER 图、数据导入导出、驱动在线下载、执行 shell、Agent 自建连接、宿主库写入（任何模式）。

## 2. 契约速查（真源见插件 docs/03 §3.1）

- HTTP `/api/db-admin/*`，类级 `[Authorize("ApiKeyPolicy")]`；错误体 `{code,message}`；code ∈ rejected_hard(403)/readonly_connection(403)/confirmation_required(409)/driver_missing(424)/sql_error(400)/not_found(404)/unauthorized(401)/invalid_request(400)。
- `/query`：200 ok|rejected（业务拒绝看 body）、202 awaiting_approval、error 按 code；响应体恒 QueryOutcome（columns/columnTypes/rows/total/page/pageSize/truncated/affectedRows/elapsedMs/approvalId/destructive/code/message）。
- 测连 `POST /connections/test` 恒 200 `{ok,code,message,serverVersion,elapsedMs}`；审批轮询 `GET /approvals?status=pending`（前端 5s）；`POST /approvals/{id}/resolve` body `{decision: approve|reject, confirmDestructive?}`。
- 连接 id：用户=数字串、系统=`sys:<ConnName>`；provider id 小写；kind 对外只有 read/write（破坏性=destructive:true）；密码恒 `****`、connStr 恒掩码；时间 ISO 8601 本地。
- Agent 工具（toolPrefix db，扩展点 Id `db-admin.<name>`）：`db_list_connections` / `db_list_tables` / `db_describe_table` / `db_query_readonly`（严格 SELECT/WITH、≤1000 行/≤256KB、truncated）/ `db_execute_write`（AskWrite→awaiting_approval）/ `db_set_permission_mode`（仅会话级）/ `db_resolve_approval`（破坏性恒 confirmation_required；批准需用户原话）。统一回包 `{success,tool,status,data,hint?}`，出口两道脱敏。

## 3. 安全模型

- 连接分级：系统只读（宿主+PluginDbs，写硬拒任何模式）｜用户连接 `DbAdminU_<id>`（写按模式：AskWrite 默认 / Auto 免普通 DML 询问）。
- SqlGuard 硬拒：无 WHERE 的 UPDATE/DELETE、多语句、DROP DATABASE/SCHEMA、ATTACH/DETACH、xp_*、OPENROWSET、BULK INSERT、LOAD_FILE、COPY…PROGRAM、pg_*_file、lo_import/export、LOAD_EXTENSION、VACUUM INTO、SHUTDOWN、不受信语言函数；分类先剥注释/字符串，fail-closed。
- 破坏性（DROP TABLE/VIEW/INDEX、TRUNCATE、ALTER…DROP）：审批后 UI 二次确认（Agent 不可代批）。
- 审批 TTL 30 分钟，原始 SQL 仅内存；密码 AES-256-GCM（插件数据目录 dbadmin.key，v1: 密文）；审计/历史/工具回包/CT fixture 全量脱敏（`****`）。

## 4. UI 交互（五态 + 主链路）

布局：左「连接+schema 树（系统只读区恒显）」右「SQL 编辑器（Ctrl+Enter）+结果格分页」；浮层互斥：连接表单→审批→破坏性二次确认→删除确认→审计抽屉。五态：加载（版本徽标「加载中」+树骨架）/空（新建引导）/出错（红横幅+结果区错误）/无权限（401 横幅）/正常。刷新恢复：连接+当前连接+编辑器草稿（localStorage ≤64KB，不恢复密码/结果集/待审批）。逐功能「点什么出现什么」与验收清单：`Plugins/DbAdmin/docs/06`（S0–S9+S-R）。

## 5. 宿主登记（并入 main 即生效）

1. `XCodeConfig.PluginDbs["DbAdmin"]="db-admin"`（插件自有库，升级不丢数据）
2. `ForgeSelf.Api.csproj` / `ForgeSelf.Api.Tests.csproj` / 根级 `Plugins/DbAdminTests/DbAdminTests.csproj` / `ForgeSelf.slnx` 项目登记
3. `ToolNamespace.PrefixByPluginId["db-admin"]="db"`（与 plugin.json toolPrefix 一致）
4. `plugin.json frontend`：route=/db-admin、menu=数据库管理、entry=web/dist/index.js（e2e/menu-route-consistency.spec.ts ④ 回归清单已登记）

## 6. 已知边界

- 非 SQLite 引擎「未实测」且不随插件打包驱动（决策1）；缺驱动测连/执行返回 driver_missing(424)+NuGet 包名，用户放 DLL 后重启生效。
- 密钥文件（dbadmin.key）丢失则已存密码不可解（重新录入）；备份/迁移须库+密钥同带（决策2）。
- 拿到数据目录（DbAdmin.db+dbadmin.key）即可解密——防「单独拷走 .db」，不防本机同用户进程；后续可迁宿主 vault。

## 7. 验收证据

`docs/ai/pilot/2026-11-05-db-admin/`（00-07）：门禁/测试/插件冒烟 e2e（e2e/plugins/db-admin/）+ 截图（screenshots/e2e/db-admin/、screenshots/dev/db-admin/）+ 走查记录。
