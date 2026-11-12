/**
 * DbAdmin 前端类型 —— 以 docs/03 §3.1「已定契约」与后端 Models/DbAdminModels.cs 为准（camelCase JSON）。
 * 这里是 normalize 之后的前端形态；后端原始 JSON 见 `BackendJson` 与 normalize.ts。
 */

/** provider id 以后端小写为准；展示名见 connForm.PROVIDER_LABELS / providers[].displayName */
export type ProviderId = 'sqlite' | 'mysql' | 'postgresql' | 'sqlserver' | 'oracle' | (string & {})
export type ConnMode = 'form' | 'connstr'
/** 后端原始 JSON 对象（fixture 与联调样本按此形态写，经 normalize 进入 UI） */
export type BackendJson = Record<string, unknown>
export type PermissionMode = 'AskWrite' | 'Auto'
export type SqlKind = 'read' | 'write'
export type Source = 'ui' | 'agent'

/** GET /providers */
export interface DbProvider {
  id: ProviderId
  displayName: string
  driverAvailable: boolean
  /** 缺驱动时提示的 NuGet 包名（如 MySqlConnector） */
  driverPackage?: string
  /** 仅 SQLite 实测；缺省按 id==='sqlite' 判断 */
  tested?: boolean
  defaultPort?: number
}

/** GET /connections 单项（密码与连接串均为掩码版） */
export interface DbConnection {
  /** 用户连接为数字串 "12"；系统只读连接为 "sys:<ConnName>" */
  id: string
  name: string
  provider: ProviderId
  /** 系统连接后端给 mode="system"，前端按 form 展示（不可编辑） */
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
  /** 由后端 kind==='system' 推断（宿主库 / PluginDbs） */
  system?: boolean
  /** 非 SQLite 为 false（「未实测」） */
  tested?: boolean
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
  /**
   * 「默认表」为本机界面偏好（落 localStorage），**不进入请求体**（`connForm.toSubmitBody` 会剔除）。
   * 填了 → 点该连接直接打开该表标签；留空 → 点连接只展开并加载全部表。
   */
  defaultTable?: string
}

