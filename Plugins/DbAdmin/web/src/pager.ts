/** 结果格分页（服务端分页；前端只算边界） */
export const PAGE_SIZES = [50, 100, 500] as const
/** 后端 UiMaxPageSize = 500（ResultLimiter）；history/audit 同上限 */
export const MAX_PAGE_SIZE = 500

/** pageSize 收敛到 [1, 500]；非法值回退 50 */
export function clampPageSize(n: number | undefined): number {
  const v = Math.floor(Number(n))
  if (!Number.isFinite(v) || v <= 0) return 50
  return Math.min(v, MAX_PAGE_SIZE)
}

export function pageCount(total: number | undefined, pageSize: number): number | undefined {
  if (total === undefined || total === null || total < 0) return undefined
  return Math.max(1, Math.ceil(total / Math.max(1, pageSize)))
}

export function canPrev(page: number): boolean {
  return page > 1
}

/** total 未知时：本页满 = 可能还有下一页 */
export function canNext(page: number, pageSize: number, total: number | undefined, rowsOnPage: number): boolean {
  const pc = pageCount(total, pageSize)
  if (pc !== undefined) return page < pc
  return rowsOnPage >= pageSize
}

/** 越界回退到合法页 */
export function clampPage(page: number, pageSize: number, total: number | undefined): number {
  const pc = pageCount(total, pageSize)
  const p = Math.max(1, Math.floor(page) || 1)
  return pc === undefined ? p : Math.min(p, pc)
}

/** 「第 a–b 行 / 共 n」 */
export function rangeLabel(page: number, pageSize: number, rowsOnPage: number, total?: number): string {
  if (rowsOnPage === 0) return total ? `共 ${total} 行` : '0 行'
  const a = (page - 1) * pageSize + 1
  const b = a + rowsOnPage - 1
  return total !== undefined ? `第 ${a}–${b} 行 / 共 ${total}` : `第 ${a}–${b} 行`
}
