/**
 * DbAdmin 数据库管理 · CT 画廊故事（验收清单见 Plugins/DbAdmin/docs/06：S0–S9 + S-R）
 *
 * 组件在插件包内（Plugins/DbAdmin/web/src），宿主薄壳 DbAdminStoryHost 注入 fixture、跳过网络。
 * fixture = 后端原始 JSON（docs/03 §3.1 已定契约，形态取自真实 DbAdminController 样本
 * Plugins/DbAdmin/web/src/__fixtures__/backend-samples.json）：
 *   provider 小写 · 用户连接 id 数字串 / 系统连接 `sys:<ConnName>` · approval/history/audit id 为数字 ·
 *   kind 只有 read/write、破坏性用 destructive:true · /query 应答带 HTTP 状态（200/202/400/409/424）·
 *   S8 Agent 回显 = 审计 kind=agent 条目。
 * 视图走与真实请求同一套 normalize。
 * fixture 红线：不出现任何明文密码 / 真实密钥；密码字段一律 `****`（服务端掩码后的形态）。
 */
import DbAdminStoryHost from './DbAdminStoryHost.vue'
import type { ComponentStory } from '../../ct/types'
import type { BackendJson, DbAdminFixture } from '../../../Plugins/DbAdmin/web/src/types'

const AT = '2026-11-04T10:30:00'

/** GET /providers（真实 DriverCatalog） */
const providers: BackendJson[] = [
  { id: 'sqlite', displayName: 'SQLite', driverAvailable: true, tested: true, driverPackage: 'System.Data.SQLite（宿主已随附）', defaultPort: null },
  { id: 'mysql', displayName: 'MySQL / MariaDB', driverAvailable: true, tested: false, driverPackage: 'MySqlConnector', defaultPort: 3306 },
  { id: 'postgresql', displayName: 'PostgreSQL', driverAvailable: false, tested: false, driverPackage: 'Npgsql', defaultPort: 5432 },
  { id: 'sqlserver', displayName: 'SQL Server', driverAvailable: false, tested: false, driverPackage: 'Microsoft.Data.SqlClient', defaultPort: 1433 },
]

/** GET /connections 单项（ConnectionView） */
const conn = (p: BackendJson): BackendJson => ({
  kind: 'user', mode: 'form', host: null, port: null, database: null, userName: null, password: null, connStr: null,
  readonly: false, tested: false, createTime: AT, updateTime: AT, ...p,
})
const systemConns: BackendJson[] = [
  conn({ id: 'sys:ForgeSelf', name: 'ForgeSelf', provider: 'sqlite', kind: 'system', mode: 'system', readonly: true, tested: true, createTime: null, updateTime: null }),
  conn({ id: 'sys:FileTools', name: 'FileTools', provider: 'sqlite', kind: 'system', mode: 'system', readonly: true, tested: true, createTime: null, updateTime: null }),
]
const demo = conn({ id: '1', name: 'demo.db', provider: 'sqlite', database: '/data/demo.db', tested: true })
const pg = conn({ id: '2', name: '报表库', provider: 'postgresql', host: '10.0.0.12', port: 5432, database: 'reports', userName: 'report_ro', password: '****' })
const allConns = [...systemConns, demo, pg]

/** GET /connections/{id}/tables（TableSummary） */
const demoTables: BackendJson[] = [
  { name: 'users', isView: false, description: null, columnCount: 4 },
  { name: 'orders', isView: false, description: null, columnCount: 5 },
  { name: 'v_active_users', isView: true, description: null, columnCount: 2 },
]
const col = (name: string, rawType: string, dataType: string, nullable: boolean, primaryKey = false): BackendJson => ({
  name, dataType, rawType, nullable, primaryKey, identity: false, length: 0, defaultValue: null, description: null,
})
/** GET /connections/{id}/tables/{table}（TableDetail） */
const usersDetail: BackendJson = {
  name: 'users', isView: false, description: null,
  columns: [col('id', 'INTEGER', 'Int64', false, true), col('name', 'TEXT', 'String', false), col('email', 'TEXT', 'String', true), col('created_at', 'DATETIME', 'DateTime', true)],
  primaryKeys: ['id'],
  indexes: [{ name: 'ix_users_email', columns: ['email'], unique: true, primaryKey: false }],
}
const sysTables: BackendJson[] = [
  { name: 'Agent', isView: false, description: null, columnCount: 12 },
  { name: 'ChatMessage', isView: false, description: null, columnCount: 9 },
]

