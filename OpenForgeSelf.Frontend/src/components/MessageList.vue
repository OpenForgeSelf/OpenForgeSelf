<script setup lang="ts">
import { ref, watch, nextTick } from 'vue'
import type { ChatMessage } from '@/types/chat'
import MessageItem from './MessageItem.vue'

const props = defineProps<{
  messages: ChatMessage[]
  isLoading?: boolean
}>()

const messagesContainer = ref<HTMLElement | null>(null)

// 监听消息变化，自动滚动到底部
watch(
  () => props.messages.length,
  async () => {
    await nextTick()
    scrollToBottom()
  }
)

// 监听流式消息内容变化
watch(
  () => props.messages[props.messages.length - 1]?.content,
  async () => {
    const lastMessage = props.messages[props.messages.length - 1]
    if (lastMessage?.isStreaming) {
      await nextTick()
      scrollToBottom()
    }
  }
)

function scrollToBottom() {
  if (messagesContainer.value) {
    messagesContainer.value.scrollTop = messagesContainer.value.scrollHeight
  }
}

defineExpose({
  scrollToBottom,
})
</script>

<template>
  <div ref="messagesContainer" class="message-list">
    <div v-if="messages.length === 0" class="empty-state">
      <div class="empty-icon">💬</div>
      <h3 class="empty-title">开始对话</h3>
      <p class="empty-description">在下方输入框中输入消息，开始与AI助手对话</p>
    </div>

    <TransitionGroup v-else name="message" tag="div" class="messages-container">
      <MessageItem
        v-for="message in messages"
        :key="message.id"
        :message="message"
      />
    </TransitionGroup>

    <div v-if="isLoading" class="loading-indicator">
      <div class="loading-dots">
        <span />
        <span />
        <span />
      </div>
      <span class="loading-text">AI正在思考...</span>
    </div>
  </div>
</template>

<style scoped>
.message-list {
  flex: 1;
  overflow-y: auto;
  padding: 20px;
  display: flex;
  flex-direction: column;
}

.empty-state {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  color: var(--el-text-color-secondary);
  text-align: center;
  padding: 40px;
}

.empty-icon {
  font-size: 64px;
  margin-bottom: 20px;
}

.empty-title {
  font-size: 24px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin-bottom: 12px;
}

.empty-description {
  font-size: 16px;
  color: var(--el-text-color-secondary);
  max-width: 400px;
}

.messages-container {
  display: flex;
  flex-direction: column;
}

.loading-indicator {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 16px 20px;
  background-color: var(--el-fill-color-light);
  border-radius: var(--el-border-radius-base);
  margin: 12px 40px;
}

.loading-dots {
  display: flex;
  gap: 4px;
}

.loading-dots span {
  width: 8px;
  height: 8px;
  background-color: var(--el-color-primary);
  border-radius: 50%;
  animation: bounce 1.4s infinite ease-in-out both;
}

.loading-dots span:nth-child(1) {
  animation-delay: -0.32s;
}

.loading-dots span:nth-child(2) {
  animation-delay: -0.16s;
}

@keyframes bounce {
  0%, 80%, 100% {
    transform: scale(0);
  }
  40% {
    transform: scale(1);
  }
}

.loading-text {
  font-size: 14px;
  color: var(--el-text-color-secondary);
}

/* 消息动画 */
.message-enter-active {
  transition: all 0.3s ease-out;
}

.message-leave-active {
  transition: all 0.2s ease-in;
}

.message-enter-from {
  opacity: 0;
  transform: translateY(20px);
}

.message-leave-to {
  opacity: 0;
  transform: translateY(-10px);
}
</style>