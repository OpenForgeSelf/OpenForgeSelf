/**
 * 响应归一 —— docs/03 §3.1「已定契约」规定此文件是前端唯一适配点。
 * 后端真实形态（camelCase）：数字 approval/history/audit id 与 approvalId → String()；
 * provider 小写；连接 kind user|system（mode=system）；query columns:string[] + columnTypes；
 * kind 只有 read/write + destructive；测连 {ok, code}；history/audit 分页 {items,total,page,pageSize}。
 * 旧形态（kind=destructive、status 三态测连、columns 对象数组）仍兼容。
 * 样本：src/__fixtures__/backend-samples.json（由真实 DbAdminController 产出，见 contract.test.ts）。
 */
import type {
  AgentToolEcho, DbApproval, DbAuditLog, DbColumn, DbConnection, DbProvider, DbQueryHistory, DbTable,
  DbTableDetail, QueryResponse, ResultColumn, ResultErrorState, SqlKind, TestConnectionResult,
} from './types'

type Raw = Record<string, unknown>
const str = (v: unknown): string | undefined => (v === null || v === undefined ? undefined : String(v))
const num = (v: unknown): number | undefined => (typeof v === 'number' ? v : v == null || v === '' ? undefined : Number(v))
const bool = (v: unknown): boolean | undefined => (typeof v === 'boolean' ? v : undefined)

/** kind 归一：对外 read / write；旧形态 destructive / insert / drop_table … 一律视为 write */
export function normKind(v: unknown): SqlKind | undefined {
  if (v === null || v === undefined || v === '') return undefined
  return v === 'read' ? 'read' : 'write'
}
/** 破坏性：契约用 destructive:true；兼容旧 kind=destructive */
function isDestructive(r: Raw): boolean {
  return r.destructive === true || r.kind === 'destructive'
}

export function normProvider(r: Raw): DbProvider {
  return {
    id: String(r.id ?? '').toLowerCase(),
    displayName: String(r.displayName ?? r.id ?? ''),
    driverAvailable: r.driverAvailable !== false,
    driverPackage: str(r.driverPackage),
    tested: bool(r.tested),
    defaultPort: num(r.defaultPort),
  }
}

export function normConnection(r: Raw): DbConnection {
  const kind = str(r.kind)
  return {
    id: String(r.id ?? ''),
    name: String(r.name ?? ''),
    provider: String(r.provider ?? '').toLowerCase(),
    mode: r.mode === 'connstr' ? 'connstr' : 'form',
    host: str(r.host),
    port: num(r.port),
    database: str(r.database),
    userName: str(r.userName),
    password: r.password ? '****' : '',
    connStr: str(r.connStr),
    readonly: !!r.readonly,
    system: typeof r.system === 'boolean' ? r.system : kind ? kind === 'system' : String(r.id ?? '').startsWith('sys:') || undefined,
    tested: bool(r.tested),
    createTime: str(r.createTime),
    updateTime: str(r.updateTime),
  }
}

export function normTable(r: Raw): DbTable {
  return { name: String(r.name ?? ''), type: r.type === 'view' || r.isView === true ? 'view' : 'table' }
}

export function normTableDetail(r: Raw): DbTableDetail {
  const pks = new Set(Array.isArray(r.primaryKeys) ? (r.primaryKeys as unknown[]).map(String) : [])
  const cols = Array.isArray(r.columns) ? (r.columns as Raw[]) : []
  return {
    name: String(r.name ?? ''),
    columns: cols.map((c): DbColumn => ({
      name: String(c.name ?? ''),
      type: String(c.type ?? c.rawType ?? c.dataType ?? ''),
      nullable: c.nullable !== false,
      primaryKey: c.primaryKey === true || pks.has(String(c.name)),
    })),
    indexes: Array.isArray(r.indexes)
      ? (r.indexes as Raw[]).map(i => ({ name: String(i.name ?? ''), columns: Array.isArray(i.columns) ? (i.columns as unknown[]).map(String) : [], unique: !!i.unique }))
      : [],
  }
}

/** QueryOutcome → 前端；httpStatus 202 且缺 status 时视为 awaiting_approval */
export function normQuery(r: Raw, httpStatus?: number): QueryResponse {
  const types = Array.isArray(r.columnTypes) ? (r.columnTypes as unknown[]) : []
  const columns: ResultColumn[] | undefined = Array.isArray(r.columns)
    ? (r.columns as unknown[]).map((c, i) =>
        typeof c === 'string' ? { name: c, type: str(types[i]) } : { name: String((c as Raw).name ?? ''), type: str((c as Raw).type) })
    : undefined
  const status: QueryResponse['status'] =
    r.status === 'awaiting_approval' || r.status === 'rejected' || r.status === 'error' ? r.status
      : r.status === undefined && httpStatus === 202 ? 'awaiting_approval'
        : 'ok'
  return {
    status,
    kind: normKind(r.kind),
    destructive: isDestructive(r),
    httpStatus,
    columns,
    rows: Array.isArray(r.rows) ? (r.rows as unknown[][]) : undefined,
    total: num(r.total),
    page: num(r.page),
    pageSize: num(r.pageSize),
    elapsedMs: num(r.elapsedMs),
    affectedRows: num(r.affectedRows),
    truncated: r.truncated === true,
    approvalId: str(r.approvalId),
    code: str(r.code),
    message: str(r.message),
  }
}

