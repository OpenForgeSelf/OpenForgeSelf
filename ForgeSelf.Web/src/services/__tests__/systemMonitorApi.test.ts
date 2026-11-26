import { describe, expect, it } from 'vitest'
import { mapProcess, mapProcessList } from '../systemMonitorApi'

describe('mapProcessList', () => {
  it('把接口的 processes 和 processId 映射成页面用的 pid', () => {
    const rows = mapProcessList({
      processes: [{ processId: 33819, processName: 'sleep', cpuUsagePercent: 0, memoryUsageBytes: 4096 }],
      totalCount: 1,
    })
    expect(rows).toEqual([
      { pid: 33819, name: 'sleep', cpuUsage: 0, memoryUsage: 0, memoryBytes: 4096, startTime: 0 },
    ])
  })

  it('单条进程同样能映射', () => {
    expect(mapProcess({ processId: 42, processName: 'sleep' }).pid).toBe(42)
  })
})
