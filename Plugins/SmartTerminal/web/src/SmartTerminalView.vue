<script setup lang="ts">
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue'
import { fetchPluginVersion, smartTerminalApi } from './api'
import { ApiError } from './http'
import { parseLaunchQuery, scrubLaunchParamsFromUrl } from './launchFromQuery'
import { redactAuditText } from './secretDisplay'
import {
  buildSnapshot,
  clearSnapshot,
  loadSnapshot,
  removeTabFromSnapshot,
  restoredTabToSession,
  saveSnapshot,
  type RestoredConnectionSummary,
} from './sessionRestore'
import {
  isUrlPasswordDisabled,
  needsUrlPasswordAck,
  persistUrlPasswordRiskAck,
  resolveUrlPasswordMode,
  type UrlPasswordMode,
} from './urlPasswordPolicy'
import type {
  AuditEvent,
  CommandRecord,
  LaunchTerminalRequest,
  OutputChunk,
  PendingApproval,
  PermissionMode,
  ShellProfile,
  SmartTerminalFixture,
  TerminalSession,
} from './types'

const props = defineProps<{ fixture?: SmartTerminalFixture }>()

const PLUGIN_ID = 'smart-terminal'
const isDemo = computed(() => !!props.fixture)

const version = ref('加载中')
const shells = ref<ShellProfile[]>([])
const sessions = ref<TerminalSession[]>([])
const activeId = ref<string>('')
const chunks = ref<OutputChunk[]>([])
const chunksBySession = ref<Record<string, OutputChunk[]>>({})
const nextIndex = ref(0)
const nextIndexBySession = ref<Record<string, number>>({})
const commands = ref<CommandRecord[]>([])
const commandsBySession = ref<Record<string, CommandRecord[]>>({})
const approvals = ref<PendingApproval[]>([])
const auditItems = ref<AuditEvent[]>([])
const auditKindFilter = ref('')
const auditQuery = ref('')
const globalMode = ref<PermissionMode>('AskWrite')

const draftTitle = ref('新标签')
const draftShell = ref('')
const draftCwd = ref('')
const commandInput = ref('')
const busy = ref('')
const errorText = ref('')
const customUnsupported = ref(false)
const unauthorized = ref(false)
const showAudit = ref(false)
const showApproval = ref(false)
const activeApproval = ref<PendingApproval | null>(null)
const launchBanner = ref('')
const launchWarning = ref('')
const reconnectBanner = ref('')
const renamingId = ref('')
const renameDraft = ref('')
const showNewTabMenu = ref(false)
const showPasswordAck = ref(false)
const passwordAckChecked = ref(false)
const passwordAckMode = ref<UrlPasswordMode>('always-ack')
const showInteractivePassword = ref(false)
const interactivePassword = ref('')
const interactivePasswordReveal = ref(false)
const pendingLaunch = ref<{
  request: LaunchTerminalRequest
  summary: string
  passwordInUrl: boolean
  needsInteractivePassword?: boolean
} | null>(null)

/** 连接摘要（无密码）；用于 S-R 恢复与标签元信息 */
const connectionsBySession = ref<Record<string, RestoredConnectionSummary>>({})
/** 仅内存：关标签即焚；永不写入 sessionStorage / localStorage */
const sessionPasswords = ref<Record<string, string>>({})
let restorePersistTimer: ReturnType<typeof setTimeout> | null = null

const consoleEl = ref<HTMLElement | null>(null)
let pollTimer: ReturnType<typeof setInterval> | null = null

const activeSession = computed(() => sessions.value.find(s => s.sessionId === activeId.value) ?? null)

const modeLabel = computed(() => {
  const m = normalizeMode(activeSession.value?.permissionMode ?? globalMode.value)
  return m === 'Auto' ? '自动执行' : m === 'AskHighRisk' ? '仅询高危' : '写询问'
})

const modeBadgeClass = computed(() => {
  const m = normalizeMode(activeSession.value?.permissionMode ?? globalMode.value)
  return m === 'Auto' ? 'badge-auto' : m === 'AskHighRisk' ? 'badge-high' : 'badge-write'
})

const auditKinds = computed(() => {
  const set = new Set<string>()
  for (const a of auditItems.value) if (a.kind) set.add(a.kind)
  return [...set].sort()
})

const filteredAudit = computed(() => {
  const kind = auditKindFilter.value.trim().toLowerCase()
  const q = auditQuery.value.trim().toLowerCase()
  return auditItems.value.filter(a => {
    if (kind && (a.kind || '').toLowerCase() !== kind) return false
    if (!q) return true
    const hay = [a.kind, a.decision, a.matchedRule, a.commandRedacted, a.detail, a.agentId, a.sessionId]
      .filter(Boolean)
      .join(' ')
      .toLowerCase()
    return hay.includes(q)
  }).map(a => ({
    ...a,
    commandRedacted: redactAuditText(a.commandRedacted),
    detail: redactAuditText(a.detail),
    matchedRule: redactAuditText(a.matchedRule),
    decision: redactAuditText(a.decision),
  }))
})

/** 浮层互斥：风险 ack > 交互密码 > 审批 > 审计 */
const canShowApprovalOverlay = computed(
  () => showApproval.value && !!activeApproval.value && !showPasswordAck.value && !showInteractivePassword.value,
)
const canShowAuditDrawer = computed(
  () => showAudit.value && !showPasswordAck.value && !showInteractivePassword.value,
)

const openSessions = computed(() => sessions.value.filter(s => s.status !== 'closed'))

function normalizeMode(m: PermissionMode | number | string | undefined): PermissionMode {
  if (m === 0 || m === 'Auto') return 'Auto'
  if (m === 2 || m === 'AskHighRisk') return 'AskHighRisk'
  return 'AskWrite'
}

function riskLabel(r: number | string | undefined): string {
  if (r === 3 || r === 'Deny') return '拒绝'
  if (r === 2 || r === 'HighRisk') return '高危'
  if (r === 1 || r === 'Write') return '写入'
  return '只读'
}

function tabMeta(s: TerminalSession): string {
  if (s.launchType === 'ssh' || s.launchType === 'wsl-ssh') {
    return `${s.remoteUser || '?'}@${s.remoteHost || '?'}`
  }
  return s.shellDisplayName
}

