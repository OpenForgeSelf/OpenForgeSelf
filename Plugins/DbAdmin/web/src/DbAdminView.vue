<script setup lang="ts">
/**
 * 数据库管理 · 可审批的 SQL 工作台（docs/02 布局、06 S0–S9 + S-R）
 * 左：连接 + schema 树；右：SQL 编辑器 + 分页结果格 + 历史 / Agent 回显。
 * 浮层互斥：连接表单 → 审批 → 破坏性二次确认 → 删除确认 → 审计抽屉（同一时刻只有一个）。
 * `fixture` 存在即演示模式：不发网络（CT 画廊用）。
 */
import { computed, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import { ApiError } from './http'
import { dbAdminApi, fetchPluginVersion } from './api'
import { classifyQueryResponse, errorReason, orchestrateApprove, rejectRequest } from './approvalFlow'
import { buildConnStr, emptyForm, formFromConnection, isSqlite, isTested, toSubmitBody, validateForm, DEFAULT_PORTS } from './connForm'
import { PAGE_SIZES, canNext, canPrev, clampPage, pageCount, rangeLabel } from './pager'
import { MASK, maskConnStr, redactText } from './redact'
import { buildSnapshot, loadSnapshot, restoredToConnection, saveSnapshot } from './restore'
import type {
  AgentToolEcho, DbAdminFixture, DbApproval, DbAuditLog, DbConnection, DbConnectionInput, DbProvider,
  DbQueryHistory, DbTable, DbTableDetail, OverlayKind, PermissionMode, QueryResponse, ResolveApprovalRequest, TestConnectionResult,
} from './types'

const props = defineProps<{ fixture?: DbAdminFixture }>()
const isDemo = computed(() => !!props.fixture)

const version = ref('加载中')
const loading = ref(true)
const unauthorized = ref(false)
const banner = ref<{ kind: 'error' | 'warn'; text: string } | null>(null)
const restoredHint = ref('')
const permissionMode = ref<PermissionMode>('AskWrite')

const providers = ref<DbProvider[]>([])
const connections = ref<DbConnection[]>([])
const activeId = ref<string | null>(null)
const tables = ref<Record<string, DbTable[]>>({})
const details = ref<Record<string, DbTableDetail>>({})
const expanded = ref<Set<string>>(new Set())

const drafts = ref<Record<string, string>>({})
const result = ref<QueryResponse | null>(null)
const resultError = ref<{ code: string; message: string } | null>(null)
const running = ref(false)
const page = ref(1)
const pageSize = ref<number>(50)
const lastSql = ref('')

const approvals = ref<DbApproval[]>([])
const activeApprovalId = ref<string | null>(null)
const history = ref<DbQueryHistory[]>([])
const audit = ref<DbAuditLog[]>([])
const auditFilter = ref('')
const agentEchoes = ref<AgentToolEcho[]>([])
const sideTab = ref<'history' | 'agent'>('history')

const overlay = ref<OverlayKind>(null)
const connForm = reactive<DbConnectionInput>(emptyForm())
const editingId = ref<string | null>(null)
const connFormError = ref('')
const testResult = ref<TestConnectionResult | null>(null)
const testing = ref(false)
const saving = ref(false)
const deleteTargetId = ref<string | null>(null)
let destructiveResolver: ((ok: boolean) => void) | null = null
let pollTimer: ReturnType<typeof setInterval> | null = null

const systemConns = computed(() => connections.value.filter(isSystem))
const userConns = computed(() => connections.value.filter(c => !isSystem(c)))
const active = computed(() => connections.value.find(c => c.id === activeId.value) ?? null)
const activeProvider = computed(() => providerOf(active.value?.provider))
const sql = computed({
  get: () => (activeId.value ? drafts.value[activeId.value] ?? '' : ''),
  set: (v: string) => {
    if (!activeId.value) return
    drafts.value = { ...drafts.value, [activeId.value]: v }
  },
})
const pendingApprovals = computed(() => approvals.value.filter(a => a.status === 'pending'))
const currentApproval = computed(() => approvals.value.find(a => a.id === activeApprovalId.value) ?? null)
const formProvider = computed(() => providerOf(connForm.provider))
const rows = computed(() => result.value?.rows ?? [])
const totalPages = computed(() => pageCount(result.value?.total, pageSize.value))
const AUDIT_KINDS = ['query', 'approval', 'rejected', 'write_result', 'conn', 'permission_mode']
const filteredAudit = computed(() => {
  const f = auditFilter.value
  if (!f) return audit.value
  return audit.value.filter(a => (f === 'approval' || f === 'conn' ? a.kind.startsWith(f) : a.kind === f))
})
const runDisabledReason = computed(() => {
  if (unauthorized.value) return '未授权'
  if (!active.value) return '先在左侧选择连接'
  if (!sql.value.trim()) return '编辑器为空'
  if (running.value) return '执行中'
  return ''
})

function isSystem(c: DbConnection): boolean {
  return c.system ?? c.readonly
}
function providerOf(id: string | undefined): DbProvider | undefined {
  if (!id) return undefined
  return providers.value.find(p => p.id === id) ?? { id, displayName: id, driverAvailable: true }
}
function providerLabel(id: string | undefined): string {
  return providerOf(id)?.displayName ?? id ?? ''
}
function connName(id: string | undefined): string {
  return connections.value.find(c => c.id === id)?.name ?? id ?? ''
}
function connSubtitle(c: DbConnection): string {
  if (c.mode === 'connstr') return maskConnStr(c.connStr) || '连接串'
  if (isSqlite(c.provider)) return c.database ?? ''
  return `${c.userName ? c.userName + '@' : ''}${c.host ?? ''}${c.port ? ':' + c.port : ''}/${c.database ?? ''}`
}
function fmtTime(s: string | undefined): string {
  if (!s) return ''
  const m = /T(\d{2}:\d{2}:\d{2})/.exec(s)
  return m ? m[1] : s
}
function cell(v: unknown): string {
  if (v === null || v === undefined) return 'NULL'
  if (typeof v === 'object') return redactText(JSON.stringify(v))
  return redactText(String(v))
}
function isNull(v: unknown): boolean {
  return v === null || v === undefined
}

/* ---------------- 浮层互斥 ---------------- */
function openOverlay(kind: Exclude<OverlayKind, null>) {
  overlay.value = kind
}
function closeOverlay() {
  if (overlay.value === 'destructive' && destructiveResolver) {
    destructiveResolver(false)
    destructiveResolver = null
  }
  overlay.value = null
}

/* ---------------- fixture ---------------- */
function applyFixture(f: DbAdminFixture) {
  restoredHint.value = ''
  version.value = f.version ?? '0.0.0-ct'
  loading.value = !!f.loading
  unauthorized.value = !!f.unauthorized
  banner.value = f.banner ?? null
  permissionMode.value = f.permissionMode ?? 'AskWrite'
  providers.value = f.providers ?? []
  connections.value = f.connections ?? []
  activeId.value = f.activeConnectionId ?? null
  tables.value = f.tables ?? {}
  details.value = f.tableDetails ?? {}
  expanded.value = new Set(f.expanded ?? [])
  drafts.value = activeId.value && f.sql !== undefined ? { [activeId.value]: f.sql } : {}
  result.value = f.result ?? null
  resultError.value = f.resultError ?? null
  if (f.result?.page) page.value = f.result.page
  if (f.result?.pageSize) pageSize.value = f.result.pageSize
  approvals.value = f.approvals ?? []
  history.value = f.history ?? []
  audit.value = f.audit ?? []
  auditFilter.value = f.auditFilter ?? ''
  agentEchoes.value = f.agentEchoes ?? []
  sideTab.value = f.sideTab ?? 'history'
  const { editingId: fixtureEditingId, ...fixtureForm } = f.connForm ?? {}
  Object.assign(connForm, emptyForm(fixtureForm.provider ?? 'SQLite'), fixtureForm)
  editingId.value = fixtureEditingId ?? null
  connFormError.value = f.connFormError ?? ''
  testResult.value = f.testResult ?? null
  activeApprovalId.value = f.activeApprovalId ?? null
  overlay.value = f.overlay ?? null
  if (f.seedRestore || (!f.connections && !f.loading)) applyRestore()
}

/* ---------------- S-R ---------------- */
function applyRestore(): boolean {
  const snap = loadSnapshot()
  if (!snap) return false
  if (connections.value.length === 0) connections.value = snap.connections.map(restoredToConnection)
  drafts.value = { ...snap.drafts, ...drafts.value }
  if (!activeId.value && snap.activeConnectionId) activeId.value = snap.activeConnectionId
  const n = Object.keys(snap.drafts).length
  restoredHint.value = `已从上次会话恢复：${snap.connections.length} 个连接 · ${n} 份草稿（结果集与密码不恢复；待审批已从服务端重拉）`
  return true
}
function persist() {
  if (loading.value || unauthorized.value) return
  saveSnapshot(buildSnapshot(connections.value, activeId.value, drafts.value))
}
watch([drafts, activeId, connections], persist, { deep: true })

/* ---------------- 加载 ---------------- */
function handleError(e: unknown, where: string) {
  if (e instanceof ApiError && (e.status === 401 || e.code === 'unauthorized')) {
    unauthorized.value = true
    return
  }
  const msg = e instanceof Error ? e.message : String(e)
  banner.value = { kind: 'error', text: `${where}失败：${redactText(msg)}` }
}

async function loadAll() {
  loading.value = true
  applyRestore()
  try {
    const [v, provs, conns, mode] = await Promise.all([
      fetchPluginVersion().catch(() => '未知版本'),
      dbAdminApi.providers(),
      dbAdminApi.connections(),
      dbAdminApi.getPermissionMode().catch(() => undefined),
    ])
    version.value = v
    providers.value = provs
    connections.value = conns
    if (mode?.mode === 'AskWrite' || mode?.mode === 'Auto') permissionMode.value = mode.mode
    if (activeId.value && !connections.value.some(c => c.id === activeId.value)) activeId.value = null
    loading.value = false
    if (activeId.value) {
      expanded.value = new Set([...expanded.value, `c:${activeId.value}`])
      void loadTables(activeId.value)
    }
    await Promise.all([refreshApprovals(true), refreshHistory()])
  } catch (e) {
    loading.value = false
    handleError(e, '加载')
  }
}

async function loadTables(id: string) {
  if (isDemo.value || tables.value[id]) return
  try {
    tables.value = { ...tables.value, [id]: await dbAdminApi.tables(id) }
  } catch (e) {
    handleError(e, '读取表')
  }
}
async function loadDetail(id: string, table: string) {
  const key = `${id}/${table}`
  if (isDemo.value || details.value[key]) return
  try {
    const d = await dbAdminApi.tableDetail(id, table)
    if (d) details.value = { ...details.value, [key]: d }
  } catch (e) {
    handleError(e, '读取列')
  }
}
async function refreshApprovals(autoOpen = false) {
  if (isDemo.value) return
  try {
    approvals.value = await dbAdminApi.approvals('pending')
    if (autoOpen && overlay.value === null && pendingApprovals.value.length > 0) {
      activeApprovalId.value = pendingApprovals.value[0].id
      openOverlay('approval')
    }
  } catch (e) {
    handleError(e, '读取审批')
  }
}
async function refreshHistory() {
  if (isDemo.value) return
  try {
    history.value = await dbAdminApi.history()
  } catch {
    /* 历史失败不打断主流程 */
  }
}
async function openAudit() {
  openOverlay('audit')
  if (isDemo.value) return
  try {
    audit.value = await dbAdminApi.audit()
  } catch (e) {
    handleError(e, '读取审计')
  }
}

/* ---------------- 树 ---------------- */
function toggle(key: string) {
  const s = new Set(expanded.value)
  if (s.has(key)) s.delete(key)
  else s.add(key)
  expanded.value = s
}
function selectConnection(c: DbConnection) {
  if (activeId.value !== c.id) {
    activeId.value = c.id
    result.value = null
    resultError.value = null
    page.value = 1
  }
  toggle(`c:${c.id}`)
  if (expanded.value.has(`c:${c.id}`)) void loadTables(c.id)
}
function toggleTable(connId: string, t: DbTable) {
  const key = `t:${connId}/${t.name}`
  toggle(key)
  if (expanded.value.has(key)) void loadDetail(connId, t.name)
}
function insertSelect(connId: string, t: DbTable) {
  activeId.value = connId
  sql.value = `SELECT * FROM ${t.name}`
}

/* ---------------- 执行 ---------------- */
async function execute(targetPage = 1) {
  if (runDisabledReason.value || !active.value) return
  if (isDemo.value) return
  running.value = true
  resultError.value = null
  const text = targetPage === 1 ? sql.value : lastSql.value || sql.value
  lastSql.value = text
  try {
    const r = await dbAdminApi.query({
      connectionId: active.value.id, sql: text, page: targetPage, pageSize: pageSize.value,
    })
    handleQueryResponse(r)
    page.value = r.page ?? targetPage
  } catch (e) {
    if (e instanceof ApiError && e.status !== 401) {
      resultError.value = { code: e.code ?? 'sql_error', message: redactText(e.message) }
    } else {
      handleError(e, '执行')
    }
  } finally {
    running.value = false
    void refreshHistory()
  }
}
function handleQueryResponse(r: QueryResponse) {
  const action = classifyQueryResponse(r)
  if (action === 'show_result') {
    result.value = r
    return
  }
  if (action === 'red_bar') {
    resultError.value = { code: r.code ?? 'rejected_hard', message: redactText(r.message ?? '') }
    return
  }
  void refreshApprovals().then(() => {
    activeApprovalId.value = r.approvalId ?? pendingApprovals.value[0]?.id ?? null
    if (activeApprovalId.value) openOverlay('approval')
  })
}
function onEditorKey(e: KeyboardEvent) {
  if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) {
    e.preventDefault()
    void execute(1)
  }
}
function goPage(p: number) {
  const next = clampPage(p, pageSize.value, result.value?.total)
  if (isDemo.value) {
    page.value = next
    return
  }
  void execute(next)
}
function changePageSize(e: Event) {
  pageSize.value = Number((e.target as HTMLSelectElement).value) || 50
  if (!isDemo.value && result.value) void execute(1)
}
function rerun(h: DbQueryHistory) {
  if (connections.value.some(c => c.id === h.connectionId)) activeId.value = h.connectionId
  sql.value = h.sqlRedacted
}

