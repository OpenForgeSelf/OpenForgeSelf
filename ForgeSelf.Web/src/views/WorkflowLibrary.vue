<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import PageHead from '@/components/PageHead.vue'
import ForgeCard from '@/components/ForgeCard.vue'
import EmptyState from '@/components/EmptyState.vue'
import { useWorkflowStore } from '@/stores/workflow'
import type { WorkflowDefinition } from '@/types/workflow'
import WorkflowEditor from '@/components/workflow/WorkflowEditor.vue'
import WorkflowExecution from '@/components/workflow/WorkflowExecution.vue'
import AIWorkflowGenerator from '@/components/workflow/AIWorkflowGenerator.vue'

type FilterTab = 'all' | 'running' | 'completed' | 'paused' | 'scheduled'

interface StepDisplay {
  id: string
  name: string
  status: 'completed' | 'current' | 'pending'
}

const workflowStore = useWorkflowStore()

const searchKeyword = ref('')
const activeFilter = ref<FilterTab>('all')
const debounceTimer = ref<number | null>(null)

const showEditor = ref(false)
const showGenerator = ref(false)
const showExecution = ref(false)
const editingWorkflow = ref<WorkflowDefinition | null>(null)
const currentExecutionId = ref<string | null>(null)

const filterTabs: { key: FilterTab; label: string }[] = [
  { key: 'all', label: '全部' },
  { key: 'running', label: '运行中' },
  { key: 'completed', label: '已完成' },
  { key: 'paused', label: '已暂停' },
  { key: 'scheduled', label: '定时' }
]

const filteredWorkflows = computed(() => {
  let result = [...workflowStore.workflows]

  if (activeFilter.value !== 'all') {
    if (activeFilter.value === 'scheduled') {
      result = result.filter(w => w.status === 'ready')
    } else {
      result = result.filter(w => w.status === activeFilter.value)
    }
  }

  if (searchKeyword.value.trim()) {
    const keyword = searchKeyword.value.toLowerCase().trim()
    result = result.filter(w =>
      w.name.toLowerCase().includes(keyword) ||
      w.description?.toLowerCase().includes(keyword)
    )
  }

  return result
})

function getStepDisplay(workflow: WorkflowDefinition): StepDisplay[] {
  const total = workflow.steps.length
  return workflow.steps.map((step, index) => {
    if (workflow.status === 'completed') {
      return { id: step.id, name: step.name, status: 'completed' as const }
    }
    if (workflow.status === 'running') {
      const currentIdx = Math.min(Math.floor(total / 2), total - 1)
      if (index < currentIdx) return { id: step.id, name: step.name, status: 'completed' as const }
      if (index === currentIdx) return { id: step.id, name: step.name, status: 'current' as const }
      return { id: step.id, name: step.name, status: 'pending' as const }
    }
    return { id: step.id, name: step.name, status: 'pending' as const }
  })
}

function getStatusLabel(workflow: WorkflowDefinition): string {
  switch (workflow.status) {
    case 'completed': return '已完成'
    case 'running': return '进行中'
    case 'paused': return '已暂停'
    case 'ready': return '定时'
    case 'draft': return '草稿'
    case 'failed': return '失败'
    case 'cancelled': return '已取消'
    default: return workflow.status
  }
}

function getStatusColor(workflow: WorkflowDefinition): { text: string; bg: string; border: string } {
  switch (workflow.status) {
    case 'completed':
      return { text: 'var(--el-color-success)', bg: 'color-mix(in srgb, var(--bronze-500) 12%, transparent)', border: 'var(--el-color-success)' }
    case 'running':
      return { text: 'var(--el-color-primary)', bg: 'var(--el-color-primary-light-9)', border: 'var(--el-color-primary)' }
    case 'paused':
      return { text: 'var(--fg-secondary)', bg: 'var(--paper-tint)', border: 'var(--border-default)' }
    case 'ready':
      return { text: 'var(--el-color-info)', bg: 'color-mix(in srgb, var(--el-color-info) 12%, transparent)', border: 'var(--el-color-info)' }
    default:
      return { text: 'var(--fg-secondary)', bg: 'var(--paper-tint)', border: 'var(--border-default)' }
  }
}

