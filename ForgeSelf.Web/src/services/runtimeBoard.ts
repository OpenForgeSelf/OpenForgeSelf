/**
 * 运行实况分类。只根据已经取回的数据决定每一行落在哪一层、点开会去哪。
 * 不发请求。同一条执行只保留一行。
 */

export type BoardLane = 'urgent' | 'wait' | 'resume' | 'running' | 'known' | 'scheduled'

export interface BoardLink {
  path: string
  query: Record<string, string>
}

export interface BoardCard {
  id: string
  lane: BoardLane
  source: string
  project: string
  title: string
  detail: string
  destructive: boolean
  once: boolean
  /** 同一会话的多条待批收在一起。 */
  sessionGroup?: string
  note?: string
  link: BoardLink
  secondaryLink?: BoardLink & { label: string }
  decide?:
    | { kind: 'agent'; id: number }
    | { kind: 'db'; id: string; destructive: boolean }
    | { kind: 'terminal'; id: string }
    | { kind: 'hub'; taskId: number; requestId: string }
    | { kind: 'resume'; url: string }
    | { kind: 'run-now'; url: string }
}

export interface LaneNotice {
  source: string
  kind: 'off' | 'denied' | 'error'
  message: string
}

export interface RuntimeInput {
  now?: number
  projects?: Array<{ id: number; name: string }>
  todos?: Array<{ id: number; agentTaskKey?: string; projectName?: string; title?: string }>
  agentApprovals?: FetchSlice<AgentApprovalRaw[]>
  dbApprovals?: FetchSlice<DbApprovalRaw[]>
  terminalApprovals?: FetchSlice<TerminalApprovalRaw[]>
  hubPermissions?: FetchSlice<HubPermissionRaw[]>
  overview?: FetchSlice<OverviewItemRaw[]>
  schedulerTasks?: FetchSlice<SchedulerTaskRaw[]>
  schedulerRunning?: FetchSlice<SchedulerRunningRaw[]>
  hubTasks?: FetchSlice<HubTaskRaw[]>
  terminalSessions?: FetchSlice<TerminalSessionRaw[]>
  runs?: FetchSlice<RunRaw[]>
  scripts?: FetchSlice<ScriptRaw[]>
  workflows?: FetchSlice<WorkflowRaw[]>
  scans?: FetchSlice<ScanRaw[]>
  browser?: FetchSlice<BrowserRaw | null>
}

export interface FetchSlice<T> {
  status: number
  data?: T
  message?: string
}

export interface AgentApprovalRaw {
  id: number
  sessionId: string
  toolName: string
  description?: string
  riskLevel?: string
  expiresAt?: string
}

export interface DbApprovalRaw {
  id: number | string
  sqlRedacted?: string
  destructive?: boolean
  status?: string
  connectionId?: string
}

export interface TerminalApprovalRaw {
  id: string
  sessionId: string
  command?: string
  risk?: string
  status?: string
  expiresAt?: string
}

export interface HubPermissionRaw {
  taskId: number
  requestId: string
  toolName?: string
  reason?: string
}

export interface OverviewItemRaw {
  id: string
  source: 'open' | 'planned'
  title: string
  status: string
  failureReason?: string | null
  startedAt?: string | null
  retryable?: boolean
  retryUrl?: string | null
  sourceRef: string
}

export interface SchedulerTaskRaw {
  id: number
  name: string
  status: number
  nextRunTime?: string | null
  failureCount?: number
}

export interface SchedulerRunningRaw {
  logId: number
  taskId: number
  name: string
  startTime?: string
}

export interface HubTaskRaw {
  id: number
  taskKey?: string
  prompt?: string
  status?: string
  agentName?: string
}

export interface TerminalSessionRaw {
  id: string
  title?: string
  cwd?: string
  blocks?: Array<{ status?: string; command?: string }>
}

export interface RunRaw {
  projectId: number
  projectName?: string
  commandName?: string
  pid: number
  origin?: string
}

export interface ScriptRaw {
  id: number | string
  scriptName?: string
  status?: string | number
}

export interface WorkflowRaw {
  id: number | string
  workflowName?: string
  status?: string | number
}

