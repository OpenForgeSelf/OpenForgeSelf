<script setup lang="ts">
/** 待办主界面：阶段流水线、关注项和可切换的列表/看板。 */
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import NotifyHost from './NotifyHost.vue'
import TaskDetail from './components/TaskDetail.vue'
import * as store from './store'
import { changeStage, fetchPluginVersion } from './http'
import { confirmAction, showToast } from './notify'
import {
  agentFallbackByStage, agentNameOrFallback, agentStatusMeta, missingLabels, priorityLabel,
  relativeTime, shortPath, sortTasks, stageLabel, stageTagType
} from './actions'
import type { TagType } from './actions'
import type { TodoItem, TodoStage } from './types'

const STAGES: TodoStage[] = ['Draft', 'Ready', 'Dispatched', 'Running', 'Blocked', 'Review', 'Done', 'Cancelled']
const STAGE_LABELS: Record<string, string> = Object.fromEntries(STAGES.map(s => [s, stageLabel(s)]))

const newTitle = ref('')
const newProjectId = ref(0)
const version = ref('加载中…')
const s = store.state
const draggingTask = ref<TodoItem | null>(null)

const visibleItems = computed(() => sortTasks(s.items))
const filtered = computed(() => !!(s.stageFilter || s.statusFilter || s.projectFilter || s.keyword.trim()))
const pageCount = computed(() => Math.max(1, Math.ceil(s.total / s.pageSize)))
const focusItems = computed(() => s.attentionItems)

const projectOptions = computed(() =>
  store.state.projects.map(p => ({ id: p.id, label: p.name || shortPath(p.root, 28), count: p.taskCount, root: p.root })))

/** 列表行委派徽标（FR-3.1）：有批量实时数据用 agent 状态，缺失按阶段兜底；未委派返回 null。 */
const delegationBadges = computed(() => {
  const map = new Map<number, { label: string; type: TagType; agent: string }>()
  for (const task of visibleItems.value) {
    if (!task.agentTaskKey) continue
    const live = s.agentStatuses[task.id]
    const meta = live ? agentStatusMeta(live.status) : agentFallbackByStage(task.stage)
    map.set(task.id, { ...meta, agent: agentNameOrFallback(live?.agentName, task.agentId) })
  }
  return map
})

function shortKey(key: string): string {
  return key.length > 8 ? `${key.slice(0, 8)}…` : key
}

async function reload(): Promise<void> {
  await store.refreshViews()
}

onMounted(async () => {
  void store.loadProjects()
  await reload()
  store.startAgentStatusPolling()
  try {
    version.value = await fetchPluginVersion('todo-tracker')
  } catch {
    version.value = '版本未知'
  }
})

onUnmounted(() => store.stopAgentStatusPolling())

// 过滤条件变了就回第 1 页再取（沿用 §3.4 交互要求 5：筛选变化回第 1 页）
watch(() => [s.stageFilter, s.statusFilter, s.projectFilter, s.priorityFilter, s.missingFilter], () => {
  s.page = 1
  void store.refreshViews()
})

let keywordTimer: ReturnType<typeof setTimeout> | undefined
watch(() => s.keyword, () => {
  clearTimeout(keywordTimer)
  keywordTimer = setTimeout(() => {
    s.page = 1
    void store.refreshViews()
  }, 300)
})

function page(target: number): void {
  if (target < 1 || target > pageCount.value || target === s.page) return
  s.page = target
  void reload()
}

async function create(): Promise<void> {
  const title = newTitle.value.trim()
  if (!title) return
  const created = await store.createTodo(title, newProjectId.value || undefined)
  if (created) {
    newTitle.value = ''
    newProjectId.value = 0
  }
}

function open(task: TodoItem): void {
  void store.selectTodo(task.id)
}

async function moveStage(task: TodoItem, target: TodoStage): Promise<void> {
  let reason: string | undefined
  if (target === 'Blocked') {
    reason = window.prompt('请填写阻塞原因')?.trim()
    if (!reason) return
  }
  if (target === 'Done' || target === 'Cancelled') {
    const accepted = await confirmAction({
      title: target === 'Done' ? '确认完成任务' : '确认取消任务',
      message: `将「${task.title}」流转到「${stageLabel(target)}」？`,
      confirmText: target === 'Done' ? '确认完成' : '确认取消',
      danger: target === 'Cancelled'
    })
    if (!accepted) return
  }
  try {
    await changeStage(task.id, target, reason, reason)
    showToast(`已流转到${stageLabel(target)}`, 'success')
    await store.refreshViews()
  } catch (error) {
    showToast(error instanceof Error ? error.message : String(error), 'error')
  }
}

