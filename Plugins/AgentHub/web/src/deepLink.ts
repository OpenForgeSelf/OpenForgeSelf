/** 运行实况打开外部任务。主参数 id，旧名 taskId 仍接受。 */
export function taskIdFromSearch(search: string): number {
  const raw = search.startsWith('?') ? search.slice(1) : search
  const params = new URLSearchParams(raw)
  const n = Number(params.get('id') || params.get('taskId') || '')
  return Number.isInteger(n) && n > 0 ? n : 0
}
