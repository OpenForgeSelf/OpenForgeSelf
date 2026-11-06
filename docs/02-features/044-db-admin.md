# 044 · 数据库管理（db-admin）—— 可审批的 SQL 工作台

> 状态：已实现（feat/db-admin），随 2026-11-05 收口并入 main｜插件 Id `db-admin`｜目录 `Plugins/DbAdmin`｜路由 `/db-admin`｜菜单「数据库管理」｜工具前缀 `db`
> 功能规格真源：`Plugins/DbAdmin/docs/01~07`（炳宏定稿）；契约真源：`Plugins/DbAdmin/docs/03` §3 + §3.1。本文件承载宿主侧功能登记与速查。

## 1. 定位与范围

「本地 + Agent-first 的轻量数据库管理台」：人在页面连库、看表、跑 SQL；Agent 经 `db_*` 工具在可审批、可审计前提下查库与改库。MVP：连接管理（SQLite 实测；MySQL/PostgreSQL/SQL Server 可配置但「未实测」；驱动预检关闭，失败回显底层错误）、系统只读挂出（宿主库 + XCodeConfig.PluginDbs）、Schema 浏览（表名过滤/刷新）、**多标签查询工作区（点表开标签、同表聚焦不覆盖、连接默认表）**、SQL 执行（**选区执行 + 多语句逐条执行 → 结果分页签**）与分页、写审批（AskWrite 默认 + 硬拒清单兜底 + 破坏性二次确认）、全量审计脱敏、刷新恢复。

**非目标**：表设计器、ER 图、数据导入、批量/全库导出（结果格单页 CSV 导出已纳入）、主动驱动在线下载（2026-11-06 起交由 XCode 在连接期自行获取）、执行 shell、Agent 自建连接、宿主库写入（任何模式）。

## 2. 契约速查（真源见插件 docs/03 §3.1）

- HTTP `/api/db-admin/*`，类级 `[Authorize("ApiKeyPolicy")]`；错误体 `{code,message}`；code ∈ rejected_hard(403)/readonly_connection(403)/confirmation_required(409)/driver_missing(424)/sql_error(400)/not_found(404)/unauthorized(401)/invalid_request(400)。
- `/query`：200 ok|rejected（业务拒绝看 body）、202 awaiting_approval、error 按 code；响应体恒 QueryOutcome（columns/columnTypes/rows/total/page/pageSize/truncated/affectedRows/elapsedMs/approvalId/destructive/code/message）。
- 测连 `POST /connections/test` 恒 200 `{ok,code,message,serverVersion,elapsedMs}`；审批轮询 `GET /approvals?status=pending`（前端 5s）；`POST /approvals/{id}/resolve` body `{decision: approve|reject, confirmDestructive?}`。
- 连接 id：用户=数字串、系统=`sys:<ConnName>`；provider id 小写；kind 对外只有 read/write（破坏性=destructive:true）；密码恒 `****`、connStr 恒掩码；时间 ISO 8601 本地。
- Agent 工具（toolPrefix db，扩展点 Id `db-admin.<name>`）：`db_list_connections` / `db_list_tables` / `db_describe_table` / `db_query_readonly`（严格 SELECT/WITH、≤1000 行/≤256KB、truncated）/ `db_execute_write`（AskWrite→awaiting_approval）/ `db_set_permission_mode`（仅会话级）/ `db_resolve_approval`（破坏性恒 confirmation_required；批准需用户原话）。统一回包 `{success,tool,status,data,hint?}`，出口两道脱敏。

### 2.1 AI 驱动入口与调用方式（2026-11-06 补齐：agent 怎么用这个插件）

**三个入口，能力不对等**：

| 入口 | 位置 | 谁在用 | 现状 |
|------|------|--------|------|
| 内置 AIAgent 对话 | `/ai-agent` 页面 → composer 🔧 工具选择器 | 本机用户 | **`db_*` 不在默认挂载**：AIAgent 默认作用域只有「本插件 + memory-system + design-system」（`Plugins/AIAgent/Services/AIAgentService.cs:674`），必须手动 🔧 勾选 `db_*`（勾选后候选池放开到全注册表 `:694`），或写进 Agent 编辑框的 Tools 白名单 |
| MCP 网关（外部 agent） | MCP 中心「网关」tab 给地址/端口/令牌 | Claude / Codex 等外部 agent | 先 `search_tools`（找不到再 `list_tools`），再 `universal_tool {tool:"db_query_readonly", parameters:{…}}`（对外工具定义 `Plugins/McpCenter/Services/UniversalToolForwarder.cs:38`） |
| DbAdmin 页面「Agent 工具回显」tab | `/db-admin` 右栏 | 本机用户 | **只读审计旁听**（`GET /audit?kind=agent`），**不能发起** agent 调用；空态「本会话暂无 Agent db_* 调用」 |

