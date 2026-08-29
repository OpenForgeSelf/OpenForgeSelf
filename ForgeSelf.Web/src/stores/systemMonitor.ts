import { ref, computed } from 'vue'
import { defineStore } from 'pinia'
import type {
  MonitorOverview,
  ProcessInfo,
  HistoryPoint,
  MonitorTab
} from '@/types/systemMonitor'
import { systemMonitorApi } from '@/services/systemMonitorApi'
import { monitorHub } from '@/services/monitorHub'

const MAX_HISTORY_POINTS = 120

export const useSystemMonitorStore = defineStore('systemMonitor', () => {
  const overview = ref<MonitorOverview | null>(null)
  const cpuHistory = ref<HistoryPoint[]>([])
  const memoryHistory = ref<HistoryPoint[]>([])
  const networkHistory = ref<{ download: number; upload: number; timestamp: number }[]>([])
  const processes = ref<ProcessInfo[]>([])
  const isConnected = ref(false)
  const updateInterval = ref(2000)
  const currentTab = ref<MonitorTab>('overview')
  const isLoading = ref(false)
  const error = ref<string | null>(null)
  const lastUpdateTime = ref(0)

  const topProcessesByCpu = computed(() => {
    return [...processes.value]
      .sort((a, b) => b.cpuUsage - a.cpuUsage)
      .slice(0, 10)
  })

  const topProcessesByMemory = computed(() => {
    return [...processes.value]
      .sort((a, b) => b.memoryBytes - a.memoryBytes)
      .slice(0, 10)
  })

  function addHistoryPoint(history: HistoryPoint[], value: number, timestamp: number): HistoryPoint[] {
    const newHistory = [...history, { value, timestamp }]
    if (newHistory.length > MAX_HISTORY_POINTS) {
      return newHistory.slice(newHistory.length - MAX_HISTORY_POINTS)
    }
    return newHistory
  }

  function handleOverviewUpdate(data: MonitorOverview): void {
    overview.value = data
    lastUpdateTime.value = data.timestamp

    cpuHistory.value = addHistoryPoint(cpuHistory.value, data.cpu.totalUsage, data.timestamp)
    memoryHistory.value = addHistoryPoint(memoryHistory.value, data.memory.usagePercent, data.timestamp)

    networkHistory.value = [
      ...networkHistory.value,
      { download: data.network.downloadSpeed, upload: data.network.uploadSpeed, timestamp: data.timestamp }
    ]
    if (networkHistory.value.length > MAX_HISTORY_POINTS) {
      networkHistory.value = networkHistory.value.slice(networkHistory.value.length - MAX_HISTORY_POINTS)
    }
  }

  async function connect(): Promise<void> {
    if (isConnected.value) return

    try {
      isLoading.value = true
      error.value = null

      await monitorHub.connect({
        onOverviewUpdate: handleOverviewUpdate,
        onConnected: () => {
          isConnected.value = true
        },
        onDisconnected: () => {
          isConnected.value = false
        },
        onError: (e) => {
          error.value = e.message
        },
        updateInterval: updateInterval.value
      })

      isConnected.value = true
    } catch (e) {
      console.error('连接监控服务失败:', e)
      error.value = e instanceof Error ? e.message : '连接失败'
      isConnected.value = false
    } finally {
      isLoading.value = false
    }
  }

  function disconnect(): void {
    monitorHub.disconnect()
    isConnected.value = false
  }

  async function loadOverview(): Promise<void> {
    try {
      isLoading.value = true
      error.value = null
      const data = await systemMonitorApi.getOverview()
      handleOverviewUpdate(data)
    } catch (e) {
      console.error('加载概览数据失败:', e)
      error.value = e instanceof Error ? e.message : '加载失败'
    } finally {
      isLoading.value = false
    }
  }

  async function loadCpuHistory(duration?: number): Promise<void> {
    try {
      const data = await systemMonitorApi.getCpuHistory(duration)
      cpuHistory.value = data.points
    } catch (e) {
      console.error('加载CPU历史失败:', e)
    }
  }

  async function loadMemoryHistory(duration?: number): Promise<void> {
    try {
      const data = await systemMonitorApi.getMemoryHistory(duration)
      memoryHistory.value = data.points
    } catch (e) {
      console.error('加载内存历史失败:', e)
    }
  }

  async function loadNetworkHistory(duration?: number): Promise<void> {
    try {
      const data = await systemMonitorApi.getNetworkHistory(duration)
      networkHistory.value = data.points.map(p => ({
        download: p.value,
        upload: p.value * 0.3,
        timestamp: p.timestamp
      }))
    } catch (e) {
      console.error('加载网络历史失败:', e)
    }
  }

  async function loadProcesses(sortBy?: string, keyword?: string): Promise<void> {
    try {
      isLoading.value = true
      error.value = null
      processes.value = await systemMonitorApi.getProcesses(sortBy, keyword)
    } catch (e) {
      console.error('加载进程列表失败:', e)
      error.value = e instanceof Error ? e.message : '加载失败'
    } finally {
      isLoading.value = false
    }
  }

  async function killProcess(pid: number): Promise<boolean> {
    try {
      const success = await systemMonitorApi.killProcess(pid)
      if (success) {
        processes.value = processes.value.filter(p => p.pid !== pid)
      }
      return success
    } catch (e) {
      console.error('结束进程失败:', e)
      error.value = e instanceof Error ? e.message : '操作失败'
      return false
    }
  }

  function setTab(tab: MonitorTab): void {
    currentTab.value = tab
  }

  function setUpdateInterval(interval: number): void {
    updateInterval.value = interval
    if (isConnected.value) {
      monitorHub.setUpdateInterval(interval)
    }
  }

  function clearError(): void {
    error.value = null
  }

  return {
    overview,
    cpuHistory,
    memoryHistory,
    networkHistory,
    processes,
    isConnected,
    updateInterval,
    currentTab,
    isLoading,
    error,
    lastUpdateTime,
    topProcessesByCpu,
    topProcessesByMemory,
    connect,
    disconnect,
    loadOverview,
    loadCpuHistory,
    loadMemoryHistory,
    loadNetworkHistory,
    loadProcesses,
    killProcess,
    setTab,
    setUpdateInterval,
    clearError
  }
})
