import { describe, expect, it } from 'vitest'
import { formatRunDuration, parseStartedAt } from './runDuration'

describe('runDuration', () => {
  it('parseStartedAt rejects empty and invalid', () => {
    expect(parseStartedAt(undefined)).toBeNull()
    expect(parseStartedAt('0001-01-01T00:00:00')).toBeNull()
  })

  it('formatRunDuration ticks with nowMs', () => {
    const start = '2026-11-03T10:00:00'
    const t0 = new Date(start).getTime()
    expect(formatRunDuration(start, t0 + 5000)).toBe('5s')
    expect(formatRunDuration(start, t0 + 65_000)).toBe('1m5s')
  })
})
