# Specification

> 阶段：Stage 2｜Task ID：`2026-11-14-db-admin-list-databases`
> 接口均已存在，本规格不发明新端点。竞品行为来自公开文档归纳（2026-11-14 调查），未实机，不确定处标 Unknown。

## Functional Requirements

1. **谁要先列库**：仅用户连接，且 provider 为 `mysql`、`sqlserver`、`postgresql`。这三类点连接名展开后，第一层是数据库，第二层才是表和列。
2. **谁保持现状**：SQLite（文件即库）、系统连接 `sys:*`（后端拒绝列库）、Oracle（枚举的是 schema，`NormalizeTargetDatabase` 拒绝把 schema 当库切换）。这三类点连接仍直接 `GET .../tables`，不渲染库层。
3. **表单**：
   - mysql / sqlserver / postgresql：标签「默认数据库」，可留空。placeholder「可留空，打开后列出全部数据库」。postgresql 额外一句说明：「留空时先连维护库 postgres，再列出其它库」。
   - sqlite：标签仍是「文件路径」，空则「请填写 SQLite 文件路径」。
   - oracle：标签「服务名」，空则「请填写服务名」（替换现在笼统的「请填写数据库名」）。
   - 连接串模式规则不变。
4. **列库**：`GET /api/db-admin/connections/{id}/databases`。展示 `databases[].name`。`isSystem=true` 的项排在非系统库之后，带「系统」。与连接已保存的 `database` 同名（忽略 sqlserver 大小写）的项带「默认」。后端已排序，前端只把系统库挪到末尾。
5. **点库**：`GET /connections/{id}/tables?database=<name>`，再沿用现有表行、列展开。查询、表详情带同一个 `database`。标签身份包含库名：同一连接下不同库的同名表是两张标签。
6. **默认表**：仅当该连接保存了非空默认库、且本机有默认表时，列库完成后展开该默认库并打开默认表。没填默认库时，点连接只列出库，不猜表。
7. **刷新**：用户连接的 ↻ 对「先列库」的三类重拉库列表，并重拉已展开库的表。SQLite/Oracle 仍只重拉表。
8. **筛选框**：placeholder 改为「筛选库或表名」。对库名和表名做本地包含过滤。不清求。

## Input

- 已有连接对象（`provider`、`database` 可空、`id`）。
- `GET .../databases` 响应：`{ databases: [{ name, kind, isDefault, isSystem }], provider, defaultDatabase }`（模型 `DatabaseList` / `DatabaseInfo`，`DbAdminModels.cs`）。
- 表单字段与现在相同，只放宽三类 provider 的空库名。

## Output

- 左树多一层库节点。
- 保存/测试请求：空库名不带空字符串也可以；与后端 `GuardRequiredDatabase` 一致，空值能通过这三类。
- `POST /query` 与表详情在选中库后带 `database`。

## Business Rules

- 不隐藏系统库（`IsSystemDatabase` 只是提示）。
- 列表只含该账号有权看到的库。空列表在加载结束后显示「未枚举到数据库」+ 重试，不在加载中显示。
- 切库沿用后端派生连接名，前端不拼连接串。
- PostgreSQL 留空的维护库回落保持 `postgres`，不在前端再填一个库名。
- Oracle 不提供库层。用户继续在 SQL 里写 `schema.table`（后端错误文案已是这个意思）。

## Boundary Conditions

- 连接串模式的 server 连接：若保存时没有可解析的默认库，点开仍尝试列库（枚举不依赖表单里的 Database 字段，依赖已保存连接能否连上）。列库失败走错误行 + 重试。
- 库名含特殊字符：作为 query 参数与路径外的查询值，走现有 `encodeURIComponent`。
- 重复点已展开的连接：仍是收起，不额外请求（与表现有表树一致）。↻ 才强制重拉。
- 同一连接列库进行中再次 ↻：序号加一，旧响应丢弃。点不同库并行加载表时按 `连接+库名` 分序号。
- 筛选无命中：库层显示「无匹配库」，不显示「未枚举到数据库」。