function getLeftBorderColor(workflow: WorkflowDefinition): string {
  if (workflow.status === 'completed' || workflow.status === 'running') {
    return 'var(--gold-400)'
  }
  return 'var(--border-default)'
}

function getActionButton(workflow: WorkflowDefinition): { label: string; variant: 'run' | 'pause' | 'resume' } {
  if (workflow.status === 'running') return { label: '暂停', variant: 'pause' }
  if (workflow.status === 'paused') return { label: '继续', variant: 'resume' }
  return { label: '运行', variant: 'run' }
}

function getStepProgress(workflow: WorkflowDefinition): { current: number; total: number; pct: number } {
  const total = workflow.steps.length || 1
  let completed = 0
  if (workflow.status === 'completed') {
    completed = total
  } else if (workflow.status === 'running') {
    completed = Math.floor(total / 2)
  }
  return { current: completed, total: workflow.steps.length, pct: (completed / total) * 100 }
}

function formatDuration(workflow: WorkflowDefinition): string {
  if (workflow.status === 'completed') {
    return `${(workflow.steps.length * 0.7).toFixed(1)}s`
  }
  if (workflow.status === 'running') {
    return '进行中'
  }
  return `${(workflow.steps.length * 0.5).toFixed(1)}s`
}

function formatTimeInfo(workflow: WorkflowDefinition): string {
  if (workflow.lastExecutedAt) {
    const diff = Date.now() - workflow.lastExecutedAt.getTime()
    const mins = Math.floor(diff / 60000)
    if (mins < 1) return '刚刚'
    if (mins < 60) return `${mins} 分钟前`
    const hours = Math.floor(mins / 60)
    if (hours < 24) return `${hours} 小时前`
    const days = Math.floor(hours / 24)
    return `${days} 天前`
  }
  if (workflow.status === 'ready') {
    return '昨天 02:00'
  }
  return ''
}

function getStatusIconBg(workflow: WorkflowDefinition): string {
  const colors = getStatusColor(workflow)
  return colors.bg
}

function handleSearchInput(): void {
  if (debounceTimer.value) {
    clearTimeout(debounceTimer.value)
  }
  debounceTimer.value = window.setTimeout(() => {
    loadWorkflows()
  }, 300)
}

function handleFilterChange(tab: FilterTab): void {
  activeFilter.value = tab
}

async function loadWorkflows(): Promise<void> {
  const params: { keyword?: string } = {}
  if (searchKeyword.value.trim()) {
    params.keyword = searchKeyword.value.trim()
  }
  await workflowStore.loadWorkflows(params)
}

function handleCardClick(workflow: WorkflowDefinition): void {
  editingWorkflow.value = workflow
  showEditor.value = true
}

async function handleExecute(workflow: WorkflowDefinition): Promise<void> {
  try {
    const execution = await workflowStore.executeWorkflow(workflow.id)
    currentExecutionId.value = execution.id
    showExecution.value = true
  } catch (e) {
    console.error('执行工作流失败:', e)
  }
}

async function handlePauseResume(workflow: WorkflowDefinition): Promise<void> {
  try {
    if (workflow.status === 'running') {
      await workflowStore.pauseExecution(workflow.id)
      const idx = workflowStore.workflows.findIndex(w => w.id === workflow.id)
      if (idx !== -1) {
        workflowStore.workflows[idx].status = 'paused'
      }
    } else if (workflow.status === 'paused') {
      await workflowStore.resumeExecution(workflow.id)
      const idx = workflowStore.workflows.findIndex(w => w.id === workflow.id)
      if (idx !== -1) {
        workflowStore.workflows[idx].status = 'running'
      }
    } else {
      await handleExecute(workflow)
    }
  } catch (e) {
    console.error('操作工作流失败:', e)
  }
}

function handleCreateWorkflow(): void {
  editingWorkflow.value = null
  showEditor.value = true
}

function handleAIGenerate(): void {
  showGenerator.value = true
}

