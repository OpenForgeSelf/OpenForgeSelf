/**
 * 运行实况取数。每一路单独记状态码，一路失败不挡住其余。
 * 重新扫描才调用 POST /api/runs/check；平时只读 GET /api/runs。
 */
import { authFetch } from './authFetch'
import type {
  AgentApprovalRaw,
  BrowserRaw,
  DbApprovalRaw,
  FetchSlice,
  HubPermissionRaw,
  HubTaskRaw,
  OverviewItemRaw,
  RunRaw,
  RuntimeInput,
  ScanRaw,
  SchedulerRunningRaw,
  SchedulerTaskRaw,
  ScriptRaw,
  TerminalApprovalRaw,
  TerminalSessionRaw,
  WorkflowRaw,
} from './runtimeBoard'

export interface TodoLinkRaw {
  id: number
  agentTaskKey?: string
  projectName?: string
  title?: string
}

async function readMessage(res: Response): Promise<string> {
  try {
    const body = (await res.json()) as { message?: string; error?: string; title?: string }
    return body.message || body.error || body.title || ''
  } catch {
    return ''
  }
}

async function take(path: string, init?: RequestInit): Promise<{ status: number; json?: unknown; message?: string }> {
  try {
    const res = await authFetch(path, init)
    if (!res.ok) return { status: res.status, message: (await readMessage(res)) || `请求失败(${res.status})` }
    return { status: res.status, json: await res.json() }
  } catch (e) {
    return { status: 599, message: e instanceof Error ? e.message : '网络错误' }
  }
}

function asRecord(value: unknown): Record<string, unknown> | null {
  return value && typeof value === 'object' ? (value as Record<string, unknown>) : null
}

function asList(json: unknown): unknown[] {
  if (Array.isArray(json)) return json
  const root = asRecord(json)
  if (!root) return []
  if (Array.isArray(root.data)) return root.data
  if (Array.isArray(root.items)) return root.items
  if (Array.isArray(root.runs)) return root.runs
  const data = asRecord(root.data)
  if (data && Array.isArray(data.items)) return data.items
  return []
}

function str(row: Record<string, unknown>, key: string): string {
  const v = row[key]
  return v == null ? '' : String(v)
}

function num(row: Record<string, unknown>, key: string): number {
  const v = row[key]
  const n = typeof v === 'number' ? v : Number(v)
  return Number.isFinite(n) ? n : 0
}

function sliceOf<T>(taken: { status: number; json?: unknown; message?: string }, map: (json: unknown) => T): FetchSlice<T> {
  if (taken.json === undefined) return { status: taken.status, message: taken.message }
  return { status: taken.status, data: map(taken.json) }
}

export async function fetchRuntimeInput(now = Date.now()): Promise<RuntimeInput> {
  const [
    overview,
    schedulerTasks,
    schedulerRunning,
    agentApprovals,
    dbApprovals,
    terminalApprovals,
    hubTasks,
    terminalSessions,
    runs,
    scripts,
    workflows,
    scans,
    browser,
    todos,
  ] = await Promise.all([
    take('/api/ai-agent/tasks-overview?limit=30'),
    take('/api/scheduler/tasks?page=1&pageSize=100'),
    take('/api/scheduler/running'),
    take('/api/ai-agent/approvals/pending'),
    take('/api/db-admin/approvals?status=pending'),
    take('/api/smart-terminal/approvals?status=pending'),
    take('/api/agent-hub/tasks?limit=50'),
    take('/api/smart-terminal/sessions'),
    take('/api/runs'),
    take('/api/script-runner/executions?status=Running&pageSize=50'),
    take('/api/workflows/executions?status=Running&pageSize=50'),
    take('/api/filetools/folders/scans'),
    take('/api/browser-use/session'),
    take('/api/todos?status=pending&page=1&pageSize=100'),
  ])

  const hubTaskRows = asList(hubTasks.json).map(mapHubTask)
  const waiting = hubTaskRows.filter((t) => t.status === 'AwaitingPermission')
  const permissionSlices = await Promise.all(
    waiting.map(async (task) => {
      const taken = await take(`/api/agent-hub/tasks/${task.id}/permissions`)
      return asList(taken.json).map((row) => mapHubPermission(task.id, row))
    }),
  )

  return {
    now,
    todos: asList(todos.json).map(mapTodo),
    overview: sliceOf(overview, (json) => {
      const root = asRecord(json)
      const items = Array.isArray(root?.items) ? root.items : []
      return items.map(mapOverview)
    }),
    schedulerTasks: sliceOf(schedulerTasks, (json) => asList(json).map(mapSchedulerTask)),
    schedulerRunning: sliceOf(schedulerRunning, (json) => asList(json).map(mapSchedulerRunning)),
    agentApprovals: sliceOf(agentApprovals, (json) => asList(json).map(mapAgentApproval)),
    dbApprovals: sliceOf(dbApprovals, (json) => asList(json).map(mapDbApproval)),
    terminalApprovals: sliceOf(terminalApprovals, (json) => asList(json).map(mapTerminalApproval)),
    hubTasks: sliceOf(hubTasks, () => hubTaskRows),
    hubPermissions: hubTasks.status >= 200 && hubTasks.status < 300
      ? { status: 200, data: permissionSlices.flat() }
      : { status: hubTasks.status, message: hubTasks.message },
    terminalSessions: sliceOf(terminalSessions, (json) => asList(json).map(mapTerminalSession)),
    runs: sliceOf(runs, (json) => asList(json).map(mapRun)),
    scripts: sliceOf(scripts, (json) => asList(json).map(mapScript)),
    workflows: sliceOf(workflows, (json) => asList(json).map(mapWorkflow)),
    scans: sliceOf(scans, (json) => asList(json).map(mapScan)),
    browser: sliceOf(browser, mapBrowser),
  }
}