const userRows = (from: number, n: number) =>
  Array.from({ length: n }, (_, i) => {
    const id = from + i
    return [id, `user_${String(id).padStart(3, '0')}`, id % 7 === 0 ? null : `u${id}@example.com`, '2026-11-01 09:00:00']
  })

/** POST /query 的 QueryOutcome（所有字段都在，和后端一致） */
const outcome = (p: BackendJson): BackendJson => ({
  status: 'ok', code: null, message: null, kind: 'read', columns: null, columnTypes: null, rows: null, total: null, page: null,
  pageSize: null, truncated: false, affectedRows: null, elapsedMs: 0, approvalId: null, destructive: false, ...p,
})
const USER_COLS = { columns: ['id', 'name', 'email', 'created_at'], columnTypes: ['Int64', 'String', 'String', 'DateTime'] }

/** GET /history（HistoryView，分页对象） */
const hist = (items: BackendJson[]): BackendJson => ({ items, total: items.length, page: 1, pageSize: 50 })
const baseHistory = hist([
  { id: 2, connectionId: '1', sqlRedacted: 'SELECT * FROM users ORDER BY id', kind: 'read', rowCount: 120, elapsedMs: 4, success: true, error: '', source: 'ui', createTime: AT },
  { id: 1, connectionId: '1', sqlRedacted: "INSERT INTO users(name) VALUES ('****')", kind: 'write', rowCount: 1, elapsedMs: 2, success: true, error: '', source: 'agent', createTime: '2026-11-04T10:28:00' },
])

/** GET /audit 单项（AuditView；kind=agent 时带 Agent 摘要字段） */
const aud = (p: BackendJson): BackendJson => ({
  connectionId: '1', sqlRedacted: null, decision: null, detail: null, createTime: AT, source: null, sessionId: null, status: null,
  code: null, sqlKind: null, rowCount: null, truncated: null, approvalId: null, tool: null, ...p,
})
const agentAud = (p: BackendJson): BackendJson => aud({ kind: 'agent', source: 'agent', sessionId: 'sess-7f3a', truncated: false, rowCount: 0, ...p, decision: p.tool })

const base = (patch: Partial<DbAdminFixture> = {}): DbAdminFixture => ({
  version: '0.1.0-ct',
  permissionMode: 'AskWrite',
  providers,
  connections: allConns,
  activeConnectionId: '1',
  tables: { '1': demoTables, 'sys:ForgeSelf': sysTables },
  tableDetails: { '1/users': usersDetail },
  expanded: ['c:1', 't:1/users'],
  sql: 'SELECT * FROM users ORDER BY id',
  history: baseHistory,
  ...patch,
})

/** GET /approvals?status=pending 单项（ApprovalView：数字 id；kind read/write + destructive） */
const pendingInsert: BackendJson = {
  id: 11, connectionId: '1', sqlRedacted: "INSERT INTO users(name, email) VALUES ('alice', 'alice@example.com')",
  kind: 'write', destructive: false, status: 'pending', source: 'ui', createTime: AT, resolveTime: null,
}
const pendingDrop: BackendJson = {
  id: 12, connectionId: '1', sqlRedacted: 'DROP TABLE orders',
  kind: 'write', destructive: true, status: 'pending', source: 'ui', createTime: AT, resolveTime: null,
}
const pendingAgent: BackendJson = {
  id: 21, connectionId: '1', sqlRedacted: "UPDATE users SET name = 'bob' WHERE id = 7",
  kind: 'write', destructive: false, status: 'pending', source: 'agent', createTime: AT, resolveTime: null,
}

