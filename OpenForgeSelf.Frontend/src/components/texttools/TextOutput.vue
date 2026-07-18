<script setup lang="ts">
import { ref, computed } from 'vue'

const props = defineProps<{
  modelValue: string
}>()

const copySuccess = ref(false)

const charCount = computed(() => props.modelValue.length)

async function handleCopy(): Promise<void> {
  if (!props.modelValue) return

  try {
    await navigator.clipboard.writeText(props.modelValue)
    copySuccess.value = true
    setTimeout(() => {
      copySuccess.value = false
    }, 2000)
  } catch (e) {
    console.error('复制失败:', e)
  }
}

function handleDownload(): void {
  if (!props.modelValue) return

  const blob = new Blob([props.modelValue], { type: 'text/plain;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = `text-tools-output-${Date.now()}.txt`
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
  URL.revokeObjectURL(url)
}
</script>

<template>
  <div class="text-output">
    <div class="output-header">
      <span class="output-title">输出</span>
      <div class="output-actions">
        <span class="char-count">{{ charCount }} 字符</span>
        <button
          class="action-btn"
          title="复制"
          :disabled="!modelValue"
          @click="handleCopy"
        >
          {{ copySuccess ? '✅ 已复制' : '📋 复制' }}
        </button>
        <button
          class="action-btn"
          title="下载"
          :disabled="!modelValue"
          @click="handleDownload"
        >
          💾 下载
        </button>
      </div>
    </div>
    <div
      class="output-content"
      :class="{ 'is-empty': !modelValue }"
    >
      <template v-if="modelValue">
        <pre class="output-text">{{ modelValue }}</pre>
      </template>
      <template v-else>
        <div class="placeholder">
          <span class="placeholder-icon">📄</span>
          <span class="placeholder-text">处理结果将显示在这里</span>
        </div>
      </template>
    </div>
  </div>
</template>

<style scoped>
.text-output {
  display: flex;
  flex-direction: column;
  height: 100%;
  background-color: var(--bg-card);
  border: 1px solid var(--border-color);
  border-radius: 8px;
  overflow: hidden;
}

.output-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 10px 12px;
  background-color: var(--bg-secondary);
  border-bottom: 1px solid var(--border-color);
}

.output-title {
  font-weight: 500;
  font-size: 14px;
  color: var(--text-secondary);
}

.output-actions {
  display: flex;
  align-items: center;
  gap: 8px;
}

.char-count {
  font-size: 12px;
  color: var(--text-muted);
}

.action-btn {
  padding: 4px 10px;
  font-size: 12px;
  border: 1px solid var(--border-color);
  background-color: var(--bg-card);
  border-radius: 4px;
  color: var(--text-secondary);
  transition: all 0.2s;
  cursor: pointer;
}

.action-btn:hover:not(:disabled) {
  background-color: var(--bg-hover);
  border-color: var(--border-strong);
}

.action-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.output-content {
  flex: 1;
  overflow: auto;
  padding: 12px;
  background-color: var(--bg-card);
}

.output-content.is-empty {
  display: flex;
  align-items: center;
  justify-content: center;
}

.output-text {
  margin: 0;
  font-family: 'Consolas', 'Monaco', 'Courier New', monospace;
  font-size: 13px;
  line-height: 1.6;
  color: var(--text-primary);
  white-space: pre-wrap;
  word-break: break-all;
}

.placeholder {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
  color: var(--text-muted);
}

.placeholder-icon {
  font-size: 32px;
  opacity: 0.5;
}

.placeholder-text {
  font-size: 14px;
}
</style>
