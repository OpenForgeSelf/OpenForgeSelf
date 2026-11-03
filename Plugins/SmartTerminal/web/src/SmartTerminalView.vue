<script setup lang="ts">
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue'
import { fetchPluginVersion, smartTerminalApi } from './api'
import type {
  AuditEvent,
  CommandRecord,
  OutputChunk,
  PendingApproval,
  PermissionMode,
  ShellProfile,
  TerminalSession,
} from './types'

const PLUGIN_ID = 'smart-terminal'

const version = ref('加载中')
const shells = ref<ShellProfile[]>([])
const sessions = ref<TerminalSession[]>([])
const activeId = ref<string>('')
const chunks = ref<OutputChunk[]>([])
const nextIndex = ref(0)
const commands = ref<CommandRecord[]>([])
const approvals = ref<PendingApproval[]>([])
const auditItems = ref<AuditEvent[]>([])
const globalMode = ref<PermissionMode>('AskWrite')

const draftTitle = ref('智能终端会话')
const draftShell = ref('')
const draftCwd = ref('')
const commandInput = ref('')
const busy = ref('')
const errorText = ref('')
const unauthorized = ref(false)
const showAudit = ref(false)
const showApproval = ref(false)
const activeApproval = ref<PendingApproval | null>(null)

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

function noteError(e: unknown) {
  const anyErr = e as { status?: number; message?: string }
  unauthorized.value = anyErr?.status === 401
  errorText.value = anyErr?.message ? `操作失败：${anyErr.message}` : '操作失败：未知错误'
}

async function refreshMeta() {
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
  sessions.value = list ?? []
  if (!draftShell.value) {
    const first = shells.value.find(s => s.available)
    draftShell.value = first?.id ?? ''
  }
  if (!draftCwd.value) draftCwd.value = ''
  if (!activeId.value && sessions.value.length) {
    activeId.value = sessions.value[0].sessionId
  }
}

async function refreshApprovals() {
  approvals.value = (await smartTerminalApi.listApprovals(activeId.value || undefined)) ?? []
  if (approvals.value.length && !showApproval.value) {
    activeApproval.value = approvals.value[0]
    showApproval.value = true
  }
}

async function selectSession(id: string) {
  activeId.value = id
  chunks.value = []
  nextIndex.value = 0
  await Promise.all([pollOutput(true), loadCommands(), refreshApprovals()])
}

async function createSession() {
  if (busy.value) return
  busy.value = 'create'
  errorText.value = ''
  try {
    const session = await smartTerminalApi.createSession({
      title: draftTitle.value || undefined,
      shellId: draftShell.value || undefined,
      cwd: draftCwd.value || undefined,
      permissionMode: globalMode.value,
    })
    await refreshMeta()
    if (session?.sessionId) await selectSession(session.sessionId)
  } catch (e) {
    noteError(e)
  } finally {
    busy.value = ''
  }
}

async function runCommand() {
  if (!activeId.value || !commandInput.value.trim() || busy.value) return
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
  try {
    await smartTerminalApi.setPermissionMode(mode)
    if (activeId.value) await smartTerminalApi.setSessionMode(activeId.value, mode)
    await refreshMeta()
  } catch (e) {
    noteError(e)
  }
}

async function loadCommands() {
  if (!activeId.value) {
    commands.value = []
    return
  }
  commands.value = (await smartTerminalApi.commands(activeId.value)) ?? []
}

async function pollOutput(reset = false) {
  if (!activeId.value) return
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
      await nextTick()
      if (consoleEl.value) consoleEl.value.scrollTop = consoleEl.value.scrollHeight
    }
  } catch (e) {
    // 轮询失败不刷大红字，避免闪烁；首屏仍走 noteError
    if (reset) noteError(e)
  }
}

async function openAudit() {
  showAudit.value = true
  try {
    const data = await smartTerminalApi.audit(100)
    auditItems.value = data?.items ?? []
  } catch (e) {
    noteError(e)
  }
}

