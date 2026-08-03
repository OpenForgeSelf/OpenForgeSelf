<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useChatStore } from '@/stores/chat'
import MessageList from '@/components/MessageList.vue'
import MessageInput from '@/components/MessageInput.vue'

const chatStore = useChatStore()

// 侧边栏显示状态
const showSidebar = ref(false)

// 错误提示显示状态
const showError = ref(false)

// 监听错误
onMounted(async () => {
  // 初始化WebSocket连接
  try {
    await chatStore.initWebSocket()
  } catch (error) {
    console.error('WebSocket初始化失败，将使用HTTP API作为后备:', error)
  }

  // 加载会话列表
  await chatStore.loadConversations()
})

// 发送消息
async function handleSend(content: string) {
  await chatStore.sendMessage(content)
}

// 切换侧边栏
function toggleSidebar() {
  showSidebar.value = !showSidebar.value
}

// 创建新会话
async function handleNewChat() {
  await chatStore.createNewConversation()
  showSidebar.value = false
}

// 选择会话
async function handleSelectConversation(conversationId: string) {
  await chatStore.loadConversation(conversationId)
  showSidebar.value = false
}

// 删除会话
async function handleDeleteConversation(conversationId: string, event: Event) {
  event.stopPropagation()
  if (confirm('确定要删除这个对话吗？')) {
    await chatStore.deleteConversation(conversationId)
  }
}

// 清除错误
function handleClearError() {
  chatStore.clearError()
  showError.value = false
}
</script>

<template>
  <div class="chat-view">
    <!-- 错误提示 -->
    <Transition name="error">
      <div v-if="chatStore.error && showError" class="error-banner">
        <span class="error-message">{{ chatStore.error }}</span>
        <button class="error-close" @click="handleClearError">✕</button>
      </div>
    </Transition>

    <!-- 侧边栏（会话列表） -->
    <Transition name="sidebar">
      <div v-if="showSidebar" class="sidebar-overlay" @click="showSidebar = false">
        <aside class="sidebar" @click.stop>
          <div class="sidebar-header">
            <button class="new-chat-button" @click="handleNewChat">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <line x1="12" y1="5" x2="12" y2="19" />
                <line x1="5" y1="12" x2="19" y2="12" />
              </svg>
              新对话
            </button>
          </div>
          <div class="sidebar-content">
            <div
              v-for="conversation in chatStore.conversations"
              :key="conversation.id"
              class="conversation-item"
              :class="{ 'active': conversation.id === chatStore.currentConversationId }"
              @click="handleSelectConversation(conversation.id)"
            >
              <span class="conversation-title">{{ conversation.title }}</span>
              <button
                class="delete-button"
                title="删除对话"
                @click="handleDeleteConversation(conversation.id, $event)"
              >
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <polyline points="3 6 5 6 21 6" />
                  <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
                </svg>
              </button>
            </div>
            <div v-if="chatStore.conversations.length === 0" class="empty-conversations">
              暂无对话记录
            </div>
          </div>
        </aside>
      </div>
    </Transition>

    <!-- 聊天主区 -->
    <section class="chat-anvil">
      <!-- Chat Header -->
      <div class="chat-header">
        <div class="chat-header-left">
          <button class="menu-button" title="切换侧边栏" @click="toggleSidebar">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <line x1="3" y1="12" x2="21" y2="12" />
              <line x1="3" y1="6" x2="21" y2="6" />
              <line x1="3" y1="18" x2="21" y2="18" />
            </svg>
          </button>
          <div class="chat-title">
            <div class="chat-status-indicator">
              <span class="status-dot" :class="{ connected: chatStore.isWsConnected, disconnected: !chatStore.isWsConnected }" />
              <span class="chat-agent-name">AI Agent</span>
            </div>
            <span class="chat-status-text" :class="{ connected: chatStore.isWsConnected }">
              {{ chatStore.isWsConnected ? '就绪' : '离线' }}
            </span>
          </div>
        </div>
        <div class="chat-header-right">
          <span class="model-badge">
            <svg
              width="10"
              height="10"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
            >
              <rect
                x="4"
                y="4"
                width="16"
                height="16"
                rx="2"
              />
              <rect x="9" y="9" width="6" height="6" />
            </svg>
            Gemma 2B
          </span>
          <span class="stats-badge">
            <svg
              width="10"
              height="10"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
            >
              <line x1="4" y1="9" x2="20" y2="9" />
              <line x1="4" y1="15" x2="20" y2="15" />
              <line x1="10" y1="3" x2="8" y2="21" />
              <line x1="16" y1="3" x2="14" y2="21" />
            </svg>
            {{ chatStore.messages.length }} 条消息
          </span>
        </div>
      </div>

      <!-- Message List -->
      <div class="chat-messages">
        <MessageList
          :messages="chatStore.messages"
          :is-loading="chatStore.isLoading && !chatStore.isStreaming"
        />
      </div>

      <!-- Chat Input -->
      <div class="chat-input-area">
        <MessageInput
          :disabled="chatStore.isLoading || chatStore.isStreaming"
          @send="handleSend"
        />
      </div>
    </section>
  </div>
