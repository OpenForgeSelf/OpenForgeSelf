<script setup lang="ts">
import { useTextToolsStore } from '@/stores/textTools'
import type { EncodingType } from '@/types/textTools'

const store = useTextToolsStore()

const encodingTypes: { value: EncodingType; label: string }[] = [
  { value: 'base64', label: 'Base64' },
  { value: 'url', label: 'URL' },
  { value: 'unicode', label: 'Unicode' }
]
</script>

<template>
  <div class="encoding-panel">
    <div class="panel-row">
      <label class="field-label">类型</label>
      <div class="type-selector">
        <button
          v-for="type in encodingTypes"
          :key="type.value"
          class="type-btn"
          :class="{ active: store.encodingType === type.value }"
          @click="store.setEncodingType(type.value)"
        >
          {{ type.label }}
        </button>
      </div>
    </div>

    <div class="panel-actions">
      <button
        class="action-btn primary"
        :disabled="store.isProcessing || !store.inputText"
        @click="store.processEncoding(true)"
      >
        <span v-if="store.isProcessing">⏳ 处理中...</span>
        <span v-else>🔒 编码</span>
      </button>
      <button
        class="action-btn"
        :disabled="store.isProcessing || !store.inputText"
        @click="store.processEncoding(false)"
      >
        🔓 解码
      </button>
      <button
        class="action-btn"
        :disabled="!store.hasOutput"
        @click="store.swapInputOutput"
      >
        🔄 互换
      </button>
    </div>
  </div>
</template>

<style scoped>
.encoding-panel {
  display: flex;
  flex-wrap: wrap;
  gap: 16px;
  padding: 16px;
  background-color: var(--bg-secondary);
  border-radius: 8px;
  align-items: center;
}

.panel-row {
  display: flex;
  align-items: center;
  gap: 12px;
}

.field-label {
  font-size: 13px;
  font-weight: 500;
  color: var(--text-secondary);
  white-space: nowrap;
}

.type-selector {
  display: flex;
  gap: 4px;
}

.type-btn {
  padding: 6px 14px;
  font-size: 13px;
  border: 1px solid var(--border-color);
  background-color: var(--bg-card);
  border-radius: 6px;
  color: var(--text-secondary);
  transition: all 0.2s;
  cursor: pointer;
}

.type-btn:hover {
  background-color: var(--bg-hover);
  border-color: var(--border-strong);
}

.type-btn.active {
  background-color: var(--primary-color);
  border-color: var(--primary-color);
  color: var(--primary-contrast);
}

.panel-actions {
  display: flex;
  gap: 8px;
  margin-left: auto;
  flex-wrap: wrap;
}

.action-btn {
  padding: 8px 16px;
  font-size: 13px;
  border: 1px solid var(--border-color);
  background-color: var(--bg-card);
  border-radius: 6px;
  color: var(--text-secondary);
  transition: all 0.2s;
  cursor: pointer;
}

.action-btn:hover:not(:disabled) {
  background-color: var(--bg-hover);
  border-color: var(--border-strong);
}

.action-btn.primary {
  background-color: var(--primary-color);
  border-color: var(--primary-color);
  color: var(--primary-contrast);
}

.action-btn.primary:hover:not(:disabled) {
  background-color: var(--primary-hover);
  border-color: var(--primary-hover);
}

.action-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

@media (max-width: 768px) {
  .encoding-panel {
    flex-direction: column;
    align-items: stretch;
  }

  .panel-actions {
    margin-left: 0;
    width: 100%;
  }

  .action-btn {
    flex: 1;
  }
}
</style>
