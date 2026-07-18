/**
 * WebSocket服务 - 支持流式响应
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
  private reconnectAttempts = 0
  private maxReconnectAttempts = 5
  private reconnectDelay = 1000
  private options: WebSocketServiceOptions = {}

  /**
   * 连接WebSocket
   */
  connect(options: WebSocketServiceOptions = {}): Promise<void> {
    this.options = options
    return new Promise((resolve, reject) => {
      try {
        this.ws = new WebSocket(WS_BASE_URL)

        this.ws.onopen = () => {
          console.log('WebSocket连接已建立')
          this.reconnectAttempts = 0
          this.options.onOpen?.()
          resolve()
        }

        this.ws.onmessage = (event) => {
          try {
            const chunk: StreamMessageChunk = JSON.parse(event.data)
            this.options.onMessage?.(chunk)
          } catch (error) {
            console.error('解析WebSocket消息失败:', error)
          }
        }

        this.ws.onerror = (error) => {
          console.error('WebSocket错误:', error)
          this.options.onError?.(error)
          reject(error)
        }

        this.ws.onclose = () => {
          console.log('WebSocket连接已关闭')
          this.options.onClose?.()
          this.attemptReconnect()
        }
      } catch (error) {
        reject(error)
      }
    })
  }

  /**
   * 尝试重连
   */
  private attemptReconnect(): void {
    if (this.reconnectAttempts < this.maxReconnectAttempts) {
      this.reconnectAttempts++
      console.log(`尝试重连 (${this.reconnectAttempts}/${this.maxReconnectAttempts})...`)
      setTimeout(() => {
        this.connect(this.options)
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