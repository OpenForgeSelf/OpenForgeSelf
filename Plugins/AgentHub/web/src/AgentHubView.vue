<script setup lang="ts">
/**
 * Agent 中枢主界面（AgentHubView）。
 *
 * 职责：把后端「Agent 注册表 + 委派总线」可视化——
 *  1) Agent 目录：本机已登记的其它 agent，含健康灯与 F1-F6 能力矩阵；
 *  2) 委派面板：选 agent、填提示词/工作目录/权限模式，发起一次委派；
 *  3) 任务台：任务列表 + 实时事件流（SSE）+ 取消；
 *  4) 审批台：人在回路——外部 agent 申请权限时在此放行/拒绝（仅本次）。
 *
 * 约束（plugin-development 铁律 §4）：插件是独立预编译产物，不能用 <ElXxx>，
 * 一律原生 HTML + --el-* token 变量；不定义任何自定义颜色 token。
 *
 * 拆文件理由：单文件超过 ~700 行后模板与逻辑相互干扰，且 SSE 订阅的生命周期
 * 需要独立管理（组件卸载必须停流，否则泄漏连接）。
 */

import { computed, onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import {
  PROBE_ALL_CONCURRENCY,
  formatProbeAllMessage,
  mapPool,
} from './probeAllRuntimes'
import {
  listAgents,
  createAgent,
  updateAgent,
  deleteAgent,
  setTrust,
  discoverAgents,
  probeAgent,
  listTasks,
  taskStats,
  cancelTask,
  createTask,
  listTaskEvents,
  listPendingPermissions,
  resolvePermission,
  streamTaskEvents,
  getSettings,
  saveSettings,
  listRuntimes,
  listSessions,
  probeRuntime,
  listRuntimeModels,
  getRuntimeInstallPlan,
  installRuntime,
  sendSessionInput,
  abortSession,
  closeSession,
  listSessionRecords,
  streamSessionRecords,
  type AgentDto,
  type AgentSaveRequest,
  type DiscoveredAgentDto,
  type PendingPermissionDto,
  type ProbeResultDto,
  type RuntimeDescriptorDto,
  type RuntimeProbeDto,
  type RuntimeModelsDto,
  type SessionDto,
  type SessionRecordDto,
  type TaskDto,
  type TaskEventDto,
  fetchPluginVersion,
} from './http'
import AgentHubAgentCard from './AgentHubAgentCard.vue'
import AgentHubAgentForm from './AgentHubAgentForm.vue'
import AgentHubEventStream from './AgentHubEventStream.vue'
import RuntimeBrandIcon from './RuntimeBrandIcon.vue'

/** 当前页签。Runtime 检测与会话分开展示，避免长列表叠会话操作。 */
type TabKey = 'agents' | 'delegate' | 'tasks' | 'runtimes' | 'sessions'
const tab = ref<TabKey>('agents')
const pluginVersion = ref('')

/** 全局提示条。 */
const message = ref('')
const messageType = ref<'ok' | 'err'>('ok')

// ────────────────────────────── Agent 目录 ──────────────────────────────

const agents = ref<AgentDto[]>([])
const loadingAgents = ref(false)
const discovering = ref(false)
const discovered = ref<DiscoveredAgentDto[]>([])

/** 附加扫描目录：每行一个，探测时除 PATH 外额外查找（解决装在不进 PATH 目录的 CLI 扫不到）。 */
const searchDirsText = ref('')
const savingDirs = ref(false)
const probingId = ref<number | null>(null)

/** 新增/编辑表单：editingId 为 null 表示「新增」。 */
const showForm = ref(false)
const editingId = ref<number | null>(null)

/** 授信对话框。 */
const trustTarget = ref<AgentDto | null>(null)
const trustScopes = ref<string[]>([])
const trustDraft = ref('')

/** 可选授信范围（与后端 PermissionBroker 的 Kind 取值对齐）。 */
const SCOPE_OPTIONS = [
  { value: 'read_file', label: '读文件' },
  { value: 'write_file', label: '写文件' },
  { value: 'exec_command', label: '执行命令' },
  { value: 'network', label: '网络访问' },
] as const

/** Hide test-only scripted AccessPoints from product Agent directory. */
function isProductAgent(a: AgentDto): boolean {
  const aps = a.accessPoints ?? []
  if (!aps.length) return true
  return !aps.some((p) => (p.runtimeId || '').toLowerCase() === 'scripted')
}

async function loadAgents() {
  loadingAgents.value = true
  try {
    const raw = (await listAgents()) ?? []
    agents.value = raw.filter(isProductAgent)
  } catch (e) {
    setMsg(`加载 agent 列表失败：${(e as Error).message}`, 'err')
  } finally {
    loadingAgents.value = false
  }
}

async function runDiscover() {
  discovering.value = true
  try {
    discovered.value = (await discoverAgents()) ?? []
    // 已在注册表中的候选无需展示（UI 不给「添加」按钮反而更吵）
    discovered.value = discovered.value.filter((d) => !d.alreadyRegistered)
    setMsg(`扫描完成，发现 ${discovered.value.length} 个未登记的 agent`)
  } catch (e) {
    setMsg(`扫描失败：${(e as Error).message}`, 'err')
  } finally {
    discovering.value = false
  }
}

/** 从一个发现候选快速登记（按 profile 预填交互口）。 */
async function registerDiscovered(d: DiscoveredAgentDto) {
  try {
    await createAgent({
      name: d.displayName || d.vendor,
      vendor: d.vendor,
      enabled: true,
      notes: `由扫描登记（${d.executable}${d.version ? ' @ ' + d.version : ''}）`,
    })
    discovered.value = discovered.value.filter((x) => x.vendor !== d.vendor)
    await loadAgents()
    setMsg(`已登记「${d.displayName || d.vendor}」`)
  } catch (e) {
    setMsg(`登记失败：${(e as Error).message}`, 'err')
  }
}

// ────────────────────────────── 附加扫描目录 ──────────────────────────────

/** 读取设置并回填文本框（每行一个目录）。读失败不打断主流程。 */
async function loadScanDirs() {
  try {
    const s = (await getSettings()) ?? {}
    searchDirsText.value = (s.searchDirectories ?? []).join('\n')
  } catch (e) {
    setMsg(`读取扫描目录配置失败：${(e as Error).message}`, 'err')
  }
}

/** 保存附加扫描目录（点即保存落盘，重新扫描即生效）。 */
async function saveScanDirs() {
  savingDirs.value = true
  try {
    const dirs = searchDirsText.value
      .split('\n')
      .map((d) => d.trim())
      .filter(Boolean)
    const saved = (await saveSettings({ searchDirectories: dirs })) ?? { searchDirectories: [] }
    // 回填后端规范化后的结果（去重/去空白后的真实值）
    searchDirsText.value = (saved.searchDirectories ?? []).join('\n')
    setMsg(`已保存 ${saved.searchDirectories?.length ?? 0} 个附加扫描目录，重新扫描即生效`)
  } catch (e) {
    setMsg(`保存失败：${(e as Error).message}`, 'err')
  } finally {
    savingDirs.value = false
  }
}

function openCreate() {
  editingId.value = null
  showForm.value = true
}

function openEdit(a: AgentDto) {
  editingId.value = a.id ?? null
  showForm.value = true
}

/** 表单提交（新增或更新）。 */
async function submitForm(body: AgentSaveRequest, id: number | null) {
  try {
    if (id == null) {
      await createAgent(body)
      setMsg('已登记新 agent')
    } else {
      await updateAgent(id, body)
      setMsg('已保存')
    }
    showForm.value = false
    await loadAgents()
  } catch (e) {
    setMsg(`保存失败：${(e as Error).message}`, 'err')
  }
}

async function removeAgent(a: AgentDto) {
  if (a.id == null) return
  try {
    await deleteAgent(a.id)
    await loadAgents()
    setMsg(`已删除「${a.name}」`)
  } catch (e) {
    setMsg(`删除失败：${(e as Error).message}`, 'err')
  }
}

async function runProbe(a: AgentDto) {
  if (a.id == null) return
  probingId.value = a.id
  try {
    const r = (await probeAgent(a.id)) as ProbeResultDto | undefined
    const tip =
      r?.health === 'Ok'
        ? `「${a.name}」可用（${r.version ?? '版本未知'}）`
        : r?.health === 'Degraded'
          ? `「${a.name}」降级：${r?.error ?? r?.profileWarning ?? '详见交互口'}`
          : `「${a.name}」不可用：${r?.error ?? '未找到可执行文件'}`
    setMsg(tip, r?.health === 'Missing' ? 'err' : 'ok')
    await loadAgents()
  } catch (e) {
    setMsg(`探测失败：${(e as Error).message}`, 'err')
  } finally {
    probingId.value = null
  }
}

/** 打开授信对话框，回填已授信范围。 */
function openTrust(a: AgentDto) {
  trustTarget.value = a
  trustScopes.value = [...(a.trustedScopes ?? [])]
  trustDraft.value = ''
}

function toggleScope(scope: string) {
  const idx = trustScopes.value.indexOf(scope)
  if (idx >= 0) trustScopes.value.splice(idx, 1)
  else trustScopes.value.push(scope)
}

function addCustomScope() {
  const v = trustDraft.value.trim()
  if (v && !trustScopes.value.includes(v)) trustScopes.value.push(v)
  trustDraft.value = ''
}

async function submitTrust() {
  const a = trustTarget.value
  if (a?.id == null) return
  if (trustScopes.value.length === 0) {
    setMsg('授信必须给出范围——空范围等于放开一切，后端会拒绝', 'err')
    return
  }
  try {
    await setTrust(a.id, true, trustScopes.value)
    trustTarget.value = null
    await loadAgents()
    setMsg(`已授信「${a.name}」（范围：${trustScopes.value.join('、')}）`)
  } catch (e) {
    setMsg(`授信失败：${(e as Error).message}`, 'err')
  }
}

async function revokeTrust(a: AgentDto) {
  if (a.id == null) return
  try {
    await setTrust(a.id, false, [])
    await loadAgents()
    setMsg(`已取消「${a.name}」的授信`)
  } catch (e) {
    setMsg(`取消授信失败：${(e as Error).message}`, 'err')
  }
}

// ────────────────────────────── 委派 ──────────────────────────────

/** 委派表单。 */
const delegate = reactive({
  agentId: null as number | null,
  prompt: '',
  cwd: '',
  permissionMode: 'read-only',
  /** P3：续接 SessionKey（可选） */
  sessionKey: '',
})

/** Runtime / Session 面（P3 + remaining-close）。 */
const runtimes = ref<RuntimeDescriptorDto[]>([])
const sessions = ref<SessionDto[]>([])
const loadingSessions = ref(false)
const activeSessionKey = ref<string | null>(null)
const sessionRecords = ref<SessionRecordDto[]>([])
const sessionInputText = ref('')
const sessionBusy = ref(false)
const runtimeProbeMap = reactive<Record<string, RuntimeProbeDto | undefined>>({})
const runtimeModelsMap = reactive<Record<string, RuntimeModelsDto | undefined>>({})
const runtimeBusyId = ref<string | null>(null)
/** Which runtime rows have expanded probe/models JSON (default collapsed — reduce clutter). */
const runtimeDetailOpen = reactive<Record<string, boolean>>({})

/** 一键探测全部 Runtime（前端并发调用既有 per-id probe；≠ 面板「刷新」）。 */
type ProbeCardStatus = 'idle' | 'running' | 'ok' | 'err'
const probingAll = ref(false)
const probeAllProgress = ref({ done: 0, total: 0, ok: 0, err: 0 })
const runtimeProbeStatus = reactive<Record<string, ProbeCardStatus>>({})
const runtimeProbeErrors = reactive<Record<string, string>>({})

/** 安装确认对话框。 */
const installTarget = ref<{ runtimeId: string; planJson: string } | null>(null)
const installBusy = ref(false)

/** Product surface never lists scripted (Worker smoke-only). */
function isProductRuntime(r: RuntimeDescriptorDto): boolean {
  const id = (r.runtimeId || '').toLowerCase()
  return id !== 'scripted'
}

/** Honest install badge from OAR snapshot fields (never fake green). */
function installBadge(r: RuntimeDescriptorDto): { label: string; cls: string } {
  const kind = (r.installationKind || '').toLowerCase()
  const via = (r.installationVia || '').toLowerCase()
  if (kind === 'not_found') return { label: '未安装', cls: 'ok-badge--err' }
  if (via === 'bundled') return { label: '内置捆绑', cls: 'ok-badge--idle' }
  if (kind === 'unsupported') return { label: '不支持', cls: 'ok-badge--idle' }
  if (via === 'executable') return { label: '已安装', cls: 'ok-badge--ok' }
  if (r.available) return { label: '已安装', cls: 'ok-badge--ok' }
  if (r.unavailableReason?.includes('not_found')) return { label: '未安装', cls: 'ok-badge--err' }
  if (r.unavailableReason) return { label: '不可用', cls: 'ok-badge--err' }
  return { label: '未探测', cls: 'ok-badge--idle' }
}

const productRuntimes = computed(() => runtimes.value.filter(isProductRuntime))
const installedRuntimes = computed(() => productRuntimes.value.filter((r) => r.available))
const missingRuntimes = computed(() => productRuntimes.value.filter((r) => !r.available))

/** 停止会话 Record SSE。 */
let stopSessionStream: (() => void) | null = null

function capsForRuntime(runtimeId?: string | null): string[] {
  if (!runtimeId) return []
  const hit = runtimes.value.find((r) => r.runtimeId === runtimeId)
  return hit?.capabilities ?? []
}

/**
 * Steer（会话输入）：若 Runtime summary 已声明 capabilities，则须含 session 或 prompt；
 * 未声明（旧 Cli / 无 RuntimeId）则放行并允许尝试。
 */
function canSteer(runtimeId?: string | null): { ok: boolean; reason: string } {
  if (!runtimeId) return { ok: true, reason: '' }
  const caps = capsForRuntime(runtimeId)
  if (!caps.length) return { ok: true, reason: '' }
  if (caps.includes('session') || caps.includes('prompt')) return { ok: true, reason: '' }
  return { ok: false, reason: `能力 [${caps.join(', ')}] 不支持 Steer/续接` }
}

function canAbort(runtimeId?: string | null): { ok: boolean; reason: string } {
  if (!runtimeId) return { ok: true, reason: '' }
  const caps = capsForRuntime(runtimeId)
  if (!caps.length) return { ok: true, reason: '' }
  if (caps.includes('abort')) return { ok: true, reason: '' }
  return { ok: false, reason: `能力 [${caps.join(', ')}] 不含 abort` }
}

const activeSession = computed(() =>
  sessions.value.find((s) => s.sessionKey === activeSessionKey.value) ?? null,
)
const activeSteer = computed(() => canSteer(activeSession.value?.runtimeId))
const activeAbort = computed(() => canAbort(activeSession.value?.runtimeId))

const submitting = ref(false)

/** 可委派的 agent（启用的）。 */
const delegatableAgents = computed(() => agents.value.filter((a) => a.enabled))

/** 当前选中 agent 的默认工作目录，用于占位提示。 */
const selectedAgent = computed(() => delegatableAgents.value.find((a) => a.id === delegate.agentId))

async function submitDelegate() {
  if (!delegate.prompt.trim()) {
    setMsg('请填写委派给外部 agent 的提示词', 'err')
    return
  }
  if (delegate.agentId == null) {
    setMsg('请选择目标 agent', 'err')
    return
  }
  submitting.value = true
  try {
    const sessionKey = delegate.sessionKey.trim() || undefined
    const task = await createTask({
      agentId: delegate.agentId,
      prompt: delegate.prompt.trim(),
      cwd: delegate.cwd.trim() || undefined,
      permissionMode: delegate.permissionMode,
      sessionKey,
      sessionRef: sessionKey,
      createdBy: 'ui',
      wait: false,
    })
    const sk = task?.sessionKey ?? task?.sessionRef
    setMsg(
      sk
        ? `任务 ${task?.taskKey ?? ''} 已入队（SessionKey ${sk}）`
        : `任务 ${task?.taskKey ?? ''} 已入队`,
    )
    delegate.prompt = ''
    // 切到任务台并自动选中新任务
    tab.value = 'tasks'
    await loadTasks()
    if (task?.id != null) selectTask(task.id)
  } catch (e) {
    setMsg(`发起失败：${(e as Error).message}`, 'err')
  } finally {
    submitting.value = false
  }
}

// ────────────────────────────── 任务台 ──────────────────────────────

const tasks = ref<TaskDto[]>([])
const stats = ref<Record<string, number>>({})
const statusFilter = ref('')
const activeTaskId = ref<number | null>(null)
const events = ref<TaskEventDto[]>([])
const activeTask = ref<TaskDto | null>(null)
const pendingPermissions = ref<PendingPermissionDto[]>([])

/** 停止函数：切换任务/卸载时调用，防 SSE 连接泄漏。 */
let stopStream: (() => void) | null = null

const STATUS_FILTERS = [
  { value: '', label: '全部' },
  { value: 'Queued', label: '排队中' },
  { value: 'Running', label: '执行中' },
  { value: 'AwaitingPermission', label: '待审批' },
  { value: 'Succeeded', label: '成功' },
  { value: 'Failed', label: '失败' },
] as const

async function loadTasks() {
  try {
    tasks.value = (await listTasks(statusFilter.value || undefined)) ?? []
    stats.value = (await taskStats()) ?? {}
  } catch (e) {
    setMsg(`加载任务失败：${(e as Error).message}`, 'err')
  }
}

/** 选中任务：拉历史事件 → 起 SSE 续读。 */
async function selectTask(id: number) {
  stopStream?.()
  stopStream = null
  activeTaskId.value = id
  events.value = []
  activeTask.value = tasks.value.find((t) => t.id === id) ?? null

  try {
    // 先补一次历史（SSE 也会补，但先渲染出内容体验更好）
    events.value = (await listTaskEvents(id, 0)) ?? []
    await refreshPending(id)
  } catch (e) {
    setMsg(`加载事件失败：${(e as Error).message}`, 'err')
  }

  // 终态任务不必再订阅流（不会有新事件）
  const st = activeTask.value?.status ?? ''
  if (['Succeeded', 'Failed', 'Cancelled', 'Timeout', 'Interrupted'].includes(st)) return

  // G1：带已有水位续读；SSE 断线时 onDone 收到 StreamError，这里做一次退化重连
  const from = events.value.length ? events.value[events.value.length - 1].seq + 1 : 0
  startStream(id, from)
}

function startStream(id: number, fromSeq: number) {
  stopStream = streamTaskEvents(
    id,
    fromSeq,
    (evt) => {
      // 去重：SSE 补历史时可能与已渲染的重叠
      if (!events.value.some((e) => e.seq === evt.seq)) events.value.push(evt)
      // 权限申请事件到达 → 立刻刷新待审批列表（审批台要即时可见）
      if (evt.type === 'PermissionRequest') void refreshPending(id)
    },
    (payload) => {
      stopStream = null
      if (payload.status === 'StreamError') {
        // 网络抖动：退避重连一次（seq 已推进，不丢事件）
        const last = events.value.length ? events.value[events.value.length - 1].seq + 1 : fromSeq
        window.setTimeout(() => {
          if (activeTaskId.value === id) startStream(id, last)
        }, 2000)
        return
      }
      // 任务终态：刷新任务与统计，审批台清空
      void refreshTask(id)
    },
  )
}

async function refreshTask(id: number) {
  try {
    const fresh = await listTasks(statusFilter.value || undefined)
    tasks.value = fresh ?? []
    activeTask.value = tasks.value.find((t) => t.id === id) ?? activeTask.value
    stats.value = (await taskStats()) ?? {}
    await refreshPending(id)
  } catch {
    /* 刷新失败不打断界面 */
  }
}

async function refreshPending(id: number) {
  try {
    pendingPermissions.value = (await listPendingPermissions(id)) ?? []
  } catch {
    pendingPermissions.value = []
  }
}

async function doCancel(t: TaskDto) {
  if (t.id == null) return
  try {
    await cancelTask(t.id)
    setMsg(`已请求取消 ${t.taskKey}`)
    await refreshTask(t.id)
  } catch (e) {
    setMsg(`取消失败：${(e as Error).message}`, 'err')
  }
}

async function decidePermission(p: PendingPermissionDto, allowed: boolean) {
  if (activeTaskId.value == null) return
  try {
    await resolvePermission(activeTaskId.value, p.requestId, allowed)
    setMsg(allowed ? `已允许 ${p.kind}（仅本次）` : `已拒绝 ${p.kind}`)
    await refreshPending(activeTaskId.value)
  } catch (e) {
    setMsg(`裁决失败：${(e as Error).message}`, 'err')
  }
}

// ────────────────────────────── 通用 ──────────────────────────────

function setMsg(text: string, type: 'ok' | 'err' = 'ok') {
  message.value = text
  messageType.value = type
}

/** 状态中文名（与后端 TaskStatus 常量一一对应，改动须双端同步）。 */
function statusLabel(status?: string): string {
  const map: Record<string, string> = {
    Queued: '排队中',
    Running: '执行中',
    AwaitingPermission: '待审批',
    Succeeded: '成功',
    Failed: '失败',
    Cancelled: '已取消',
    Timeout: '超时',
    Interrupted: '被中断',
  }
  return status ? (map[status] ?? status) : '—'
}

/** 状态徽标样式后缀。 */
function statusClass(status?: string): string {
  if (status === 'Succeeded') return 'ok-badge--ok'
  if (status === 'Running') return 'ok-badge--run'
  if (status === 'AwaitingPermission') return 'ok-badge--wait'
  if (status === 'Queued') return 'ok-badge--idle'
  return 'ok-badge--bad'
}

/** 耗时展示（秒）。 */
function elapsedText(ms?: number): string {
  if (!ms || ms <= 0) return '—'
  return ms < 1000 ? `${ms}ms` : `${(ms / 1000).toFixed(1)}s`
}

async function loadRuntimeSurface() {
  loadingSessions.value = true
  try {
    const raw = (await listRuntimes()) ?? []
    // Belt-and-suspenders: never show scripted on product UI even if API regresses.
    runtimes.value = raw.filter(isProductRuntime)
    sessions.value = (await listSessions()) ?? []
  } catch (e) {
    setMsg(`加载 Runtime/Session 失败：${(e as Error).message}`, 'err')
  } finally {
    loadingSessions.value = false
  }
}

function toggleRuntimeDetail(runtimeId: string) {
  runtimeDetailOpen[runtimeId] = !runtimeDetailOpen[runtimeId]
}

async function runRuntimeProbe(runtimeId?: string) {
  if (!runtimeId) {
    setMsg('RuntimeId 为空，无法探测', 'err')
    return
  }
  if (probingAll.value) return
  runtimeBusyId.value = runtimeId
  runtimeProbeStatus[runtimeId] = 'running'
  delete runtimeProbeErrors[runtimeId]
  try {
    const r = await probeRuntime(runtimeId)
    runtimeProbeMap[runtimeId] = r
    runtimeDetailOpen[runtimeId] = true
    runtimeProbeStatus[runtimeId] = 'ok'
    setMsg(`已探测 ${runtimeId}`)
  } catch (e) {
    const msg = (e as Error).message
    runtimeProbeStatus[runtimeId] = 'err'
    runtimeProbeErrors[runtimeId] = msg
    setMsg(`探测失败：${msg}`, 'err')
  } finally {
    runtimeBusyId.value = null
  }
}

/**
 * 对当前产品面 Runtime 列表一键探测：并发上限 PROBE_ALL_CONCURRENCY，
 * 逐条复用 POST …/runtimes/{id}/probe；与「刷新」（只 list）分离。
 */
async function runProbeAllRuntimes() {
  if (probingAll.value) return
  const ids = productRuntimes.value
    .map((r) => r.runtimeId)
    .filter((id): id is string => !!id && id.trim().length > 0)
  if (!ids.length) {
    setMsg('暂无 Runtime 可探测（先刷新列表或启动 OAR Worker）', 'err')
    return
  }
  probingAll.value = true
  probeAllProgress.value = { done: 0, total: ids.length, ok: 0, err: 0 }
  for (const id of ids) {
    runtimeProbeStatus[id] = 'idle'
    delete runtimeProbeErrors[id]
  }

  await mapPool(ids, PROBE_ALL_CONCURRENCY, async (id) => {
    runtimeProbeStatus[id] = 'running'
    try {
      const r = await probeRuntime(id)
      runtimeProbeMap[id] = r
      runtimeDetailOpen[id] = true
      runtimeProbeStatus[id] = 'ok'
      probeAllProgress.value.ok += 1
    } catch (e) {
      const msg = (e as Error).message
      runtimeProbeStatus[id] = 'err'
      runtimeProbeErrors[id] = msg
      probeAllProgress.value.err += 1
    } finally {
      probeAllProgress.value.done += 1
    }
  })

  probingAll.value = false
  const summary = formatProbeAllMessage(probeAllProgress.value)
  setMsg(summary.text, summary.level === 'ok' ? undefined : 'err')
}

function probeStatusLabel(runtimeId?: string | null): string {
  if (!runtimeId) return ''
  const st = runtimeProbeStatus[runtimeId]
  if (st === 'running') return '探测中…'
  if (st === 'ok') return '已探测'
  if (st === 'err') return '失败'
  return ''
}

async function runRuntimeModels(runtimeId?: string) {
  if (!runtimeId) {
    setMsg('RuntimeId 为空，无法拉模型', 'err')
    return
  }
  runtimeBusyId.value = runtimeId
  try {
    const r = await listRuntimeModels(runtimeId)
    runtimeModelsMap[runtimeId] = r
    runtimeDetailOpen[runtimeId] = true
    setMsg(`已拉取模型 ${runtimeId}`)
  } catch (e) {
    setMsg(`模型失败：${(e as Error).message}`, 'err')
  } finally {
    runtimeBusyId.value = null
  }
}

async function openInstallConfirm(runtimeId?: string) {
  if (!runtimeId) {
    setMsg('RuntimeId 为空', 'err')
    return
  }
  runtimeBusyId.value = runtimeId
  try {
    const plan = await getRuntimeInstallPlan(runtimeId)
    installTarget.value = {
      runtimeId,
      planJson: plan?.planJson ?? JSON.stringify({ reason: 'empty_plan' }),
    }
  } catch (e) {
    setMsg(`获取安装计划失败：${(e as Error).message}`, 'err')
  } finally {
    runtimeBusyId.value = null
  }
}

async function confirmInstall() {
  const target = installTarget.value
  if (!target) return
  installBusy.value = true
  try {
    const result = await installRuntime(target.runtimeId, true)
    setMsg(`安装已执行：${(result?.resultJson ?? '').slice(0, 200)}`)
    installTarget.value = null
    await runRuntimeProbe(target.runtimeId)
  } catch (e) {
    setMsg(`安装失败：${(e as Error).message}`, 'err')
  } finally {
    installBusy.value = false
  }
}

async function selectSession(sessionKey?: string) {
  stopSessionStream?.()
  stopSessionStream = null
  if (!sessionKey) {
    activeSessionKey.value = null
    sessionRecords.value = []
    return
  }
  activeSessionKey.value = sessionKey
  sessionRecords.value = []
  try {
    sessionRecords.value = (await listSessionRecords(sessionKey)) ?? []
    const from = sessionRecords.value.length
      ? (sessionRecords.value[sessionRecords.value.length - 1].agentHubSequence ?? 0) + 1
      : 0
    stopSessionStream = streamSessionRecords(
      sessionKey,
      from,
      (rec) => {
        sessionRecords.value = [...sessionRecords.value, rec]
      },
      () => {
        /* done / error：保留已有 records */
      },
    )
  } catch (e) {
    setMsg(`加载会话 Record 失败：${(e as Error).message}`, 'err')
  }
}

async function runSessionInput() {
  const key = activeSessionKey.value
  if (!key) return
  const steer = canSteer(activeSession.value?.runtimeId)
  if (!steer.ok) {
    setMsg(steer.reason || '不支持 Steer', 'err')
    return
  }
  const text = sessionInputText.value.trim()
  if (!text) {
    setMsg('请输入要投递的内容', 'err')
    return
  }
  sessionBusy.value = true
  try {
    const r = await sendSessionInput(key, text)
    setMsg(r?.accepted ? `已投递（${r.code ?? 'ok'}）` : `投递被拒：${r?.reason ?? r?.code ?? 'rejected'}`, r?.accepted ? 'ok' : 'err')
    sessionInputText.value = ''
    sessionRecords.value = (await listSessionRecords(key)) ?? sessionRecords.value
  } catch (e) {
    setMsg(`投递失败：${(e as Error).message}`, 'err')
  } finally {
    sessionBusy.value = false
  }
}

async function runSessionAbort() {
  const key = activeSessionKey.value
  if (!key) return
  const ab = canAbort(activeSession.value?.runtimeId)
  if (!ab.ok) {
    setMsg(ab.reason || '不支持 abort', 'err')
    return
  }
  sessionBusy.value = true
  try {
    const r = await abortSession(key)
    setMsg(r?.accepted ? `已 abort（${r.code ?? 'ok'}）` : `abort 被拒：${r?.reason ?? r?.code}`, r?.accepted ? 'ok' : 'err')
    await loadRuntimeSurface()
  } catch (e) {
    setMsg(`abort 失败：${(e as Error).message}`, 'err')
  } finally {
    sessionBusy.value = false
  }
}

async function runSessionClose() {
  const key = activeSessionKey.value
  if (!key) return
  sessionBusy.value = true
  try {
    await closeSession(key)
    setMsg(`会话 ${key} 已关闭`)
    stopSessionStream?.()
    stopSessionStream = null
    activeSessionKey.value = null
    sessionRecords.value = []
    await loadRuntimeSurface()
  } catch (e) {
    setMsg(`关闭失败：${(e as Error).message}`, 'err')
  } finally {
    sessionBusy.value = false
  }
}

onMounted(async () => {
  pluginVersion.value = await fetchPluginVersion('agent-hub')
  await loadAgents()
  await loadTasks()
  await loadScanDirs()
})

onBeforeUnmount(() => {
  // 铁律：卸载必须停流，否则宿主切页后仍有一条 SSE 长连接挂着
  stopStream?.()
  stopStream = null
  stopSessionStream?.()
  stopSessionStream = null
})
</script>

<template>
  <div class="ok ok-root">
    <header class="ok-header">
      <div>
        <div class="ok-eyebrow">AGENT HUB · 中枢调度</div>
        <div class="ok-title">Agent 中枢 <span v-if="pluginVersion" class="ok-version">v{{ pluginVersion }}</span></div>
        <div class="ok-subtitle">
          登记本机的其它 agent，选路后把任务委派给它，并收回可复盘的结果。CLI 只是交互口之一。
        </div>
      </div>
      <div class="ok-stats">
        <span class="ok-stat"><b>{{ agents.length }}</b> 已登记</span>
        <span class="ok-stat"><b>{{ stats.Running ?? 0 }}</b> 执行中</span>
        <span class="ok-stat" :class="{ 'ok-stat--alert': (stats.PendingApprovals ?? 0) > 0 }">
          <b>{{ stats.PendingApprovals ?? 0 }}</b> 待审批
        </span>
      </div>
    </header>

    <div v-if="message" class="ok-alert" :class="messageType === 'err' ? 'ok-alert--err' : 'ok-alert--ok'">
      {{ message }}
    </div>

    <nav class="ok-tabs">
      <button class="ok-tab" :class="{ 'ok-tab--on': tab === 'agents' }" @click="tab = 'agents'">
        Agent 目录
      </button>
      <button class="ok-tab" :class="{ 'ok-tab--on': tab === 'delegate' }" @click="tab = 'delegate'">
        发起委派
      </button>
      <button class="ok-tab" :class="{ 'ok-tab--on': tab === 'tasks' }" @click="tab = 'tasks'">
        任务台
      </button>
      <button
        class="ok-tab"
        :class="{ 'ok-tab--on': tab === 'runtimes' }"
        @click="tab = 'runtimes'; loadRuntimeSurface()"
      >
        Runtime 检测
      </button>
      <button
        class="ok-tab"
        :class="{ 'ok-tab--on': tab === 'sessions' }"
        @click="tab = 'sessions'; loadRuntimeSurface()"
      >
        会话
      </button>
    </nav>

    <!-- ─────────── Agent 目录 ─────────── -->
    <section v-show="tab === 'agents'" class="ok-panel">
      <div class="ok-toolbar">
        <button class="ok-btn ok-btn--primary" @click="openCreate">登记新 Agent</button>
        <button class="ok-btn" :disabled="discovering" @click="runDiscover">
          {{ discovering ? '扫描中…' : '扫描本机' }}
        </button>
        <button class="ok-btn" :disabled="loadingAgents" @click="loadAgents">刷新</button>
        <span class="ok-hint">扫描只列出候选，不会自动登记——登记才写入注册表。</span>
      </div>

      <!-- 附加扫描目录：探测时除 PATH 外额外查找（装在不进 PATH 目录的 CLI 在这里补上） -->
      <div class="ok-scan-dirs">
        <div class="ok-scan-dirs__head">
          <span class="ok-scan-dirs__label">附加扫描目录</span>
          <span class="ok-hint">每行一个，探测时除 PATH 外额外查找；保存后点「扫描本机」即生效。</span>
          <button class="ok-btn ok-btn--sm" :disabled="savingDirs" @click="saveScanDirs">
            {{ savingDirs ? '保存中…' : '保存' }}
          </button>
        </div>
        <textarea
          class="ok-input ok-textarea ok-scan-dirs__input"
          v-model="searchDirsText"
          rows="3"
          placeholder="例如：C:\Users\me\.local\bin"
        ></textarea>
      </div>

      <div v-if="discovered.length" class="ok-discovered">
        <div class="ok-discovered__title">扫描到 {{ discovered.length }} 个未登记的 agent</div>
        <div class="ok-discovered__row" v-for="(d, i) in discovered" :key="`${d.vendor ?? 'v'}-${d.executable ?? i}`">
          <span class="ok-discovered__name">{{ d.displayName || d.vendor }}</span>
          <code class="ok-code">{{ d.executable }}</code>
          <span v-if="d.version" class="ok-hint">{{ d.version }}</span>
          <button class="ok-btn ok-btn--sm" @click="registerDiscovered(d)">登记</button>
        </div>
      </div>

      <AgentHubAgentForm
        v-if="showForm"
        :agents="agents"
        :editing-id="editingId"
        @submit="submitForm"
        @cancel="showForm = false"
      />

      <div v-if="!agents.length && !loadingAgents" class="ok-empty">
        还没有登记任何 agent。点「扫描本机」自动发现，或点「登记新 Agent」手工填写。
      </div>

      <AgentHubAgentCard
        v-for="a in agents"
        :key="a.id"
        :agent="a"
        :probing="probingId === a.id"
        @edit="openEdit(a)"
        @remove="removeAgent(a)"
        @probe="runProbe(a)"
        @trust="openTrust(a)"
        @revoke="revokeTrust(a)"
      />
    </section>

    <!-- ─────────── 发起委派 ─────────── -->
    <section v-show="tab === 'delegate'" class="ok-panel">
      <div v-if="!delegatableAgents.length" class="ok-empty">
        没有启用的 agent。先到「Agent 目录」登记并启用一个。
      </div>

      <div v-else class="ok-form">
        <label class="ok-form__label">目标 Agent</label>
        <select class="ok-input" v-model.number="delegate.agentId">
          <option :value="null">请选择…</option>
          <option v-for="a in delegatableAgents" :key="a.id" :value="a.id">
            {{ a.name }}{{ a.trusted ? '（已授信）' : '' }}
          </option>
        </select>

        <label class="ok-form__label">提示词</label>
        <textarea
          class="ok-input ok-textarea"
          v-model="delegate.prompt"
          rows="6"
          placeholder="把要它做的事描述清楚：目标、约束、期望产出。"
        ></textarea>

        <label class="ok-form__label">工作目录</label>
        <input
          class="ok-input"
          v-model="delegate.cwd"
          :placeholder="selectedAgent?.defaultCwd || '留空使用该 agent 的默认目录'"
        />

        <label class="ok-form__label">权限模式</label>
        <select class="ok-input" v-model="delegate.permissionMode">
          <option value="read-only">read-only（只读，最安全）</option>
          <option value="workspace-write">workspace-write（可写工作区）</option>
          <option value="accept-edits">accept-edits（自动接受编辑）</option>
        </select>

        <label class="ok-form__label">SessionKey（可选，续接）</label>
        <input
          class="ok-input"
          v-model="delegate.sessionKey"
          placeholder="留空开新会话；填入上次任务返回的 SessionKey 以续接"
        />

        <div class="ok-form__note">
          默认只读。越权的写操作会被外部 agent 转成权限申请，回到「任务台 → 审批台」等你在场裁决；
          对单个 agent 显式授信（带范围）后才自动放行。对外只使用 SessionKey，不暴露内部会话 id。
        </div>

        <div class="ok-form__actions">
          <button class="ok-btn ok-btn--primary" :disabled="submitting" @click="submitDelegate">
            {{ submitting ? '提交中…' : '发起委派' }}
          </button>
          <span class="ok-hint">任务在后台执行，界面会自动切到任务台跟随事件流。</span>
        </div>
      </div>
    </section>

    <!-- ─────────── 任务台 ─────────── -->
    <section v-show="tab === 'tasks'" class="ok-panel">
      <div class="ok-toolbar">
        <select class="ok-input ok-input--inline" v-model="statusFilter" @change="loadTasks">
          <option v-for="f in STATUS_FILTERS" :key="f.value" :value="f.value">{{ f.label }}</option>
        </select>
        <button class="ok-btn" @click="loadTasks">刷新</button>
        <span class="ok-hint">共 {{ tasks.length }} 条</span>
      </div>

      <div v-if="!tasks.length" class="ok-empty">还没有委派记录。</div>

      <div v-else class="ok-split">
        <ul class="ok-tasklist">
          <li
            v-for="t in tasks"
            :key="t.id"
            class="ok-task"
            :class="{ 'ok-task--on': t.id === activeTaskId }"
            @click="t.id != null && selectTask(t.id)"
          >
            <div class="ok-task__top">
              <span class="ok-badge" :class="statusClass(t.status)">{{ statusLabel(t.status) }}</span>
              <span class="ok-task__key">{{ t.taskKey }}</span>
            </div>
            <div class="ok-task__prompt">{{ t.prompt }}</div>
            <div class="ok-task__meta">
              {{ t.agentName || ('agent #' + t.agentId) }} · {{ elapsedText(t.elapsedMs) }}
            </div>
          </li>
        </ul>

        <div class="ok-detail">
          <div v-if="!activeTask" class="ok-empty">选一条任务看事件流。</div>

          <template v-else>
            <div class="ok-detail__head">
              <span class="ok-badge" :class="statusClass(activeTask.status)">
                {{ statusLabel(activeTask.status) }}
              </span>
              <code class="ok-code">{{ activeTask.taskKey }}</code>
              <code
                v-if="activeTask.sessionKey || activeTask.sessionRef"
                class="ok-code"
                title="SessionKey"
              >{{ activeTask.sessionKey || activeTask.sessionRef }}</code>
              <button
                v-if="['Queued', 'Running', 'AwaitingPermission'].includes(activeTask.status ?? '')"
                class="ok-btn ok-btn--sm ok-btn--danger"
                @click="doCancel(activeTask)"
              >
                取消任务
              </button>
            </div>

            <!-- 审批台：人在回路 -->
            <div v-if="pendingPermissions.length" class="ok-approvals">
              <div class="ok-approvals__title">待审批 {{ pendingPermissions.length }} 项</div>
              <div class="ok-approval" v-for="p in pendingPermissions" :key="p.requestId">
                <div class="ok-approval__body">
                  <span class="ok-approval__kind">{{ p.kind }}</span>
                  <span class="ok-approval__detail">{{ p.detail || '（无详情）' }}</span>
                </div>
                <div class="ok-approval__actions">
                  <button class="ok-btn ok-btn--sm ok-btn--primary" @click="decidePermission(p, true)">
                    允许本次
                  </button>
                  <button class="ok-btn ok-btn--sm" @click="decidePermission(p, false)">拒绝</button>
                </div>
              </div>
            </div>

            <AgentHubEventStream :events="events" />

            <div v-if="activeTask.resultText" class="ok-result">
              <div class="ok-result__title">结果</div>
              <pre class="ok-result__body">{{ activeTask.resultText }}</pre>
            </div>
          </template>
        </div>
      </div>
    </section>

    <!-- ─────────── Runtime 检测（仅库存；会话见独立页签） ─────────── -->
    <section v-show="tab === 'runtimes'" class="ok-panel ok-panel--stack">
      <div class="ok-toolbar">
        <button class="ok-btn" :disabled="loadingSessions" @click="loadRuntimeSurface">
          {{ loadingSessions ? '加载中…' : '刷新' }}
        </button>
        <span class="ok-hint">
          已装 {{ installedRuntimes.length }} · 未装/不可用 {{ missingRuntimes.length }}
          · 会话请到「会话」页签（{{ sessions.length }}）
        </span>
      </div>

      <div class="ok-section">
        <div class="ok-section__head ok-section__head--row">
          <div class="ok-section__head-text">
            <h3 class="ok-section__title">Runtime 库存</h3>
            <p class="ok-section__desc">
              本机真实安装态（OAR installation）。仅 <code class="ok-code">via=executable</code> 标「已安装」；
              未装显示 not_found，不假装成功。测试用 scripted 不在此列表。
              「一键探测」会对列表逐条真实 probe；与上方「刷新」（只重拉列表）不同。
              Steer / Abort / Records 在「会话」页签。
            </p>
          </div>
          <div class="ok-section__head-actions">
            <button
              class="ok-btn ok-btn--primary"
              :disabled="probingAll || loadingSessions || !productRuntimes.length"
              :title="productRuntimes.length ? '对当前列表全部 Runtime 调用 probe（并发上限 3）' : '暂无 Runtime'"
              @click="runProbeAllRuntimes"
            >
              {{ probingAll
                ? `探测中… ${probeAllProgress.done}/${probeAllProgress.total}`
                : '一键探测' }}
            </button>
            <span
              v-if="probeAllProgress.total > 0 && !probingAll"
              class="ok-hint"
            >
              上次：{{ probeAllProgress.ok }} 成功
              <template v-if="probeAllProgress.err"> · {{ probeAllProgress.err }} 失败</template>
            </span>
          </div>
        </div>

        <div v-if="!productRuntimes.length" class="ok-empty">
          暂无 Runtime（启动 OAR Worker 后点刷新）。委派请先到「Agent 目录」登记。
        </div>

        <ul v-else class="ok-runtime-grid">
          <li
            v-for="(r, i) in productRuntimes"
            :key="(r.runtimeId || '') + '-' + i"
            class="ok-runtime-card"
            :class="{
              'ok-runtime-card--ok': r.available,
              'ok-runtime-card--probing': r.runtimeId && runtimeProbeStatus[r.runtimeId] === 'running',
              'ok-runtime-card--probe-err': r.runtimeId && runtimeProbeStatus[r.runtimeId] === 'err',
            }"
          >
            <div class="ok-runtime-card__head">
              <RuntimeBrandIcon
                :runtime-id="r.runtimeId"
                :vendor="r.vendor"
                :name="r.name"
                :size="36"
              />
              <div class="ok-runtime-card__ident">
                <div class="ok-runtime-card__name">{{ r.name || r.runtimeId }}</div>
                <div class="ok-runtime-card__meta">
                  <code class="ok-code">{{ r.runtimeId || '—' }}</code>
                  <span>· {{ r.transport || '—' }}</span>
                </div>
              </div>
              <div class="ok-runtime-card__badges">
                <span
                  v-if="r.runtimeId && probeStatusLabel(r.runtimeId)"
                  class="ok-badge"
                  :class="{
                    'ok-badge--busy': runtimeProbeStatus[r.runtimeId!] === 'running',
                    'ok-badge--ok': runtimeProbeStatus[r.runtimeId!] === 'ok',
                    'ok-badge--err': runtimeProbeStatus[r.runtimeId!] === 'err',
                  }"
                >{{ probeStatusLabel(r.runtimeId) }}</span>
                <span class="ok-badge" :class="installBadge(r).cls">{{ installBadge(r).label }}</span>
              </div>
            </div>
            <p v-if="r.unavailableReason" class="ok-runtime-card__reason">{{ r.unavailableReason }}</p>
            <p
              v-if="r.runtimeId && runtimeProbeErrors[r.runtimeId]"
              class="ok-runtime-card__reason ok-runtime-card__reason--err"
            >{{ runtimeProbeErrors[r.runtimeId] }}</p>
            <div class="ok-runtime-card__actions">
              <button
                class="ok-btn ok-btn--sm"
                :disabled="!r.runtimeId || probingAll || runtimeBusyId === r.runtimeId"
                @click="runRuntimeProbe(r.runtimeId)"
              >检查</button>
              <button
                class="ok-btn ok-btn--sm"
                :disabled="!r.runtimeId || probingAll || runtimeBusyId === r.runtimeId"
                @click="runRuntimeModels(r.runtimeId)"
              >模型</button>
              <button
                class="ok-btn ok-btn--sm"
                :disabled="!r.runtimeId || probingAll || runtimeBusyId === r.runtimeId || r.available"
                :title="r.available ? '已安装，无需安装' : '查看安装计划（须确认才执行）'"
                @click="openInstallConfirm(r.runtimeId)"
              >安装</button>
              <button
                v-if="r.runtimeId && (runtimeProbeMap[r.runtimeId] || runtimeModelsMap[r.runtimeId])"
                class="ok-btn ok-btn--sm"
                @click="toggleRuntimeDetail(r.runtimeId!)"
              >{{ runtimeDetailOpen[r.runtimeId!] ? '收起详情' : '展开详情' }}</button>
            </div>
            <div
              v-if="r.runtimeId && runtimeDetailOpen[r.runtimeId] && runtimeProbeMap[r.runtimeId]"
              class="ok-result ok-result--compact"
            >
              <div class="ok-result__title">installation</div>
              <pre class="ok-result__body">{{ runtimeProbeMap[r.runtimeId]?.installationJson || '—' }}</pre>
              <div class="ok-result__title">auth</div>
              <pre class="ok-result__body">{{
                runtimeProbeMap[r.runtimeId]?.authError
                  ? ('错误: ' + runtimeProbeMap[r.runtimeId]?.authError)
                  : (runtimeProbeMap[r.runtimeId]?.authJson || '—')
              }}</pre>
            </div>
            <div
              v-if="r.runtimeId && runtimeDetailOpen[r.runtimeId] && runtimeModelsMap[r.runtimeId]"
              class="ok-result ok-result--compact"
            >
              <div class="ok-result__title">models</div>
              <pre class="ok-result__body">{{ runtimeModelsMap[r.runtimeId]?.payloadJson || '—' }}</pre>
            </div>
          </li>
        </ul>
      </div>
    </section>

    <!-- ─────────── 会话（独立页签 · 不含 Runtime 长列表） ─────────── -->
    <section v-show="tab === 'sessions'" class="ok-panel ok-panel--stack">
      <div class="ok-toolbar">
        <button class="ok-btn" :disabled="loadingSessions" @click="loadRuntimeSurface">
          {{ loadingSessions ? '加载中…' : '刷新' }}
        </button>
        <span class="ok-hint">Session {{ sessions.length }} · Runtime 检测请切到「Runtime 检测」页签</span>
      </div>

      <div class="ok-section">
        <div class="ok-section__head">
          <h3 class="ok-section__title">会话</h3>
          <p class="ok-section__desc">
            选会话 → Steer / Abort / 关闭。对外只展示 SessionKey，不暴露内部 OarSessionId。
          </p>
        </div>

        <div v-if="!sessions.length" class="ok-empty">
          还没有持久化会话。从「发起委派」对已装 Runtime（如 Grok）开新任务后会出现在这里。
        </div>

        <div v-else class="ok-split">
          <ul class="ok-tasklist">
            <li
              v-for="s in sessions"
              :key="s.sessionKey"
              class="ok-task"
              :class="{ 'ok-task--on': s.sessionKey === activeSessionKey }"
              @click="selectSession(s.sessionKey)"
            >
              <div class="ok-task__top">
                <RuntimeBrandIcon :runtime-id="s.runtimeId" :name="s.runtimeId" :size="22" />
                <span class="ok-badge">{{ s.status || '—' }}</span>
                <code class="ok-code">{{ s.sessionKey }}</code>
              </div>
              <div class="ok-task__meta">
                agent #{{ s.agentId }} · {{ s.transport || '—' }} · {{ s.runtimeId || '—' }}
                · gen {{ s.generation ?? 0 }}
              </div>
              <div class="ok-form__actions">
                <button
                  class="ok-btn ok-btn--sm"
                  @click.stop="delegate.sessionKey = s.sessionKey || ''; tab = 'delegate'"
                >
                  用此 SessionKey 委派
                </button>
              </div>
            </li>
          </ul>

          <div class="ok-detail" v-if="activeSession">
            <div class="ok-detail__head">
              <RuntimeBrandIcon
                :runtime-id="activeSession.runtimeId"
                :name="activeSession.runtimeId"
                :size="28"
              />
              <span class="ok-badge">{{ activeSession.status || '—' }}</span>
              <code class="ok-code">{{ activeSession.sessionKey }}</code>
            </div>
            <div class="ok-hint" v-if="!activeSteer.ok">Steer 禁用：{{ activeSteer.reason }}</div>
            <div class="ok-hint" v-if="!activeAbort.ok">Abort 禁用：{{ activeAbort.reason }}</div>
            <div class="ok-form__row" style="margin-top: 8px">
              <input
                class="ok-input"
                v-model="sessionInputText"
                :disabled="!activeSteer.ok || sessionBusy"
                placeholder="向会话投递输入（Steer）"
                @keyup.enter="runSessionInput"
              />
            </div>
            <div class="ok-form__actions">
              <button
                class="ok-btn ok-btn--sm ok-btn--primary"
                :disabled="!activeSteer.ok || sessionBusy"
                :title="activeSteer.reason || '发送'"
                @click="runSessionInput"
              >发送</button>
              <button
                class="ok-btn ok-btn--sm"
                :disabled="!activeAbort.ok || sessionBusy"
                :title="activeAbort.reason || 'Abort'"
                @click="runSessionAbort"
              >Abort</button>
              <button class="ok-btn ok-btn--sm ok-btn--danger" :disabled="sessionBusy" @click="runSessionClose">关闭会话</button>
            </div>
            <div class="ok-form__label" style="margin-top: 12px">Records / stream</div>
            <div v-if="!sessionRecords.length" class="ok-empty">暂无 Record（已订阅 stream 增量）。</div>
            <ul v-else class="ok-tasklist">
              <li v-for="rec in sessionRecords" :key="(rec.agentHubSequence ?? 0) + '-' + (rec.type || '')" class="ok-task">
                <div class="ok-task__top">
                  <span class="ok-badge">#{{ rec.agentHubSequence }}</span>
                  <span class="ok-task__key">{{ rec.type || 'record' }}</span>
                </div>
                <pre class="ok-result__body">{{ rec.payloadJson || '—' }}</pre>
              </li>
            </ul>
          </div>
          <div v-else class="ok-empty">选一条会话查看详情与操作。</div>
        </div>
      </div>
    </section>

    <!-- 授信对话框 -->
    <div v-if="trustTarget" class="ok-modal" @click.self="trustTarget = null">
      <div class="ok-modal__box">
        <div class="ok-modal__title">授信「{{ trustTarget.name }}」</div>
        <div class="ok-modal__desc">
          授信后，范围内（且仅范围内）的权限申请将自动放行、不再打断你。
          范围之外仍走人工审批。留空范围不予提交——空范围等于放开一切。
        </div>

        <div class="ok-scopes">
          <label v-for="s in SCOPE_OPTIONS" :key="s.value" class="ok-scope">
            <input
              type="checkbox"
              :checked="trustScopes.includes(s.value)"
              @change="toggleScope(s.value)"
            />
            <span>{{ s.label }}</span>
            <code class="ok-code">{{ s.value }}</code>
          </label>
        </div>

        <div class="ok-scope-add">
          <input class="ok-input" v-model="trustDraft" placeholder="自定义范围（如 read_dir）" @keyup.enter="addCustomScope" />
          <button class="ok-btn ok-btn--sm" @click="addCustomScope">加入</button>
        </div>

        <div class="ok-scope-current">
          已选：<code class="ok-code">{{ trustScopes.join(', ') || '（空）' }}</code>
        </div>

        <div class="ok-modal__actions">
          <button class="ok-btn ok-btn--primary" @click="submitTrust">确认授信</button>
          <button class="ok-btn" @click="trustTarget = null">取消</button>
        </div>
      </div>
    </div>

    <!-- 安装确认对话框 -->
    <div v-if="installTarget" class="ok-modal" @click.self="installTarget = null">
      <div class="ok-modal__box">
        <div class="ok-modal__title">确认安装 Runtime「{{ installTarget.runtimeId }}」</div>
        <div class="ok-modal__desc">
          将按下方 installPlan 执行安装。未点确认不会改动本机。
          未装 Runtime 可能返回 not_found / not_supported——不会假装安装成功。
        </div>
        <pre class="ok-result__body">{{ installTarget.planJson }}</pre>
        <div class="ok-modal__actions">
          <button class="ok-btn ok-btn--primary" :disabled="installBusy" @click="confirmInstall">
            {{ installBusy ? '安装中…' : '确认安装' }}
          </button>
          <button class="ok-btn" :disabled="installBusy" @click="installTarget = null">取消</button>
        </div>
      </div>
    </div>
  </div>
