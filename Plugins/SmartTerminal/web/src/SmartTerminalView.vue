<script setup lang="ts">
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue'
import { fetchPluginVersion, smartTerminalApi } from './api'
import { parseLaunchQuery, scrubLaunchParamsFromUrl } from './launchFromQuery'
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
const unauthorized = ref(false)
const showAudit = ref(false)
const showApproval = ref(false)
const activeApproval = ref<PendingApproval | null>(null)
const launchBanner = ref('')
const launchWarning = ref('')
const renamingId = ref('')
const renameDraft = ref('')
const showNewTabMenu = ref(false)

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
  })
})

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
  unauthorized.value = !!f.unauthorized
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
}

function noteError(e: unknown) {
  const anyErr = e as { status?: number; message?: string }
  unauthorized.value = anyErr?.status === 401
  errorText.value = anyErr?.message ? `操作失败：${anyErr.message}` : '操作失败：未知错误'
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
  if (approvals.value.length && !showApproval.value) {
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

async function closeTab(id: string, ev?: Event) {
  ev?.stopPropagation()
  if (isDemo.value) {
    sessions.value = sessions.value.filter(s => s.sessionId !== id)
    if (activeId.value === id) {
      activeId.value = sessions.value[0]?.sessionId ?? ''
      chunks.value = activeId.value ? chunksBySession.value[activeId.value] ?? [] : []
      commands.value = activeId.value ? commandsBySession.value[activeId.value] ?? [] : []
    }
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
    if (!activeId.value && sessions.value.length) {
      await selectSession(sessions.value[0].sessionId)
    }
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

async function applyLaunch(request: LaunchTerminalRequest, summary: string, passwordInUrl: boolean) {
  launchBanner.value = `URL 启动：${summary}`
  if (passwordInUrl) {
    launchWarning.value = '地址栏曾含密码，已从 URL 擦除；密码仅短暂注入进程环境，不写审计原文。建议改用密钥。'
  }
  if (isDemo.value) return
  busy.value = 'launch'
  try {
    // 发送后立即丢弃本地明文
    const body = { ...request }
    const res = await smartTerminalApi.launch(body)
    body.password = undefined
    request.password = undefined
    if (res?.warning) launchWarning.value = res.warning
    await refreshMeta()
    if (res?.session?.sessionId) await selectSession(res.session.sessionId)
  } catch (e) {
    noteError(e)
  } finally {
    busy.value = ''
  }
}

async function consumeUrlLaunch() {
  if (isDemo.value || typeof location === 'undefined') return
  const parsed = parseLaunchQuery(location.search || '')
  scrubLaunchParamsFromUrl()
  if (!parsed) return
  await applyLaunch(parsed.request, parsed.summary, parsed.passwordInUrl)
}

onMounted(async () => {
  if (props.fixture) {
    applyFixture(props.fixture)
    return
  }
  try {
    await refreshMeta()
    await consumeUrlLaunch()
    if (activeId.value) await selectSession(activeId.value)
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
})
</script>

<template>
  <div class="st-root" data-testid="smart-terminal-root">
    <header class="st-header">
      <div>
        <h1 class="view-title">智能终端 <span class="ver">v{{ version }}</span></h1>
        <p class="sub">多标签 · URL 深链 · 三级权限 · JSONL 审计 · <code>st_*</code></p>
      </div>
      <div class="header-actions">
        <span class="badge" :class="modeBadgeClass" data-testid="mode-badge">权限：{{ modeLabel }}</span>
        <button class="btn ghost" type="button" data-testid="open-audit" @click="openAudit">审计</button>
      </div>
    </header>

    <div v-if="launchBanner" class="banner info" data-testid="launch-banner">{{ launchBanner }}</div>
    <div v-if="launchWarning" class="banner warn" data-testid="launch-warning">{{ launchWarning }}</div>
    <div v-if="unauthorized" class="banner error" data-testid="banner-unauthorized">未授权（401）。请先登录宿主，确保 localStorage 有 forge_api_token。</div>
    <div v-else-if="errorText" class="banner error" data-testid="banner-error">{{ errorText }}</div>

    <!-- Windows Terminal 风格标签栏 -->
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
          </span>
        </div>
        <button v-if="activeSession" class="btn ghost tiny" type="button" @click="closeTab(activeSession.sessionId)">关闭</button>
      </div>

      <div ref="consoleEl" class="console" aria-live="polite" data-testid="console">
        <div v-if="!activeSession" class="empty">点「+」新建标签，或通过 URL 深链启动（见文档 03-URL协议）。</div>
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

    <div v-if="showApproval && activeApproval" class="modal-mask" data-testid="approval-modal" @click.self="showApproval = false">
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

    <aside v-if="showAudit" class="audit-drawer" data-testid="audit-drawer">
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
  --bg: var(--el-bg-color, #0f1419);
  --panel: var(--el-bg-color-overlay, #161b22);
  --line: var(--el-border-color, #30363d);
  --text: var(--el-text-color-primary, #e6edf3);
  --muted: var(--el-text-color-secondary, #8b949e);
  --accent: var(--el-color-primary, #2f81f7);
  --ok: #3fb950;
  --warn: #d29922;
  --danger: #f85149;
  height: 100%;
  display: flex;
  flex-direction: column;
  gap: 10px;
  padding: 16px;
  color: var(--text);
  box-sizing: border-box;
  min-height: 0;
  position: relative;
}
.st-header { display: flex; justify-content: space-between; align-items: flex-start; gap: 12px; flex-shrink: 0; }
.view-title { margin: 0; font-size: 20px; }
.ver { font-size: 12px; color: var(--muted); font-weight: 500; }
.sub { margin: 4px 0 0; color: var(--muted); font-size: 13px; }
.header-actions { display: flex; gap: 8px; align-items: center; }

.tabbar {
  display: flex; align-items: stretch; gap: 4px; flex-shrink: 0;
  border-bottom: 1px solid var(--line); padding-bottom: 0; overflow-x: auto;
}
.tab {
  display: flex; align-items: center; gap: 8px;
  max-width: 220px; padding: 8px 10px;
  border: 1px solid var(--line); border-bottom: none;
  border-radius: 10px 10px 0 0; background: transparent; cursor: pointer;
  color: var(--muted); font-size: 12px;
}
.tab.active { color: var(--text); background: var(--panel); border-color: var(--accent); }
.tab-title { font-weight: 600; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.tab-meta { color: var(--muted); font-size: 11px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.tab-close {
  border: none; background: transparent; color: var(--muted); cursor: pointer;
  font-size: 14px; line-height: 1; padding: 0 2px;
}
.tab-close:hover { color: var(--danger); }
.tab-new {
  border: 1px dashed var(--line); background: transparent; color: var(--text);
  border-radius: 8px; width: 36px; cursor: pointer; margin-bottom: 4px;
}
.tab-rename {
  width: 120px; background: transparent; color: var(--text);
  border: 1px solid var(--accent); border-radius: 4px; padding: 2px 6px; font-size: 12px;
}

.new-tab-panel {
  display: grid; gap: 8px; padding: 12px; border: 1px solid var(--line);
  border-radius: 12px; background: var(--panel); flex-shrink: 0;
}
.new-tab-panel label { display: grid; gap: 4px; font-size: 12px; color: var(--muted); }
.new-tab-panel input, .new-tab-panel select, .cmdline input, .audit-filters input, .audit-filters select {
  width: 100%; box-sizing: border-box; background: transparent; color: var(--text);
  border: 1px solid var(--line); border-radius: 8px; padding: 8px 10px;
}
.new-tab-actions { display: flex; justify-content: flex-end; gap: 8px; }
.mode-row { display: flex; gap: 6px; flex-wrap: wrap; }
.chip {
  border: 1px solid var(--line); background: transparent; color: var(--text);
  border-radius: 999px; padding: 4px 10px; cursor: pointer; font-size: 12px;
}
.chip.on { border-color: var(--accent); color: var(--accent); }

.panel {
  background: var(--panel); border: 1px solid var(--line); border-radius: 12px;
  padding: 12px; display: flex; flex-direction: column; min-height: 0; flex: 1;
}
.panel-head { display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px; gap: 8px; }
.muted { color: var(--muted); font-size: 12px; }
.console {
  flex: 1; min-height: 220px; overflow: auto; background: #0b0f14;
  border: 1px solid var(--line); border-radius: 10px; padding: 10px;
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; font-size: 12.5px;
}
.line { display: grid; grid-template-columns: 64px 1fr; gap: 8px; margin-bottom: 4px; }
.line .tag { color: var(--muted); }
.line.stderr .txt { color: var(--danger); }
.line.system .txt { color: var(--warn); }
.cmdline { display: flex; gap: 8px; margin-top: 10px; }
.history { margin-top: 12px; max-height: 160px; overflow: auto; }
.history ul { list-style: none; margin: 0; padding: 0; display: grid; gap: 6px; }
.history li { display: flex; gap: 8px; align-items: center; flex-wrap: wrap; font-size: 12px; }
.pill { border: 1px solid var(--line); border-radius: 999px; padding: 1px 8px; }
.btn {
  border: 1px solid var(--line); background: transparent; color: var(--text);
  border-radius: 8px; padding: 8px 12px; cursor: pointer;
}
.btn.primary { background: var(--accent); border-color: var(--accent); color: #fff; }
.btn.ghost { background: transparent; }
.btn.tiny { padding: 4px 8px; font-size: 12px; }
.btn:disabled { opacity: 0.5; cursor: not-allowed; }
.badge { border-radius: 999px; padding: 4px 10px; font-size: 12px; border: 1px solid var(--line); }
.badge-auto { color: var(--ok); border-color: var(--ok); }
.badge-write { color: var(--accent); border-color: var(--accent); }
.badge-high { color: var(--warn); border-color: var(--warn); }
.banner { border-radius: 8px; padding: 8px 10px; font-size: 13px; flex-shrink: 0; }
.banner.error { background: color-mix(in srgb, var(--danger) 16%, transparent); color: var(--danger); }
.banner.warn { background: color-mix(in srgb, var(--warn) 16%, transparent); color: var(--warn); }
.banner.info { background: color-mix(in srgb, var(--accent) 14%, transparent); color: var(--accent); }
.empty { color: var(--muted); font-size: 13px; padding: 8px 0; }
.modal-mask {
  position: absolute; inset: 0; background: rgba(0,0,0,.45);
  display: grid; place-items: center; z-index: 40;
}
.modal {
  width: min(520px, 92%); background: var(--panel); border: 1px solid var(--line);
  border-radius: 14px; padding: 18px;
}
.modal-cmd { background: #0b0f14; border-radius: 8px; padding: 10px; margin: 10px 0; word-break: break-all; }
.modal-actions { display: flex; justify-content: flex-end; gap: 8px; margin-top: 14px; }
.audit-drawer {
  position: absolute; top: 0; right: 0; bottom: 0; width: min(420px, 92%);
  background: var(--panel); border-left: 1px solid var(--line);
  padding: 14px; overflow: auto; z-index: 30; box-sizing: border-box;
}
.audit-filters { display: grid; gap: 8px; margin-bottom: 12px; }
.audit-filters label { display: grid; gap: 4px; font-size: 12px; color: var(--muted); }
.audit-drawer ul { list-style: none; margin: 0; padding: 0; display: grid; gap: 10px; }
.a-top { display: flex; justify-content: space-between; gap: 8px; font-size: 13px; }
.a-cmd { margin: 4px 0; word-break: break-all; }
</style>
