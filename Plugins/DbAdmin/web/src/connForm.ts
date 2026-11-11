/** 连接表单：校验、表单↔连接串、未实测判定（S1 / S9） */
import type { DbConnection, DbConnectionInput, DbProvider } from './types'

/** provider id 以后端小写为准（§3.1）；后端 providers[].defaultPort 优先 */
export const DEFAULT_PORTS: Record<string, number> = { mysql: 3306, postgresql: 5432, sqlserver: 1433, oracle: 1521 }

/** 前端自带展示名（providers 列表未加载 / 连接上只有 id 时用） */
export const PROVIDER_LABELS: Record<string, string> = {
  sqlite: 'SQLite', mysql: 'MySQL / MariaDB', postgresql: 'PostgreSQL', sqlserver: 'SQL Server', oracle: 'Oracle',
}

export function providerLabel(id: string | undefined, providers: Pick<DbProvider, 'id' | 'displayName'>[] = []): string {
  const k = (id ?? '').toLowerCase()
  return providers.find(p => p.id === k)?.displayName ?? PROVIDER_LABELS[k] ?? id ?? ''
}

export function defaultPort(provider: string | undefined, providers: Pick<DbProvider, 'id' | 'defaultPort'>[] = []): number | undefined {
  const k = (provider ?? '').toLowerCase()
  return providers.find(p => p.id === k)?.defaultPort ?? DEFAULT_PORTS[k]
}

export function isSqlite(provider: string | undefined): boolean {
  return (provider ?? '').toLowerCase() === 'sqlite'
}

/** 仅 SQLite 实测；其它一律「未实测」 */
export function isTested(p: Pick<DbProvider, 'id' | 'tested'> | undefined): boolean {
  if (!p) return false
  return p.tested ?? isSqlite(p.id)
}

export function emptyForm(provider = 'sqlite'): DbConnectionInput {
  const p = provider.toLowerCase()
  return { name: '', provider: p, mode: 'form', host: '', port: DEFAULT_PORTS[p], database: '', userName: '', password: '', connStr: '', defaultTable: '' }
}

/** 编辑已有连接：密码置空（留空 = 不改，UI 占位显示 ****），连接串不回填掩码值 */
export function formFromConnection(c: DbConnection, defaultTable = ''): DbConnectionInput {
  return {
    name: c.name, provider: c.provider, mode: c.mode,
    host: c.host ?? '', port: c.port, database: c.database ?? '', userName: c.userName ?? '',
    password: '', connStr: '', defaultTable,
  }
}

/** 校验；返回首个错误文案或 null */
export function validateForm(f: DbConnectionInput, editing: boolean): string | null {
  if (!f.name.trim()) return '请填写连接名称'
  if (f.mode === 'connstr') {
    if (!editing && !(f.connStr ?? '').trim()) return '请填写连接串'
    return null
  }
  if (isSqlite(f.provider)) {
    if (!(f.database ?? '').trim()) return '请填写 SQLite 文件路径'
    return null
  }
  if (!(f.host ?? '').trim()) return '请填写主机'
  if (f.port !== undefined && (!Number.isInteger(f.port) || f.port <= 0 || f.port > 65535)) return '端口应为 1–65535'
  if (!(f.database ?? '').trim()) return '请填写数据库名'
  return null
}

/** 表单 → 连接串（切到「连接串」模式时预填；输入框始终遮罩） */
export function buildConnStr(f: DbConnectionInput): string {
  if (isSqlite(f.provider)) return `Data Source=${f.database ?? ''}`
  if ((f.provider ?? '').toLowerCase() === 'oracle') {
    // ODP.NET EZConnect，与服务端 BuildConnStr oracle 分支同构
    const port = f.port || 1521
    return [`Data Source=//${f.host ?? ''}:${port}/${f.database ?? ''}`,
      f.userName ? `User Id=${f.userName}` : '',
      f.password ? `Password=${f.password}` : ''].filter(Boolean).join(';')
  }
  const parts = [
    `Server=${f.host ?? ''}`,
    f.port ? `Port=${f.port}` : '',
    `Database=${f.database ?? ''}`,
    f.userName ? `User Id=${f.userName}` : '',
    f.password ? `Password=${f.password}` : '',
  ].filter(Boolean)
  return parts.join(';')
}

/** 提交体：编辑且密码为空 → 不带 password 字段（03：密码空 = 不改）；`defaultTable` 是本机偏好，绝不进请求体 */
export function toSubmitBody(f: DbConnectionInput, editing: boolean): DbConnectionInput {
  const body: DbConnectionInput = { ...f }
  delete body.defaultTable
  if (body.mode === 'connstr') {
    delete body.host; delete body.port; delete body.database; delete body.userName; delete body.password
    if (editing && !body.connStr) delete body.connStr
  } else {
    delete body.connStr
    if (isSqlite(body.provider)) { delete body.host; delete body.port; delete body.userName; delete body.password }
    if (editing && !body.password) delete body.password
  }
  return body
}
