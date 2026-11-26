<script setup lang="ts">
/**
 * 运行页（一屏）：页头「检查」「启动全部」；左栏命令列表（添加 / 编辑 / 删除）
 * 并入检查到的本机进程；右栏日志与启动 / 停止。
 */
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { ic } from '../icons'
import {
  store, currentProject, projectTasks, runTask, startAllTasks, stopTask,
  selectTask, clearSelectedTask, taskLogsOf, taskStatusOf, taskHistoryOf,
  loadProjects, refreshRunningCount, checkRuns, liveSessionOfCommand,
  unmatchedSessionsFor, stopSessionByPid,
} from '../workbenchStore'
import { apiPost, apiPut, apiDelete } from '../../http'
import {
  deleteCommandMessage, errorMessage, runWithConfirm, stopExternalMessage,
} from '../../confirmOps'
import type { RunCommandAdd, RunCommandInfo, RunCommandUpdate, RunSession } from '../../types'

const p = computed(() => currentProject())
/** 项目目录不可达：启动类按钮先禁用并标出原因，不发请求。 */
const unreachable = computed(() => !!p.value && p.value.pathExists === false)
const tasks = computed(() => projectTasks(p.value))
const logBox = ref<HTMLElement | null>(null)
const launchableCount = computed(() => p.value?.commands?.length ?? 0)
const hasCommands = computed(() => launchableCount.value > 0)
/** 对不上已登记命令的进程，单独成行。 */
const extras = computed<RunSession[]>(() => unmatchedSessionsFor(p.value))

const selectedId = computed<number | null>(() => store.selectedTaskId)
const selectedTask = computed(() => tasks.value.find((t) => t.id === selectedId.value) ?? null)
/** 选中的外部进程（与命令选中互斥）。 */
const selectedPid = ref<number | null>(null)
const selectedExtra = computed(() => extras.value.find((s) => s.pid === selectedPid.value) ?? null)

const logs = computed<string[]>(() => taskLogsOf(selectedId.value))
const history = computed(() => (selectedId.value == null ? undefined : taskHistoryOf(selectedId.value)))

function sessionOf(commandId: number): RunSession | undefined {
  return p.value ? liveSessionOfCommand(p.value.id, commandId) : undefined
}
/** 命令行状态：本页启动的或检查捕获的进程，都只在这一行显示。 */
function rowStatus(commandId: number): string {
  if (taskStatusOf(commandId) === 'running' || sessionOf(commandId)) return 'running'
  return taskStatusOf(commandId)
}
const status = computed(() => (selectedId.value == null ? 'idle' : rowStatus(selectedId.value)))

function statusText(s: string): string {
  return s === 'running' ? '运行中' : s === 'done' ? '已完成' : '空闲'
}
function statusClass(s: string): string {
  return s === 'running' ? 'warn' : s === 'done' ? 'ok' : 'muted'
}
function fmtTime(iso?: string): string {
  if (!iso) return '—'
  const d = new Date(iso)
  if (Number.isNaN(d.getTime()) || d.getFullYear() < 2000) return '—'
  return d.toLocaleTimeString('zh-CN', { hour12: false })
}

async function onSelect(id: number) {
  selectedPid.value = null
  await selectTask(id)
  await scrollToBottom()
}

function onSelectExtra(pid: number) {
  clearSelectedTask()
  selectedPid.value = pid
}

async function syncSelection() {
  if (selectedPid.value != null && !selectedExtra.value) selectedPid.value = null
  if (selectedPid.value != null) return
  if (tasks.value.some((t) => t.id === selectedId.value)) return
  if (tasks.value.length) {
    await onSelect(tasks.value[0].id)
    return
  }
  if (selectedId.value != null) clearSelectedTask()
  if (extras.value.length) onSelectExtra(extras.value[0].pid)
}

async function scrollToBottom() {
  await nextTick()
  const el = logBox.value
  if (el) el.scrollTop = el.scrollHeight
}

async function onRun() {
  const t = selectedTask.value
  if (!t || unreachable.value) return
  await runTask(t.id, t.name)
  await scrollToBottom()
}

const stopping = ref(false)

async function confirmStop(message: string): Promise<boolean> {
  try {
    await ElMessageBox.confirm(message, '停止进程', {
      type: 'warning',
      confirmButtonText: '确认停止',
      confirmButtonClass: 'el-button--danger',
      cancelButtonText: '取消',
    })
    return true
  } catch {
    return false
  }
}