/** S8：GET /audit?kind=agent 的条目 */
const agentWrite = agentAud({ id: 33, tool: 'db_execute_write', sqlRedacted: "UPDATE users SET name = 'bob' WHERE id = 7", status: 'awaiting_approval', sqlKind: 'write', approvalId: 21, detail: '写操作等待批准', createTime: '2026-11-04T10:32:00' })
const agentRead = agentAud({ id: 32, tool: 'db_query_readonly', sqlRedacted: 'SELECT * FROM events', status: 'ok', sqlKind: 'read', rowCount: 1000, truncated: true, createTime: '2026-11-04T10:31:30' })
const agentRejected = agentAud({ id: 31, tool: 'db_query_readonly', sqlRedacted: 'DROP TABLE events', status: 'rejected', code: 'rejected_hard', sqlKind: 'write', detail: 'db_query_readonly 只接受 SELECT / WITH 等只读语句，写操作请用 db_execute_write', createTime: '2026-11-04T10:31:00' })

const restoreSeed = {
  version: 1,
  savedAt: AT,
  activeConnectionId: '1',
  connections: [
    { id: 'sys:ForgeSelf', name: 'ForgeSelf', provider: 'sqlite', mode: 'form', readonly: true, system: true },
    { id: '1', name: 'demo.db', provider: 'sqlite', mode: 'form', readonly: false, system: false, database: '/data/demo.db' },
    // 落盘前由 StoryHost + saveSnapshot 双重剥离 password；此处故意给掩码值验证剥离
    { id: '2', name: '报表库', provider: 'postgresql', mode: 'form', readonly: false, system: false, host: '10.0.0.12', port: 5432, database: 'reports', userName: 'report_ro', password: '****' },
  ],
  drafts: {
    '1': '-- 刷新前未执行的草稿\nSELECT id, name FROM users WHERE email IS NULL',
    '2': 'SELECT count(*) FROM daily_sales',
  },
}

