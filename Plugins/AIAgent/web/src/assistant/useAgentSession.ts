import { computed, onUnmounted, reactive, ref, watch } from 'vue'
import * as api from './api'
import { applyEvent, defaultCatalog, emptyThreadState, extractCodeFences, isSettledStatus, primaryKind, type ThreadState } from './session.logic'
import { subscribeSessionEvents } from './sse'
import type {
  AgentAssistantLayout,
  AgentAttachment,
  AgentCatalogAgent,
  AgentCatalogModel,
  AgentEmbed,
  AgentEmbedContext,
  AgentMode,
  AgentSessionSummary,
  AgentToolScope,
  AgentWorkspacePanelId,
} from './types'

export interface UseAgentSessionOptions {
  layout: () => AgentAssistantLayout
  context: () => AgentEmbedContext | undefined
  toolScope: () => AgentToolScope | undefined
  surfaceKey?: () => string | undefined
}

export function useAgentSession(opts: UseAgentSessionOptions) {
  const sessions = ref<AgentSessionSummary[]>([])
  const activeId = ref<string | null>(null)
  const thread = reactive<ThreadState>(emptyThreadState())
  const title = ref('新会话')
  const mode = ref<AgentMode>('open')
  const agentId = ref('')
  const modelId = ref('')
  const draft = ref('')
  const attachments = ref<AgentAttachment[]>([])
  const attachOpen = ref(false)
  const agents = ref<AgentCatalogAgent[]>([])
  const models = ref<AgentCatalogModel[]>([])
  const error = ref('')
  const workspacePanel = ref<AgentWorkspacePanelId>('catalog')
  const workspaceDetail = ref<Record<string, unknown> | null>(null)
  const memoryDraft = ref('')
  const panelLayer = ref<'chat' | 'sessions' | 'workspace'>('chat')
  const loading = ref(false)
  const revising = ref(false)
  const reviseNote = ref('')
  const stuckInput = ref('')

  let stopEvents: (() => void) | null = null

  const embed = (): AgentEmbed => {
    const ctx = opts.context()
    const pluginId = ctx?.pluginId || 'ai-agent'
    return {
      surface: opts.layout(),
      pluginId,
      surfaceKey: opts.surfaceKey?.() ?? ctx?.surfaceKey,
      context: ctx,
      toolScope: opts.toolScope(),
    }
  }

  const listQuery = () => {
    const e = embed()
    return { pluginId: e.pluginId, surfaceKey: e.surfaceKey }
  }

  function report(e: unknown) {
    error.value = e instanceof Error ? e.message : String(e)
  }

  async function refreshSessions() {
    try {
      const rows = await api.listSessions(listQuery())
      sessions.value = Array.isArray(rows) ? rows : []
    } catch (e) {
      report(e)
      sessions.value = []
    }
  }

  async function loadCatalogs() {
    try {
      const [a, m] = await Promise.all([api.listAgents(), api.listModels()])
      agents.value = Array.isArray(a) ? a : []
      models.value = Array.isArray(m) ? m : []
      if (!agentId.value && agents.value[0]) agentId.value = agents.value[0].id
      if (!modelId.value && models.value[0]) modelId.value = models.value[0].id
    } catch (e) {
      report(e)
    }
  }

  function startEvents(id: string, after = 0) {
    stopEvents?.()
    stopEvents = subscribeSessionEvents({
      sessionId: id,
      after,
      onEvent: (ev) => {
        const next = applyEvent(thread, ev)
        Object.assign(thread, next)
        if (next.autoOpenPanel) {
          workspacePanel.value = next.autoOpenPanel
          if (opts.layout() === 'panel') panelLayer.value = 'workspace'
          void loadWorkspace(next.autoOpenPanel)
        }
        if (ev.type === 'session.status' || ev.type.startsWith('run.') || ev.type === 'plan.proposed' || ev.type === 'approval.required') {
          void refreshSessions()
        }
        if (ev.type === 'run.cancelled' || ev.type === 'run.completed' || ev.type === 'run.failed') {
          const panel = workspacePanel.value
          void loadWorkspace(panel === 'catalog' ? undefined : panel)
        }
        if (ev.type === 'message.completed') {
          const payload = ev.payload as { text?: string; messageId?: string; id?: string } | undefined
          const mid = String(payload?.messageId ?? payload?.id ?? `asst-${ev.seq}`)
          const item = thread.items.find((it) => it.kind === 'assistant' && it.id === mid)
          const text = String(payload?.text || (item && item.kind === 'assistant' ? item.text : '') || '')
          void collectSnippets(id, text)
        }
        const cur = sessions.value.find((s) => s.id === id)
        if (cur) cur.status = thread.status
      },
      onError: (err) => {
        if (!error.value) error.value = err.message
      },
    })
  }

  async function openSession(id: string) {
    loading.value = true
    error.value = ''
    activeId.value = id
    Object.assign(thread, emptyThreadState())
    workspacePanel.value = 'catalog'
    workspaceDetail.value = null
    try {
      const detail = await api.getSession(id)
      title.value = String(detail.title ?? '会话')
      if (detail.mode === 'open' || detail.mode === 'planned') mode.value = detail.mode
      if (typeof detail.agentId === 'string') agentId.value = detail.agentId
      if (typeof detail.modelId === 'string') modelId.value = detail.modelId
      if (typeof detail.status === 'string') thread.status = detail.status as ThreadState['status']
      if (Array.isArray(detail.messages)) {
        for (const msg of detail.messages as Array<Record<string, unknown>>) {
          const role = String(msg.role ?? msg.kind ?? '')
          const text = String(msg.text ?? msg.content ?? '')
          const mid = String(msg.id ?? `msg-${thread.items.length}`)
          if (role === 'user') thread.items.push({ kind: 'user', id: mid, text })
          else if (role === 'assistant' || role === 'agent') thread.items.push({ kind: 'assistant', id: mid, text })
        }
      }
      if (detail.workspace && typeof detail.workspace === 'object') {
        const items = (detail.workspace as { items?: ThreadState['catalog'] }).items
        if (items) thread.catalog = items
      } else {
        try {
          const ws = await api.getWorkspace(id)
          if (Array.isArray((ws as { items?: unknown }).items)) {
            thread.catalog = (ws as { items: ThreadState['catalog'] }).items
          }
        } catch {
          if (!thread.catalog.length) thread.catalog = defaultCatalog()
        }
      }
      startEvents(id, thread.lastSeq)
    } catch (e) {
      report(e)
    } finally {
      loading.value = false
    }
  }

  async function newSession(options?: { keepInput?: boolean }) {
    error.value = ''
    try {
      const created = await api.createSession({
        mode: mode.value,
        agentId: agentId.value || undefined,
        modelId: modelId.value || undefined,
        embed: embed(),
      })
      sessions.value = [created, ...sessions.value.filter((s) => s.id !== created.id)]
      title.value = created.title || '新会话'
      mode.value = created.mode
      activeId.value = created.id
      Object.assign(thread, emptyThreadState(created.status))
      workspacePanel.value = 'catalog'
      workspaceDetail.value = null
      if (!options?.keepInput) {
        draft.value = ''
        attachments.value = []
      }
      if (opts.layout() === 'panel') panelLayer.value = 'chat'
      startEvents(created.id, 0)
    } catch (e) {
      report(e)
    }
  }

  async function send() {
    const text = draft.value.trim()
    if (!text || thread.status === 'running') return
    error.value = ''
    const pins = attachments.value.slice()
    if (!activeId.value) await newSession({ keepInput: true })
    const id = activeId.value
    if (!id) return
    const userId = `user-${Date.now()}`
    thread.items.push({ kind: 'user', id: userId, text })
    draft.value = ''
    try {
      const res = await api.sendMessage(id, { text, attachments: pins, embed: embed() })
      thread.runId = res.runId
      // 关键词 mock 可能在 send 返回前就把终态事件推完，不能再把状态改回 running
      if (!isSettledStatus(thread.status)) {
        thread.status = mode.value === 'planned' ? 'awaiting_plan' : 'running'
      }
    } catch (e) {
      report(e)
      draft.value = text
    }
  }

  async function stop() {
    if (!activeId.value || !thread.runId) return
    try {
      await api.stopRun(activeId.value, thread.runId)
    } catch (e) {
      report(e)
    }
  }

  async function confirm() {
    if (!activeId.value) return
    try {
      await api.confirmPlan(activeId.value)
    } catch (e) {
      report(e)
    }
  }

  async function revise() {
    if (!activeId.value) return
    try {
      await api.revisePlan(activeId.value, { note: reviseNote.value })
      revising.value = false
      reviseNote.value = ''
    } catch (e) {
      report(e)
    }
  }

  async function decide(approvalId: string, approved: boolean) {
    if (!activeId.value) return
    try {
      await api.decideApproval(activeId.value, approvalId, approved)
    } catch (e) {
      report(e)
    }
  }

  async function actStuck(action: 'continue' | 'skip' | 'override' | 'retry' | 'cancel') {
    if (!activeId.value || !thread.runId) return
    try {
      await api.intervene(activeId.value, thread.runId, {
        action,
        input: action === 'continue' ? stuckInput.value : undefined,
        stepId: thread.stuck?.stepId,
      })
      stuckInput.value = ''
    } catch (e) {
      report(e)
    }
  }


  const savedSnippetKeys = new Set<string>()

  async function collectSnippets(sessionId: string, text: string) {
    const fences = extractCodeFences(text)
    if (!fences.length) return
    for (const fence of fences) {
      const key = `${fence.language}\n${fence.content}`
      if (savedSnippetKeys.has(key)) continue
      savedSnippetKeys.add(key)
      const title = fence.language || '片段'
      try {
        await api.addSnippet(sessionId, { title, content: fence.content })
      } catch (e) {
        savedSnippetKeys.delete(key)
        report(e)
      }
    }
    if (workspacePanel.value === 'snippets' || workspacePanel.value === 'catalog') {
      void loadWorkspace(workspacePanel.value === 'catalog' ? undefined : 'snippets')
    }
  }

  function textOf(v: unknown): string {

    return typeof v === 'string' ? v.trim() : ''
  }

  function memoryItemsContent(data: Record<string, unknown> | null): string {
    const items = Array.isArray(data?.items) ? (data!.items as Array<Record<string, unknown>>) : []
    return items
      .map((it) => textOf(it.content) || textOf(it.title) || textOf(it.summary))
      .filter(Boolean)
      .join('\n')
  }

  function normalizePanel(panel: Exclude<AgentWorkspacePanelId, 'catalog'>, data: Record<string, unknown>) {
    const out: Record<string, unknown> = { ...data }
    if (panel === 'records') {
      const run = (data.run ?? null) as Record<string, unknown> | null
      if (run) {
        if (out.round == null) out.round = run.turn ?? run.Turn
        if (out.turnMax == null) out.turnMax = run.turnMax ?? run.TurnMax
        if (out.runStatus == null) out.runStatus = run.status ?? run.Status
        if (out.stuckReason == null) out.stuckReason = run.stuckReason ?? run.StuckReason
        if (out.failureReason == null) out.failureReason = run.failureReason ?? run.FailureReason
        const plan = (run.plan ?? run.Plan ?? null) as Record<string, unknown> | null
        const steps = run.steps ?? plan?.steps
        if (!out.steps && steps) out.steps = steps
      }
    }
    if (panel === 'project') {
      const ctx = (data.context ?? {}) as Record<string, unknown>
      if (!out.name) out.name = ctx.title ?? ctx.projectId ?? ctx.Title ?? ctx.ProjectId
      if (!out.cwd) out.cwd = ctx.cwd ?? ctx.Cwd
      if (!out.summary) {
        const bits = [ctx.objectType ?? ctx.ObjectType, ctx.objectId ?? ctx.ObjectId].map((x) => textOf(x)).filter(Boolean)
        out.summary = bits.join(' ')
      }
    }
    if (panel === 'skills') {
      if (!out.enabled && Array.isArray(data.attachments)) out.enabled = data.attachments
      if (!out.disabled) out.disabled = []
    }
    if (panel === 'workflow' && !out.id && data.workflowId) out.id = data.workflowId
    return out
  }

  async function loadWorkspace(panel?: Exclude<AgentWorkspacePanelId, 'catalog'>) {
    if (!activeId.value) {
      workspaceDetail.value = null
      return
    }
    try {
      const data = await api.getWorkspace(activeId.value, panel)
      if (!panel && Array.isArray((data as { items?: unknown }).items)) {
        thread.catalog = (data as { items: ThreadState['catalog'] }).items
      }
      if (panel) {
        const rec = (data ?? {}) as Record<string, unknown>
        workspaceDetail.value = normalizePanel(panel, rec)
        if (panel === 'memory' && !memoryDraft.value) memoryDraft.value = memoryItemsContent(workspaceDetail.value)
      }
    } catch (e) {
      if (panel) report(e)
    }
  }

  async function openPanel(id: AgentWorkspacePanelId) {
    workspacePanel.value = id
    if (opts.layout() === 'panel') panelLayer.value = 'workspace'
    if (id === 'catalog') {
      workspaceDetail.value = null
      await loadWorkspace()
      return
    }
    await loadWorkspace(id)
  }

  async function writeMemory() {
    if (!activeId.value) return
    const content = (memoryDraft.value || memoryItemsContent(workspaceDetail.value)).trim()
    if (!content) return
    try {
      await api.writeMemory(activeId.value, { content })
      memoryDraft.value = ''
      await loadWorkspace('memory')
    } catch (e) {
      report(e)
    }
  }

  async function commitDesign() {
    if (!activeId.value) return
    const content = String(workspaceDetail.value?.preview ?? workspaceDetail.value?.content ?? '').trim()
    if (!content) return
    try {
      await api.commitDesign(activeId.value, { content })
      await loadWorkspace('design')
    } catch (e) {
      report(e)
    }
  }

  async function dispatchTodo(todoId: string) {
    if (!activeId.value) return
    try {
      await api.dispatchTodo(activeId.value, todoId)
      await loadWorkspace('todo')
    } catch (e) {
      report(e)
    }
  }

  async function changeMode(next: AgentMode) {
    if (next === mode.value) return
    if (thread.status === 'running') return
    mode.value = next
    if (activeId.value) {
      try {
        await api.patchSession(activeId.value, { mode: next })
      } catch (e) {
        report(e)
      }
    }
  }

  const kind = computed(() =>
    primaryKind({
      status: thread.status,
      hasPlan: !!thread.plan,
      approvalPending: !!thread.approval && !thread.approval.resolved,
    })
  )

  const running = computed(() => thread.status === 'running')
  const empty = computed(() => thread.items.length === 0)
  const railOpen = computed(() => workspacePanel.value !== 'catalog')

  watch(
    () => [opts.context()?.pluginId, opts.surfaceKey?.(), opts.context()?.surfaceKey],
    () => {
      void refreshSessions()
    }
  )

  onUnmounted(() => stopEvents?.())

  return {
    sessions,
    activeId,
    thread,
    title,
    mode,
    agentId,
    modelId,
    draft,
    attachments,
    attachOpen,
    agents,
    models,
    error,
    workspacePanel,
    workspaceDetail,
    memoryDraft,
    panelLayer,
    loading,
    revising,
    reviseNote,
    stuckInput,
    kind,
    running,
    empty,
    railOpen,
    refreshSessions,
    loadCatalogs,
    openSession,
    newSession,
    send,
    stop,
    confirm,
    revise,
    decide,
    actStuck,
    openPanel,
    loadWorkspace,
    writeMemory,
    commitDesign,
    dispatchTodo,
    changeMode,
  }
}
