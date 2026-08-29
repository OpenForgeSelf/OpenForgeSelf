/**
 * WebSocket服务 - 支持流式响应
 *
 * 支持多订阅者：聊天页与聊天记录页都可注册自己的消息处理器，
 * 连接本身在首次 connect 时建立并保持单例。
 */

import type { StreamMessageChunk } from '@/types/chat'

const WS_BASE_URL = import.meta.env.VITE_WS_BASE_URL || `ws://${window.location.host}/ws`

export type MessageHandler = (chunk: StreamMessageChunk) => void
export type ErrorHandler = (error: Event) => void
export type ConnectionHandler = () => void

export interface WebSocketServiceOptions {
  onMessage?: MessageHandler
  onError?: ErrorHandler
  onOpen?: ConnectionHandler
  onClose?: ConnectionHandler
}

/**
 * WebSocket服务类
 */
export class WebSocketService {
  private ws: WebSocket | null = null
  private connectPromise: Promise<void> | null = null
  private reconnectAttempts = 0
  private maxReconnectAttempts = 5
  private reconnectDelay = 1000
  private messageHandlers = new Set<MessageHandler>()
  private openHandlers = new Set<ConnectionHandler>()
  private closeHandlers = new Set<ConnectionHandler>()
  private errorHandlers = new Set<ErrorHandler>()

  /**
   * 连接WebSocket（幂等：已连接/连接中时仅注册回调并返回既有连接）。
   */
  connect(options: WebSocketServiceOptions = {}): Promise<void> {
    if (options.onMessage) this.messageHandlers.add(options.onMessage)
    if (options.onOpen) this.openHandlers.add(options.onOpen)
    if (options.onClose) this.closeHandlers.add(options.onClose)
    if (options.onError) this.errorHandlers.add(options.onError)

    if (this.connectPromise) return this.connectPromise

    this.connectPromise = new Promise<void>((resolve, reject) => {
      try {
        this.ws = new WebSocket(WS_BASE_URL)

        this.ws.onopen = () => {
          console.log('WebSocket连接已建立')
          this.reconnectAttempts = 0
          this.openHandlers.forEach((h) => h())
          resolve()
        }

        this.ws.onmessage = (event) => {
          try {
            const chunk: StreamMessageChunk = JSON.parse(event.data)
            this.messageHandlers.forEach((h) => h(chunk))
          } catch (error) {
            console.error('解析WebSocket消息失败:', error)
          }
        }

        this.ws.onerror = (error) => {
          console.error('WebSocket错误:', error)
          this.errorHandlers.forEach((h) => h(error))
          this.connectPromise = null
          reject(error)
        }

        this.ws.onclose = () => {
          console.log('WebSocket连接已关闭')
          this.closeHandlers.forEach((h) => h())
          this.connectPromise = null
          this.attemptReconnect()
        }
      } catch (error) {
        this.connectPromise = null
        reject(error)
      }
    })
    return this.connectPromise
  }

  /**
   * 订阅消息（幂等连接 + 注册处理器）。用于聊天记录页等旁路消费者。
   */
  subscribe(handler: MessageHandler): void {
    this.connect()
    this.messageHandlers.add(handler)
  }

  /**
   * 取消订阅消息处理器。
   */
  unsubscribe(handler: MessageHandler): void {
    this.messageHandlers.delete(handler)
  }

  /**
   * 尝试重连
   */
  private attemptReconnect(): void {
    if (this.reconnectAttempts < this.maxReconnectAttempts) {
      this.reconnectAttempts++
      console.log(`尝试重连 (${this.reconnectAttempts}/${this.maxReconnectAttempts})...`)
      setTimeout(() => {
        this.connect()
      }, this.reconnectDelay * this.reconnectAttempts)
    }
  }

  /**
   * 发送消息
   */
  send(content: string, conversationId?: string): void {
    if (!this.ws || this.ws.readyState !== WebSocket.OPEN) {
      throw new Error('WebSocket未连接')
    }

    this.ws.send(JSON.stringify({
      type: 'message',
      content,
      conversationId,
    }))
  }

  /**
   * 发送流式消息请求
   */
  sendStreamRequest(content: string, conversationId?: string): void {
    if (!this.ws || this.ws.readyState !== WebSocket.OPEN) {
      throw new Error('WebSocket未连接')
    }

    this.ws.send(JSON.stringify({
      type: 'stream',
      content,
      conversationId,
    }))
  }

  /**
   * 关闭连接
   */
  disconnect(): void {
    if (this.ws) {
      this.ws.close()
      this.ws = null
    }
  }

  /**
   * 检查连接状态
   */
  isConnected(): boolean {
    return this.ws !== null && this.ws.readyState === WebSocket.OPEN
  }
}

// 创建单例实例
export const wsService = new WebSocketService()
