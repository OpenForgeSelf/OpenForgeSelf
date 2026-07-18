import type { MonitorOverview } from '@/types/systemMonitor'
import { systemMonitorApi } from './systemMonitorApi'

export type MonitorDataType = 'overview' | 'cpu' | 'memory' | 'disk' | 'network' | 'processes'

export interface MonitorHubOptions {
  onOverviewUpdate?: (data: MonitorOverview) => void
  onConnected?: () => void
  onDisconnected?: () => void
  onError?: (error: Error) => void
  updateInterval?: number
}

const DEFAULT_UPDATE_INTERVAL = 2000
const MIN_UPDATE_INTERVAL = 500
const MAX_UPDATE_INTERVAL = 30000

class MonitorHubService {
  private options: MonitorHubOptions = {}
  private isConnected = false
  private pollingTimer: ReturnType<typeof setInterval> | null = null
  private updateInterval = DEFAULT_UPDATE_INTERVAL
  private subscribedTypes = new Set<MonitorDataType>(['overview'])
  private reconnectAttempts = 0
  private maxReconnectAttempts = 5
  private lastUpdateTime = 0

  connect(options: MonitorHubOptions = {}): Promise<void> {
    return new Promise((resolve) => {
      this.options = options
      if (options.updateInterval) {
        this.updateInterval = this.clampInterval(options.updateInterval)
      }
      this.subscribedTypes.add('overview')
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
      this.fetchData()
    }, this.updateInterval)
    this.fetchData()
  }

  private stopPolling(): void {
    if (this.pollingTimer) {
      clearInterval(this.pollingTimer)
      this.pollingTimer = null
    }
  }

  private async fetchData(): Promise<void> {
    if (this.subscribedTypes.has('overview')) {
      try {
        const data = await systemMonitorApi.getOverview()
        this.lastUpdateTime = Date.now()
        this.options.onOverviewUpdate?.(data)
      } catch (error) {
        console.error('获取监控数据失败:', error)
        this.options.onError?.(error instanceof Error ? error : new Error(String(error)))
        this.attemptReconnect()
      }
    }
  }

  private attemptReconnect(): void {
    if (this.reconnectAttempts < this.maxReconnectAttempts) {
      this.reconnectAttempts++
      console.log(`尝试重连监控服务 (${this.reconnectAttempts}/${this.maxReconnectAttempts})...`)
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
    this.options.onDisconnected?.()
  }

  subscribe(dataType: MonitorDataType): void {
    this.subscribedTypes.add(dataType)
  }

  unsubscribe(dataType: MonitorDataType): void {
    this.subscribedTypes.delete(dataType)
  }

  setUpdateInterval(interval: number): void {
    this.updateInterval = this.clampInterval(interval)
    if (this.isConnected && this.pollingTimer) {
      this.startPolling()
    }
  }

  private clampInterval(interval: number): number {
    return Math.max(MIN_UPDATE_INTERVAL, Math.min(MAX_UPDATE_INTERVAL, interval))
  }

  getUpdateInterval(): number {
    return this.updateInterval
  }

  isConnectedStatus(): boolean {
    return this.isConnected
  }

  getLastUpdateTime(): number {
    return this.lastUpdateTime
  }
}

export const monitorHub = new MonitorHubService()