async function handleEditorSave(workflowData: Partial<WorkflowDefinition>): Promise<void> {
  try {
    if (editingWorkflow.value) {
      await workflowStore.updateWorkflow(editingWorkflow.value.id, workflowData)
    } else {
      await workflowStore.createWorkflow(workflowData as Omit<WorkflowDefinition, 'id' | 'createdAt' | 'updatedAt' | 'usageCount'>)
    }
    showEditor.value = false
    editingWorkflow.value = null
  } catch (e) {
    console.error('保存工作流失败:', e)
  }
}

function handleEditorCancel(): void {
  showEditor.value = false
  editingWorkflow.value = null
}

function handleGeneratorClose(): void {
  showGenerator.value = false
}

async function handleGeneratorConfirm(workflow: WorkflowDefinition): Promise<void> {
  try {
    await workflowStore.createWorkflow({
      name: workflow.name,
      description: workflow.description,
      category: workflow.category,
      icon: workflow.icon,
      steps: workflow.steps,
      variables: workflow.variables,
      isFavorite: false,
      status: 'draft'
    })
    showGenerator.value = false
  } catch (e) {
    console.error('保存AI生成的工作流失败:', e)
  }
}

function handleExecutionClose(): void {
  showExecution.value = false
  currentExecutionId.value = null
}

onMounted(() => {
  loadWorkflows()
})
</script>