/* ---------------- 审批 + 二次确认 ---------------- */
function askDestructive(): Promise<boolean> {
  openOverlay('destructive')
  return new Promise(resolve => {
    destructiveResolver = resolve
  })
}
function confirmDestructive(ok: boolean) {
  const r = destructiveResolver
  destructiveResolver = null
  overlay.value = null
  r?.(ok)
}
async function approve() {
  const a = currentApproval.value
  if (!a) return
  const outcome = await orchestrateApprove(a, askDestructive)
  if (outcome.action === 'cancelled') {
    banner.value = { kind: 'warn', text: '已取消二次确认：语句未执行，审批仍待处理' }
    return
  }
  await resolveApproval(a, outcome.request.decision === 'approve', outcome.request.confirmDestructive)
}
async function reject() {
  const a = currentApproval.value
  if (!a) return
  await resolveApproval(a, rejectRequest().decision === 'approve')
}
async function resolveApproval(a: DbApproval, approveIt: boolean, confirmDestructiveFlag?: boolean) {
  overlay.value = null
  if (isDemo.value) {
    approvals.value = approvals.value.map(x => (x.id === a.id ? { ...x, status: approveIt ? 'approved' : 'rejected' } : x))
    audit.value = [
      { id: `demo-${a.id}`, kind: approveIt ? 'approval_approved' : 'approval_rejected', connectionId: a.connectionId, sqlRedacted: a.sqlRedacted, decision: approveIt ? 'approved' : 'rejected', createTime: a.createTime },
      ...audit.value,
    ]
    banner.value = { kind: approveIt ? 'warn' : 'error', text: approveIt ? '已批准并执行' : '已拒绝：语句未执行' }
    return
  }
  try {
    const body: ResolveApprovalRequest = approveIt
      ? { decision: 'approve', ...(confirmDestructiveFlag ? { confirmDestructive: true } : {}) }
      : rejectRequest()
    const r = await dbAdminApi.resolveApproval(a.id, body)
    if (approveIt) handleQueryResponse({ ...r, status: r.status === 'awaiting_approval' ? 'ok' : r.status })
    banner.value = approveIt ? null : { kind: 'warn', text: '已拒绝：语句未执行' }
  } catch (e) {
    if (e instanceof ApiError) resultError.value = { code: e.code ?? 'sql_error', message: redactText(e.message) }
    else handleError(e, '审批')
  } finally {
    activeApprovalId.value = null
    await Promise.all([refreshApprovals(), refreshHistory()])
  }
}
function openPending() {
  const first = pendingApprovals.value[0]
  if (!first) return
  activeApprovalId.value = first.id
  openOverlay('approval')
}