/** 按进程标识停止（检查捕获的进程），先确认后请求。 */
async function stopByPid(s: RunSession) {
  const result = await runWithConfirm({
    title: '停止进程',
    message: stopExternalMessage(s.projectName, s.commandName, s.pid),
    confirm: confirmStop,
    action: () => stopSessionByPid(s.pid),
  })
  if (result.outcome === 'done') {
    const resp = result.data as { outcome?: string; message?: string } | undefined
    if (resp?.outcome === 'already-stopped') ElMessage.info(resp.message || '进程已不在运行，无需停止')
    else ElMessage.success(`已停止进程 PID ${s.pid}`)
  } else if (result.outcome === 'failed') {
    ElMessage.error(`停止失败：${result.error}`)
  }
}

async function onStop() {
  const t = selectedTask.value
  const extra = selectedExtra.value
  if (stopping.value || (!t && !extra)) return
  stopping.value = true
  try {
    if (extra) {
      await stopByPid(extra)
      await syncSelection()
      return
    }
    const s = sessionOf(t!.id)
    // 检查捕获、对得上命令的进程：本页没有启动记录，按进程标识停止
    if (s && s.origin !== 'Launched' && taskStatusOf(t!.id) !== 'running') await stopByPid(s)
    else {
      // stopTask 的停止路径已在收敛状态时刷新会话；未运行时不发任何请求
      await stopTask(t!.id, t!.name)
    }
  } finally {
    stopping.value = false
  }
}

async function onRunAll() {
  if (unreachable.value) return
  await startAllTasks()
}

const checking = ref(false)
async function onCheck() {
  if (checking.value) return
  checking.value = true
  try {
    await checkRuns()
    await syncSelection()
  } catch (e) {
    ElMessage.error(`检查失败：${errorMessage(e)}`)
  } finally {
    checking.value = false
  }
}

async function onChanged() {
  await loadProjects()
  await refreshRunningCount()
  await syncSelection()
}

watch(selectedId, async () => { await nextTick(); scrollToBottom() })
watch(() => logs.value.length, () => { void scrollToBottom() })
watch(() => extras.value.map((s) => s.pid).join(','), () => { void syncSelection() })

/** 有进程在跑时定时同步运行会话，保证行内状态和计数一致。 */
let sessionPoll: ReturnType<typeof setInterval> | undefined
watch(() => store.runningSessions.length, (n) => {
  if (n > 0 && !sessionPoll) sessionPoll = setInterval(() => { void refreshRunningCount() }, 3000)
  if (n === 0 && sessionPoll) { clearInterval(sessionPoll); sessionPoll = undefined }
}, { immediate: true })
onUnmounted(() => { if (sessionPoll) clearInterval(sessionPoll) })

onMounted(async () => {
  await refreshRunningCount()
  await syncSelection()
})

/* ---- 左栏添加 / 编辑 / 删除 ---- */
const dialogOpen = ref(false)
const editing = ref<RunCommandInfo | null>(null)
const saving = ref(false)
const formError = ref('')
const form = ref({ name: '', script: '', url: '', sort: '' })
/** 排序：整数，小的在前；留空按 0。 */
const sortValid = computed(() => /^(-?\d{1,6})?$/.test(String(form.value.sort).trim()))
const canSave = computed(() => !!form.value.name.trim() && !!form.value.script.trim() && sortValid.value && !saving.value)

function nextSort(): number {
  const list = p.value?.commands ?? []
  return list.length ? Math.max(...list.map((c) => c.sort ?? 0)) + 1 : 0
}

function openAdd() {
  if (!p.value) return
  editing.value = null
  form.value = { name: '', script: '', url: '', sort: String(nextSort()) }
  formError.value = ''
  dialogOpen.value = true
}

function openEdit(id: number) {
  const cmd = p.value?.commands.find((c) => c.id === id)
  if (!cmd) return
  editing.value = cmd
  form.value = { name: cmd.name, script: cmd.script, url: cmd.url ?? '', sort: String(cmd.sort ?? 0) }
  formError.value = ''
  dialogOpen.value = true
}