<template>
  <div class="workflow-library">
    <div class="library-inner">
      <!-- ===== PAGE HEADER ===== -->
      <PageHead eyebrow="Workflow Engine · 工作流引擎" title="工作流 · 执行 · 调度">
        <template #actions>
          <div class="search-box">
            <svg
              class="search-icon"
              width="14"
              height="14"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
            >
              <circle cx="11" cy="11" r="8" />
              <line x1="21" y1="21" x2="16.65" y2="16.65" />
            </svg>
            <input
              v-model="searchKeyword"
              type="text"
              class="search-input"
              placeholder="搜索工作流..."
              aria-label="搜索工作流"
              @input="handleSearchInput"
            />
            <button
              v-if="searchKeyword"
              class="search-clear"
              aria-label="清除搜索"
              @click="searchKeyword = ''; loadWorkflows()"
            >
              <svg
                width="12"
                height="12"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                stroke-width="2"
                stroke-linecap="round"
                stroke-linejoin="round"
              >
                <line x1="18" y1="6" x2="6" y2="18" />
                <line x1="6" y1="6" x2="18" y2="18" />
              </svg>
            </button>
          </div>
          <button class="btn-create" @click="handleCreateWorkflow">
            <svg
              width="14"
              height="14"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
            >
              <line x1="12" y1="5" x2="12" y2="19" />
              <line x1="5" y1="12" x2="19" y2="12" />
            </svg>
            创建工作流
          </button>
        </template>
      </PageHead>

      <!-- ===== FILTER TABS ===== -->
      <nav class="filter-tabs" aria-label="工作流筛选">
        <button
          v-for="tab in filterTabs"
          :key="tab.key"
          class="filter-tab"
          :class="{ active: activeFilter === tab.key }"
          @click="handleFilterChange(tab.key)"
        >
          {{ tab.label }}
        </button>
      </nav>

      <!-- ===== WORKFLOW LIST ===== -->
      <div v-if="!workflowStore.isLoading" class="workflow-list">
        <ForgeCard
          v-for="workflow in filteredWorkflows"
          :key="workflow.id"
          interactive
          :padding="14"
          class="workflow-card"
          :style="{ borderLeftColor: getLeftBorderColor(workflow) }"
          @click="handleCardClick(workflow)"
        >
          <!-- Left: Status Icon -->
          <div class="card-status-icon" :style="{ background: getStatusIconBg(workflow) }">
            <!-- completed -->
            <svg
              v-if="workflow.status === 'completed'"
              width="18"
              height="18"
              viewBox="0 0 24 24"
              fill="none"
              stroke="var(--el-color-success)"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
            >
              <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14" />
              <polyline points="22 4 12 14.01 9 11.01" />
            </svg>
            <!-- running: animated dot -->
            <div v-else-if="workflow.status === 'running'" class="pulse-dot" :style="{ background: 'var(--gold-400)' }" />
            <!-- paused -->
            <svg
              v-else-if="workflow.status === 'paused'"
              width="16"
              height="16"
              viewBox="0 0 24 24"
              fill="none"
              stroke="var(--fg-secondary)"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
            >
              <rect
                x="6"
                y="4"
                width="4"
                height="16"
                rx="1"
              />
              <rect
                x="14"
                y="4"
                width="4"
                height="16"
                rx="1"
              />
            </svg>
            <!-- scheduled / ready -->
            <svg
              v-else-if="workflow.status === 'ready'"
              width="16"
              height="16"
              viewBox="0 0 24 24"
              fill="none"
              stroke="var(--el-color-info)"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
            >
              <circle cx="12" cy="12" r="10" />
              <polyline points="12 6 12 12 16 14" />
            </svg>
            <!-- default -->
            <svg
              v-else
              width="16"
              height="16"
              viewBox="0 0 24 24"
              fill="none"
              stroke="var(--fg-secondary)"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
            >
              <path d="M14.5 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7.5L14.5 2z" />
              <polyline points="14 2 14 8 20 8" />
            </svg>
          </div>

          <!-- Middle: Info -->
          <div class="card-info">
            <div class="card-title-row">
              <span class="card-name">{{ workflow.name }}</span>
              <span class="card-status-label" :style="{ color: getStatusColor(workflow).text }">
                {{ getStatusLabel(workflow) }}
              </span>
            </div>

            <!-- Steps: pills with chevrons (for completed/running) -->
            <div v-if="workflow.status === 'completed' || workflow.status === 'running'" class="card-steps">
              <template v-for="(step, sIdx) in getStepDisplay(workflow)" :key="step.id">
                <span
                  class="step-pill"
                  :class="{
                    'step-completed': step.status === 'completed',
                    'step-current': step.status === 'current',
                    'step-pending': step.status === 'pending'
                  }"
                  :style="step.status === 'current' ? { borderColor: 'var(--gold-400)', background: 'var(--gold-tint)' } : {}"
                >
                  <svg
                    v-if="step.status === 'completed'"
                    width="10"
                    height="10"
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    stroke-width="3"
                    stroke-linecap="round"
                    stroke-linejoin="round"
                  >
                    <polyline points="20 6 9 17 4 12" />
                  </svg>
                  <span v-else-if="step.status === 'current'" class="step-dot" :style="{ background: 'var(--gold-400)' }" />
                  {{ step.name }}
                </span>
                <svg
                  v-if="sIdx < workflow.steps.length - 1"
                  class="step-chevron"
                  width="12"
                  height="12"
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="var(--fg-tertiary)"
                  stroke-width="1.5"
                  stroke-linecap="round"
                  stroke-linejoin="round"
                >
                  <polyline points="9 18 15 12 9 6" />
                </svg>
              </template>
            </div>

            <!-- Steps: progress bar (for paused/ready/other) -->
            <div v-else class="card-progress">
              <div class="progress-track">
                <div
                  class="progress-fill"
                  :style="{
                    width: getStepProgress(workflow).pct + '%',
                    background: workflow.status === 'ready' ? 'var(--el-color-info)' : 'var(--fg-tertiary)'
                  }"
                />
              </div>
              <span class="progress-text">
                {{ getStepProgress(workflow).current }}/{{ getStepProgress(workflow).total }} 步骤
              </span>
              <span
                v-if="workflow.status === 'ready'"
                class="cron-badge"
              >
                <svg
                  width="10"
                  height="10"
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  stroke-width="2"
                  stroke-linecap="round"
                  stroke-linejoin="round"
                >
                  <circle cx="12" cy="12" r="10" />
                  <polyline points="12 6 12 12 16 14" />
                </svg>
                0 2 * * *
              </span>
            </div>
          </div>

          <!-- Right: Meta + Actions -->
          <div class="card-meta">
            <div class="meta-info">
              <span
                class="meta-duration"
                :style="{ color: workflow.status === 'running' ? 'var(--gold-400)' : 'var(--fg-secondary)' }"
              >{{ formatDuration(workflow) }}</span>
              <span v-if="formatTimeInfo(workflow)" class="meta-time">{{ formatTimeInfo(workflow) }}</span>
            </div>
            <div class="card-actions">
              <button class="btn-ghost" @click.stop="handleCardClick(workflow)">查看</button>
              <button
                class="btn-ghost"
                :class="{
                  'btn-action-run': getActionButton(workflow).variant === 'run',
                  'btn-action-pause': getActionButton(workflow).variant === 'pause',
                  'btn-action-resume': getActionButton(workflow).variant === 'resume'
                }"
                :style="getActionButton(workflow).variant === 'run' ? {} :
                  getActionButton(workflow).variant === 'pause' ? { color: 'var(--gold-400)', borderColor: 'var(--border-brand)' } :
                  { color: 'var(--el-color-info)', borderColor: 'color-mix(in srgb, var(--el-color-info) 30%, transparent)' }"
                @click.stop="handlePauseResume(workflow)"
              >
                {{ getActionButton(workflow).label }}
              </button>
            </div>
          </div>
        </ForgeCard>

        <!-- Empty state -->
        <EmptyState
          v-if="filteredWorkflows.length === 0"
          title="没有找到工作流"
          :sub="searchKeyword ? '试试调整搜索条件' : '还没有工作流，点击创建开始'"
        >
          <template #icon>
            <svg
              width="28"
              height="28"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="1.5"
              stroke-linecap="round"
              stroke-linejoin="round"
            >
              <rect
                x="3"
                y="3"
                width="7"
                height="7"
                rx="1.5"
              />
              <rect
                x="14"
                y="3"
                width="7"
                height="7"
                rx="1.5"
              />
              <rect
                x="3"
                y="14"
                width="7"
                height="7"
                rx="1.5"
              />
              <rect
                x="14"
                y="14"
                width="7"
                height="7"
                rx="1.5"
              />
              <line x1="10" y1="6.5" x2="14" y2="6.5" />
              <line x1="6.5" y1="10" x2="6.5" y2="14" />
            </svg>
          </template>
          <template #action>
            <div class="empty-actions">
              <button class="btn-create" @click="handleCreateWorkflow">
                <svg
                  width="14"
                  height="14"
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  stroke-width="2"
                  stroke-linecap="round"
                  stroke-linejoin="round"
                >
                  <line x1="12" y1="5" x2="12" y2="19" />
                  <line x1="5" y1="12" x2="19" y2="12" />
                </svg>
                创建工作流
              </button>
              <button class="btn-ghost btn-ai-gen" @click="handleAIGenerate">
                <svg
                  width="14"
                  height="14"
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  stroke-width="2"
                  stroke-linecap="round"
                  stroke-linejoin="round"
                >
                  <polygon points="12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2" />
                </svg>
                AI生成
              </button>
            </div>
          </template>
        </EmptyState>
      </div>

      <!-- Loading state -->
      <div v-if="workflowStore.isLoading" class="loading-state">
        <div class="loading-spinner" />
        <p>加载工作流中…</p>
      </div>

      <!-- ===== MODALS ===== -->
      <div v-if="showEditor" class="modal-overlay" @click.self="handleEditorCancel">
        <div class="modal modal-large">
          <WorkflowEditor
            :workflow="editingWorkflow"
            @save="handleEditorSave"
            @cancel="handleEditorCancel"
          />
        </div>
      </div>

      <div v-if="showGenerator" class="modal-overlay" @click.self="handleGeneratorClose">
        <div class="modal modal-large">
          <AIWorkflowGenerator
            @close="handleGeneratorClose"
            @confirm="handleGeneratorConfirm"
          />
        </div>
      </div>

      <div v-if="showExecution" class="execution-modal">
        <WorkflowExecution
          :execution="workflowStore.currentExecution"
          @close="handleExecutionClose"
        />
      </div>
    </div>
  </div>
