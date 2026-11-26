/** 运行实况打开外部任务：/agent-hub?taskId= */
export function taskIdFromSearch(search: string): number {
  const raw = search.startsWith('?') ? search.slice(1) : search
  const n = Number(new URLSearchParams(raw).get('taskId') || '')
  return Number.isInteger(n) && n > 0 ? n : 0
}