async function saveCommand() {
  if (!canSave.value || !p.value) return
  const name = form.value.name.trim()
  const script = form.value.script.trim()
  const url = form.value.url.trim() || null
  const sort = Number(String(form.value.sort).trim() || 0)
  saving.value = true
  formError.value = ''
  const known = new Set(tasks.value.map((t) => t.id))
  try {
    if (editing.value) {
      await apiPut(`/api/commands/${editing.value.id}`, { name, script, url, sort } as RunCommandUpdate)
    } else {
      await apiPost(`/api/projects/${p.value.id}/commands`, {
        projectId: p.value.id,
        name,
        script,
        url,
        sort,
      } as RunCommandAdd)
    }
    dialogOpen.value = false
    await onChanged()
    if (!editing.value) {
      const added = tasks.value.find((t) => !known.has(t.id))
      if (added) await onSelect(added.id)
    }
  } catch (e) {
    formError.value = errorMessage(e)
  } finally {
    saving.value = false
  }
}

async function removeCommand(cmd: RunCommandInfo) {
  const result = await runWithConfirm({
    title: '删除命令',
    message: deleteCommandMessage(cmd.name),
    confirm: async (message, title) => {
      try {
        await ElMessageBox.confirm(message, title, {
          type: 'warning',
          confirmButtonText: '确认删除',
          confirmButtonClass: 'el-button--danger',
          cancelButtonText: '取消',
        })
        return true
      } catch {
        return false
      }
    },
    action: () => apiDelete(`/api/commands/${cmd.id}`),
  })
  if (result.outcome === 'done') {
    ElMessage.success(`已删除命令「${cmd.name}」`)
    await onChanged()
  } else if (result.outcome === 'failed') {
    ElMessage.error(`删除失败：${result.error}`)
  }
}
</script>