</template>

<style scoped>
/* ===================== Layout ===================== */
.workflow-library {
  height: 100%;
  overflow-y: auto;
}

.library-inner {
  position: relative;
  z-index: 1;
  max-width: 1100px;
  margin: 0 auto;
  padding: 24px 32px;
}

/* ===================== Page Header actions ===================== */
.search-box {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 12px;
  background: var(--paper-tint);
  border: 1px solid var(--border-default);
  border-radius: var(--el-border-radius-base);
  transition: border-color var(--dur-fast) var(--ease-out);
}

.search-box:focus-within {
  border-color: var(--border-brand);
  box-shadow: 0 0 0 2px var(--gold-tint);
}

.search-icon {
  color: var(--fg-tertiary);
  flex-shrink: 0;
}

.search-input {
  flex: 1;
  background: transparent;
  border: none;
  outline: none;
  font-size: 0.8125rem;
  color: var(--fg-primary);
  font-family: var(--font-sans-cn);
  width: 180px;
}

.search-input::placeholder {
  color: var(--fg-tertiary);
}

.search-clear {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 16px;
  height: 16px;
  border-radius: var(--el-border-round);
  background: var(--paper-tint);
  color: var(--fg-tertiary);
  border: none;
  cursor: pointer;
  flex-shrink: 0;
  transition: background var(--dur-fast) var(--ease-out);
}