function applyFixture(f: SmartTerminalFixture) {
  version.value = f.version ?? '0.0.0-ct'
  shells.value = f.shells ?? []
  sessions.value = f.sessions ?? []
  activeId.value = f.activeId ?? ''
  chunksBySession.value = f.chunksBySession ?? {}
  commandsBySession.value = f.commandsBySession ?? {}
  if (f.chunks) chunks.value = f.chunks
  else chunks.value = activeId.value ? chunksBySession.value[activeId.value] ?? [] : []
  if (f.commands) commands.value = f.commands
  else commands.value = activeId.value ? commandsBySession.value[activeId.value] ?? [] : []
  globalMode.value = f.globalMode ?? 'AskWrite'
  approvals.value = f.approvals ?? []
  showApproval.value = !!f.showApproval
  activeApproval.value = f.activeApproval ?? (f.showApproval ? f.approvals?.[0] ?? null : null)
  showAudit.value = !!f.showAudit
  auditItems.value = f.auditItems ?? []
  auditKindFilter.value = f.auditKindFilter ?? ''
  auditQuery.value = f.auditQuery ?? ''
  errorText.value = f.errorText ?? ''
  customUnsupported.value = !!f.customUnsupported
    || /本里程碑不支持\s*custom|custom:<|不会静默当作 local/i.test(f.errorText ?? '')
  unauthorized.value = !!f.unauthorized
  reconnectBanner.value = f.reconnectBanner ?? ''
  draftTitle.value = f.draftTitle ?? '新标签'
  draftShell.value = f.draftShell ?? shells.value.find(s => s.available)?.id ?? ''
  draftCwd.value = f.draftCwd ?? ''
  commandInput.value = f.commandInput ?? ''
  launchBanner.value = f.launchBanner ?? ''
  launchWarning.value = f.launchWarning ?? ''
  renamingId.value = f.renamingId ?? ''
  if (renamingId.value) {
    renameDraft.value = sessions.value.find(s => s.sessionId === renamingId.value)?.title ?? ''
  }
  showNewTabMenu.value = !!f.showNewTabMenu
  showPasswordAck.value = !!f.showPasswordAck
  passwordAckChecked.value = !!f.passwordAckChecked
  passwordAckMode.value = f.passwordAckMode ?? 'always-ack'
  showInteractivePassword.value = !!f.showInteractivePassword
  interactivePassword.value = ''
  interactivePasswordReveal.value = false
  if (f.showPasswordAck) {
    pendingLaunch.value = {
      request: { profile: 'ssh', type: 'ssh', host: '10.0.0.1', user: 'root', auth: 'sshpass' },
      summary: f.pendingLaunchSummary ?? 'profile=ssh · host=10.0.0.1 · user=root · auth=sshpass · password=***REDACTED***',
      passwordInUrl: true,
    }
    launchBanner.value = f.launchBanner ?? `URL 启动：${pendingLaunch.value.summary}`
    launchWarning.value = f.launchWarning ?? '地址栏曾含密码，已从 URL 擦除。继续前请确认风险。'
  }
  if (f.showInteractivePassword) {
    pendingLaunch.value = {
      request: {
        profile: f.interactivePasswordProfile ?? 'ssh',
        type: (f.interactivePasswordProfile as any) === 'wsl-ssh' ? 'wsl-ssh' : 'ssh',
        host: f.interactivePasswordHost ?? '10.0.0.1',
        user: f.interactivePasswordUser ?? 'root',
        port: f.interactivePasswordPort ?? 22,
        auth: 'interactive',
      },
      summary: f.pendingLaunchSummary
        ?? `profile=${f.interactivePasswordProfile ?? 'ssh'} · host=${f.interactivePasswordHost ?? '10.0.0.1'} · user=${f.interactivePasswordUser ?? 'root'} · auth=interactive`,
      passwordInUrl: false,
      needsInteractivePassword: true,
    }
    launchBanner.value = f.launchBanner ?? `URL 启动：${pendingLaunch.value.summary}`
    launchWarning.value = f.launchWarning ?? '未提供密码：请先在遮罩输入（确认后再建标签；关标签即焚）。强烈推荐改用 auth=key 密钥登录。'
  }
}

function noteError(e: unknown) {
  const anyErr = e as { status?: number; message?: string }
  unauthorized.value = anyErr?.status === 401
  errorText.value = anyErr?.message ? `操作失败：${anyErr.message}` : '操作失败：未知错误'
}

function schedulePersistRestore() {
  if (isDemo.value) return
  if (restorePersistTimer) clearTimeout(restorePersistTimer)
  restorePersistTimer = setTimeout(() => {
    restorePersistTimer = null
    persistRestoreNow()
  }, 200)
}

function persistRestoreNow() {
  if (isDemo.value) return
  cacheActiveBuckets()
  const open = sessions.value.filter(s => s.status !== 'closed')
  if (!open.length) {
    clearSnapshot()
    return
  }
  saveSnapshot(
    buildSnapshot({
      activeId: activeId.value,
      sessions: open,
      connectionsBySession: connectionsBySession.value,
      chunksBySession: chunksBySession.value,
      commandsBySession: commandsBySession.value,
      nextIndexBySession: nextIndexBySession.value,
    }),
  )
}

function rememberConnection(sessionId: string, request: LaunchTerminalRequest, launchType?: string) {
  const summary: RestoredConnectionSummary = {
    profile: request.profile || request.type || launchType || 'local',
    launchType: launchType || (request.type as string) || request.profile,
    host: request.host,
    port: request.port,
    user: request.user,
    cwd: request.cwd,
    shellId: request.shellId,
    title: request.title,
    authMethod: request.auth === 'key' ? 'key' : request.auth === 'sshpass' ? 'sshpass' : request.auth || 'interactive',
  }
  connectionsBySession.value = { ...connectionsBySession.value, [sessionId]: summary }
}

function burnSessionSecrets(sessionId: string) {
  if (sessionPasswords.value[sessionId]) {
    const next = { ...sessionPasswords.value }
    delete next[sessionId]
    sessionPasswords.value = next
  }
  removeTabFromSnapshot(sessionId)
}

/** 应用 S-R 快照：填充输出缓冲与连接摘要；sessions 仍以服务端为准并在其后合并 */
function applyRestoreSnapshot() {
  const snap = loadSnapshot()
  if (!snap) return null
  chunksBySession.value = { ...(snap.chunksBySession || {}) }
  commandsBySession.value = { ...(snap.commandsBySession || {}) }
  nextIndexBySession.value = { ...(snap.nextIndexBySession || {}) }
  const conns: Record<string, RestoredConnectionSummary> = {}
  for (const t of snap.tabs) {
    if (t.connection) conns[t.sessionId] = { ...t.connection }
  }
  connectionsBySession.value = { ...connectionsBySession.value, ...conns }
  return snap
}

function cacheActiveBuckets() {
  if (!activeId.value) return
  chunksBySession.value = { ...chunksBySession.value, [activeId.value]: [...chunks.value] }
  commandsBySession.value = { ...commandsBySession.value, [activeId.value]: [...commands.value] }
  nextIndexBySession.value = { ...nextIndexBySession.value, [activeId.value]: nextIndex.value }
}

async function refreshMeta() {
  if (isDemo.value) return
  const [v, sh, mode, list] = await Promise.all([
    fetchPluginVersion(PLUGIN_ID),
    smartTerminalApi.listShells(),
    smartTerminalApi.getPermissionMode(),
    smartTerminalApi.listSessions(),
  ])
  version.value = v
  shells.value = sh ?? []
  if (mode?.mode === 'Auto' || mode?.mode === 'AskWrite' || mode?.mode === 'AskHighRisk') {
    globalMode.value = mode.mode
  }
  sessions.value = (list ?? []).filter(s => s.status !== 'closed')
  if (!draftShell.value) {
    const first = shells.value.find(s => s.available)
    draftShell.value = first?.id ?? ''
  }
  if (!activeId.value && sessions.value.length) {
    activeId.value = sessions.value[0].sessionId
  }
}

