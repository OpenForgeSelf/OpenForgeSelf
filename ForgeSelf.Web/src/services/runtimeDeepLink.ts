/** 从路由查询里取出一个字符串。数组取第一项。 */
export function queryText(query: Record<string, unknown>, key: string): string {
  const value = query[key]
  if (Array.isArray(value)) return value[0] == null ? '' : String(value[0])
  return value == null ? '' : String(value)
}

export function scriptExecutionFromQuery(query: Record<string, unknown>): string {
  return queryText(query, 'executionId').trim()
}

export function workflowExecutionFromQuery(query: Record<string, unknown>): string {
  return queryText(query, 'executionId').trim()
}

export function schedulerTaskFromQuery(query: Record<string, unknown>): string {
  return queryText(query, 'taskId').trim()
}

export function monitorPidFromQuery(query: Record<string, unknown>): number {
  const n = Number(queryText(query, 'pid'))
  return Number.isInteger(n) && n > 0 ? n : 0
}