.search-clear:hover {
  background: var(--border-strong);
  color: var(--fg-secondary);
}

/* ===================== Buttons ===================== */
.btn-create {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 7px 14px;
  font-size: 0.8125rem;
  font-weight: 500;
  color: var(--ink-950);
  background: var(--gold-400);
  border: none;
  border-radius: var(--el-border-radius-base);
  cursor: pointer;
  white-space: nowrap;
  transition: background var(--dur-fast) var(--ease-out);
}

.btn-create:hover {
  background: var(--gold-300);
}

.btn-ghost {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 5px 10px;
  font-size: 0.75rem;
  color: var(--fg-secondary);
  background: transparent;
  border: 1px solid var(--border-default);
  border-radius: var(--el-border-radius-small);
  cursor: pointer;
  white-space: nowrap;
  transition: background var(--dur-fast) var(--ease-out), border-color var(--dur-fast) var(--ease-out), color var(--dur-fast) var(--ease-out);
}

.btn-ghost:hover {
  background: var(--paper-tint);
  border-color: var(--border-brand);
  color: var(--fg-primary);
}

.btn-ai-gen {
  color: var(--el-color-primary);
  border-color: var(--border-brand);
}

.btn-ai-gen:hover {
  background: var(--gold-tint);
  border-color: var(--border-brand-hover);
  color: var(--el-color-primary);
}

/* ===================== Filter Tabs ===================== */
.filter-tabs {
  display: flex;
  align-items: center;
  gap: 4px;
  border-bottom: 1px solid var(--border-subtle);
  padding-bottom: 12px;
  margin-bottom: 20px;
}

.filter-tab {
  font-size: 0.8125rem;
  font-weight: 500;
  color: var(--fg-secondary);
  background: transparent;
  border: none;
  padding: 6px 12px;
  border-radius: var(--el-border-radius-small);
  cursor: pointer;
  border-bottom: 2px solid transparent;
  margin-bottom: -13px;
  transition: background var(--dur-fast) var(--ease-out), color var(--dur-fast) var(--ease-out);
}

.filter-tab:hover {
  color: var(--fg-primary);
  background: var(--paper-tint);
}

.filter-tab.active {
  color: var(--fg-primary);
  border-bottom-color: var(--gold-400);
}

/* ===================== Workflow Cards ===================== */
.workflow-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.workflow-card {
  display: flex;
  align-items: center;
  gap: 16px;
  border-left: 3px solid var(--border-default);
}

/* Status icon */
.card-status-icon {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 32px;
  height: 32px;
  border-radius: var(--el-border-round);
  flex-shrink: 0;
}

.pulse-dot {
  width: 10px;
  height: 10px;
  border-radius: var(--el-border-round);
  animation: fs-pulse-dot 1.5s ease-in-out infinite;
}

/* Card info */
.card-info {
  display: flex;
  flex-direction: column;
  gap: 6px;
  flex: 1;
  min-width: 0;
}

.card-title-row {
  display: flex;
  align-items: center;
  gap: 8px;
}

.card-name {
  font-size: 0.8125rem;
  font-weight: 600;
  color: var(--fg-primary);
}

.card-status-label {
  font-size: 0.75rem;
  font-weight: 500;
}

/* Step pills */
.card-steps {
  display: flex;
  align-items: center;
  gap: 6px;
  flex-wrap: wrap;
}

