import type {
  CpuUsage,
  MemoryInfo,
  DiskDrive,
  NetworkSpeed,
  NetworkConnection,
  ProcessInfo,
  MonitorOverview,
  MonitorHistory
} from '@/types/systemMonitor'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api'
// 后端 SystemMonitorController 路由前缀为 api/monitor（此前误用 api/system-monitor 导致 404 后 fallback mock）
const MONITOR_API_BASE = `${API_BASE_URL}/monitor`

/** 解包后端 ApiResponse 包装（{ data: T } 或直接 T），否则 store 拿到的 overview 结构错误 */
async function unwrap<T>(response: Response): Promise<T> {
  const json = (await response.json()) as { data?: T }
  return (json?.data ?? json) as T
}

/** 后端 overview 字段名与前端类型不一致（totalUsagePercent vs totalUsage 等），统一映射为前端结构 */
function mapOverview(raw: unknown): MonitorOverview {
  const d = (raw ?? {}) as Record<string, any>
  const cpu = (d.cpu ?? {}) as Record<string, any>
  const memory = (d.memory ?? {}) as Record<string, any>
  const network = (d.network ?? {}) as Record<string, any>
  const disks = Array.isArray(d.disks)
    ? (d.disks as Record<string, any>[]).map((disk) => ({
        name: String(disk.driveName ?? disk.name ?? ''),
        label: String(disk.volumeLabel ?? disk.label ?? ''),
        totalSize: Number(disk.totalSizeBytes ?? disk.totalSize ?? 0),
        freeSpace: Number(disk.availableFreeSpaceBytes ?? disk.freeSpace ?? 0),
        usedSpace: Number(disk.usedSpaceBytes ?? disk.usedSpace ?? 0),
        usagePercent: Number(disk.usagePercent ?? 0),
        readSpeed: Number(disk.readSpeed ?? 0),
        writeSpeed: Number(disk.writeSpeed ?? 0),
      }))
    : []

  return {
    cpu: {
      totalUsage: Number(cpu.totalUsagePercent ?? cpu.totalUsage ?? 0),
      perCoreUsage: Array.isArray(cpu.perCoreUsage)
        ? (cpu.perCoreUsage as Record<string, any>[]).map((c) => Number(c.usagePercent ?? c))
        : [],
      timestamp: Number(cpu.timestamp ?? Date.now()),
    },
    memory: {
      total: Number(memory.totalMemoryBytes ?? memory.total ?? 0),
      used: Number(memory.usedMemoryBytes ?? memory.used ?? 0),
      available: Number(memory.availableMemoryBytes ?? memory.available ?? 0),
      usagePercent: Number(memory.usagePercent ?? 0),
      timestamp: Number(memory.timestamp ?? Date.now()),
    },
    disks,
    network: {
      uploadSpeed: Number(network.uploadSpeedBytesPerSecond ?? network.uploadSpeed ?? 0),
      downloadSpeed: Number(network.downloadSpeedBytesPerSecond ?? network.downloadSpeed ?? 0),
      timestamp: Number(network.timestamp ?? Date.now()),
    },
    timestamp: Number(d.timestamp ?? Date.now()),
  }
}

function generateMockCpuUsage(): CpuUsage {
  const coreCount = 8
  const perCoreUsage = Array.from({ length: coreCount }, () => Math.random() * 100)
  const totalUsage = perCoreUsage.reduce((sum, val) => sum + val, 0) / coreCount
  return {
    totalUsage,
    perCoreUsage,
    timestamp: Date.now()
  }
}

function generateMockMemory(): MemoryInfo {
  const total = 16 * 1024 * 1024 * 1024
  const used = Math.random() * total * 0.7 + total * 0.3
  const available = total - used
  return {
    total,
    used,
    available,
    usagePercent: (used / total) * 100,
    timestamp: Date.now()
  }
}

function generateMockDisks(): DiskDrive[] {
  return [
    {
      name: 'C:',
      label: '系统盘',
      totalSize: 512 * 1024 * 1024 * 1024,
      usedSpace: 320 * 1024 * 1024 * 1024,
      freeSpace: 192 * 1024 * 1024 * 1024,
      usagePercent: 62.5,
      readSpeed: Math.random() * 100 * 1024 * 1024,
      writeSpeed: Math.random() * 80 * 1024 * 1024
    },
    {
      name: 'D:',
      label: '数据盘',
      totalSize: 1024 * 1024 * 1024 * 1024,
      usedSpace: 450 * 1024 * 1024 * 1024,
      freeSpace: 574 * 1024 * 1024 * 1024,
      usagePercent: 43.9,
      readSpeed: Math.random() * 150 * 1024 * 1024,
      writeSpeed: Math.random() * 120 * 1024 * 1024
    },
    {
      name: 'E:',
      label: '存储盘',
      totalSize: 2048 * 1024 * 1024 * 1024,
      usedSpace: 1850 * 1024 * 1024 * 1024,
      freeSpace: 198 * 1024 * 1024 * 1024,
      usagePercent: 90.3,
      readSpeed: Math.random() * 80 * 1024 * 1024,
      writeSpeed: Math.random() * 60 * 1024 * 1024
    }
  ]
}

