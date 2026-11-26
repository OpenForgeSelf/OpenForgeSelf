/** 从路由查询里取出一个字符串。数组取第一项。 */
export function queryText(query: Record<string, unknown>, key: string): string {
  const value = query[key]
  if (Array.isArray(value)) return value[0] == null ? '' : String(value[0])
  return value == null ? '' : String(value)
}

/** 按顺序取第一个非空查询值。主参数在前，旧名在后。 */
export function queryTextFirst(query: Record<string, unknown>, keys: string[]): string {
  for (const key of keys) {
    const text = queryText(query, key).trim()
    if (text) return text
  }
  return ''
}

export function scriptExecutionFromQuery(query: Record<string, unknown>): string {
  return queryTextFirst(query, ['id', 'executionId'])
}

export function workflowExecutionFromQuery(query: Record<string, unknown>): string {
  return queryTextFirst(query, ['id', 'executionId'])
}

export function schedulerTaskFromQuery(query: Record<string, unknown>): string {
  return queryTextFirst(query, ['id', 'taskId'])
}

export function monitorPidFromQuery(query: Record<string, unknown>): number {
  const n = Number(queryTextFirst(query, ['id', 'pid']))
  return Number.isInteger(n) && n > 0 ? n : 0
}
