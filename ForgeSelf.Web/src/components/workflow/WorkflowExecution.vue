<script setup lang="ts">
import { computed } from 'vue'
import AppLogo from '@/components/AppLogo.vue'
import type { WorkflowExecution } from '@/types/workflow'
import StepTimeline from './StepTimeline.vue'
import ExecutionLog from './ExecutionLog.vue'
import { useWorkflowStore } from '@/stores/workflow'

const props = defineProps<{
  execution?: WorkflowExecution | null
}>()

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'pause'): void
  (e: 'resume'): void
  (e: 'cancel'): void
}>()

const workflowStore = useWorkflowStore()

const statusLabel = computed(() => {
  switch (props.execution?.status) {
    case 'running':
      return '运行中'
    case 'paused':
      return '已暂停'
    case 'completed':
      return '已完成'
    case 'failed':
      return '失败'
    case 'cancelled':
      return '已取消'
    default:
      return '未知'
  }
})

const statusClass = computed(() => {
  return `status-${props.execution?.status || 'unknown'}`
})

const duration = computed(() => {
  if (!props.execution?.startTime) return '-'
  const end = props.execution.endTime || new Date()
  const durationMs = end.getTime() - new Date(props.execution.startTime).getTime()
  
  if (durationMs < 1000) {
    return `${durationMs}ms`
  } else if (durationMs < 60000) {
    return `${(durationMs / 1000).toFixed(1)}秒`
  } else {
    const minutes = Math.floor(durationMs / 60000)
    const seconds = Math.floor((durationMs % 60000) / 1000)
    return `${minutes}分${seconds}秒`
  }
})

const canPause = computed(() => props.execution?.status === 'running')
const canResume = computed(() => props.execution?.status === 'paused')
const canCancel = computed(() => 
  props.execution?.status === 'running' || props.execution?.status === 'paused'
)

async function handlePause(): Promise<void> {
  if (!props.execution) return
  try {
    await workflowStore.pauseExecution(props.execution.id)
    emit('pause')
  } catch (e) {
    console.error('暂停失败:', e)
  }
}

async function handleResume(): Promise<void> {
  if (!props.execution) return
  try {
    await workflowStore.resumeExecution(props.execution.id)
    emit('resume')
  } catch (e) {
    console.error('继续失败:', e)
  }
}

async function handleCancel(): Promise<void> {
  if (!props.execution) return
  if (!confirm('确定要取消执行吗？')) return
  try {
    await workflowStore.cancelExecution(props.execution.id)
    emit('cancel')
  } catch (e) {
    console.error('取消失败:', e)
  }
}

function handleClose(): void {
  emit('close')
}
</script>

<template>
  <div class="workflow-execution">
    <div class="execution-header">
      <div class="header-left">
        <button class="back-btn" aria-label="返回" @click="handleClose">
          ←
        </button>
        <AppLogo :size="20" />
        <div class="header-info">
          <h2>{{ execution?.workflowName || '工作流执行' }}</h2>
          <div class="execution-meta">
            <span class="status-badge" :class="statusClass">
              <span class="status-dot" />
              {{ statusLabel }}
            </span>
            <span class="duration">⏱ {{ duration }}</span>
          </div>
        </div>
      </div>
      <div class="header-actions">
        <button
          v-if="canPause"
          class="btn btn-warning"
          @click="handlePause"
        >
          ⏸ 暂停
        </button>
        <button
          v-if="canResume"
          class="btn btn-success"
          @click="handleResume"
        >
          ▶ 继续
        </button>
        <button
          v-if="canCancel"
          class="btn btn-danger"
          @click="handleCancel"
        >
          ⏹ 取消
        </button>
      </div>
    </div>

    <div v-if="execution" class="execution-body">
      <div class="execution-sidebar">
        <StepTimeline
          :steps="execution.steps"
          :current-step-id="execution.currentStepId"
        />
        
        <div v-if="execution.outputVariables && Object.keys(execution.outputVariables).length > 0" class="output-section">
          <h4>输出结果</h4>
          <div class="output-list">
            <div
              v-for="(value, key) in execution.outputVariables"
              :key="key"
              class="output-item"
            >
              <span class="output-key">{{ key }}</span>
              <span class="output-value">{{ typeof value === 'object' ? JSON.stringify(value) : String(value) }}</span>
            </div>
          </div>
        </div>

        <div v-if="execution.error" class="error-section">
          <h4>错误信息</h4>
          <div class="error-message">{{ execution.error }}</div>
        </div>
      </div>

      <div class="execution-main">
        <ExecutionLog :logs="execution.logs" :auto-scroll="true" />
      </div>
    </div>

    <div v-else class="loading-state">
      <div class="loading-spinner" />
      <p>加载执行详情...</p>
    </div>
  </div>
</template>

<style scoped>
.workflow-execution {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: var(--el-fill-color-light);
}