</template>

<style>
/* 仅走 --el-* token，不定义独立色值；插件无 Tailwind，布局类自写。
   前缀 ok- 取自「其它 agent 中枢」的语义缩写，避免与宿主类名撞车。 */
.ok-root {
  font-family: var(--el-font-family, system-ui, sans-serif);
  color: var(--el-text-color-primary);
  background: var(--el-bg-color-page);
  padding: 20px;
  box-sizing: border-box;
}
.ok-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 16px;
  margin-bottom: 14px;
  flex-wrap: wrap;
}
.ok-eyebrow {
  font-size: 11px;
  font-weight: 600;
  letter-spacing: 0.12em;
  text-transform: uppercase;
  color: var(--el-text-color-secondary);
  margin-bottom: 6px;
}
.ok-title {
  font-size: 28px;
  font-weight: 600;
  font-family: var(--el-font-family-serif, Georgia, 'Times New Roman', serif);
  line-height: 1.2;
}
.ok-version {
  font-size: 12px;
  font-weight: 500;
  padding: 1px 8px;
  border-radius: 10px;
  background: var(--el-fill-color-light);
  color: var(--el-text-color-secondary);
  vertical-align: middle;
}
.ok-subtitle {
  margin-top: 4px;
  font-size: 13px;
  color: var(--el-text-color-secondary);
  line-height: 1.5;
  max-width: 640px;
}
.ok-stats {
  display: flex;
  gap: 14px;
  font-size: 13px;
  color: var(--el-text-color-secondary);
}
.ok-stat b {
  color: var(--el-text-color-primary);
  font-size: 15px;
  margin-right: 2px;
}
.ok-stat--alert b {
  color: var(--el-color-warning);
}
.ok-alert {
  padding: 10px 14px;
  border-radius: var(--el-border-radius-base, 6px);
  margin-bottom: 14px;
  font-size: 13px;
}
.ok-alert--ok {
  background: var(--el-color-success-light-9);
  color: var(--el-color-success);
  border: 1px solid var(--el-color-success-light-5);
}
.ok-alert--err {
  background: var(--el-color-danger-light-9);
  color: var(--el-color-danger);
  border: 1px solid var(--el-color-danger-light-5);
}
.ok-panel {
  background: var(--el-bg-color, rgba(255, 255, 255, 0.6));
  border: 1px solid var(--el-border-color-light, rgba(255, 255, 255, 0.15));
  border-radius: var(--el-border-radius-lg, 12px);
  padding: 20px;
  backdrop-filter: blur(12px);
}
.ok-panel--stack {
  display: flex;
  flex-direction: column;
  gap: 28px;
}
.ok-panel--stack > * {
  flex-shrink: 0;
}
.ok-section {
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding-top: 4px;
  border-top: 1px solid var(--el-border-color-lighter);
}
.ok-section:first-of-type {
  border-top: none;
  padding-top: 0;
}
.ok-section__head {
  display: flex;
  flex-direction: column;
  gap: 4px;
}
.ok-section__head--row {
  flex-direction: row;
  flex-wrap: wrap;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
}
.ok-section__head-text {
  display: flex;
  flex-direction: column;
  gap: 4px;
  min-width: 0;
  flex: 1 1 240px;
}
.ok-section__head-actions {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
  flex: 0 0 auto;
}
.ok-section__title {
  margin: 0;
  font-size: 16px;
  font-weight: 600;
  font-family: var(--el-font-family-serif, Georgia, 'Times New Roman', serif);
  color: var(--el-text-color-primary);
  letter-spacing: 0.02em;
}
.ok-section__desc {
  margin: 0;
  font-size: 12px;
  line-height: 1.5;
  color: var(--el-text-color-secondary);
  max-width: 720px;
}
.ok-runtime-grid {
  list-style: none;
  margin: 0;
  padding: 0;
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 12px;
}
.ok-runtime-card {
  border: 1px solid var(--el-border-color-light);
  border-radius: var(--el-border-radius-base, 8px);
  background: var(--el-fill-color-blank, var(--el-bg-color));
  padding: 12px 14px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.ok-runtime-card--ok {
  border-color: color-mix(in srgb, var(--el-color-success) 35%, var(--el-border-color-light));
}
.ok-runtime-card--probing {
  border-color: color-mix(in srgb, var(--el-color-primary) 40%, var(--el-border-color-light));
}
.ok-runtime-card--probe-err {
  border-color: color-mix(in srgb, var(--el-color-danger) 40%, var(--el-border-color-light));
}
.ok-runtime-card__badges {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: 4px;
}
.ok-runtime-card__reason--err {
  color: var(--el-color-danger);
}
.ok-runtime-card__head {
  display: flex;
  align-items: flex-start;
  gap: 10px;
}
.ok-runtime-card__ident {
  flex: 1;
  min-width: 0;
}
.ok-runtime-card__name {
  font-size: 14px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  line-height: 1.3;
}
.ok-runtime-card__meta {
  margin-top: 2px;
  font-size: 12px;
  color: var(--el-text-color-secondary);
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  align-items: center;
}
.ok-runtime-card__reason {
  margin: 0;
  font-size: 12px;
  color: var(--el-text-color-secondary);
  line-height: 1.4;
}
.ok-runtime-card__actions {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}
.ok-result--compact {
  margin-top: 4px;
}
.ok-result--compact .ok-result__body {
  max-height: 140px;
  overflow: auto;
}
.ok-tabs {
  display: flex;
  gap: 4px;
  border-bottom: 1px solid var(--el-border-color-lighter);
  margin-bottom: 16px;
}
.ok-tab {
  border: none;
  background: transparent;
  padding: 9px 16px;
  font-size: 14px;
  color: var(--el-text-color-regular);
  cursor: pointer;
  border-bottom: 2px solid transparent;
}
.ok-tab--on {
  color: var(--el-color-primary);
  border-bottom-color: var(--el-color-primary);
  font-weight: 500;
}
.ok-toolbar {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 14px;
  flex-wrap: wrap;
}
.ok-hint {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}
.ok-code {
  font-family: var(--el-font-family-mono, monospace);
  font-size: 12px;
  background: var(--el-fill-color-light);
  padding: 1px 6px;
  border-radius: 4px;
  color: var(--el-text-color-regular);
}
.ok-empty {
  padding: 32px 16px;
  text-align: center;
  font-size: 13px;
  color: var(--el-text-color-secondary);
  background: var(--el-bg-color);
  border: 1px dashed var(--el-border-color-lighter);
  border-radius: var(--el-border-radius-base, 6px);
}
.ok-btn {
  height: 32px;
  padding: 0 14px;
  border-radius: var(--el-border-radius-base, 6px);
  border: 1px solid var(--el-border-color);
  background: var(--el-bg-color);
  color: var(--el-text-color-primary);
  font-size: 13px;
  cursor: pointer;
}
.ok-btn:hover {
  border-color: var(--el-color-primary-light-5);
  color: var(--el-color-primary);
}
.ok-btn--primary {
  background: var(--el-color-primary);
  border-color: var(--el-color-primary);
  /* 主色按钮文字：用 token 反色，禁用裸白色值 —— 主题切换时才能跟着走 */
  color: var(--el-color-white);
}
.ok-btn--primary:hover {
  background: var(--el-color-primary-light-3);
  border-color: var(--el-color-primary-light-3);
  color: var(--el-color-white);
}
.ok-btn--danger {
  border-color: var(--el-color-danger-light-5);
  color: var(--el-color-danger);
}
.ok-btn--danger:hover {
  background: var(--el-color-danger-light-9);
  border-color: var(--el-color-danger);
  color: var(--el-color-danger);
}
.ok-btn--sm {
  height: 26px;
  padding: 0 10px;
  font-size: 12px;
}
.ok-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}
.ok-input {
  height: 32px;
  padding: 0 10px;
  border: 1px solid var(--el-border-color);
  border-radius: var(--el-border-radius-base, 6px);
  background: var(--el-fill-color-blank);
  color: var(--el-text-color-primary);
  font-size: 13px;
  box-sizing: border-box;
  outline: none;
  font-family: inherit;
  width: 100%;
}
.ok-input:focus {
  border-color: var(--el-color-primary);
}
.ok-input--inline {
  width: auto;
  min-width: 130px;
}
.ok-textarea {
  height: auto;
  padding: 8px 10px;
  line-height: 1.6;
  resize: vertical;
}
.ok-badge {
  font-size: 12px;
  padding: 1px 8px;
  border-radius: 10px;
  font-weight: 500;
  white-space: nowrap;
}
.ok-badge--ok {
  background: var(--el-color-success-light-9);
  color: var(--el-color-success);
}
.ok-badge--run {
  background: var(--el-color-primary-light-9);
  color: var(--el-color-primary);
}
.ok-badge--wait {
  background: var(--el-color-warning-light-9);
  color: var(--el-color-warning);
}
.ok-badge--idle {
  background: var(--el-fill-color-light);
  color: var(--el-text-color-secondary);
}
.ok-badge--bad {
  background: var(--el-color-danger-light-9);
  color: var(--el-color-danger);
}
.ok-badge--err {
  background: var(--el-color-danger-light-9);
  color: var(--el-color-danger);
}
.ok-badge--busy {
  background: var(--el-color-primary-light-9);
  color: var(--el-color-primary);
}