## Error Handling

- 列库失败：该连接下显示错误原文（脱敏沿用 `redactText`）和「重试」，不展示「未枚举到数据库」。
- 某库的表加载失败：只挂在该库节点下，其它库保持。
- 表单校验失败仍只写 `cf-error`，不发请求。

## Compatibility

- 后端契约不改。旧的「连接上填了库名、点连接直接看该库的表」改为「先看到全部库，默认库带标记」。已填库名的连接不会丢库，只是多一层。
- SQLite 冒烟（建连→建表→查询）路径不经过库层，断言保持。
- 已打开的查询标签没有 `database` 字段时，执行仍不带 `database`（用连接默认库），避免旧 localStorage 快照执行失败。

## Non-functional Requirements

- 点连接后 100ms 内出「加载数据库…」，完成前不出空态。
- 新颜色只用现有 `--el-*`。新 class 使用 `dba-` 前缀。
- 不新增 npm/NuGet 依赖。

## Interaction Design（交互设计）

### 交互规格

| 交互点 | 触发 | 结果 | 状态模型 | 反馈 | 空态 | 边界 |
| --- | --- | --- | --- | --- | --- | --- |
| 默认数据库 | 新建/编辑，provider=mysql/sqlserver/postgresql | 可留空保存；测试连接同样放行 | 可选 | 校验失败才出红字 | — | sqlite/oracle 留空仍阻止 |
| 点用户连接 | provider 为上述三类 | 展开；「加载数据库…」；结束后列出全部库 | 会话内展开态，换连接不丢已展开库的缓存，↻ 才丢 | 系统库在末尾标「系统」；默认库标「默认」 | 加载结束后 0 条：「未枚举到数据库」+ 重试 | 加载中不出空态；重复 ↻ 丢弃旧响应 |
| 点库名 | 点击某一库 | 展开该库并「加载表…」，表来自 `tables?database=` | 当前查询库记在标签上 | 失败只在该库下 | 加载结束后 0 表：「（无表）」 | 其它库的表不串 |
| 点表 | 与现在相同 | 打开/聚焦标签，SQL 预填；标签带库名 | 同连接同库同表只聚焦 | — | — | 不同库同名表各一标签 |
| SQLite / 系统 / Oracle | 点连接 | 与现在相同：直接「加载表…」再出表 | 无库层 | — | 「（无表）」仍在加载结束后 | Oracle 不渲染 schema 列表 |
| 筛选 | 输入 | 库名或表名包含即留 | 本地 | — | 「无匹配库」 | 不发请求 |
| 五态 | 页面级 | 加载=树骨架；空=无自建连接引导；出错=红横幅或节点错误；无权限=401 横幅；正常=库/表树 | 沿用现有页级五态 | — | 库列表空态见上 | 401 时不发列库 |

### 视觉规格

- 库节点与表现有 `.node` 对齐，多一级缩进（与表现相对连接的缩进同一差值，再嵌一层）。不新造卡片。
- 「系统」「默认」用现有 `.tag` / `.tag-muted`，不新配色。
- 选中行继续 `.node.on` 的 `--el-color-primary-light-8`。本批不改该 token（PC-025 已修语义）。亮/暗走查时库节点选中行仍测对比度 ≥4.5:1。
- 加载行复用「加载表…」的 spinner，文案改为「加载数据库…」。
- 本批不改结果网格、不改弹窗宽度。

### 交互验收标准

- AC-I1 mysql 空库名可保存（校验通过，请求发出）。sqlite 空路径、oracle 空服务名被对应文案阻止。
- AC-I2 点 mysql 连接：先见「加载数据库…」，其后见库名；完成前「未枚举到数据库」数量为 0。
- AC-I3 点其中一库：请求的 tables URL 含 `database=` 该库名；表出现在该库下。
- AC-I4 点 sqlite 或系统连接：不出现库节点，仍直接出表。
- AC-I5 列库失败：见错误文案与重试；重试成功后出库列表。
- AC-I6 从某库打开表再执行：query body 含该 `database`。

