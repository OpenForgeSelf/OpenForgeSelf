<script setup lang="ts">
import { computed } from 'vue'
import type { ScriptExecution, ScriptExecutionStatus, ScriptLanguage } from '@/types/scriptRunner'

const props = defineProps<{
  execution: ScriptExecution
}>()

const emit = defineEmits<{
  (e: 'close'): void
}>()

const statusLabels: Record<ScriptExecutionStatus, string> = {
  pending: '等待中',
  running: '运行中',
  completed: '已完成',
  failed: '失败',
  cancelled: '已取消',
  timeout: '超时'
}

const statusClasses: Record<ScriptExecutionStatus, string> = {
  pending: 'status-pending',
  running: 'status-running',
  completed: 'status-completed',
  failed: 'status-failed',
  cancelled: 'status-cancelled',
  timeout: 'status-timeout'
}

const languageNames: Record<ScriptLanguage, string> = {
  powershell: 'PowerShell',
  python: 'Python',
  nodejs: 'Node.js',
  shell: 'Shell',
  cmd: 'CMD'
}

const statusLabel = computed(() => statusLabels[props.execution.status])
const statusClass = computed(() => statusClasses[props.execution.status])
const languageLabel = computed(() => languageNames[props.execution.language])

const formattedStartTime = computed(() => {
  return props.execution.startTime
    ? new Date(props.execution.startTime).toLocaleString()
    : '-'
})

const formattedEndTime = computed(() => {
  return props.execution.endTime
    ? new Date(props.execution.endTime).toLocaleString()
    : '-'
})

const formattedDuration = computed(() => {
  if (props.execution.duration) {
    return formatDuration(props.execution.duration)
  }
  if (props.execution.startTime) {
    const end = props.execution.endTime || new Date()
    return formatDuration(end.getTime() - props.execution.startTime.getTime())
  }
  return '-'
})

function formatDuration(ms: number): string {
  if (ms < 1000) return `${ms}ms`
  const seconds = Math.floor(ms / 1000)
  const minutes = Math.floor(seconds / 60)
  const hours = Math.floor(minutes / 60)

  if (hours > 0) {
    return `${hours}小时 ${minutes % 60}分 ${seconds % 60}秒`
  }
  if (minutes > 0) {
    return `${minutes}分 ${seconds % 60}秒`
  }
  return `${seconds}秒`
}

function handleClose(): void {
  emit('close')
}
</script>

<template>
  <div class="execution-detail">
    <div class="detail-header">
      <h3>执行详情</h3>
      <button class="close-btn" aria-label="关闭" @click="handleClose">
        ✕
      </button>
    </div>

    <div class="detail-body">
      <section class="info-section">
        <h4>基本信息</h4>
        <div class="info-grid">
          <div class="info-item">
            <span class="info-label">脚本名称</span>
            <span class="info-value">{{ execution.scriptName }}</span>
          </div>
          <div class="info-item">
            <span class="info-label">语言</span>
            <span class="info-value">{{ languageLabel }}</span>
          </div>
          <div class="info-item">
            <span class="info-label">状态</span>
            <span class="status-badge" :class="statusClass">
              <span class="status-dot" />
              {{ statusLabel }}
            </span>
          </div>
          <div class="info-item">
            <span class="info-label">退出码</span>
            <span class="info-value">
              {{ execution.exitCode !== undefined ? execution.exitCode : '-' }}
            </span>
          </div>
          <div class="info-item">
            <span class="info-label">开始时间</span>
            <span class="info-value">{{ formattedStartTime }}</span>
          </div>
          <div class="info-item">
            <span class="info-label">结束时间</span>
            <span class="info-value">{{ formattedEndTime }}</span>
          </div>
          <div class="info-item full-width">
            <span class="info-label">执行耗时</span>
            <span class="info-value">{{ formattedDuration }}</span>
          </div>
        </div>
      </section>

      <section v-if="execution.parameters && Object.keys(execution.parameters).length > 0" class="info-section">
        <h4>执行参数</h4>
        <div class="parameters-list">
          <div
            v-for="(value, key) in execution.parameters"
            :key="key"
            class="param-item"
          >
            <span class="param-name">{{ key }}</span>
            <span class="param-value">{{ String(value) }}</span>
          </div>
        </div>
      </section>

      <section class="info-section">
        <h4>执行输出</h4>
        <div class="output-box">
          <pre>{{ execution.output || '无输出' }}</pre>
        </div>
      </section>

      <section v-if="execution.error" class="info-section">
        <h4>错误信息</h4>
        <div class="error-box">
          <pre>{{ execution.error }}</pre>
        </div>
      </section>
    </div>
  </div>
</template>

<style scoped>
.execution-detail {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: #fff;
  border-radius: 12px;
  overflow: hidden;
}

.detail-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px 20px;
  border-bottom: 1px solid #e9ecef;
  background: #f8f9fa;
}

.detail-header h3 {
  font-size: 16px;
  font-weight: 600;
  color: #212529;
  margin: 0;
}

.close-btn {
  width: 32px;
  height: 32px;
  border: 1px solid #dee2e6;
  background: #fff;
  border-radius: 8px;
  font-size: 14px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s ease;
  color: #6c757d;
}

.close-btn:hover {
  background: #f1f3f5;
  color: #212529;
}

.detail-body {
  flex: 1;
  overflow-y: auto;
  padding: 20px;
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.info-section h4 {
  font-size: 14px;
  font-weight: 600;
  color: #212529;
  margin: 0 0 12px 0;
  padding-bottom: 8px;
  border-bottom: 1px solid #e9ecef;
}

.info-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 12px 20px;
}

.info-item {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.info-item.full-width {
  grid-column: 1 / -1;
}

.info-label {
  font-size: 12px;
  color: #6c757d;
}

.info-value {
  font-size: 14px;
  color: #212529;
  font-weight: 500;
}

.status-badge {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 4px 10px;
  border-radius: 12px;
  font-size: 12px;
  font-weight: 500;
}

.status-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
}

.status-pending {
  background: #fff3cd;
  color: #856404;
}
.status-pending .status-dot {
  background: #ffc107;
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

.status-timeout {
  background: #ffe5d0;
  color: #7a4a00;
}
.status-timeout .status-dot {
  background: #fd7e14;
}

.parameters-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.param-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 10px 12px;
  background: #f8f9fa;
  border-radius: 8px;
}

.param-name {
  font-size: 13px;
  font-weight: 500;
  color: #495057;
}

.param-value {
  font-size: 13px;
  color: #212529;
  font-family: monospace;
  background: #fff;
  padding: 2px 8px;
  border-radius: 4px;
  border: 1px solid #e9ecef;
}

.output-box {
  background: #1e1e1e;
  border-radius: 8px;
  padding: 16px;
  max-height: 300px;
  overflow-y: auto;
}

.output-box pre {
  margin: 0;
  color: #d4d4d4;
  font-family: 'Consolas', 'Monaco', 'Courier New', monospace;
  font-size: 13px;
  line-height: 1.6;
  white-space: pre-wrap;
  word-break: break-all;
}

.error-box {
  background: #fff5f5;
  border: 1px solid #fed7d7;
  border-radius: 8px;
  padding: 16px;
  max-height: 200px;
  overflow-y: auto;
}

.error-box pre {
  margin: 0;
  color: #742a2a;
  font-family: 'Consolas', 'Monaco', 'Courier New', monospace;
  font-size: 13px;
  line-height: 1.6;
  white-space: pre-wrap;
  word-break: break-all;
}

@media (max-width: 768px) {
  .info-grid {
    grid-template-columns: 1fr;
  }
}
</style>
