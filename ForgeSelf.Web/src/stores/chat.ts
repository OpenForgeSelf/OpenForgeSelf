/**
 * 聊天状态管理Store
 */

import { ref, computed } from 'vue'
import { defineStore } from 'pinia'
import type { ChatMessage, Conversation } from '@/types/chat'
import { chatApi, generateId } from '@/services/api'
import { wsService } from '@/services/websocket'

export const useChatStore = defineStore('chat', () => {
  // 状态
  const messages = ref<ChatMessage[]>([])
  const conversations = ref<Conversation[]>([])
  const currentConversationId = ref<string | null>(null)
  const isLoading = ref(false)
  const isStreaming = ref(false)
  const isWsConnected = ref(false)
  const error = ref<string | null>(null)

  // 计算属性
  const currentMessages = computed(() => messages.value)
  const hasMessages = computed(() => messages.value.length > 0)
  const currentConversation = computed(() =>
    conversations.value.find(c => c.id === currentConversationId.value)
  )

  /**
   * 初始化WebSocket连接
   */
  async function initWebSocket(): Promise<void> {
    try {
      await wsService.connect({
        onOpen: () => {
          isWsConnected.value = true
          error.value = null
        },
        onClose: () => {
          isWsConnected.value = false
        },
        onError: () => {
          error.value = 'WebSocket连接失败'
          isWsConnected.value = false
        },
        onMessage: handleStreamMessage,
      })
    } catch (e) {
      console.error('WebSocket初始化失败:', e)
      error.value = '无法建立WebSocket连接'
    }
  }

  /**
   * 处理流式消息
   */
  function handleStreamMessage(chunk: { type: string; content?: string; messageId?: string; error?: string }): void {
    if (chunk.type === 'content' && chunk.content) {
      // 找到正在流式传输的消息并追加内容
      const streamingMsg = messages.value.find(m => m.isStreaming)
      if (streamingMsg) {
        streamingMsg.content += chunk.content
      }
    } else if (chunk.type === 'done') {
      // 标记流式传输完成
      const streamingMsg = messages.value.find(m => m.isStreaming)
      if (streamingMsg) {
        streamingMsg.isStreaming = false
      }
      isStreaming.value = false
    } else if (chunk.type === 'error') {
      error.value = chunk.error || '未知错误'
      isStreaming.value = false
      // 移除正在流式传输的消息
      const streamingMsgIndex = messages.value.findIndex(m => m.isStreaming)
      if (streamingMsgIndex !== -1) {
        messages.value.splice(streamingMsgIndex, 1)
      }
    }
  }

  /**
   * 发送消息
   *
   * 走 HTTP 非流式接口（POST /api/chat，经 e2e 验证真实可用）。
   * 后端 /ws WebSocket 仅用于聊天记录实时广播，不处理客户端上行消息，
   * 故不再通过 WebSocket 发送流式请求（该路径为静默空操作）。
   */
  async function sendMessage(content: string, chatModelId?: string): Promise<void> {
    if (!content.trim()) return

    error.value = null

    // 添加用户消息
    const userMessage: ChatMessage = {
      id: generateId(),
      role: 'user',
      content: content.trim(),
      timestamp: new Date(),
    }
    messages.value.push(userMessage)

    // 创建助手消息占位符
    const assistantMessage: ChatMessage = {
      id: generateId(),
      role: 'assistant',
      content: '',
      timestamp: new Date(),
      isStreaming: true,
    }
    messages.value.push(assistantMessage)

    isLoading.value = true
    isStreaming.value = true

    try {
      const response = await chatApi.sendMessage({
        content: content.trim(),
        conversationId: currentConversationId.value || undefined,
        chatModelId: chatModelId || undefined,
      })

      const isNewConversation = !currentConversationId.value
      if (isNewConversation) {
        currentConversationId.value = response.sessionId
      }

      // 用响应内容直接更新助手消息
      const msgIndex = messages.value.findIndex(m => m.id === assistantMessage.id)
      if (msgIndex !== -1) {
        messages.value[msgIndex] = {
          id: String(response.id),
          role: 'assistant',
          content: response.content,
          timestamp: new Date(response.createTime),
          isStreaming: false,
        }
      }

      // 新会话首条消息后刷新会话列表（标题/条数由后端生成）
      if (isNewConversation) {
        await loadConversations()
      }
    } catch (e) {
      console.error('发送消息失败:', e)
      error.value = e instanceof Error ? e.message : '发送消息失败'
      // 移除失败的助手消息
      const msgIndex = messages.value.findIndex(m => m.id === assistantMessage.id)
      if (msgIndex !== -1) {
        messages.value.splice(msgIndex, 1)
      }
    } finally {
      isLoading.value = false
      isStreaming.value = false
    }
  }

  /**
   * 创建新会话
   *
   * 后端无独立建会话端点：会话在首条消息发送时由 ChatController 自动创建。
   * 这里仅本地重置当前会话（与 AIAgent 插件同款模式）。
   */
  async function createNewConversation(): Promise<void> {
    currentConversationId.value = null
    messages.value = []
  }

  /**
   * 加载会话消息
   */
  async function loadConversation(conversationId: string): Promise<void> {
    try {
      isLoading.value = true
      currentConversationId.value = conversationId
      const loadedMessages = await chatApi.getConversationMessages(conversationId)
      messages.value = loadedMessages
    } catch (e) {
      console.error('加载会话失败:', e)
      error.value = '加载会话失败'
    } finally {
      isLoading.value = false
    }
  }

  /**
   * 加载所有会话
   */
  async function loadConversations(): Promise<void> {
    try {
      const loadedConversations = await chatApi.getConversations()
      conversations.value = loadedConversations.map(c => ({
        id: c.id,
        title: c.title,
        messages: [],
        createdAt: new Date(c.updatedAt),
        updatedAt: new Date(c.updatedAt),
      }))
    } catch (e) {
      console.error('加载会话列表失败:', e)
    }
  }

  /**
   * 删除会话
   */
  async function deleteConversation(conversationId: string): Promise<void> {
    try {
      await chatApi.deleteConversation(conversationId)
      conversations.value = conversations.value.filter(c => c.id !== conversationId)
      if (currentConversationId.value === conversationId) {
        currentConversationId.value = null
        messages.value = []
      }
    } catch (e) {
      console.error('删除会话失败:', e)
      error.value = '删除会话失败'
    }
  }

  /**
   * 清空当前会话消息
   */
  function clearMessages(): void {
    messages.value = []
  }

  /**
   * 清除错误
   */
  function clearError(): void {
    error.value = null
  }

  return {
    // 状态
    messages,
    conversations,
    currentConversationId,
    isLoading,
    isStreaming,
    isWsConnected,
    error,
    // 计算属性
    currentMessages,
    hasMessages,
    currentConversation,
    // 方法
    initWebSocket,
    sendMessage,
    createNewConversation,
    loadConversation,
    loadConversations,
    deleteConversation,
    clearMessages,
    clearError,
  }
})