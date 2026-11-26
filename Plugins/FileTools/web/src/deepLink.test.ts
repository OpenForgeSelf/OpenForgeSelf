import { describe, expect, it } from 'vitest'
import { scanIdFromSearch } from './deepLink'

describe('scanIdFromSearch', () => {
  it('带 scanId 时选中该次扫描', () => {
    expect(scanIdFromSearch('?scanId=scan-9')).toBe('scan-9')
  })

  it('没有 scanId 时不切换扫描', () => {
    expect(scanIdFromSearch('')).toBe('')
  })
})