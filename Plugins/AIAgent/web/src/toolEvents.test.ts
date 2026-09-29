import { describe, expect, it } from 'vitest'
import { settleToolEventFifo } from './toolEvents'
import type { ToolEvent } from './types'

/** B9-2 门禁：tool_result 与 pending 工具事件的 FIFO 配对（B8 帧序下同名多次调用不互换）。 */
describe('settleToolEventFifo', () => {
  it('同名两次调用按 FIFO 配对——第一个结果结算第一个 pending', () => {
    const events: ToolEvent[] = [
      { name: 'read_file', args: '{"path":"a.txt"}', pending: true },
      { name: 'read_file', args: '{"path":"b.txt"}', pending: true },
    ]

    settleToolEventFifo(events, { name: 'read_file', result: '内容A', success: true })
    expect(events[0]).toMatchObject({ name: 'read_file', result: '内容A', success: true, pending: false })
    expect(events[1]?.pending).toBe(true)

    settleToolEventFifo(events, { name: 'read_file', result: '内容B', success: true })
    expect(events[1]).toMatchObject({ name: 'read_file', result: '内容B', success: true, pending: false })
  })

  it('不同名调用互不干扰', () => {
    const events: ToolEvent[] = [
      { name: 'list_files', pending: true },
      { name: 'read_file', pending: true },
    ]
    settleToolEventFifo(events, { name: 'read_file', result: 'ok', success: true })
    expect(events[0]?.pending).toBe(true)
    expect(events[1]).toMatchObject({ result: 'ok', pending: false })
  })

  it('无同名 pending 时兜底补一条（异常到达序）', () => {
    const events: ToolEvent[] = [{ name: 'other_tool', pending: false }]
    settleToolEventFifo(events, { name: 'ghost_tool', result: 'late', success: false })
    expect(events).toHaveLength(2)
    expect(events[1]).toMatchObject({ name: 'ghost_tool', result: 'late', success: false, pending: false })
  })

  it('已结算事件不再被重复命中', () => {
    const events: ToolEvent[] = [
      { name: 'read_file', pending: false, result: '旧' },
      { name: 'read_file', pending: true },
    ]
    settleToolEventFifo(events, { name: 'read_file', result: '新', success: true })
    expect(events[0]?.result).toBe('旧')
    expect(events[1]).toMatchObject({ result: '新', pending: false })
  })
})
