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
  background: #f5f7fa;
}

.execution-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px 24px;
  background: #fff;
  border-bottom: 1px solid #e9ecef;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 16px;
}

.back-btn {
  width: 36px;
  height: 36px;
  border: 1px solid #dee2e6;
  background: #fff;
  border-radius: 8px;
  font-size: 18px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s ease;
}

.back-btn:hover {
  border-color: #1976d2;
  color: #1976d2;
  background: #f0f7ff;
}

.header-info h2 {
  font-size: 18px;
  font-weight: 600;
  color: #212529;
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
  background: #cce5ff;
  color: #004085;
}

.status-running .status-dot {
  background: #007bff;
  animation: pulse 1.5s ease-in-out infinite;
}

@keyframes pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.5; }
}

.status-paused {
  background: #fff3cd;
  color: #856404;
}

.status-paused .status-dot {
  background: #ffc107;
}

.status-completed {
  background: #d4edda;
  color: #155724;
}

.status-completed .status-dot {
  background: #28a745;
}

.status-failed {
  background: #f8d7da;
  color: #721c24;
}

.status-failed .status-dot {
  background: #dc3545;
}

.status-cancelled {
  background: #e2e3e5;
  color: #383d41;
}

.status-cancelled .status-dot {
  background: #6c757d;
}

.duration {
  font-size: 13px;
  color: #6c757d;
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
  background: #ffc107;
  color: #212529;
  border-color: #ffc107;
}

.btn-warning:hover {
  background: #e0a800;
  border-color: #e0a800;
}

.btn-success {
  background: #28a745;
  color: #fff;
  border-color: #28a745;
}

.btn-success:hover {
  background: #218838;
  border-color: #218838;
}

.btn-danger {
  background: #dc3545;
  color: #fff;
  border-color: #dc3545;
}

.btn-danger:hover {
  background: #c82333;
  border-color: #c82333;
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
  background: #fff;
  border-radius: 8px;
  padding: 16px;
}

.output-section h4,
.error-section h4 {
  font-size: 14px;
  font-weight: 600;
  color: #212529;
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
  background: #f8f9fa;
  border-radius: 6px;
}

.output-key {
  font-size: 12px;
  font-weight: 500;
  color: #6c757d;
}

.output-value {
  font-size: 13px;
  color: #212529;
  word-break: break-all;
  font-family: 'Consolas', 'Monaco', monospace;
}

.error-message {
  padding: 12px;
  background: #fff5f5;
  border-left: 3px solid #dc3545;
  border-radius: 4px;
  font-size: 13px;
  color: #dc3545;
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
  color: #6c757d;
}

.loading-spinner {
  width: 40px;
  height: 40px;
  border: 3px solid #e9ecef;
  border-top-color: #1976d2;
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