async function refreshApprovals() {
  if (isDemo.value) return
  approvals.value = (await smartTerminalApi.listApprovals(activeId.value || undefined)) ?? []
  // 浮层顺序：风险 ack / 交互密码优先于审批
  if (approvals.value.length && !showApproval.value && !showPasswordAck.value && !showInteractivePassword.value) {
    activeApproval.value = approvals.value[0]
    showApproval.value = true
  }
}

async function selectSession(id: string) {
  if (id === activeId.value) return
  cacheActiveBuckets()
  activeId.value = id
  if (isDemo.value) {
    chunks.value = chunksBySession.value[id] ?? []
    commands.value = commandsBySession.value[id] ?? []
    evaluateReconnect(sessions.value.find(s => s.sessionId === id) ?? null)
    return
  }
  chunks.value = []
  nextIndex.value = nextIndexBySession.value[id] ?? 0
  if (nextIndex.value === 0) {
    await Promise.all([pollOutput(true), loadCommands(), refreshApprovals()])
  } else {
    chunks.value = chunksBySession.value[id] ?? []
    await Promise.all([pollOutput(false), loadCommands(), refreshApprovals()])
  }
  try {
    const fresh = await smartTerminalApi.getSession(id)
    if (fresh) {
      const idx = sessions.value.findIndex(s => s.sessionId === id)
      if (idx >= 0) sessions.value[idx] = { ...sessions.value[idx], ...fresh }
      evaluateReconnect(fresh)
    } else {
      evaluateReconnect(sessions.value.find(s => s.sessionId === id) ?? null)
    }
  } catch {
    evaluateReconnect(sessions.value.find(s => s.sessionId === id) ?? null)
  }
}

async function createSession() {
  if (busy.value) return
  if (isDemo.value) {
    const id = `sess-demo-${Date.now()}`
    const shell = shells.value.find(s => s.id === draftShell.value) ?? shells.value.find(s => s.available)
    sessions.value = [
      ...sessions.value,
      {
        sessionId: id,
        title: draftTitle.value || '新标签',
        shellId: shell?.id ?? 'bash',
        shellDisplayName: shell?.displayName ?? 'Bash',
        cwd: draftCwd.value || '/workspace',
        permissionMode: globalMode.value,
        status: 'idle',
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
        commandCount: 0,
        launchType: 'local',
      },
    ]
    showNewTabMenu.value = false
    await selectSession(id)
    return
  }
  busy.value = 'create'
  errorText.value = ''
  try {
    const session = await smartTerminalApi.createSession({
      title: draftTitle.value || undefined,
      shellId: draftShell.value || undefined,
      cwd: draftCwd.value || undefined,
      permissionMode: globalMode.value,
    })
    showNewTabMenu.value = false
    await refreshMeta()
    if (session?.sessionId) await selectSession(session.sessionId)
  } catch (e) {
    noteError(e)
  } finally {
    busy.value = ''
  }
}

function pickAdjacentSessionId(id: string, list: TerminalSession[]): string {
  const idx = list.findIndex(s => s.sessionId === id)
  if (idx < 0) return list[0]?.sessionId ?? ''
  const neighbor = list[idx + 1] ?? list[idx - 1]
  return neighbor?.sessionId ?? ''
}

async function closeTab(id: string, ev?: Event) {
  ev?.stopPropagation()
  const before = sessions.value.filter(s => s.status !== 'closed')
  const nextFocus = activeId.value === id ? pickAdjacentSessionId(id, before) : activeId.value
  burnSessionSecrets(id)
  const { [id]: _c, ...restChunks } = chunksBySession.value
  const { [id]: _cmd, ...restCmds } = commandsBySession.value
  const { [id]: _n, ...restNext } = nextIndexBySession.value
  const { [id]: _conn, ...restConn } = connectionsBySession.value
  chunksBySession.value = restChunks
  commandsBySession.value = restCmds
  nextIndexBySession.value = restNext
  connectionsBySession.value = restConn

  if (isDemo.value) {
    sessions.value = sessions.value.filter(s => s.sessionId !== id)
    if (activeId.value === id) {
      activeId.value = nextFocus
      chunks.value = activeId.value ? chunksBySession.value[activeId.value] ?? [] : []
      commands.value = activeId.value ? commandsBySession.value[activeId.value] ?? [] : []
    }
    schedulePersistRestore()
    return
  }
  try {
    await smartTerminalApi.close(id)
    if (activeId.value === id) {
      activeId.value = ''
      chunks.value = []
      commands.value = []
    }
    await refreshMeta()
    if (nextFocus && sessions.value.some(s => s.sessionId === nextFocus)) {
      await selectSession(nextFocus)
    } else if (!activeId.value && sessions.value.length) {
      await selectSession(sessions.value[0].sessionId)
    }
    schedulePersistRestore()
  } catch (e) {
    noteError(e)
  }
}

function startRename(s: TerminalSession, ev?: Event) {
  ev?.stopPropagation()
  renamingId.value = s.sessionId
  renameDraft.value = s.title
}

async function commitRename() {
  const id = renamingId.value
  if (!id) return
  const title = renameDraft.value.trim()
  renamingId.value = ''
  if (!title) return
  if (isDemo.value) {
    const idx = sessions.value.findIndex(s => s.sessionId === id)
    if (idx >= 0) sessions.value[idx] = { ...sessions.value[idx], title }
    return
  }
  try {
    await smartTerminalApi.renameSession(id, title)
    await refreshMeta()
  } catch (e) {
    noteError(e)
  }
}

async function runCommand() {
  if (isDemo.value || !activeId.value || !commandInput.value.trim() || busy.value) return
  busy.value = 'run'
  errorText.value = ''
  try {
    const res = await smartTerminalApi.run(activeId.value, commandInput.value)
    commandInput.value = ''
    await Promise.all([pollOutput(true), loadCommands(), refreshApprovals(), refreshMeta()])
    if (res?.status === 'awaiting_approval' && res.approvalId) {
      const list = await smartTerminalApi.listApprovals(activeId.value)
      approvals.value = list ?? []
      activeApproval.value = approvals.value.find(a => a.approvalId === res.approvalId) ?? approvals.value[0] ?? null
      showApproval.value = !!activeApproval.value
    }
  } catch (e) {
    noteError(e)
  } finally {
    busy.value = ''
  }
}

async function resolveApproval(approve: boolean) {
  if (!activeApproval.value || busy.value) return
  if (isDemo.value) {
    showApproval.value = false
    const id = activeApproval.value.approvalId
    approvals.value = approvals.value.filter(a => a.approvalId !== id)
    activeApproval.value = null
    return
  }
  busy.value = 'approval'
  try {
    await smartTerminalApi.resolveApproval(activeApproval.value.approvalId, approve)
    showApproval.value = false
    activeApproval.value = null
    await Promise.all([pollOutput(true), loadCommands(), refreshApprovals(), refreshMeta()])
  } catch (e) {
    noteError(e)
  } finally {
    busy.value = ''
  }
}

async function changeMode(mode: PermissionMode) {
  globalMode.value = mode
  if (isDemo.value) {
    if (activeSession.value) {
      const idx = sessions.value.findIndex(s => s.sessionId === activeId.value)
      if (idx >= 0) sessions.value[idx] = { ...sessions.value[idx], permissionMode: mode }
    }
    return
  }
  try {
    await smartTerminalApi.setPermissionMode(mode)
    if (activeId.value) await smartTerminalApi.setSessionMode(activeId.value, mode)
    await refreshMeta()
  } catch (e) {
    noteError(e)
  }
}

