import { describe, expect, it, vi } from 'vitest'
import {
  PROBE_ALL_CONCURRENCY,
  formatProbeAllMessage,
  mapPool,
} from './probeAllRuntimes'

describe('probeAllRuntimes（一键探测纯逻辑 · #78）', () => {
  it('PROBE_ALL_CONCURRENCY 固定为 3', () => {
    expect(PROBE_ALL_CONCURRENCY).toBe(3)
  })

  it('mapPool 并发上限生效（同时 in-flight ≤ concurrency）', async () => {
    let inflight = 0
    let peak = 0
    const items = Array.from({ length: 8 }, (_, i) => i)

    await mapPool(items, 3, async (n) => {
      inflight++
      peak = Math.max(peak, inflight)
      await new Promise((r) => setTimeout(r, 20))
      inflight--
      return n * 2
    })

    expect(peak).toBeLessThanOrEqual(3)
    expect(peak).toBeGreaterThan(1)
  })

  it('mapPool 空列表不调 worker', async () => {
    const worker = vi.fn(async () => 1)
    const out = await mapPool([], 3, worker)
    expect(out).toEqual([])
    expect(worker).not.toHaveBeenCalled()
  })

  it('formatProbeAllMessage 区分全成/全败/部分失败', () => {
    expect(formatProbeAllMessage({ ok: 5, err: 0, total: 5 }).level).toBe('ok')
    expect(formatProbeAllMessage({ ok: 0, err: 3, total: 3 }).text).toContain('全部失败')
    const mixed = formatProbeAllMessage({ ok: 2, err: 1, total: 3 })
    expect(mixed.level).toBe('err')
    expect(mixed.text).toContain('2 成功')
    expect(mixed.text).toContain('1 失败')
  })
})