<template>
  <div class="proj-header">
    <div class="proj-lead">
      <span class="wb-eyebrow">{{ p?.name }}</span>
      <h2>运行</h2>
    </div>
    <div class="proj-tools">
      <span v-if="unreachable" class="wb-badge warn" :title="p?.path">不可达</span>
      <button class="btn btn-secondary" :disabled="checking" @click="onCheck()">
        <span v-html="ic('refresh', 'sm')"></span>{{ checking ? '检查中…' : '检查' }}
      </button>
      <button class="btn btn-secondary" :disabled="!hasCommands || unreachable" @click="onRunAll()">
        <span v-html="ic('play', 'sm')"></span>启动全部
      </button>
    </div>
  </div>

  <div class="proj-canvas run-screen">
    <div class="task-layout">
      <div class="task-panel">
        <div class="task-tools">
          <label class="search">
            <span v-html="ic('search', 'sm')"></span>
            <input v-model="store.taskFilter" placeholder="搜索命令" aria-label="搜索命令" />
          </label>
          <button class="btn btn-sm" :class="hasCommands ? 'btn-secondary' : 'btn-primary'"
                  :disabled="!p" @click="openAdd()">
            添加
          </button>
        </div>
        <div class="task-list">
          <div v-if="!hasCommands && !extras.length" class="wb-empty-inline">
            <span v-html="ic('terminal', 'lg')"></span>
            <div>还没有命令，点「添加」新建一条</div>
          </div>
          <div v-for="t in tasks" :key="t.id" class="task-item" :class="{ sel: t.id === selectedId }"
               @click="onSelect(t.id)">
            <div class="row-main">
              <div class="row-title">{{ t.name }}</div>
              <div class="row-sub">
                <span class="run-dot" :class="statusClass(rowStatus(t.id))"></span>{{ statusText(rowStatus(t.id)) }}<template v-if="sessionOf(t.id)"> · PID {{ sessionOf(t.id)!.pid }}</template>
              </div>
            </div>
            <div class="task-item-ops">
              <button class="btn-icon" aria-label="编辑" @click.stop="openEdit(t.id)">
                <span v-html="ic('pencil', 'sm')"></span>
              </button>
              <button class="btn-icon danger" aria-label="删除" @click.stop="removeCommand(t)">
                <span v-html="ic('close', 'sm')"></span>
              </button>
            </div>
          </div>
          <div v-for="s in extras" :key="`pid-${s.pid}`" class="task-item" :class="{ sel: s.pid === selectedPid }"
               @click="onSelectExtra(s.pid)">
            <div class="row-main">
              <div class="row-title">{{ s.commandName }}</div>
              <div class="row-sub"><span class="run-dot warn"></span>本机进程 · PID {{ s.pid }}</div>
            </div>
          </div>
        </div>
      </div>

      <div class="task-detail">
        <template v-if="selectedTask">
          <div class="task-detail-head">
            <div class="task-detail-title">
              <strong>{{ selectedTask.name }}</strong>
              <span class="wb-badge" :class="statusClass(status)">{{ statusText(status) }}</span>
              <span v-if="history" class="task-time">
                起 {{ fmtTime(history.startedAt) }}<template v-if="history.endedAt"> · 止 {{ fmtTime(history.endedAt) }}</template>
              </span>
            </div>
            <div class="task-detail-actions">
              <a v-if="selectedTask.url && status === 'running'" class="btn btn-secondary" :href="selectedTask.url"
                 target="_blank" rel="noopener">打开</a>
              <button class="btn btn-primary" :disabled="status === 'running' || unreachable" @click="onRun()">
                <span v-html="ic('play', 'sm')"></span>{{ status === 'running' ? '运行中' : '启动' }}
              </button>
              <button class="btn btn-secondary" :disabled="stopping" @click="onStop()">
                <span v-html="ic('close', 'sm')"></span>{{ stopping ? '停止中…' : '停止' }}
              </button>
            </div>
          </div>
          <div v-if="unreachable" class="task-unreachable" role="status">
            <span v-html="ic('info', 'sm')"></span>项目目录不可达，不能启动：{{ p?.path }}
          </div>
          <div class="task-script">
            <span v-html="ic('terminal', 'sm')"></span>{{ selectedTask.script }}
          </div>
          <div ref="logBox" class="log log-box">
            <div v-if="!logs.length" class="wb-empty-inline">点「启动」后输出显示在这里</div>
            <div v-for="(l, i) in logs" :key="i" class="log-line"
                 :class="{ err: l.startsWith('[err]') || l.startsWith('✗'), ok: l.startsWith('✓') }">
              {{ l }}
            </div>
          </div>
        </template>
        <template v-else-if="selectedExtra">
          <div class="task-detail-head">
            <div class="task-detail-title">
              <strong>{{ selectedExtra.commandName }}</strong>
              <span class="wb-badge warn">运行中</span>
              <span class="task-time">PID {{ selectedExtra.pid }}</span>
            </div>
            <div class="task-detail-actions">
              <button class="btn btn-secondary" :disabled="stopping" @click="onStop()">
                <span v-html="ic('close', 'sm')"></span>{{ stopping ? '停止中…' : '停止' }}
              </button>
            </div>
          </div>
          <div class="wb-empty-inline">检查到的本机进程，读不到输出，只能停止</div>
        </template>
        <div v-else class="wb-empty-inline">在左侧添加命令后即可启动</div>
      </div>
    </div>
  </div>

<div v-if="dialogOpen" class="cmd-dlg">
    <div class="cmd-dlg-mask" @click="dialogOpen = false"></div>
    <div class="cmd-dlg-box">
      <h3>{{ editing ? '编辑命令' : '添加命令' }}</h3>
      <div class="form-group">
        <label class="form-label">名称</label>
        <input v-model="form.name" class="form-control" aria-label="命令名称" maxlength="80" placeholder="如：前端 dev" />
      </div>
      <div class="form-group">
        <label class="form-label">脚本</label>
        <input v-model="form.script" class="form-control" aria-label="命令脚本" placeholder="在项目根执行的命令，如 npm run dev" />
      </div>
      <div class="form-group">
        <label class="form-label">访问地址（可选）</label>
        <input v-model="form.url" class="form-control" aria-label="访问地址" placeholder="如 http://localhost:5173" />
      </div>
      <div class="form-group">
        <label class="form-label">排序</label>
        <input v-model="form.sort" class="form-control" aria-label="排序" inputmode="numeric" placeholder="整数，小的排在前面" />
      </div>
      <p v-if="formError" class="wb-hint" style="color:var(--el-color-error)">{{ formError }}</p>
      <div class="cmd-dlg-actions">
        <button class="btn btn-secondary" :disabled="saving" @click="dialogOpen = false">取消</button>
        <button class="btn btn-primary" :disabled="!canSave" @click="saveCommand()">
          {{ saving ? '保存中…' : '保存' }}
        </button>
      </div>
    </div>
  </div>
</template>