**下参示例（`db_query_readonly`，schema 真源 `Plugins/DbAdmin/Services/DbAdminToolFunctions.cs:442`）**：

```json
{"connectionId":"1","sql":"SELECT id, name FROM users WHERE id = @id LIMIT 50","params":{"id":1},"maxRows":50}
```

`connectionId` 先用 `db_list_connections` 取；`params` 与 SQL 里的 `@name` 对应；`sessionId` 可选（决定会话级权限模式并用于审计回显）。

**写操作链路**：`db_execute_write` 返回 `status=awaiting_approval` + `approvalId` → 用户在 DbAdmin 页面「审批」批准（审批中心在插件页，不在 AI 对话里）；Agent 侧 `db_resolve_approval` 仅当用户本轮原话明确同意才可调用，后端只校验 quote 非空（工具自述局限，`DbAdminToolFunctions.cs:540`），破坏性语句 Agent 恒无法批准。

**已知缺口**（已记 `TODO.md`）：插件页面内没有「自然语言 → 查询」入口，必须切到 `/ai-agent` 并手动勾选工具；`db_*` 不在默认作用域 → 「装了插件但 AI 默认用不了」没有任何界面提示。

## 3. 安全模型

- 连接分级：系统只读（宿主+PluginDbs，写硬拒任何模式）｜用户连接 `DbAdminU_<id>`（写按模式：AskWrite 默认 / Auto 免普通 DML 询问）。
- SqlGuard 硬拒：无 WHERE 的 UPDATE/DELETE、多语句、DROP DATABASE/SCHEMA、ATTACH/DETACH、xp_*、OPENROWSET、BULK INSERT、LOAD_FILE、COPY…PROGRAM、pg_*_file、lo_import/export、LOAD_EXTENSION、VACUUM INTO、SHUTDOWN、不受信语言函数；分类先剥注释/字符串，fail-closed。
- 破坏性（DROP TABLE/VIEW/INDEX、TRUNCATE、ALTER…DROP）：审批后 UI 二次确认（Agent 不可代批）。
- 审批 TTL 30 分钟，原始 SQL 仅内存；密码 AES-256-GCM（插件数据目录 dbadmin.key，v1: 密文）；审计/历史/工具回包/CT fixture 全量脱敏（`****`）。

## 4. UI 交互（五态 + 主链路）

布局：左「连接+schema 树（系统只读区恒显，支持表名过滤与单连接刷新）」右「多标签查询工作区」；浮层互斥：连接表单→审批→破坏性二次确认→删除确认→审计抽屉→单元格详情。五态：加载（版本徽标「加载中」+树骨架）/空（新建引导、工作区空态）/出错（红横幅+结果区错误）/无权限（401 横幅）/正常。刷新恢复：连接+当前连接+**打开的标签与各自 SQL**+**默认表偏好**（localStorage ≤64KB，按预算先丢非活动长草稿，不恢复密码/结果集/待审批）。逐功能「点什么出现什么」与验收清单：`Plugins/DbAdmin/docs/06`（S0–S9+S-R）。

**多标签查询工作区（2026-11-06 增量，PILOT `2026-11-06-db-admin-query-tabs`）**：
- 点表名行 → 新建/聚焦该表标签（`SELECT * FROM <表>` 预填，同时展开该表列）；同连接同名表已有标签则只聚焦、不重置 SQL；`▸` 只展开列不开标签；上限 12 个（满则提示，不静默丢弃）。
- 关闭标签：右邻优先激活，无右邻取左邻，全部关闭显示工作区空态；关闭会丢弃该标签的草稿与结果集（表标签可随时重点重建）。
- 连接「默认表（可选，仅本机）」：填了则点连接直接打开该表标签；留空则点连接只展开并加载全部表（不再「一张表一个连接」）。默认表不进连接请求体（`toSubmitBody` 剔除 + 单测守卫），只落 localStorage。
- 执行：编辑器有选区 → 只执行选中（按钮变「执行选中」+「仅执行选中」提示）；多条语句按客户端镜像 SqlGuard 的切分语义（跳过字符串/引号标识符/行注释/块注释/`$tag$`，`CREATE TRIGGER … BEGIN…END` 块不切，未闭合引号报错）**逐条 HTTP 执行**，不改 `/query` 单语句契约；结果区按语句分「结果 1/2/3」标签条（单语句不渲染），每条带状态点与失败即停（后续标「未执行（第 N 条失败）」）；分页只重发该条语句。
- 结果工具：状态栏「N 行 · X ms」/「影响 N 行」；点行选中后可「复制整行」；「导出 CSV」只导当前结果当前页（RFC 4180 + BOM，文件名 `<表或连接>-r<结果>-p<页>-<yyMMddHHmm>.csv`）；双击单元格看详情浮层（`NULL` 与长文本区分，Esc 关闭）。
- 审批按标签作用域回填：关闭标签后迟到的响应按 `(tabKey, runSeq)` 丢弃，不会写到别的标签或已关闭标签。

