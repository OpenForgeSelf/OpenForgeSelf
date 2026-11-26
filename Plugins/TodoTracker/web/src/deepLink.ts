/** 运行实况「来自待办」：/todo?todoId= */
export function todoIdFromSearch(search: string): number {
  const raw = search.startsWith('?') ? search.slice(1) : search
  const n = Number(new URLSearchParams(raw).get('todoId') || '')
  return Number.isInteger(n) && n > 0 ? n : 0
}