async function loadCommands() {
  if (isDemo.value) return
  if (!activeId.value) {
    commands.value = []
    return
  }
  commands.value = (await smartTerminalApi.commands(activeId.value)) ?? []
  commandsBySession.value = { ...commandsBySession.value, [activeId.value]: [...commands.value] }
}

async function pollOutput(reset = false) {
  if (isDemo.value || !activeId.value) return
  try {
    if (reset) {
      chunks.value = []
      nextIndex.value = 0
    }
    const snap = await smartTerminalApi.output(activeId.value, nextIndex.value)
    if (!snap) return
    if (snap.chunks?.length) {
      chunks.value = chunks.value.concat(snap.chunks)
      nextIndex.value = snap.nextIndex
      cacheActiveBuckets()
      schedulePersistRestore()
      await nextTick()
      if (consoleEl.value) consoleEl.value.scrollTop = consoleEl.value.scrollHeight
    }
  } catch (e) {
    if (reset) noteError(e)
  }
}

async function openAudit() {
  showAudit.value = true
  if (isDemo.value) return
  try {
    const data = await smartTerminalApi.audit(100)
    auditItems.value = data?.items ?? []
  } catch (e) {
    noteError(e)
  }
}

async function rerun(commandId: string) {
  if (isDemo.value || !activeId.value || busy.value) return
  busy.value = 'rerun'
  try {
    await smartTerminalApi.rerun(activeId.value, commandId)
    await Promise.all([pollOutput(true), loadCommands(), refreshApprovals()])
  } catch (e) {
    noteError(e)
  } finally {
    busy.value = ''
  }
}

/** 同时有 steps 与 commands 时只送 steps（后端亦以 steps 为准） */
function buildLaunchBody(request: LaunchTerminalRequest): LaunchTerminalRequest {
  const body: LaunchTerminalRequest = { ...request }
  if (body.steps?.length) {
    body.commands = undefined
  }
  return body
}

function openInteractivePasswordForRequest(request: LaunchTerminalRequest, summary: string, extraWarn?: string) {
  pendingLaunch.value = {
    request: { ...request, password: undefined, auth: 'interactive' },
    summary,
    passwordInUrl: false,
    needsInteractivePassword: true,
  }
  launchBanner.value = `URL 启动：${summary}`
  launchWarning.value = extraWarn
    || '需要交互密码：确认后再创建标签并 launch（auth=sshpass）。强烈推荐改用 auth=key。'
  interactivePassword.value = ''
  interactivePasswordReveal.value = false
  showInteractivePassword.value = true
  showPasswordAck.value = false
}

function evaluateReconnect(session: TerminalSession | null | undefined) {
  if (!session || isDemo.value) {
    reconnectBanner.value = ''
    return
  }
  const hint = session.reconnectHint || ''
  const alive = !!session.processAlive
  const can = session.reconnectable !== false
  const isSsh = session.launchType === 'ssh' || session.launchType === 'wsl-ssh'
  const conn = connectionsBySession.value[session.sessionId]
  const authMethod = conn?.authMethod || ''
  const hasMemPwd = !!sessionPasswords.value[session.sessionId]

  if (!can) {
    reconnectBanner.value = hint || '会话已关闭，请重新 launch / 新建。'
    return
  }
  if (alive) {
    reconnectBanner.value = hint || ''
    return
  }
  // 进程已死：展示提示；缺密 SSH 引导再走 S5
  if (isSsh && authMethod !== 'key' && !hasMemPwd) {
    reconnectBanner.value = (hint || '活动进程可能已退出。')
      + ' 缺密 SSH 需重新输入密码后再 launch（先框后标签）。'
  } else {
    reconnectBanner.value = hint || (isSsh ? '活动进程可能已退出；可继续拉历史 output，或重新 launch。' : '')
  }
}

/** 从当前标签连接摘要打开 S5，确认后重新 launch（不复用旧孤儿空标签） */
function promptReconnectPassword() {
  const s = activeSession.value
  if (!s) return
  const conn = connectionsBySession.value[s.sessionId] || {}
  const request: LaunchTerminalRequest = {
    profile: conn.profile || (s.launchType as string) || 'ssh',
    type: (s.launchType as string) || 'ssh',
    host: conn.host || s.remoteHost,
    port: conn.port || 22,
    user: conn.user || s.remoteUser,
    cwd: conn.cwd || s.cwd,
    shellId: conn.shellId || s.shellId,
    title: conn.title || s.title,
    auth: 'interactive',
  }
  const summary = [
    `profile=${request.profile}`,
    request.host ? `host=${request.host}` : '',
    request.user ? `user=${request.user}` : '',
    'auth=interactive',
    'reconnect=1',
  ].filter(Boolean).join(' · ')
  openInteractivePasswordForRequest(
    request,
    summary,
    '刷新/断连后密码未恢复。确认后将重新 launch（先框后标签）；推荐改用 auth=key。',
  )
}

async function applyLaunch(request: LaunchTerminalRequest, summary: string, passwordInUrl: boolean) {
  launchBanner.value = `URL 启动：${summary}`
  if (passwordInUrl) {
    launchWarning.value = '地址栏曾含密码，已从 URL 擦除；密码仅短暂注入进程环境，不写审计原文。建议改用密钥（auth=key）。'
  }
  if (isDemo.value) return
  busy.value = 'launch'
  try {
    const body = buildLaunchBody(request)
    // S5 确认路径：password + auth=sshpass；禁止无密 interactive 误打出孤儿标签
    const res = await smartTerminalApi.launch(body)
    const pwd = body.password
    body.password = undefined
    request.password = undefined
    if (res?.needsInteractivePassword) {
      // 防御：即便 200 带此标志也不建标签
      openInteractivePasswordForRequest(request, summary, res.message || undefined)
      return
    }
    if (res?.warning) launchWarning.value = res.warning
    await refreshMeta()
    if (res?.session?.sessionId) {
      rememberConnection(res.session.sessionId, request, res.launchType || res.profile)
      if (pwd) {
        sessionPasswords.value = { ...sessionPasswords.value, [res.session.sessionId]: pwd }
      }
      await selectSession(res.session.sessionId)
      schedulePersistRestore()
    }
  } catch (e) {
    const err = e as ApiError
    const data = (err?.data || {}) as { needsInteractivePassword?: boolean; message?: string; NeedsInteractivePassword?: boolean }
    const needs = !!(data.needsInteractivePassword || data.NeedsInteractivePassword || /交互密码|needsInteractivePassword|先框后/i.test(err?.message || ''))
    if (err instanceof ApiError && err.status === 400 && needs) {
      // 后端硬拒无密 interactive：无 session；打开 S5，勿留孤儿标签
      openInteractivePasswordForRequest(
        { ...request, password: undefined, auth: 'interactive' },
        summary,
        err.message || '需要交互密码后再 launch。',
      )
      errorText.value = ''
      return
    }
    noteError(e)
  } finally {
    busy.value = ''
  }
}