function beginDrag(task: TodoItem, event: DragEvent): void {
  if (!event.dataTransfer) return
  draggingTask.value = task
  event.dataTransfer.setData('text/plain', String(task.id))
  event.dataTransfer.effectAllowed = 'move'
}

async function dropStage(stage: TodoStage, event: DragEvent): Promise<void> {
  const id = Number(event.dataTransfer?.getData('text/plain'))
  const task = visibleItems.value.find(item => item.id === id)
    ?? Object.values(s.boardBuckets).flatMap(bucket => bucket.items).find(item => item.id === id)
  if (task?.allowedTargets.includes(stage)) await moveStage(task, stage)
  draggingTask.value = null
}

function onShortcut(event: KeyboardEvent): void {
  const isEditing = ['INPUT', 'TEXTAREA', 'SELECT'].includes((event.target as HTMLElement)?.tagName ?? '')
  if (event.key === 'Escape' && s.selectedId) {
    store.closeDetail()
  } else if (event.key === '/' && !isEditing) {
    event.preventDefault()
    document.querySelector<HTMLInputElement>('.tt-search')?.focus()
  }
  if (event.key.toLowerCase() === 'n' && !isEditing) {
    document.querySelector<HTMLInputElement>('.tt-create-input')?.focus()
  }
}

onMounted(() => window.addEventListener('keydown', onShortcut))
onUnmounted(() => window.removeEventListener('keydown', onShortcut))
</script>

