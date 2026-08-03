<script setup lang="ts">
import { ref } from 'vue'
import { useTextToolsStore } from '@/stores/textTools'
import type { HashType } from '@/types/textTools'

const store = useTextToolsStore()
const copySuccess = ref(false)

const hashTypes: { value: HashType; label: string }[] = [
  { value: 'md5', label: 'MD5' },
  { value: 'sha1', label: 'SHA-1' },
  { value: 'sha256', label: 'SHA-256' },
  { value: 'sha512', label: 'SHA-512' }
]

async function handleCopy(): Promise<void> {
  if (!store.outputText) return

  try {
    await navigator.clipboard.writeText(store.outputText)
    copySuccess.value = true
    setTimeout(() => {
      copySuccess.value = false
    }, 2000)
  } catch (e) {
    console.error('复制失败:', e)
  }
}
</script>

<template>
  <div class="hash-panel">
    <div class="panel-row">
      <label class="field-label">哈希类型</label>
      <div class="type-selector">
        <button
          v-for="type in hashTypes"
          :key="type.value"
          class="type-btn"
          :class="{ active: store.hashType === type.value }"
          @click="store.setHashType(type.value)"
        >
          {{ type.label }}
        </button>
      </div>
    </div>

    <div class="panel-actions">
      <button
        class="action-btn primary"
        :disabled="store.isProcessing || !store.inputText"
        @click="store.processHash()"
      >
        <span v-if="store.isProcessing">⏳ 计算中...</span>
        <span v-else>🔒 计算哈希</span>
      </button>
      <button
        class="action-btn"
        :disabled="!store.outputText"
        @click="handleCopy"
      >
        {{ copySuccess ? '✅ 已复制' : '📋 复制结果' }}
      </button>
    </div>

    <div v-if="store.outputText" class="hash-result">
      <div class="result-label">哈希结果</div>
      <div class="result-value">{{ store.outputText }}</div>
    </div>
  </div>
</template>

<style scoped>
.hash-panel {
  display: flex;
  flex-direction: column;
  gap: 16px;
  padding: 16px;
  background-color: var(--el-bg-color-page);
  border-radius: 8px;
}

.panel-row {
  display: flex;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
}

.field-label {
  font-size: 13px;
  font-weight: 500;
  color: var(--el-text-color-regular);
  white-space: nowrap;
}

.type-selector {
  display: flex;
  gap: 4px;
  flex-wrap: wrap;
}

.type-btn {
  padding: 6px 14px;
  font-size: 13px;
  border: 1px solid var(--el-border-color);
  background-color: var(--el-bg-color);
  border-radius: 6px;
  color: var(--el-text-color-regular);
  transition: all 0.2s;
  cursor: pointer;
}

.type-btn:hover {
  background-color: var(--el-fill-color);
  border-color: var(--border-strong);
}

.type-btn.active {
  background-color: var(--el-color-primary);
  border-color: var(--el-color-primary);
  color: var(--el-color-white);
}

.panel-actions {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.action-btn {
  padding: 8px 16px;
  font-size: 13px;
  border: 1px solid var(--el-border-color);
  background-color: var(--el-bg-color);
  border-radius: 6px;
  color: var(--el-text-color-regular);
  transition: all 0.2s;
  cursor: pointer;
}

.action-btn:hover:not(:disabled) {
  background-color: var(--el-fill-color);
  border-color: var(--border-strong);
}

.action-btn.primary {
  background-color: var(--el-color-primary);
  border-color: var(--el-color-primary);
  color: var(--el-color-white);
}

.action-btn.primary:hover:not(:disabled) {
  background-color: var(--el-color-primary-light-3);
  border-color: var(--el-color-primary-light-3);
}

.action-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.hash-result {
  padding: 12px;
  background-color: var(--el-bg-color);
  border: 1px solid var(--el-border-color);
  border-radius: 6px;
}

.result-label {
  font-size: 12px;
  color: var(--el-text-color-secondary);
  margin-bottom: 8px;
}

.result-value {
  font-family: 'Consolas', 'Monaco', 'Courier New', monospace;
  font-size: 13px;
  word-break: break-all;
  color: var(--el-text-color-primary);
  padding: 8px;
  background-color: var(--el-bg-color-page);
  border-radius: 4px;
}
</style>
