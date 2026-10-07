<script setup lang="ts">
/**
 * todo-tracker 主界面：任务台账（可下发给 agent 的工作单元）。
 *
 * 版式：左列 = 过滤条 + 任务列表 + 分页；右列 = 选中任务的详情/执行记录（TaskDetail）。
 * 三条要求：标题旁版本徽标（铁律 13）、滚动容器子区块 flex-shrink:0（铁律 8）、
 * 空态分级（无任务 / 筛选无匹配 / 未关联项目）文案各不相同（§3.4 交互要求 4）。
 */
import { computed, onMounted, ref, watch } from 'vue'
import NotifyHost from './NotifyHost.vue'
import TaskDetail from './components/TaskDetail.vue'
import * as store from './store'
import { fetchPluginVersion } from './http'
import { missingLabels, priorityLabel, relativeTime, shortPath, sortTasks, stageLabel, stageTagType } from './actions'
import type { TodoItem, TodoStage } from './types'

const STAGES: TodoStage[] = ['Draft', 'Ready', 'Dispatched', 'Running', 'Blocked', 'Review', 'Done', 'Cancelled']
const STAGE_LABELS: Record<string, string> = Object.fromEntries(STAGES.map(s => [s, stageLabel(s)]))

const newTitle = ref('')
const version = ref('加载中…')
const s = store.state

const visibleItems = computed(() => sortTasks(s.items))
const filtered = computed(() => !!(s.stageFilter || s.statusFilter || s.projectFilter || s.keyword.trim()))
const pageCount = computed(() => Math.max(1, Math.ceil(s.total / s.pageSize)))

const projectOptions = computed(() =>
  store.state.projects.map(p => ({ id: p.id, label: p.name || shortPath(p.root, 28), count: p.taskCount })))

async function reload(): Promise<void> {
  await store.loadTodos()
}

onMounted(async () => {
  void store.loadProjects()
  await reload()
  try {
    version.value = await fetchPluginVersion('todo-tracker')
  } catch {
    version.value = '版本未知'
  }
})

// 过滤条件变了就回第 1 页再取（沿用 §3.4 交互要求 5：筛选变化回第 1 页）
watch(() => [s.stageFilter, s.statusFilter, s.projectFilter], () => {
  s.page = 1
  void reload()
})

let keywordTimer: ReturnType<typeof setTimeout> | undefined
watch(() => s.keyword, () => {
  clearTimeout(keywordTimer)
  keywordTimer = setTimeout(() => {
    s.page = 1
    void reload()
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
  const created = await store.createTodo(title)
  if (created) newTitle.value = ''
}

function open(task: TodoItem): void {
  void store.selectTodo(task.id)
}
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
        <input v-model="newTitle" class="tt-input tt-create-input" maxlength="200"
          placeholder="新任务标题（回车创建）" @keyup.enter="create">
        <button type="button" class="tt-btn is-primary" :disabled="!newTitle.trim()" @click="create">新建</button>
      </div>
    </header>

    <section class="tt-filters" aria-label="过滤">
      <div class="tt-chips" role="group" aria-label="阶段过滤">
        <button type="button" class="tt-chip" :class="{ 'is-on': !s.stageFilter }" @click="s.stageFilter = ''">全部</button>
        <button v-for="st in STAGES" :key="st" type="button" class="tt-chip"
          :class="{ 'is-on': s.stageFilter === st }" @click="s.stageFilter = s.stageFilter === st ? '' : st">
          {{ STAGE_LABELS[st] }}
        </button>
      </div>
      <div class="tt-filter-right">
        <select v-model.number="s.projectFilter" class="tt-input tt-select" aria-label="项目过滤">
          <option :value="0">全部项目</option>
          <option v-for="p in projectOptions" :key="p.id" :value="p.id">{{ p.label }}（{{ p.count }}）</option>
        </select>
        <select v-model="s.statusFilter" class="tt-input tt-select" aria-label="完成状态过滤">
          <option value="">不限完成态</option>
          <option value="Pending">未完成</option>
          <option value="Completed">已完成</option>
        </select>
        <input v-model="s.keyword" class="tt-input tt-search" placeholder="搜索标题/备注/目标">
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
      <div class="tt-list-panel">
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
          <li v-for="task in visibleItems" :key="task.id" class="tt-item"
            :class="{ 'is-active': task.id === s.selectedId }" tabindex="0"
            @click="open(task)" @keyup.enter="open(task)">
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
              <span v-if="task.assignee" class="tt-dim">→ {{ task.assignee }}</span>
              <span v-if="task.recordCount" class="tt-dim">记录 {{ task.recordCount }}</span>
              <span class="tt-dim">{{ relativeTime(task.updatedAt) }}</span>
            </div>
            <div v-if="task.missing?.length" class="tt-item-gap">还缺：{{ missingLabels(task.missing) }}</div>
            <div v-if="task.objective" class="tt-item-obj" :title="task.objective">{{ task.objective }}</div>
          </li>
        </ul>

        <nav v-if="pageCount > 1" class="tt-pager" aria-label="分页">
          <button type="button" class="tt-btn tt-btn-sm" :disabled="s.page <= 1" @click="page(s.page - 1)">上一页</button>
          <span class="tt-dim">{{ s.page }} / {{ pageCount }}</span>
          <button type="button" class="tt-btn tt-btn-sm" :disabled="s.page >= pageCount" @click="page(s.page + 1)">下一页</button>
        </nav>
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
.tt-list-panel { flex: 1 1 auto; min-width: 320px; display: flex; flex-direction: column; gap: 8px; }
.tt-detail-panel { flex: 0 0 520px; max-width: 520px; }

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
.tt-chips { display: flex; flex-wrap: wrap; gap: 6px; }
.tt-chip {
  border: 1px solid var(--el-border-color-lighter); background: var(--el-bg-color);
  color: var(--el-text-color-regular); border-radius: 999px; padding: 3px 11px; font-size: 12px; cursor: pointer;
}
.tt-chip.is-on { background: var(--el-color-primary); border-color: var(--el-color-primary); color: #fff; }
.tt-filter-right { display: flex; gap: 8px; align-items: center; flex-wrap: wrap; }

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
.tt-item-gap { margin-top: 5px; font-size: 12px; color: var(--el-color-danger); }
.tt-item-obj { margin-top: 4px; font-size: 12px; color: var(--el-text-color-regular); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }

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
.tt-btn.is-primary { background: var(--el-color-primary); border-color: var(--el-color-primary); color: #fff; }
.tt-dim { color: var(--el-text-color-secondary); }

@media (max-width: 1100px) {
  .tt-body { flex-direction: column; }
  .tt-detail-panel { flex: 0 0 auto; max-width: none; width: 100%; }
}
</style>