<template>
  <div class="tt-root">
    <NotifyHost />

    <header class="tt-head">
      <div class="tt-head-left">
        <h1 class="tt-title">待办任务</h1>
        <span class="tt-version" :title="`插件版本 ${version}`">{{ version }}</span>
      </div>
      <div class="tt-create">
        <select v-model.number="newProjectId" class="tt-input tt-select tt-create-proj" aria-label="新建任务所属项目">
          <option :value="0">不选项目</option>
          <option v-for="p in projectOptions" :key="p.id" :value="p.id" :title="`${p.root}`">{{ p.label }} · {{ shortPath(p.root, 30) }}（{{ p.count }} 任务）</option>
        </select>
        <input v-model="newTitle" class="tt-input tt-create-input" maxlength="200"
          placeholder="新任务标题（回车创建）" @keyup.enter="create">
        <button type="button" class="tt-btn is-primary" :disabled="!newTitle.trim()" @click="create">新建</button>
      </div>
    </header>

    <nav class="tt-pipeline" aria-label="任务阶段">
      <button type="button" class="tt-pipeline-step" :class="{ 'is-on': !s.stageFilter }" @click="s.stageFilter = ''">
        <span>全部</span><b>{{ Object.values(s.stageCounts).reduce((sum, count) => sum + count, 0) }}</b>
      </button>
      <button v-for="st in STAGES" :key="st" type="button" class="tt-pipeline-step"
        :class="{ 'is-on': s.stageFilter === st }" @click="s.stageFilter = s.stageFilter === st ? '' : st">
        <span>{{ STAGE_LABELS[st] }}</span><b>{{ s.stageCounts[st] ?? '—' }}</b>
      </button>
    </nav>

    <section class="tt-filters" aria-label="过滤">
      <div class="tt-filter-right">
        <select v-model.number="s.projectFilter" class="tt-input tt-select" aria-label="项目过滤">
          <option :value="0">全部项目</option>
          <option v-for="p in projectOptions" :key="p.id" :value="p.id" :title="`${p.root}`">{{ p.label }} · {{ shortPath(p.root, 30) }}（{{ p.count }} 任务）</option>
        </select>
        <select v-model="s.statusFilter" class="tt-input tt-select" aria-label="完成状态过滤">
          <option value="">不限完成态</option>
          <option value="Pending">未完成</option>
          <option value="Completed">已完成</option>
        </select>
        <select v-model.number="s.priorityFilter" class="tt-input tt-select" aria-label="优先级过滤">
          <option :value="0">全部优先级</option><option :value="1">P1</option><option :value="2">P2</option><option :value="3">P3</option>
        </select>
        <label class="tt-gap-filter"><input v-model="s.missingFilter" type="checkbox"> 只看待补齐</label>
        <input v-model="s.keyword" class="tt-input tt-search" placeholder="搜索标题/备注/目标">
        <div class="tt-view-switch" role="group" aria-label="视图切换">
          <button type="button" :class="{ 'is-on': s.view === 'list' }" @click="s.view = 'list'; void store.refreshViews()">列表</button>
          <button type="button" :class="{ 'is-on': s.view === 'board' }" @click="s.view = 'board'; void store.refreshViews()">看板</button>
        </div>
      </div>
    </section>

    <section class="tt-focus" aria-label="待处理关注项">
      <button type="button" class="tt-focus-card" @click="s.stageFilter = 'Review'">
        <span>待验收</span><b>{{ s.stageCounts.Review ?? 0 }}</b>
      </button>
      <button type="button" class="tt-focus-card is-blocked" @click="s.stageFilter = 'Blocked'">
        <span>阻塞待处理</span><b>{{ s.stageCounts.Blocked ?? 0 }}</b>
      </button>
      <button type="button" class="tt-focus-card is-gap" @click="s.missingFilter = !s.missingFilter">
        <span>待补齐</span><b>{{ s.missingCount }}</b>
      </button>
      <div v-if="focusItems.length" class="tt-focus-list">
        <button v-for="task in focusItems.slice(0, 3)" :key="task.id" type="button" @click="open(task)">
          {{ task.title }} · {{ task.stageLabel }} · {{ priorityLabel(task.priority) }}
        </button>
      </div>
    </section>

    <section class="tt-summary">
      <span>共 <b>{{ s.total }}</b> 条</span>
      <span v-if="s.stageFilter">阶段「{{ STAGE_LABELS[s.stageFilter] }}」</span>
      <span v-if="filtered" class="tt-dim">（筛选中）</span>
      <button type="button" class="tt-btn tt-btn-sm" :disabled="s.loading" @click="reload">
        {{ s.loading ? '加载中…' : '刷新' }}
      </button>
    </section>

    <main class="tt-body">
      <div v-if="s.view === 'list'" class="tt-list-panel">
        <div v-if="!s.items.length && !s.loading" class="tt-empty">
          <template v-if="filtered">
            <p class="tt-empty-title">筛选没有匹配的任务</p>
            <p class="tt-empty-tip">换个阶段/项目，或清空搜索词再试。</p>
          </template>
          <template v-else>
            <p class="tt-empty-title">还没有任务</p>
            <p class="tt-empty-tip">在上方输入标题新建一条；要下发给 agent，还需补齐
              <b>可验证目标 / 任务正文 / 验收判据 / 验证命令</b> 四栏。</p>
          </template>
        </div>

        <ul v-else class="tt-list">
          <li v-for="task in visibleItems" :key="task.id" class="tt-item" draggable="true"
            :class="{ 'is-active': task.id === s.selectedId }" tabindex="0"
            @dragstart="beginDrag(task, $event)" @click="open(task)" @keyup.enter="open(task)">
            <div class="tt-item-top">
              <span class="tt-stage" :class="`tag-${stageTagType(task.stage)}`">{{ task.stageLabel }}</span>
              <span class="tt-prio" :title="`优先级 ${priorityLabel(task.priority)}`">{{ priorityLabel(task.priority) }}</span>
              <span class="tt-item-title" :title="task.title">{{ task.title }}</span>
            </div>
            <div class="tt-item-meta">
              <span v-if="task.projectName || task.projectRoot" class="tt-proj" :title="task.projectRoot">
                {{ task.projectName || shortPath(task.projectRoot, 34) }}
              </span>
              <span v-else class="tt-proj is-missing">未关联项目</span>
              <span v-if="task.agentTaskKey" class="tt-deleg"
                :class="`tag-${delegationBadges.get(task.id)?.type ?? 'info'}`"
                :title="`委派给 ${delegationBadges.get(task.id)?.agent ?? 'agent'} · 编号 ${task.agentTaskKey}（实时以详情页为准）`">
                {{ delegationBadges.get(task.id)?.label ?? '委派中' }} · {{ delegationBadges.get(task.id)?.agent ?? 'agent' }}
              </span>
              <span v-if="task.agentTaskKey" class="tt-dim tt-key" :title="`委派编号 ${task.agentTaskKey}`">
                #{{ shortKey(task.agentTaskKey) }}
              </span>
              <span v-if="task.assignee" class="tt-dim">→ {{ task.assignee }}</span>
              <span v-if="task.recordCount" class="tt-dim">记录 {{ task.recordCount }}</span>
              <span class="tt-dim">{{ relativeTime(task.updatedAt) }}</span>
            </div>
            <div v-if="task.missing?.length" class="tt-item-gap">还缺：{{ missingLabels(task.missing) }}</div>
            <div v-if="task.objective" class="tt-item-obj" :title="task.objective">{{ task.objective }}</div>
            <div class="tt-item-actions" @click.stop>
              <select class="tt-input tt-select" aria-label="流转阶段" :value="''" @change="moveStage(task, ($event.target as HTMLSelectElement).value as TodoStage)">
                <option value="">流转到…</option><option v-for="target in task.allowedTargets" :key="target" :value="target">{{ stageLabel(target) }}</option>
              </select>
            </div>
          </li>
        </ul>

        <nav v-if="pageCount > 1" class="tt-pager" aria-label="分页">
          <button type="button" class="tt-btn tt-btn-sm" :disabled="s.page <= 1" @click="page(s.page - 1)">上一页</button>
          <span class="tt-dim">{{ s.page }} / {{ pageCount }}</span>
          <button type="button" class="tt-btn tt-btn-sm" :disabled="s.page >= pageCount" @click="page(s.page + 1)">下一页</button>
        </nav>
      </div>

      <div v-else class="tt-board" aria-label="任务看板">
        <section v-for="stage in (s.stageFilter ? [s.stageFilter as TodoStage] : STAGES)" :key="stage" class="tt-board-column"
          :class="{ 'is-drop-valid': draggingTask?.allowedTargets.includes(stage) }"
          @dragover.prevent @drop.prevent="dropStage(stage, $event)">
          <header><b>{{ STAGE_LABELS[stage] }}</b><span>{{ s.boardBuckets[stage]?.total ?? 0 }}</span></header>
          <p v-if="s.boardBuckets[stage]?.error" class="tt-missing">{{ s.boardBuckets[stage].error }}</p>
          <button v-for="task in s.boardBuckets[stage]?.items ?? []" :key="task.id" class="tt-board-card" draggable="true"
            :class="{ 'is-active': task.id === s.selectedId }" @dragstart="beginDrag(task, $event)" @dragend="draggingTask = null" @click="open(task)">
            <span class="tt-stage" :class="`tag-${stageTagType(task.stage)}`">{{ task.stageLabel }}</span>
            <b>{{ task.title }}</b><small>{{ priorityLabel(task.priority) }} · {{ task.projectName || '未关联项目' }}</small>
            <span v-if="task.missing?.length" class="tt-item-gap">还缺：{{ missingLabels(task.missing) }}</span>
          </button>
          <button v-if="(s.boardBuckets[stage]?.items.length ?? 0) < (s.boardBuckets[stage]?.total ?? 0)"
            class="tt-btn tt-btn-sm" :disabled="s.boardBuckets[stage]?.loading" @click="store.loadBoardStage(stage)">
            {{ s.boardBuckets[stage]?.loading ? '加载中…' : '加载更多' }}
          </button>
        </section>
      </div>

      <aside class="tt-detail-panel">
        <TaskDetail v-if="s.selected" :key="s.selected.id" :task="s.selected" />
        <div v-else class="tt-empty tt-empty-side">
          <p class="tt-empty-title">未选中任务</p>
          <p class="tt-empty-tip">左侧点一条任务，可编辑内容、关联项目、导入工件、下发给 agent，并查看执行记录。</p>
        </div>
      </aside>
    </main>
  </div>