function cancelPasswordAck() {
  if (pendingLaunch.value?.request) {
    pendingLaunch.value.request.password = undefined
  }
  pendingLaunch.value = null
  showPasswordAck.value = false
  passwordAckChecked.value = false
  launchWarning.value = '已取消带密码的 URL 启动。请改用 auth=key，或重新打开深链走交互式密码输入。'
}

async function confirmPasswordAck() {
  if (!passwordAckChecked.value || !pendingLaunch.value) return
  const pending = pendingLaunch.value
  showPasswordAck.value = false
  passwordAckChecked.value = false
  pendingLaunch.value = null
  if (passwordAckMode.value === 'intranet') {
    persistUrlPasswordRiskAck()
  }
  await applyLaunch(pending.request, pending.summary, pending.passwordInUrl)
}

function cancelInteractivePassword() {
  if (pendingLaunch.value?.request) {
    pendingLaunch.value.request.password = undefined
  }
  interactivePassword.value = ''
  interactivePasswordReveal.value = false
  showInteractivePassword.value = false
  pendingLaunch.value = null
  launchWarning.value = '已取消交互式密码输入。可改用 auth=key，或重新发起深链。'
}

async function confirmInteractivePassword() {
  if (!pendingLaunch.value) return
  const pwd = interactivePassword.value
  if (!pwd) {
    errorText.value = '请输入密码，或取消后改用 auth=key（推荐密钥登录）。'
    return
  }
  // 先框后标签：确认前不创建任何标签；确认后才 launch（服务端建会话）
  const pending = pendingLaunch.value
  pending.request.password = pwd
  // 定稿：确认后带 password + auth=sshpass 再 launch（key 除外）
  pending.request.auth = pending.request.auth === 'key' ? 'key' : 'sshpass'
  interactivePassword.value = ''
  interactivePasswordReveal.value = false
  showInteractivePassword.value = false
  pendingLaunch.value = null
  try {
    await applyLaunch(pending.request, pending.summary, false)
  } finally {
    pending.request.password = undefined
  }
}

async function consumeUrlLaunch() {
  if (isDemo.value || typeof location === 'undefined') return
  const parsed = parseLaunchQuery(location.search || '')
  scrubLaunchParamsFromUrl()
  if (!parsed) return

  if (parsed.unsupportedProfileError) {
    launchBanner.value = `URL 启动：${parsed.summary}`
    errorText.value = parsed.unsupportedProfileError
    customUnsupported.value = true
    launchWarning.value = 'custom:<id> 仅协议位预留，本里程碑不提供选择器，也不会静默当作 local。'
    return
  }

  const mode = resolveUrlPasswordMode({ queryMode: parsed.urlPasswordMode })
  passwordAckMode.value = mode

  if (parsed.passwordInUrl && isUrlPasswordDisabled(mode)) {
    parsed.request.password = undefined
    launchBanner.value = `URL 启动：${parsed.summary}`
    errorText.value = '当前策略禁用 URL 密码（url-password-mode=disabled）。请使用 auth=key，或将模式设为 intranet / always-ack。'
    // 仍可尝试无密码启动（interactive/key）；若 auth=sshpass 则后端会报错——改为不调用 launch
    if ((parsed.request.auth || 'sshpass') === 'sshpass' || (parsed.request.auth || '') === 'password') {
      return
    }
  }

  if (parsed.passwordInUrl && needsUrlPasswordAck(mode)) {
    pendingLaunch.value = {
      request: parsed.request,
      summary: parsed.summary,
      passwordInUrl: true,
    }
    launchBanner.value = `URL 启动：${parsed.summary}`
    launchWarning.value = '检测到 URL 密码（内网过渡能力）。请确认风险后再继续；推荐改用 auth=key 或交互式输入。'
    showPasswordAck.value = true
    passwordAckChecked.value = false
    return
  }

  if (parsed.needsInteractivePassword) {
    // S5：先出密码框，确认前不建标签（禁止空/孤儿标签）
    pendingLaunch.value = {
      request: parsed.request,
      summary: parsed.summary,
      passwordInUrl: false,
      needsInteractivePassword: true,
    }
    launchBanner.value = `URL 启动：${parsed.summary}`
    launchWarning.value = '未提供密码：请先在遮罩中输入（仅内存，关标签即焚）。强烈推荐改用 auth=key 密钥登录。'
    interactivePassword.value = ''
    interactivePasswordReveal.value = false
    showInteractivePassword.value = true
    return
  }

  await applyLaunch(parsed.request, parsed.summary, parsed.passwordInUrl)
}

onMounted(async () => {
  if (props.fixture) {
    applyFixture(props.fixture)
    return
  }
  try {
    const snap = applyRestoreSnapshot()
    await refreshMeta()
    // 合并：服务端仍有的会话保留；快照补齐缓冲 / 激活项 / 连接摘要
    if (snap) {
      if (snap.activeId && sessions.value.some(s => s.sessionId === snap.activeId)) {
        activeId.value = snap.activeId
      }
      // 服务端已无、但快照仍有：展示只读恢复态（可能已断连；SSH 缺密需重连再走 S5）
      const serverIds = new Set(sessions.value.map(s => s.sessionId))
      const orphans = snap.tabs.filter(t => !serverIds.has(t.sessionId))
      if (orphans.length && !sessions.value.length) {
        sessions.value = orphans.map(restoredTabToSession)
        activeId.value = snap.activeId && sessions.value.some(s => s.sessionId === snap.activeId)
          ? snap.activeId
          : sessions.value[0]?.sessionId ?? ''
        launchWarning.value = (launchWarning.value ? launchWarning.value + ' ' : '')
          + '已从本页会话恢复标签与近期输出（无密码）。远端/本机壳进程可能已结束；SSH 若需重连将再次弹出密码框。'
      }
    }
    await consumeUrlLaunch()
    if (activeId.value) {
      // 先灌入恢复缓冲；强制走一次加载（避免 activeId 已相同导致 selectSession 早退）
      const resumeId = activeId.value
      if (chunksBySession.value[resumeId]?.length) {
        chunks.value = [...chunksBySession.value[resumeId]]
        const restoredNext = nextIndexBySession.value[resumeId]
        nextIndex.value = restoredNext && restoredNext > 0 ? restoredNext : chunks.value.length
        nextIndexBySession.value = { ...nextIndexBySession.value, [resumeId]: nextIndex.value }
      }
      activeId.value = ''
      await selectSession(resumeId)
    }
    schedulePersistRestore()
  } catch (e) {
    noteError(e)
  }
  pollTimer = setInterval(() => {
    void pollOutput(false)
    void refreshApprovals()
  }, 1000)
})

onUnmounted(() => {
  if (pollTimer) clearInterval(pollTimer)
  if (restorePersistTimer) clearTimeout(restorePersistTimer)
  persistRestoreNow()
})

watch(
  () => props.fixture,
  f => {
    if (f) applyFixture(f)
  },
  { deep: true },
)

watch(activeId, () => {
  if (!isDemo.value) void loadCommands()
  schedulePersistRestore()
})

watch(
  [sessions, chunks, chunksBySession],
  () => schedulePersistRestore(),
  { deep: true },
)
</script>

