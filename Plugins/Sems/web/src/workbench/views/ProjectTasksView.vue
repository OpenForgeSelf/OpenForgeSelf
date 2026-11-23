<script setup lang="ts">
/**
 * S10 任务（v0.2 口径：HTML 原型为功能基线）
 *  - 左栏任务列表 + 右栏常驻日志区（不再把日志藏在折叠面板里）
 *  - 选中即拉取：切换任务立刻拉一次日志，不依赖「先点开输出日志」
 *  - 实时追加：运行中按 1.2s 增量轮询追加，自动滚到底
 *  - 状态机：空闲 → 运行中 → 已完成；结束后保留日志与结束时间，记录不消失
 *  - 保留 HTML 原型没有、但 sems 独有的能力：停止会话、启动全部、本机进程检测、命令维护
 */
import { computed, nextTick, onMounted, ref, watch } from 'vue'
import { ic } from '../icons'
import {
  store, currentProject, projectTasks, runTask, startAllTasks, stopTask,
  selectTask, pullTaskLogs, taskLogsOf, taskStatusOf, taskHistoryOf,
  markTaskStarted, loadProjects, refreshRunningCount, toast,
} from '../workbenchStore'
import CommandList from '../../CommandList.vue'
import RunPanel from '../../RunPanel.vue'

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

/** 选中任务：立刻拉一次日志并把滚动条压到底。 */
async function onSelect(id: number) {
  await selectTask(id)
  await scrollToBottom()
}

/** 自动滚到底：日志追加后保持视线在最新一行。 */
async function scrollToBottom() {
  await nextTick()
  const el = logBox.value
  if (el) el.scrollTop = el.scrollHeight
}

async function onRun() {
  const t = selectedTask.value
  if (!t) { toast('请先在运行命令维护中添加一条命令'); return }
  await runTask(t.id, t.name)
  await scrollToBottom()
}

const stopping = ref(false)

/** 停止任务：不管结果如何 stopTask 内部必给反馈（toast + 日志留痕）；进行中去重（AC-F09-1/4，修 PILOT-087）。 */
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
}

/** 维护区「启动」上报：与详情区「运行任务」共用状态入口，状态机不再只更新一条路径（修 B-1）。 */
async function onCommandRun(id: number) {
  markTaskStarted(id)
  toast('命令已启动，运行页已刷新')
  void runPanelRef.value?.refresh()
  await onChanged()
}

// 选中任务变化 → 重置滚动位置
watch(selectedId, async () => { await nextTick(); scrollToBottom() })
// 日志追加 → 自动滚到底
watch(() => logs.value.length, () => { void scrollToBottom() })

onMounted(async () => {
  // 首次进入：默认选中第一个任务并拉一次日志
  const first = tasks.value[0]
  if (first && selectedId.value == null) await onSelect(first.id)
  await refreshRunningCount()
})
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
      <!-- 左：任务列表 -->
      <div class="task-panel">
        <div class="task-tools">
          <label class="search">
            <span v-html="ic('search', 'sm')"></span>
            <input v-model="store.taskFilter" placeholder="搜索命令" aria-label="搜索命令" />
          </label>
          <button class="btn-icon" aria-label="刷新" @click="onChanged()">
            <span v-html="ic('refresh', 'sm')"></span>
          </button>
        </div>
        <div v-if="!tasks.length" class="wb-empty-inline">暂无运行命令，请在下方添加</div>
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
        </div>
      </div>

      <!-- 右：常驻日志区 -->
      <div class="task-detail">
        <div class="task-detail-head">
          <div class="task-detail-title">
            <strong>{{ selectedTask?.name || '—' }}</strong>
            <span class="wb-badge" :class="statusClass(status)">{{ statusText(status) }}</span>
            <span v-if="history" class="task-time">
              起 {{ fmtTime(history.startedAt) }}<template v-if="history.endedAt"> · 止 {{ fmtTime(history.endedAt) }}</template>
            </span>
          </div>
          <div class="task-detail-actions">
            <button class="btn btn-primary" :disabled="launchableCount === 0 || status === 'running'" @click="onRun()">
              <span v-html="ic('play', 'sm')"></span>{{ status === 'running' ? '运行中' : '启动' }}
            </button>
            <!-- 停止按钮保持可点（点得动就必有反馈：已停止/已不在运行/停止失败），仅进行中去重（AC-F09-1/4，修 PILOT-087） -->
            <button class="btn btn-secondary" :disabled="!selectedTask || stopping" @click="onStop()">
              <span v-html="ic('close', 'sm')"></span>{{ stopping ? '停止中…' : '停止' }}
            </button>
          </div>
        </div>

        <div v-if="selectedTask" class="task-script">
          <span v-html="ic('terminal', 'sm')"></span>{{ selectedTask.script }}
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
      </div>
    </div>

    <div class="wb-embedded">
      <h3>运行会话（sems 原有能力：会话列表 / 停止 / 本机进程检测）</h3>
      <RunPanel ref="runPanelRef" :command-urls="commandUrls" :launchable-count="launchableCount"
                :emphasize-run-all="false"
                @run-all="onRunAll" @count="(n: number) => (store.runningCount = n)" />
    </div>

    <div v-if="p" class="wb-embedded">
      <h3>运行命令维护（sems 原有能力）</h3>
      <CommandList :project-id="p.id" :commands="p.commands" @changed="onChanged" @run="onCommandRun" />
      <p class="wb-hint">运行会话只保存在宿主内存中，宿主重启后清空；完成记录与日志在前端本地留存，不会因会话消失而丢失。</p>
    </div>
  </div>
</template>
