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
  /** 聊天模型 id（格式：提供商:上游模型id）；空则走后端默认模型 */
  chatModelId?: string
}

/** 后端 ChatController 的 ChatResponse 形状（POST /api/chat 直接返回裸对象） */
export interface SendMessageResponse {
  id: number
  sessionId: string
  role: string
  content: string
  createTime: string
}

export interface StreamMessageChunk {
  // 主聊天流：content/done/error（ChatController / UnifiedAI 网关推送）
  // 聊天记录实时流：chat_record_chunk/chat_record_completed（ChatRecordStreamRecorder 推送）
  // 二者共用同一 WebSocket 分发，故联合类型需覆盖全部事件名
  type: 'content' | 'done' | 'error' | 'chat_record_chunk' | 'chat_record_completed'
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