export interface ScanRaw {
  scanId: string
  rootPath?: string
  state?: string | number
}

export interface BrowserRaw {
  open?: boolean
  url?: string | null
}

const DESTRUCTIVE_TOOLS = new Set(['delete_file'])
const TWO_MIN = 2 * 60 * 1000
const KNOWN_CAP = 20

export interface ClassifiedBoard {
  cards: BoardCard[]
  notices: LaneNotice[]
}

export function classifyRuntime(input: RuntimeInput): ClassifiedBoard {
  const now = input.now ?? Date.now()
  const notices: LaneNotice[] = []
  const cards: BoardCard[] = []
  const todoByKey = new Map<string, { id: number; project: string; title: string }>()
  for (const todo of input.todos ?? []) {
    const key = (todo.agentTaskKey ?? '').trim()
    if (!key) continue
    todoByKey.set(key, {
      id: todo.id,
      project: todo.projectName?.trim() || '未归属',
      title: todo.title?.trim() || '',
    })
  }

  const approvalSessions = new Set<string>()

  pushSlice(notices, '智能体审批', input.agentApprovals)
  for (const row of input.agentApprovals?.data ?? []) {
    if (soon(row.expiresAt, now) === 'expired') continue
    approvalSessions.add(row.sessionId)
    const destructive = DESTRUCTIVE_TOOLS.has(row.toolName)
    const urgent = destructive || soon(row.expiresAt, now) === 'soon'
    cards.push({
      id: `agent-approval:${row.id}`,
      lane: urgent ? 'urgent' : 'wait',
      source: '智能体',
      project: projectForTodo(todoByKey, `session:${row.sessionId}`),
      title: row.description?.trim() || row.toolName,
      detail: `${row.toolName}${remainText(row.expiresAt, now)}`,
      destructive,
      once: false,
      sessionGroup: row.sessionId,
      link: { path: '/ai-agent', query: { session: row.sessionId } },
      decide: { kind: 'agent', id: row.id },
    })
  }

  pushSlice(notices, '数据库', input.dbApprovals)
  for (const row of input.dbApprovals?.data ?? []) {
    if (row.status && row.status !== 'pending') continue
    const id = String(row.id)
    cards.push({
      id: `db-approval:${id}`,
      lane: row.destructive ? 'urgent' : 'wait',
      source: '数据库',
      project: '未归属',
      title: row.sqlRedacted?.trim() || '数据库写操作',
      detail: row.destructive ? '会破坏数据，批准前要再确认' : '等待批准',
      destructive: !!row.destructive,
      once: false,
      link: { path: '/db-admin', query: { approvalId: id } },
      decide: { kind: 'db', id, destructive: !!row.destructive },
    })
  }

  pushSlice(notices, '终端', input.terminalApprovals)
  const terminalWaitSessions = new Set<string>()
  for (const row of input.terminalApprovals?.data ?? []) {
    if (row.status && row.status !== 'pending') continue
    if (soon(row.expiresAt, now) === 'expired') continue
    terminalWaitSessions.add(row.sessionId)
    const urgent = (row.risk ?? '').toLowerCase() === 'high' || soon(row.expiresAt, now) === 'soon'
    cards.push({
      id: `term-approval:${row.id}`,
      lane: urgent ? 'urgent' : 'wait',
      source: '终端',
      project: '未归属',
      title: row.command?.trim() || '终端命令',
      detail: `${row.risk || '待批'}${remainText(row.expiresAt, now)}`,
      destructive: false,
      once: false,
      sessionGroup: row.sessionId,
      note: '本次宿主运行期间有效',
      link: { path: '/smart-terminal', query: { sessionId: row.sessionId, approvalId: row.id } },
      decide: { kind: 'terminal', id: row.id },
    })
  }

  pushSlice(notices, '外部 Agent', input.hubPermissions)
  const hubWaiting = new Set<number>()
  for (const row of input.hubPermissions?.data ?? []) {
    hubWaiting.add(row.taskId)
    const task = (input.hubTasks?.data ?? []).find((t) => t.id === row.taskId)
    const todo = task?.taskKey ? todoByKey.get(task.taskKey) : undefined
    cards.push({
      id: `hub-permission:${row.taskId}:${row.requestId}`,
      lane: 'wait',
      source: '外部 Agent',
      project: todo?.project || '未归属',
      title: row.reason?.trim() || row.toolName || task?.prompt || '权限请求',
      detail: '同意只放行这一次',
      destructive: false,
      once: true,
      link: { path: '/agent-hub', query: { taskId: String(row.taskId) } },
      secondaryLink: todo
        ? { path: '/todo', query: { todoId: String(todo.id) }, label: '来自待办' }
        : undefined,
      decide: { kind: 'hub', taskId: row.taskId, requestId: row.requestId },
    })
  }

  pushSlice(notices, '智能体', input.overview)
  const known: BoardCard[] = []
  for (const row of input.overview?.data ?? []) {
    const todo = todoByKey.get(row.source === 'planned' ? `run:${row.sourceRef}` : '')
    const project = todo?.project || '未归属'
    const link: BoardLink = row.source === 'planned'
      ? { path: '/ai-agent', query: { runId: row.sourceRef } }
      : { path: '/ai-agent', query: { session: row.sourceRef } }
    const secondary = todo
      ? { path: '/todo', query: { todoId: String(todo.id) }, label: '来自待办' as const }
      : undefined
    if (row.status === 'stuck' && !approvalSessions.has(row.sourceRef) && row.retryUrl) {
      cards.push({
        id: `resume:${row.id}`,
        lane: 'resume',
        source: '智能体',
        project,
        title: row.title,
        detail: row.failureReason?.trim() || '回合暂停，可以继续',
        destructive: false,
        once: false,
        link,
        secondaryLink: secondary,
        decide: { kind: 'resume', url: row.retryUrl },
      })
      continue
    }
    if (row.status === 'running' || row.status === 'planning' || row.status === 'pending') {
      if (row.source === 'open' && approvalSessions.has(row.sourceRef)) continue
      cards.push({
        id: `run:${row.id}`,
        lane: 'running',
        source: '智能体',
        project,
        title: row.title,
        detail: statusText(row.status),
        destructive: false,
        once: false,
        link,
        secondaryLink: secondary,
      })
      continue
    }
    if (row.status === 'completed' || row.status === 'failed' || row.status === 'cancelled' || row.status === 'stuck') {
      known.push({
        id: `known:${row.id}`,
        lane: 'known',
        source: '智能体',
        project,
        title: row.title,
        detail: row.status === 'failed' ? (row.failureReason?.trim() || '失败') : statusText(row.status),
        destructive: row.status === 'failed',
        once: false,
        link,
        secondaryLink: secondary,
      })
    }
  }

  pushSlice(notices, '定时任务', input.schedulerRunning)
  const runningTaskIds = new Set<number>()
  const agentCoversScheduler = new Set<string>()
  for (const item of input.overview?.data ?? []) {
    if (item.status === 'running' || item.status === 'planning' || item.status === 'pending') {
      agentCoversScheduler.add(item.sourceRef)
    }
  }
  for (const row of input.schedulerRunning?.data ?? []) {
    runningTaskIds.add(row.taskId)
    if (agentCoversScheduler.has(`scheduler:${row.taskId}`)) continue
    cards.push({
      id: `sched-run:${row.logId}`,
      lane: 'running',
      source: '定时任务',
      project: '未归属',
      title: row.name,
      detail: '这次触发还在执行',
      destructive: false,
      once: false,
      link: { path: '/scheduler', query: { taskId: String(row.taskId) } },
    })
  }

  pushSlice(notices, '定时任务', input.schedulerTasks)
  for (const row of input.schedulerTasks?.data ?? []) {
    if (runningTaskIds.has(row.id)) continue
    if (row.status === 0) {
      cards.push({
        id: `sched:${row.id}`,
        lane: 'scheduled',
        source: '定时任务',
        project: '未归属',
        title: row.name,
        detail: row.nextRunTime ? `下次 ${row.nextRunTime}` : '等待触发',
        destructive: false,
        once: false,
        link: { path: '/scheduler', query: { taskId: String(row.id) } },
        decide: { kind: 'run-now', url: `/api/scheduler/tasks/${row.id}/run-now` },
      })
    } else if (row.status === 3) {
      known.push({
        id: `sched-known:${row.id}`,
        lane: 'known',
        source: '定时任务',
        project: '未归属',
        title: row.name,
        detail: `失败 ${row.failureCount ?? 0} 次`,
        destructive: true,
        once: false,
        link: { path: '/scheduler', query: { taskId: String(row.id) } },
      })
    }
  }

  pushSlice(notices, '外部 Agent', input.hubTasks)
  for (const row of input.hubTasks?.data ?? []) {
    if (hubWaiting.has(row.id)) continue
    const status = row.status ?? ''
    const todo = row.taskKey ? todoByKey.get(row.taskKey) : undefined
    const link = { path: '/agent-hub', query: { taskId: String(row.id) } }
    const secondary = todo
      ? { path: '/todo', query: { todoId: String(todo.id) }, label: '来自待办' as const }
      : undefined
    const title = firstLine(row.prompt) || row.taskKey || `任务 ${row.id}`
    if (status === 'AwaitingPermission' && !hubWaiting.has(row.id)) {
      cards.push({
        id: `hub-wait:${row.id}`,
        lane: 'wait',
        source: '外部 Agent',
        project: todo?.project || '未归属',
        title,
        detail: '等待权限，打开任务查看这次请求',
        destructive: false,
        once: true,
        link,
        secondaryLink: secondary,
      })
    } else if (status === 'Queued' || status === 'Running') {
      cards.push({
        id: `hub:${row.id}`,
        lane: 'running',
        source: '外部 Agent',
        project: todo?.project || '未归属',
        title,
        detail: status === 'Queued' ? '排队中' : '执行中',
        destructive: false,
        once: false,
        link,
        secondaryLink: secondary,
      })
    } else if (['Succeeded', 'Failed', 'Cancelled', 'Timeout', 'Interrupted'].includes(status)) {
      known.push({
        id: `hub-known:${row.id}`,
        lane: 'known',
        source: '外部 Agent',
        project: todo?.project || '未归属',
        title,
        detail: statusText(status),
        destructive: status === 'Failed' || status === 'Timeout',
        once: false,
        link,
        secondaryLink: secondary,
      })
    }
  }

  pushSlice(notices, '终端', input.terminalSessions)
  for (const row of input.terminalSessions?.data ?? []) {
    if (terminalWaitSessions.has(row.id)) continue
    const running = (row.blocks ?? []).find((b) => b.status === 'running')
    if (!running) continue
    cards.push({
      id: `term:${row.id}`,
      lane: 'running',
      source: '终端',
      project: '未归属',
      title: running.command?.trim() || row.title || '终端命令',
      detail: '命令还在跑',
      destructive: false,
      once: false,
      link: { path: '/smart-terminal', query: { sessionId: row.id } },
    })
  }

  pushSlice(notices, '工程台', input.runs)
  const seenPid = new Set<number>()
  for (const row of input.runs?.data ?? []) {
    if (!row.pid || seenPid.has(row.pid)) continue
    seenPid.add(row.pid)
    const named = (row.projectName ?? '').trim()
    const fromRegistry = input.projects?.find((p) => p.id === row.projectId)?.name
    const project = named || fromRegistry || '未归属'
    const hasProject = row.projectId > 0
    cards.push({
      id: `run-proc:${row.pid}`,
      lane: 'running',
      source: row.origin === 'Detected' ? '进程' : '工程台',
      project,
      title: row.commandName?.trim() || `进程 ${row.pid}`,
      detail: `PID ${row.pid}`,
      destructive: false,
      once: false,
      link: hasProject
        ? { path: '/sems', query: { projectId: String(row.projectId), runId: String(row.pid) } }
        : { path: '/system-monitor', query: { pid: String(row.pid) } },
    })
  }

  pushSlice(notices, '脚本', input.scripts)
  for (const row of input.scripts?.data ?? []) {
    cards.push({
      id: `script:${row.id}`,
      lane: 'running',
      source: '脚本',
      project: '未归属',
      title: row.scriptName?.trim() || `脚本 ${row.id}`,
      detail: '执行中',
      destructive: false,
      once: false,
      link: { path: '/script-runner', query: { executionId: String(row.id) } },
    })
  }

  pushSlice(notices, '工作流', input.workflows)
  for (const row of input.workflows?.data ?? []) {
    cards.push({
      id: `wf:${row.id}`,
      lane: 'running',
      source: '工作流',
      project: '未归属',
      title: row.workflowName?.trim() || `工作流 ${row.id}`,
      detail: '执行中',
      destructive: false,
      once: false,
      link: { path: '/workflows', query: { executionId: String(row.id) } },
    })
  }

  pushSlice(notices, '文件工具', input.scans)
  for (const row of input.scans?.data ?? []) {
    cards.push({
      id: `scan:${row.scanId}`,
      lane: 'running',
      source: '文件工具',
      project: '未归属',
      title: row.rootPath?.trim() || '目录扫描',
      detail: '扫描中',
      destructive: false,
      once: false,
      link: { path: '/file-tools', query: { scanId: row.scanId } },
    })
  }

  pushSlice(notices, '浏览器', input.browser)
  if (input.browser?.data?.open) {
    cards.push({
      id: 'browser',
      lane: 'running',
      source: '浏览器',
      project: '未归属',
      title: input.browser.data.url?.trim() || '浏览器已打开',
      detail: '单实例',
      destructive: false,
      once: false,
      link: { path: '/browser-use', query: {} },
    })
  }

  known.sort((a, b) => (a.destructive === b.destructive ? 0 : a.destructive ? -1 : 1))
  cards.push(...known.slice(0, KNOWN_CAP))
  return { cards, notices: dedupeNotices(notices) }
}

