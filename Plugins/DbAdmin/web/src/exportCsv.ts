/**
 * 结果导出 CSV（FR-21 / BR-21 / AC-23）。
 * RFC 4180：含逗号/双引号/换行的字段用双引号包裹并双写引号；NULL 写 `NULL`；
 * 文件内容带 UTF-8 BOM（Excel 直接双击不乱码）。只导出当前结果标签的当前页。
 */

type Column = { name: string }

/** 单元格 → CSV 文本（NULL 与界面一致写 `NULL`） */
export function csvCell(v: unknown): string {
  if (v === null || v === undefined) return 'NULL'
  if (typeof v === 'object') return JSON.stringify(v)
  return String(v)
}

/** 单字段转义（RFC 4180） */
export function csvField(text: string): string {
  if (/[",\r\n]/.test(text)) return `"${text.replace(/"/g, '""')}"`
  return text
}

/** 当前页 → CSV 文本（含表头行，行分隔 CRLF，末尾换行） */
export function toCsv(columns: Column[], rows: unknown[][]): string {
  const lines: string[] = []
  lines.push(columns.map(c => csvField(c.name)).join(','))
  for (const r of rows) lines.push(r.map(v => csvField(csvCell(v))).join(','))
  return lines.join('\r\n') + '\r\n'
}

const BOM = '\uFEFF'

/** Blob 用文本：带 UTF-8 BOM */
export function csvWithBom(text: string): string {
  return BOM + text
}

/** 整行复制文本（FR-21 / AC-22）：制表符分隔，NULL 与界面一致写 `NULL` */
export function rowText(row: unknown[]): string {
  return row.map(csvCell).join('\t')
}

function two(n: number): string {
  return n < 10 ? `0${n}` : String(n)
}

/** 发行时间码 yyMMddHHmm（与项目版本串口径一致） */
export function timeCode(d: Date): string {
  return `${two(d.getFullYear() % 100)}${two(d.getMonth() + 1)}${two(d.getDate())}${two(d.getHours())}${two(d.getMinutes())}`
}

/** 文件名非法字符 → `_`，空白折叠 */
export function safeBaseName(base: string): string {
  const t = base.trim().replace(/[\\/:*?"<>|\r\n]+/g, '_').replace(/\s+/g, '_')
  return t || 'query'
}

/** `<表名或query>-r<结果序号>-p<页码>-<yyMMddHHmm>.csv`（序号/页码均 1 起） */
export function csvFileName(base: string, resultIndex: number, page: number, now: Date = new Date()): string {
  return `${safeBaseName(base)}-r${Math.max(1, Math.floor(resultIndex))}-p${Math.max(1, Math.floor(page))}-${timeCode(now)}.csv`
}