</template>

<style scoped>
.tt-root {
  height: 100%;
  display: flex;
  flex-direction: column;
  padding: 20px 24px 0;
  box-sizing: border-box;
  color: var(--el-text-color-primary);
  background: var(--el-bg-color-page);
}

/* 滚动容器的直接子区块必须不许被压扁（plugin-development 铁律 8） */
.tt-body { flex: 1; min-height: 0; overflow: auto; display: flex; gap: 16px; padding-bottom: 16px; }
.tt-body > * { flex-shrink: 0; }
/* PILOT-057A：列表稳定 400px，详情吃满剩余主区（原 520px 固定侧边栏太挤） */
.tt-list-panel { flex: 0 0 400px; min-width: 320px; display: flex; flex-direction: column; gap: 8px; }
.tt-board { display: flex; align-items: flex-start; gap: 10px; min-width: 0; flex: 1 1 auto; overflow-x: auto; }
.tt-board-column { flex: 0 0 230px; min-height: 100%; display: flex; flex-direction: column; gap: 8px; padding: 10px; background: var(--el-fill-color-lighter); border-radius: var(--el-border-radius-base, 4px); }
.tt-board-column.is-drop-valid { outline: 2px solid var(--el-color-primary); outline-offset: -2px; background: var(--el-color-primary-light-9); }
.tt-board-column > header { display: flex; justify-content: space-between; align-items: center; color: var(--el-text-color-primary); }
.tt-board-card { display: flex; flex-direction: column; align-items: flex-start; gap: 6px; padding: 10px; border: 1px solid var(--el-border-color-lighter); border-radius: var(--el-border-radius-base, 4px); background: var(--el-bg-color); color: var(--el-text-color-primary); text-align: left; cursor: pointer; }
.tt-board-card.is-active { border-color: var(--el-color-primary); }
.tt-board-card small { color: var(--el-text-color-secondary); }
.tt-detail-panel { flex: 1 1 auto; min-width: 0; max-width: none; }

