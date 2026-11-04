/**
 * DbAdmin 数据库管理 · CT 画廊故事（验收清单见 Plugins/DbAdmin/docs/06：S0–S9 + S-R）
 *
 * 组件在插件包内（Plugins/DbAdmin/web/src），宿主薄壳 DbAdminStoryHost 注入 fixture、跳过网络。
 * fixture 红线：不出现任何明文密码 / 真实密钥；密码字段一律 `****`（服务端掩码后的形态）。
 */
import DbAdminStoryHost from './DbAdminStoryHost.vue'
import type { ComponentStory } from '../../ct/types'
import type {
  DbAdminFixture, DbConnection, DbProvider, DbTable, DbTableDetail,
} from '../../../Plugins/DbAdmin/web/src/types'

const AT = '2026-11-04T10:30:00+08:00'

const providers: DbProvider[] = [
  { id: 'SQLite', displayName: 'SQLite', driverAvailable: true, tested: true },
  { id: 'MySql', displayName: 'MySQL', driverAvailable: true, driverPackage: 'XCode.MySql' },
  { id: 'PostgreSQL', displayName: 'PostgreSQL', driverAvailable: false, driverPackage: 'XCode.PostgreSQL' },
  { id: 'SqlServer', displayName: 'SQL Server', driverAvailable: false, driverPackage: 'XCode.SqlServer' },
]

const systemConns: DbConnection[] = [
  { id: 'sys:ForgeSelf', name: 'ForgeSelf', provider: 'SQLite', mode: 'form', database: 'Data/ForgeSelf.db', readonly: true, system: true },
  { id: 'sys:FileTools', name: 'FileTools', provider: 'SQLite', mode: 'form', database: 'Data/file-tools.db', readonly: true, system: true },
]
const demo: DbConnection = { id: '1', name: 'demo.db', provider: 'SQLite', mode: 'form', database: '/data/demo.db', readonly: false, system: false }
const pg: DbConnection = {
  id: '2', name: '报表库', provider: 'PostgreSQL', mode: 'form', host: '10.0.0.12', port: 5432,
  database: 'reports', userName: 'report_ro', password: '****', readonly: false, system: false,
}
const allConns = [...systemConns, demo, pg]

const demoTables: DbTable[] = [{ name: 'users', type: 'table' }, { name: 'orders', type: 'table' }, { name: 'v_active_users', type: 'view' }]
const usersDetail: DbTableDetail = {
  name: 'users',
  columns: [
    { name: 'id', type: 'INTEGER', nullable: false, primaryKey: true },
    { name: 'name', type: 'TEXT', nullable: false, primaryKey: false },
    { name: 'email', type: 'TEXT', nullable: true, primaryKey: false },
    { name: 'created_at', type: 'DATETIME', nullable: true, primaryKey: false },
  ],
  indexes: [{ name: 'ix_users_email', columns: ['email'], unique: true }],
}
const sysTables: DbTable[] = [{ name: 'Agent', type: 'table' }, { name: 'ChatMessage', type: 'table' }]

const userRows = (from: number, n: number) =>
  Array.from({ length: n }, (_, i) => {
    const id = from + i
    return [id, `user_${String(id).padStart(3, '0')}`, id % 7 === 0 ? null : `u${id}@example.com`, '2026-11-01 09:00:00']
  })

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
  history: [
    { id: 'h2', connectionId: '1', sqlRedacted: 'SELECT * FROM users ORDER BY id', kind: 'read', rowCount: 120, elapsedMs: 4, success: true, source: 'ui', createTime: AT },
    { id: 'h1', connectionId: '1', sqlRedacted: "INSERT INTO users(name) VALUES ('****')", kind: 'write', rowCount: 1, elapsedMs: 2, success: true, source: 'agent', createTime: '2026-11-04T10:28:00+08:00' },
  ],
  ...patch,
})

const pendingInsert = {
  id: '11', connectionId: '1', connectionName: 'demo.db', sqlRedacted: "INSERT INTO users(name, email) VALUES ('alice', 'alice@example.com')",
  kind: 'insert', destructive: false, status: 'pending' as const, source: 'ui' as const, createTime: AT,
}
const pendingDrop = {
  id: '12', connectionId: '1', connectionName: 'demo.db', sqlRedacted: 'DROP TABLE orders',
  kind: 'drop_table', destructive: true, status: 'pending' as const, source: 'ui' as const, createTime: AT,
}