/* ---------------- 权限模式 ---------------- */
async function togglePermission() {
  const next: PermissionMode = permissionMode.value === 'AskWrite' ? 'Auto' : 'AskWrite'
  if (isDemo.value) {
    permissionMode.value = next
    return
  }
  try {
    const r = await dbAdminApi.setPermissionMode(next)
    permissionMode.value = r?.mode ?? next
  } catch (e) {
    handleError(e, '切换权限模式')
  }
}

/* ---------------- 连接表单 ---------------- */
function openNewConnection() {
  Object.assign(connForm, emptyForm(providers.value[0]?.id ?? 'SQLite'))
  editingId.value = null
  connFormError.value = ''
  testResult.value = null
  openOverlay('connForm')
}
function openEditConnection(c: DbConnection) {
  Object.assign(connForm, formFromConnection(c))
  editingId.value = c.id
  connFormError.value = ''
  testResult.value = null
  openOverlay('connForm')
}
function onProviderChange() {
  connForm.port = DEFAULT_PORTS[connForm.provider]
  testResult.value = null
}
function switchMode(mode: 'form' | 'connstr') {
  if (mode === connForm.mode) return
  if (mode === 'connstr' && !connForm.connStr) connForm.connStr = buildConnStr(connForm)
  connForm.mode = mode
  testResult.value = null
}
async function testConnection() {
  const err = validateForm(connForm, !!editingId.value)
  if (err) {
    connFormError.value = err
    return
  }
  connFormError.value = ''
  if (isDemo.value) return
  testing.value = true
  try {
    const body = { ...toSubmitBody(connForm, !!editingId.value), ...(editingId.value ? { id: editingId.value } : {}) }
    const r = await dbAdminApi.testConnection(body)
    testResult.value = { ...r, message: redactText(r.message) }
  } catch (e) {
    if (e instanceof ApiError) {
      testResult.value = e.code === 'driver_missing'
        ? { status: 'driver_missing', message: redactText(e.message), driverPackage: formProvider.value?.driverPackage }
        : { status: 'error', message: redactText(e.message) }
    } else {
      testResult.value = { status: 'error', message: String(e) }
    }
  } finally {
    testing.value = false
  }
}
async function saveConnection() {
  const err = validateForm(connForm, !!editingId.value)
  if (err) {
    connFormError.value = err
    return
  }
  connFormError.value = ''
  if (isDemo.value) return
  saving.value = true
  try {
    const body = toSubmitBody(connForm, !!editingId.value)
    const saved = editingId.value
      ? await dbAdminApi.updateConnection(editingId.value, body)
      : await dbAdminApi.createConnection(body)
    connForm.password = ''
    connForm.connStr = ''
    overlay.value = null
    connections.value = await dbAdminApi.connections()
    if (saved.id) activeId.value = saved.id
  } catch (e) {
    connFormError.value = e instanceof Error ? `保存失败：${redactText(e.message)}` : '保存失败'
  } finally {
    saving.value = false
  }
}
function askDelete(c: DbConnection) {
  deleteTargetId.value = c.id
  openOverlay('deleteConn')
}
async function confirmDelete() {
  const id = deleteTargetId.value
  overlay.value = null
  if (!id || isDemo.value) return
  try {
    await dbAdminApi.deleteConnection(id)
    connections.value = connections.value.filter(c => c.id !== id)
    const { [id]: _drop, ...rest } = drafts.value
    void _drop
    drafts.value = rest
    if (activeId.value === id) activeId.value = null
  } catch (e) {
    handleError(e, '删除连接')
  }
}

