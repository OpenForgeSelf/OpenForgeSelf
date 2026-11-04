/**
 * DbAdmin 前端类型 —— 字段名以 docs/03-架构与接口.md 为准（camelCase JSON）。
 * 03 未写死的响应细节（如 query 结果的 columns/rows 形态）在此标注「前端假设」，联调时对齐。
 */

export type ProviderId = 'SQLite' | 'MySql' | 'PostgreSQL' | 'SqlServer' | (string & {})
export type ConnMode = 'form' | 'connstr'
export type PermissionMode = 'AskWrite' | 'Auto'
export type SqlKind = 'read' | 'write'
export type Source = 'ui' | 'agent'

/** GET /providers */
export interface DbProvider {
  id: ProviderId
  displayName: string
  driverAvailable: boolean
  /** 前端假设：缺驱动时提示的 NuGet 包名（如 XCode.MySql） */
  driverPackage?: string
  /** 前端假设：仅 SQLite 实测；缺省按 id==='SQLite' 判断 */
  tested?: boolean
}

/** GET /connections 单项（密码与连接串均为掩码版） */
export interface DbConnection {
  id: string
  name: string
  provider: ProviderId
  mode: ConnMode
  host?: string
  port?: number
  database?: string
  userName?: string
  /** 永远是 `****` 或空；前端绝不持有明文 */
  password?: string
  /** 掩码后的连接串（PasswordEnc / ConnStrEnc 不下发） */
  connStr?: string
  readonly: boolean
  /** 前端假设：系统只读连接标记（宿主库 / PluginDbs）；缺省按 readonly 推断 */
  system?: boolean
  createTime?: string
  updateTime?: string
}

/** POST/PUT /connections 请求体；password 空 = 不改 */
export interface DbConnectionInput {
  name: string
  provider: ProviderId
  mode: ConnMode
  host?: string
  port?: number
  database?: string
  userName?: string
  password?: string
  connStr?: string
}

/** POST /connections/test */
export interface TestConnectionResult {
  status: 'ok' | 'error' | 'driver_missing'
  message?: string
  serverVersion?: string
  /** driver_missing 时需安装的包 */
  driverPackage?: string
}

/** GET /connections/{id}/tables */
export interface DbTable {
  name: string
  type: 'table' | 'view'
}

export interface DbColumn {
  name: string
  type: string
  nullable: boolean
  primaryKey: boolean
}

export interface DbIndex {
  name: string
  columns: string[]
  unique: boolean
}

/** GET /connections/{id}/tables/{table} */
export interface DbTableDetail {
  name: string
  columns: DbColumn[]
  indexes: DbIndex[]
}

/** POST /query 请求 */
export interface QueryRequest {
  connectionId: string
  sql: string
  params?: Record<string, unknown>
  page: number
  pageSize: number
}

export interface ResultColumn {
  name: string
  type?: string
}

/** POST /query 响应（前端假设：status 三态 + 结果形态） */
export interface QueryResponse {
  status: 'ok' | 'awaiting_approval' | 'rejected'
  kind?: SqlKind
  columns?: ResultColumn[]
  rows?: unknown[][]
  total?: number
  page?: number
  pageSize?: number
  elapsedMs?: number
  affectedRows?: number
  truncated?: boolean
  approvalId?: string
  /** rejected 时：03 错误码 */
  code?: string
  message?: string
}

/** GET /approvals（DbApproval） */
export interface DbApproval {
  id: string
  connectionId: string
  connectionName?: string
  sqlRedacted: string
  kind: string
  destructive: boolean
  status: 'pending' | 'approved' | 'rejected' | 'expired'
  source: Source
  createTime: string
  resolveTime?: string
  /** 前端假设：预估风险说明 */
  risk?: string
}

/** POST /approvals/{id}/resolve 请求（03 未定字段；对齐后端 ApprovalResolveInput） */
export interface ResolveApprovalRequest {
  decision: 'approve' | 'reject'
  /** 破坏性语句：UI 二次确认后才为 true */
  confirmDestructive?: boolean
}

/** GET /history（DbQueryHistory） */
export interface DbQueryHistory {
  id: string
  connectionId: string
  sqlRedacted: string
  kind: SqlKind
  rowCount?: number
  elapsedMs?: number
  success: boolean
  error?: string
  source: Source
  createTime: string
}

/** GET /audit（DbAuditLog） */
export interface DbAuditLog {
  id: string
  kind: string
  connectionId?: string
  sqlRedacted?: string
  decision?: string
  detail?: string
  createTime: string
}

export interface PagedList<T> {
  items: T[]
  total: number
}

/** S8：Agent `db_*` 工具回显（来自聊天，前端只展示） */
export interface AgentToolEcho {
  tool: string
  args: Record<string, unknown>
  status: 'ok' | 'awaiting_approval' | 'rejected'
  rowCount?: number
  truncated?: boolean
  approvalId?: string
  message?: string
  at: string
}

export type OverlayKind = 'connForm' | 'approval' | 'destructive' | 'deleteConn' | 'audit' | null

/** CT / 演示 fixture：存在即跳过网络 */
export interface DbAdminFixture {
  version?: string
  loading?: boolean
  unauthorized?: boolean
  banner?: { kind: 'error' | 'warn'; text: string } | null
  permissionMode?: PermissionMode
  providers?: DbProvider[]
  connections?: DbConnection[]
  activeConnectionId?: string | null
  tables?: Record<string, DbTable[]>
  tableDetails?: Record<string, DbTableDetail>
  expanded?: string[]
  sql?: string
  result?: QueryResponse | null
  resultError?: { code: string; message: string } | null
  approvals?: DbApproval[]
  history?: DbQueryHistory[]
  audit?: DbAuditLog[]
  auditFilter?: string
  agentEchoes?: AgentToolEcho[]
  overlay?: OverlayKind
  activeApprovalId?: string
  connForm?: Partial<DbConnectionInput> & { editingId?: string }
  connFormError?: string
  testResult?: TestConnectionResult | null
  sideTab?: 'history' | 'agent'
  /** 先写入 localStorage 再挂载（S-R 恢复后态）；落盘前会剥密码 */
  seedRestore?: Record<string, unknown>
  /** 演示「恢复前」：显示上次会话草稿摘要 */
  showRestoreHint?: boolean
}
