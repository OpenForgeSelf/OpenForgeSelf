<script setup lang="ts">
import { ref, computed, watch, nextTick, onMounted, onUnmounted } from 'vue'
import type { ScriptExecution, ScriptExecutionStatus } from '@/types/scriptRunner'

const props = defineProps<{
  execution: ScriptExecution | null
  autoScroll?: boolean
}>()

const emit = defineEmits<{
  (e: 'cancel'): void
  (e: 'close'): void
}>()

const consoleRef = ref<HTMLDivElement | null>(null)
const autoScrollEnabled = ref(true)
const userScrolled = ref(false)

const statusLabels: Record<ScriptExecutionStatus, string> = {
  pending: '等待中',
  running: '运行中',
  completed: '已完成',
  failed: '失败',
  cancelled: '已取消',
  timeout: '超时'
}

const statusColors: Record<ScriptExecutionStatus, string> = {
  pending: '#ffc107',
  running: '#007bff',
  completed: '#28a745',
  failed: '#dc3545',
  cancelled: '#6c757d',
  timeout: '#fd7e14'
}

const displayStatus = computed(() => {
  if (!props.execution) return ''
  return statusLabels[props.execution.status]
})

const statusColor = computed(() => {
  if (!props.execution) return '#6c757d'
  return statusColors[props.execution.status]
})

const isRunning = computed(() => {
  return props.execution?.status === 'running' || props.execution?.status === 'pending'
})

const formattedDuration = computed(() => {
  if (!props.execution?.duration) {
    if (props.execution?.startTime) {
      const end = props.execution.endTime || new Date()
      const ms = end.getTime() - props.execution.startTime.getTime()
      return formatDuration(ms)
    }
    return '0s'
  }
  return formatDuration(props.execution.duration)
})

function formatDuration(ms: number): string {
  if (ms < 1000) return `${ms}ms`
  const seconds = Math.floor(ms / 1000)
  const minutes = Math.floor(seconds / 60)
  const hours = Math.floor(minutes / 60)

  if (hours > 0) {
    return `${hours}h ${minutes % 60}m ${seconds % 60}s`
  }
  if (minutes > 0) {
    return `${minutes}m ${seconds % 60}s`
  }
  return `${seconds}s`
}

function scrollToBottom(): void {
  if (consoleRef.value && autoScrollEnabled.value) {
    consoleRef.value.scrollTop = consoleRef.value.scrollHeight
  }
}

function handleScroll(): void {
  if (!consoleRef.value) return
  const { scrollTop, scrollHeight, clientHeight } = consoleRef.value
  const isAtBottom = scrollHeight - scrollTop - clientHeight < 50
  userScrolled.value = !isAtBottom
  if (isAtBottom) {
    autoScrollEnabled.value = true
  }
}

function toggleAutoScroll(): void {
  autoScrollEnabled.value = !autoScrollEnabled.value
  if (autoScrollEnabled.value) {
    nextTick(() => scrollToBottom())
  }
}

async function copyOutput(): Promise<void> {
  if (!props.execution) return
  try {
    await navigator.clipboard.writeText(props.execution.output)
  } catch (err) {
    console.error('复制失败:', err)
  }
}

function handleCancel(): void {
  emit('cancel')
}

function handleClose(): void {
  emit('close')
}

watch(() => props.execution?.logs.length, () => {
  nextTick(() => {
    if (autoScrollEnabled.value) {
      scrollToBottom()
    }
  })
})

let durationTimer: ReturnType<typeof setInterval> | null = null

onMounted(() => {
  if (isRunning.value) {
    durationTimer = setInterval(() => {
      // 触发重新计算
    }, 1000)
  }
})

onUnmounted(() => {
  if (durationTimer) {
    clearInterval(durationTimer)
  }
})
</script>