export function attentionCount(cards: BoardCard[]): number {
  return cards.filter((c) => c.lane === 'urgent' || c.lane === 'wait').length
}

function projectForTodo(map: Map<string, { id: number; project: string; title: string }>, key: string): string {
  return map.get(key)?.project || '未归属'
}

function soon(iso: string | undefined, now: number): 'ok' | 'soon' | 'expired' | 'none' {
  if (!iso) return 'none'
  const t = Date.parse(iso)
  if (Number.isNaN(t)) return 'none'
  const left = t - now
  if (left <= 0) return 'expired'
  if (left < TWO_MIN) return 'soon'
  return 'ok'
}

function remainText(iso: string | undefined, now: number): string {
  const kind = soon(iso, now)
  if (kind === 'none' || kind === 'expired') return ''
  const left = Date.parse(iso!) - now
  const min = Math.max(1, Math.ceil(left / 60000))
  return ` · 剩余 ${min} 分`
}

function statusText(status: string): string {
  switch (status) {
    case 'running':
    case 'Running':
      return '执行中'
    case 'planning':
      return '规划中'
    case 'pending':
    case 'Queued':
      return '排队中'
    case 'completed':
    case 'Succeeded':
      return '完成'
    case 'failed':
    case 'Failed':
    case 'Timeout':
      return '失败'
    case 'cancelled':
    case 'Cancelled':
      return '已取消'
    case 'stuck':
      return '卡住'
    case 'Interrupted':
      return '已中断'
    default:
      return status
  }
}

function firstLine(text?: string): string {
  if (!text) return ''
  const line = text.split('\n', 1)[0].trim()
  return line.length > 60 ? `${line.slice(0, 60)}…` : line
}

function pushSlice<T>(notices: LaneNotice[], source: string, slice?: FetchSlice<T>): void {
  if (!slice || slice.status === 0 || (slice.status >= 200 && slice.status < 300)) return
  if (slice.status === 404) {
    notices.push({ source, kind: 'off', message: `${source} 未启用` })
    return
  }
  if (slice.status === 401 || slice.status === 403) {
    notices.push({ source, kind: 'denied', message: `${source} 需要访问密钥` })
    return
  }
  notices.push({ source, kind: 'error', message: slice.message || `${source} 读取失败(${slice.status})` })
}

function dedupeNotices(notices: LaneNotice[]): LaneNotice[] {
  const seen = new Set<string>()
  return notices.filter((n) => {
    const key = `${n.source}:${n.kind}:${n.message}`
    if (seen.has(key)) return false
    seen.add(key)
    return true
  })
}
