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
  type AgentDto,
  type AgentSaveRequest,
  type DiscoveredAgentDto,
  type PendingPermissionDto,
  type ProbeResultDto,
  type TaskDto,
  type TaskEventDto,
} from './http'
import AgentHubAgentCard from './AgentHubAgentCard.vue'
import AgentHubAgentForm from './AgentHubAgentForm.vue'
import AgentHubEventStream from './AgentHubEventStream.vue'

/** 当前页签。 */
type TabKey = 'agents' | 'delegate' | 'tasks'
const tab = ref<TabKey>('agents')

/** 全局提示条。 */
const message = ref('')
const messageType = ref<'ok' | 'err'>('ok')

// ────────────────────────────── Agent 目录 ──────────────────────────────

const agents = ref<AgentDto[]>([])
const loadingAgents = ref(false)
const discovering = ref(false)
const discovered = ref<DiscoveredAgentDto[]>([])
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

async function loadAgents() {
  loadingAgents.value = true
  try {
    agents.value = (await listAgents()) ?? []
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
})

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
    const task = await createTask({
      agentId: delegate.agentId,
      prompt: delegate.prompt.trim(),
      cwd: delegate.cwd.trim() || undefined,
      permissionMode: delegate.permissionMode,
      createdBy: 'ui',
      wait: false,
    })
    setMsg(`任务 ${task?.taskKey ?? ''} 已入队`)
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

onMounted(async () => {
  await loadAgents()
  await loadTasks()
})

onBeforeUnmount(() => {
  // 铁律：卸载必须停流，否则宿主切页后仍有一条 SSE 长连接挂着
  stopStream?.()
  stopStream = null
})
</script>

<template>
  <div class="ok ok-root">
    <header class="ok-header">
      <div>
        <div class="ok-title">Agent 中枢</div>
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

        <div class="ok-form__note">
          默认只读。越权的写操作会被外部 agent 转成权限申请，回到「任务台 → 审批台」等你在场裁决；
          对单个 agent 显式授信（带范围）后才自动放行。
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
  </div>
</template>

<style>
/* 仅走 --el-* token，不定义独立色值；插件无 Tailwind，布局类自写。
   前缀 ok- 取自「其它 agent 中枢」的语义缩写，避免与宿主类名撞车。 */
