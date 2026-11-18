import type {
  AgentApproval,
  AgentEventEnvelope,
  AgentSessionStatus,
  AgentStuck,
  AgentThreadItem,
  AgentToolCall,
  AgentWorkspaceItemStatus,
  AgentWorkspacePanelId,
} from './types'

export const WORKSPACE_PANEL_DEFS: Array<{
  id: Exclude<AgentWorkspacePanelId, 'catalog'>
  name: string
  description: string
  emptyStatus: string
}> = [
  { id: 'plan', name: '计划', description: '先看步骤，确认后再执行', emptyStatus: '无计划' },
  { id: 'records', name: '执行记录', description: '本轮步骤、工具结果与轮次', emptyStatus: '暂无' },
  { id: 'workflow', name: '工作流', description: '关联工作流的执行与控制', emptyStatus: '未启动' },
  { id: 'memory', name: '记忆', description: '本轮检索到的条目，可写回', emptyStatus: '暂无' },
  { id: 'design', name: '设计', description: '组件与令牌预览，默认不写库', emptyStatus: '无预览' },
  { id: 'suggest', name: '建议', description: '按最近习惯推荐下一步', emptyStatus: '暂无' },
  { id: 'project', name: '项目', description: '当前工作目录、档案与命令', emptyStatus: '未绑定' },
  { id: 'todo', name: '待办', description: '可下发给智能体的任务', emptyStatus: '暂无' },
  { id: 'skills', name: '技能', description: '本智能体已启用的技能', emptyStatus: '未启用' },
  { id: 'tools', name: '工具', description: '本轮可见的宿主与 MCP 工具', emptyStatus: '可见范围' },
  { id: 'snippets', name: '代码片段', description: '本轮产出或收藏的片段', emptyStatus: '暂无' },
]

export const ATTACH_KINDS = [
  { kind: 'skill', name: '技能' },
  { kind: 'prompt', name: '提示词' },
  { kind: 'memory', name: '记忆' },
  { kind: 'mcp', name: 'MCP' },
  { kind: 'project', name: '项目' },
  { kind: 'image', name: '图片' },
] as const

export function statusLabel(status: AgentSessionStatus): string {
  switch (status) {
    case 'idle':
      return '刚刚'
    case 'running':
      return '进行中'
    case 'awaiting_plan':
      return '计划待确认'
    case 'awaiting_approval':
      return '待审批'
    case 'stuck':
      return '已卡住'
    case 'completed':
      return '已完成'
    case 'failed':
      return '失败'
    case 'cancelled':
      return '已取消'
    default:
      return status
  }
}

export type PrimaryKind = 'new' | 'confirm-plan' | 'approve'

export function primaryKind(input: {
  status: AgentSessionStatus
  hasPlan: boolean
  approvalPending: boolean
}): PrimaryKind {
  if (input.approvalPending || input.status === 'awaiting_approval') return 'approve'
  if (input.hasPlan && input.status === 'awaiting_plan') return 'confirm-plan'
  return 'new'
}

export function defaultCatalog(): AgentWorkspaceItemStatus[] {
  return WORKSPACE_PANEL_DEFS.map((d) => ({
    id: d.id,
    name: d.name,
    description: d.description,
    statusText: d.emptyStatus,
    tone: 'default',
  }))
}

export function mergeCatalog(
  current: AgentWorkspaceItemStatus[],
  incoming: AgentWorkspaceItemStatus[] | undefined
): AgentWorkspaceItemStatus[] {
  const base = current.length ? current : defaultCatalog()
  if (!incoming?.length) return base
  const map = new Map(incoming.map((i) => [i.id, i]))
  return base.map((item) => {
    const next = map.get(item.id)
    return next ? { ...item, ...next } : item
  })
}

export interface ThreadState {
  items: AgentThreadItem[]
  lastSeq: number
  status: AgentSessionStatus
  runId: string | null
  plan: { goal?: string; steps?: Array<{ id?: string; title: string; detail?: string; status?: string }>; constraints?: string[] } | null
  approval: AgentApproval | null
  stuck: AgentStuck | null
  catalog: AgentWorkspaceItemStatus[]
  autoOpenPanel: Exclude<AgentWorkspacePanelId, 'catalog'> | null
}

export function emptyThreadState(status: AgentSessionStatus = 'idle'): ThreadState {
  return {
    items: [],
    lastSeq: 0,
    status,
    runId: null,
    plan: null,
    approval: null,
    stuck: null,
    catalog: defaultCatalog(),
    autoOpenPanel: null,
  }
}

function str(v: unknown, fallback = ''): string {
  return typeof v === 'string' ? v : fallback
}

function upsertAssistantDelta(items: AgentThreadItem[], id: string, chunk: string): AgentThreadItem[] {
  const next = items.slice()
  const idx = next.findIndex((it) => it.kind === 'assistant' && it.id === id)
  if (idx >= 0 && next[idx].kind === 'assistant') {
    const cur = next[idx]
    next[idx] = { ...cur, text: cur.text + chunk, streaming: true }
  } else {
    next.push({ kind: 'assistant', id, text: chunk, streaming: true })
  }
  return next
}

