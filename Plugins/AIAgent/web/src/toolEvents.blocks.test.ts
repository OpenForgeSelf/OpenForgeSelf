import { describe, expect, it } from 'vitest'
import { toolEventsToBlocks } from './toolEvents'

describe('历史工具轨迹 → tool 块', () => {
  it('成功调用映射为 ok，且带上结果与耗时', () => {
    const [b] = toolEventsToBlocks([{ name: 'read_file', result: 'ok', success: true, durationMs: 12 }])
    expect(b.type).toBe('tool')
    expect(b.name).toBe('read_file')
    expect(b.status).toBe('ok')
    expect(b.result).toBe('ok')
    expect(b.durationMs).toBe(12)
  })

  it('失败调用映射为 error', () => {
    expect(toolEventsToBlocks([{ name: 'run_terminal_command', success: false }])[0].status).toBe('error')
  })

  it('未结算的调用保持 running，不伪装成成功', () => {
    expect(toolEventsToBlocks([{ name: 'write_file', pending: true }])[0].status).toBe('running')
  })

  it('写/执行类工具打危险标', () => {
    expect(toolEventsToBlocks([{ name: 'write_file' }])[0].dangerous).toBe(true)
    expect(toolEventsToBlocks([{ name: 'read_file' }])[0].dangerous).toBe(false)
  })

  it('空/脏数据不炸也不产出空卡', () => {
    expect(toolEventsToBlocks(undefined)).toEqual([])
    expect(toolEventsToBlocks([{ name: '' }, {} as never])).toEqual([])
  })
})
