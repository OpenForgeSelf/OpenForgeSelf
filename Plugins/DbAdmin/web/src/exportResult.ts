/**
 * §8.2 导出：多页 CSV 拼接、当前页 JSON、当前页 INSERT。
 * 下载与翻页循环留在视图。
 */
import { csvFileName, toCsv } from './exportCsv'

export const EXPORT_ALL_MAX_PAGES = 50

export function concatRows(pages: unknown[][][]): unknown[][] {
  const out: unknown[][] = []
  for (const page of pages) for (const row of page) out.push(row)
  return out
}

/** 多页行拼成一份 CSV（表头只一次） */
export function csvFromPages(columns: { name: string }[], pages: unknown[][][]): string {
  return toCsv(columns, concatRows(pages))
}

/** 成功拿到这一页之后是否停止。总行数未知时，本页不满即停。 */
export function shouldStopAfterPage(
  page: number,
  pageSize: number,
  rowsOnPage: number,
  total: number | undefined,
  maxPages = EXPORT_ALL_MAX_PAGES,
): boolean {
  if (page >= maxPages) return true
  if (typeof total === 'number' && total >= 0 && page * pageSize >= total) return true
  if (total === undefined && rowsOnPage < pageSize) return true
  return false
}

/** 对象数组。NULL / 缺列 → null。 */
export function toJson(columns: { name: string }[], rows: unknown[][]): string {
  const arr = rows.map(row => {
    const obj: Record<string, unknown> = {}
    columns.forEach((col, i) => {
      const v = row[i]
      obj[col.name] = v === undefined ? null : v
    })
    return obj
  })
  return JSON.stringify(arr)
}

function sqlIdent(name: string): string {
  return /^[A-Za-z_][A-Za-z0-9_]*$/.test(name) ? name : `"${name.replace(/"/g, '""')}"`
}

function sqlLiteral(v: unknown): string {
  if (v === null || v === undefined) return 'NULL'
  if (typeof v === 'number') return Number.isFinite(v) ? String(v) : 'NULL'
  if (typeof v === 'boolean') return v ? 'TRUE' : 'FALSE'
  if (typeof v === 'string') return `'${v.replace(/'/g, "''")}'`
  return `'${JSON.stringify(v).replace(/'/g, "''")}'`
}

/** 每行一条 INSERT。字符串单引号加倍。 */
export function toInsert(table: string, columns: { name: string }[], rows: unknown[][]): string {
  const cols = columns.map(c => sqlIdent(c.name)).join(', ')
  const into = sqlIdent(table)
  const lines = rows.map(row => {
    const values = columns.map((_, i) => sqlLiteral(row[i])).join(', ')
    return `INSERT INTO ${into} (${cols}) VALUES (${values});`
  })
  return lines.join('\n') + (lines.length ? '\n' : '')
}

/** 与 CSV 同一命名，扩展名替换。page 可以是 `all`。 */
export function exportFileName(
  base: string,
  resultIndex: number,
  page: number | 'all',
  ext: string,
  now: Date = new Date(),
): string {
  return csvFileName(base, resultIndex, page, now).replace(/\.csv$/i, `.${ext}`)
}