export function applyEvent(state: ThreadState, ev: AgentEventEnvelope): ThreadState {
  const s: ThreadState = {
    ...state,
    items: state.items.slice(),
    catalog: state.catalog.slice(),
    lastSeq: ev.seq > state.lastSeq ? ev.seq : state.lastSeq,
    autoOpenPanel: null,
  }
  if (ev.runId) s.runId = ev.runId
  const p = ev.payload ?? {}

  switch (ev.type) {
    case 'session.status': {
      const status = str(p.status) as AgentSessionStatus
      if (status) s.status = status
      break
    }
    case 'message.delta': {
      const id = str(p.messageId, str(p.id, `asst-${ev.seq}`))
      s.items = upsertAssistantDelta(s.items, id, str(p.text, str(p.delta)))
      if (s.status === 'idle') s.status = 'running'
      break
    }
    case 'message.completed': {
      const id = str(p.messageId, str(p.id, `asst-${ev.seq}`))
      const text = str(p.text)
      const idx = s.items.findIndex((it) => it.kind === 'assistant' && it.id === id)
      if (idx >= 0 && s.items[idx].kind === 'assistant') {
        const cur = s.items[idx]
        s.items[idx] = { ...cur, text: text || cur.text, streaming: false }
      } else if (text) {
        s.items.push({ kind: 'assistant', id, text, streaming: false })
      }
      break
    }
    case 'tool.started':
    case 'tool.completed': {
      const id = str(p.toolCallId, str(p.id, `tool-${ev.seq}`))
      const tool: AgentToolCall = {
        id,
        name: str(p.name, '工具'),
        summary: str(p.summary, ev.type === 'tool.completed' ? '已完成' : '进行中'),
        detail: typeof p.detail === 'string' ? p.detail : undefined,
        status: ev.type === 'tool.completed' ? (str(p.status) === 'failed' ? 'failed' : 'done') : 'running',
      }
      const idx = s.items.findIndex((it) => it.kind === 'tool' && it.id === id)
      const item: AgentThreadItem = { kind: 'tool', id, tool }
      if (idx >= 0) s.items[idx] = item
      else s.items.push(item)
      break
    }
    case 'plan.proposed':
    case 'plan.updated': {
      s.plan = {
        goal: str(p.goal, undefined as unknown as string) || undefined,
        steps: Array.isArray(p.steps) ? (p.steps as ThreadState['plan'] extends infer X | null ? NonNullable<X>['steps'] : never) : [],
        constraints: Array.isArray(p.constraints) ? (p.constraints as string[]) : undefined,
      }
      if (ev.type === 'plan.proposed') {
        s.status = 'awaiting_plan'
        s.autoOpenPanel = 'plan'
      }
      break
    }
    case 'step.started':
    case 'step.progress':
    case 'step.completed':
    case 'step.failed': {
      if (s.plan?.steps && p.stepId) {
        s.plan = {
          ...s.plan,
          steps: s.plan.steps.map((st) =>
            st.id === p.stepId || st.title === p.title
              ? { ...st, status: ev.type === 'step.failed' ? 'failed' : ev.type === 'step.completed' ? 'done' : 'running' }
              : st
          ),
        }
      }
      if (ev.type !== 'step.failed' && s.status !== 'awaiting_approval' && s.status !== 'stuck') {
        s.status = 'running'
      }
      break
    }
    case 'approval.required': {
      const approval: AgentApproval = {
        id: str(p.approvalId, str(p.id, `appr-${ev.seq}`)),
        summary: str(p.summary, str(p.action, '需要确认')),
        risk: str(p.risk) || undefined,
        action: str(p.action) || undefined,
      }
      s.approval = approval
      s.status = 'awaiting_approval'
      s.items.push({ kind: 'approval', id: approval.id, approval })
      break
    }
    case 'approval.resolved': {
      const id = str(p.approvalId, str(p.id, s.approval?.id ?? ''))
      const approved = Boolean(p.approved)
      if (s.approval && s.approval.id === id) {
        s.approval = { ...s.approval, resolved: true, approved }
      }
      s.items = s.items.map((it) =>
        it.kind === 'approval' && it.id === id
          ? { ...it, approval: { ...it.approval, resolved: true, approved } }
          : it
      )
      if (s.status === 'awaiting_approval') s.status = approved ? 'running' : s.status
      break
    }
    case 'run.stuck': {
      const stuck: AgentStuck = {
        runId: ev.runId ?? str(p.runId),
        reason: str(p.reason, '无法继续'),
        allowedActions: Array.isArray(p.allowedActions)
          ? (p.allowedActions as AgentStuck['allowedActions'])
          : ['continue', 'skip', 'cancel'],
        stepId: str(p.stepId) || undefined,
      }
      s.stuck = stuck
      s.status = 'stuck'
      s.items.push({ kind: 'stuck', id: `stuck-${ev.seq}`, stuck })
      s.autoOpenPanel = 'records'
      break
    }
    case 'run.failed':
      s.status = 'failed'
      break
    case 'run.completed':
      s.status = 'completed'
      s.stuck = null
      break
    case 'run.cancelled':
      s.status = 'cancelled'
      break
    case 'workspace.updated': {
      const items = Array.isArray(p.items) ? (p.items as AgentWorkspaceItemStatus[]) : undefined
      s.catalog = mergeCatalog(s.catalog, items)
      break
    }
    default:
      break
  }
  return s
}

export const MODE_LABEL: Record<'open' | 'planned', string> = {
  open: '开放任务',
  planned: '计划任务',
}
