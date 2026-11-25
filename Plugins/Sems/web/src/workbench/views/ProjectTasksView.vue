<script setup lang="ts">
/**
 * 运行页：左栏命令列表（含添加 / 编辑 / 删除），右栏日志与启动 / 停止。
 * 「启动全部」只在页头；运行面板只保留检查和会话列表。
 */
import { computed, nextTick, onMounted, ref, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { ic } from '../icons'
import {
  store, currentProject, projectTasks, runTask, startAllTasks, stopTask,
  selectTask, clearSelectedTask, taskLogsOf, taskStatusOf, taskHistoryOf,
  loadProjects, refreshRunningCount, toast,
} from '../workbenchStore'
import RunPanel from '../../RunPanel.vue'
import { apiPost, apiPut, apiDelete } from '../../http'
import { deleteCommandMessage, errorMessage, runWithConfirm } from '../../confirmOps'
import type { RunCommandAdd, RunCommandInfo, RunCommandUpdate } from '../../types'

const p = computed(() => currentProject())
const tasks = computed(() => projectTasks(p.value))
const runPanelRef = ref<InstanceType<typeof RunPanel> | null>(null)
const logBox = ref<HTMLElement | null>(null)
const commandUrls = computed<Record<number, string>>(() => {
  const m: Record<number, string> = {}
  for (const c of p.value?.commands ?? []) if (c.url) m[c.id] = c.url
  return m
})
const launchableCount = computed(() => p.value?.commands?.length ?? 0)
const canOperate = computed(() => launchableCount.value > 0 && !!selectedTask.value)

const selectedId = computed<number | null>(() => store.selectedTaskId)
const selectedTask = computed(() => tasks.value.find((t) => t.id === selectedId.value) ?? null)
const logs = computed<string[]>(() => taskLogsOf(selectedId.value))
const status = computed(() => (selectedId.value == null ? 'idle' : taskStatusOf(selectedId.value)))
const history = computed(() => (selectedId.value == null ? undefined : taskHistoryOf(selectedId.value)))

function statusText(s: string): string {
  return s === 'running' ? '运行中' : s === 'done' ? '已完成' : '空闲'
}
function statusClass(s: string): string {
  return s === 'running' ? 'warn' : s === 'done' ? 'ok' : 'muted'
}
function fmtTime(iso?: string): string {
  if (!iso) return '—'
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return '—'
  return d.toLocaleTimeString('zh-CN', { hour12: false })
}

async function onSelect(id: number) {
  await selectTask(id)
  await scrollToBottom()
}

async function syncSelection() {
  if (!tasks.value.length) {
    if (selectedId.value != null) clearSelectedTask()
    return
  }
  if (!tasks.value.some((t) => t.id === selectedId.value)) {
    await onSelect(tasks.value[0].id)
  }
}

async function scrollToBottom() {
  await nextTick()
  const el = logBox.value
  if (el) el.scrollTop = el.scrollHeight
}

async function onRun() {
  const t = selectedTask.value
  if (!t) return
  await runTask(t.id, t.name)
  await scrollToBottom()
}

const stopping = ref(false)

async function onStop() {
  const t = selectedTask.value
  if (!t || stopping.value) return
  stopping.value = true
  try {
    await stopTask(t.id, t.name)
  } finally {
    stopping.value = false
  }
}

async function onRunAll() {
  await startAllTasks()
  await runPanelRef.value?.refresh()
}

async function onChanged() {
  await loadProjects()
  await refreshRunningCount()
  await syncSelection()
}

watch(selectedId, async () => { await nextTick(); scrollToBottom() })
watch(() => logs.value.length, () => { void scrollToBottom() })

onMounted(async () => {
  await syncSelection()
  await refreshRunningCount()
})

/* ---- 左栏添加 / 编辑 / 删除 ---- */
const dialogOpen = ref(false)
const editing = ref<RunCommandInfo | null>(null)
const saving = ref(false)
const formError = ref('')
const form = ref({ name: '', script: '', url: '' })
const canSave = computed(() => !!form.value.name.trim() && !!form.value.script.trim() && !saving.value)

function openAdd() {
  if (!p.value) return
  editing.value = null
  form.value = { name: '', script: '', url: '' }
  formError.value = ''
  dialogOpen.value = true
}

function openEdit(id: number) {
  const cmd = p.value?.commands.find((c) => c.id === id)
  if (!cmd) return
  editing.value = cmd
  form.value = { name: cmd.name, script: cmd.script, url: cmd.url ?? '' }
  formError.value = ''
  dialogOpen.value = true
}

async function saveCommand() {
  if (!canSave.value || !p.value) return
  const name = form.value.name.trim()
  const script = form.value.script.trim()
  const url = form.value.url.trim() || null
  saving.value = true
  formError.value = ''
  const known = new Set(tasks.value.map((t) => t.id))
  try {
    if (editing.value) {
      await apiPut(`/api/commands/${editing.value.id}`, { name, script, url } as RunCommandUpdate)
    } else {
      await apiPost(`/api/projects/${p.value.id}/commands`, {
        projectId: p.value.id,
        name,
        script,
        url,
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
    <h2>运行</h2>
    <div class="proj-tools">
      <span class="wb-badge info">运行中 {{ store.runningCount }}</span>
      <button class="btn btn-secondary" :disabled="launchableCount === 0" @click="onRunAll()">
        <span v-html="ic('play', 'sm')"></span>启动全部
      </button>
      <span v-if="launchableCount === 0" class="wb-hint">没有可启动的命令</span>
      <button class="btn btn-secondary" @click="onChanged()">
        <span v-html="ic('refresh', 'sm')"></span>刷新
      </button>
    </div>
  </div>

  <div class="proj-canvas">
    <div class="task-layout">
      <div class="task-panel">
        <div class="task-tools">
          <label class="search">
            <span v-html="ic('search', 'sm')"></span>
            <input v-model="store.taskFilter" placeholder="搜索命令" aria-label="搜索命令" />
          </label>
          <button class="btn btn-secondary btn-sm" :disabled="!p" @click="openAdd()">
            添加命令
          </button>
        </div>
        <div v-if="!tasks.length" class="wb-empty-inline">暂无运行命令，请在左侧添加</div>
        <div v-for="t in tasks" :key="t.id" class="task-item" :class="{ sel: t.id === selectedId }"
             @click="onSelect(t.id)">
          <span class="row-icon"><span v-html="ic(t.name === 'lint' ? 'tasks' : 'play', 'sm')"></span></span>
          <div class="row-main">
            <div class="row-title">{{ t.name }}</div>
            <div class="row-sub">
              {{ statusText(t.status) }} · {{ t.description }}
            </div>
          </div>
          <span v-if="t.status === 'running'" class="spinner"></span>
          <span v-else-if="t.status === 'done'" class="wb-badge ok sm">完成</span>
          <div class="task-item-ops">
            <button class="btn-icon" aria-label="编辑" @click.stop="openEdit(t.id)">
              <span v-html="ic('pencil', 'sm')"></span>
            </button>
            <button class="btn-icon danger" aria-label="删除" @click.stop="removeCommand(t)">
              <span v-html="ic('close', 'sm')"></span>
            </button>
          </div>
        </div>
      </div>

      <div class="task-detail">
        <template v-if="canOperate">
          <div class="task-detail-head">
            <div class="task-detail-title">
              <strong>{{ selectedTask?.name }}</strong>
              <span class="wb-badge" :class="statusClass(status)">{{ statusText(status) }}</span>
              <span v-if="history" class="task-time">
                起 {{ fmtTime(history.startedAt) }}<template v-if="history.endedAt"> · 止 {{ fmtTime(history.endedAt) }}</template>
              </span>
            </div>
            <div class="task-detail-actions">
              <button class="btn btn-primary" :disabled="status === 'running'" @click="onRun()">
                <span v-html="ic('play', 'sm')"></span>{{ status === 'running' ? '运行中' : '启动' }}
              </button>
              <button class="btn btn-secondary" :disabled="stopping" @click="onStop()">
                <span v-html="ic('close', 'sm')"></span>{{ stopping ? '停止中…' : '停止' }}
              </button>
            </div>
          </div>

          <div class="task-script">
            <span v-html="ic('terminal', 'sm')"></span>{{ selectedTask?.script }}
          </div>

          <div ref="logBox" class="log log-box">
            <div v-if="!logs.length" class="wb-empty-inline">
              选择左侧命令后点击「启动」，实时输出会持续追加到这里。
            </div>
            <div v-for="(l, i) in logs" :key="i" class="log-line"
                 :class="{ err: l.startsWith('[err]') || l.startsWith('✗'), ok: l.startsWith('✓') }">
              {{ l }}
            </div>
          </div>
        </template>
        <div v-else class="wb-empty-inline">
          还没有运行命令。请在左侧添加一条后再启动。
        </div>
      </div>
    </div>

    <div class="wb-embedded">
      <h3>运行会话</h3>
      <RunPanel ref="runPanelRef" :command-urls="commandUrls" :launchable-count="launchableCount"
                :emphasize-run-all="false" :show-run-all="false"
                @count="(n: number) => (store.runningCount = n)" />
      <p class="wb-hint">运行会话只保存在宿主内存中，宿主重启后清空；完成记录与日志在前端本地留存，不会因会话消失而丢失。</p>
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