onMounted(() => {
  if (props.fixture) {
    applyFixture(props.fixture)
    return
  }
  void loadAll()
  pollTimer = setInterval(() => {
    if (!unauthorized.value) void refreshApprovals(true)
  }, 5000)
})
onBeforeUnmount(() => {
  if (pollTimer) clearInterval(pollTimer)
})
watch(() => props.fixture, f => f && applyFixture(f), { deep: true })
</script>

<template>
  <div class="dba-root" data-testid="db-admin-root">
    <header class="head">
      <div class="title-block">
        <h1 class="view-title">数据库管理 <span class="ver" data-testid="version-badge">v{{ version }}</span></h1>
        <p class="sub">可审批的 SQL 工作台</p>
      </div>
      <div class="head-actions">
        <button
          class="badge"
          :class="permissionMode === 'Auto' ? 'badge-auto' : 'badge-ask'"
          data-testid="permission-badge"
          :disabled="unauthorized"
          :title="permissionMode === 'Auto' ? '普通写自动执行；破坏性仍审批+二次确认；点击切换' : '写语句需审批；点击切换'"
          @click="togglePermission"
        >
          {{ permissionMode === 'Auto' ? 'Auto · 写自动' : 'AskWrite · 写询问' }}
        </button>
        <button
          v-if="pendingApprovals.length"
          class="btn warn"
          data-testid="pending-approvals"
          @click="openPending"
        >待审批 {{ pendingApprovals.length }}</button>
        <button class="btn" data-testid="open-audit" :disabled="unauthorized" @click="openAudit">审计</button>
      </div>
    </header>

    <div v-if="unauthorized" class="banner banner-error" data-testid="banner-401">
      401 未授权：API Token 缺失或失效，请在宿主设置中重新登录后刷新。
    </div>
    <div
      v-else-if="banner"
      class="banner"
      :class="banner.kind === 'error' ? 'banner-error' : 'banner-warn'"
      data-testid="banner"
    >{{ banner.text }}</div>
    <div v-if="restoredHint && !unauthorized" class="banner banner-info" data-testid="restore-hint">{{ restoredHint }}</div>

    <div v-if="!unauthorized" class="body">
      <!-- 左栏：连接 + schema 树 -->
      <aside class="side" data-testid="conn-tree">
        <button class="btn primary block" data-testid="new-connection" @click="openNewConnection">+ 新建连接</button>

        <div v-if="loading" class="skeleton" data-testid="tree-skeleton">
          <div v-for="i in 5" :key="i" class="sk-line" :style="{ width: `${90 - i * 9}%` }" />
        </div>

        <template v-else>
          <div class="group-title">系统（只读）</div>
          <div v-if="systemConns.length === 0" class="muted pad">无系统库</div>
          <template v-for="c in systemConns" :key="c.id">
            <div
              class="node conn"
              :class="{ on: c.id === activeId }"
              :data-testid="`conn-${c.id}`"
              :title="connSubtitle(c)"
              @click="selectConnection(c)"
            >
              <span class="caret">{{ expanded.has(`c:${c.id}`) ? '▾' : '▸' }}</span>
              <span class="name">{{ c.name }}</span>
              <span class="lock" title="系统只读连接：任何写入硬拒">🔒</span>
            </div>
            <template v-if="expanded.has(`c:${c.id}`)">
              <template v-for="t in tables[c.id] ?? []" :key="t.name">
                <div class="node table" :data-testid="`table-${c.id}-${t.name}`" @click="toggleTable(c.id, t)" @dblclick="insertSelect(c.id, t)">
                  <span class="caret">{{ expanded.has(`t:${c.id}/${t.name}`) ? '▾' : '▸' }}</span>
                  <span class="name">{{ t.type === 'view' ? '视图' : '表' }} {{ t.name }}</span>
                </div>
                <template v-if="expanded.has(`t:${c.id}/${t.name}`)">
                  <div v-for="col in details[`${c.id}/${t.name}`]?.columns ?? []" :key="col.name" class="node column">
                    · {{ col.name }} <span class="type">{{ col.type }}</span>
                    <span v-if="col.primaryKey" class="pk">PK</span>
                    <span v-if="!col.nullable && !col.primaryKey" class="nn">NOT NULL</span>
                  </div>
                </template>
              </template>
            </template>
          </template>

          <div class="group-title">我的连接</div>
          <div v-if="userConns.length === 0" class="empty-guide" data-testid="empty-guide">
            <p>还没有自建连接。</p>
            <button class="btn primary" data-testid="empty-new-connection" @click="openNewConnection">+ 新建连接</button>
            <p class="muted">支持 SQLite 文件，或 MySQL / PostgreSQL / SQL Server（未实测）。</p>
          </div>
          <template v-for="c in userConns" :key="c.id">
            <div
              class="node conn"
              :class="{ on: c.id === activeId }"
              :data-testid="`conn-${c.id}`"
              :title="connSubtitle(c)"
              @click="selectConnection(c)"
            >
              <span class="caret">{{ expanded.has(`c:${c.id}`) ? '▾' : '▸' }}</span>
              <span class="name">{{ c.name }}</span>
              <span v-if="c.readonly" class="lock" title="只读连接">🔒</span>
              <span class="node-actions">
                <button class="icon" title="编辑连接" :data-testid="`edit-${c.id}`" @click.stop="openEditConnection(c)">✎</button>
                <button class="icon danger" title="删除连接（仅删配置，不删库文件）" :data-testid="`delete-${c.id}`" @click.stop="askDelete(c)">✕</button>
              </span>
            </div>
            <template v-if="expanded.has(`c:${c.id}`)">
              <div v-if="(tables[c.id] ?? []).length === 0" class="node column muted">（无表）</div>
              <template v-for="t in tables[c.id] ?? []" :key="t.name">
                <div class="node table" :data-testid="`table-${c.id}-${t.name}`" @click="toggleTable(c.id, t)" @dblclick="insertSelect(c.id, t)">
                  <span class="caret">{{ expanded.has(`t:${c.id}/${t.name}`) ? '▾' : '▸' }}</span>
                  <span class="name">{{ t.type === 'view' ? '视图' : '表' }} {{ t.name }}</span>
                </div>
                <template v-if="expanded.has(`t:${c.id}/${t.name}`)">
                  <div
                    v-for="col in details[`${c.id}/${t.name}`]?.columns ?? []"
                    :key="col.name"
                    class="node column"
                    :data-testid="`col-${c.id}-${t.name}-${col.name}`"
                  >
                    · {{ col.name }} <span class="type">{{ col.type }}</span>
                    <span v-if="col.primaryKey" class="pk">PK</span>
                    <span v-if="!col.nullable && !col.primaryKey" class="nn">NOT NULL</span>
                  </div>
                </template>
              </template>
            </template>
          </template>
        </template>
      </aside>

      <!-- 右区：编辑器 + 结果 -->
      <main class="main">
        <div class="conn-bar" data-testid="conn-bar">
          <template v-if="active">
            <span class="label">连接:</span>
            <strong class="conn-name">{{ active.name }}</strong>
            <span class="tag">{{ providerLabel(active.provider) }}</span>
            <span v-if="active.readonly" class="tag tag-ro" data-testid="readonly-badge">只读</span>
            <span v-if="!isTested(activeProvider)" class="tag tag-untested">未实测</span>
            <span class="muted ellipsis" :title="connSubtitle(active)">{{ connSubtitle(active) }}</span>
          </template>
          <span v-else class="muted">未选择连接 —— 在左侧点一个连接开始</span>
        </div>

        <textarea
          v-model="sql"
          class="editor"
          data-testid="sql-editor"
          spellcheck="false"
          :disabled="!active"
          :placeholder="active ? '输入 SQL，Ctrl+Enter 执行' : '先选择连接'"
          @keydown="onEditorKey"
        />

        <div class="run-bar">
          <button
            class="btn primary"
            data-testid="run"
            :disabled="!!runDisabledReason"
            :title="runDisabledReason || 'Ctrl+Enter'"
            @click="execute(1)"
          >{{ running ? '执行中…' : '执行' }}</button>
          <span v-if="runDisabledReason && !running" class="muted">{{ runDisabledReason }}</span>
          <span class="spacer" />
          <span v-if="result" class="stats" data-testid="result-stats">
            <template v-if="result.elapsedMs !== undefined">耗时 {{ result.elapsedMs }} ms</template>
            <template v-if="result.affectedRows !== undefined"> · 影响 {{ result.affectedRows }} 行</template>
            <template v-if="result.columns?.length"> · {{ rangeLabel(page, pageSize, rows.length, result.total) }}</template>
          </span>
        </div>

        <div v-if="resultError" class="result-error" data-testid="result-error">
          <strong>{{ errorReason(resultError.code) }}</strong>
          <span class="code">[{{ resultError.code }}]</span>
          <div v-if="resultError.message" class="err-msg">{{ resultError.message }}</div>
          <div class="muted">未弹出审批：该语句没有被执行。</div>
        </div>

        <div class="grid-wrap" data-testid="result-grid">
          <div v-if="result?.truncated" class="truncated" data-testid="truncated">结果已截断（truncated=true）：仅显示上限内的行</div>
          <table v-if="result?.columns?.length" class="grid">
            <thead>
              <tr>
                <th class="rownum">#</th>
                <th v-for="c in result.columns" :key="c.name">{{ c.name }}<span v-if="c.type" class="type">{{ c.type }}</span></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(r, i) in rows" :key="i">
                <td class="rownum">{{ (page - 1) * pageSize + i + 1 }}</td>
                <td v-for="(v, j) in r" :key="j" :class="{ null: isNull(v) }" :title="cell(v)">{{ cell(v) }}</td>
              </tr>
            </tbody>
          </table>
          <div v-else-if="result && result.affectedRows !== undefined" class="placeholder">执行成功 · 影响 {{ result.affectedRows }} 行</div>
          <div v-else-if="!resultError" class="placeholder muted">结果将显示在这里</div>
        </div>

        <div v-if="result?.columns?.length" class="pager" data-testid="pager">
          <button class="btn" data-testid="page-prev" :disabled="!canPrev(page)" @click="goPage(page - 1)">‹</button>
          <span data-testid="page-label">{{ page }} / {{ totalPages ?? '?' }}</span>
          <button class="btn" data-testid="page-next" :disabled="!canNext(page, pageSize, result.total, rows.length)" @click="goPage(page + 1)">›</button>
          <label class="muted">每页
            <select :value="pageSize" data-testid="page-size" @change="changePageSize">
              <option v-for="s in PAGE_SIZES" :key="s" :value="s">{{ s }}</option>
            </select>
          </label>
        </div>

        <section class="bottom">
          <div class="tabs">
            <button class="tab" :class="{ on: sideTab === 'history' }" data-testid="tab-history" @click="sideTab = 'history'">历史</button>
            <button class="tab" :class="{ on: sideTab === 'agent' }" data-testid="tab-agent" @click="sideTab = 'agent'">Agent 工具回显</button>
          </div>
          <div v-if="sideTab === 'history'" class="list" data-testid="history-list">
            <div v-if="history.length === 0" class="muted pad">暂无执行历史</div>
            <div v-for="h in history" :key="h.id" class="row" :class="{ fail: !h.success }">
              <span class="time">{{ fmtTime(h.createTime) }}</span>
              <span class="pill">{{ h.source === 'agent' ? 'Agent' : 'UI' }}</span>
              <span class="pill">{{ h.kind === 'write' ? '写' : '读' }}</span>
              <code class="sqltext" :title="redactText(h.sqlRedacted)">{{ redactText(h.sqlRedacted) }}</code>
              <span class="muted">{{ h.success ? `${h.rowCount ?? 0} 行 · ${h.elapsedMs ?? 0} ms` : redactText(h.error) }}</span>
              <button class="icon" title="放回编辑器" @click="rerun(h)">↺</button>
            </div>
          </div>
          <div v-else class="list" data-testid="agent-echo-list">
            <div v-if="agentEchoes.length === 0" class="muted pad">本会话暂无 Agent db_* 调用</div>
            <div v-for="(a, i) in agentEchoes" :key="i" class="row echo">
              <span class="time">{{ fmtTime(a.at) }}</span>
              <code class="tool">{{ a.tool }}</code>
              <code class="sqltext" :title="redactText(JSON.stringify(a.args))">{{ redactText(JSON.stringify(a.args)) }}</code>
              <span class="pill" :class="`st-${a.status}`">{{ a.status }}</span>
              <span v-if="a.rowCount !== undefined" class="muted">{{ a.rowCount }} 行</span>
              <span v-if="a.truncated" class="pill st-truncated">truncated=true</span>
              <span v-if="a.approvalId" class="muted">审批 {{ a.approvalId }}</span>
              <span v-if="a.message" class="muted">{{ redactText(a.message) }}</span>
            </div>
          </div>
        </section>
      </main>
    </div>

    <!-- 浮层：连接表单 -->
    <div v-if="overlay === 'connForm'" class="mask" data-testid="conn-form">
      <div class="modal">
        <h3>{{ editingId ? '编辑连接' : '新建连接' }}</h3>
        <label>名称<input v-model="connForm.name" data-testid="cf-name" placeholder="如 demo.db" /></label>
        <label>Provider
          <div class="row-inline">
            <select v-model="connForm.provider" data-testid="cf-provider" @change="onProviderChange">
              <option v-for="p in providers" :key="p.id" :value="p.id">{{ p.displayName }}{{ p.driverAvailable ? '' : '（需驱动）' }}</option>
            </select>
            <span v-if="formProvider && !formProvider.driverAvailable" class="tag tag-muted" data-testid="cf-needs-driver">需驱动</span>
            <span v-if="!isTested(formProvider)" class="tag tag-untested" data-testid="cf-untested">未实测</span>
          </div>
        </label>
        <div class="mode-switch">
          <button class="chip" :class="{ on: connForm.mode === 'form' }" data-testid="cf-mode-form" @click="switchMode('form')">表单</button>
          <button class="chip" :class="{ on: connForm.mode === 'connstr' }" data-testid="cf-mode-connstr" @click="switchMode('connstr')">连接串</button>
        </div>
        <template v-if="connForm.mode === 'form'">
          <label v-if="isSqlite(connForm.provider)">文件路径<input v-model="connForm.database" data-testid="cf-path" placeholder="/data/demo.db" /></label>
          <template v-else>
            <div class="row-inline">
              <label class="grow">主机<input v-model="connForm.host" data-testid="cf-host" placeholder="127.0.0.1" /></label>
              <label class="port">端口<input v-model.number="connForm.port" data-testid="cf-port" type="number" /></label>
            </div>
            <label>数据库<input v-model="connForm.database" data-testid="cf-database" /></label>
            <label>用户名<input v-model="connForm.userName" data-testid="cf-user" autocomplete="off" /></label>
            <label>密码
              <input
                v-model="connForm.password"
                data-testid="cf-password"
                type="password"
                autocomplete="new-password"
                :placeholder="editingId ? `${MASK}（留空 = 不改）` : ''"
              />
            </label>
          </template>
        </template>
        <label v-else>连接串（含密码，始终遮罩）
          <input
            v-model="connForm.connStr"
            data-testid="cf-connstr"
            type="password"
            autocomplete="off"
            :placeholder="editingId ? `${MASK}（留空 = 不改）` : 'Server=…;Database=…;User Id=…;Password=…'"
          />
        </label>

        <div v-if="testResult" class="test-result" :class="`tr-${testResult.status}`" data-testid="test-result">
          <template v-if="testResult.status === 'ok'">✔ 连接成功{{ testResult.serverVersion ? ` · 服务器 ${testResult.serverVersion}` : '' }}</template>
          <template v-else-if="testResult.status === 'driver_missing'">⚠ 驱动缺失：需安装 <code>{{ testResult.driverPackage ?? formProvider?.driverPackage ?? 'XCode.*' }}</code>{{ testResult.message ? ` · ${redactText(testResult.message)}` : '' }}</template>
          <template v-else>✖ 连接失败：{{ redactText(testResult.message) || '未知原因' }}</template>
        </div>
        <div v-if="connFormError" class="form-error" data-testid="cf-error">{{ connFormError }}</div>

        <div class="modal-actions">
          <button class="btn" data-testid="cf-test" :disabled="testing" @click="testConnection">{{ testing ? '测试中…' : '测试连接' }}</button>
          <span class="spacer" />
          <button class="btn" data-testid="cf-cancel" @click="closeOverlay">取消</button>
          <button class="btn primary" data-testid="cf-save" :disabled="saving" @click="saveConnection">{{ saving ? '保存中…' : '保存' }}</button>
        </div>
      </div>
    </div>

    <!-- 浮层：写审批 -->
    <div v-if="overlay === 'approval' && currentApproval" class="mask" data-testid="approval-modal">
      <div class="modal">
        <h3>写操作审批 <span v-if="currentApproval.source === 'agent'" class="tag tag-agent" data-testid="approval-from-agent">来自 Agent</span></h3>
        <dl class="kv">
          <dt>目标连接</dt><dd>{{ currentApproval.connectionName ?? connName(currentApproval.connectionId) }}</dd>
          <dt>类型</dt><dd>{{ currentApproval.kind }}<span v-if="currentApproval.destructive" class="tag tag-danger" data-testid="approval-destructive">破坏性</span></dd>
          <dt>来源</dt><dd>{{ currentApproval.source === 'agent' ? 'Agent（db_execute_write）' : 'UI 编辑器' }}</dd>
          <dt>预估风险</dt><dd>{{ currentApproval.risk ?? (currentApproval.destructive ? '高：结构或数据不可恢复' : '中：修改数据') }}</dd>
        </dl>
        <pre class="sql-preview" data-testid="approval-sql">{{ redactText(currentApproval.sqlRedacted) }}</pre>
        <p v-if="currentApproval.destructive" class="muted">批准后还需二次确认才会执行。</p>
        <div class="modal-actions">
          <span class="muted">待审批 {{ pendingApprovals.length }} 项</span>
          <span class="spacer" />
          <button class="btn" data-testid="approval-later" @click="closeOverlay">稍后</button>
          <button class="btn danger-outline" data-testid="approval-reject" @click="reject">拒绝</button>
          <button class="btn primary" data-testid="approval-approve" @click="approve">批准</button>
        </div>
      </div>
    </div>

    <!-- 浮层：破坏性二次确认 -->
    <div v-if="overlay === 'destructive' && currentApproval" class="mask" data-testid="destructive-confirm">
      <div class="modal modal-danger">
        <h3>再次确认：破坏性操作</h3>
        <p>该语句会删除结构或清空数据，执行后无法恢复。</p>
        <pre class="sql-preview">{{ redactText(currentApproval.sqlRedacted) }}</pre>
        <div class="modal-actions">
          <span class="spacer" />
          <button class="btn" data-testid="destructive-cancel" @click="confirmDestructive(false)">取消</button>
          <button class="btn danger" data-testid="destructive-ok" @click="confirmDestructive(true)">确认执行</button>
        </div>
      </div>
    </div>

    <!-- 浮层：删除连接 -->
    <div v-if="overlay === 'deleteConn'" class="mask" data-testid="delete-confirm">
      <div class="modal">
        <h3>删除连接「{{ connName(deleteTargetId ?? undefined) }}」？</h3>
        <p>只删除连接配置，<strong>不会删除数据库文件</strong>。</p>
        <div class="modal-actions">
          <span class="spacer" />
          <button class="btn" @click="closeOverlay">取消</button>
          <button class="btn danger" data-testid="delete-ok" @click="confirmDelete">删除配置</button>
        </div>
      </div>
    </div>

    <!-- 浮层：审计抽屉 -->
    <div v-if="overlay === 'audit'" class="mask mask-drawer" data-testid="audit-drawer" @click.self="closeOverlay">
      <aside class="drawer">
        <div class="drawer-head">
          <h3>审计</h3>
          <select v-model="auditFilter" data-testid="audit-filter">
            <option value="">全部</option>
            <option v-for="k in AUDIT_KINDS" :key="k" :value="k">{{ k }}</option>
          </select>
          <span class="muted">筛选 {{ filteredAudit.length }}/{{ audit.length }}</span>
          <span class="spacer" />
          <button class="icon" data-testid="audit-close" @click="closeOverlay">✕</button>
        </div>
        <div class="list">
          <div v-if="filteredAudit.length === 0" class="muted pad">无匹配记录</div>
          <div v-for="a in filteredAudit" :key="a.id" class="audit-row" :data-kind="a.kind">
            <div class="audit-meta">
              <span class="time">{{ fmtTime(a.createTime) }}</span>
              <span class="pill" :class="`k-${a.kind.split('_')[0]}`">{{ a.kind }}</span>
              <span v-if="a.connectionId" class="muted">{{ connName(a.connectionId) }}</span>
              <span v-if="a.decision" class="muted">· {{ a.decision }}</span>
            </div>
            <code v-if="a.sqlRedacted" class="sqltext">{{ redactText(a.sqlRedacted) }}</code>
            <div v-if="a.detail" class="muted">{{ redactText(a.detail) }}</div>
          </div>
        </div>
      </aside>
    </div>
  </div>
