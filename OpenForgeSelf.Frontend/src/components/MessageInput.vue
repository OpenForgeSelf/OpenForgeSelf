<script setup lang="ts">
import { ref, computed } from 'vue'

const props = defineProps<{
  disabled?: boolean
  placeholder?: string
}>()

const emit = defineEmits<{
  send: [content: string]
}>()

const inputText = ref('')
const textareaRef = ref<HTMLTextAreaElement | null>(null)

const isDisabled = computed(() => props.disabled || !inputText.value.trim())

function handleSend() {
  if (isDisabled.value) return

  const content = inputText.value.trim()
  emit('send', content)
  inputText.value = ''

  // 重置文本框高度
  if (textareaRef.value) {
    textareaRef.value.style.height = 'auto'
  }
}

function handleKeydown(event: KeyboardEvent) {
  // Enter发送，Shift+Enter换行
  if (event.key === 'Enter' && !event.shiftKey) {
    event.preventDefault()
    handleSend()
  }
}

function adjustHeight() {
  if (textareaRef.value) {
    textareaRef.value.style.height = 'auto'
    textareaRef.value.style.height = `${Math.min(textareaRef.value.scrollHeight, 200)}px`
  }
}
</script>

<template>
  <div class="message-input">
    <div class="input-container">
      <textarea
        ref="textareaRef"
        v-model="inputText"
        :placeholder="placeholder || '输入消息... (Enter发送, Shift+Enter换行)'"
        :disabled="disabled"
        class="input-textarea"
        rows="1"
        @keydown="handleKeydown"
        @input="adjustHeight"
      />
      <button
        class="send-button"
        :class="{ 'send-button-disabled': isDisabled }"
        :disabled="isDisabled"
        title="发送消息"
        @click="handleSend"
      >
        <svg
          class="send-icon"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="2"
          stroke-linecap="round"
          stroke-linejoin="round"
        >
          <line x1="22" y1="2" x2="11" y2="13" />
          <polygon points="22 2 15 22 11 13 2 9 22 2" />
        </svg>
      </button>
    </div>
    <div class="input-hint">
      <span v-if="disabled" class="hint-text">AI正在回复中...</span>
      <span v-else class="hint-text">按 Enter 发送，Shift + Enter 换行</span>
    </div>
  </div>
</template>

<style scoped>
.message-input {
  padding: 16px 20px;
  background-color: var(--bg-secondary);
  border-top: 1px solid var(--border-color);
}

.input-container {
  display: flex;
  gap: 12px;
  align-items: flex-end;
  background-color: var(--bg-primary);
  border: 1px solid var(--border-color);
  border-radius: var(--radius-lg);
  padding: 12px 16px;
  transition: border-color var(--motion-fast), box-shadow var(--motion-fast);
}

.input-container:focus-within {
  border-color: var(--primary-color);
  box-shadow: 0 0 0 3px var(--primary-light);
}

.input-textarea {
  flex: 1;
  border: none;
  background: transparent;
  font-size: 15px;
  line-height: 1.5;
  resize: none;
  outline: none;
  font-family: inherit;
  color: var(--text-primary);
  max-height: 200px;
  overflow-y: auto;
}

.input-textarea::placeholder {
  color: var(--text-muted);
}

.input-textarea:disabled {
  cursor: not-allowed;
  opacity: 0.6;
}

.send-button {
  flex-shrink: 0;
  width: 40px;
  height: 40px;
  border: none;
  border-radius: var(--radius-md);
  background-color: var(--primary-color);
  color: var(--primary-contrast);
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: background-color var(--motion-fast), transform 0.1s ease;
}

.send-button:hover:not(.send-button-disabled) {
  background-color: var(--primary-hover);
}

.send-button:active:not(.send-button-disabled) {
  transform: scale(0.95);
}

.send-button-disabled {
  background-color: var(--bg-muted);
  cursor: not-allowed;
}

.send-icon {
  width: 20px;
  height: 20px;
}

.input-hint {
  margin-top: 8px;
  text-align: center;
}

.hint-text {
  font-size: 12px;
  color: var(--text-muted);
}
</style>