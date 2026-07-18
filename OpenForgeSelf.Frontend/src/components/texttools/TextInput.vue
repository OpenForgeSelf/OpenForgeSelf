<script setup lang="ts">
import { computed } from 'vue'

const props = defineProps<{
  modelValue: string
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: string): void
  (e: 'clear'): void
}>()

const charCount = computed(() => props.modelValue.length)

async function handlePaste(): Promise<void> {
  try {
    const text = await navigator.clipboard.readText()
    emit('update:modelValue', text)
  } catch (e) {
    console.error('粘贴失败:', e)
  }
}

function handleClear(): void {
  emit('update:modelValue', '')
  emit('clear')
}

function handleInput(event: Event): void {
  const target = event.target as HTMLTextAreaElement
  emit('update:modelValue', target.value)
}
</script>

<template>
  <div class="text-input">
    <div class="input-header">
      <span class="input-title">输入</span>
      <div class="input-actions">
        <span class="char-count">{{ charCount }} 字符</span>
        <button class="action-btn" title="粘贴" @click="handlePaste">
          📋 粘贴
        </button>
        <button
          class="action-btn"
          title="清空"
          :disabled="!modelValue"
          @click="handleClear"
        >
          🗑️ 清空
        </button>
      </div>
    </div>
    <textarea
      class="input-textarea"
      :value="modelValue"
      placeholder="在此输入或粘贴文本..."
      @input="handleInput"
    />
  </div>
</template>

<style scoped>
.text-input {
  display: flex;
  flex-direction: column;
  height: 100%;
  background-color: var(--bg-card);
  border: 1px solid var(--border-color);
  border-radius: 8px;
  overflow: hidden;
}

.input-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 10px 12px;
  background-color: var(--bg-secondary);
  border-bottom: 1px solid var(--border-color);
}

.input-title {
  font-weight: 500;
  font-size: 14px;
  color: var(--text-secondary);
}

.input-actions {
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

.input-textarea {
  flex: 1;
  width: 100%;
  padding: 12px;
  border: none;
  resize: none;
  font-family: 'Consolas', 'Monaco', 'Courier New', monospace;
  font-size: 13px;
  line-height: 1.6;
  color: var(--text-primary);
  background-color: var(--bg-card);
}

.input-textarea::placeholder {
  color: var(--text-muted);
}

.input-textarea:focus {
  outline: none;
}
</style>