### 走查符合性

实现后按 plugin-development 走起源码 dev-stack：建一条 sqlite（回归直接列表）+ 若环境有 mysql/sqlserver/pg 则建一条空库名连接并点开库列表。没有 server 型实例时，走查只覆盖 sqlite 与 CT 夹具，server 型真机标 Unknown，不假装走过。亮/暗各看一次库节点选中态。

## Acceptance Criteria

1. AC-I1 … AC-I6。
2. 插件 web `pnpm run test`、`pnpm run build` 通过。
3. 宿主 `DbAdmin.ct.spec.ts` 里「请填写数据库名」的用例改为 oracle 服务名文案；新增「mysql 空库名不报错」与「点连接先出库节点」的 CT。
4. `docs/02-features/044-db-admin.md` §4 与 `Plugins/DbAdmin/docs/ui-design-brief.md` 写成实现后的行为（现在 §4 只记了缺口）。
5. 不修改 `DbConnectionService` 的枚举 SQL 与必填矩阵。

## Failure Modes

- PC-025：新节点若自配底色会复发。本批禁止新色，选中沿用 `.node.on`。
- PC-026：禁止无前缀布局类名。
- 空态抢先：库列表与表现共用「loading 为真则不渲染空文案」。

## Unknown

| 不确定点 | 影响 | 处理 |
| --- | --- | --- |
| 本机是否有 mysql/sqlserver/postgresql 可连 | server 型真机列库 | 单测/CT 用夹具；无实例则 05 标 Unknown |
| DBeaver SQL Server 是否默认列出全部库 | 不影响 | 已有 SSMS 文档写明 Database Name 可选且对象资源管理器列全部库，采用该行为 |
| DataGrip / TablePlus / Beekeeper 的必填细节 | 不作为本批交互 | 已标 Unknown，不照搬 |
| PostgreSQL 的 schema 中间层 | 库下直接列表，不按 schema 分组 | 接口无 schema 分组；要分组另立批次 |
| 连接串模式空库能否列库 | 取决于连接串能否连上 | 失败走节点错误 + 重试 |

## 竞品（文档级，非实机）

采用：Navicat（Initial Database 不是强制过滤）、SSMS/Azure Data Studio（Database Name 可选，对象树先出 Databases）、pgAdmin（维护库只是入口，树列出全部库）、MySQL Workbench（Default Schema 可留空，Schemas 列出全部）。

不采用：DataGrip 的灰色未内省名单与 Single database mode；TablePlus 的工具栏切库；DBeaver 默认关闭的 Show all databases（用户要求打开就列出，等价于该选项打开，不再加开关）。

## 同批：044 §8.2（用户 2026-11-14「P1 一并做」）

纯前端，零新依赖。不改 `/query` 契约。