.tt-head { display: flex; align-items: center; justify-content: space-between; gap: 12px; margin-bottom: 12px; flex-shrink: 0; }
.tt-head-left { display: flex; align-items: baseline; gap: 8px; }
.tt-title { margin: 0; font-size: 21px; font-weight: 700; }
.tt-version {
  font-size: 11px;
  padding: 2px 7px;
  border-radius: 999px;
  background: var(--el-fill-color-light);
  color: var(--el-text-color-secondary);
}
.tt-create { display: flex; gap: 8px; align-items: center; }
.tt-create-input { width: min(360px, 40vw); }

.tt-filters {
  display: flex; flex-wrap: wrap; gap: 10px; align-items: center; justify-content: space-between;
  padding: 10px 12px; margin-bottom: 8px; flex-shrink: 0;
  background: var(--el-bg-color); border: 1px solid var(--el-border-color-lighter);
  border-radius: var(--el-border-radius-base, 4px);
}
.tt-filter-right { display: flex; gap: 8px; align-items: center; flex-wrap: wrap; }
.tt-pipeline { display: flex; gap: 5px; overflow-x: auto; padding: 2px 0 10px; flex-shrink: 0; }
.tt-pipeline-step { display: flex; justify-content: space-between; align-items: center; gap: 12px; min-width: 92px; padding: 7px 10px; border: 1px solid var(--el-border-color-lighter); border-radius: var(--el-border-radius-base, 4px); background: var(--el-bg-color); color: var(--el-text-color-regular); cursor: pointer; }
.tt-pipeline-step b { color: var(--el-text-color-secondary); }
.tt-pipeline-step.is-on { border-color: var(--el-color-primary); color: var(--el-color-primary); background: var(--el-color-primary-light-9); }
.tt-focus { display: flex; align-items: stretch; gap: 8px; margin-bottom: 8px; flex-shrink: 0; }
.tt-focus-card { display: flex; align-items: center; gap: 10px; padding: 7px 11px; border: 1px solid var(--el-border-color-lighter); border-radius: var(--el-border-radius-base, 4px); background: var(--el-bg-color); color: var(--el-text-color-regular); cursor: pointer; }
.tt-focus-card b { font-size: 16px; }
.tt-focus-card.is-blocked { color: var(--el-color-danger); }
.tt-focus-card.is-gap { color: var(--el-color-warning); }
.tt-focus-list { display: flex; gap: 6px; overflow: hidden; }
.tt-focus-list button { border: 0; background: transparent; color: var(--el-color-primary); cursor: pointer; white-space: nowrap; }
.tt-gap-filter { display: flex; align-items: center; gap: 4px; font-size: 12px; color: var(--el-text-color-regular); white-space: nowrap; }
.tt-view-switch { display: inline-flex; border: 1px solid var(--el-border-color); border-radius: var(--el-border-radius-base, 4px); overflow: hidden; }
.tt-view-switch button { border: 0; padding: 6px 10px; background: var(--el-bg-color); color: var(--el-text-color-regular); cursor: pointer; }
.tt-view-switch button.is-on { background: var(--el-color-primary); color: var(--el-color-white); }