/** 测连：契约 `{ok, code(ok|sql_error|driver_missing), message, serverVersion}`；实测成功时 code 为 null */
export function normTest(r: Raw): TestConnectionResult {
  let status: TestConnectionResult['status']
  if (r.code === 'driver_missing' || r.status === 'driver_missing') status = 'driver_missing'
  else if (typeof r.ok === 'boolean') status = r.ok ? 'ok' : 'error'
  else if (r.status === 'ok' || r.status === 'error') status = r.status
  else status = r.code === 'ok' ? 'ok' : 'error'
  return {
    status,
    ok: status === 'ok',
    code: str(r.code) ?? (status === 'ok' ? 'ok' : undefined),
    message: str(r.message),
    serverVersion: str(r.serverVersion),
    elapsedMs: num(r.elapsedMs),
    driverPackage: str(r.driverPackage),
  }
}

export function normApproval(r: Raw): DbApproval {
  const st = str(r.status)
  return {
    id: String(r.id ?? ''),
    connectionId: String(r.connectionId ?? ''),
    connectionName: str(r.connectionName),
    sqlRedacted: String(r.sqlRedacted ?? ''),
    kind: normKind(r.kind) ?? 'write',
    destructive: isDestructive(r),
    status: st === 'approved' || st === 'rejected' || st === 'expired' ? st : 'pending',
    source: r.source === 'agent' ? 'agent' : 'ui',
    createTime: String(r.createTime ?? ''),
    resolveTime: str(r.resolveTime),
    risk: str(r.risk),
  }
}

export function normHistory(r: Raw): DbQueryHistory {
  return {
    id: String(r.id ?? ''),
    connectionId: String(r.connectionId ?? ''),
    sqlRedacted: String(r.sqlRedacted ?? ''),
    kind: normKind(r.kind) ?? 'read',
    rowCount: num(r.rowCount),
    elapsedMs: num(r.elapsedMs),
    success: r.success !== false,
    error: str(r.error) || undefined,
    source: r.source === 'agent' ? 'agent' : 'ui',
    createTime: String(r.createTime ?? ''),
  }
}

export function normAudit(r: Raw): DbAuditLog {
  return {
    id: String(r.id ?? ''),
    kind: String(r.kind ?? ''),
    connectionId: str(r.connectionId),
    sqlRedacted: str(r.sqlRedacted),
    decision: str(r.decision),
    detail: str(r.detail),
    createTime: String(r.createTime ?? ''),
    source: r.source === 'agent' ? 'agent' : r.source === 'ui' ? 'ui' : undefined,
    sessionId: str(r.sessionId),
    status: str(r.status),
    code: str(r.code),
    sqlKind: normKind(r.sqlKind),
    rowCount: num(r.rowCount),
    truncated: bool(r.truncated),
    approvalId: str(r.approvalId),
    tool: str(r.tool),
  }
}

/** §3.1 ④：审计 kind=agent 条目 → S8 回显；非 agent 条目返回 null */
export function auditToAgentEcho(a: DbAuditLog): AgentToolEcho | null {
  if (a.kind !== 'agent') return null
  const st = a.status
  return {
    id: a.id,
    tool: a.tool ?? a.decision ?? 'db_*',
    sql: a.sqlRedacted,
    status: st === 'awaiting_approval' || st === 'rejected' || st === 'error' ? st : 'ok',
    code: a.code,
    sqlKind: a.sqlKind,
    rowCount: a.rowCount,
    truncated: a.truncated,
    approvalId: a.approvalId,
    sessionId: a.sessionId,
    connectionId: a.connectionId,
    message: a.detail,
    at: a.createTime,
  }
}

/** /audit 原始应答 → S8 回显列表（只取 kind=agent） */
export function agentEchoesOf(data: unknown): AgentToolEcho[] {
  return listOf(data, normAudit).map(auditToAgentEcho).filter((x): x is AgentToolEcho => x !== null)
}

/**
 * /query、/resolve 的一次 HTTP 应答 → 结果区状态（真实请求与 fixture 共用）：
 * - 2xx：status ok → 结果格；awaiting_approval(202) → 审批；rejected(200) → 红条
 * - ≥400：body {code,message}（或带 code 的 QueryOutcome）→ 红条（409 confirmation_required、424 driver_missing…）
 */
export type ReplyState =
  | { kind: 'result'; result: QueryResponse }
  | { kind: 'error'; error: ResultErrorState }

const CODE_BY_STATUS: Record<number, string> = {
  400: 'sql_error', 401: 'unauthorized', 403: 'rejected_hard', 404: 'not_found', 409: 'confirmation_required', 424: 'driver_missing',
}

export function replyToState(httpStatus: number, body: unknown): ReplyState {
  const r: Raw = body && typeof body === 'object' ? (body as Raw) : {}
  if (httpStatus >= 400) {
    return {
      kind: 'error',
      error: { code: str(r.code) ?? CODE_BY_STATUS[httpStatus] ?? 'sql_error', message: str(r.message) ?? '', httpStatus },
    }
  }
  return { kind: 'result', result: normQuery(r, httpStatus) }
}

/** 数组或 {items} → 数组，并逐项归一 */
export function listOf<T>(data: unknown, fn: (r: Raw) => T): T[] {
  const arr = Array.isArray(data) ? data : data && typeof data === 'object' && Array.isArray((data as Raw).items) ? ((data as Raw).items as unknown[]) : []
  return arr.filter(x => x && typeof x === 'object').map(x => fn(x as Raw))
}