function generateMockNetworkSpeed(): NetworkSpeed {
  return {
    uploadSpeed: Math.random() * 10 * 1024 * 1024,
    downloadSpeed: Math.random() * 50 * 1024 * 1024,
    timestamp: Date.now()
  }
}

function generateMockProcesses(): ProcessInfo[] {
  const processNames = [
    'chrome.exe', 'Code.exe', 'explorer.exe', 'svchost.exe',
    'node.exe', 'python.exe', 'docker.exe', 'WmiPrvSE.exe',
    'System', 'Registry', 'csrss.exe', 'wininit.exe',
    'services.exe', 'lsass.exe', 'fontdrvhost.exe', 'dwm.exe'
  ]
  return processNames.map((name, index) => ({
    pid: 1000 + index * 137,
    name,
    cpuUsage: Math.random() * 15,
    memoryBytes: Math.random() * 500 * 1024 * 1024,
    memoryUsage: Math.random() * 5,
    startTime: Date.now() - Math.random() * 86400000 * 7
  }))
}

function generateMockConnections(): NetworkConnection[] {
  const states = ['ESTABLISHED', 'LISTENING', 'TIME_WAIT', 'CLOSE_WAIT']
  const protocols = ['TCP', 'UDP']
  return Array.from({ length: 15 }, (_, i) => ({
    protocol: protocols[i % 2],
    localAddress: `192.168.1.100:${3000 + i * 7}`,
    remoteAddress: i % 3 === 0 ? '0.0.0.0:*' : `${10 + i}.${i * 3}.${i * 5}.${i * 7}:${443 + i}`,
    state: states[i % states.length],
    processName: ['chrome.exe', 'Code.exe', 'node.exe', 'svchost.exe'][i % 4]
  }))
}

function generateMockHistory(duration: number = 60000, count: number = 60): MonitorHistory {
  const now = Date.now()
  const interval = duration / count
  const points = Array.from({ length: count }, (_, i) => ({
    value: Math.random() * 100,
    timestamp: now - (count - i) * interval
  }))
  return { points, duration }
}

async function getCpuUsage(): Promise<CpuUsage> {
  try {
    const response = await fetch(`${MONITOR_API_BASE}/cpu`)
    if (!response.ok) throw new Error('获取CPU数据失败')
    return unwrap<CpuUsage>(response)
  } catch {
    return generateMockCpuUsage()
  }
}

async function getCpuHistory(duration?: number): Promise<MonitorHistory> {
  try {
    const url = duration
      ? `${MONITOR_API_BASE}/cpu/history?duration=${duration}`
      : `${MONITOR_API_BASE}/cpu/history`
    const response = await fetch(url)
    if (!response.ok) throw new Error('获取CPU历史失败')
    return unwrap<MonitorHistory>(response)
  } catch {
    return generateMockHistory(duration)
  }
}

async function getMemoryUsage(): Promise<MemoryInfo> {
  try {
    const response = await fetch(`${MONITOR_API_BASE}/memory`)
    if (!response.ok) throw new Error('获取内存数据失败')
    return unwrap<MemoryInfo>(response)
  } catch {
    return generateMockMemory()
  }
}

async function getMemoryHistory(duration?: number): Promise<MonitorHistory> {
  try {
    const url = duration
      ? `${MONITOR_API_BASE}/memory/history?duration=${duration}`
      : `${MONITOR_API_BASE}/memory/history`
    const response = await fetch(url)
    if (!response.ok) throw new Error('获取内存历史失败')
    return unwrap<MonitorHistory>(response)
  } catch {
    return generateMockHistory(duration)
  }
}

async function getDiskDrives(): Promise<DiskDrive[]> {
  try {
    const response = await fetch(`${MONITOR_API_BASE}/disks`)
    if (!response.ok) throw new Error('获取磁盘数据失败')
    return unwrap<DiskDrive[]>(response)
  } catch {
    return generateMockDisks()
  }
}

