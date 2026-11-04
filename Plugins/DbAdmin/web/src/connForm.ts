/** 连接表单：校验、表单↔连接串、未实测判定（S1 / S9） */
import type { DbConnection, DbConnectionInput, DbProvider } from './types'

export const DEFAULT_PORTS: Record<string, number> = { MySql: 3306, PostgreSQL: 5432, SqlServer: 1433 }

export function isSqlite(provider: string | undefined): boolean {
  return (provider ?? '').toLowerCase() === 'sqlite'
}

/** 仅 SQLite 实测；其它一律「未实测」 */
export function isTested(p: Pick<DbProvider, 'id' | 'tested'> | undefined): boolean {
  if (!p) return false
  return p.tested ?? isSqlite(p.id)
}

export function emptyForm(provider = 'SQLite'): DbConnectionInput {
  return { name: '', provider, mode: 'form', host: '', port: DEFAULT_PORTS[provider], database: '', userName: '', password: '', connStr: '' }
}

/** 编辑已有连接：密码置空（留空 = 不改，UI 占位显示 ****），连接串不回填掩码值 */
export function formFromConnection(c: DbConnection): DbConnectionInput {
  return {
    name: c.name, provider: c.provider, mode: c.mode,
    host: c.host ?? '', port: c.port, database: c.database ?? '', userName: c.userName ?? '',
    password: '', connStr: '',
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
  const parts = [
    `Server=${f.host ?? ''}`,
    f.port ? `Port=${f.port}` : '',
    `Database=${f.database ?? ''}`,
    f.userName ? `User Id=${f.userName}` : '',
    f.password ? `Password=${f.password}` : '',
  ].filter(Boolean)
  return parts.join(';')
}

/** 提交体：编辑且密码为空 → 不带 password 字段（03：密码空 = 不改） */
export function toSubmitBody(f: DbConnectionInput, editing: boolean): DbConnectionInput {
  const body: DbConnectionInput = { ...f }
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
