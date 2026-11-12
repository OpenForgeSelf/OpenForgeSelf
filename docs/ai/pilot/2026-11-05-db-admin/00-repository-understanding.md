# Repository Understanding — db-admin 并入 main（收口与验收）

> Task ID：`2026-11-05-db-admin`｜执行者：db-dev｜分支：`feat/db-admin`（worktree `D:\src\my-proj\OpenForgeSelf\wt-db`）
> 本任务是「分支收口」型：插件代码已由先前会话写完（10 个自有提交），本批负责工件链 + rebase 到新 main + 全量验证 + e2e + 走查 + 证据。以下事实全部实读于 wt-db@ d9fca5a。

## 1 相关架构事实（实读代码）

| 事实 | 依据（文件:行） |
|------|----------------|
| 插件身份与前端注册 | `Plugins/DbAdmin/plugin.json`：Id=`db-admin`、toolPrefix=`db`、Version=0.1.0、frontend={views:[DbAdminView], menu:"数据库管理", route:"/db-admin", icon:"fa-database", entry:"web/dist/index.js"} |
| 控制器与鉴权 | `Plugins/DbAdmin/Controllers/DbAdminController.cs:13-16`：`[ApiController][Authorize("ApiKeyPolicy")][Route("api/db-admin")]`；只注入本插件自有服务（铁律12c）；`StatusOf()`（:19-29）错误码→HTTP：not_found=404、rejected_hard/readonly_connection=403、confirmation_required=409、driver_missing=424、unauthorized=401、sql_error=400 |
| 端点全集 | 同上 :43-115：GET providers / connections(+{id}) / connections/{id}/tables(+/{table}) / approvals?status= / history / audit / permission-mode；POST connections、connections/test、query、approvals/{id}/resolve；PUT connections/{id}、permission-mode；DELETE connections/{id}（仅删配置） |
| /query 状态码契约 | 同上 :76-87：ok/rejected→200（业务拒绝看 body.code）；awaiting_approval→202；error 按 code 映射；响应体恒为 QueryOutcome |
| 插件入口与生命周期 | `Plugins/DbAdmin/DbAdminPlugin.cs:18-51`：Apply 内 `DbAdminTables.EnsureCreated()`（失败只 Warn 降级）→ ctx.EnsurePluginDataDirectory()（失败退程序目录子目录）→ 单例注册 ISecretProtector/DbAuditWriter/DbConnectionService/DbQueryService/DbAdminAgentService → `queries.ExpireStale()` 清过期审批 → RegisterTools（7 件 db_*） |
| 插件自有库建表 | `Plugins/DbAdmin/Data/DbAdminTables.cs:27-41`：ConnName=DbAdmin（连接串由宿主 XCodeConfig.PluginDbs 注册，插件内禁自注册）；EntityTypes 四表；EnsureCreated 幂等、失败返回 false 不静默吞 |
| 实体层 | `Data/Model.xml`（ConnName=DbAdmin、Namespace=ForgeSelf.Api.Plugins.DbAdmin.Entities、Output=Entities）：DbConnection（PasswordEnc/ConnStrEnc 加密字段、Name 唯一索引）、DbQueryHistory（仅存脱敏 SQL）、DbApproval（原始 SQL 仅内存、Status 索引）、DbAuditLog（含 kind=agent 摘要列 Source/SessionId/Status/Code/SqlKind/RowCount/Truncated/ApprovalId）；生成件 `Entities/*.cs` + 人工 `*.Biz.cs` 成对（xcode 幂等生成） |
| 连接模型 | `Services/DbConnectionService.cs:17-41`：系统只读连接=进程内 `DAL.ConnStrs` 全部非 `DbAdminU_` 前缀（宿主+PluginDbs），id=`sys:<ConnName>`，恒 Readonly；用户连接 ConnName=`DbAdminU_<id>` 隔离；唯一性直查 DB（:169-171 附近）；删除只删配置绝不删库文件 |
| 防误指系统库 | 同上 GuardNotSystemFile（:214-226 附近）：用户 SQLite 连接指向宿主/插件库文件 → invalid_request 拒绝；EnsureSqliteFileExists 不自动建库文件（:297-303 附近） |
| 测连恒 200 | 同上 Test（:363-415 附近）：未保存表单也可测；返回 {ok, code, message, serverVersion, elapsedMs}；driver_missing 带 NuGet 包名 |
| SQL 执行链 | `Services/DbQueryService.cs:58-87`：SqlGuard 分类 → 硬拒即 Reject → Resolve 连接 → 非只读语句遇只读连接硬拒（readonly_connection）→ 只读 RunRead → 写按权限模式（AskWrite 全部询问 / Auto 仅破坏性询问）→ Enqueue 审批或 RunWrite → ResultLimiter → 审计/历史（脱敏）；`Execute()` 对 Agent 来源额外写 kind=agent 审计（:41-46） |
| 审批模型 | 同上：审批 TTL 30 分钟（:19）；原始 SQL 仅内存 PendingWrite（:24-26）；`Resolve()` approve/reject；破坏性必须 UI 且 confirmDestructive=true，Agent 恒 confirmation_required；重启丢内存待审（落库行为 pending 行过期清理 ExpireStale） |
| SQL 分类与硬拒 | `Services/SqlGuard.cs:40-67`：先剥注释/字符串/引号标识符再判定（fail-closed：无法确定=普通写）；硬拒模式约 20 条（xp_*、sp_OA*、OPENROWSET、BULK INSERT、LOAD_FILE、COPY…PROGRAM、pg_*file、lo_import/export、LOAD_EXTENSION、VACUUM INTO、ATTACH/DETACH、DROP DATABASE/SCHEMA、SHUTDOWN、不受信语言函数）+ 无 WHERE 的 UPDATE/DELETE + 多语句（字符串/注释内分号不算） |
| 权限模式 | `Services/PermissionModeStore.cs:8-49`：全局默认持久化插件数据目录 permission-mode.txt（缺省 AskWrite）+ Agent 会话级覆盖仅内存；Agent 改模式只动会话级（DbQueryService.SetMode :369-380 校验） |
| 密码加密 | `Services/SecretProtector.cs:22-99`：密钥=插件数据目录 dbadmin.key（32B 随机，FileMode.CreateNew 防并发覆盖，Unix 0600）；AES-256-GCM，密文 `v1:`+Base64(nonce12\|tag16\|cipher)；GET 恒掩码 `****`；PUT 空/`****`=不改 |
| 脱敏 | `Services/SqlRedactor.cs`：RedactSql（字符串字面量疑似密钥整段掩码、IDENTIFIED BY/PASSWORD）、RedactText（password=/pwd=/Bearer/token/URI userinfo）；DescribeParams 只记参数名（:59-63） |
| 结果上限 | `Services/ResultLimiter.cs:6-48`：Agent ≤1000 行/≤256KB（带 truncated）；UI 分页 ≤500/页（默认 50）；单元格 ≤4096 字符、二进制只给长度 |
| 多引擎与驱动 | `Services/DriverCatalog.cs:21-56`：sqlite（宿主随附，tested=true）/mysql/postgresql/sqlserver（tested=false「未实测」）；MVP 不打包非 SQLite 驱动、禁 XCode 自动下载；缺驱动统一 driver_missing+包名（HTTP 424）；DefaultDriverProbe 探测已加载程序集→Type.GetType→宿主/插件/数据目录 DLL |
| Agent 工具 | `Services/DbAdminToolFunctions.cs:18-68`：DbToolBase 构造只存 pluginId+IServiceProvider，执行期 CreateHostScope 取 DbAdminAgentService（铁律26）；统一回包 {success,tool,status,data,hint?} / {success:false,…,code,message,hint}；出口两道擦除（RedactText+明文密钥清单逐字替换****）；7 件=db_list_connections/db_list_tables/db_describe_table/db_query_readonly（严格版仅 SELECT/WITH、服务端子查询包装强制上限）/db_execute_write/db_set_permission_mode（会话级）/db_resolve_approval（破坏性恒 confirmation_required、批准需用户原话 user_confirmation_required）（:576-588 Create 清单） |
| 审计写入 | `Services/DbAuditWriter.cs:9-80`：kind=conn_*/query/classify/approval_*/write_result/rejected/permission_mode/agent；写失败只 Warn 不阻断主流程；时间戳显式赋值（PC-022 lens） |
| 前端结构 | `web/src/DbAdminView.vue`（1150 行）：左连接+schema 树/右 SQL 编辑器（Ctrl+Enter :389）+结果格（分页）；浮层互斥 connForm→approval→destructive→deleteConn→audit（:147-157）；审批 5 秒轮询 /approvals?status=pending（:587-597）；S-R 刷新恢复（连接+当前连接+每连接草稿 localStorage，不恢复密码/结果集/待审批，:203-218）；fixture 存在即演示模式不发网络（CT 用）；`data-testid` 全集 70+（db-admin-root/version-badge/tree-skeleton/conn-tree/new-connection/cf-*/run/sql-editor/result-grid/result-error/approval-modal/approval-approve/approval-reject/approval-destructive/audit-drawer/pager/page-next/truncated/empty-guide 等） |
| 前端契约适配 | `web/src/normalize.ts` 唯一适配点（docs/03 §3.1）：provider 小写、id String()、kind read/write+destructive、列表/分页双形态；`web/src/approvalFlow.ts` 纯函数编排（批准→破坏性二次确认→resolve；拒绝不执行；错误码→中文红条） |
| 插件前端约束 | `web/vite.config.ts` lib 模式产物 dist/index.js+style.css；`web/src/index.ts` 仅导出 DbAdminView；vue/vue-router/element-plus 走 external import map（铁律4） |
| 宿主登记（4 处齐） | `ForgeSelf.Api/Data/XCodeConfig.cs` PluginDbs["DbAdmin"]="db-admin"；`ForgeSelf.Api/ForgeSelf.Api.csproj` ProjectReference(ReferenceOutputAssembly=false)；`ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj` + 根级 `Plugins/DbAdminTests/DbAdminTests.csproj`；`ForgeSelf.slnx` 登记；`ForgeSelf.Abstractions/ToolNamespace.cs` PrefixByPluginId["db-admin"]="db" |
| 宿主侧 CT | `ForgeSelf.Web/src/components/DbAdmin.story.ts`（S0–S9+S-R 全态 fixture，无明文密码）+ `DbAdmin.ct.spec.ts`（视觉+契约回归，expectNoPlaintextSecret 守卫）+ `DbAdminStoryHost.vue` 薄壳 + `ct/stories.ts:19,27` 注册 |
| 后端测试 | `ForgeSelf.Api.Tests/Plugins/DbAdminTests/`：9 文件（Fixture 随机隔离 temp 目录、Contract、ToolFunction、ToolEvalReplay、ConnectionService、QueryService、SqlGuard、SqlGuardScope、RedactionAndCrypto）；铁律10/11（随机隔离目录、只建不删） |
| 前端单测 | `Plugins/DbAdmin/web/src/*.test.ts` 9 文件（pager/normalize/restore/contract/api/http/approvalFlow/connForm/redact） |
| e2e 基建 | `ForgeSelf.Web/e2e/fixtures/e2e.ts`（beforeEach injectRealApiKey）；插件 e2e 范式=import {test,expect} from '../fixtures/e2e' → page.goto(前端 route)；globalSetup 需 build/runtime/Plugins 有 System.Data.SQLite.dll+e_sqlite3.dll、Plugins/*/web/dist 齐全（AGENTS §5.0 两缺件） |
| 路由一致性门 | `ForgeSelf.Web/e2e/menu-route-consistency.spec.ts`：①②③ 为运行时对账（manifest↔menu-items↔真实导航），新路由自动纳入；④ 为硬编码回归清单（mcp-center/design-system）——新插件条目加在此处（铁律19③） |

## 2 约束与红线

- 主工作区（main@2aa93aa 起，lead 届时通知最新头）只读参照：**禁切分支、禁碰** ImGateway/AIAgent WIP 清单（含 docs/02-features/031-im-gateway.md 只读）；开发全在 wt-db。
- **禁 git push**；**禁停/启/杀宿主进程**（:51888 pid 36180 用户实例）；本地前后端只准 `pwsh scripts/dev-stack.ps1`（worktree 内）。
- 实体改动必须 Model.xml→xcode，禁手改生成件；本批**不改实体**。
- 提交 trailer：`Pilot: 2026-11-05-db-admin`（非文档文件改动必须带）；插件代码本身不因收口而改逻辑（测试暴露缺陷才修，修则记 03-plan 偏差）。
- 测试前置：`$env:TEMP=$env:TMP='<wt>\.temp\tmp'` + `$env:NO_PROXY='localhost,127.0.0.1,::1'`；判定看日志正文不看 exit code；一律 pwsh 禁 5.1。
- 测试库文件一律落 .temp（绝不指向宿主/插件真实库文件）；清数据只删本批自建连接/历史。

## 3 复用与模式

- 执行链复用 XCode `DAL`（AddConnStr→DAL.Create→Tables/Query/Execute 参数化），与宿主同源；建表模式照 FileToolsTables（EnsureCreated 幂等 + 单一真源）。
- 插件前端远程加载契约照 SmartTerminal/Home（lib 产物 + import map external + forgeOpenPage 导航桥）。
- CT fixture 模式照既有插件（宿主薄壳 StoryHost 注入后端原始 JSON，normalize 同真路径）。
- e2e 用统一 fixture（injectRealApiKey）+ runtime manifest 对账（menu-route-consistency）。

## 4 适用问题类（docs/11-troubleshooting/problem-classes/ 检索）

| PC | 关系 | 本任务动作 |
|----|------|-----------|
| PC-003 插件库未登记 PluginDbs→升级丢数据 | 直接相关 | 已登记（XCodeConfig PluginDbs["DbAdmin"]，分支内）；AC3 过滤集含 XCodeConfigTests 复核 |
| PC-020 插件依赖宿主加载期解析不到/控制器构造注入宿主契约 | 直接相关 | 控制器只注入自有服务；AC3 含 PluginLoadDependencyGuardTests + PluginToolConstructorDiGuardTests |
| PC-005 插件执行体构造期 eager 解析宿主 provider | 镜面已满足 | DbToolBase 构造只存 IServiceProvider，执行期 CreateHostScope（铁律26） |
| PC-014 测试 ctor 手搓 AddConnStr 换库 | lens | DbAdminTestFixture 集合级单次建连+随机隔离目录+种子连接名唯一，不复用换库 |
| PC-019 flex/grid 子项压扁 | lens | 插件根视图滚动容器子块 flex-shrink:0（06 清单项5）；⑥ 走查读图核对 |
| PC-002 后端鉴权、前端裸 fetch 未带 token | lens | 插件 http.ts 统一封装携带 token；401 五态有专门 UI（banner-401）；e2e 走真实 token |
| PC-017 工具 schema JSON 重复键/坏括号 | lens | 7 件 db_* ParametersJsonSchema 由仓库级 ToolSchemaJsonGuardTests 全量兜底（qa-ci 中档） |
| PC-022 实体直插漏时间戳 | lens | DbAuditWriter/History 显式赋 CreateTime |
| PC-006/PC-007 PS 5.1 编码 / 手敲起服务 | 流程 | 全程 pwsh；起服务仅 dev-stack.ps1 |

未命中需逃逸分析的缺陷类：无（本任务非缺陷修复；测试/走查若暴露缺陷→按 problem-class-closure 另行定性并记 07）。