export const stories: ComponentStory[] = [
  // 测试状态：五态·加载 —— 版本徽标「加载中」+ 树骨架（S0 前置）
  { id: 'DbAdmin/Loading', component: DbAdminStoryHost, props: { fixture: { version: '加载中', loading: true, providers, connections: [] } satisfies DbAdminFixture } },
  // 测试状态：S0 空态 —— 无自建连接；系统只读区（sys:<ConnName>）带 🔒；「+ 新建连接」引导可点
  { id: 'DbAdmin/Empty', component: DbAdminStoryHost, props: { fixture: base({ connections: systemConns, activeConnectionId: null, expanded: [], sql: undefined, history: hist([]) }) } },
  // 测试状态：五态·无权限 —— 401 横幅，不假装有数据
  { id: 'DbAdmin/Unauthorized', component: DbAdminStoryHost, props: { fixture: { version: '0.1.0-ct', unauthorized: true } satisfies DbAdminFixture } },
  // 测试状态：S1 连接表单 —— SQLite 文件路径表单（新建，provider=sqlite）
  { id: 'DbAdmin/ConnFormSqlite', component: DbAdminStoryHost, props: { fixture: base({ overlay: 'connForm', connForm: { name: 'demo.db', provider: 'sqlite', mode: 'form', database: '/data/demo.db' } }) } },
  // 测试状态：S1 连接表单 —— 校验失败留窗 + 编辑态密码遮罩（**** 留空不改）
  { id: 'DbAdmin/ConnFormInvalid', component: DbAdminStoryHost, props: { fixture: base({ overlay: 'connForm', connForm: { editingId: '2', name: '报表库', provider: 'postgresql', mode: 'form', host: '10.0.0.12', port: 5432, database: '', userName: 'report_ro', password: '' }, connFormError: '请填写数据库名' }) } },
  // 测试状态：S2 Schema 树 —— 连接 → 表 → 列（rawType / PK）；系统库展开并带只读徽标
  { id: 'DbAdmin/SchemaTree', component: DbAdminStoryHost, props: { fixture: base({ activeConnectionId: 'sys:ForgeSelf', expanded: ['c:1', 't:1/users', 'c:sys:ForgeSelf'], sql: 'SELECT * FROM Agent' }) } },
  // 测试状态：S3 只读结果 —— 200 ok：columns:string[] + columnTypes，服务端分页（第 3 页 / 末页）+ 耗时
  { id: 'DbAdmin/ReadResult', component: DbAdminStoryHost, props: { fixture: base({ reply: { httpStatus: 200, body: outcome({ ...USER_COLS, rows: userRows(101, 20), total: 120, page: 3, pageSize: 50, elapsedMs: 4 }) } }) } },
  // 测试状态：S4 写审批 —— POST /query 202 awaiting_approval → 审批弹窗（语句 / 类型 / 来源 / 目标连接）
  { id: 'DbAdmin/WriteApproval', component: DbAdminStoryHost, props: { fixture: base({ sql: String(pendingInsert.sqlRedacted), reply: { httpStatus: 202, body: outcome({ status: 'awaiting_approval', kind: 'write', approvalId: 11, message: '写操作等待批准' }) }, approvals: [pendingInsert], activeApprovalId: '11', overlay: 'approval' }) } },
  // 测试状态：S4 破坏性 —— kind=write + destructive:true；批准后进入二次确认
  { id: 'DbAdmin/DestructiveApproval', component: DbAdminStoryHost, props: { fixture: base({ sql: 'DROP TABLE orders', reply: { httpStatus: 202, body: outcome({ status: 'awaiting_approval', kind: 'write', destructive: true, approvalId: 12, message: '破坏性语句：需在界面批准并二次确认' }) }, approvals: [pendingDrop], activeApprovalId: '12', overlay: 'approval' }) } },
  // 测试状态：S4 破坏性二次确认 —— 直接定格在二次确认框（确认后 resolve 带 confirmDestructive=true）
  { id: 'DbAdmin/DestructiveConfirm', component: DbAdminStoryHost, props: { fixture: base({ sql: 'DROP TABLE orders', approvals: [pendingDrop], activeApprovalId: '12', overlay: 'destructive' }) } },
  // 测试状态：S4 破坏性未确认 —— resolve 409 confirmation_required：红条，审批仍待处理
  { id: 'DbAdmin/ConfirmRequired409', component: DbAdminStoryHost, props: { fixture: base({ sql: 'DROP TABLE orders', approvals: [pendingDrop], reply: { httpStatus: 409, body: { code: 'confirmation_required', message: '破坏性语句需在界面批准并二次确认（confirmDestructive=true）' } } }) } },
  // 测试状态：S5 失败 —— 400 sql_error（body 为 QueryOutcome status=error），结果区红条，不弹审批
  { id: 'DbAdmin/SqlError', component: DbAdminStoryHost, props: { fixture: base({ sql: 'SELEC * FROM users', reply: { httpStatus: 400, body: outcome({ status: 'error', code: 'sql_error', message: 'SQL logic error\nnear "SELEC": syntax error' }) } }) } },
  // 测试状态：S5 硬拒 —— 200 + status=rejected / rejected_hard：无 WHERE 的 DELETE 直接红条
  { id: 'DbAdmin/HardReject', component: DbAdminStoryHost, props: { fixture: base({ sql: 'DELETE FROM users', reply: { httpStatus: 200, body: outcome({ status: 'rejected', code: 'rejected_hard', kind: 'write', message: '禁止无 WHERE 的 DELETE' }) } }) } },
  // 测试状态：S5 只读连接写入被拒 —— 系统库 UPDATE：200 + rejected / readonly_connection
  { id: 'DbAdmin/ReadonlyWriteRejected', component: DbAdminStoryHost, props: { fixture: base({ activeConnectionId: 'sys:ForgeSelf', expanded: ['c:sys:ForgeSelf'], sql: "UPDATE Agent SET Name = 'x' WHERE Id = 1", reply: { httpStatus: 200, body: outcome({ status: 'rejected', code: 'readonly_connection', kind: 'write', message: '系统连接（宿主/插件库）只读，任何写操作均被拒绝' }) } }) } },
  // 测试状态：S5 驱动缺失 —— 424 driver_missing：PostgreSQL 连接执行，红条指明需安装的包
  { id: 'DbAdmin/DriverMissing424', component: DbAdminStoryHost, props: { fixture: base({ activeConnectionId: '2', expanded: [], sql: 'SELECT count(*) FROM daily_sales', reply: { httpStatus: 424, body: outcome({ status: 'error', code: 'driver_missing', message: 'PostgreSQL 驱动未安装：本插件 MVP 未随附该驱动。请将 NuGet 包「Npgsql」的 DLL 放入宿主程序目录或 DbAdmin 插件目录后重启。' }) } }) } },
  // 测试状态：S6 审计 —— 抽屉列出 query / approval_* / rejected / conn_create 记录（后端真实 kind）
  {
    id: 'DbAdmin/AuditDrawer', component: DbAdminStoryHost, props: { fixture: base({
      overlay: 'audit',
      audit: { total: 5, page: 1, pageSize: 100, items: [
        aud({ id: 5, kind: 'rejected', sqlRedacted: 'DELETE FROM users', decision: 'rejected_hard', detail: 'source=ui 禁止无 WHERE 的 DELETE', createTime: '2026-11-04T10:31:00' }),
        aud({ id: 4, kind: 'approval_approved', sqlRedacted: "INSERT INTO users(name) VALUES ('****')", decision: 'approved', detail: 'approvalId=11 by=ui confirm=False', createTime: '2026-11-04T10:30:40' }),
        aud({ id: 3, kind: 'approval_pending', sqlRedacted: "INSERT INTO users(name) VALUES ('****')", decision: 'ask', detail: 'approvalId=11 destructive=False source=ui', createTime: '2026-11-04T10:30:20' }),
        aud({ id: 2, kind: 'query', sqlRedacted: 'SELECT * FROM users ORDER BY id', decision: 'allow', detail: 'source=ui rows=50 truncated=False', createTime: AT }),
        aud({ id: 1, kind: 'conn_create', connectionId: '2', decision: 'ok', detail: 'postgresql 10.0.0.12:5432/reports（password=****）', createTime: '2026-11-04T10:20:00' }),
      ] },
    }) },
  },
  // 测试状态：S7 脱敏 —— 连接、历史、审计中密码与连接串全部 `****`，全页无明文
  {
    id: 'DbAdmin/Redaction', component: DbAdminStoryHost, props: { fixture: base({
      connections: [...systemConns, demo, pg, conn({ id: '3', name: '旧 MySQL（连接串）', provider: 'mysql', mode: 'connstr', connStr: 'Server=10.0.0.5;Database=legacy;User Id=app;Password=****' })],
      activeConnectionId: '3',
      expanded: [],
      sql: 'SELECT 1',
      history: hist([
        { id: 3, connectionId: '3', sqlRedacted: "UPDATE accounts SET api_key = '****' WHERE id = 3", kind: 'write', rowCount: 1, elapsedMs: 3, success: true, error: '', source: 'agent', createTime: AT },
        { id: 2, connectionId: '3', sqlRedacted: "SELECT * FROM t WHERE token = '****'", kind: 'read', rowCount: 0, elapsedMs: 0, success: false, error: 'Access denied for user app (using password: ****)', source: 'ui', createTime: AT },
      ]),
      sideTab: 'history',
    }) },
  },
  // 测试状态：S8 Agent 工具回显 —— 来自 GET /audit?kind=agent（tool/status/rowCount/truncated/approvalId）+ Agent 写挂起后 UI 出审批「来自 Agent」
  {
    id: 'DbAdmin/AgentToolEcho', component: DbAdminStoryHost, props: { fixture: base({
      sideTab: 'agent',
      audit: { total: 3, page: 1, pageSize: 50, items: [agentWrite, agentRead, agentRejected] },
      approvals: [pendingAgent],
      activeApprovalId: '21',
      overlay: 'approval',
    }) },
  },
  // 测试状态：S8 Agent 截断 —— 审计 kind=agent 条目 truncated=true / rowCount=1000（Agent 上限截断，UI 结果不截断）
  {
    id: 'DbAdmin/AgentTruncated', component: DbAdminStoryHost, props: { fixture: base({
      sideTab: 'agent',
      audit: { total: 2, page: 1, pageSize: 50, items: [
        agentRead,
        // 非 agent 审计不进回显
        aud({ id: 30, kind: 'query', sqlRedacted: 'SELECT * FROM events', decision: 'allow', detail: 'source=agent rows=1000 truncated=True' }),
      ] },
      reply: { httpStatus: 200, body: outcome({ ...USER_COLS, rows: userRows(1, 12), total: 12, page: 1, pageSize: 50, elapsedMs: 18 }) },
    }) },
  },
  // 测试状态：S9 多 provider —— MySQL 表单，测连成功（恒 200 {ok:true, code:null, serverVersion}），旁标「未实测」
  { id: 'DbAdmin/ProviderTestOk', component: DbAdminStoryHost, props: { fixture: base({ overlay: 'connForm', connForm: { name: '订单库', provider: 'mysql', mode: 'form', host: '10.0.0.20', port: 3306, database: 'orders', userName: 'app', password: '' }, testResult: { ok: true, code: null, message: '连接成功', serverVersion: '8.0.36', elapsedMs: 12 } }) } },
  // 测试状态：S9 多 provider —— 连接串模式（遮罩），测连失败（恒 200 {ok:false, code:sql_error}，原因已脱敏）
  { id: 'DbAdmin/ProviderTestError', component: DbAdminStoryHost, props: { fixture: base({ overlay: 'connForm', connForm: { name: '订单库', provider: 'mysql', mode: 'connstr', connStr: '' }, testResult: { ok: false, code: 'sql_error', message: 'Unable to connect to 10.0.0.20:3306（password=****）: Connection refused', serverVersion: null, elapsedMs: 3004 } }) } },
  // 测试状态：S9 多 provider —— PostgreSQL 驱动缺失（恒 200 {ok:false, code:driver_missing}，message 指明 Npgsql）+ 下拉「需驱动」
  { id: 'DbAdmin/ProviderDriverMissing', component: DbAdminStoryHost, props: { fixture: base({ overlay: 'connForm', connForm: { name: '报表库', provider: 'postgresql', mode: 'form', host: '10.0.0.12', port: 5432, database: 'reports', userName: 'report_ro', password: '' }, testResult: { ok: false, code: 'driver_missing', message: 'PostgreSQL 驱动未安装：本插件 MVP 未随附该驱动。请将 NuGet 包「Npgsql」的 DLL 放入宿主程序目录或 DbAdmin 插件目录后重启。', serverVersion: null, elapsedMs: 0 } }) } },
  // 测试状态：S-R 刷新前 —— 编辑器有草稿、结果格有数据（草稿写入 localStorage，结果集不落盘）
  { id: 'DbAdmin/RestoreBefore', component: DbAdminStoryHost, props: { fixture: base({ sql: restoreSeed.drafts['1'], reply: { httpStatus: 200, body: outcome({ columns: ['id', 'name'], columnTypes: ['Int64', 'String'], rows: [[7, 'user_007'], [14, 'user_014']], total: 2, page: 1, pageSize: 50, elapsedMs: 2 }) } }) } },
  // 测试状态：S-R 刷新后 —— 连接列表 / 当前连接 / 草稿从 localStorage 恢复；无密码、无结果集；待审批从服务端重拉
  { id: 'DbAdmin/RestoreAfter', component: DbAdminStoryHost, props: { fixture: { version: '0.1.0-ct', providers, seedRestore: restoreSeed, approvals: [pendingInsert] } satisfies DbAdminFixture } },
]
