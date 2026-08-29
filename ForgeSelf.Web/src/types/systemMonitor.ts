export interface CpuUsage {
  totalUsage: number
  perCoreUsage: number[]
  timestamp: number
}

export interface MemoryInfo {
  total: number
  used: number
  available: number
  usagePercent: number
  timestamp: number
}

export interface DiskDrive {
  name: string
  label: string
  totalSize: number
  freeSpace: number
  usedSpace: number
  usagePercent: number
  readSpeed: number
  writeSpeed: number
}

export interface NetworkSpeed {
  uploadSpeed: number
  downloadSpeed: number
  timestamp: number
}

export interface NetworkConnection {
  protocol: string
  localAddress: string
  remoteAddress: string
  state: string
  processName: string
}

export interface ProcessInfo {
  pid: number
  name: string
  cpuUsage: number
  memoryUsage: number
  memoryBytes: number
  startTime: number
}

export interface MonitorOverview {
  cpu: CpuUsage
  memory: MemoryInfo
  disks: DiskDrive[]
  network: NetworkSpeed
  timestamp: number
}

export interface HistoryPoint {
  value: number
  timestamp: number
}

export interface MonitorHistory {
  points: HistoryPoint[]
  duration: number
}

export type MonitorTab = 'overview' | 'processes' | 'disks' | 'network'

export type ProcessSortField = 'name' | 'cpuUsage' | 'memoryUsage' | 'pid'

export type SortDirection = 'asc' | 'desc'
