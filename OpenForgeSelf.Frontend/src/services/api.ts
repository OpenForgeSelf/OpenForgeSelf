/**
 * API服务层 - 封装与后端通信
 */

import type { ChatMessage, SendMessageRequest, SendMessageResponse } from '@/types/chat'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api'

/**
 * 生成唯一ID
 */
function generateId(): string {
  return `${Date.now()}-${Math.random().toString(36).substring(2, 9)}`
}

/**
 * API服务类
 */
export const chatApi = {
  /**
   * 发送消息（非流式）
   */
  async sendMessage(request: SendMessageRequest): Promise<SendMessageResponse> {
    const response = await fetch(`${API_BASE_URL}/chat/message`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(request),
    })

    if (!response.ok) {
      const error = await response.json().catch(() => ({ message: '请求失败' }))
      throw new Error(error.message || `HTTP error: ${response.status}`)
    }

    return response.json()
  },

  /**
   * 获取会话历史消息
   */
  async getConversationMessages(conversationId: string): Promise<ChatMessage[]> {
    const response = await fetch(`${API_BASE_URL}/chat/conversation/${conversationId}/messages`)

    if (!response.ok) {
      throw new Error(`获取消息失败: ${response.status}`)
    }

    const data = await response.json()
    return data.map((msg: ChatMessage) => ({
      ...msg,
      timestamp: new Date(msg.timestamp),
    }))
  },

  /**
   * 获取所有会话列表
   */
  async getConversations(): Promise<{ id: string; title: string; updatedAt: string }[]> {
    const response = await fetch(`${API_BASE_URL}/chat/conversations`)

    if (!response.ok) {
      throw new Error(`获取会话列表失败: ${response.status}`)
    }

    return response.json()
  },

  /**
   * 创建新会话
   */
  async createConversation(title?: string): Promise<{ id: string }> {
    const response = await fetch(`${API_BASE_URL}/chat/conversation`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ title: title || '新对话' }),
    })

    if (!response.ok) {
      throw new Error(`创建会话失败: ${response.status}`)
    }

    return response.json()
  },

  /**
   * 删除会话
   */
  async deleteConversation(conversationId: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/chat/conversation/${conversationId}`, {
      method: 'DELETE',
    })

    if (!response.ok) {
      throw new Error(`删除会话失败: ${response.status}`)
    }
  },
}

export { generateId }