const restoreSeed = {
  version: 1,
  savedAt: AT,
  activeConnectionId: '1',
  connections: [
    { id: 'sys:ForgeSelf', name: 'ForgeSelf', provider: 'SQLite', mode: 'form', readonly: true, system: true, database: 'Data/ForgeSelf.db' },
    { id: '1', name: 'demo.db', provider: 'SQLite', mode: 'form', readonly: false, system: false, database: '/data/demo.db' },
    // 落盘前由 StoryHost + saveSnapshot 双重剥离 password；此处故意给掩码值验证剥离
    { id: '2', name: '报表库', provider: 'PostgreSQL', mode: 'form', readonly: false, system: false, host: '10.0.0.12', port: 5432, database: 'reports', userName: 'report_ro', password: '****' },
  ],
  drafts: {
    '1': '-- 刷新前未执行的草稿\nSELECT id, name FROM users WHERE email IS NULL',
    '2': 'SELECT count(*) FROM daily_sales',
  },
}

export const stories: ComponentStory[] = [
  // 测试状态：五态·加载 —— 版本徽标「加载中」+ 树骨架（S0 前置）
  { id: 'DbAdmin/Loading', component: DbAdminStoryHost, props: { fixture: { version: '加载中', loading: true, providers, connections: [] } satisfies DbAdminFixture } },
  // 测试状态：S0 空态 —— 无自建连接；系统只读区带 🔒；「+ 新建连接」引导可点
  { id: 'DbAdmin/Empty', component: DbAdminStoryHost, props: { fixture: base({ connections: systemConns, activeConnectionId: null, expanded: [], sql: undefined, history: [] }) } },
  // 测试状态：五态·无权限 —— 401 横幅，不假装有数据
  { id: 'DbAdmin/Unauthorized', component: DbAdminStoryHost, props: { fixture: { version: '0.1.0-ct', unauthorized: true } satisfies DbAdminFixture } },
  // 测试状态：S1 连接表单 —— SQLite 文件路径表单（新建）
  { id: 'DbAdmin/ConnFormSqlite', component: DbAdminStoryHost, props: { fixture: base({ overlay: 'connForm', connForm: { name: 'demo.db', provider: 'SQLite', mode: 'form', database: '/data/demo.db' } }) } },
  // 测试状态：S1 连接表单 —— 校验失败留窗 + 编辑态密码遮罩（**** 留空不改）
  { id: 'DbAdmin/ConnFormInvalid', component: DbAdminStoryHost, props: { fixture: base({ overlay: 'connForm', connForm: { editingId: '2', name: '报表库', provider: 'PostgreSQL', mode: 'form', host: '10.0.0.12', port: 5432, database: '', userName: 'report_ro', password: '' }, connFormError: '请填写数据库名' }) } },
  // 测试状态：S2 Schema 树 —— 连接 → 表 → 列（类型 / PK）；系统库展开并带只读徽标
  { id: 'DbAdmin/SchemaTree', component: DbAdminStoryHost, props: { fixture: base({ activeConnectionId: 'sys:ForgeSelf', expanded: ['c:1', 't:1/users', 'c:sys:ForgeSelf'], sql: 'SELECT * FROM Agent' }) } },
  // 测试状态：S3 只读结果 —— SELECT 结果格 + 分页（第 3 页 / 末页，下一页禁用）+ 耗时
  { id: 'DbAdmin/ReadResult', component: DbAdminStoryHost, props: { fixture: base({ result: { status: 'ok', kind: 'read', columns: [{ name: 'id', type: 'INTEGER' }, { name: 'name', type: 'TEXT' }, { name: 'email', type: 'TEXT' }, { name: 'created_at', type: 'DATETIME' }], rows: userRows(101, 20), total: 120, page: 3, pageSize: 50, elapsedMs: 4 } }) } },
  // 测试状态：S4 写审批 —— 审批弹窗（语句 / 类型 / 来源 / 目标连接）
  { id: 'DbAdmin/WriteApproval', component: DbAdminStoryHost, props: { fixture: base({ sql: pendingInsert.sqlRedacted, approvals: [pendingInsert], activeApprovalId: '11', overlay: 'approval' }) } },
  // 测试状态：S4 破坏性 —— 审批弹窗带「破坏性」；批准后进入二次确认
  { id: 'DbAdmin/DestructiveApproval', component: DbAdminStoryHost, props: { fixture: base({ sql: 'DROP TABLE orders', approvals: [pendingDrop], activeApprovalId: '12', overlay: 'approval' }) } },
  // 测试状态：S4 破坏性二次确认 —— 直接定格在二次确认框
  { id: 'DbAdmin/DestructiveConfirm', component: DbAdminStoryHost, props: { fixture: base({ sql: 'DROP TABLE orders', approvals: [pendingDrop], activeApprovalId: '12', overlay: 'destructive' }) } },
  // 测试状态：S5 失败 —— SQL 错误显示在结果区，不弹审批
  { id: 'DbAdmin/SqlError', component: DbAdminStoryHost, props: { fixture: base({ sql: 'SELEC * FROM users', resultError: { code: 'sql_error', message: 'SQLite Error 1: near "SELEC": syntax error' } }) } },
  // 测试状态：S5 硬拒 —— 无 WHERE 的 DELETE 直接红条拒绝
  { id: 'DbAdmin/HardReject', component: DbAdminStoryHost, props: { fixture: base({ sql: 'DELETE FROM users', resultError: { code: 'rejected_hard', message: 'DELETE 缺少 WHERE 子句' } }) } },
  // 测试状态：S5 只读连接写入被拒 —— 系统库上的 UPDATE，红条 readonly_connection
  { id: 'DbAdmin/ReadonlyWriteRejected', component: DbAdminStoryHost, props: { fixture: base({ activeConnectionId: 'sys:ForgeSelf', expanded: ['c:sys:ForgeSelf'], sql: "UPDATE Agent SET Name = 'x' WHERE Id = 1", resultError: { code: 'readonly_connection', message: '系统连接 ForgeSelf 只读' } }) } },
  // 测试状态：S6 审计 —— 抽屉列出 query / approval / rejected 记录
  {
    id: 'DbAdmin/AuditDrawer', component: DbAdminStoryHost, props: { fixture: base({
      overlay: 'audit',
      audit: [
        { id: 'a5', kind: 'rejected', connectionId: '1', sqlRedacted: 'DELETE FROM users', decision: 'rejected_hard', detail: '无 WHERE', createTime: '2026-11-04T10:31:00+08:00' },
        { id: 'a4', kind: 'approval_approved', connectionId: '1', sqlRedacted: "INSERT INTO users(name) VALUES ('****')", decision: 'approved', createTime: '2026-11-04T10:30:40+08:00' },
        { id: 'a3', kind: 'approval_requested', connectionId: '1', sqlRedacted: "INSERT INTO users(name) VALUES ('****')", createTime: '2026-11-04T10:30:20+08:00' },
        { id: 'a2', kind: 'query', connectionId: '1', sqlRedacted: 'SELECT * FROM users ORDER BY id', detail: '120 行 · 4 ms', createTime: AT },
        { id: 'a1', kind: 'conn_create', connectionId: '2', detail: 'PostgreSQL 10.0.0.12:5432/reports（password=****）', createTime: '2026-11-04T10:20:00+08:00' },
      ],
    }) },
  },
  // 测试状态：S7 脱敏 —— 连接、历史、审计中密码与连接串全部 `****`，全页无明文
  {
    id: 'DbAdmin/Redaction', component: DbAdminStoryHost, props: { fixture: base({
      connections: [...systemConns, demo, pg, { id: '3', name: '旧 MySQL（连接串）', provider: 'MySql', mode: 'connstr', connStr: 'Server=10.0.0.5;Database=legacy;User Id=app;Password=****', password: '****', readonly: false, system: false }],
      activeConnectionId: '3',
      expanded: [],
      sql: 'SELECT 1',
      history: [
        { id: 'h3', connectionId: '3', sqlRedacted: "UPDATE accounts SET api_key = '****' WHERE id = 3", kind: 'write', rowCount: 1, elapsedMs: 3, success: true, source: 'agent', createTime: AT },
        { id: 'h2', connectionId: '3', sqlRedacted: "SELECT * FROM t WHERE token = '****'", kind: 'read', success: false, error: 'Access denied for user app (using password: ****)', source: 'ui', createTime: AT },
      ],
      sideTab: 'history',
    }) },
  },
  // 测试状态：S8 Agent 工具回显 —— db_* 调用列表（truncated=true）+ Agent 写挂起后 UI 出审批「来自 Agent」
  {
    id: 'DbAdmin/AgentToolEcho', component: DbAdminStoryHost, props: { fixture: base({
      sideTab: 'agent',
      agentEchoes: [
        { tool: 'db_execute_write', args: { connectionId: '1', sql: "UPDATE users SET name = 'bob' WHERE id = 7" }, status: 'awaiting_approval', approvalId: '21', at: '2026-11-04T10:32:00+08:00' },
        { tool: 'db_query_readonly', args: { connectionId: '1', sql: 'SELECT * FROM events' }, status: 'ok', rowCount: 1000, truncated: true, message: '1000 行 / 256KB 上限', at: '2026-11-04T10:31:30+08:00' },
        { tool: 'db_list_tables', args: { connectionId: '1' }, status: 'ok', rowCount: 3, at: '2026-11-04T10:31:00+08:00' },
      ],
      approvals: [{ id: '21', connectionId: '1', connectionName: 'demo.db', sqlRedacted: "UPDATE users SET name = 'bob' WHERE id = 7", kind: 'update', destructive: false, status: 'pending', source: 'agent', createTime: AT }],
      activeApprovalId: '21',
      overlay: 'approval',
    }) },
  },
  // 测试状态：S8 Agent 截断结果 —— 结果格顶部「truncated=true」提示（审批关闭后的同屏）
  {
    id: 'DbAdmin/AgentTruncated', component: DbAdminStoryHost, props: { fixture: base({
      sideTab: 'agent',
      agentEchoes: [{ tool: 'db_query_readonly', args: { connectionId: '1', sql: 'SELECT * FROM users' }, status: 'ok', rowCount: 1000, truncated: true, at: AT }],
      result: { status: 'ok', columns: [{ name: 'id', type: 'INTEGER' }, { name: 'name', type: 'TEXT' }, { name: 'email', type: 'TEXT' }, { name: 'created_at', type: 'DATETIME' }], rows: userRows(1, 12), page: 1, pageSize: 50, total: 1000, truncated: true, elapsedMs: 18 },
    }) },
  },
  // 测试状态：S9 多 provider —— MySQL 表单，测连成功（绿，服务器版本），旁标「未实测」
  { id: 'DbAdmin/ProviderTestOk', component: DbAdminStoryHost, props: { fixture: base({ overlay: 'connForm', connForm: { name: '订单库', provider: 'MySql', mode: 'form', host: '10.0.0.20', port: 3306, database: 'orders', userName: 'app', password: '' }, testResult: { status: 'ok', serverVersion: 'MySQL 8.0.36' } }) } },
  // 测试状态：S9 多 provider —— 连接串模式（遮罩），测连失败（红，原因已脱敏）
  { id: 'DbAdmin/ProviderTestError', component: DbAdminStoryHost, props: { fixture: base({ overlay: 'connForm', connForm: { name: '订单库', provider: 'MySql', mode: 'connstr', connStr: '' }, testResult: { status: 'error', message: 'Unable to connect to 10.0.0.20:3306（password=****）: Connection refused' } }) } },
  // 测试状态：S9 多 provider —— PostgreSQL 驱动缺失（黄，指明 XCode.PostgreSQL）+ 下拉「需驱动」
  { id: 'DbAdmin/ProviderDriverMissing', component: DbAdminStoryHost, props: { fixture: base({ overlay: 'connForm', connForm: { name: '报表库', provider: 'PostgreSQL', mode: 'form', host: '10.0.0.12', port: 5432, database: 'reports', userName: 'report_ro', password: '' }, testResult: { status: 'driver_missing', driverPackage: 'XCode.PostgreSQL', message: '未找到 Npgsql 提供程序' } }) } },
  // 测试状态：S-R 刷新前 —— 编辑器有草稿、结果格有数据（草稿写入 localStorage，结果集不落盘）
  { id: 'DbAdmin/RestoreBefore', component: DbAdminStoryHost, props: { fixture: base({ sql: restoreSeed.drafts['1'], result: { status: 'ok', columns: [{ name: 'id', type: 'INTEGER' }, { name: 'name', type: 'TEXT' }], rows: [[7, 'user_007'], [14, 'user_014']], total: 2, page: 1, pageSize: 50, elapsedMs: 2 } }) } },
  // 测试状态：S-R 刷新后 —— 连接列表 / 当前连接 / 草稿从 localStorage 恢复；无密码、无结果集；待审批从服务端重拉
  { id: 'DbAdmin/RestoreAfter', component: DbAdminStoryHost, props: { fixture: { version: '0.1.0-ct', providers, seedRestore: restoreSeed, approvals: [pendingInsert] } satisfies DbAdminFixture } },
]
