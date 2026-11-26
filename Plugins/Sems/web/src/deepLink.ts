/** 运行实况打开工程台：主参数 id 是项目，第二参数 pid 是这次进程。旧名 projectId、runId 仍接受。 */
export function semsFocusFromSearch(search: string): { projectId: number; pid: number } {
  const raw = search.startsWith('?') ? search.slice(1) : search
  const params = new URLSearchParams(raw)
  return {
    projectId: positiveInt(params, ['id', 'projectId']),
    pid: positiveInt(params, ['pid', 'runId']),
  }
}

function positiveInt(params: URLSearchParams, keys: string[]): number {
  for (const key of keys) {
    const n = Number(params.get(key) || '')
    if (Number.isInteger(n) && n > 0) return n
  }
  return 0
}
