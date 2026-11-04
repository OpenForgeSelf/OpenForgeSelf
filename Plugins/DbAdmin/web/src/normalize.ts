/**
 * 响应归一：03 只定了路径与实体字段，未定 JSON 细节。这里把后端可能的形态
 * （如 columns:string[] + columnTypes、数字 id、isView、ok:boolean、kind:'system'）
 * 统一成前端类型，联调时差异集中在此一处。
 */
import type {
  DbApproval, DbAuditLog, DbColumn, DbConnection, DbProvider, DbQueryHistory, DbTable, DbTableDetail,
  QueryResponse, ResultColumn, TestConnectionResult,
} from './types'

type Raw = Record<string, unknown>
const str = (v: unknown): string | undefined => (v === null || v === undefined ? undefined : String(v))
const num = (v: unknown): number | undefined => (typeof v === 'number' ? v : v == null || v === '' ? undefined : Number(v))

export function normProvider(r: Raw): DbProvider {
  return {
    id: String(r.id ?? ''),
    displayName: String(r.displayName ?? r.id ?? ''),
    driverAvailable: r.driverAvailable !== false,
    driverPackage: str(r.driverPackage),
    tested: typeof r.tested === 'boolean' ? r.tested : undefined,
  }
}

export function normConnection(r: Raw): DbConnection {
  const kind = str(r.kind)
  return {
    id: String(r.id ?? ''),
    name: String(r.name ?? ''),
    provider: String(r.provider ?? ''),
    mode: r.mode === 'connstr' ? 'connstr' : 'form',
    host: str(r.host),
    port: num(r.port),
    database: str(r.database),
    userName: str(r.userName),
    password: r.password ? '****' : '',
    connStr: str(r.connStr),
    readonly: !!r.readonly,
    system: typeof r.system === 'boolean' ? r.system : kind ? kind === 'system' : undefined,
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

export function normQuery(r: Raw): QueryResponse {
  const types = Array.isArray(r.columnTypes) ? (r.columnTypes as unknown[]) : []
  const columns: ResultColumn[] | undefined = Array.isArray(r.columns)
    ? (r.columns as unknown[]).map((c, i) =>
        typeof c === 'string' ? { name: c, type: str(types[i]) } : { name: String((c as Raw).name ?? ''), type: str((c as Raw).type) })
    : undefined
  const status = r.status === 'awaiting_approval' || r.status === 'rejected' ? r.status : 'ok'
  return {
    status,
    kind: r.kind === 'write' ? 'write' : r.kind === 'read' ? 'read' : undefined,
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

export function normTest(r: Raw): TestConnectionResult {
  let status: TestConnectionResult['status']
  if (r.status === 'ok' || r.status === 'error' || r.status === 'driver_missing') status = r.status
  else if (r.code === 'driver_missing') status = 'driver_missing'
  else status = r.ok === true ? 'ok' : 'error'
  return { status, message: str(r.message), serverVersion: str(r.serverVersion), driverPackage: str(r.driverPackage) }
}

export function normApproval(r: Raw): DbApproval {
  const st = str(r.status)
  return {
    id: String(r.id ?? ''),
    connectionId: String(r.connectionId ?? ''),
    connectionName: str(r.connectionName),
    sqlRedacted: String(r.sqlRedacted ?? ''),
    kind: String(r.kind ?? 'write'),
    destructive: !!r.destructive,
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
    kind: r.kind === 'write' ? 'write' : 'read',
    rowCount: num(r.rowCount),
    elapsedMs: num(r.elapsedMs),
    success: r.success !== false,
    error: str(r.error),
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
  }
}

/** 数组或 {items} → 数组，并逐项归一 */
export function listOf<T>(data: unknown, fn: (r: Raw) => T): T[] {
  const arr = Array.isArray(data) ? data : data && typeof data === 'object' && Array.isArray((data as Raw).items) ? ((data as Raw).items as unknown[]) : []
  return arr.filter(x => x && typeof x === 'object').map(x => fn(x as Raw))
}