<template>
  <div class="script-console">
    <div class="console-header">
      <div class="console-title">
        <span class="status-indicator" :style="{ background: statusColor }" />
        <span class="status-text">{{ displayStatus }}</span>
        <span v-if="execution" class="script-name">{{ execution.scriptName }}</span>
      </div>
      <div class="console-actions">
        <span class="duration">{{ formattedDuration }}</span>
        <button
          class="icon-btn"
          :class="{ active: autoScrollEnabled }"
          :title="autoScrollEnabled ? '自动滚动已开启' : '自动滚动已关闭'"
          aria-label="切换自动滚动"
          @click="toggleAutoScroll"
        >
          ⬇
        </button>
        <button
          class="icon-btn"
          title="复制输出"
          aria-label="复制输出"
          @click="copyOutput"
        >
          📋
        </button>
        <button
          v-if="isRunning"
          class="icon-btn cancel-btn"
          title="取消执行"
          aria-label="取消执行"
          @click="handleCancel"
        >
          ⏹
        </button>
        <button
          class="icon-btn"
          title="关闭"
          aria-label="关闭控制台"
          @click="handleClose"
        >
          ✕
        </button>
      </div>
    </div>

    <div
      ref="consoleRef"
      class="console-output"
      role="log"
      aria-live="polite"
      @scroll="handleScroll"
    >
      <div v-if="!execution" class="console-empty">
        <p>等待执行...</p>
      </div>
      <div v-else class="console-logs">
        <div
          v-for="(log, index) in execution.logs"
          :key="index"
          class="log-line"
          :class="log.stream"
        >
          <span class="log-time">{{ new Date(log.timestamp).toLocaleTimeString() }}</span>
          <span class="log-stream">{{ log.stream === 'stdout' ? '›' : '!' }}</span>
          <span class="log-message">{{ log.message }}</span>
        </div>
        <div v-if="execution.logs.length === 0" class="console-empty">
          <p>暂无输出</p>
        </div>
        <div v-if="isRunning" class="console-prompt">
          <span class="prompt-cursor">█</span>
        </div>
      </div>
    </div>

    <div v-if="execution && execution.exitCode !== undefined" class="console-footer">
      <div class="exit-info">
        退出码: <span :class="{ 'exit-success': execution.exitCode === 0, 'exit-error': execution.exitCode !== 0 }">
          {{ execution.exitCode }}
        </span>
      </div>
    </div>
  </div>
</template>

<style scoped>
.script-console {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: var(--el-bg-color-page);
  border-radius: 8px;
  overflow: hidden;
  font-family: 'Consolas', 'Monaco', 'Courier New', monospace;
}

.console-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 10px 14px;
  background: var(--el-fill-color);
  border-bottom: 1px solid var(--el-border-color);
}

.console-title {
  display: flex;
  align-items: center;
  gap: 10px;
}

.status-indicator {
  width: 10px;
  height: 10px;
  border-radius: 50%;
}

.status-text {
  font-size: 13px;
  font-weight: 500;
  color: var(--el-text-color-primary);
}

.script-name {
  font-size: 12px;
  color: var(--el-text-color-secondary);
  margin-left: 8px;
}

.console-actions {
  display: flex;
  align-items: center;
  gap: 8px;
}

.duration {
  font-size: 12px;
  color: var(--el-text-color-secondary);
  margin-right: 4px;
}

.icon-btn {
  width: 28px;
  height: 28px;
  border: 1px solid var(--el-border-color);
  background: var(--el-fill-color-light);
  color: var(--el-text-color-primary);
  border-radius: 4px;
  font-size: 12px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s ease;
}

.icon-btn:hover {
  background: var(--el-fill-color);
  border-color: var(--el-border-color);
}

.icon-btn.active {
  background: var(--el-color-primary);
  border-color: var(--el-color-primary);
  color: var(--el-color-white);
}

.cancel-btn:hover {
  background: var(--el-color-error);
  border-color: var(--el-color-error);
  color: var(--el-color-white);
}

.console-output {
  flex: 1;
  overflow-y: auto;
  padding: 12px;
  background: var(--el-bg-color-page);
}

.console-output::-webkit-scrollbar {
  width: 8px;
}

.console-output::-webkit-scrollbar-track {
  background: var(--el-fill-color);
}

.console-output::-webkit-scrollbar-thumb {
  background: var(--el-fill-color-light);
  border-radius: 4px;
}

.console-output::-webkit-scrollbar-thumb:hover {
  background: var(--el-fill-color-light);
}

.console-empty {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
  color: var(--el-text-color-secondary);
  font-size: 14px;
}

.console-logs {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.log-line {
  display: flex;
  gap: 10px;
  font-size: 13px;
  line-height: 1.5;
  word-break: break-all;
}

.log-time {
  color: var(--el-color-success);
  flex-shrink: 0;
  font-size: 11px;
  opacity: 0.8;
}

.log-stream {
  flex-shrink: 0;
  width: 14px;
  text-align: center;
}

.log-line.stdout .log-stream {
  color: var(--el-color-info-dark-2);
}

.log-line.stderr .log-stream {
  color: var(--el-color-error);
}

.log-line.stdout .log-message {
  color: var(--el-text-color-primary);
}

.log-line.stderr .log-message {
  color: var(--el-color-error);
}

.console-prompt {
  margin-top: 4px;
}

.prompt-cursor {
  color: var(--el-text-color-primary);
  animation: blink 1s step-end infinite;
}

@keyframes blink {
  50% { opacity: 0; }
}

.console-footer {
  padding: 8px 14px;
  background: var(--el-fill-color);
  border-top: 1px solid var(--el-border-color);
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.exit-info {
  display: flex;
  align-items: center;
  gap: 6px;
}

.exit-success {
  color: var(--el-color-success);
  font-weight: 600;
}

.exit-error {
  color: var(--el-color-error);
  font-weight: 600;
}
</style>
