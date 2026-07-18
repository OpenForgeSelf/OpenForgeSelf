<script setup lang="ts">
import { computed } from 'vue'
import type { ChatMessage } from '@/types/chat'

const props = defineProps<{
  message: ChatMessage
}>()

const isUser = computed(() => props.message.role === 'user')
const isAssistant = computed(() => props.message.role === 'assistant')
const formattedTime = computed(() => {
  const date = new Date(props.message.timestamp)
  return date.toLocaleTimeString('zh-CN', { hour: '2-digit', minute: '2-digit' })
})

const roleLabel = computed(() => {
  switch (props.message.role) {
    case 'user':
      return '你'
    case 'assistant':
      return 'AI助手'
    case 'system':
      return '系统'
    default:
      return ''
  }
})
</script>

<template>
  <div
    class="message-item"
    :class="{
      'message-user': isUser,
      'message-assistant': isAssistant,
      'message-system': message.role === 'system',
    }"
  >
    <div class="message-avatar">
      <div class="avatar-icon" :class="{ 'avatar-user': isUser, 'avatar-assistant': isAssistant }">
        {{ isUser ? '👤' : '🤖' }}
      </div>
    </div>
    <div class="message-content">
      <div class="message-header">
        <span class="message-role">{{ roleLabel }}</span>
        <span class="message-time">{{ formattedTime }}</span>
      </div>
      <div class="message-text" :class="{ 'streaming': message.isStreaming }">
        {{ message.content || '...' }}
        <span v-if="message.isStreaming" class="cursor-blink">▌</span>
      </div>
    </div>
  </div>
</template>

<style scoped>
.message-item {
  display: flex;
  gap: 12px;
  padding: 16px 20px;
  border-radius: var(--radius-md);
  margin-bottom: 12px;
  transition: background-color var(--motion-fast);
}

.message-user {
  background-color: var(--primary-soft);
  margin-left: 40px;
}

.message-assistant {
  background-color: var(--bg-tertiary);
  margin-right: 40px;
}

.message-system {
  background-color: rgba(217, 119, 6, 0.12);
  margin: 0 40px;
}

.message-avatar {
  flex-shrink: 0;
}

.avatar-icon {
  width: 36px;
  height: 36px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 18px;
}

.avatar-user {
  background-color: var(--primary-light);
}

.avatar-assistant {
  background-color: rgba(16, 185, 129, 0.15);
}

.message-content {
  flex: 1;
  min-width: 0;
}

.message-header {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 6px;
}

.message-role {
  font-weight: 600;
  font-size: 14px;
  color: var(--text-primary);
}

.message-time {
  font-size: 12px;
  color: var(--text-muted);
}

.message-text {
  font-size: 15px;
  line-height: 1.6;
  color: var(--text-primary);
  word-wrap: break-word;
  white-space: pre-wrap;
}

.message-text.streaming {
  color: var(--text-secondary);
}

.cursor-blink {
  animation: blink 1s infinite;
  color: var(--primary-color);
  font-weight: bold;
}

@keyframes blink {
  0%, 50% {
    opacity: 1;
  }
  51%, 100% {
    opacity: 0;
  }
}
</style>