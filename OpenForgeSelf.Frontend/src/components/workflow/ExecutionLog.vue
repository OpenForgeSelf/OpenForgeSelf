<script setup lang="ts">
import { ref, onMounted, watch, nextTick } from 'vue'
import type { ExecutionLogEntry } from '@/types/workflow'

const props = defineProps<{
  logs: ExecutionLogEntry[]
  autoScroll?: boolean
}>()

const logContainerRef = ref<HTMLElement | null>(null)
const autoScrollEnabled = ref(props.autoScroll ?? true)

watch(() => props.logs, () => {
  if (autoScrollEnabled.value) {
    nextTick(() => {
      scrollToBottom()
    })
  }
}, { deep: true })

onMounted(() => {
  if (autoScrollEnabled.value) {
    scrollToBottom()
  }
})

function scrollToBottom(): void {
  if (logContainerRef.value) {
    logContainerRef.value.scrollTop = logContainerRef.value.scrollHeight
  }
}

function handleScroll(): void {
  if (logContainerRef.value) {
    const { scrollTop, scrollHeight, clientHeight } = logContainerRef.value
    const isAtBottom = scrollHeight - scrollTop - clientHeight < 20
    autoScrollEnabled.value = isAtBottom
  }
}

function formatTime(timestamp: Date): string {
  const d = new Date(timestamp)
  const hours = d.getHours().toString().padStart(2, '0')
  const minutes = d.getMinutes().toString().padStart(2, '0')
  const seconds = d.getSeconds().toString().padStart(2, '0')
  const ms = d.getMilliseconds().toString().padStart(3, '0')
  return `${hours}:${minutes}:${seconds}.${ms}`
}

function getLevelLabel(level: string): string {
  const labels: Record<string, string> = {
    info: 'INFO',
    warn: 'WARN',
    error: 'ERROR',
    debug: 'DEBUG'
  }
  return labels[level] || level.toUpperCase()
}
</script>

<template>
  <div class="execution-log">
    <div class="log-header">
      <h4>执行日志</h4>
      <div class="log-controls">
        <button
          class="toggle-btn"
          :class="{ active: autoScrollEnabled }"
          :title="autoScrollEnabled ? '取消自动滚动' : '自动滚动到底部'"
          @click="autoScrollEnabled = !autoScrollEnabled"
        >
          {{ autoScrollEnabled ? '🔽 自动滚动' : '⏸ 已暂停' }}
        </button>
      </div>
    </div>
    <div
      ref="logContainerRef"
      class="log-container"
      role="log"
      aria-live="polite"
      @scroll="handleScroll"
    >
      <div v-if="logs.length === 0" class="empty-logs">
        <p>暂无日志记录</p>
      </div>
      <div
        v-for="log in logs"
        :key="log.id"
        class="log-entry"
        :class="`log-${log.level}`"
      >
        <span class="log-time">{{ formatTime(log.timestamp) }}</span>
        <span class="log-level">{{ getLevelLabel(log.level) }}</span>
        <span v-if="log.stepName" class="log-step">[{{ log.stepName }}]</span>
        <span class="log-message">{{ log.message }}</span>
      </div>
    </div>
  </div>
</template>

<style scoped>
.execution-log {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: #1e1e1e;
  border-radius: 8px;
  overflow: hidden;
}

.log-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 10px 16px;
  background: #2d2d2d;
  border-bottom: 1px solid #3d3d3d;
}

.log-header h4 {
  font-size: 13px;
  font-weight: 600;
  color: #e0e0e0;
  margin: 0;
}

.log-controls {
  display: flex;
  gap: 8px;
}

.toggle-btn {
  padding: 4px 10px;
  font-size: 12px;
  background: #3d3d3d;
  color: #b0b0b0;
  border: 1px solid #4d4d4d;
  border-radius: 4px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.toggle-btn:hover {
  background: #4d4d4d;
  color: #e0e0e0;
}

.toggle-btn.active {
  color: #4fc3f7;
  border-color: #4fc3f7;
}

.log-container {
  flex: 1;
  overflow-y: auto;
  padding: 12px;
  font-family: 'Consolas', 'Monaco', 'Courier New', monospace;
  font-size: 12px;
  line-height: 1.6;
}

.empty-logs {
  text-align: center;
  padding: 40px 20px;
  color: #6c757d;
}

.log-entry {
  display: flex;
  gap: 8px;
  padding: 2px 0;
  word-break: break-all;
}

.log-time {
  color: #888;
  flex-shrink: 0;
}

.log-level {
  font-weight: 600;
  flex-shrink: 0;
  min-width: 50px;
}

.log-step {
  color: #ce93d8;
  flex-shrink: 0;
}

.log-message {
  flex: 1;
  color: #e0e0e0;
}

.log-info .log-level {
  color: #4fc3f7;
}

.log-warn .log-level {
  color: #ffb74d;
}

.log-warn .log-message {
  color: #ffe0b2;
}

.log-error .log-level {
  color: #ef5350;
}

.log-error .log-message {
  color: #ffcdd2;
}

.log-debug .log-level {
  color: #90a4ae;
}

.log-debug .log-message {
  color: #b0bec5;
}

.log-container::-webkit-scrollbar {
  width: 6px;
}

.log-container::-webkit-scrollbar-track {
  background: #2d2d2d;
}

.log-container::-webkit-scrollbar-thumb {
  background: #4d4d4d;
  border-radius: 3px;
}

.log-container::-webkit-scrollbar-thumb:hover {
  background: #5d5d5d;
}
</style>
