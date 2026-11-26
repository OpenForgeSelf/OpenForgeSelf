/** 宿主接口常把实体包在 `{ data }` 里。页面直接展开外层时，名称和状态都是空的。 */

export interface PagedBody {
  items?: unknown[]
  total?: number
  page?: number
  pageSize?: number
}

/** 分页字段缺失时用这次拿到的行数，避免把可选数字传给必须是数字的返回值。 */
export function pageMeta(body: PagedBody | unknown[] | null | undefined, rows: unknown[]): { total: number; page: number; pageSize: number } {
  const record = body && !Array.isArray(body) ? body : undefined
  return {
    total: typeof record?.total === 'number' ? record.total : rows.length,
    page: typeof record?.page === 'number' ? record.page : 1,
    pageSize: typeof record?.pageSize === 'number' ? record.pageSize : rows.length,
  }
}

export function unwrapApiBody<T = unknown>(body: unknown): T {
  if (body && typeof body === 'object' && 'data' in body) {
    const data = (body as { data?: T }).data
    if (data != null) return data
  }
  return body as T
}

const SCRIPT_STATUS = ['pending', 'running', 'completed', 'failed', 'cancelled', 'timeout'] as const

/** 脚本执行状态：接口给数字（Pending=0），详情页按英文字符串取文案。 */
export function scriptStatusFromApi(status: unknown): string | undefined {
  if (typeof status === 'string' && status) return status
  if (typeof status === 'number' && status >= 0 && status < SCRIPT_STATUS.length) return SCRIPT_STATUS[status]
  return undefined
}

const WORKFLOW_STATUS: Record<number, string> = {
  2: 'running',
  3: 'paused',
  4: 'completed',
  5: 'failed',
  6: 'cancelled',
}

/** 工作流执行状态：Running=2。未识别的数字留给页面显示「未知」。 */
export function workflowStatusFromApi(status: unknown): string | undefined {
  if (typeof status === 'string' && status) return status
  if (typeof status === 'number') return WORKFLOW_STATUS[status]
  return undefined
}

/** 年份过小的占位时间（未结束的执行）不当作已经结束。 */
export function dateOrUndefined(value: unknown): Date | undefined {
  if (typeof value !== 'string' || !value) return undefined
  const parsed = new Date(value)
  if (Number.isNaN(parsed.getTime()) || parsed.getUTCFullYear() < 1970) return undefined
  return parsed
}