</template>

<style scoped>
.dba-root {
  --bg: var(--el-bg-color);
  --panel: var(--el-bg-color-overlay);
  --line: var(--el-border-color);
  --text: var(--el-text-color-primary);
  --muted: var(--el-text-color-secondary);
  --accent: var(--el-color-primary);
  --ok: var(--el-color-success);
  --warn: var(--el-color-warning);
  --danger: var(--el-color-danger);
  position: relative;
  height: 100%;
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding: 12px;
  box-sizing: border-box;
  color: var(--text);
  font-size: 13px;
  overflow: hidden;
}
.dba-root > * { flex-shrink: 0; }
.dba-root > .body { flex: 1 1 auto; min-height: 0; }
.head { display: flex; align-items: flex-end; gap: 12px; }
.title-block { flex: 1; min-width: 0; }
.view-title { margin: 0; font-size: 18px; font-weight: 600; }
.ver { font-size: 12px; color: var(--muted); font-weight: 500; }
.sub { margin: 2px 0 0; color: var(--muted); font-size: 12px; }
.head-actions { display: flex; gap: 8px; align-items: center; }
.banner { border-radius: 8px; padding: 8px 12px; border: 1px solid; }
.banner-error { color: var(--danger); border-color: var(--danger); background: var(--el-color-danger-light-9); }
.banner-warn { color: var(--warn); border-color: var(--warn); background: var(--el-color-warning-light-9); }
.banner-info { color: var(--accent); border-color: var(--accent); background: var(--el-color-primary-light-9); }
.body { display: grid; grid-template-columns: 260px 1fr; gap: 10px; }
.side {
  border: 1px solid var(--line); border-radius: 12px; background: var(--panel);
  padding: 10px; overflow: auto; display: flex; flex-direction: column; gap: 2px; min-height: 0;
}
.side > * { flex-shrink: 0; }
.group-title { margin: 10px 0 4px; color: var(--muted); font-size: 12px; font-weight: 600; }
.node { display: flex; align-items: center; gap: 6px; padding: 3px 6px; border-radius: 6px; cursor: pointer; white-space: nowrap; }
.node:hover { background: var(--el-fill-color-light); }
.node.on { background: var(--el-color-primary-light-8); }
.node .name { overflow: hidden; text-overflow: ellipsis; }
.node.table { padding-left: 20px; }
.node.column { padding-left: 38px; cursor: default; color: var(--muted); font-size: 12px; }
.caret { width: 10px; color: var(--muted); }
.lock { font-size: 11px; }
.node-actions { margin-left: auto; display: none; gap: 2px; }
.node:hover .node-actions, .node.on .node-actions { display: inline-flex; }
.type { margin-left: 4px; color: var(--muted); font-size: 11px; font-family: ui-monospace, monospace; }
.pk { color: var(--warn); font-size: 10px; border: 1px solid var(--warn); border-radius: 4px; padding: 0 3px; }
.nn { color: var(--muted); font-size: 10px; }
.empty-guide { border: 1px dashed var(--line); border-radius: 10px; padding: 10px; display: grid; gap: 6px; }
.empty-guide p { margin: 0; }
.skeleton { display: grid; gap: 8px; padding: 8px 0; }
.sk-line { height: 12px; border-radius: 6px; background: var(--el-fill-color); }
.main { display: flex; flex-direction: column; gap: 8px; min-width: 0; min-height: 0; overflow: hidden; }
.main > * { flex-shrink: 0; }
.conn-bar { display: flex; align-items: center; gap: 8px; min-width: 0; }
.conn-name { white-space: nowrap; }
.ellipsis { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; min-width: 0; }
.tag { border: 1px solid var(--line); border-radius: 999px; padding: 0 8px; font-size: 11px; white-space: nowrap; }
.tag-ro { color: var(--warn); border-color: var(--warn); }
.tag-untested { color: var(--muted); border-style: dashed; }
.tag-muted { color: var(--muted); }
.tag-agent { color: var(--accent); border-color: var(--accent); }
.tag-danger { color: var(--danger); border-color: var(--danger); margin-left: 6px; }
.editor {
  height: 96px; resize: vertical; background: var(--bg); color: var(--text);
  border: 1px solid var(--line); border-radius: 10px; padding: 8px 10px;
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 13px; box-sizing: border-box;
}
.run-bar { display: flex; align-items: center; gap: 8px; }
.spacer { flex: 1; }
.stats { color: var(--muted); font-size: 12px; }
.result-error { border: 1px solid var(--danger); background: var(--el-color-danger-light-9); color: var(--danger); border-radius: 10px; padding: 8px 12px; display: grid; gap: 2px; }
.result-error .code { font-family: ui-monospace, monospace; font-size: 11px; }
.err-msg { color: var(--text); font-family: ui-monospace, monospace; font-size: 12px; white-space: pre-wrap; }
.main > .grid-wrap { flex: 1 1 auto; min-height: 80px; overflow: auto; border: 1px solid var(--line); border-radius: 10px; background: var(--panel); }
.truncated { padding: 6px 10px; color: var(--warn); border-bottom: 1px solid var(--line); font-size: 12px; }
.grid { border-collapse: collapse; width: max-content; min-width: 100%; font-family: ui-monospace, monospace; font-size: 12px; }
.grid th, .grid td { border-bottom: 1px solid var(--line); padding: 4px 10px; text-align: left; max-width: 280px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.grid th { position: sticky; top: 0; background: var(--panel); font-weight: 600; }
.grid td.null { color: var(--muted); font-style: italic; }
.rownum { color: var(--muted); width: 36px; }
.placeholder { padding: 16px; }
.pager { display: flex; align-items: center; gap: 8px; }
.pager select { margin-left: 4px; background: var(--bg); color: var(--text); border: 1px solid var(--line); border-radius: 6px; }
.bottom { height: 132px; border: 1px solid var(--line); border-radius: 10px; background: var(--panel); display: flex; flex-direction: column; overflow: hidden; }
.tabs { display: flex; gap: 2px; border-bottom: 1px solid var(--line); padding: 4px 6px 0; }
.tab { border: none; background: transparent; color: var(--muted); padding: 4px 10px; cursor: pointer; border-bottom: 2px solid transparent; }
.tab.on { color: var(--text); border-bottom-color: var(--accent); }
.list { overflow: auto; flex: 1; }
.row { display: flex; align-items: center; gap: 8px; padding: 3px 10px; white-space: nowrap; }
.row.fail .sqltext { color: var(--danger); }
.time { color: var(--muted); font-family: ui-monospace, monospace; font-size: 11px; }
.sqltext { overflow: hidden; text-overflow: ellipsis; min-width: 0; flex: 1; font-size: 12px; }
.tool { color: var(--accent); font-size: 12px; }
.pill { border: 1px solid var(--line); border-radius: 999px; padding: 0 6px; font-size: 11px; }
.st-ok { color: var(--ok); border-color: var(--ok); }
.st-awaiting_approval, .st-truncated { color: var(--warn); border-color: var(--warn); }
.st-rejected, .k-rejected { color: var(--danger); border-color: var(--danger); }
.k-approval { color: var(--warn); border-color: var(--warn); }
.k-query { color: var(--accent); border-color: var(--accent); }
.muted { color: var(--muted); font-size: 12px; }
.pad { padding: 6px 10px; }
.btn {
  border: 1px solid var(--line); background: transparent; color: var(--text);
  border-radius: 8px; padding: 5px 12px; cursor: pointer; font-size: 13px; white-space: nowrap;
}
.btn:disabled { opacity: 0.5; cursor: not-allowed; }
.btn.primary { background: var(--accent); border-color: var(--accent); color: var(--el-color-white); }
.btn.block { width: 100%; }
.btn.warn { color: var(--warn); border-color: var(--warn); }
.btn.danger { background: var(--danger); border-color: var(--danger); color: var(--el-color-white); }
.btn.danger-outline { color: var(--danger); border-color: var(--danger); }
.icon { border: none; background: transparent; color: var(--muted); cursor: pointer; padding: 0 4px; }
.icon.danger:hover { color: var(--danger); }
.badge { border-radius: 999px; padding: 4px 10px; font-size: 12px; border: 1px solid var(--line); background: transparent; cursor: pointer; }
.badge-ask { color: var(--accent); border-color: var(--accent); }
.badge-auto { color: var(--ok); border-color: var(--ok); }
.mask { position: absolute; inset: 0; background: var(--el-mask-color); display: flex; align-items: center; justify-content: center; z-index: 10; }
.modal {
  width: min(520px, 92%); max-height: 90%; overflow: auto; background: var(--panel); border: 1px solid var(--line);
  border-radius: 14px; padding: 16px; display: grid; gap: 10px; box-sizing: border-box;
}
.modal h3 { margin: 0; font-size: 16px; display: flex; align-items: center; gap: 8px; }
.modal p { margin: 0; }
.modal label { display: grid; gap: 4px; font-size: 12px; color: var(--muted); }
.modal input, .modal select {
  background: var(--bg); color: var(--text); border: 1px solid var(--line); border-radius: 8px;
  padding: 6px 10px; font-size: 13px; box-sizing: border-box; width: 100%;
}
.modal-danger { border-color: var(--danger); }
.row-inline { display: flex; gap: 8px; align-items: center; }
.row-inline select { flex: 1; }
.grow { flex: 1; }
.port { width: 110px; }
.mode-switch { display: flex; gap: 6px; }
.chip { border: 1px solid var(--line); background: transparent; color: var(--muted); border-radius: 999px; padding: 3px 12px; cursor: pointer; }
.chip.on { border-color: var(--accent); color: var(--accent); }
.test-result { border-radius: 8px; padding: 8px 10px; border: 1px solid; }
.tr-ok { color: var(--ok); border-color: var(--ok); background: var(--el-color-success-light-9); }
.tr-error { color: var(--danger); border-color: var(--danger); background: var(--el-color-danger-light-9); }
.tr-driver_missing { color: var(--warn); border-color: var(--warn); background: var(--el-color-warning-light-9); }
.form-error { color: var(--danger); }
.modal-actions { display: flex; gap: 8px; align-items: center; }
.kv { display: grid; grid-template-columns: 72px 1fr; gap: 4px 8px; margin: 0; }
.kv dt { color: var(--muted); }
.kv dd { margin: 0; }
.sql-preview { margin: 0; background: var(--bg); border: 1px solid var(--line); border-radius: 8px; padding: 8px 10px; white-space: pre-wrap; word-break: break-all; font-size: 12px; }
.mask-drawer { justify-content: flex-end; align-items: stretch; }
.drawer { width: min(420px, 90%); background: var(--panel); border-left: 1px solid var(--line); display: flex; flex-direction: column; }
.drawer-head { display: flex; align-items: center; gap: 8px; padding: 12px; border-bottom: 1px solid var(--line); }
.drawer-head h3 { margin: 0; font-size: 15px; }
.drawer-head select { background: var(--bg); color: var(--text); border: 1px solid var(--line); border-radius: 6px; }
.audit-row { padding: 8px 12px; border-bottom: 1px solid var(--line); display: grid; gap: 4px; }
.audit-meta { display: flex; gap: 6px; align-items: center; }
</style>