/** 只查找已登记项目目录下的进程。不启动、不停止。 */
export async function rescanProjectProcesses(): Promise<{ ok: boolean; message: string }> {
  const taken = await take('/api/runs/check', { method: 'POST' })
  if (taken.status >= 200 && taken.status < 300) return { ok: true, message: '' }
  return { ok: false, message: taken.message || '重新扫描失败' }
}

export async function postJson(path: string, body: unknown): Promise<{ ok: boolean; message: string }> {
  const taken = await take(path, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  if (taken.status >= 200 && taken.status < 300) return { ok: true, message: '' }
  return { ok: false, message: taken.message || `请求失败(${taken.status})` }
}

function mapTodo(row: unknown): TodoLinkRaw {
  const r = asRecord(row) ?? {}
  return {
    id: num(r, 'id'),
    agentTaskKey: str(r, 'agentTaskKey'),
    projectName: str(r, 'projectName'),
    title: str(r, 'title'),
  }
}

function mapOverview(row: unknown): OverviewItemRaw {
  const r = asRecord(row) ?? {}
  const source = str(r, 'source') === 'planned' ? 'planned' : 'open'
  return {
    id: str(r, 'id'),
    source,
    title: str(r, 'title'),
    status: str(r, 'status'),
    failureReason: str(r, 'failureReason') || null,
    startedAt: str(r, 'startedAt') || null,
    retryable: r.retryable === true,
    retryUrl: str(r, 'retryUrl') || null,
    sourceRef: str(r, 'sourceRef'),
  }
}

function mapSchedulerTask(row: unknown): SchedulerTaskRaw {
  const r = asRecord(row) ?? {}
  return {
    id: num(r, 'id'),
    name: str(r, 'name'),
    status: num(r, 'status'),
    nextRunTime: str(r, 'nextRunTime') || null,
    failureCount: num(r, 'failureCount'),
  }
}

function mapSchedulerRunning(row: unknown): SchedulerRunningRaw {
  const r = asRecord(row) ?? {}
  return { logId: num(r, 'logId'), taskId: num(r, 'taskId'), name: str(r, 'name'), startTime: str(r, 'startTime') }
}

function mapAgentApproval(row: unknown): AgentApprovalRaw {
  const r = asRecord(row) ?? {}
  return {
    id: num(r, 'id'),
    sessionId: str(r, 'sessionId'),
    toolName: str(r, 'toolName'),
    description: str(r, 'description'),
    riskLevel: str(r, 'riskLevel'),
    expiresAt: str(r, 'expiresAt'),
  }
}

function mapDbApproval(row: unknown): DbApprovalRaw {
  const r = asRecord(row) ?? {}
  return {
    id: str(r, 'id'),
    sqlRedacted: str(r, 'sqlRedacted'),
    destructive: r.destructive === true,
    status: str(r, 'status') || 'pending',
    connectionId: str(r, 'connectionId'),
  }
}

function mapTerminalApproval(row: unknown): TerminalApprovalRaw {
  const r = asRecord(row) ?? {}
  return {
    id: str(r, 'id'),
    sessionId: str(r, 'sessionId'),
    command: str(r, 'command'),
    risk: str(r, 'risk'),
    status: str(r, 'status') || 'pending',
    expiresAt: str(r, 'expiresAt'),
  }
}

function mapHubTask(row: unknown): HubTaskRaw {
  const r = asRecord(row) ?? {}
  return {
    id: num(r, 'id'),
    taskKey: str(r, 'taskKey'),
    prompt: str(r, 'prompt'),
    status: str(r, 'status'),
    agentName: str(r, 'agentName'),
  }
}

function mapHubPermission(taskId: number, row: unknown): HubPermissionRaw {
  const r = asRecord(row) ?? {}
  return {
    taskId,
    requestId: str(r, 'requestId'),
    toolName: str(r, 'kind'),
    reason: str(r, 'detail'),
  }
}

function mapTerminalSession(row: unknown): TerminalSessionRaw {
  const r = asRecord(row) ?? {}
  const blocks = Array.isArray(r.blocks) ? r.blocks : []
  return {
    id: str(r, 'id'),
    title: str(r, 'title'),
    cwd: str(r, 'cwd'),
    blocks: blocks.map((b) => {
      const block = asRecord(b) ?? {}
      return { status: str(block, 'status'), command: str(block, 'command') }
    }),
  }
}

function mapRun(row: unknown): RunRaw {
  const r = asRecord(row) ?? {}
  return {
    projectId: num(r, 'projectId'),
    projectName: str(r, 'projectName'),
    commandName: str(r, 'commandName'),
    pid: num(r, 'pid'),
    origin: str(r, 'origin'),
  }
}

function mapScript(row: unknown): ScriptRaw {
  const r = asRecord(row) ?? {}
  return { id: str(r, 'id'), scriptName: str(r, 'scriptName') || str(r, 'name'), status: str(r, 'status') }
}

function mapWorkflow(row: unknown): WorkflowRaw {
  const r = asRecord(row) ?? {}
  return { id: str(r, 'id'), workflowName: str(r, 'workflowName') || str(r, 'name'), status: str(r, 'status') }
}

function mapScan(row: unknown): ScanRaw {
  const r = asRecord(row) ?? {}
  return { scanId: str(r, 'scanId'), rootPath: str(r, 'rootPath'), state: str(r, 'state') }
}

function mapBrowser(json: unknown): BrowserRaw | null {
  const root = asRecord(json)
  const data = asRecord(root?.data) ?? root
  if (!data) return null
  return { open: data.open === true, url: str(data, 'url') || null }
}