.ok-root {
  font-family: var(--el-font-family, system-ui, sans-serif);
  color: var(--el-text-color-primary);
  background: var(--el-bg-color-page, #f5f7fa);
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
.ok-title {
  font-size: 20px;
  font-weight: 600;
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
  color: var(--el-color-warning, #e6a23c);
}
.ok-alert {
  padding: 10px 14px;
  border-radius: var(--el-border-radius-base, 6px);
  margin-bottom: 14px;
  font-size: 13px;
}
.ok-alert--ok {
  background: var(--el-color-success-light-9, #e1f3d8);
  color: var(--el-color-success, #67c23a);
  border: 1px solid var(--el-color-success-light-5, #b3e19d);
}
.ok-alert--err {
  background: var(--el-color-danger-light-9, #fef0f0);
  color: var(--el-color-danger, #f56c6c);
  border: 1px solid var(--el-color-danger-light-5, #fab6b6);
}
.ok-tabs {
  display: flex;
  gap: 4px;
  border-bottom: 1px solid var(--el-border-color-lighter, #ebeef5);
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
  color: var(--el-color-primary, #409eff);
  border-bottom-color: var(--el-color-primary, #409eff);
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
  background: var(--el-fill-color-light, #f4f4f5);
  padding: 1px 6px;
  border-radius: 4px;
  color: var(--el-text-color-regular);
}
.ok-empty {
  padding: 32px 16px;
  text-align: center;
  font-size: 13px;
  color: var(--el-text-color-secondary);
  background: var(--el-bg-color, #fff);
  border: 1px dashed var(--el-border-color-lighter, #ebeef5);
  border-radius: var(--el-border-radius-base, 6px);
}
.ok-btn {
  height: 32px;
  padding: 0 14px;
  border-radius: var(--el-border-radius-base, 6px);
  border: 1px solid var(--el-border-color, #dcdfe6);
  background: var(--el-bg-color, #fff);
  color: var(--el-text-color-primary);
  font-size: 13px;
  cursor: pointer;
}
.ok-btn:hover {
  border-color: var(--el-color-primary-light-5, #a0cfff);
  color: var(--el-color-primary, #409eff);
}
.ok-btn--primary {
  background: var(--el-color-primary, #409eff);
  border-color: var(--el-color-primary, #409eff);
  /* 主色按钮文字：用 token 反色，禁用裸 #fff —— 主题切换时才能跟着走 */
  color: var(--el-color-white, #ffffff);
}
.ok-btn--primary:hover {
  background: var(--el-color-primary-light-3, #79bbff);
  border-color: var(--el-color-primary-light-3, #79bbff);
  color: var(--el-color-white, #ffffff);
}
.ok-btn--danger {
  border-color: var(--el-color-danger-light-5, #fab6b6);
  color: var(--el-color-danger, #f56c6c);
}
.ok-btn--danger:hover {
  background: var(--el-color-danger-light-9, #fef0f0);
  border-color: var(--el-color-danger, #f56c6c);
  color: var(--el-color-danger, #f56c6c);
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
  border: 1px solid var(--el-border-color, #dcdfe6);
  border-radius: var(--el-border-radius-base, 6px);
  background: var(--el-fill-color-blank, #fff);
  color: var(--el-text-color-primary);
  font-size: 13px;
  box-sizing: border-box;
  outline: none;
  font-family: inherit;
  width: 100%;
}
.ok-input:focus {
  border-color: var(--el-color-primary, #409eff);
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
  background: var(--el-color-success-light-9, #e1f3d8);
  color: var(--el-color-success, #67c23a);
}
.ok-badge--run {
  background: var(--el-color-primary-light-9, #ecf5ff);
  color: var(--el-color-primary, #409eff);
}
.ok-badge--wait {
  background: var(--el-color-warning-light-9, #fdf6ec);
  color: var(--el-color-warning, #e6a23c);
}
.ok-badge--idle {
  background: var(--el-fill-color-light, #f4f4f5);
  color: var(--el-text-color-secondary);
}
.ok-badge--bad {
  background: var(--el-color-danger-light-9, #fef0f0);
  color: var(--el-color-danger, #f56c6c);
}

/* 扫描候选 */
.ok-discovered {
  background: var(--el-bg-color, #fff);
  border: 1px solid var(--el-color-primary-light-7, #c6e2ff);
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
  background: var(--el-bg-color, #fff);
  border: 1px solid var(--el-border-color-lighter, #ebeef5);
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
  background: var(--el-fill-color-light, #f4f4f5);
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
  background: var(--el-bg-color, #fff);
  border: 1px solid var(--el-border-color-lighter, #ebeef5);
  border-radius: var(--el-border-radius-base, 6px);
  overflow: hidden;
  max-height: 620px;
  overflow-y: auto;
}
.ok-task {
  padding: 10px 12px;
  border-bottom: 1px solid var(--el-border-color-lighter, #ebeef5);
  cursor: pointer;
}
.ok-task:last-child {
  border-bottom: none;
}
.ok-task:hover {
  background: var(--el-fill-color-light, #f4f4f5);
}
.ok-task--on {
  background: var(--el-color-primary-light-9, #ecf5ff);
  box-shadow: inset 3px 0 0 var(--el-color-primary, #409eff);
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
  background: var(--el-bg-color, #fff);
  border: 1px solid var(--el-border-color-lighter, #ebeef5);
  border-radius: var(--el-border-radius-base, 6px);
  padding: 14px;
  min-height: 320px;
}
.ok-detail__head {
  display: flex;
  align-items: center;
  gap: 10px;
  padding-bottom: 10px;
  border-bottom: 1px solid var(--el-border-color-lighter, #ebeef5);
  margin-bottom: 12px;
}

/* 审批台 */
.ok-approvals {
  border: 1px solid var(--el-color-warning-light-5, #f5dab1);
  background: var(--el-color-warning-light-9, #fdf6ec);
  border-radius: var(--el-border-radius-base, 6px);
  padding: 12px;
  margin-bottom: 12px;
}
.ok-approvals__title {
  font-size: 13px;
  font-weight: 600;
  color: var(--el-color-warning, #e6a23c);
  margin-bottom: 8px;
}
.ok-approval {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
  padding: 8px 0;
  border-top: 1px solid var(--el-color-warning-light-7, #f8e3c5);
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
  color: var(--el-color-warning, #e6a23c);
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
  border-top: 1px solid var(--el-border-color-lighter, #ebeef5);
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
  background: var(--el-fill-color-light, #f4f4f5);
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
  background: var(--el-bg-color, #fff);
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
