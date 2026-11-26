/** 运行实况「来自待办」。主参数 id，旧名 todoId 仍接受。 */
export function todoIdFromSearch(search: string): number {
  const raw = search.startsWith('?') ? search.slice(1) : search
  const params = new URLSearchParams(raw)
  const n = Number(params.get('id') || params.get('todoId') || '')
  return Number.isInteger(n) && n > 0 ? n : 0
}