.execution-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px 24px;
  background: var(--el-bg-color);
  border-bottom: 1px solid var(--el-border-color-lighter);
}

.header-left {
  display: flex;
  align-items: center;
  gap: 16px;
}

.back-btn {
  width: 36px;
  height: 36px;
  border: 1px solid var(--el-border-color-light);
  background: var(--el-bg-color);
  border-radius: 8px;
  font-size: 18px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s ease;
}

.back-btn:hover {
  border-color: var(--el-color-primary);
  color: var(--el-color-primary);
  background: var(--el-color-info-light-9);
}

.header-info h2 {
  font-size: 18px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin: 0 0 4px 0;
}

.execution-meta {
  display: flex;
  align-items: center;
  gap: 12px;
}

.status-badge {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 4px 12px;
  border-radius: 12px;
  font-size: 12px;
  font-weight: 500;
}

.status-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
}

.status-running {
  background: var(--el-color-info-light-8);
  color: var(--el-color-info-dark-2);
}

.status-running .status-dot {
  background: var(--el-color-primary);
  animation: pulse 1.5s ease-in-out infinite;
}

@keyframes pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.5; }
}

.status-paused {
  background: var(--el-color-warning-light-9);
  color: var(--el-color-warning-dark-2);
}

.status-paused .status-dot {
  background: var(--el-color-warning);
}

.status-completed {
  background: var(--el-color-success-light-9);
  color: var(--el-color-success-dark-2);
}

.status-completed .status-dot {
  background: var(--el-color-success);
}

.status-failed {
  background: var(--el-color-error-light-9);
  color: var(--el-color-error-dark-2);
}

.status-failed .status-dot {
  background: var(--el-color-error);
}

.status-cancelled {
  background: var(--el-color-info-light-9);
  color: var(--el-text-color-regular);
}

.status-cancelled .status-dot {
  background: var(--el-fill-color);
}

.duration {
  font-size: 13px;
  color: var(--el-text-color-secondary);
}

.header-actions {
  display: flex;
  gap: 8px;
}

.btn {
  padding: 8px 16px;
  border-radius: 8px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s ease;
  border: 1px solid transparent;
}

.btn-warning {
  background: var(--el-color-warning);
  color: var(--el-text-color-primary);
  border-color: var(--el-color-warning);
}

.btn-warning:hover {
  background: var(--el-color-warning);
  border-color: var(--el-color-warning);
}

.btn-success {
  background: var(--el-color-success);
  color: var(--el-color-white);
  border-color: var(--el-color-success);
}

.btn-success:hover {
  background: var(--el-color-success);
  border-color: var(--el-color-success);
}

.btn-danger {
  background: var(--el-color-error);
  color: var(--el-color-white);
  border-color: var(--el-color-error);
}

.btn-danger:hover {
  background: var(--el-color-error-dark-2);
  border-color: var(--el-color-error-dark-2);
}

.execution-body {
  flex: 1;
  display: flex;
  overflow: hidden;
  padding: 16px;
  gap: 16px;
}

.execution-sidebar {
  width: 360px;
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  gap: 16px;
  overflow-y: auto;
}

.output-section,
.error-section {
  background: var(--el-bg-color);
  border-radius: 8px;
  padding: 16px;
}

.output-section h4,
.error-section h4 {
  font-size: 14px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin: 0 0 12px 0;
}

.output-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.output-item {
  display: flex;
  flex-direction: column;
  gap: 4px;
  padding: 8px 12px;
  background: var(--el-fill-color-light);
  border-radius: 6px;
}

.output-key {
  font-size: 12px;
  font-weight: 500;
  color: var(--el-text-color-secondary);
}

.output-value {
  font-size: 13px;
  color: var(--el-text-color-primary);
  word-break: break-all;
  font-family: 'Consolas', 'Monaco', monospace;
}

.error-message {
  padding: 12px;
  background: var(--el-color-error-light-9);
  border-left: 3px solid var(--el-color-error);
  border-radius: 4px;
  font-size: 13px;
  color: var(--el-color-error);
  word-break: break-all;
}

.execution-main {
  flex: 1;
  min-height: 0;
}

.loading-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  flex: 1;
  gap: 16px;
  color: var(--el-text-color-secondary);
}

.loading-spinner {
  width: 40px;
  height: 40px;
  border: 3px solid var(--el-border-color-lighter);
  border-top-color: var(--el-color-primary);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

@media (max-width: 1024px) {
  .execution-sidebar {
    width: 300px;
  }
}

@media (max-width: 768px) {
  .execution-header {
    padding: 12px 16px;
    flex-wrap: wrap;
    gap: 12px;
  }

  .header-info h2 {
    font-size: 16px;
  }

  .execution-body {
    flex-direction: column;
    padding: 12px;
  }

  .execution-sidebar {
    width: 100%;
    max-height: 40vh;
  }

  .execution-main {
    height: 400px;
  }
}
</style>
