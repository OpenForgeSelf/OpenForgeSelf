/**
 * 聊天消息类型定义
 */

export type MessageRole = 'user' | 'assistant' | 'system'

export interface ChatMessage {
  id: string
  role: MessageRole
  content: string
  timestamp: Date
  isStreaming?: boolean
}

export interface SendMessageRequest {
  content: string
  conversationId?: string
}

export interface SendMessageResponse {
  messageId: string
  conversationId: string
}

export interface StreamMessageChunk {
  type: 'content' | 'done' | 'error'
  content?: string
  messageId?: string
  error?: string
}

export interface Conversation {
  id: string
  title: string
  messages: ChatMessage[]
  createdAt: Date
  updatedAt: Date
}