import { describe, expect, it } from 'vitest'
import { activeResult, buildResultSets, markSkippedFrom, resultDot, resultDotTitle, resultLabel, statsLabel } from './results'

describe('结果标签（FR-16~19 / BR-15~19）', () => {
  it('按语句创建顺序建结果集，默认 pending 第 1 页', () => {
    const sets = buildResultSets(['select 1', 'select 2'], 100)
    expect(sets).toHaveLength(2)
    expect(sets[0].status).toBe('pending')
    expect(sets[0].page).toBe(1)
    expect(sets[0].pageSize).toBe(100)
    expect(sets[0].sql).toBe('select 1')
  })

  it('pageSize 收敛到合法范围', () => {
    expect(buildResultSets(['x'], 9999)[0].pageSize).toBe(500)
    expect(buildResultSets(['x'], 0)[0].pageSize).toBe(50)
  })

  it('失败即停：从失败的下一条起标 skipped，已完成的保持', () => {
    const sets = buildResultSets(['a', 'b', 'c'])
    sets[0] = { ...sets[0], status: 'ok' }
    sets[1] = { ...sets[1], status: 'error' }
    const after = markSkippedFrom(sets, 2, 1)
    expect(after[0].status).toBe('ok')
    expect(after[1].status).toBe('error')
    expect(after[2].status).toBe('skipped')
    expect(after[2].skipReason).toBe('未执行（第 2 条失败）')
  })

  it('markSkippedFrom 不覆盖已终态的结果', () => {
    const sets = buildResultSets(['a', 'b'])
    sets[1] = { ...sets[1], status: 'ok' }
    expect(markSkippedFrom(sets, 1, 0)[1].status).toBe('ok')
  })

  it('结果标题「结果 i」从 1 起', () => {
    expect(resultLabel(0)).toBe('结果 1')
    expect(resultLabel(2)).toBe('结果 3')
  })

  it('状态点与 tooltip', () => {
    const sets = buildResultSets(['a'])
    expect(resultDot(sets[0])).toBe('pending')
    expect(resultDot({ ...sets[0], status: 'skipped', skipReason: '未执行（第 1 条失败）' })).toBe('skipped')
    expect(resultDotTitle(sets[0], 0)).toBe('第 1 条：待执行')
    expect(resultDotTitle({ ...sets[0], status: 'skipped', skipReason: '未执行（第 1 条失败）' }, 0)).toBe('未执行（第 1 条失败）')
  })

  it('activeResult 越界返回 null', () => {
    const sets = buildResultSets(['a'])
    expect(activeResult(sets, 0)).not.toBeNull()
    expect(activeResult(sets, 1)).toBeNull()
    expect(activeResult(sets, -1)).toBeNull()
  })

  it('状态栏「N 行 · X ms」按结果独立；无结果为空串', () => {
    const sets = buildResultSets(['a'])
    expect(statsLabel(sets[0])).toBe('')
    expect(statsLabel({ ...sets[0], status: 'ok', result: { status: 'ok', total: 120, elapsedMs: 7 } })).toBe('120 行 · 7 ms')
    expect(statsLabel({ ...sets[0], status: 'ok', result: { status: 'ok', affectedRows: 3 } })).toBe('影响 3 行')
  })
})
