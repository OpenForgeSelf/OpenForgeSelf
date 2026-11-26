/** 运行实况打开工程台：/sems?projectId=&runId= */
export function semsFocusFromSearch(search: string): { projectId: number; runId: number } {
  const raw = search.startsWith('?') ? search.slice(1) : search
  const params = new URLSearchParams(raw)
  const projectId = Number(params.get('projectId') || '')
  const runId = Number(params.get('runId') || '')
  return {
    projectId: Number.isInteger(projectId) && projectId > 0 ? projectId : 0,
    runId: Number.isInteger(runId) && runId > 0 ? runId : 0,
  }
}
