/** 左树筛选：一个框同时匹配连接名、库名、表名。空查询不过滤。 */

export interface FilterNode {
  name: string
  tables: string[]
}

export function filterQuery(q: string): string {
  return q.trim().toLowerCase()
}

function has(text: string, q: string): boolean {
  return text.toLowerCase().includes(q)
}

/** 表是否留下。库名命中时该库下的表都留。连接名命中不因此放出全部表。 */
export function tableVisible(table: string, qRaw: string, dbName?: string): boolean {
  const q = filterQuery(qRaw)
  if (!q) return true
  if (dbName && has(dbName, q)) return true
  return has(table, q)
}

/** 库是否留下：库名或其中任一表名命中。 */
export function databaseVisible(db: FilterNode, qRaw: string): boolean {
  const q = filterQuery(qRaw)
  if (!q) return true
  if (has(db.name, q)) return true
  return db.tables.some(t => has(t, q))
}

/**
 * 连接是否留下。
 * 连接名不匹配，且其下没有匹配的库/表时，整条隐藏。
 */
export function connectionVisible(name: string, dbs: FilterNode[], looseTables: string[], qRaw: string): boolean {
  const q = filterQuery(qRaw)
  if (!q) return true
  if (has(name, q)) return true
  if (dbs.some(d => databaseVisible(d, q))) return true
  return looseTables.some(t => has(t, q))
}

/** 表清单缓存键。无库名时就是连接 id，兼容既有 fixture。 */
export function tablesCacheKey(connId: string, database?: string): string {
  const db = (database ?? '').trim()
  return db ? `${connId}\u0001${db}` : connId
}

/** 列详情缓存键。无库名时保持 `连接/表`。 */
export function detailKey(connId: string, table: string, database?: string): string {
  const db = (database ?? '').trim()
  return db ? `${connId}/${encodeURIComponent(db)}/${table}` : `${connId}/${table}`
}

export function dbNodeKey(connId: string, database: string): string {
  return `d:${connId}/${encodeURIComponent(database)}`
}

export function tableNodeKey(connId: string, table: string, database?: string): string {
  const db = (database ?? '').trim()
  return db ? `t:${connId}/${encodeURIComponent(db)}/${table}` : `t:${connId}/${table}`
}
