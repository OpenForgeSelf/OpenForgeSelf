/**
 * API服务层 - 封装与后端通信
 */

import type { ChatMessage, MessageRole, SendMessageRequest, SendMessageResponse } from '@/types/chat'

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
    // 后端 ChatController 路由为 [Route("api/[controller]")] + [HttpPost] → POST /api/chat
    // ChatRequest 契约：{ message, sessionId, stream, chatModelId }
    const response = await fetch(`${API_BASE_URL}/chat`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({
        message: request.content,
        sessionId: request.conversationId,
        stream: false,
        chatModelId: request.chatModelId,
      }),
    })

    if (!response.ok) {
      const error = await response.json().catch(() => ({ message: '请求失败' }))
      throw new Error(error.message || error.error || `HTTP error: ${response.status}`)
    }

    return response.json()
  },

  /**
   * 获取会话历史消息（真实路由：GET /api/chat/history/{sessionId}）
   */
  async getConversationMessages(conversationId: string): Promise<ChatMessage[]> {
    const response = await fetch(`${API_BASE_URL}/chat/history/${conversationId}`)

    if (!response.ok) {
      throw new Error(`获取消息失败: ${response.status}`)
    }

    const data = await response.json() as Array<{
      id: number
      sessionId: string
      role: MessageRole
      content: string
      createTime: string
    }>
    return data.map((msg) => ({
      id: String(msg.id),
      role: msg.role,
      content: msg.content,
      timestamp: new Date(msg.createTime),
    }))
  },

  /**
   * 获取所有会话列表（真实路由：GET /api/chat-sessions，仅 app 自有聊天）
   */
  async getConversations(): Promise<{ id: string; title: string; updatedAt: string }[]> {
    const response = await fetch(`${API_BASE_URL}/chat-sessions?source=App`)

    if (!response.ok) {
      throw new Error(`获取会话列表失败: ${response.status}`)
    }

    const json = await response.json() as {
      success: boolean
      data: Array<{ sessionKey: string; title: string | null; updatedTime: string }>
    }
    return (json.data ?? []).map((s) => ({
      // 会话列表用 SessionKey 作为会话 id（与 /api/chat 的 sessionId 同一键空间）
      id: s.sessionKey,
      title: s.title || '新对话',
      updatedAt: s.updatedTime,
    }))
  },

  /**
   * 删除会话消息（真实路由：DELETE /api/chat/session/{sessionId}）
   */
  async deleteConversation(conversationId: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/chat/session/${conversationId}`, {
      method: 'DELETE',
    })

    if (!response.ok) {
      throw new Error(`删除会话失败: ${response.status}`)
    }
  },
}

export { generateId }