async function getDiskIO(driveName: string): Promise<{ readSpeed: number; writeSpeed: number }> {
  try {
    const response = await fetch(`${MONITOR_API_BASE}/disks/${encodeURIComponent(driveName)}/io`)
    if (!response.ok) throw new Error('获取磁盘IO失败')
    return unwrap<{ readSpeed: number; writeSpeed: number }>(response)
  } catch {
    return {
      readSpeed: Math.random() * 100 * 1024 * 1024,
      writeSpeed: Math.random() * 80 * 1024 * 1024
    }
  }
}

async function getNetworkSpeed(): Promise<NetworkSpeed> {
  try {
    const response = await fetch(`${MONITOR_API_BASE}/network/speed`)
    if (!response.ok) throw new Error('获取网络速度失败')
    return unwrap<NetworkSpeed>(response)
  } catch {
    return generateMockNetworkSpeed()
  }
}

async function getNetworkConnections(): Promise<NetworkConnection[]> {
  try {
    const response = await fetch(`${MONITOR_API_BASE}/network/connections`)
    if (!response.ok) throw new Error('获取网络连接失败')
    return unwrap<NetworkConnection[]>(response)
  } catch {
    return generateMockConnections()
  }
}

async function getNetworkHistory(duration?: number): Promise<MonitorHistory> {
  try {
    const url = duration
      ? `${MONITOR_API_BASE}/network/history?duration=${duration}`
      : `${MONITOR_API_BASE}/network/history`
    const response = await fetch(url)
    if (!response.ok) throw new Error('获取网络历史失败')
    return unwrap<MonitorHistory>(response)
  } catch {
    return generateMockHistory(duration)
  }
}

async function getProcesses(sortBy?: string, keyword?: string): Promise<ProcessInfo[]> {
  try {
    const params = new URLSearchParams()
    if (sortBy) params.set('sortBy', sortBy)
    if (keyword) params.set('keyword', keyword)
    const query = params.toString()
    const url = query
      ? `${MONITOR_API_BASE}/processes?${query}`
      : `${MONITOR_API_BASE}/processes`
    const response = await fetch(url)
    if (!response.ok) throw new Error('获取进程列表失败')
    return unwrap<ProcessInfo[]>(response)
  } catch {
    let processes = generateMockProcesses()
    if (keyword) {
      processes = processes.filter(p =>
        p.name.toLowerCase().includes(keyword.toLowerCase())
      )
    }
    return processes
  }
}

async function killProcess(pid: number): Promise<boolean> {
  try {
    const response = await fetch(`${MONITOR_API_BASE}/processes/${pid}`, {
      method: 'DELETE'
    })
    if (!response.ok) throw new Error('结束进程失败')
    return true
  } catch {
    console.warn(`结束进程 ${pid} 失败，使用模拟模式`)
    return true
  }
}

async function getOverview(): Promise<MonitorOverview> {
  try {
    const response = await fetch(`${MONITOR_API_BASE}/overview`)
    if (!response.ok) throw new Error('获取概览数据失败')
    const raw = await unwrap<unknown>(response)
    return mapOverview(raw)
  } catch {
    return {
      cpu: generateMockCpuUsage(),
      memory: generateMockMemory(),
      disks: generateMockDisks(),
      network: generateMockNetworkSpeed(),
      timestamp: Date.now()
    }
  }
}

function formatBytes(bytes: number, decimals: number = 2): string {
  if (bytes === 0) return '0 B'
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB', 'TB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  return parseFloat((bytes / Math.pow(k, i)).toFixed(decimals)) + ' ' + sizes[i]
}

function formatSpeed(bytesPerSecond: number): string {
  return formatBytes(bytesPerSecond) + '/s'
}

function formatUptime(startTime: number): string {
  const diff = Date.now() - startTime
  const days = Math.floor(diff / (1000 * 60 * 60 * 24))
  const hours = Math.floor((diff % (1000 * 60 * 60 * 24)) / (1000 * 60 * 60))
  const minutes = Math.floor((diff % (1000 * 60 * 60)) / (1000 * 60))
  if (days > 0) return `${days}天 ${hours}小时`
  if (hours > 0) return `${hours}小时 ${minutes}分钟`
  return `${minutes}分钟`
}

export const systemMonitorApi = {
  getCpuUsage,
  getCpuHistory,
  getMemoryUsage,
  getMemoryHistory,
  getDiskDrives,
  getDiskIO,
  getNetworkSpeed,
  getNetworkConnections,
  getNetworkHistory,
  getProcesses,
  killProcess,
  getOverview,
  formatBytes,
  formatSpeed,
  formatUptime
}