async function rerun(commandId: string) {
  if (!activeId.value || busy.value) return
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

async function closeActive() {
  if (!activeId.value) return
  try {
    await smartTerminalApi.close(activeId.value)
    activeId.value = ''
    chunks.value = []
    await refreshMeta()
  } catch (e) {
    noteError(e)
  }
}

onMounted(async () => {
  try {
    await refreshMeta()
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

watch(activeId, () => {
  void loadCommands()
})
</script>

<template>
  <div class="st-root">
    <header class="st-header">
      <div>
        <h1 class="view-title">智能终端 <span class="ver">v{{ version }}</span></h1>
        <p class="sub">会话化终端 · 三级权限 · JSONL 审计 · Agent 工具 <code>st_*</code></p>
      </div>
      <div class="header-actions">
        <span class="badge" :class="modeBadgeClass">权限：{{ modeLabel }}</span>
        <button class="btn ghost" type="button" @click="openAudit">审计</button>
      </div>
    </header>

    <div v-if="unauthorized" class="banner error">未授权（401）。请先登录宿主，确保 localStorage 有 forge_api_token。</div>
    <div v-else-if="errorText" class="banner error">{{ errorText }}</div>

    <div class="st-grid">
      <aside class="panel side">
        <div class="panel-head">
          <strong>会话</strong>
          <button class="btn tiny" type="button" :disabled="!!busy" @click="createSession">新建</button>
        </div>
        <div class="create-box">
          <label>标题</label>
          <input v-model="draftTitle" placeholder="会话标题" />
          <label>外壳</label>
          <select v-model="draftShell">
            <option disabled value="">选择外壳</option>
            <option v-for="s in shells" :key="s.id" :value="s.id" :disabled="!s.available">
              {{ s.displayName }}{{ s.available ? '' : '（不可用）' }}
            </option>
          </select>
          <label>工作目录</label>
          <input v-model="draftCwd" placeholder="留空=进程当前目录" />
          <label>全局权限</label>
          <div class="mode-row">
            <button type="button" class="chip" :class="{ on: globalMode === 'Auto' }" @click="changeMode('Auto')">Auto</button>
            <button type="button" class="chip" :class="{ on: globalMode === 'AskWrite' }" @click="changeMode('AskWrite')">AskWrite</button>
            <button type="button" class="chip" :class="{ on: globalMode === 'AskHighRisk' }" @click="changeMode('AskHighRisk')">AskHighRisk</button>
          </div>
        </div>
        <ul class="session-list">
          <li v-if="!sessions.length" class="empty">暂无会话。点「新建」开始。</li>
          <li
            v-for="s in sessions"
            :key="s.sessionId"
            :class="{ active: s.sessionId === activeId }"
            @click="selectSession(s.sessionId)"
          >
            <div class="s-title">{{ s.title }}</div>
            <div class="s-meta">{{ s.shellDisplayName }} · {{ s.status }} · {{ normalizeMode(s.permissionMode) }}</div>
          </li>
        </ul>
      </aside>

      <main class="panel main">
        <div class="panel-head">
          <div>
            <strong>{{ activeSession?.title || '未选择会话' }}</strong>
            <span v-if="activeSession" class="muted"> · {{ activeSession.shellDisplayName }} · {{ activeSession.cwd }}</span>
          </div>
          <button v-if="activeSession" class="btn ghost tiny" type="button" @click="closeActive">关闭会话</button>
        </div>

        <div ref="consoleEl" class="console" aria-live="polite">
          <div v-if="!activeSession" class="empty">请先新建或选择一个会话。</div>
          <div v-else-if="!chunks.length" class="empty">等待输出…输入命令后回车执行。</div>
          <div v-for="c in chunks" :key="c.index" class="line" :class="c.stream">
            <span class="tag">{{ c.stream }}</span>
            <span class="txt">{{ c.text }}</span>
          </div>
        </div>

        <form class="cmdline" @submit.prevent="runCommand">
          <input
            v-model="commandInput"
            :disabled="!activeSession || !!busy"
            placeholder="输入命令，Enter 执行（密钥会在日志中脱敏）"
            autocomplete="off"
            spellcheck="false"
          />
          <button class="btn primary" type="submit" :disabled="!activeSession || !commandInput.trim() || !!busy">
            {{ busy === 'run' ? '执行中…' : '运行' }}
          </button>
        </form>

        <div class="history">
          <div class="panel-head"><strong>命令历史</strong></div>
          <ul>
            <li v-if="!commands.length" class="empty">暂无历史。</li>
            <li v-for="c in commands" :key="c.commandId">
              <code>{{ c.commandRedacted }}</code>
              <span class="pill">{{ riskLabel(c.risk) }}</span>
              <span class="muted">{{ c.status }}{{ c.exitCode != null ? ` · exit ${c.exitCode}` : '' }}</span>
              <button class="btn tiny ghost" type="button" @click="rerun(c.commandId)">重跑</button>
            </li>
          </ul>
        </div>
      </main>
    </div>

    <div v-if="showApproval && activeApproval" class="modal-mask" @click.self="showApproval = false">
      <div class="modal" role="dialog" aria-modal="true">
        <h2>需要审批</h2>
        <p class="muted">{{ activeApproval.reason }}</p>
        <div class="modal-cmd"><code>{{ activeApproval.commandRedacted }}</code></div>
        <p>风险：<strong>{{ riskLabel(activeApproval.risk) }}</strong> · 规则：{{ activeApproval.matchedRule }}</p>
        <div class="modal-actions">
          <button class="btn ghost" type="button" :disabled="!!busy" @click="resolveApproval(false)">拒绝</button>
          <button class="btn primary" type="button" :disabled="!!busy" @click="resolveApproval(true)">批准执行</button>
        </div>
      </div>
    </div>

    <aside v-if="showAudit" class="audit-drawer">
      <div class="panel-head">
        <strong>审计日志（JSONL）</strong>
        <button class="btn tiny ghost" type="button" @click="showAudit = false">关闭</button>
      </div>
      <ul>
        <li v-for="a in auditItems" :key="a.eventId">
          <div class="a-top"><span>{{ a.kind }}</span><span class="muted">{{ a.time }}</span></div>
          <div v-if="a.commandRedacted" class="a-cmd"><code>{{ a.commandRedacted }}</code></div>
          <div class="muted">{{ a.decision || '' }} {{ a.matchedRule || '' }} {{ a.detail || '' }}</div>
        </li>
        <li v-if="!auditItems.length" class="empty">暂无审计事件。</li>
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
  gap: 12px;
  padding: 16px;
  color: var(--text);
  box-sizing: border-box;
  min-height: 0;
}
.st-root > * { flex-shrink: 0; }
.st-grid { flex: 1; min-height: 0; display: grid; grid-template-columns: 280px 1fr; gap: 12px; }
.st-header { display: flex; justify-content: space-between; align-items: flex-start; gap: 12px; }
.view-title { margin: 0; font-size: 20px; }
.ver { font-size: 12px; color: var(--muted); font-weight: 500; }
.sub { margin: 4px 0 0; color: var(--muted); font-size: 13px; }
.header-actions { display: flex; gap: 8px; align-items: center; }
.panel {
  background: var(--panel);
  border: 1px solid var(--line);
  border-radius: 12px;
  padding: 12px;
  display: flex;
  flex-direction: column;
  min-height: 0;
}
.side { overflow: auto; }
.main { min-height: 0; }
.panel-head { display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px; gap: 8px; }
.create-box { display: grid; gap: 6px; margin-bottom: 12px; }
.create-box label { font-size: 12px; color: var(--muted); }
.create-box input, .create-box select, .cmdline input {
  width: 100%;
  box-sizing: border-box;
  background: transparent;
  color: var(--text);
  border: 1px solid var(--line);
  border-radius: 8px;
  padding: 8px 10px;
}
.mode-row { display: flex; gap: 6px; flex-wrap: wrap; }
.chip {
  border: 1px solid var(--line);
  background: transparent;
  color: var(--text);
  border-radius: 999px;
  padding: 4px 10px;
  cursor: pointer;
  font-size: 12px;
}
.chip.on { border-color: var(--accent); color: var(--accent); }
.session-list { list-style: none; margin: 0; padding: 0; display: grid; gap: 6px; }
.session-list li {
  border: 1px solid var(--line);
  border-radius: 8px;
  padding: 8px;
  cursor: pointer;
}
.session-list li.active { border-color: var(--accent); background: color-mix(in srgb, var(--accent) 12%, transparent); }
.s-title { font-weight: 600; font-size: 13px; }
.s-meta, .muted { color: var(--muted); font-size: 12px; }
.console {
  flex: 1;
  min-height: 220px;
  overflow: auto;
  background: #0b0f14;
  border: 1px solid var(--line);
  border-radius: 10px;
  padding: 10px;
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 12.5px;
}
.line { display: grid; grid-template-columns: 64px 1fr; gap: 8px; margin-bottom: 4px; }
.line .tag { color: var(--muted); }
.line.stderr .txt { color: var(--danger); }
.line.system .txt { color: var(--warn); }
.cmdline { display: flex; gap: 8px; margin-top: 10px; }
.history { margin-top: 12px; max-height: 180px; overflow: auto; }
.history ul { list-style: none; margin: 0; padding: 0; display: grid; gap: 6px; }
.history li { display: flex; gap: 8px; align-items: center; flex-wrap: wrap; font-size: 12px; }
.pill { border: 1px solid var(--line); border-radius: 999px; padding: 1px 8px; }
.btn {
  border: 1px solid var(--line);
  background: transparent;
  color: var(--text);
  border-radius: 8px;
  padding: 8px 12px;
  cursor: pointer;
}
.btn.primary { background: var(--accent); border-color: var(--accent); color: #fff; }
.btn.ghost { background: transparent; }
.btn.tiny { padding: 4px 8px; font-size: 12px; }
.btn:disabled { opacity: 0.5; cursor: not-allowed; }
.badge { border-radius: 999px; padding: 4px 10px; font-size: 12px; border: 1px solid var(--line); }
.badge-auto { color: var(--ok); border-color: var(--ok); }
.badge-write { color: var(--accent); border-color: var(--accent); }
.badge-high { color: var(--warn); border-color: var(--warn); }
.banner { border-radius: 8px; padding: 8px 10px; font-size: 13px; }
.banner.error { background: color-mix(in srgb, var(--danger) 16%, transparent); color: var(--danger); }
.empty { color: var(--muted); font-size: 13px; padding: 8px 0; }
.modal-mask {
  position: fixed; inset: 0; background: rgba(0,0,0,.45);
  display: grid; place-items: center; z-index: 40;
}
.modal {
  width: min(520px, 92vw);
  background: var(--panel);
  border: 1px solid var(--line);
  border-radius: 14px;
  padding: 18px;
}
.modal-cmd {
  background: #0b0f14;
  border-radius: 8px;
  padding: 10px;
  margin: 10px 0;
  word-break: break-all;
}
.modal-actions { display: flex; justify-content: flex-end; gap: 8px; margin-top: 14px; }
.audit-drawer {
  position: fixed; top: 0; right: 0; bottom: 0; width: min(420px, 92vw);
  background: var(--panel); border-left: 1px solid var(--line);
  padding: 14px; overflow: auto; z-index: 30;
}
.audit-drawer ul { list-style: none; margin: 0; padding: 0; display: grid; gap: 10px; }
.a-top { display: flex; justify-content: space-between; gap: 8px; font-size: 13px; }
.a-cmd { margin: 4px 0; word-break: break-all; }
@media (max-width: 960px) {
  .st-grid { grid-template-columns: 1fr; }
}
</style>
