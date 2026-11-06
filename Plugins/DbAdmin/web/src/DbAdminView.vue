<script setup lang="ts">
/**
 * 数据库管理 · 可审批的 SQL 工作台（多标签查询版，044 多标签工作区）
 * 左：连接 + schema 树（表名过滤 / 刷新）；右：多查询标签（同表聚焦，互不覆盖）+ 多语句结果标签 + 分页结果格。
 * 浮层互斥：连接表单 → 审批 → 破坏性二次确认 → 删除确认 → 审计抽屉 → 单元格详情（同一时刻只有一个）。
 * `fixture` 存在即演示模式：不发网络（CT 画廊用）。
 */
import { computed, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import { ApiError } from './http'
import { dbAdminApi, fetchPluginVersion } from './api'
import { classifyQueryResponse, errorReason, kindLabel, orchestrateApprove, rejectRequest } from './approvalFlow'
import {
  buildConnStr, defaultPort, emptyForm, formFromConnection, isSqlite, isTested, providerLabel as labelOf, toSubmitBody, validateForm,
} from './connForm'
import {
  agentEchoesOf, listOf, normApproval, normAudit, normConnection, normHistory, normProvider, normTable, normTableDetail, normTest,
  replyToState,
} from './normalize'
import type { ReplyState } from './normalize'
import { PAGE_SIZES, canNext, canPrev, clampPage, clampPageSize, pageCount, rangeLabel } from './pager'
import { MASK, maskConnStr, redactText } from './redact'
import { buildSnapshot, loadSnapshot, restoredToConnection, saveSnapshot } from './restore'
import { csvFileName, csvWithBom, rowText, toCsv } from './exportCsv'
import {
  DEFAULT_PAGE_SIZE, buildResultSets, markSkippedFrom, resultDot, resultDotTitle, resultLabel, statsLabel, activeResult as resultAt,
} from './results'
import type { ResultSet } from './results'
import { SqlSplitError, splitExecutable } from './sqlSplit'
import {
  MAX_TABS, closeTab as closeTabOf, findTab, manualTabKey, openManualTab, openTableTab, replaceTab, tableTabKey, tabTitle,
} from './tabs'
import type { QueryTab } from './tabs'
import type {
  AgentToolEcho, DbAdminFixture, DbApproval, DbAuditLog, DbConnection, DbConnectionInput, DbProvider,
  DbQueryHistory, DbTable, DbTableDetail, OverlayKind, PermissionMode, QueryResponse, ResolveApprovalRequest,
  TestConnectionResult,
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
const lastConnectionId = ref<string | null>(null)
const tables = ref<Record<string, DbTable[]>>({})
/** 每个连接独立的「加载表」状态：点击连接即显示局部 loading，空态必须等加载结束再出现 */
const tablesLoading = ref<Record<string, boolean>>({})
/** 请求序号：force 重拉时旧响应不得覆盖新结果（竞态防护） */
const tableLoadSeq: Record<string, number> = {}
const details = ref<Record<string, DbTableDetail>>({})
const expanded = ref<Set<string>>(new Set())
/** 树内「筛选表名」（本地过滤，不发请求） */
const tableFilter = ref('')

/** 多查询标签（FR-1~6）；活动标签派生编辑器/结果/分页 */
const tabs = ref<QueryTab[]>([])
const activeTabKey = ref<string | null>(null)
/** 本机界面偏好：connectionId → 默认表名（不进后端请求体，AC-12） */
const defaultTables = ref<Record<string, string>>({})

/** 编辑器选区长度（决定按钮文案「执行选中」） */
const selectionLen = ref(0)
const editorEl = ref<HTMLTextAreaElement | null>(null)
/** 结果行选中（复制整行用） */
const selectedRowIndex = ref<number | null>(null)
/** 单元格详情浮层 */
const cellDetail = ref<{ col: string; value: string } | null>(null)

/** demo 模式的顺序应答队列（多语句批量） */
const fixtureReplies = ref<Array<{ httpStatus: number; body: unknown }>>([])
/** 审批发起位置（结果回填到发起标签，FR-10/FR-18） */
const approvalCtx = ref<{ tabKey: string; resultIndex: number } | null>(null)

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
const activeTab = computed(() => findTab(tabs.value, activeTabKey.value))
const activeSet = computed<ResultSet | null>(() =>
  activeTab.value ? resultAt(activeTab.value.results, activeTab.value.activeResultIndex) : null)
const sql = computed({
  get: () => activeTab.value?.sql ?? '',
  set: (v: string) => {
    if (activeTabKey.value) patchTab(activeTabKey.value, { sql: v })
  },
})
const result = computed(() => activeSet.value?.result ?? null)
const resultError = computed(() => activeSet.value?.error ?? null)
const running = computed(() => !!activeTab.value?.running || !!activeSet.value?.running)
const rows = computed(() => result.value?.rows ?? [])
const page = computed(() => activeSet.value?.page ?? 1)
const pageSize = computed(() => activeSet.value?.pageSize ?? DEFAULT_PAGE_SIZE)
const totalPages = computed(() => pageCount(result.value?.total, pageSize.value))
const pendingApprovals = computed(() => approvals.value.filter(a => a.status === 'pending'))
const currentApproval = computed(() => approvals.value.find(a => a.id === activeApprovalId.value) ?? null)
const formProvider = computed(() => providerOf(connForm.provider))
/** 后端真实审计 kind：query / classify / rejected / approval_pending|approved|rejected / write_result /
 *  conn_create|update|delete|test / permission_mode / agent；approval、conn 为前缀分组 */
const AUDIT_KINDS = ['query', 'classify', 'rejected', 'approval', 'write_result', 'conn', 'permission_mode', 'agent']
const filteredAudit = computed(() => {
  const f = auditFilter.value
  if (!f) return audit.value
  return audit.value.filter(a => (f === 'approval' || f === 'conn' ? a.kind.startsWith(`${f}_`) : a.kind === f))
})
const runDisabledReason = computed(() => {
  if (unauthorized.value) return '未授权'
  if (!activeTab.value) return activeId.value ? '点一张表或新建查询打开标签' : '先在左侧选择连接'
  const tab = activeTab.value
  if (tab.running) return '执行中'
  if (!tab.sql.trim()) return '编辑器为空'
  return ''
})
/** 有选区 → 「执行选中」；多语句 → 提示会分结果标签展示 */
const runLabel = computed(() => {
  if (activeTab.value?.running) return '执行中…'
  return selectionLen.value > 0 ? '执行选中' : '执行'
})

function isSystem(c: DbConnection): boolean {
  return c.system ?? c.readonly
}
function providerOf(id: string | undefined): DbProvider | undefined {
  if (!id) return undefined
  const k = id.toLowerCase()
  return providers.value.find(p => p.id === k) ?? { id: k, displayName: labelOf(k), driverAvailable: true }
}
function providerLabel(id: string | undefined): string {
  return labelOf(id, providers.value)
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

/* ---------------- 标签（FR-1~6 / FR-10~11） ---------------- */
function patchTab(key: string, patch: Partial<QueryTab>) {
  tabs.value = replaceTab(tabs.value, key, patch)
}
function tabExists(key: string, seq?: number): boolean {
  const t = findTab(tabs.value, key)
  return !!t && (seq === undefined || t.runSeq === seq)
}
function activateTab(key: string) {
  activeTabKey.value = key
  const t = findTab(tabs.value, key)
  if (t) {
    activeId.value = t.connectionId
    lastConnectionId.value = t.connectionId
  }
  selectionLen.value = 0
  selectedRowIndex.value = null
}
/** 打开/聚焦表标签：已存在只聚焦，绝不重置 SQL 与结果（FR-2/FR-3） */
function openTableQuery(connId: string, table: string) {
  const r = openTableTab(tabs.value, connId, table)
  if (r.full) {
    banner.value = { kind: 'warn', text: `已打开 ${MAX_TABS} 个查询标签，请先关闭一些再继续` }
    return
  }
  if (r.created) tabs.value = r.tabs
  if (r.key) activateTab(r.key)
  activeId.value = connId
  lastConnectionId.value = connId
  const key = `t:${connId}/${table}`
  if (!expanded.value.has(key)) expanded.value = new Set([...expanded.value, key])
  void loadDetail(connId, table)
}
function newQueryTab() {
  const connId = activeId.value
  if (!connId) {
    banner.value = { kind: 'warn', text: '先在左侧选择一个连接，再新建查询' }
    return
  }
  const r = openManualTab(tabs.value, connId)
  if (r.full) {
    banner.value = { kind: 'warn', text: `已打开 ${MAX_TABS} 个查询标签，请先关闭一些再继续` }
    return
  }
  tabs.value = r.tabs
  if (r.key) activateTab(r.key)
}
function closeTabAction(key: string) {
  const res = closeTabOf(tabs.value, key, activeTabKey.value)
  tabs.value = res.tabs
  activeTabKey.value = res.activeKey
  if (approvalCtx.value?.tabKey === key) approvalCtx.value = null
  const t = findTab(tabs.value, res.activeKey)
  if (t) activeId.value = t.connectionId
}
/** 删除连接时一并关闭其标签 */
function removeConnectionTabs(connId: string) {
  const remain = tabs.value.filter(t => t.connectionId !== connId)
  tabs.value = remain
  if (activeTabKey.value && !remain.some(t => t.key === activeTabKey.value)) {
    activeTabKey.value = remain[0]?.key ?? null
  }
  if (approvalCtx.value && !remain.some(t => t.key === approvalCtx.value!.tabKey)) approvalCtx.value = null
}
/** 标签上的状态点：运行中 > 失败 > 待审批 > 成功 */
function tabDot(t: QueryTab): string {
  if (t.running) return 'pending'
  const sets = t.results
  if (sets.some(s => s.status === 'error')) return 'error'
  if (sets.some(s => s.status === 'awaiting_approval')) return 'awaiting'
  if (sets.some(s => s.status === 'ok')) return 'ok'
  return ''
}
function selectResult(i: number) {
  if (!activeTabKey.value) return
  patchTab(activeTabKey.value, { activeResultIndex: i })
  selectedRowIndex.value = null
}

/* ---------------- fixture ---------------- */
/** fixture 里的单条 HTTP 应答 → 单个结果（预置展示用） */
function resultFromReply(reply: { httpStatus: number; body: unknown }, sqlText: string, size = DEFAULT_PAGE_SIZE): ResultSet {
  const base: ResultSet = { sql: sqlText, status: 'pending', result: null, error: null, page: 1, pageSize: size, running: false }
  const st = replyToState(reply.httpStatus, reply.body)
  if (st.kind === 'error') return { ...base, status: 'error', error: { ...st.error, message: redactText(st.error.message) } }
  const r = st.result
  const action = classifyQueryResponse(r)
  if (action === 'red_bar') {
    return { ...base, status: 'error', error: { code: r.code ?? 'rejected_hard', message: redactText(r.message ?? ''), httpStatus: r.httpStatus } }
  }
  if (action === 'open_approval') return { ...base, status: 'awaiting_approval', result: r }
  return { ...base, status: 'ok', result: r, page: r.page ?? 1, pageSize: clampPageSize(r.pageSize ?? size) }
}

function applyFixture(f: DbAdminFixture) {
  restoredHint.value = ''
  version.value = f.version ?? '0.0.0-ct'
  loading.value = !!f.loading
  unauthorized.value = !!f.unauthorized
  banner.value = f.banner ?? null
  permissionMode.value = f.permissionMode ?? 'AskWrite'
  // 所有数据字段都是后端原始 JSON，经与真实请求相同的 normalize 进入 UI
  providers.value = listOf(f.providers ?? [], normProvider)
  connections.value = listOf(f.connections ?? [], normConnection)
  activeId.value = f.activeConnectionId ?? null
  tables.value = Object.fromEntries(Object.entries(f.tables ?? {}).map(([k, v]) => [k, listOf(v, normTable)]))
  tablesLoading.value = { ...(f.tablesLoading ?? {}) }
  details.value = Object.fromEntries(Object.entries(f.tableDetails ?? {}).map(([k, v]) => [k, normTableDetail(v)]))
  expanded.value = new Set(f.expanded ?? [])
  defaultTables.value = { ...(f.defaultTables ?? {}) }
  lastConnectionId.value = f.lastConnectionId ?? activeId.value
  tableFilter.value = ''
  fixtureReplies.value = [...(f.replies ?? [])]
  approvalCtx.value = null

  const tabsIn: QueryTab[] = (f.tabs ?? []).map((t, i) => {
    const repliesIn = t.replies ?? (t.reply ? [t.reply] : [])
    return {
      key: t.key ?? (t.table ? tableTabKey(t.connectionId, t.table) : manualTabKey(t.connectionId, i)),
      connectionId: t.connectionId,
      table: t.table,
      sql: t.sql ?? '',
      results: repliesIn.map(r => resultFromReply(r, t.sql ?? '')),
      activeResultIndex: 0,
      lastSql: '',
      running: false,
      runSeq: 0,
    }
  })
  if (tabsIn.length === 0 && activeId.value && f.sql !== undefined) {
    const connId = activeId.value
    tabsIn.push({
      key: manualTabKey(connId, 0), connectionId: connId, sql: f.sql,
      results: [], activeResultIndex: 0, lastSql: '', running: false, runSeq: 0,
    })
  }
  tabs.value = tabsIn
  activeTabKey.value = f.activeTabKey ?? tabsIn[0]?.key ?? null
  // 旧 fixture 的单条 reply：挂到活动标签的首个结果
  if (f.reply && tabs.value.length > 0) {
    const idx = Math.max(0, tabs.value.findIndex(t => t.key === activeTabKey.value))
    const tab = tabs.value[idx]
    if (tab) tabs.value = replaceTab(tabs.value, tab.key, { results: [resultFromReply(f.reply, tab.sql)] })
  }
  approvals.value = listOf(f.approvals ?? [], normApproval)
  history.value = listOf(f.history ?? [], normHistory)
  audit.value = listOf(f.audit ?? [], normAudit)
  auditFilter.value = f.auditFilter ?? ''
  agentEchoes.value = agentEchoesOf(f.audit ?? [])
  sideTab.value = f.sideTab ?? 'history'
  const { editingId: fixtureEditingId, ...fixtureForm } = f.connForm ?? {}
  Object.assign(connForm, emptyForm(fixtureForm.provider ?? 'sqlite'), fixtureForm)
  editingId.value = fixtureEditingId ?? null
  connFormError.value = f.connFormError ?? ''
  testResult.value = f.testResult ? normTest(f.testResult) : null
  activeApprovalId.value = f.activeApprovalId ?? null
  overlay.value = f.overlay ?? null
  selectedRowIndex.value = null
  cellDetail.value = null
  selectionLen.value = 0
  if (f.seedRestore) applyRestore()
}

/* ---------------- S-R ---------------- */
function applyRestore(): boolean {
  const snap = loadSnapshot()
  if (!snap) return false
  if (connections.value.length === 0) connections.value = snap.connections.map(restoredToConnection)
  if (tabs.value.length === 0) {
    tabs.value = snap.tabs
      .filter(t => connections.value.some(c => c.id === t.connectionId))
      .map(t => ({
        key: t.key, connectionId: t.connectionId, table: t.table, sql: t.sql,
        results: [], activeResultIndex: 0, lastSql: '', running: false, runSeq: 0,
      }))
    if (snap.activeTabKey) activeTabKey.value = snap.activeTabKey
    else if (tabs.value[0]) activeTabKey.value = tabs.value[0].key
    const tab = findTab(tabs.value, activeTabKey.value)
    if (tab) activeId.value = tab.connectionId
  }
  defaultTables.value = { ...snap.defaultTables, ...defaultTables.value }
  if (!activeId.value && snap.activeConnectionId) activeId.value = snap.activeConnectionId
  const n = snap.tabs.length
  restoredHint.value = `已从上次会话恢复：${snap.connections.length} 个连接 · ${n} 个查询标签（结果集与密码不恢复；待审批已从服务端重拉）`
  return true
}
function persist() {
  if (loading.value || unauthorized.value) return
  saveSnapshot(buildSnapshot(
    connections.value,
    tabs.value.map(t => ({ key: t.key, connectionId: t.connectionId, table: t.table, sql: t.sql })),
    activeTabKey.value,
    defaultTables.value,
  ))
}
watch([tabs, activeTabKey, connections, defaultTables], persist, { deep: true })

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
    // 丢弃已不存在连接的标签（BR-10）
    const gone = tabs.value.filter(t => !connections.value.some(c => c.id === t.connectionId))
    if (gone.length) {
      const remain = tabs.value.filter(t => connections.value.some(c => c.id === t.connectionId))
      tabs.value = remain
      if (!remain.some(t => t.key === activeTabKey.value)) activeTabKey.value = remain[0]?.key ?? null
    }
    if (activeId.value && !connections.value.some(c => c.id === activeId.value)) activeId.value = null
    if (!activeId.value) activeId.value = lastConnectionId.value && connections.value.some(c => c.id === lastConnectionId.value)
      ? lastConnectionId.value
      : (findTab(tabs.value, activeTabKey.value)?.connectionId ?? null)
    loading.value = false
    if (activeId.value) {
      expanded.value = new Set([...expanded.value, `c:${activeId.value}`])
      void loadTables(activeId.value)
    }
    await Promise.all([refreshApprovals(true), refreshHistory(), refreshAgentEchoes()])
  } catch (e) {
    loading.value = false
    handleError(e, '加载')
  }
}

async function loadTables(id: string, force = false) {
  if (isDemo.value || (!force && tables.value[id])) return // force：建表/写执行后强制重拉（R1）
  if (!force && tablesLoading.value[id]) return // 同一连接加载中不重复发请求
  const seq = (tableLoadSeq[id] ?? 0) + 1
  tableLoadSeq[id] = seq
  tablesLoading.value = { ...tablesLoading.value, [id]: true }
  try {
    const data = await dbAdminApi.tables(id)
    if (tableLoadSeq[id] !== seq) return // 旧请求被 force 重拉取代，丢弃
    tables.value = { ...tables.value, [id]: data }
  } catch (e) {
    if (tableLoadSeq[id] === seq) handleError(e, '读取表')
  } finally {
    if (tableLoadSeq[id] === seq) tablesLoading.value = { ...tablesLoading.value, [id]: false }
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
/** S8：§3.1 ④ GET /audit?kind=agent（失败不打断主流程） */
async function refreshAgentEchoes() {
  if (isDemo.value) return
  try {
    agentEchoes.value = await dbAdminApi.agentEchoes()
  } catch {
    /* 回显失败不打断主流程 */
  }
}
function openAgentTab() {
  sideTab.value = 'agent'
  void refreshAgentEchoes()
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
/** 连接点击：展开并加载表（有默认表 → 直接打开该表标签）；已展开 → 收起（FR-7 / AC-12） */
function selectConnection(c: DbConnection) {
  const willExpand = !expanded.value.has(`c:${c.id}`)
  activeId.value = c.id
  lastConnectionId.value = c.id
  if (!willExpand) {
    toggle(`c:${c.id}`)
    return
  }
  expanded.value = new Set([...expanded.value, `c:${c.id}`])
  void loadTables(c.id)
  const dt = (defaultTables.value[c.id] ?? '').trim()
  if (dt) openTableQuery(c.id, dt)
}
/** 表行单击：打开/聚焦查询标签 + 展开列（不是「重置」已打开的标签） */
function onTableRowClick(connId: string, t: DbTable) {
  openTableQuery(connId, t.name)
}
/** 展开箭头单独单击：只切换列展开，不开标签（AC-13） */
function onTableCaretClick(connId: string, t: DbTable) {
  const key = `t:${connId}/${t.name}`
  toggle(key)
  if (expanded.value.has(key)) void loadDetail(connId, t.name)
}
function refreshTables(id: string) {
  void loadTables(id, true)
}
/** 树内表名过滤（本地过滤，不发请求，FR-20） */
function tablesFor(connId: string): DbTable[] {
  const list = tables.value[connId] ?? []
  const q = tableFilter.value.trim().toLowerCase()
  return q ? list.filter(t => t.name.toLowerCase().includes(q)) : list
}

/* ---------------- 执行（选区 / 多语句 / 批量，FR-14~19） ---------------- */
/** 编辑器选区非空 → 只执行选中；否则执行全文 */
function textToRun(full: string): string {
  const el = editorEl.value
  if (!el) return full.trim()
  const s = el.selectionStart ?? 0
  const e = el.selectionEnd ?? 0
  if (e > s) {
    const sel = full.slice(s, e)
    if (sel.trim()) return sel
  }
  return full.trim()
}
async function runFromEditor() {
  const tab = activeTab.value
  if (!tab || runDisabledReason.value) return
  const text = textToRun(tab.sql)
  let statements: string[]
  try {
    statements = splitExecutable(text)
  } catch (e) {
    banner.value = { kind: 'error', text: e instanceof SqlSplitError ? `SQL 切分失败：${e.message}` : String(e) }
    return
  }
  if (statements.length === 0) {
    banner.value = { kind: 'warn', text: '没有可执行的语句（选区/编辑器为空或只有注释）' }
    return
  }
  const r = findTab(tabs.value, tab.key)
  if (!r) return
  const sets = buildResultSets(statements, r.results[r.activeResultIndex]?.pageSize ?? DEFAULT_PAGE_SIZE)
  tabs.value = replaceTab(tabs.value, tab.key, {
    results: sets,
    activeResultIndex: 0,
    lastSql: text,
    running: true,
    runSeq: r.runSeq + 1,
    batch: { tabKey: tab.key, statements, nextIndex: 0 },
  })
  selectedRowIndex.value = null
  banner.value = null
  await runFrom(tab.key, statements, 0, sets[0]?.pageSize ?? DEFAULT_PAGE_SIZE)
}

/** 顺序执行（每条单语句一次 POST /query）；失败即停；待审批暂停 */
async function runFrom(tabKey: string, statements: string[], startIndex: number, size: number) {
  const start = findTab(tabs.value, tabKey)
  if (!start) return
  const connId = start.connectionId
  const seq = start.runSeq
  let wrote = false
  for (let i = startIndex; i < statements.length; i++) {
    if (!tabExists(tabKey, seq)) return
    patchResult(tabKey, i, { running: true })
    const state = await sendQuery(connId, statements[i], 1, size)
    if (!tabExists(tabKey, seq)) return
    const applied = applyReplyToSet(tabKey, i, state)
    if (applied.action === 'red_bar') {
      markSkipped(tabKey, i + 1, i)
      break
    }
    if (applied.action === 'open_approval') {
      const r = applied.result
      approvalCtx.value = { tabKey, resultIndex: i }
      patchTab(tabKey, { running: false, batch: { tabKey, statements, nextIndex: i + 1, approvalId: r?.approvalId } })
      await refreshApprovals()
      activeApprovalId.value = r?.approvalId ?? pendingApprovals.value[0]?.id ?? null
      if (activeApprovalId.value) openOverlay('approval')
      return
    }
    if (applied.result?.kind === 'write' || applied.result?.affectedRows !== undefined) wrote = true
  }
  patchTab(tabKey, { running: false, batch: undefined })
  if (wrote && !isDemo.value) void loadTables(connId, true) // R1：写后 schema 树即时可见
  void refreshHistory()
}
async function sendQuery(connectionId: string, sqlText: string, pageNum: number, size: number): Promise<ReplyState> {
  if (isDemo.value) {
    const next = fixtureReplies.value.shift() ?? props.fixture?.reply ?? null
    if (!next) {
      return { kind: 'result', result: { status: 'ok', columns: [], rows: [], total: 0, page: pageNum, pageSize: size } }
    }
    return replyToState(next.httpStatus, next.body)
  }
  try {
    const q = await dbAdminApi.query({ connectionId, sql: sqlText, page: pageNum, pageSize: clampPageSize(size) })
    return { kind: 'result', result: { ...q, page: q.page ?? pageNum, pageSize: q.pageSize ?? clampPageSize(size) } }
  } catch (e) {
    if (e instanceof ApiError && e.status === 401) {
      unauthorized.value = true
      return { kind: 'error', error: { code: 'unauthorized', message: '401 未授权', httpStatus: 401 } }
    }
    if (e instanceof ApiError) {
      return replyToState(e.status, (e.body as unknown) ?? { code: e.code, message: e.message })
    }
    return { kind: 'error', error: { code: 'sql_error', message: e instanceof Error ? e.message : String(e) } }
  }
}
function patchResult(tabKey: string, index: number, patch: Partial<ResultSet>) {
  const tab = findTab(tabs.value, tabKey)
  if (!tab) return
  tabs.value = replaceTab(tabs.value, tabKey, {
    results: tab.results.map((s, i) => (i === index ? { ...s, ...patch } : s)),
  })
}
function markSkipped(tabKey: string, from: number, failed: number) {
  const tab = findTab(tabs.value, tabKey)
  if (!tab) return
  tabs.value = replaceTab(tabs.value, tabKey, { results: markSkippedFrom(tab.results, from, failed) })
}
/** 一次 HTTP 应答写进指定结果槽，返回 UI 动作（审批/红条/结果） */
function applyReplyToSet(
  tabKey: string,
  index: number,
  state: ReplyState,
): { action: 'show_result' | 'open_approval' | 'red_bar'; result?: QueryResponse } {
  const tab = findTab(tabs.value, tabKey)
  if (!tab) return { action: 'red_bar' }
  const cur = tab.results[index]
  const baseSize = clampPageSize(cur?.pageSize ?? DEFAULT_PAGE_SIZE)
  if (state.kind === 'error') {
    patchResult(tabKey, index, {
      status: 'error', result: null, running: false,
      error: { ...state.error, message: redactText(state.error.message) },
    })
    return { action: 'red_bar' }
  }
  const r = state.result
  const action = classifyQueryResponse(r)
  if (action === 'red_bar') {
    patchResult(tabKey, index, {
      status: 'error', result: null, running: false,
      error: { code: r.code ?? 'rejected_hard', message: redactText(r.message ?? ''), httpStatus: r.httpStatus },
    })
    return { action: 'red_bar', result: r }
  }
  if (action === 'open_approval') {
    patchResult(tabKey, index, { status: 'awaiting_approval', result: r, error: null, running: false })
    return { action: 'open_approval', result: r }
  }
  patchResult(tabKey, index, {
    status: 'ok', result: r, error: null, running: false,
    page: r.page ?? 1, pageSize: clampPageSize(r.pageSize ?? baseSize),
  })
  return { action: 'show_result', result: r }
}
function onEditorKey(e: KeyboardEvent) {
  if ((e.key === 'Enter' && (e.ctrlKey || e.metaKey)) || e.key === 'F5') {
    e.preventDefault()
    void runFromEditor()
  }
}
function onEditorSelect() {
  const el = editorEl.value
  if (!el) {
    selectionLen.value = 0
    return
  }
  selectionLen.value = Math.max(0, (el.selectionEnd ?? 0) - (el.selectionStart ?? 0))
}
/** 结果分页只重发该结果对应的单条语句（FR-19） */
async function goResultPage(index: number, p: number) {
  const tab = activeTab.value
  if (!tab) return
  const set = tab.results[index]
  if (!set) return
  const next = clampPage(p, set.pageSize, set.result?.total)
  if (isDemo.value) {
    patchResult(tab.key, index, { page: next })
    return
  }
  const seq = tab.runSeq
  patchResult(tab.key, index, { running: true })
  const state = await sendQuery(tab.connectionId, set.sql, next, set.pageSize)
  if (!tabExists(tab.key, seq)) return
  if (state.kind === 'result' && state.result.status === 'ok') {
    patchResult(tab.key, index, {
      status: 'ok', running: false, page: next,
      pageSize: clampPageSize(state.result.pageSize ?? set.pageSize),
      result: { ...state.result, page: next },
    })
    selectedRowIndex.value = null
  } else {
    applyReplyToSet(tab.key, index, state)
  }
}
function changeResultPageSize(index: number, e: Event) {
  const tab = activeTab.value
  if (!tab) return
  const size = clampPageSize(Number((e.target as HTMLSelectElement).value))
  const set = tab.results[index]
  const hasResult = !!set?.result
  patchResult(tab.key, index, { pageSize: size })
  if (!isDemo.value && hasResult) void goResultPage(index, 1)
}
function rerun(h: DbQueryHistory) {
  let tab = activeTab.value
  if (!tab || tab.connectionId !== h.connectionId) {
    const candidate = tabs.value.find(t => t.connectionId === h.connectionId && !t.table)
    tab = candidate ?? null
    if (!tab) {
      const r = openManualTab(tabs.value, h.connectionId)
      if (r.full) {
        banner.value = { kind: 'warn', text: `已打开 ${MAX_TABS} 个查询标签，请先关闭一些` }
        return
      }
      tabs.value = r.tabs
      tab = findTab(tabs.value, r.key)
    }
    if (tab) activateTab(tab.key)
  }
  if (tab) patchTab(tab.key, { sql: h.sqlRedacted })
}

/* ---------------- 结果工具：复制 / 导出 / 单元格详情（FR-21~23） ---------------- */
async function copyText(text: string, okHint = '已复制') {
  try {
    await navigator.clipboard.writeText(text)
    banner.value = { kind: 'warn', text: okHint }
  } catch {
    banner.value = { kind: 'error', text: '复制失败：浏览器未授权剪贴板' }
  }
}
function copySelectedRow() {
  if (selectedRowIndex.value === null) return
  const r = rows.value[selectedRowIndex.value]
  if (!r) return
  void copyText(rowText(r), '已复制整行（制表符分隔）')
}
function exportCsvNow() {
  const set = activeSet.value
  if (!set?.result?.columns?.length) return
  const tab = activeTab.value
  const base = tab?.table ?? (tab ? connName(tab.connectionId) : 'query')
  const text = csvWithBom(toCsv(set.result.columns, set.result.rows ?? []))
  const name = csvFileName(base || 'query', (tab?.activeResultIndex ?? 0) + 1, set.page)
  const blob = new Blob([text], { type: 'text/csv;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = name
  a.rel = 'noopener'
  document.body.appendChild(a)
  a.click()
  a.remove()
  setTimeout(() => URL.revokeObjectURL(url), 0)
  banner.value = { kind: 'warn', text: `已导出 ${name}` }
}
function openCellDetail(col: string, v: unknown) {
  cellDetail.value = { col, value: isNull(v) ? 'NULL' : (typeof v === 'object' ? JSON.stringify(v, null, 2) : String(v)) }
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
/** 审批结果回填到发起标签，并续跑（批准）或标未执行（拒绝）（FR-18） */
async function resolveApproval(a: DbApproval, approveIt: boolean, confirmDestructiveFlag?: boolean) {
  overlay.value = null
  const ctx = approvalCtx.value
  if (isDemo.value) {
    approvals.value = approvals.value.map(x => (x.id === a.id ? { ...x, status: approveIt ? 'approved' : 'rejected' } : x))
    if (ctx) {
      const tab = findTab(tabs.value, ctx.tabKey)
      const batch = tab?.batch
      const set = tab?.results[ctx.resultIndex]
      if (approveIt) {
        const reply = fixtureReplies.value.shift() ?? props.fixture?.reply ?? null
        if (reply && set) applyReplyToSet(ctx.tabKey, ctx.resultIndex, replyToState(reply.httpStatus, reply.body))
        else patchResult(ctx.tabKey, ctx.resultIndex, { status: 'ok', running: false })
        patchTab(ctx.tabKey, { running: false, batch: undefined })
        if (batch && batch.statements.length > batch.nextIndex) {
          patchTab(ctx.tabKey, { running: true })
          void runFrom(ctx.tabKey, batch.statements, batch.nextIndex, set?.pageSize ?? DEFAULT_PAGE_SIZE)
        }
      } else {
        patchResult(ctx.tabKey, ctx.resultIndex, {
          status: 'error', running: false,
          error: { code: 'approval_rejected', message: '审批已拒绝：语句未执行' },
        })
        markSkipped(ctx.tabKey, ctx.resultIndex + 1, ctx.resultIndex)
        patchTab(ctx.tabKey, { running: false, batch: undefined })
      }
    }
    approvalCtx.value = null
    activeApprovalId.value = null
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
    if (ctx) {
      if (approveIt) {
        const applied = applyReplyToSet(ctx.tabKey, ctx.resultIndex, { kind: 'result', result: { ...r, status: r.status === 'awaiting_approval' ? 'ok' : r.status } })
        patchTab(ctx.tabKey, { running: false })
        const tab = findTab(tabs.value, ctx.tabKey)
        const batch = tab?.batch
        patchTab(ctx.tabKey, { batch: undefined })
        if (applied.action === 'show_result' && batch && batch.statements.length > batch.nextIndex) {
          patchTab(ctx.tabKey, { running: true })
          void runFrom(ctx.tabKey, batch.statements, batch.nextIndex, tab?.results[ctx.resultIndex]?.pageSize ?? DEFAULT_PAGE_SIZE)
        }
      } else {
        patchResult(ctx.tabKey, ctx.resultIndex, {
          status: 'error', running: false,
          error: { code: 'approval_rejected', message: '审批已拒绝：语句未执行' },
        })
        markSkipped(ctx.tabKey, ctx.resultIndex + 1, ctx.resultIndex)
        patchTab(ctx.tabKey, { running: false, batch: undefined })
      }
    } else if (approveIt) {
      // 外部（Agent）审批：结果落到该连接的活动/新标签
      const tab = tabForConnection(a.connectionId)
      if (tab) {
        const sets = buildResultSets([a.sqlRedacted], DEFAULT_PAGE_SIZE)
        sets[0] = resultFromReply({ httpStatus: 200, body: { ...r, status: r.status === 'awaiting_approval' ? 'ok' : r.status } }, a.sqlRedacted)
        tabs.value = replaceTab(tabs.value, tab.key, { results: [...tab.results, ...sets], activeResultIndex: tab.results.length })
        activateTab(tab.key)
      }
    }
    banner.value = approveIt ? null : { kind: 'warn', text: '已拒绝：语句未执行' }
  } catch (e) {
    // 409 confirmation_required / 404 not_found（已过期）/ 400 invalid_request（已处理）→ 红条
    if (e instanceof ApiError && e.status !== 401) {
      const st = replyToState(e.status, (e.body as unknown) ?? { code: e.code, message: e.message })
      if (ctx) applyReplyToSet(ctx.tabKey, ctx.resultIndex, st)
      else banner.value = { kind: 'error', text: `审批失败：${redactText(e instanceof Error ? e.message : String(e))}` }
    } else {
      handleError(e, '审批')
    }
  } finally {
    approvalCtx.value = null
    activeApprovalId.value = null
    if (approveIt) void loadTables(a.connectionId, true) // R1：批准执行后 schema 树即时可见
    await Promise.all([refreshApprovals(), refreshHistory(), refreshAgentEchoes()])
  }
}
/** 找该连接的活动标签，没有则新建自由标签 */
function tabForConnection(connId: string): QueryTab | null {
  const cur = activeTab.value
  if (cur && cur.connectionId === connId) return cur
  const existing = tabs.value.find(t => t.connectionId === connId)
  if (existing) return existing
  const r = openManualTab(tabs.value, connId)
  if (r.full) return null
  tabs.value = r.tabs
  activeId.value = connId
  return findTab(tabs.value, r.key)
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
  Object.assign(connForm, emptyForm(providers.value[0]?.id ?? 'sqlite'))
  editingId.value = null
  connFormError.value = ''
  testResult.value = null
  openOverlay('connForm')
}
function openEditConnection(c: DbConnection) {
  Object.assign(connForm, formFromConnection(c, defaultTables.value[c.id] ?? ''))
  editingId.value = c.id
  connFormError.value = ''
  testResult.value = null
  openOverlay('connForm')
}
function onProviderChange() {
  connForm.port = defaultPort(connForm.provider, providers.value)
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
    // 恒 200：{ok, code, message, serverVersion}；id 仅用户连接（数字）才带
    const r = await dbAdminApi.testConnection(toSubmitBody(connForm, !!editingId.value), editingId.value)
    testResult.value = { ...r, message: redactText(r.message), driverPackage: r.driverPackage ?? formProvider.value?.driverPackage }
  } catch (e) {
    // 兼容：非 200 时按旧行为降级
    const code = e instanceof ApiError ? e.code : undefined
    const msg = redactText(e instanceof Error ? e.message : String(e))
    testResult.value = code === 'driver_missing'
      ? { status: 'driver_missing', ok: false, code, message: msg, driverPackage: formProvider.value?.driverPackage }
      : { status: 'error', ok: false, code, message: msg }
  } finally {
    testing.value = false
  }
}
/** 默认表只落本机偏好（localStorage），不进请求体（AC-12） */
function rememberDefaultTable(id: string) {
  const t = (connForm.defaultTable ?? '').trim()
  const next = { ...defaultTables.value }
  if (t) next[id] = t
  else delete next[id]
  defaultTables.value = next
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
    rememberDefaultTable(saved.id || (editingId.value ?? ''))
    connForm.password = ''
    connForm.connStr = ''
    overlay.value = null
    connections.value = await dbAdminApi.connections()
    if (saved.id) {
      activeId.value = saved.id
      lastConnectionId.value = saved.id
    }
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
    removeConnectionTabs(id)
    const { [id]: _drop, ...rest } = defaultTables.value
    void _drop
    defaultTables.value = rest
    if (activeId.value === id) activeId.value = null
  } catch (e) {
    handleError(e, '删除连接')
  }
}

function onWindowKey(e: KeyboardEvent) {
  if (e.key === 'Escape' && cellDetail.value) cellDetail.value = null
}

onMounted(() => {
  if (props.fixture) {
    applyFixture(props.fixture)
  } else {
    void loadAll()
    pollTimer = setInterval(() => {
      if (unauthorized.value) return
      void refreshApprovals(true)
      if (sideTab.value === 'agent') void refreshAgentEchoes()
    }, 5000)
  }
  window.addEventListener('keydown', onWindowKey)
})
onBeforeUnmount(() => {
  if (pollTimer) clearInterval(pollTimer)
  window.removeEventListener('keydown', onWindowKey)
})
watch(() => props.fixture, f => f && applyFixture(f), { deep: true })
// 切结果标签/切查询标签时清掉行选中
watch([activeTabKey, () => activeTab.value?.activeResultIndex], () => { selectedRowIndex.value = null })
</script>

<template>
  <div class="dba-root" data-testid="db-admin-root">
    <header class="head">
      <div class="title-block">
        <h1 class="view-title">数据库管理 <span class="ver" data-testid="version-badge">v{{ version }}</span></h1>
        <p class="sub">可审批的 SQL 工作台 · 多标签查询</p>
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
          <input
            v-model="tableFilter"
            class="table-filter"
            data-testid="table-filter"
            placeholder="筛选表名（本地过滤）"
            aria-label="筛选表名"
          >
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
              <div v-if="tablesLoading[c.id]" class="node node-loading" role="status" :data-testid="`tables-loading-${c.id}`">
                <span class="spinner" aria-hidden="true"></span>加载表…
              </div>
              <template v-else>
                <template v-for="t in tablesFor(c.id)" :key="t.name">
                  <div class="node table" :data-testid="`table-${c.id}-${t.name}`" @click="onTableRowClick(c.id, t)">
                    <span class="caret" @click.stop="onTableCaretClick(c.id, t)">{{ expanded.has(`t:${c.id}/${t.name}`) ? '▾' : '▸' }}</span>
                    <span class="name">{{ t.type === 'view' ? '视图 ' : '' }}{{ t.name }}</span>
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
              :title="defaultTables[c.id] ? `${connSubtitle(c)} · 默认表 ${defaultTables[c.id]}` : connSubtitle(c)"
              @click="selectConnection(c)"
            >
              <span class="caret">{{ expanded.has(`c:${c.id}`) ? '▾' : '▸' }}</span>
              <span class="name">{{ c.name }}</span>
              <span v-if="c.readonly" class="lock" title="只读连接">🔒</span>
              <span class="node-actions">
                <button class="icon" title="刷新表" :data-testid="`refresh-${c.id}`" @click.stop="refreshTables(c.id)">↻</button>
                <button class="icon" title="编辑连接" :data-testid="`edit-${c.id}`" @click.stop="openEditConnection(c)">✎</button>
                <button class="icon danger" title="删除连接（仅删配置，不删库文件）" :data-testid="`delete-${c.id}`" @click.stop="askDelete(c)">✕</button>
              </span>
            </div>
            <template v-if="expanded.has(`c:${c.id}`)">
              <div v-if="tablesLoading[c.id]" class="node node-loading" role="status" :data-testid="`tables-loading-${c.id}`">
                <span class="spinner" aria-hidden="true"></span>加载表…
              </div>
              <div v-else-if="(tables[c.id] ?? []).length === 0" class="node column muted">（无表）</div>
              <div v-else-if="tablesFor(c.id).length === 0" class="node column muted" data-testid="table-filter-empty">无匹配表</div>
              <template v-else>
                <template v-for="t in tablesFor(c.id)" :key="t.name">
                  <div class="node table" :data-testid="`table-${c.id}-${t.name}`" @click="onTableRowClick(c.id, t)">
                    <span class="caret" @click.stop="onTableCaretClick(c.id, t)">{{ expanded.has(`t:${c.id}/${t.name}`) ? '▾' : '▸' }}</span>
                    <span class="name">{{ t.type === 'view' ? '视图 ' : '' }}{{ t.name }}</span>
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
        </template>
      </aside>

      <!-- 右区：查询标签 + 编辑器 + 结果 -->
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

        <!-- 查询标签条（同表去重聚焦 / 关闭右邻优先 / 上限 12） -->
        <div v-if="tabs.length" class="query-tabs" data-testid="query-tabs">
          <div
            v-for="t in tabs"
            :key="t.key"
            class="qtab"
            :class="{ on: t.key === activeTabKey }"
            :data-testid="`qtab-${t.key}`"
            :title="tabTitle(t)"
            @click="activateTab(t.key)"
          >
            <span v-if="tabDot(t)" class="qdot" :class="`d-${tabDot(t)}`" aria-hidden="true"></span>
            <span class="qname">{{ tabTitle(t) }}</span>
            <button class="qclose" data-testid="close-tab" :title="`关闭 ${tabTitle(t)}`" @click.stop="closeTabAction(t.key)">✕</button>
          </div>
          <button class="qnew" data-testid="new-query-tab" :disabled="!activeId" title="新建空白查询标签" @click="newQueryTab">＋</button>
        </div>

        <div v-if="activeTab" class="workspace">
          <textarea
            ref="editorEl"
            v-model="sql"
            class="editor"
            data-testid="sql-editor"
            spellcheck="false"
            placeholder="输入 SQL（可多语句，用 ; 分隔）；Ctrl+Enter 执行；选中一段只执行选中"
            @keydown="onEditorKey"
            @select="onEditorSelect"
            @click="onEditorSelect"
            @keyup="onEditorSelect"
            @input="onEditorSelect"
          />

          <div class="run-bar">
            <button
              class="btn primary"
              data-testid="run"
              :disabled="!!runDisabledReason"
              :title="runDisabledReason || 'Ctrl+Enter / F5'"
              @click="runFromEditor"
            >{{ runLabel }}</button>
            <span v-if="selectionLen > 0" class="muted" data-testid="selection-hint">仅执行选中 {{ selectionLen }} 字符</span>
            <span v-else-if="runDisabledReason && !running" class="muted">{{ runDisabledReason }}</span>
            <span class="spacer" />
            <button class="btn" data-testid="new-query-inline" @click="newQueryTab">+ 新查询</button>
          </div>

          <!-- 多语句结果标签：单语句不渲染标签条 -->
          <div v-if="activeTab.results.length > 1" class="result-tabs" data-testid="result-tabs">
            <button
              v-for="(s, i) in activeTab.results"
              :key="i"
              class="rtab"
              :class="{ on: i === activeTab.activeResultIndex }"
              :data-testid="`result-tab-${i}`"
              :title="resultDotTitle(s, i)"
              @click="selectResult(i)"
            >
              <span class="rtab-dot" :class="`d-${resultDot(s)}`" aria-hidden="true"></span>{{ resultLabel(i) }}
            </button>
          </div>

          <div v-if="resultError" class="result-error" data-testid="result-error">
            <strong>{{ errorReason(resultError.code) }}</strong>
            <span class="code" data-testid="result-error-code">[{{ resultError.code }}<template v-if="resultError.httpStatus"> · HTTP {{ resultError.httpStatus }}</template>]</span>
            <div v-if="resultError.message" class="err-msg">{{ resultError.message }}</div>
            <div class="muted">{{ resultError.code === 'confirmation_required' ? '审批仍待处理：语句未执行，请在审批里二次确认。' : '未弹出审批：该语句没有被执行。' }}</div>
          </div>
          <div v-if="activeSet?.status === 'skipped'" class="result-skip" data-testid="result-skip">
            <strong>{{ activeSet.skipReason ?? '未执行' }}</strong>
            <span class="muted">批量执行已停止：前面的语句失败后，本语句不会再执行。</span>
          </div>

          <div class="result-tools" data-testid="result-tools">
            <span class="stats" data-testid="result-stats">{{ statsLabel(activeSet) }}</span>
            <span v-if="result?.columns?.length" class="muted" data-testid="result-range">{{ rangeLabel(page, pageSize, rows.length, result.total) }}</span>
            <span class="spacer" />
            <button class="btn" data-testid="copy-row" :disabled="selectedRowIndex === null" @click="copySelectedRow">复制整行</button>
            <button class="btn" data-testid="export-csv" :disabled="!result?.columns?.length" @click="exportCsvNow">导出 CSV</button>
          </div>

          <div v-if="!resultError && activeSet?.status !== 'skipped'" class="grid-wrap" data-testid="result-grid">
            <div v-if="result?.truncated" class="truncated" data-testid="truncated">结果已截断（truncated=true）：仅显示上限内的行</div>
            <table v-if="result?.columns?.length" class="dba-grid">
              <thead>
                <tr>
                  <th class="rownum">#</th>
                  <th v-for="c in result.columns" :key="c.name">{{ c.name }}<span v-if="c.type" class="type">{{ c.type }}</span></th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="(r, i) in rows"
                  :key="i"
                  :class="{ sel: i === selectedRowIndex }"
                  :data-testid="`row-${i}`"
                  @click="selectedRowIndex = i"
                >
                  <td class="rownum">{{ (page - 1) * pageSize + i + 1 }}</td>
                  <td
                    v-for="(v, j) in r"
                    :key="j"
                    :class="{ null: isNull(v) }"
                    :title="cell(v)"
                    :data-testid="`cell-${i}-${j}`"
                    @dblclick="openCellDetail(result.columns![j]?.name ?? String(j), v)"
                  >{{ cell(v) }}</td>
                </tr>
              </tbody>
            </table>
            <div v-else-if="result && result.affectedRows !== undefined" class="placeholder">执行成功 · 影响 {{ result.affectedRows }} 行</div>
            <div v-else class="placeholder muted">结果将显示在这里</div>
          </div>

          <div v-if="result?.columns?.length" class="pager" data-testid="pager">
            <button class="btn" data-testid="page-prev" :disabled="!canPrev(page)" @click="goResultPage(activeTab.activeResultIndex, page - 1)">‹</button>
            <span data-testid="page-label">{{ page }} / {{ totalPages ?? '?' }}</span>
            <button class="btn" data-testid="page-next" :disabled="!canNext(page, pageSize, result.total, rows.length)" @click="goResultPage(activeTab.activeResultIndex, page + 1)">›</button>
            <label class="muted">每页
              <select :value="pageSize" data-testid="page-size" @change="changeResultPageSize(activeTab.activeResultIndex, $event)">
                <option v-for="s in PAGE_SIZES" :key="s" :value="s">{{ s }}</option>
              </select>
            </label>
          </div>

          <section class="bottom">
            <div class="tabs">
              <button class="tab" :class="{ on: sideTab === 'history' }" data-testid="tab-history" @click="sideTab = 'history'">历史</button>
              <button class="tab" :class="{ on: sideTab === 'agent' }" data-testid="tab-agent" @click="openAgentTab">Agent 工具回显</button>
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
              <div class="muted pad src-note">来源：审计 kind=agent（GET /audit?kind=agent）</div>
              <div v-for="a in agentEchoes" :key="a.id" class="row echo" :data-status="a.status">
                <span class="time">{{ fmtTime(a.at) }}</span>
                <code class="tool">{{ a.tool }}</code>
                <code class="sqltext" :title="redactText(a.sql ?? '')">{{ redactText(a.sql ?? '') }}</code>
                <span class="pill" :class="`st-${a.status}`">{{ a.status }}</span>
                <span v-if="a.code" class="code">[{{ a.code }}]</span>
                <span v-if="a.rowCount !== undefined" class="muted">{{ a.rowCount }} 行</span>
                <span v-if="a.truncated" class="pill st-truncated">truncated=true</span>
                <span v-if="a.approvalId" class="muted">审批 {{ a.approvalId }}</span>
                <span v-if="a.message" class="muted">{{ redactText(a.message) }}</span>
              </div>
            </div>
          </section>
        </div>

        <div v-else class="workspace-empty" data-testid="workspace-empty">
          <p class="empty-title">还没有打开的查询</p>
          <p class="muted">在左侧点一张表打开查询标签，或选择连接后新建空白查询。</p>
          <p class="muted">同一张表重复点击只会切回已打开的标签，草稿与结果都不会丢。</p>
          <button class="btn primary" data-testid="empty-new-query" :disabled="!activeId" @click="newQueryTab">+ 新建查询</button>
        </div>
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
        <label>默认表（可选，仅本机）
          <input v-model="connForm.defaultTable" data-testid="cf-default-table" placeholder="如 users；留空则点连接只展开加载全部表" />
        </label>
        <p class="muted">填写默认表：点该连接就直接打开这张表的查询标签；留空：点连接只展开并加载全部表。默认表只保存在本机，不写入连接记录。</p>

        <div v-if="testResult" class="test-result" :class="`tr-${testResult.status}`" data-testid="test-result">
          <template v-if="testResult.status === 'ok'">✔ 连接成功{{ testResult.serverVersion ? ` · 服务器 ${testResult.serverVersion}` : '' }}</template>
          <template v-else-if="testResult.status === 'driver_missing'">⚠ 驱动缺失：需安装 <code>{{ testResult.driverPackage ?? formProvider?.driverPackage ?? 'NuGet 驱动包' }}</code>{{ testResult.message ? ` · ${redactText(testResult.message)}` : '' }}</template>
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
          <dt>类型</dt><dd>{{ kindLabel(currentApproval.kind, currentApproval.destructive) }}<span v-if="currentApproval.destructive" class="tag tag-danger" data-testid="approval-destructive">不可恢复</span></dd>
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
        <p class="muted">该连接下已打开的查询标签会一并关闭。</p>
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

    <!-- 浮层：单元格详情（双击单元格打开，Esc/遮罩关闭） -->
    <div v-if="cellDetail" class="mask" data-testid="cell-detail" @click.self="cellDetail = null">
      <div class="modal">
        <h3>单元格 · {{ cellDetail.col }}</h3>
        <pre class="sql-preview cell-value" data-testid="cell-detail-value">{{ cellDetail.value }}</pre>
        <div class="modal-actions">
          <span class="spacer" />
          <button class="btn" data-testid="cell-detail-copy" @click="copyText(cellDetail.value)">复制</button>
          <button class="btn primary" data-testid="cell-detail-close" @click="cellDetail = null">关闭</button>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.dba-root {
  position: relative;
  height: 100%;
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding: 12px;
  box-sizing: border-box;
  color: var(--el-text-color-primary);
  font-size: 13px;
  overflow: hidden;
}
.dba-root > * { flex-shrink: 0; }
.dba-root > .body { flex: 1 1 auto; min-height: 0; }
.head { display: flex; align-items: flex-end; gap: 12px; }
.title-block { flex: 1; min-width: 0; }
.view-title { margin: 0; font-size: 18px; font-weight: 600; }
.ver { font-size: 12px; color: var(--el-text-color-secondary); font-weight: 500; }
.sub { margin: 2px 0 0; color: var(--el-text-color-secondary); font-size: 12px; }
.head-actions { display: flex; gap: 8px; align-items: center; }
.banner { border-radius: 8px; padding: 8px 12px; border: 1px solid; }
.banner-error { color: var(--el-color-danger); border-color: var(--el-color-danger); background: var(--el-color-danger-light-9); }
.banner-warn { color: var(--el-color-warning); border-color: var(--el-color-warning); background: var(--el-color-warning-light-9); }
.banner-info { color: var(--el-color-primary); border-color: var(--el-color-primary); background: var(--el-color-primary-light-9); }
.body { display: grid; grid-template-columns: 260px 1fr; gap: 10px; }
.side {
  border: 1px solid var(--el-border-color); border-radius: 12px; background: var(--el-bg-color-overlay);
  padding: 10px; overflow: auto; display: flex; flex-direction: column; gap: 2px; min-height: 0;
}
.side > * { flex-shrink: 0; }
.table-filter {
  margin: 8px 0 2px; width: 100%; box-sizing: border-box;
  background: var(--el-bg-color); color: var(--el-text-color-primary);
  border: 1px solid var(--el-border-color); border-radius: 8px; padding: 4px 8px; font-size: 12px;
}
.group-title { margin: 10px 0 4px; color: var(--el-text-color-secondary); font-size: 12px; font-weight: 600; }
.node { display: flex; align-items: center; gap: 6px; padding: 3px 6px; border-radius: 6px; cursor: pointer; white-space: nowrap; }
.node:hover { background: var(--el-fill-color-light); }
.node.on { background: var(--el-color-primary-light-8); }
.node .name { overflow: hidden; text-overflow: ellipsis; }
.node.table { padding-left: 20px; }
.node.column { padding-left: 38px; cursor: default; color: var(--el-text-color-secondary); font-size: 12px; }
.node-loading { padding-left: 20px; color: var(--el-text-color-secondary); font-size: 12px; cursor: default; }
.node-loading .spinner {
  width: 10px; height: 10px; flex-shrink: 0; border-radius: 50%;
  border: 2px solid var(--el-border-color); border-top-color: var(--el-color-primary);
  animation: dba-spin 0.8s linear infinite;
}
@keyframes dba-spin { to { transform: rotate(360deg); } }
@media (prefers-reduced-motion: reduce) { .node-loading .spinner { animation: none; } }
.caret { width: 10px; color: var(--el-text-color-secondary); }
.lock { font-size: 11px; }
.node-actions { margin-left: auto; display: none; gap: 2px; }
.node:hover .node-actions, .node.on .node-actions { display: inline-flex; }
.type { margin-left: 4px; color: var(--el-text-color-secondary); font-size: 11px; font-family: ui-monospace, monospace; }
.pk { color: var(--el-color-warning); font-size: 10px; border: 1px solid var(--el-color-warning); border-radius: 4px; padding: 0 3px; }
.nn { color: var(--el-text-color-secondary); font-size: 10px; }
.empty-guide { border: 1px dashed var(--el-border-color); border-radius: 10px; padding: 10px; display: grid; gap: 6px; }
.empty-guide p { margin: 0; }
.skeleton { display: grid; gap: 8px; padding: 8px 0; }
.sk-line { height: 12px; border-radius: 6px; background: var(--el-fill-color); }
.main { display: flex; flex-direction: column; gap: 8px; min-width: 0; min-height: 0; overflow: hidden; }
.main > * { flex-shrink: 0; }
.conn-bar { display: flex; align-items: center; gap: 8px; min-width: 0; }
.conn-name { white-space: nowrap; }
.ellipsis { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; min-width: 0; }
.tag { border: 1px solid var(--el-border-color); border-radius: 999px; padding: 0 8px; font-size: 11px; white-space: nowrap; }
.tag-ro { color: var(--el-color-warning); border-color: var(--el-color-warning); }
.tag-untested { color: var(--el-text-color-secondary); border-style: dashed; }
.tag-muted { color: var(--el-text-color-secondary); }
.tag-agent { color: var(--el-color-primary); border-color: var(--el-color-primary); }
.tag-danger { color: var(--el-color-danger); border-color: var(--el-color-danger); margin-left: 6px; }
/* 查询标签条 */
.query-tabs { display: flex; align-items: stretch; gap: 2px; overflow-x: auto; border-bottom: 1px solid var(--el-border-color); padding-bottom: 0; }
.query-tabs::-webkit-scrollbar { height: 4px; }
.qtab {
  display: inline-flex; align-items: center; gap: 6px; padding: 4px 8px 4px 10px; cursor: pointer;
  border: 1px solid transparent; border-bottom: 2px solid transparent; border-radius: 8px 8px 0 0;
  color: var(--el-text-color-secondary); background: transparent; white-space: nowrap; max-width: 190px;
}
.qtab:hover { background: var(--el-fill-color-light); }
.qtab.on { color: var(--el-text-color-primary); background: var(--el-bg-color-overlay); border-bottom-color: var(--el-color-primary); }
.qtab .qname { overflow: hidden; text-overflow: ellipsis; max-width: 130px; }
.qtab .qclose {
  border: none; background: transparent; color: var(--el-text-color-secondary); cursor: pointer;
  padding: 0 2px; font-size: 12px; line-height: 1; border-radius: 4px;
}
.qtab .qclose:hover { color: var(--el-color-danger); background: var(--el-fill-color); }
.qdot { width: 6px; height: 6px; border-radius: 50%; flex-shrink: 0; }
.qnew {
  border: 1px dashed var(--el-border-color); background: transparent; color: var(--el-text-color-secondary);
  border-radius: 8px; padding: 2px 8px; cursor: pointer; font-size: 13px; margin-left: 4px;
}
.qnew:disabled { opacity: 0.5; cursor: not-allowed; }
.workspace { display: flex; flex-direction: column; gap: 8px; min-height: 0; flex: 1 1 auto; }
.workspace > * { flex-shrink: 0; }
.editor {
  height: 96px; resize: vertical; background: var(--el-bg-color); color: var(--el-text-color-primary);
  border: 1px solid var(--el-border-color); border-radius: 10px; padding: 8px 10px;
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 13px; box-sizing: border-box;
}
.run-bar { display: flex; align-items: center; gap: 8px; }
.spacer { flex: 1; }
.stats { color: var(--el-text-color-secondary); font-size: 12px; }
/* 结果标签条（多语句，仅 >1 条时出现） */
.result-tabs { display: flex; gap: 2px; overflow-x: auto; }
.rtab {
  display: inline-flex; align-items: center; gap: 6px; padding: 3px 10px; cursor: pointer;
  border: 1px solid var(--el-border-color); border-radius: 999px; background: transparent;
  color: var(--el-text-color-secondary); font-size: 12px; white-space: nowrap;
}
.rtab.on { color: var(--el-text-color-primary); border-color: var(--el-color-primary); background: var(--el-color-primary-light-9); }
.rtab-dot { width: 6px; height: 6px; border-radius: 50%; flex-shrink: 0; }
.d-ok { background: var(--el-color-success); }
.d-error { background: var(--el-color-danger); }
.d-awaiting { background: var(--el-color-warning); }
.d-skipped { background: var(--el-text-color-secondary); }
.d-pending { background: var(--el-border-color-dark); }
.result-tools { display: flex; align-items: center; gap: 8px; }
.result-error { border: 1px solid var(--el-color-danger); background: var(--el-color-danger-light-9); color: var(--el-color-danger); border-radius: 10px; padding: 8px 12px; display: grid; gap: 2px; }
.result-skip {
  border: 1px solid var(--el-border-color); background: var(--el-fill-color-light);
  color: var(--el-text-color-primary); border-radius: 10px; padding: 8px 12px; display: flex; align-items: center; gap: 8px;
}
.result-error .code { font-family: ui-monospace, monospace; font-size: 11px; }
.err-msg { color: var(--el-text-color-primary); font-family: ui-monospace, monospace; font-size: 12px; white-space: pre-wrap; }
.main > .grid-wrap, .workspace > .grid-wrap { flex: 1 1 auto; min-height: 80px; overflow: auto; border: 1px solid var(--el-border-color); border-radius: 10px; background: var(--el-bg-color-overlay); }
.truncated { padding: 6px 10px; color: var(--el-color-warning); border-bottom: 1px solid var(--el-border-color); font-size: 12px; }
/* 类名带 dba- 前缀：避免与宿主 Tailwind 工具类（.grid → display:grid）冲突，导致 table 脱离表格布局、表头与内容错列 */
.dba-grid { border-collapse: collapse; width: max-content; min-width: 100%; font-family: ui-monospace, monospace; font-size: 12px; }
.dba-grid th, .dba-grid td { border-bottom: 1px solid var(--el-border-color); padding: 4px 10px; text-align: left; max-width: 280px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.dba-grid th { position: sticky; top: 0; background: var(--el-bg-color-overlay); font-weight: 600; }
.dba-grid td.null { color: var(--el-text-color-secondary); font-style: italic; }
.dba-grid tbody tr { cursor: default; }
.dba-grid tbody tr:hover { background: var(--el-fill-color-light); }
.dba-grid tbody tr.sel { background: var(--el-color-primary-light-9); }
.rownum { color: var(--el-text-color-secondary); width: 36px; }
.placeholder { padding: 16px; }
.workspace-empty {
  flex: 1 1 auto; border: 1px dashed var(--el-border-color); border-radius: 12px;
  padding: 24px; display: flex; flex-direction: column; align-items: flex-start; justify-content: center; gap: 8px;
}
.workspace-empty p { margin: 0; }
.empty-title { font-size: 15px; font-weight: 600; }
.pager { display: flex; align-items: center; gap: 8px; }
.pager select { margin-left: 4px; background: var(--el-bg-color); color: var(--el-text-color-primary); border: 1px solid var(--el-border-color); border-radius: 6px; }
.bottom { height: 132px; border: 1px solid var(--el-border-color); border-radius: 10px; background: var(--el-bg-color-overlay); display: flex; flex-direction: column; overflow: hidden; }
.tabs { display: flex; gap: 2px; border-bottom: 1px solid var(--el-border-color); padding: 4px 6px 0; }
.tab { border: none; background: transparent; color: var(--el-text-color-secondary); padding: 4px 10px; cursor: pointer; border-bottom: 2px solid transparent; }
.tab.on { color: var(--el-text-color-primary); border-bottom-color: var(--el-color-primary); }
.list { overflow: auto; flex: 1; }
.row { display: flex; align-items: center; gap: 8px; padding: 3px 10px; white-space: nowrap; }
.row.fail .sqltext { color: var(--el-color-danger); }
.time { color: var(--el-text-color-secondary); font-family: ui-monospace, monospace; font-size: 11px; }
.sqltext { overflow: hidden; text-overflow: ellipsis; min-width: 0; flex: 1; font-size: 12px; }
.tool { color: var(--el-color-primary); font-size: 12px; }
.pill { border: 1px solid var(--el-border-color); border-radius: 999px; padding: 0 6px; font-size: 11px; }
.st-ok { color: var(--el-color-success); border-color: var(--el-color-success); }
.st-awaiting_approval, .st-truncated { color: var(--el-color-warning); border-color: var(--el-color-warning); }
.st-rejected, .k-rejected { color: var(--el-color-danger); border-color: var(--el-color-danger); }
.k-approval { color: var(--el-color-warning); border-color: var(--el-color-warning); }
.k-query { color: var(--el-color-primary); border-color: var(--el-color-primary); }
.muted { color: var(--el-text-color-secondary); font-size: 12px; }
.pad { padding: 6px 10px; }
.btn {
  border: 1px solid var(--el-border-color); background: transparent; color: var(--el-text-color-primary);
  border-radius: 8px; padding: 5px 12px; cursor: pointer; font-size: 13px; white-space: nowrap;
}
.btn:disabled { opacity: 0.5; cursor: not-allowed; }
.btn.primary { background: var(--el-color-primary); border-color: var(--el-color-primary); color: var(--el-color-white); }
.btn.block { width: 100%; }
.btn.warn { color: var(--el-color-warning); border-color: var(--el-color-warning); }
.btn.danger { background: var(--el-color-danger); border-color: var(--el-color-danger); color: var(--el-color-white); }
.btn.danger-outline { color: var(--el-color-danger); border-color: var(--el-color-danger); }
.icon { border: none; background: transparent; color: var(--el-text-color-secondary); cursor: pointer; padding: 0 4px; }
.icon.danger:hover { color: var(--el-color-danger); }
.badge { border-radius: 999px; padding: 4px 10px; font-size: 12px; border: 1px solid var(--el-border-color); background: transparent; cursor: pointer; }
.badge-ask { color: var(--el-color-primary); border-color: var(--el-color-primary); }
.badge-auto { color: var(--el-color-success); border-color: var(--el-color-success); }
.mask { position: absolute; inset: 0; background: var(--el-mask-color); display: flex; align-items: center; justify-content: center; z-index: 10; }
.modal {
  width: min(520px, 92%); max-height: 90%; overflow: auto; background: var(--el-bg-color-overlay); border: 1px solid var(--el-border-color);
  border-radius: 14px; padding: 16px; display: grid; gap: 10px; box-sizing: border-box;
}
.modal h3 { margin: 0; font-size: 16px; display: flex; align-items: center; gap: 8px; }
.modal p { margin: 0; }
.modal label { display: grid; gap: 4px; font-size: 12px; color: var(--el-text-color-secondary); }
.modal input, .modal select {
  background: var(--el-bg-color); color: var(--el-text-color-primary); border: 1px solid var(--el-border-color); border-radius: 8px;
  padding: 6px 10px; font-size: 13px; box-sizing: border-box; width: 100%;
}
.modal-danger { border-color: var(--el-color-danger); }
.modal-actions .btn.warn, .modal .btn.warn { color: var(--el-color-warning); border-color: var(--el-color-warning); }
.row-inline { display: flex; gap: 8px; align-items: center; }
.row-inline select { flex: 1; }
.grow { flex: 1; }
.port { width: 110px; }
.mode-switch { display: flex; gap: 6px; }
.chip { border: 1px solid var(--el-border-color); background: transparent; color: var(--el-text-color-secondary); border-radius: 999px; padding: 3px 12px; cursor: pointer; }
.chip.on { border-color: var(--el-color-primary); color: var(--el-color-primary); }
.test-result { border-radius: 8px; padding: 8px 10px; border: 1px solid; }
.tr-ok { color: var(--el-color-success); border-color: var(--el-color-success); background: var(--el-color-success-light-9); }
.tr-error { color: var(--el-color-danger); border-color: var(--el-color-danger); background: var(--el-color-danger-light-9); }
.tr-driver_missing { color: var(--el-color-warning); border-color: var(--el-color-warning); background: var(--el-color-warning-light-9); }
.form-error { color: var(--el-color-danger); }
.modal-actions { display: flex; gap: 8px; align-items: center; }
.kv { display: grid; grid-template-columns: 72px 1fr; gap: 4px 8px; margin: 0; }
.kv dt { color: var(--el-text-color-secondary); }
.kv dd { margin: 0; }
.sql-preview { margin: 0; background: var(--el-bg-color); border: 1px solid var(--el-border-color); border-radius: 8px; padding: 8px 10px; white-space: pre-wrap; word-break: break-all; font-size: 12px; }
.cell-value { max-height: 50vh; overflow: auto; white-space: pre-wrap; word-break: break-word; }
.mask-drawer { justify-content: flex-end; align-items: stretch; }
.drawer { width: min(420px, 90%); background: var(--el-bg-color-overlay); border-left: 1px solid var(--el-border-color); display: flex; flex-direction: column; }
.drawer-head { display: flex; align-items: center; gap: 8px; padding: 12px; border-bottom: 1px solid var(--el-border-color); }
.drawer-head h3 { margin: 0; font-size: 15px; }
.drawer-head select { background: var(--el-bg-color); color: var(--el-text-color-primary); border: 1px solid var(--el-border-color); border-radius: 6px; }
.audit-row { padding: 8px 12px; border-bottom: 1px solid var(--el-border-color); display: grid; gap: 4px; }
.audit-meta { display: flex; gap: 6px; align-items: center; }
</style>