/** POST /connections/test —— 恒 200，后端 `{ok, code, message, serverVersion, elapsedMs}`；status 为前端派生三态 */
export interface TestConnectionResult {
  status: 'ok' | 'error' | 'driver_missing'
  ok: boolean
  /** 后端 code：null(=ok) | sql_error | driver_missing */
  code?: string
  message?: string
  serverVersion?: string
  elapsedMs?: number
  /** driver_missing 时需安装的包（后端放在 message 里；此字段仅兼容） */
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

/**
 * POST /query 与 /approvals/{id}/resolve 的响应（后端 QueryOutcome）。
 * HTTP：ok / rejected → 200；awaiting_approval → 202；error → 按 code 映射（400/404/424/409…）。
 */
export interface QueryResponse {
  status: 'ok' | 'awaiting_approval' | 'rejected' | 'error'
  /** 对外只有 read / write；破坏性看 destructive */
  kind?: SqlKind
  destructive?: boolean
  /** 响应的 HTTP 状态码（202 = 待审批） */
  httpStatus?: number
  columns?: ResultColumn[]
  rows?: unknown[][]
  total?: number
  page?: number
  pageSize?: number
  elapsedMs?: number
  affectedRows?: number
  truncated?: boolean
  approvalId?: string
  /** rejected / error 时：错误码（rejected_hard / readonly_connection / approval_rejected / sql_error …） */
  code?: string
  message?: string
}

/** 结果区错误红条（HTTP 非 2xx 或业务 rejected） */
export interface ResultErrorState {
  code: string
  message: string
  httpStatus?: number
}

/** GET /approvals（DbApproval） */
export interface DbApproval {
  id: string
  connectionId: string
  connectionName?: string
  sqlRedacted: string
  /** 对外只有 read / write（§3.1）；破坏性看 destructive */
  kind: SqlKind
  destructive: boolean
  status: 'pending' | 'approved' | 'rejected' | 'expired'
  source: Source
  createTime: string
  resolveTime?: string
  /** 后端不提供；仅前端演示用的风险说明 */
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

/** GET /audit（AuditView）；kind==='agent' 时附带 Agent 回显摘要字段（§3.1 ④） */
export interface DbAuditLog {
  id: string
  kind: string
  connectionId?: string
  sqlRedacted?: string
  decision?: string
  detail?: string
  createTime: string
  source?: Source
  sessionId?: string
  status?: string
  code?: string
  sqlKind?: SqlKind
  rowCount?: number
  truncated?: boolean
  approvalId?: string
  tool?: string
}

export interface PagedList<T> {
  items: T[]
  total: number
  page?: number
  pageSize?: number
}

/** /audit 与 /history 过滤（pageSize 上限 500） */
export interface LogFilter {
  kind?: string
  source?: Source
  sessionId?: string
  status?: string
  connectionId?: string
  page?: number
  pageSize?: number
}

/**
 * S8：Agent `db_*` 工具回显 —— 由 `GET /audit?kind=agent` 的审计条目映射（§3.1 ④）：
 * tool←tool、status←status、rowCount←rowCount、truncated←truncated、approvalId←approvalId、
 * message←detail、at←createTime；参数只展示 sqlRedacted。
 */
export interface AgentToolEcho {
  id: string
  tool: string
  sql?: string
  status: 'ok' | 'awaiting_approval' | 'rejected' | 'error'
  code?: string
  sqlKind?: SqlKind
  rowCount?: number
  truncated?: boolean
  approvalId?: string
  sessionId?: string
  connectionId?: string
  message?: string
  at: string
}

export type OverlayKind = 'connForm' | 'approval' | 'destructive' | 'deleteConn' | 'audit' | null

/**
 * CT / 演示 fixture：存在即跳过网络。
 * 数据字段一律写**后端原始 JSON 形态**（数字 id、小写 provider、kind read/write + destructive、sys: 前缀…），
 * 由 DbAdminView 走与真实请求同一套 normalize 进入 UI —— fixture 即契约样例。
 */
export interface DbAdminFixture {
  version?: string
  loading?: boolean
  unauthorized?: boolean
  banner?: { kind: 'error' | 'warn'; text: string } | null
  permissionMode?: PermissionMode
  providers?: BackendJson[]
  connections?: BackendJson[]
  activeConnectionId?: string | null
  tables?: Record<string, BackendJson[]>
  /** 每个连接的「加载表」局部状态（CT 用于定格 loading；真实请求由 DbAdminView 维护） */
  tablesLoading?: Record<string, boolean>
  tableDetails?: Record<string, BackendJson>
  expanded?: string[]
  sql?: string
  /** 多标签 fixture：初始标签（key 可省略，按 table/seq 规则生成） */
  tabs?: Array<{
    key?: string
    connectionId: string
    table?: string
    sql?: string
    /** 该标签预置一条结果（旧 fixture 形态） */
    reply?: { httpStatus: number; body: BackendJson }
    /** 该标签预置多条结果（多语句结果标签条定格用） */
    replies?: Array<{ httpStatus: number; body: BackendJson }>
  }>
  /** 活动标签 key（缺省取第一个） */
  activeTabKey?: string | null
  /** 本机偏好：连接默认表 */
  defaultTables?: Record<string, string>
  /** 上次使用的连接（刷新后高亮侧栏） */
  lastConnectionId?: string | null
  /** 最近一次 POST /query 或 resolve 的 HTTP 应答（status + 原始 body）；≥400 → 红条 */
  reply?: { httpStatus: number; body: BackendJson } | null
  /** 多语句批量：按顺序取用；用尽后回落到 `reply`（每条单语句一条应答） */
  replies?: Array<{ httpStatus: number; body: BackendJson }>
  /** GET /approvals?status=pending 原始数组 */
  approvals?: BackendJson[]
  /** GET /history 原始分页对象或数组 */
  history?: BackendJson[] | BackendJson
  /** GET /audit 原始分页对象或数组（含 kind=agent 条目 → S8 回显） */
  audit?: BackendJson[] | BackendJson
  auditFilter?: string
  overlay?: OverlayKind
  activeApprovalId?: string
  connForm?: Partial<DbConnectionInput> & { editingId?: string }
  connFormError?: string
  /** POST /connections/test 原始 body（恒 200） */
  testResult?: BackendJson | null
  sideTab?: 'history' | 'agent'
  /** 先写入 localStorage 再挂载（S-R 恢复后态）；落盘前会剥密码 */
  seedRestore?: Record<string, unknown>
  /** 演示「恢复前」：显示上次会话草稿摘要 */
  showRestoreHint?: boolean
}
