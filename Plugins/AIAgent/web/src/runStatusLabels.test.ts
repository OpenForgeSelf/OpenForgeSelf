import { describe, expect, it } from 'vitest'
import { isActiveRunStatus, runStatusText } from './runStatusLabels'

describe('runStatusLabels', () => {
  it('runStatusText maps enum int', () => {
    expect(runStatusText(2)).toBe('运行中')
    expect(runStatusText(4)).toBe('卡住')
  })

  it('isActiveRunStatus excludes terminal states', () => {
    expect(isActiveRunStatus(2)).toBe(true)
    expect(isActiveRunStatus(3)).toBe(false)
    expect(isActiveRunStatus(6)).toBe(false)
  })
})
