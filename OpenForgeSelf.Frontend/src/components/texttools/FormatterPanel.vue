<script setup lang="ts">
import { useTextToolsStore } from '@/stores/textTools'
import type { FormatterType } from '@/types/textTools'

const store = useTextToolsStore()

const formatterTypes: { value: FormatterType; label: string }[] = [
  { value: 'json', label: 'JSON' },
  { value: 'xml', label: 'XML' },
  { value: 'html', label: 'HTML' }
]

const indentOptions = [2, 4, 8]
</script>

<template>
  <div class="formatter-panel">
    <div class="panel-row">
      <label class="field-label">类型</label>
      <div class="type-selector">
        <button
          v-for="type in formatterTypes"
          :key="type.value"
          class="type-btn"
          :class="{ active: store.formatterType === type.value }"
          @click="store.setFormatterType(type.value)"
        >
          {{ type.label }}
        </button>
      </div>
    </div>

    <div class="panel-row">
      <label class="field-label">缩进大小</label>
      <div class="indent-selector">
        <button
          v-for="size in indentOptions"
          :key="size"
          class="indent-btn"
          :class="{ active: store.indentSize === size }"
          @click="store.setIndentSize(size)"
        >
          {{ size }}
        </button>
      </div>
    </div>

    <div class="panel-actions">
      <button
        class="action-btn primary"
        :disabled="store.isProcessing || !store.inputText"
        @click="store.processFormat(true)"
      >
        <span v-if="store.isProcessing">⏳ 处理中...</span>
        <span v-else>✨ 格式化</span>
      </button>
      <button
        class="action-btn"
        :disabled="store.isProcessing || !store.inputText"
        @click="store.processFormat(false)"
      >
        📦 压缩
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
.formatter-panel {
  display: flex;
  flex-wrap: wrap;
  gap: 16px;
  padding: 16px;
  background-color: var(--bg-secondary);
  border-radius: 8px;
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

.type-selector,
.indent-selector {
  display: flex;
  gap: 4px;
}

.type-btn,
.indent-btn {
  padding: 6px 14px;
  font-size: 13px;
  border: 1px solid var(--border-color);
  background-color: var(--bg-card);
  border-radius: 6px;
  color: var(--text-secondary);
  transition: all 0.2s;
  cursor: pointer;
}

.type-btn:hover,
.indent-btn:hover {
  background-color: var(--bg-hover);
  border-color: var(--border-strong);
}

.type-btn.active,
.indent-btn.active {
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
  .formatter-panel {
    flex-direction: column;
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