</template>

<style scoped>
/* ============================
   ChatView — 纯聊天页
   侧边栏 + 聊天头部 + 消息列表 + 输入区
   ============================ */

.chat-view {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: var(--el-bg-color);
  position: relative;
  overflow: hidden;
  padding: 16px 20px;
}

/* ============================
   聊天主区（琥珀色边框）
   ============================ */
.chat-anvil {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
  max-width: 1200px;
  width: 100%;
  margin: 0 auto;
  background: linear-gradient(135deg, var(--el-bg-color-page) 0%, var(--el-fill-color-light) 100%);
  border: 1px solid var(--el-border-color);
  border-left: 3px solid var(--el-color-primary);
  border-radius: 12px;
  overflow: hidden;
  box-shadow: 0 0 50px rgba(245, 158, 11, 0.06), inset 0 1px 0 rgba(245, 158, 11, 0.04);
  transition: border-color 250ms ease, box-shadow 250ms ease;
}

.chat-anvil:focus-within {
  border-color: var(--el-color-primary);
  box-shadow: 0 0 60px rgba(245, 158, 11, 0.1), inset 0 1px 0 rgba(245, 158, 11, 0.06);
}

.chat-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 16px;
  flex-shrink: 0;
  border-bottom: 1px solid var(--el-border-color);
}

.chat-header-left {
  display: flex;
  align-items: center;
  gap: 10px;
}

.menu-button {
  width: 28px;
  height: 28px;
  border: none;
  background: transparent;
  border-radius: var(--el-border-radius-small);
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--el-text-color-regular);
  transition: background 150ms ease, color 150ms ease;
}

.menu-button:hover {
  background: var(--el-fill-color);
  color: var(--el-text-color-primary);
}

.menu-button svg {
  width: 16px;
  height: 16px;
}

.chat-title {
  display: flex;
  align-items: center;
  gap: 10px;
}

.chat-status-indicator {
  display: flex;
  align-items: center;
  gap: 6px;
}

.status-dot {
  width: 8px;
  height: 8px;
  border-radius: 9999px;
  flex-shrink: 0;
}

.status-dot.connected {
  background: var(--el-color-success);
  box-shadow: 0 0 6px rgba(63, 185, 80, 0.4);
}

.status-dot.disconnected {
  background: var(--el-color-danger);
  box-shadow: 0 0 6px rgba(248, 81, 73, 0.4);
}

.chat-agent-name {
  font-size: 0.875rem;
  font-weight: 600;
  color: var(--el-text-color-primary);
  white-space: nowrap;
}

.chat-status-text {
  font-size: 0.75rem;
  white-space: nowrap;
}

.chat-status-text.connected {
  color: var(--el-color-success);
}

.chat-status-text:not(.connected) {
  color: var(--el-color-danger);
}

.chat-header-right {
  display: flex;
  align-items: center;
  gap: 8px;
}

.model-badge {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 2px 8px;
  font-size: 0.75rem;
  color: var(--el-text-color-secondary);
  background: var(--el-fill-color-light);
  border-radius: var(--el-border-radius-small);
  font-family: var(--font-family-mono);
  white-space: nowrap;
}

.model-badge svg {
  flex-shrink: 0;
  color: var(--el-color-primary);
}

.stats-badge {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 2px 8px;
  font-size: 0.75rem;
  color: var(--el-text-color-secondary);
  background: var(--el-fill-color-light);
  border-radius: var(--el-border-radius-small);
  font-family: var(--font-family-mono);
  white-space: nowrap;
}