/* 附加扫描目录配置 */
.ok-scan-dirs {
  background: var(--el-bg-color);
  border: 1px dashed var(--el-border-color-lighter);
  border-radius: var(--el-border-radius-base, 6px);
  padding: 12px 14px;
  margin-bottom: 14px;
}
.ok-scan-dirs__head {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
  margin-bottom: 8px;
}
.ok-scan-dirs__label {
  font-size: 13px;
  font-weight: 600;
}
.ok-scan-dirs__input {
  font-family: var(--el-font-family-mono, monospace);
  font-size: 12px;
}

/* 扫描候选 */
.ok-discovered {
  background: var(--el-bg-color);
  border: 1px solid var(--el-color-primary-light-7);
  border-radius: var(--el-border-radius-base, 6px);
  padding: 12px 14px;
  margin-bottom: 14px;
}
.ok-discovered__title {
  font-size: 13px;
  font-weight: 600;
  margin-bottom: 8px;
}
.ok-discovered__row {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 6px 0;
  font-size: 13px;
}
.ok-discovered__name {
  min-width: 120px;
  font-weight: 500;
}

/* 表单 */
.ok-form {
  background: var(--el-bg-color);
  border: 1px solid var(--el-border-color-lighter);
  border-radius: var(--el-border-radius-base, 6px);
  padding: 18px;
  max-width: 720px;
}
.ok-form__label {
  display: block;
  font-size: 13px;
  color: var(--el-text-color-regular);
  margin: 12px 0 6px;
}
.ok-form__label:first-child {
  margin-top: 0;
}
.ok-form__note {
  margin-top: 12px;
  padding: 10px 12px;
  font-size: 12px;
  line-height: 1.7;
  color: var(--el-text-color-secondary);
  background: var(--el-fill-color-light);
  border-radius: var(--el-border-radius-base, 6px);
}
.ok-form__actions {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-top: 16px;
}

