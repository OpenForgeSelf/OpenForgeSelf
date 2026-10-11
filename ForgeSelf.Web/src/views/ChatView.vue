<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useChatStore } from '@/stores/chat'
import { aiModelsApi } from '@/services/aiModelsApi'
import type { AIModel } from '@/types/aiModel'
import MessageList from '@/components/MessageList.vue'
import MessageInput from '@/components/MessageInput.vue'
import PageHead from '@/components/PageHead.vue'

const chatStore = useChatStore()

// 侧边栏显示状态
const showSidebar = ref(false)

// 错误提示显示状态
const showError = ref(false)

// 可选模型列表（从后端 /api/ai-models 动态加载，仅启用项）
const models = ref<AIModel[]>([])
const selectedModelId = ref<string>('')
const MODEL_STORAGE_KEY = 'forgeself-chat-current-model'

/** 当前选中模型的展示名（别名优先，无选中时显示默认） */
const selectedModelName = computed(() => {
  const found = models.value.find(m => m.chatModelId === selectedModelId.value)
  return found ? (found.alias || found.upstreamModelId) : '默认模型'
})

// 监听错误 + 加载模型列表
onMounted(async () => {
  // 初始化WebSocket连接
  try {
    await chatStore.initWebSocket()
  } catch (error) {
    console.error('WebSocket初始化失败，将使用HTTP API作为后备:', error)
  }

  // 加载会话列表
  await chatStore.loadConversations()

  // 加载可选模型列表（失败不阻塞聊天，走后端默认模型）
  try {
    const groups = await aiModelsApi.list({ enabledOnly: true })
    models.value = groups.flatMap(g => g.models)
    const saved = localStorage.getItem(MODEL_STORAGE_KEY)
    if (saved && models.value.some(m => m.chatModelId === saved)) {
      selectedModelId.value = saved
    } else if (models.value.length > 0) {
      selectedModelId.value = models.value[0].chatModelId
    }
  } catch (error) {
    console.error('加载模型列表失败，将使用后端默认模型:', error)
  }
})

// 切换模型（v-model 已更新选中值，此处仅持久化）
function persistModelChoice() {
  if (selectedModelId.value) {
    localStorage.setItem(MODEL_STORAGE_KEY, selectedModelId.value)
  }
}

// 发送消息（携带所选模型）
async function handleSend(content: string) {
  await chatStore.sendMessage(content, selectedModelId.value || undefined)
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

    <!-- PageHead -->
    <PageHead
      eyebrow="Chat · 对话"
      title="AI 对话"
    >
      <template #actions>
        <span class="status-chip" :class="{ connected: chatStore.isWsConnected, disconnected: !chatStore.isWsConnected }">
          <span class="status-dot" />
          {{ chatStore.isWsConnected ? '就绪' : '离线' }}
        </span>
        <select
          v-model="selectedModelId"
          class="model-select"
          :title="selectedModelName"
          @change="persistModelChoice"
        >
          <option value="">默认模型</option>
          <option v-for="model in models" :key="model.chatModelId" :value="model.chatModelId">
            {{ model.alias || model.upstreamModelId }}
          </option>
        </select>
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
      </template>
    </PageHead>

    <!-- 聊天主区 -->
    <section class="chat-anvil">
      <!-- Chat Header (in-anvil toolbar) -->
      <div class="chat-header">
        <button class="menu-button" title="切换侧边栏" @click="toggleSidebar">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <line x1="3" y1="12" x2="21" y2="12" />
            <line x1="3" y1="6" x2="21" y2="6" />
            <line x1="3" y1="18" x2="21" y2="18" />
          </svg>
        </button>
        <span class="chat-agent-name">AI Agent</span>
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
   ChatView — 纯聊天页（冻结模式）
   ============================ */

.chat-view {
  display: flex;
  flex-direction: column;
  height: 100%;
  padding: 20px 24px;
  overflow: hidden;
}

/* ============================
   状态 chip（PageHead actions）
   ============================ */
.status-chip {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 4px 10px;
  font-size: 11px;
  font-family: var(--font-mono);
  border-radius: var(--radius-md);
}

.status-chip.connected {
  color: var(--bronze-500);
  background: var(--el-color-success-light-9);
}

.status-chip.disconnected {
  color: var(--cinnabar-400);
  background: color-mix(in srgb, var(--cinnabar-400) 10%, transparent);
}

.status-chip .status-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: currentColor;
}

.model-select {
  padding: 4px 10px;
  font-size: 11px;
  color: var(--fg-secondary);
  background: var(--glass-bg);
  border: 1px solid var(--border-default);
  border-radius: var(--radius-md);
  font-family: var(--font-mono);
  white-space: nowrap;
  cursor: pointer;
  max-width: 200px;
  outline: none;
  transition: border-color var(--dur-fast) var(--ease-out);
}

.model-select:hover,
.model-select:focus {
  border-color: var(--gold-400);
}

.stats-badge {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 4px 10px;
  font-size: 11px;
  color: var(--fg-tertiary);
  background: var(--paper-tint);
  border-radius: var(--radius-md);
  font-family: var(--font-mono);
  white-space: nowrap;
}

