# Evidence — DbAdmin 连接级多库 + connectionId 数字/字符串双收

> 阶段：Stage 7｜Task ID：`2026-11-07-db-multi-database`｜实施：2026-11-07（并行会话；输入4/输入5 用户直接指令，工件 00-04 先行落盘）｜验证收口：2026-11-11（提交闸门前补验证）
> 分级：**Verified** = 亲自跑过拿到真实输出；Inferred = 代码/结构推断；Unknown = 未验证。

## Validation 结果（全部实际执行）

| 门禁 | 命令 | 结果 | 级别 |
| --- | --- | --- | --- |
| 后端过滤集（首跑） | `dotnet test --filter "FullyQualifiedName~DbAdmin\|~ToolRegistry\|…"` | **418 总 / 417 过 / 1 失败**——唯一红 = `DbAdminToolFunctionTests.Tools_SevenWithDbPrefix_IdsAndSchemas`（Expected 7 / Actual 8：实现新增 `db_list_databases` 后工具目录契约断言未跟上，属测试陈旧非行为缺陷，03-plan S5.14 预登记「预期红一次后更新」） | ✅ Verified（`.temp/tmp/filtered-tests-20261111.log`） |
| 契约修复 | `Tools_SevenWithDbPrefix` → `Tools_EightWithDbPrefix_IdsAndSchemas`（8 工具名单 + `db_list_databases`） | 复跑 **418/418 全绿** | ✅ Verified（`.temp/tmp/filtered-tests-20261111-r2.log`） |
| 机制级测试补齐（03-plan S5 计划项，2026-11-11 补写 7 例） | DbConnectionServiceTests ×2 + DbQueryServiceTests ×2 + DbAdminToolFunctionTests ×2 + ToolRegistryTests ×1 | 再跑 **425/425 全绿**（1.16 min） | ✅ Verified（`.temp/tmp/filtered-tests-20261111-r3.log`） |
| 插件前端单测 | `cd Plugins/DbAdmin/web && pnpm run test` | **104/104 通过（13 文件）**，含 databaseSelector/contract/connForm 断言 | ✅ Verified（`.temp/tmp/dbadmin-web-test.log`） |
| 插件前端构建 | `pnpm run build` | dist/index.js 102.95 kB，0 错误（插件无 check 脚本，门禁=test+build） | ✅ Verified（`.temp/tmp/dbadmin-web-build.log`） |
| 宿主前端 check | `pnpm run check` | 0 errors（166 warnings 既有）+ component-conventions PASS（story 6 / ct.spec 6 配对注册） | ✅ Verified（`.temp/tmp/web-check.log`） |
| 宿主前端单测 | `pnpm run test` | **112 文件 / 1203 用例全过** | ✅ Verified（`.temp/tmp/web-test.log`） |
| 设计门禁 | `pnpm run design:check` | style-tokens PASS（1761 vs 基线 2501，-740）+ compliance PASS（棘轮零增量 a=209 b=0 c=0 d=0） | ✅ Verified（`.temp/tmp/web-design.log`） |
| e2e 多库段 | `db-admin.spec.ts` 已按 S5.16 增改选择器交互段 | **用例已写、未执行**（本批收口未跑 e2e） | ⚠️ Unknown |
| CT 选择器 story | `DbAdmin.story.ts`/`DbAdmin.ct.spec.ts` 已增改 | **story 已写、CT 未执行** | ⚠️ Unknown |
| 起源码走查 / 运行实例复验 | dev-stack 走查 + :51888 只读复验 | **未做**（提交后按 plugin-development ⑥ 补） | ⚠️ Unknown |

## 验收标准覆盖对照（AC1–AC7 → 证据）