## 5. 宿主登记（并入 main 即生效）

1. `XCodeConfig.PluginDbs["DbAdmin"]="db-admin"`（插件自有库，升级不丢数据）
2. `ForgeSelf.Api.csproj` / `ForgeSelf.Api.Tests.csproj` / 根级 `Plugins/DbAdminTests/DbAdminTests.csproj` / `ForgeSelf.slnx` 项目登记
3. `ToolNamespace.PrefixByPluginId["db-admin"]="db"`（与 plugin.json toolPrefix 一致）
4. `plugin.json frontend`：route=/db-admin、menu=数据库管理、entry=web/dist/index.js（e2e/menu-route-consistency.spec.ts ④ 回归清单已登记）

## 6. 已知边界

- 非 SQLite 引擎「未实测」且不随插件打包驱动（决策1）；2026-11-06 起暂时关闭驱动预检（决策3）：`/providers` 全部 `driverAvailable=true`，连接/测连交给 XCode，失败回显底层错误（`sql_error` 400，脱敏）；`driver_missing`(424) 仅在预检开关开启时产生。
- 密钥文件（dbadmin.key）丢失则已存密码不可解（重新录入）；备份/迁移须库+密钥同带（决策2）。
- 拿到数据目录（DbAdmin.db+dbadmin.key）即可解密——防「单独拷走 .db」，不防本机同用户进程；后续可迁宿主 vault。

## 7. 验收证据

- 基线（2026-11-05）：`docs/ai/pilot/2026-11-05-db-admin/`（00-07）：门禁/测试/插件冒烟 e2e（`e2e/plugins/db-admin/`）+ 截图（`screenshots/e2e/db-admin/`、`screenshots/dev/db-admin/`）+ 走查记录。
- 本轮增量（多标签/默认表/选区与多语句/结果工具，2026-11-06）：`docs/ai/pilot/2026-11-06-db-admin-query-tabs/`（00-07）。门禁：插件 `pnpm run build` + `pnpm run test`（103 用例）、宿主 `pnpm run check` / `pnpm run test` / `design:check`、Playwright CT（`DbAdmin.ct.spec.ts` 32 用例含新增多标签/多语句/结果工具）、插件层冒烟 e2e（单用例内扩展到建连→建表→多标签→默认表→多语句/选区→结果工具→INSERT 拒绝）。

## 8. 竞品对标与待补功能（Backlog）

> 调研对象：DBeaver（SQL Editor 与结果集能力）、Navicat（查询与结果功能）、TablePlus（官网功能列表）、HeidiSQL（功能清单）、DataGrip（通用能力）。
> 证据等级：**文档级调研（Inferred）**，非逐版本实机对比；竞品结论用于「该不该有」，不作为验收证据。
> 逐项理由原始表：`docs/ai/pilot/2026-11-06-db-admin-query-tabs/02-spec.md`「竞品对标与能力补齐」。

### 8.1 本轮已补齐（2026-11-06，11 项，Verified）

| 能力 | 竞品依据 | 落地 |
|------|----------|------|
| 多查询标签（不同表独立 / 同表聚焦） | DBeaver、Navicat、TablePlus | `tabs.ts` + `DbAdminView.vue` |
| 每连接可选默认表 | Navicat 双击表开查询、TablePlus 快速跳转 | `connForm.ts` + `defaultTables` |
| 选区执行 | DBeaver「执行选中」、HeidiSQL、DataGrip | `runFromEditor` + `sqlSplit.ts` |
| 多语句执行 + 结果分页签 | DBeaver 多结果集 | `results.ts` + 结果标签条 |
| 会话恢复（标签 / 草稿 / 默认表） | DBeaver 恢复编辑器与脚本 | `restore.ts`（旧快照可迁移） |
| 表名过滤 + 刷新表 | DBeaver/Navicat 对象过滤器、HeidiSQL | `tableFilter` + `loadTables(force)` |
| 复制整行 | 全部竞品通用 | `rowText` + `copySelectedRow` |
| 导出 CSV（当前页，RFC 4180 + BOM） | HeidiSQL/Navicat/DBeaver | `exportCsv.ts` |
| 单元格长值详情（可复制） | TablePlus/DBeaver 单元格查看器 | `cell-detail` 浮层 |
| 结果状态栏（N 行 · X ms / 影响 N 行） | DBeaver/Navicat 状态栏 | `statsLabel` + `result-range` |
| 快捷键 Ctrl+Enter / F5 | DBeaver、HeidiSQL | `onEditorKey` / 窗口级 `onWindowKey` |

