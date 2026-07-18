import type { ScriptExecution, ScriptExecutionLog } from '@/types/scriptRunner'
import { scriptRunnerApi } from './scriptRunnerApi'

export interface ScriptHubOptions {
  onExecutionUpdate?: (execution: ScriptExecution) => void
  onOutputReceived?: (executionId: string, log: ScriptExecutionLog) => void
  onExecutionComplete?: (execution: ScriptExecution) => void
  onConnected?: () => void
  onDisconnected?: () => void
  onError?: (error: Error) => void
  pollInterval?: number
}

const DEFAULT_POLL_INTERVAL = 1000
const MIN_POLL_INTERVAL = 500
const MAX_POLL_INTERVAL = 5000

class ScriptHubService {
  private options: ScriptHubOptions = {}
  private isConnected = false
  private pollingTimer: ReturnType<typeof setInterval> | null = null
  private pollInterval = DEFAULT_POLL_INTERVAL
  private subscribedExecutions = new Map<string, ScriptExecution>()
  private reconnectAttempts = 0
  private maxReconnectAttempts = 5
  private lastUpdateTime = 0

  connect(options: ScriptHubOptions = {}): Promise<void> {
    return new Promise((resolve) => {
      this.options = options
      if (options.pollInterval) {
        this.pollInterval = this.clampInterval(options.pollInterval)
      }
      this.startPolling()
      this.isConnected = true
      this.reconnectAttempts = 0
      this.options.onConnected?.()
      resolve()
    })
  }

  private startPolling(): void {
    this.stopPolling()
    this.pollingTimer = setInterval(() => {
      this.pollExecutions()
    }, this.pollInterval)
    this.pollExecutions()
  }

  private stopPolling(): void {
    if (this.pollingTimer) {
      clearInterval(this.pollingTimer)
      this.pollingTimer = null
    }
  }

  private async pollExecutions(): Promise<void> {
    if (this.subscribedExecutions.size === 0) {
      return
    }

    for (const [executionId, execution] of this.subscribedExecutions) {
      if (execution.status === 'running' || execution.status === 'pending') {
        try {
          const updated = await scriptRunnerApi.getExecution(executionId)
          this.lastUpdateTime = Date.now()

          if (updated.logs.length > execution.logs.length) {
            const newLogs = updated.logs.slice(execution.logs.length)
            for (const log of newLogs) {
              this.options.onOutputReceived?.(executionId, log)
            }
          }

          this.subscribedExecutions.set(executionId, updated)
          this.options.onExecutionUpdate?.(updated)

          if (updated.status !== 'running' && updated.status !== 'pending') {
            this.options.onExecutionComplete?.(updated)
          }
        } catch (error) {
          console.error('获取执行状态失败:', error)
          this.options.onError?.(error instanceof Error ? error : new Error(String(error)))
          this.attemptReconnect()
        }
      }
    }
  }

  private attemptReconnect(): void {
    if (this.reconnectAttempts < this.maxReconnectAttempts) {
      this.reconnectAttempts++
      console.log(`尝试重连脚本执行服务 (${this.reconnectAttempts}/${this.maxReconnectAttempts})...`)
      this.stopPolling()
      setTimeout(() => {
        if (this.isConnected) {
          this.startPolling()
        }
      }, 2000 * this.reconnectAttempts)
    } else {
      this.disconnect()
    }
  }

  disconnect(): void {
    this.stopPolling()
    this.isConnected = false
    this.subscribedExecutions.clear()
    this.options.onDisconnected?.()
  }

  subscribe(executionId: string, initialExecution?: ScriptExecution): void {
    if (initialExecution) {
      this.subscribedExecutions.set(executionId, initialExecution)
    } else {
      scriptRunnerApi.getExecution(executionId).then(execution => {
        this.subscribedExecutions.set(executionId, execution)
      }).catch(error => {
        console.error('订阅执行失败:', error)
        this.options.onError?.(error instanceof Error ? error : new Error(String(error)))
      })
    }
  }

  unsubscribe(executionId: string): void {
    this.subscribedExecutions.delete(executionId)
  }

  isSubscribed(executionId: string): boolean {
    return this.subscribedExecutions.has(executionId)
  }

  setPollInterval(interval: number): void {
    this.pollInterval = this.clampInterval(interval)
    if (this.isConnected && this.pollingTimer) {
      this.startPolling()
    }
  }

  private clampInterval(interval: number): number {
    return Math.max(MIN_POLL_INTERVAL, Math.min(MAX_POLL_INTERVAL, interval))
  }

  getPollInterval(): number {
    return this.pollInterval
  }

  isConnectedStatus(): boolean {
    return this.isConnected
  }

  getLastUpdateTime(): number {
    return this.lastUpdateTime
  }

  getSubscribedCount(): number {
    return this.subscribedExecutions.size
  }
}

export const scriptHub = new ScriptHubService()