1. **标签快捷键**（浮层打开时不响应）：Ctrl+W 关闭当前查询标签并 `preventDefault`；Ctrl+Tab 下一标签；Ctrl+Shift+Tab 上一标签；Ctrl+T 新建查询（无当前连接则不新建，沿用现有空态原因）。输入框里的普通字符不拦截；这四组带 Ctrl 的组合即使焦点在编辑器也生效。
2. **列宽**：结果表头右缘可拖，最小 48px。双击该边缘按当前页单元格文本宽度自适应。宽度只留在本次页面状态。
3. **单元格复制**：单击单元格选中该格（与选中整行可同时存在）。焦点不在输入框时 Ctrl+C 复制选中格文本；未选格则不抢复制整行按钮的行为。单元格右键「复制单元格」。列头右键「复制列名」。NULL 复制文本为 `NULL`（与 `csvCell` 一致）。
4. **分页键盘**：焦点不在 input/textarea/select 时，PgDn 下一页、PgUp 上一页、Home 第一页、End 最后一页（总行数未知时 End 不发请求）。浮层打开时不响应。
5. **表节点右键**：复制表名；查看结构（展开列，等同点 `▸`）；只刷新该表（只重拉该表列详情，不重拉整库表清单）。
6. **筛选**：现有一个输入框同时匹配连接名、库名、表名。连接名不匹配且其下没有匹配的库/表时整条连接隐藏。placeholder「筛选连接、库或表」。
7. **标签重命名**：仅自由查询标签。双击标题变成输入框，Enter 保存，Esc 取消，空标题回退「新查询」。表标签标题仍是表名。自定义标题进现有标签快照（`restore` 能读回）。
8. **结果空态**：执行中结果区出骨架，不出「0 行」也不出「结果将显示在这里」。成功且 0 行时结果区写「0 行（X ms）」（无 elapsed 时写「0 行」）。状态栏沿用 `statsLabel`。
9. **导出全部页 CSV**：在现有「导出 CSV」（当前页）旁增加「导出全部」。按当前语句从第 1 页请求到最后一页，pageSize 用该结果已选的值，上限 50 页；总行数已知则到齐即停；中途失败保留已拿到的页并在横幅说明停在第几页。文件名页码段用 `all`。进行中按钮显示「导出中…」，重复点击不发第二轮。
10. **导出 JSON / INSERT（当前页）**：JSON 为对象数组，键是列名，NULL 为 `null`。INSERT 为每行一条 `INSERT INTO <表> (cols) VALUES (...);`，表名取标签上的表名；自由查询没有表名时按钮禁用，title 说明原因。字符串单引号加倍。不导出全部页的 JSON/INSERT。

### 交互规格（§8.2）

| 交互点 | 触发 | 结果 | 空态 / 边界 |
| --- | --- | --- | --- |
| 关标签 | Ctrl+W | 当前查询标签关闭，激活邻标签 | 无标签时不报错；浮层开着不响应 |
| 切标签 | Ctrl+Tab / Ctrl+Shift+Tab | 循环切换 | 只剩一张则留在原标签 |
| 新查询 | Ctrl+T | 等同点「+ 新查询」 | 没有连接时不打开 |
| 列宽 | 拖表头右缘 / 双击 | 列宽变化 / 适应当前页 | 不窄于 48px |
| 复制格 | 点单元格后 Ctrl+C，或右键 | 剪贴板为该格文本 | 未选格时 Ctrl+C 不写入 |
| 复制列名 | 列头右键 | 剪贴板为列名 | — |
| 翻页 | PgUp/PgDn/Home/End | 等同点分页按钮 | 编辑器、筛选框、浮层内不抢键 |
| 表右键 | 表节点右键 | 菜单三项 | 点菜单外关闭 |
| 重命名 | 双击自由标签标题 | 输入框，Enter 保存 | 空串回「新查询」 |
| 0 行 | 查询成功且无行 | 结果区「0 行（X ms）」 | 执行中只见骨架 |
| 导出全部 | 点「导出全部」 | 下载合并 CSV | 失败停在已成功页并提示 |
| 导出 JSON/INSERT | 点对应按钮 | 下载当前页 | 无表名时 INSERT 禁用 |

## 明确不在本批

§8.3（取消执行、错误定位到行、当前页排序、列类型 tooltip、脚本库、编辑器增强、并排结果、参数化查询）与决策 056（语法高亮、格式化、事务、EXPLAIN、内联编辑、图表、标签拖拽、SSH/SSL、整库 dump）。另有既有 TODO：AI 入口、`db_resolve_approval` 伪造确认、SQL 切分对等守卫、数据根迁移。都不是本批。PostgreSQL 的 schema 中间层也不做。
