import { describe, expect, it } from 'vitest'

import { mapLimit } from './pool'

describe('mapLimit 限流并发（导出页批量读回用的那把闸）', () => {
  it('每个任务恰好跑一次，全部跑完才 resolve', async () => {
    const seen: number[] = []
    await mapLimit([1, 2, 3, 4, 5], 2, async (n) => { seen.push(n) })
    expect(seen).toHaveLength(5)
    expect([...seen].sort((a, b) => a - b)).toEqual([1, 2, 3, 4, 5])
  })

  it('同时在途的任务数不得超过上限，但确实并发过', async () => {
    let live = 0
    let peak = 0
    await mapLimit(Array.from({ length: 9 }, (_, i) => i), 3, async () => {
      live++
      peak = Math.max(peak, live)
      await new Promise(resolve => setTimeout(resolve, 2))
      live--
    })
    expect(peak).toBeLessThanOrEqual(3)
    expect(peak, '峰值只有 1 = 实际是串行跑的，限流器等于没并发').toBeGreaterThan(1)
  })

  it('空清单与 limit<1 都不抛错、也不跑任务', async () => {
    let calls = 0
    await mapLimit([], 3, async () => { calls++ })
    await mapLimit([1, 2], 0, async () => { calls++ })
    expect(calls).toBe(0)
  })
})