.stats-badge svg {
  flex-shrink: 0;
  color: var(--fg-tertiary);
}

/* ============================
   聊天主区（玻璃卡）
   ============================ */
.chat-anvil {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
  max-width: 1200px;
  width: 100%;
  margin: 0 auto;
  border: var(--glass-border);
  border-radius: var(--glass-radius);
  background: var(--glass-bg);
  backdrop-filter: blur(var(--glass-blur)) saturate(var(--glass-saturate));
  -webkit-backdrop-filter: blur(var(--glass-blur)) saturate(var(--glass-saturate));
  box-shadow: var(--glass-shadow);
  overflow: hidden;
  transition: border-color var(--dur-base) var(--ease-out);
}

.chat-anvil:focus-within {
  border-color: var(--border-brand);
}

.chat-header {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 8px 16px;
  flex-shrink: 0;
  border-bottom: 1px solid var(--border-hairline);
}

.menu-button {
  width: 28px;
  height: 28px;
  border: none;
  background: transparent;
  border-radius: var(--radius-sm);
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--fg-secondary);
  transition: background var(--dur-fast) var(--ease-out), color var(--dur-fast) var(--ease-out);
}

.menu-button:hover {
  background: var(--paper-tint);
  color: var(--fg-primary);
}

.menu-button svg {
  width: 16px;
  height: 16px;
}

.chat-agent-name {
  font-size: 13px;
  font-weight: 600;
  color: var(--fg-primary);
  white-space: nowrap;
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
  background: var(--ink-veil);
  z-index: 100;
}

.sidebar {
  position: fixed;
  top: 0;
  left: 0;
  bottom: 0;
  width: 280px;
  background: var(--el-bg-color-overlay);
  border-right: 1px solid var(--border-default);
  box-shadow: var(--shadow-lg);
  display: flex;
  flex-direction: column;
  z-index: 101;
}

.sidebar-header {
  padding: 16px;
  border-bottom: 1px solid var(--border-hairline);
}

.new-chat-button {
  width: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  padding: 10px 16px;
  background: var(--gold-400);
  color: var(--ink-950);
  border: none;
  border-radius: var(--radius-md);
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: background var(--dur-fast) var(--ease-out);
}

.new-chat-button:hover {
  background: var(--gold-300);
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
  padding: 10px 14px;
  border-radius: var(--radius-md);
  cursor: pointer;
  transition: background var(--dur-fast) var(--ease-out);
  margin-bottom: 4px;
}

.conversation-item:hover {
  background: var(--paper-tint);
}

.conversation-item.active {
  background: var(--gold-tint);
}

.conversation-title {
  flex: 1;
  font-size: 13px;
  color: var(--fg-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.delete-button {
  width: 28px;
  height: 28px;
  border: none;
  background: transparent;
  border-radius: var(--radius-sm);
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  opacity: 0;
  transition: opacity var(--dur-fast) var(--ease-out), background var(--dur-fast) var(--ease-out);
}

.conversation-item:hover .delete-button {
  opacity: 1;
}

.delete-button:hover {
  background: color-mix(in srgb, var(--cinnabar-400) 15%, transparent);
}

.delete-button svg {
  width: 16px;
  height: 16px;
  color: var(--cinnabar-400);
}

.empty-conversations {
  text-align: center;
  padding: 40px 20px;
  color: var(--fg-tertiary);
  font-size: 13px;
}

/* ============================
   错误提示
   ============================ */
.error-banner {
  position: fixed;
  top: 60px;
  left: 50%;
  transform: translateX(-50%);
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px 18px;
  background: color-mix(in srgb, var(--cinnabar-400) 12%, transparent);
  border: 1px solid color-mix(in srgb, var(--cinnabar-400) 30%, transparent);
  border-radius: var(--radius-md);
  box-shadow: var(--shadow-md);
  z-index: 200;
}

.error-message {
  font-size: 13px;
  color: var(--cinnabar-400);
}

.error-close {
  width: 24px;
  height: 24px;
  border: none;
  background: transparent;
  cursor: pointer;
  font-size: 14px;
  color: var(--cinnabar-400);
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 4px;
  transition: background var(--dur-fast) var(--ease-out);
}

.error-close:hover {
  background: color-mix(in srgb, var(--cinnabar-400) 15%, transparent);
}

/* ============================
   动画
   ============================ */
.sidebar-enter-active,
.sidebar-leave-active {
  transition: opacity var(--dur-base) var(--ease-out);
}

.sidebar-enter-active .sidebar,
.sidebar-leave-active .sidebar {
  transition: transform var(--dur-base) var(--ease-out);
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
  transition: opacity var(--dur-base) var(--ease-out), transform var(--dur-base) var(--ease-out);
}

.error-enter-from,
.error-leave-to {
  opacity: 0;
  transform: translateX(-50%) translateY(-20px);
}

/* ============================
   响应式
   ============================ */
@media (max-width: 1023px) {
  .chat-view {
    padding: 12px;
  }
}
</style>