### 8.2 待补 · P1（纯前端、零新依赖，可作为一个独立小批）

| 能力 | 竞品依据 | 现状缺口（代码依据） | 成本 |
|------|----------|----------------------|------|
| 标签快捷键：Ctrl+W 关闭当前、Ctrl+Tab / Ctrl+Shift+Tab 切换、Ctrl+T 新建 | DBeaver、DataGrip | `onWindowKey` 只管 Ctrl+Enter/F5（`DbAdminView.vue` 692 行） | S |
| 结果列宽拖拽 + 双击列头自适应 | HeidiSQL、Navicat 表格列宽可调 | 结果格无任何 resizer 逻辑，长值只能靠横向滚动 | S |
| 单元格级复制：点选单元格 Ctrl+C / 右键「复制单元格」；列头右键「复制列名」 | 全部竞品通用 | 只有「复制整行」+ 详情浮层「复制」 | S |
| 结果分页键盘操作：PgUp/PgDn/Home/End | DBeaver、HeidiSQL | 只有 `‹ ›` 按钮 | S |
| 表树右键菜单：复制表名 / 查看结构 / 只刷新该表 | HeidiSQL、TablePlus | 单击只开标签、`▸` 只展开；无右键菜单 | S |
| 连接树按名称过滤（多连接场景） | DBeaver、Navicat | 只有表名过滤（`tableFilter`），连接区无搜索 | S |
| 查询标签重命名 | DBeaver、DataGrip | `tabTitle` 手写标签恒为「新查询」 | S |
| 结果空态明确化：「0 行（X ms）」+ 加载骨架 | DBeaver/Navicat 状态栏 | 空结果与加载态区分仅靠状态点 | S |
| 导出全部页（客户端循环分页拼一份 CSV） | HeidiSQL/Navicat「导出全部」 | 现仅「当前页」 | M |
| 导出 JSON / SQL INSERT 语句 | DBeaver「Generate SQL」、Navicat | 只有 CSV | M |

### 8.3 待补 · P2（需小范围契约扩展或轻量实现）

| 能力 | 竞品依据 | 依赖 / 风险 | 成本 |
|------|----------|-------------|------|
| 取消执行（长查询中断） | DBeaver、DataGrip | 后端需 cancel 端点（现 `POST /query` 单请求无句柄） | L |
| 错误定位到编辑器行号 | DBeaver error indication | 需 `/query` 回传语句偏移 | M |
| 结果列排序/过滤（仅当前页，明确标注） | TablePlus 筛选、DBeaver | 纯前端可做，但全页排序需服务端改 SQL → 先限当前页并显式提示 | M |
| 结果列类型 / 主键标识（列头 tooltip） | HeidiSQL、DBeaver | 依赖表详情接口的列元数据覆盖度 | M |
| 查询收藏 / 命名脚本库（本机） | DBeaver 脚本管理、Navicat 收藏 | 纯 localStorage，需与快照预算共用配额 | M |
| 编辑器增强：括号配对、Tab 缩进、当前行高亮 | DBeaver、HeidiSQL | 纯 `<textarea>` 可实现，但复杂度上升；超过阈值应上 CodeMirror | M |
| 多结果并排对比视图 | DBeaver「并排结果」 | 布局改造，不改契约 | M |
| 参数化查询（`:param` 绑定） | DBeaver 参数绑定、DataGrip | 前端替换有注入风险，须复用 SqlGuard 校验 + 脱敏审计 | M |

### 8.4 明确不做（不在 backlog，见 ADR）

语法高亮/自动补全、SQL 格式化、事务模式、服务端排序改写、EXPLAIN、结果内联编辑、结果图表、标签拖拽排序、内部 SSH/SSL/用户管理、整库 dump 导入导出 —— 决策与重审条件见 `docs/07-decisions/not-taken-decisions.md` **056**。