/* 任务台左右分栏 */
.ok-split {
  display: grid;
  grid-template-columns: 320px 1fr;
  gap: 14px;
  align-items: start;
}
@media (max-width: 900px) {
  .ok-split {
    grid-template-columns: 1fr;
  }
}
.ok-tasklist {
  list-style: none;
  margin: 0;
  padding: 0;
  background: var(--el-bg-color);
  border: 1px solid var(--el-border-color-lighter);
  border-radius: var(--el-border-radius-base, 6px);
  overflow: hidden;
  max-height: 620px;
  overflow-y: auto;
}
.ok-task {
  padding: 10px 12px;
  border-bottom: 1px solid var(--el-border-color-lighter);
  cursor: pointer;
}
.ok-task:last-child {
  border-bottom: none;
}
.ok-task:hover {
  background: var(--el-fill-color-light);
}
.ok-task--on {
  background: var(--el-color-primary-light-9);
  box-shadow: inset 3px 0 0 var(--el-color-primary);
}
.ok-task__top {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 4px;
}
.ok-task__key {
  font-family: var(--el-font-family-mono, monospace);
  font-size: 11px;
  color: var(--el-text-color-secondary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.ok-task__prompt {
  font-size: 13px;
  line-height: 1.5;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}
.ok-task__meta {
  margin-top: 4px;
  font-size: 11px;
  color: var(--el-text-color-secondary);
}
.ok-detail {
  background: var(--el-bg-color);
  border: 1px solid var(--el-border-color-lighter);
  border-radius: var(--el-border-radius-base, 6px);
  padding: 14px;
  min-height: 320px;
}
.ok-detail__head {
  display: flex;
  align-items: center;
  gap: 10px;
  padding-bottom: 10px;
  border-bottom: 1px solid var(--el-border-color-lighter);
  margin-bottom: 12px;
}

/* 审批台 */
.ok-approvals {
  border: 1px solid var(--el-color-warning-light-5);
  background: var(--el-color-warning-light-9);
  border-radius: var(--el-border-radius-base, 6px);
  padding: 12px;
  margin-bottom: 12px;
}
.ok-approvals__title {
  font-size: 13px;
  font-weight: 600;
  color: var(--el-color-warning);
  margin-bottom: 8px;
}
.ok-approval {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
  padding: 8px 0;
  border-top: 1px solid var(--el-color-warning-light-7);
}
.ok-approval:first-of-type {
  border-top: none;
}
.ok-approval__body {
  display: flex;
  align-items: baseline;
  gap: 8px;
  min-width: 0;
}
.ok-approval__kind {
  font-family: var(--el-font-family-mono, monospace);
  font-size: 12px;
  font-weight: 600;
  color: var(--el-color-warning);
  white-space: nowrap;
}
.ok-approval__detail {
  font-size: 12px;
  color: var(--el-text-color-regular);
  word-break: break-all;
}
.ok-approval__actions {
  display: flex;
  gap: 6px;
  flex: none;
}

.ok-result {
  margin-top: 12px;
  border-top: 1px solid var(--el-border-color-lighter);
  padding-top: 10px;
}
.ok-result__title {
  font-size: 13px;
  font-weight: 600;
  margin-bottom: 6px;
}
.ok-result__body {
  margin: 0;
  padding: 10px;
  background: var(--el-fill-color-light);
  border-radius: var(--el-border-radius-base, 6px);
  font-family: var(--el-font-family-mono, monospace);
  font-size: 12px;
  line-height: 1.6;
  max-height: 260px;
  overflow: auto;
  white-space: pre-wrap;
  word-break: break-word;
}

/* 对话框 */
.ok-modal {
  position: fixed;
  inset: 0;
  background: color-mix(in srgb, var(--el-text-color-primary) 45%, transparent);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 2000;
}
.ok-modal__box {
  background: var(--el-bg-color);
  border-radius: var(--el-border-radius-base, 6px);
  padding: 20px;
  width: 460px;
  max-width: calc(100vw - 40px);
  box-shadow: var(--el-box-shadow, 0 12px 32px color-mix(in srgb, var(--el-text-color-primary) 12%, transparent));
}
.ok-modal__title {
  font-size: 16px;
  font-weight: 600;
  margin-bottom: 6px;
}
.ok-modal__desc {
  font-size: 12px;
  line-height: 1.7;
  color: var(--el-text-color-secondary);
  margin-bottom: 14px;
}
.ok-scopes {
  display: flex;
  flex-direction: column;
  gap: 8px;
  margin-bottom: 12px;
}
.ok-scope {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  cursor: pointer;
}
.ok-scope-add {
  display: flex;
  gap: 8px;
  margin-bottom: 10px;
}
.ok-scope-current {
  font-size: 12px;
  color: var(--el-text-color-secondary);
  margin-bottom: 16px;
}
.ok-modal__actions {
  display: flex;
  gap: 10px;
}
</style>