.step-pill {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 2px 8px;
  font-size: 0.75rem;
  border-radius: var(--el-border-radius-small);
  font-family: var(--font-mono);
  white-space: nowrap;
}

.step-completed {
  color: var(--bronze-500);
  background: color-mix(in srgb, var(--bronze-500) 10%, transparent);
}

.step-current {
  color: var(--gold-400);
  background: var(--gold-tint);
  border: 1px solid var(--gold-400);
}

.step-pending {
  color: var(--fg-tertiary);
  background: var(--paper-tint);
}

.step-dot {
  width: 6px;
  height: 6px;
  border-radius: var(--el-border-round);
  animation: fs-pulse-dot 1.5s ease-in-out infinite;
  flex-shrink: 0;
}

.step-chevron {
  flex-shrink: 0;
}

/* Progress bar */
.card-progress {
  display: flex;
  align-items: center;
  gap: 8px;
}

.progress-track {
  flex: 1;
  max-width: 200px;
  height: 4px;
  border-radius: var(--el-border-round);
  background: var(--paper-tint);
  overflow: hidden;
}

.progress-fill {
  height: 100%;
  border-radius: var(--el-border-round);
  transition: width var(--dur-fast) var(--ease-out);
}

.progress-text {
  font-size: 0.75rem;
  color: var(--fg-tertiary);
  font-family: var(--font-mono);
  white-space: nowrap;
}

.cron-badge {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 2px 6px;
  font-size: 0.75rem;
  color: var(--el-color-info);
  background: color-mix(in srgb, var(--el-color-info) 12%, transparent);
  border-radius: var(--el-border-radius-small);
  font-family: var(--font-mono);
  white-space: nowrap;
}

/* Card meta */
.card-meta {
  display: flex;
  align-items: center;
  gap: 12px;
  flex-shrink: 0;
}

.meta-info {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: 2px;
}

.meta-duration {
  font-family: var(--font-mono);
  font-size: 0.75rem;
}

.meta-time {
  font-size: 0.75rem;
  color: var(--fg-tertiary);
  font-family: var(--font-mono);
  white-space: nowrap;
}

.card-actions {
  display: flex;
  align-items: center;
  gap: 6px;
}

.empty-actions {
  display: flex;
  gap: 12px;
}

/* ===================== Loading State ===================== */
.loading-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 60px 20px;
  text-align: center;
}

.loading-spinner {
  width: 36px;
  height: 36px;
  border: 3px solid var(--border-default);
  border-top-color: var(--gold-400);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
  margin-bottom: 16px;
}

.loading-state p {
  font-size: 0.8125rem;
  color: var(--fg-tertiary);
  margin: 0;
}

/* ===================== Modal ===================== */
.modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: color-mix(in srgb, var(--ink-950) 55%, transparent);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
  padding: 20px;
}

.modal {
  background: var(--el-bg-color-overlay);
  border-radius: var(--radius-xl);
  max-height: 90vh;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.modal-large {
  width: 100%;
  max-width: 1100px;
  height: 85vh;
}

.execution-modal {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: var(--el-bg-color);
  z-index: 1000;
}

/* ===================== Animations ===================== */
@keyframes fs-pulse-dot {
  0%, 100% { opacity: 1; transform: scale(1); }
  50% { opacity: 0.5; transform: scale(0.85); }
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

/* Reduced motion */
@media (prefers-reduced-motion: reduce) {
  .pulse-dot,
  .step-dot,
  .loading-spinner {
    animation: none;
  }
}

/* ===================== Responsive ===================== */
@media (max-width: 768px) {
  .library-inner {
    padding: 16px;
  }

  .workflow-card {
    flex-direction: column;
    align-items: flex-start;
    gap: 12px;
  }

  .card-meta {
    width: 100%;
    justify-content: space-between;
  }

  .modal-large {
    height: 100vh;
    max-height: 100vh;
    border-radius: 0;
  }

  .modal-overlay {
    padding: 0;
  }

  .filter-tabs {
    overflow-x: auto;
    flex-wrap: nowrap;
  }

  .filter-tab {
    flex-shrink: 0;
    white-space: nowrap;
  }
}
</style>