<template>
  <div class="st-root" data-testid="smart-terminal-root">
    <header class="st-header">
      <div>
        <h1 class="view-title">智能终端 <span class="ver">v{{ version }}</span></h1>
        <p class="sub">可审批的命令会话 · 基础多标签 · URL 深链 · <code>st_*</code></p>
      </div>
      <div class="header-actions">
        <span class="badge" :class="modeBadgeClass" data-testid="mode-badge">权限：{{ modeLabel }}</span>
        <button class="btn ghost" type="button" data-testid="open-audit" @click="openAudit">审计</button>
      </div>
    </header>

    <div v-if="launchBanner" class="banner info" data-testid="launch-banner">{{ launchBanner }}</div>
    <div v-if="launchWarning" class="banner warn" data-testid="launch-warning">{{ launchWarning }}</div>
    <div v-if="reconnectBanner" class="banner warn" data-testid="reconnect-banner">
      <span>{{ reconnectBanner }}</span>
      <button
        v-if="activeSession && (activeSession.launchType === 'ssh' || activeSession.launchType === 'wsl-ssh') && !sessionPasswords[activeSession.sessionId] && connectionsBySession[activeSession.sessionId]?.authMethod !== 'key'"
        class="btn tiny ghost"
        type="button"
        data-testid="reconnect-password-btn"
        @click="promptReconnectPassword"
      >重新输入密码并 launch</button>
    </div>
    <div v-if="unauthorized" class="banner error" data-testid="banner-unauthorized">未授权（401）。请先登录宿主，确保 localStorage 有 forge_api_token。</div>
    <div
      v-else-if="errorText"
      class="banner error"
      :data-testid="customUnsupported ? 'custom-unsupported-banner' : 'banner-error'"
    >{{ errorText }}</div>

    <!-- Windows Terminal 基础标签栏（新建/关闭/切换/重命名） -->
    <div class="tabbar" data-testid="tab-bar">
      <div
        v-for="s in openSessions"
        :key="s.sessionId"
        class="tab"
        :class="{ active: s.sessionId === activeId }"
        :data-session-id="s.sessionId"
        @click="selectSession(s.sessionId)"
        @dblclick="startRename(s, $event)"
      >
        <template v-if="renamingId === s.sessionId">
          <input
            v-model="renameDraft"
            class="tab-rename"
            data-testid="tab-rename-input"
            @click.stop
            @keydown.enter.prevent="commitRename"
            @keydown.esc.prevent="renamingId = ''"
            @blur="commitRename"
          />
        </template>
        <template v-else>
          <span class="tab-title">{{ s.title }}</span>
          <span class="tab-meta">{{ tabMeta(s) }}</span>
        </template>
        <button class="tab-close" type="button" title="关闭标签" data-testid="tab-close" @click="closeTab(s.sessionId, $event)">×</button>
      </div>
      <button class="tab-new" type="button" data-testid="tab-new" title="新建标签" @click="showNewTabMenu = !showNewTabMenu">+</button>
    </div>

    <div v-if="showNewTabMenu" class="new-tab-panel" data-testid="new-tab-panel">
      <label>标题 <input v-model="draftTitle" placeholder="新标签" /></label>
      <label>外壳
        <select v-model="draftShell" data-testid="shell-picker">
          <option disabled value="">选择外壳</option>
          <option v-for="s in shells" :key="s.id" :value="s.id" :disabled="!s.available">
            {{ s.displayName }}{{ s.available ? '' : '（不可用）' }}{{ s.note ? ` · ${s.note}` : '' }}
          </option>
        </select>
      </label>
      <label>工作目录 <input v-model="draftCwd" placeholder="留空=进程当前目录" /></label>
      <div class="mode-row" data-testid="mode-row">
        <button type="button" class="chip" :class="{ on: globalMode === 'Auto' }" @click="changeMode('Auto')">Auto</button>
        <button type="button" class="chip" :class="{ on: globalMode === 'AskWrite' }" @click="changeMode('AskWrite')">AskWrite</button>
        <button type="button" class="chip" :class="{ on: globalMode === 'AskHighRisk' }" @click="changeMode('AskHighRisk')">AskHighRisk</button>
      </div>
      <div class="new-tab-actions">
        <button class="btn ghost" type="button" @click="showNewTabMenu = false">取消</button>
        <button class="btn primary" type="button" :disabled="!!busy" @click="createSession">打开标签</button>
      </div>
    </div>

    <main class="panel main">
      <div class="panel-head">
        <div>
          <strong>{{ activeSession?.title || '未选择标签' }}</strong>
          <span v-if="activeSession" class="muted">
            · {{ tabMeta(activeSession) }}
            <template v-if="activeSession.launchType && activeSession.launchType !== 'local'"> · {{ activeSession.launchType }}</template>
            · {{ activeSession.cwd }}
            <template v-if="activeSession.processAlive != null">
              · <span :data-testid="'process-alive-' + (activeSession.processAlive ? 'yes' : 'no')">{{ activeSession.processAlive ? '进程存活' : '进程已退出' }}</span>
            </template>
          </span>
        </div>
        <button v-if="activeSession" class="btn ghost tiny" type="button" @click="closeTab(activeSession.sessionId)">关闭</button>
      </div>

      <div ref="consoleEl" class="console" aria-live="polite" data-testid="console">
        <div v-if="!activeSession" class="empty" data-testid="empty-state">
          <p>点「+」新建标签，或通过 URL 深链启动（<code>profile</code> + <code>step</code>/<code>run</code>）。</p>
          <p class="empty-hint"><strong>推荐密钥登录</strong>（<code>auth=key</code>）。URL 明文密码仅内网过渡；SSH 无密码时会先弹出交互密码框，确认后再建标签。</p>
        </div>
        <div v-else-if="!chunks.length" class="empty">等待输出…输入命令后回车执行。</div>
        <div v-for="c in chunks" :key="c.index" class="line" :class="c.stream">
          <span class="tag">{{ c.stream }}</span>
          <span class="txt">{{ c.text }}</span>
        </div>
      </div>

      <form class="cmdline" @submit.prevent="runCommand">
        <input
          v-model="commandInput"
          :disabled="!activeSession || !!busy || isDemo"
          placeholder="输入命令，Enter 执行（密钥/密码会在日志中脱敏）"
          autocomplete="off"
          spellcheck="false"
          data-testid="command-input"
        />
        <button class="btn primary" type="submit" :disabled="!activeSession || !commandInput.trim() || !!busy || isDemo">
          {{ busy === 'run' ? '执行中…' : '运行' }}
        </button>
      </form>

      <div class="history" data-testid="command-history">
        <div class="panel-head"><strong>命令历史</strong></div>
        <ul>
          <li v-if="!commands.length" class="empty">暂无历史。</li>
          <li v-for="c in commands" :key="c.commandId">
            <code>{{ c.commandRedacted }}</code>
            <span class="pill">{{ riskLabel(c.risk) }}</span>
            <span class="muted">{{ c.status }}{{ c.exitCode != null ? ` · exit ${c.exitCode}` : '' }}</span>
            <button class="btn tiny ghost" type="button" :disabled="isDemo" @click="rerun(c.commandId)">重跑</button>
          </li>
        </ul>
      </div>
    </main>


    <div v-if="showPasswordAck" class="modal-mask modal-ack" data-testid="password-ack-modal" @click.self="cancelPasswordAck">
      <div class="modal" role="dialog" aria-modal="true" aria-labelledby="st-pwd-ack-title">
        <h2 id="st-pwd-ack-title">URL 密码风险确认</h2>
        <p class="muted">当前策略：<code data-testid="password-ack-mode">{{ passwordAckMode }}</code>。密码深链仅为<strong>内网过渡</strong>，推荐 <code>auth=key</code>。</p>
        <ul class="ack-list">
          <li>密码可能进入浏览器历史、同步账号、代理与访问日志</li>
          <li>转发或截屏链接即可能泄露凭证</li>
          <li>已从地址栏擦除明文；若继续，密码仅短暂注入进程环境 SSHPASS，不写审计原文</li>
        </ul>
        <label class="ack-check" data-testid="password-ack-label">
          <input v-model="passwordAckChecked" type="checkbox" data-testid="password-ack-checkbox" />
          我已知晓风险，仍要在本环境使用 URL 密码启动
        </label>
        <div class="modal-actions">
          <button class="btn ghost" type="button" data-testid="password-ack-cancel" @click="cancelPasswordAck">取消</button>
          <button
            class="btn primary"
            type="button"
            data-testid="password-ack-continue"
            :disabled="!passwordAckChecked || !!busy"
            @click="confirmPasswordAck"
          >
            继续启动
          </button>
        </div>
      </div>
    </div>

    <div
      v-if="!showPasswordAck && showInteractivePassword"
      class="modal-mask modal-interactive"
      data-testid="interactive-password-modal"
      @click.self="cancelInteractivePassword"
    >
      <div class="modal" role="dialog" aria-modal="true" aria-labelledby="st-interactive-pwd-title">
        <h2 id="st-interactive-pwd-title">输入连接密码</h2>
        <p class="muted">
          <code data-testid="interactive-password-target">
            {{ pendingLaunch?.request?.user || '?' }}@{{ pendingLaunch?.request?.host || '?' }}:{{ pendingLaunch?.request?.port || 22 }}
          </code>
          · profile=<code>{{ pendingLaunch?.request?.profile || pendingLaunch?.request?.type || 'ssh' }}</code>
        </p>
        <p class="muted">密码仅保存在内存，确认后<strong>再</strong>创建标签并连接；取消不建标签。关标签即焚，不做「记住本会话」。<strong>强烈推荐改用密钥登录</strong>（<code>auth=key</code>）。</p>
        <div class="key-recommend" data-testid="key-recommend">主推：使用 <code>auth=key</code> 与 <code>identityFile</code>，避免密码进入浏览器。</div>
        <label class="field-label">
          密码
          <div class="pwd-row">
            <input
              v-model="interactivePassword"
              :type="interactivePasswordReveal ? 'text' : 'password'"
              autocomplete="current-password"
              data-testid="interactive-password-input"
              @keydown.enter.prevent="confirmInteractivePassword"
            />
            <button
              class="btn tiny ghost"
              type="button"
              data-testid="interactive-password-reveal"
              @click="interactivePasswordReveal = !interactivePasswordReveal"
            >
              {{ interactivePasswordReveal ? '隐藏' : '显示' }}
            </button>
          </div>
        </label>
        <div class="modal-actions">
          <button class="btn ghost" type="button" data-testid="interactive-password-cancel" @click="cancelInteractivePassword">取消</button>
          <button
            class="btn primary"
            type="button"
            data-testid="interactive-password-confirm"
            :disabled="!interactivePassword || !!busy"
            @click="confirmInteractivePassword"
          >
            连接并启动
          </button>
        </div>
      </div>
    </div>

    <div v-if="canShowApprovalOverlay && activeApproval" class="modal-mask modal-approval" data-testid="approval-modal" @click.self="showApproval = false">
      <div class="modal" role="dialog" aria-modal="true">
        <h2>需要审批</h2>
        <p class="muted">{{ activeApproval.reason }}</p>
        <div class="modal-cmd"><code>{{ activeApproval.commandRedacted }}</code></div>
        <p>风险：<strong>{{ riskLabel(activeApproval.risk) }}</strong> · 规则：{{ activeApproval.matchedRule }}</p>
        <div class="modal-actions">
          <button class="btn ghost" type="button" data-testid="approval-deny" :disabled="!!busy" @click="resolveApproval(false)">拒绝</button>
          <button class="btn primary" type="button" data-testid="approval-approve" :disabled="!!busy" @click="resolveApproval(true)">批准执行</button>
        </div>
      </div>
    </div>

    <aside v-if="canShowAuditDrawer" class="audit-drawer" data-testid="audit-drawer">
      <div class="panel-head">
        <strong>审计日志（JSONL）</strong>
        <button class="btn tiny ghost" type="button" @click="showAudit = false">关闭</button>
      </div>
      <div class="audit-filters">
        <label>
          类型
          <select v-model="auditKindFilter" data-testid="audit-kind-filter">
            <option value="">全部</option>
            <option v-for="k in auditKinds" :key="k" :value="k">{{ k }}</option>
          </select>
        </label>
        <label>
          搜索
          <input v-model="auditQuery" data-testid="audit-query" placeholder="命令 / 决策 / 规则" />
        </label>
      </div>
      <ul>
        <li v-for="a in filteredAudit" :key="a.eventId">
          <div class="a-top"><span>{{ a.kind }}</span><span class="muted">{{ a.time }}</span></div>
          <div v-if="a.commandRedacted" class="a-cmd"><code>{{ a.commandRedacted }}</code></div>
          <div class="muted">{{ a.decision || '' }} {{ a.matchedRule || '' }} {{ a.detail || '' }}</div>
        </li>
        <li v-if="!filteredAudit.length" class="empty">暂无审计事件。</li>
      </ul>
    </aside>
  </div>