- **AC1**（server 型 Database 可空 201/200 回显 null；sqlite/oracle 不填仍拒）：单测 `Database_Optional_ServerTypes_SqliteStillRequired`（mysql 不填建连成功 + 实体 Database=null；sqlite 不填抛 DbAdminException）。oracle 不填的 400 = 既有必填分支保留（Inferred，代码路径未删）。
- **AC2**（databases 端点按 FR2 矩阵）：单测 `ListDatabases_SqliteMainOnly_SystemConnRejected`（sqlite 实测恒 [main]/kind=main/IsDefault + sys:* 拒 invalid_request）；mysql/sqlserver/pg = 枚举 SQL 生成断言（`DbConnectionService.cs:589-591`，Inferred）；**真机枚举 Unknown（spec Unknown 表已预登记：本机无实例）**。
- **AC3**（tables/describe/query/db_execute_write +database）：单测 `Database_Param_SqliteMainPasses_OtherRejected`（sqlite `main` 归一放行 + 非 main 拒 invalid_request + 审计 Detail 标 `db=main`）；server 型派生注册名/连接串改写 = `DeriveConnName`/`BuildConnStr(e, database)`/`RewriteConnStrDatabase` 代码路径（Inferred）；**真机切库 Unknown**。
- **AC4**（写审批同库重放）：单测 `AskWrite_ApprovalView_KeepsDatabase_OnPendingAndReplay`（`PendingWrite.Database` → 审批视图 `ApprovalView.Database` 透传 Verified + 批准后重放执行成功）。sqlite `main` 归一后与默认库等价，「同库」区分性断言（派生库名 ≠ 默认库的真机场景）Unknown。
- **AC5**（db_list_databases 可发现可调用）：工具目录单测 `Tools_EightWithDbPrefix_IdsAndSchemas`（8 工具名单、db_ 前缀、命名空间、schema type=object）+ 服务层 `ListDatabases_SqliteMainOnly`（sqlite 调用成功）。网关 search_tools/list_tools 发现性 = 既有插件工具注册机制未变（Inferred，走查复验待做）。
- **AC6**（connectionId 数字直传不再 INVALID_PARAMETERS——输入4 判据）：`ConnectionId_NumberDirect_QueryReadonly_And_ListTables_Ok`（插件层 Number→GetRawText 双收）+ `ToolRegistryTests.ValidateParameters_PropertyWithoutType_AcceptsNumber`（网关无 type 属性放行 Number/字符串，对照有 type 仍严校）+ `ConnectionId_BooleanStillRejected_AtPluginLayer`（兜底下沉插件层）。✅ Verified。
- **AC7**（既有全绿）：后端过滤集 425/425 + 插件 web 104/104 + 宿主 web 1203/1203 + check/design PASS ✅；**CT 与冒烟 e2e 未跑**（缺口显式披露，见 Unknown）。

## 机制级测试清单（2026-11-11 补写，对应 03-plan S5.11/S5.12 与 mini-task 改动清单）

1. `DbConnectionServiceTests.Database_Optional_ServerTypes_SqliteStillRequired` —— AC1 必填矩阵。
2. `DbConnectionServiceTests.ListDatabases_SqliteMainOnly_SystemConnRejected` —— AC2 sqlite 实测。
3. `DbQueryServiceTests.Database_Param_SqliteMainPasses_OtherRejected` —— AC3 database 透传 + 拒绝分支 + BR4 审计标注。
4. `DbQueryServiceTests.AskWrite_ApprovalView_KeepsDatabase_OnPendingAndReplay` —— AC4 审批保库透传。
5. `DbAdminToolFunctionTests.ConnectionId_NumberDirect_QueryReadonly_And_ListTables_Ok` —— AC6（mini-task 计划 2 例合并）。
6. `DbAdminToolFunctionTests.ConnectionId_BooleanStillRejected_AtPluginLayer` —— 兜底行为。
7. `ToolRegistryTests.ValidateParameters_PropertyWithoutType_AcceptsNumber` —— AC6 网关层锁（防未来回改）。

## Unknown / 未覆盖

- mysql/sqlserver/postgresql/oracle 真机的枚举/切库/派生连接串端到端：本机无实例，spec Unknown 表四项沿承（PG 维护库、connstr 非标准键、SHOW DATABASES 权限、枚举结果依赖账号权限）。
- 审批重放「同库」的真机区分场景（派生库名 ≠ 默认库）：机制链 Verified，区分行为 Unknown。
- e2e 多库段执行、CT story 执行、起源码走查、运行实例只读复验：未跑，记 TODO 提交后补。