.tt-summary { display: flex; align-items: center; gap: 10px; font-size: 12px; color: var(--el-text-color-regular); margin-bottom: 6px; flex-shrink: 0; }

.tt-list { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 8px; }
.tt-item {
  background: var(--el-bg-color); border: 1px solid var(--el-border-color-lighter);
  border-radius: var(--el-border-radius-base, 4px); padding: 10px 12px; cursor: pointer;
}
.tt-item:hover { border-color: var(--el-color-primary-light-5); }
.tt-item.is-active { border-color: var(--el-color-primary); box-shadow: 0 0 0 1px var(--el-color-primary-light-7) inset; }
.tt-item-top { display: flex; align-items: center; gap: 8px; min-width: 0; }
.tt-item-title { font-weight: 600; font-size: 14px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.tt-item-meta { display: flex; flex-wrap: wrap; gap: 8px; margin-top: 5px; font-size: 12px; color: var(--el-text-color-secondary); }
.tt-proj { color: var(--el-color-primary); }
.tt-proj.is-missing { color: var(--el-color-warning); }
.tt-create-proj { max-width: 200px; }
.tt-deleg { font-size: 11px; padding: 1px 7px; border-radius: 3px; }
.tt-key { font-size: 11px; }
.tt-item-gap { margin-top: 5px; font-size: 12px; color: var(--el-color-danger); }
.tt-item-obj { margin-top: 4px; font-size: 12px; color: var(--el-text-color-regular); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.tt-item-actions { margin-top: 6px; }

.tt-stage { font-size: 11px; padding: 1px 7px; border-radius: 3px; flex-shrink: 0; }
.tt-prio { font-size: 11px; padding: 1px 5px; border-radius: 3px; background: var(--el-fill-color-light); color: var(--el-text-color-secondary); flex-shrink: 0; }
.tag-info { background: var(--el-fill-color-light); color: var(--el-text-color-secondary); }
.tag-primary { background: var(--el-color-primary-light-9); color: var(--el-color-primary); }
.tag-success { background: var(--el-color-success-light-9); color: var(--el-color-success); }
.tag-warning { background: var(--el-color-warning-light-9); color: var(--el-color-warning); }
.tag-danger { background: var(--el-color-danger-light-9); color: var(--el-color-danger); }

.tt-empty { padding: 26px 18px; text-align: center; color: var(--el-text-color-secondary); font-size: 13px; line-height: 1.8; }
.tt-empty-side { background: var(--el-bg-color); border: 1px dashed var(--el-border-color); border-radius: var(--el-border-radius-base, 4px); }
.tt-empty-title { margin: 0 0 4px; font-size: 14px; color: var(--el-text-color-regular); }
.tt-empty-tip { margin: 0; font-size: 12px; }

.tt-pager { display: flex; align-items: center; gap: 10px; justify-content: center; padding: 10px 0 2px; }

.tt-input {
  border: 1px solid var(--el-border-color); background: var(--el-bg-color);
  color: var(--el-text-color-primary); border-radius: var(--el-border-radius-base, 4px);
  padding: 5px 9px; font-size: 13px; line-height: 1.5;
}
.tt-select { max-width: 190px; }
.tt-search { width: 190px; }
.tt-btn {
  border: 1px solid var(--el-border-color); background: var(--el-bg-color);
  color: var(--el-text-color-regular); border-radius: var(--el-border-radius-base, 4px);
  padding: 5px 12px; font-size: 13px; cursor: pointer;
}
.tt-btn:disabled { opacity: .55; cursor: not-allowed; }
.tt-btn-sm { padding: 2px 9px; font-size: 12px; }
.tt-btn.is-primary { background: var(--el-color-primary); border-color: var(--el-color-primary); color: var(--el-color-white); }
.tt-dim { color: var(--el-text-color-secondary); }

@media (max-width: 1100px) {
  .tt-body { flex-direction: column; }
  .tt-detail-panel { flex: 0 0 auto; max-width: none; width: 100%; }
}
</style>
