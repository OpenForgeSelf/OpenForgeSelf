import { describe, expect, it } from 'vitest'
import { canNext, canPrev, clampPage, pageCount, rangeLabel } from './pager'

describe('结果格分页边界', () => {
  it('页数', () => {
    expect(pageCount(0, 50)).toBe(1)
    expect(pageCount(120, 50)).toBe(3)
    expect(pageCount(undefined, 50)).toBeUndefined()
  })
  it('首页不能上一页；末页不能下一页', () => {
    expect(canPrev(1)).toBe(false)
    expect(canPrev(2)).toBe(true)
    expect(canNext(3, 50, 120, 20)).toBe(false)
    expect(canNext(2, 50, 120, 50)).toBe(true)
  })
  it('total 未知：本页满才可能有下一页', () => {
    expect(canNext(1, 50, undefined, 50)).toBe(true)
    expect(canNext(1, 50, undefined, 49)).toBe(false)
  })
  it('越界回退', () => {
    expect(clampPage(9, 50, 120)).toBe(3)
    expect(clampPage(0, 50, 120)).toBe(1)
    expect(clampPage(7, 50, undefined)).toBe(7)
  })
  it('区间文案', () => {
    expect(rangeLabel(3, 50, 20, 120)).toBe('第 101–120 行 / 共 120')
    expect(rangeLabel(1, 50, 0, 0)).toBe('0 行')
  })
})

describe('pageSize clamp（后端上限 500）', () => {
  it('clampPageSize', async () => {
    const { clampPageSize } = await import('./pager')
    expect(clampPageSize(100000)).toBe(500)
    expect(clampPageSize(100)).toBe(100)
    expect(clampPageSize(0)).toBe(50)
    expect(clampPageSize(Number.NaN)).toBe(50)
  })
})
