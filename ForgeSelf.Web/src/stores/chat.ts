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
   */
  async function sendMessage(content: string): Promise<void> {
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
      // 优先使用WebSocket流式传输
      if (wsService.isConnected()) {
        wsService.sendStreamRequest(content.trim(), currentConversationId.value || undefined)
      } else {
        // 回退到HTTP API
        const response = await chatApi.sendMessage({
          content: content.trim(),
          conversationId: currentConversationId.value || undefined,
        })

        if (!currentConversationId.value) {
          currentConversationId.value = response.conversationId
        }

        // 更新助手消息
        const msgIndex = messages.value.findIndex(m => m.id === assistantMessage.id)
        if (msgIndex !== -1) {
          // 获取完整的助手回复
          const conversationMessages = await chatApi.getConversationMessages(response.conversationId)
          const lastAssistantMsg = [...conversationMessages].reverse().find(m => m.role === 'assistant')
          if (lastAssistantMsg) {
            messages.value[msgIndex] = {
              ...lastAssistantMsg,
              isStreaming: false,
            }
          }
        }
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
    }
  }

  /**
   * 创建新会话
   */
  async function createNewConversation(title?: string): Promise<void> {
    try {
      const result = await chatApi.createConversation(title)
      currentConversationId.value = result.id
      messages.value = []
      conversations.value.unshift({
        id: result.id,
        title: title || '新对话',
        messages: [],
        createdAt: new Date(),
        updatedAt: new Date(),
      })
    } catch (e) {
      console.error('创建会话失败:', e)
      error.value = '创建会话失败'
    }
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