.stats-badge svg {
  flex-shrink: 0;
  color: var(--el-text-color-secondary);
}

.chat-messages {
  flex: 1;
  overflow: hidden;
  display: flex;
  flex-direction: column;
  min-height: 0;
}

.chat-messages :deep(.message-list) {
  flex: 1;
}

.chat-input-area {
  flex-shrink: 0;
}

/* ============================
   侧边栏（会话列表）
   ============================ */
.sidebar-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background-color: rgba(0, 0, 0, 0.5);
  z-index: 100;
}

.sidebar {
  position: fixed;
  top: 0;
  left: 0;
  bottom: 0;
  width: 280px;
  background-color: var(--el-bg-color-page);
  box-shadow: 2px 0 8px rgba(0, 0, 0, 0.1);
  display: flex;
  flex-direction: column;
  z-index: 101;
}

.sidebar-header {
  padding: 16px;
  border-bottom: 1px solid var(--el-border-color);
}

.new-chat-button {
  width: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  padding: 12px 16px;
  background-color: var(--el-color-primary);
  color: var(--el-color-white);
  border: none;
  border-radius: var(--el-border-radius-small);
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: background-color 150ms ease;
}

.new-chat-button:hover {
  background-color: var(--el-color-primary-light-3);
}

.new-chat-button svg {
  width: 18px;
  height: 18px;
}

.sidebar-content {
  flex: 1;
  overflow-y: auto;
  padding: 8px;
}

.conversation-item {
  display: flex;
  align-items: center;
  padding: 12px 16px;
  border-radius: var(--el-border-radius-small);
  cursor: pointer;
  transition: background-color 150ms ease;
  margin-bottom: 4px;
}

.conversation-item:hover {
  background-color: var(--el-fill-color);
}

.conversation-item.active {
  background-color: var(--el-color-primary-light-9);
}

.conversation-title {
  flex: 1;
  font-size: 14px;
  color: var(--el-text-color-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.delete-button {
  width: 28px;
  height: 28px;
  border: none;
  background: transparent;
  border-radius: var(--el-border-radius-small);
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  opacity: 0;
  transition: opacity 150ms ease, background-color 150ms ease;
}

.conversation-item:hover .delete-button {
  opacity: 1;
}

.delete-button:hover {
  background-color: rgba(248, 81, 73, 0.15);
}

.delete-button svg {
  width: 16px;
  height: 16px;
  color: var(--el-color-danger);
}

.empty-conversations {
  text-align: center;
  padding: 40px 20px;
  color: var(--el-text-color-secondary);
  font-size: 14px;
}

/* ============================
   错误提示
   ============================ */
.error-banner {
  position: fixed;
  top: 54px;
  left: 50%;
  transform: translateX(-50%);
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px 20px;
  background-color: rgba(185, 28, 28, 0.12);
  border: 1px solid rgba(185, 28, 28, 0.3);
  border-radius: var(--el-border-radius-small);
  box-shadow: 0 4px 6px rgba(0, 0, 0, 0.07);
  z-index: 200;
}

.error-message {
  font-size: 14px;
  color: var(--el-color-danger);
}

.error-close {
  width: 24px;
  height: 24px;
  border: none;
  background: transparent;
  cursor: pointer;
  font-size: 16px;
  color: var(--el-color-danger);
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 4px;
  transition: background-color 150ms ease;
}

.error-close:hover {
  background-color: rgba(185, 28, 28, 0.15);
}

/* ============================
   动画
   ============================ */
.sidebar-enter-active,
.sidebar-leave-active {
  transition: opacity 0.3s ease;
}

.sidebar-enter-active .sidebar,
.sidebar-leave-active .sidebar {
  transition: transform 0.3s ease;
}

.sidebar-enter-from,
.sidebar-leave-to {
  opacity: 0;
}

.sidebar-enter-from .sidebar,
.sidebar-leave-to .sidebar {
  transform: translateX(-100%);
}

.error-enter-active,
.error-leave-active {
  transition: opacity 0.3s ease, transform 0.3s ease;
}

.error-enter-from,
.error-leave-to {
  opacity: 0;
  transform: translateX(-50%) translateY(-20px);
}

/* ============================
   响应式：小屏适配
   ============================ */
@media (max-width: 1023px) {
  .chat-view {
    padding: 12px;
  }

  .chat-anvil {
    min-height: 400px;
  }
}
</style>