</template>

<style scoped>
.st-root {
  height: 100%;
  display: flex;
  flex-direction: column;
  gap: 10px;
  padding: 16px;
  color: var(--el-text-color-primary);
  box-sizing: border-box;
  min-height: 0;
  position: relative;
}
.st-header { display: flex; justify-content: space-between; align-items: flex-start; gap: 12px; flex-shrink: 0; }
.view-title { margin: 0; font-size: 20px; }
.ver { font-size: 12px; color: var(--el-text-color-secondary); font-weight: 500; }
.sub { margin: 4px 0 0; color: var(--el-text-color-secondary); font-size: 13px; }
.header-actions { display: flex; gap: 8px; align-items: center; }

.tabbar {
  display: flex; align-items: stretch; gap: 4px; flex-shrink: 0;
  border-bottom: 1px solid var(--el-border-color); padding-bottom: 0; overflow-x: auto;
}
.tab {
  display: flex; align-items: center; gap: 8px;
  max-width: 220px; padding: 8px 10px;
  border: 1px solid var(--el-border-color); border-bottom: none;
  border-radius: 10px 10px 0 0; background: transparent; cursor: pointer;
  color: var(--el-text-color-secondary); font-size: 12px;
}
.tab.active { color: var(--el-text-color-primary); background: var(--el-bg-color-overlay); border-color: var(--el-color-primary); }
.tab-title { font-weight: 600; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.tab-meta { color: var(--el-text-color-secondary); font-size: 11px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.tab-close {
  border: none; background: transparent; color: var(--el-text-color-secondary); cursor: pointer;
  font-size: 14px; line-height: 1; padding: 0 2px;
}
.tab-close:hover { color: var(--el-color-danger); }
.tab-new {
  border: 1px dashed var(--el-border-color); background: transparent; color: var(--el-text-color-primary);
  border-radius: 8px; width: 36px; cursor: pointer; margin-bottom: 4px;
}
.tab-rename {
  width: 120px; background: transparent; color: var(--el-text-color-primary);
  border: 1px solid var(--el-color-primary); border-radius: 4px; padding: 2px 6px; font-size: 12px;
}

.new-tab-panel {
  display: grid; gap: 8px; padding: 12px; border: 1px solid var(--el-border-color);
  border-radius: 12px; background: var(--el-bg-color-overlay); flex-shrink: 0;
}
.new-tab-panel label { display: grid; gap: 4px; font-size: 12px; color: var(--el-text-color-secondary); }
.new-tab-panel input, .new-tab-panel select, .cmdline input, .audit-filters input, .audit-filters select {
  width: 100%; box-sizing: border-box; background: transparent; color: var(--el-text-color-primary);
  border: 1px solid var(--el-border-color); border-radius: 8px; padding: 8px 10px;
}
.new-tab-actions { display: flex; justify-content: flex-end; gap: 8px; }
.mode-row { display: flex; gap: 6px; flex-wrap: wrap; }
.chip {
  border: 1px solid var(--el-border-color); background: transparent; color: var(--el-text-color-primary);
  border-radius: 999px; padding: 4px 10px; cursor: pointer; font-size: 12px;
}
.chip.on { border-color: var(--el-color-primary); color: var(--el-color-primary); }

.panel {
  background: var(--el-bg-color-overlay); border: 1px solid var(--el-border-color); border-radius: 12px;
  padding: 12px; display: flex; flex-direction: column; min-height: 0; flex: 1;
}
.panel-head { display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px; gap: 8px; }
.muted { color: var(--el-text-color-secondary); font-size: 12px; }
.console {
  flex: 1; min-height: 220px; overflow: auto; background: var(--el-bg-color-overlay);
  border: 1px solid var(--el-border-color); border-radius: 10px; padding: 10px;
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; font-size: 12.5px;
}
.line { display: grid; grid-template-columns: 64px 1fr; gap: 8px; margin-bottom: 4px; }
.line .tag { color: var(--el-text-color-secondary); }
.line.stderr .txt { color: var(--el-color-danger); }
.line.system .txt { color: var(--el-color-warning); }
.cmdline { display: flex; gap: 8px; margin-top: 10px; }
.history { margin-top: 12px; max-height: 160px; overflow: auto; }
.history ul { list-style: none; margin: 0; padding: 0; display: grid; gap: 6px; }
.history li { display: flex; gap: 8px; align-items: center; flex-wrap: wrap; font-size: 12px; }
.pill { border: 1px solid var(--el-border-color); border-radius: 999px; padding: 1px 8px; }
.btn {
  border: 1px solid var(--el-border-color); background: transparent; color: var(--el-text-color-primary);
  border-radius: 8px; padding: 8px 12px; cursor: pointer;
}
.btn.primary { background: var(--el-color-primary); border-color: var(--el-color-primary); color: var(--el-color-white); }
.btn.ghost { background: transparent; }
.btn.tiny { padding: 4px 8px; font-size: 12px; }
.btn:disabled { opacity: 0.5; cursor: not-allowed; }
.badge { border-radius: 999px; padding: 4px 10px; font-size: 12px; border: 1px solid var(--el-border-color); }
.badge-auto { color: var(--el-color-success); border-color: var(--el-color-success); }
.badge-write { color: var(--el-color-primary); border-color: var(--el-color-primary); }
.badge-high { color: var(--el-color-warning); border-color: var(--el-color-warning); }
.banner { border-radius: 8px; padding: 8px 10px; font-size: 13px; flex-shrink: 0; }
.banner.error { background: color-mix(in srgb, var(--el-color-danger) 16%, transparent); color: var(--el-color-danger); }
.banner.warn { background: color-mix(in srgb, var(--el-color-warning) 16%, transparent); color: var(--el-color-warning); }
.banner.warn[data-testid="reconnect-banner"] { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; justify-content: space-between; }
.banner.info { background: color-mix(in srgb, var(--el-color-primary) 14%, transparent); color: var(--el-color-primary); }
.empty { color: var(--el-text-color-secondary); font-size: 13px; padding: 8px 0; }
.modal-mask {
  position: absolute; inset: 0; background: var(--el-overlay-color-lighter);
  display: grid; place-items: center; z-index: 40;
}
/* 浮层顺序：风险 ack → 交互密码 → 审批 → 审计 */
.modal-ack { z-index: 50; }
.modal-interactive { z-index: 45; }
.modal-approval { z-index: 40; }
.empty-hint { margin: 8px 0 0; font-size: 12px; line-height: 1.5; }
.modal {
  width: min(520px, 92%); background: var(--el-bg-color-overlay); border: 1px solid var(--el-border-color);
  border-radius: 14px; padding: 18px;
}
.modal-cmd { background: var(--el-bg-color-overlay); border-radius: 8px; padding: 10px; margin: 10px 0; word-break: break-all; }
.modal-actions { display: flex; justify-content: flex-end; gap: 8px; margin-top: 14px; }
.ack-list { margin: 10px 0; padding-left: 18px; color: var(--el-text-color-secondary); font-size: 13px; line-height: 1.5; }
.ack-check { display: flex; gap: 8px; align-items: flex-start; font-size: 13px; margin-top: 8px; cursor: pointer; }
.ack-check input { margin-top: 2px; }
.audit-drawer {
  position: absolute; top: 0; right: 0; bottom: 0; width: min(420px, 92%);
  background: var(--el-bg-color-overlay); border-left: 1px solid var(--el-border-color);
  padding: 14px; overflow: auto; z-index: 30; box-sizing: border-box;
}
.audit-filters { display: grid; gap: 8px; margin-bottom: 12px; }
.audit-filters label { display: grid; gap: 4px; font-size: 12px; color: var(--el-text-color-secondary); }
.audit-drawer ul { list-style: none; margin: 0; padding: 0; display: grid; gap: 10px; }
.a-top { display: flex; justify-content: space-between; gap: 8px; font-size: 13px; }
.a-cmd { margin: 4px 0; word-break: break-all; }

.key-recommend {
  margin: 10px 0; padding: 8px 10px; border-radius: 8px; font-size: 12px;
  background: color-mix(in srgb, var(--el-color-success) 14%, transparent); color: var(--el-color-success);
}
.field-label { display: flex; flex-direction: column; gap: 6px; margin: 12px 0; font-size: 13px; }
.pwd-row { display: flex; gap: 8px; align-items: center; }
.pwd-row input { flex: 1; min-width: 0; padding: 8px 10px; border-radius: 6px; border: 1px solid var(--el-border-color); background: var(--el-fill-color-blank); color: inherit; }
</style>
