import { describe, expect, it } from 'vitest'
import { scanIdFromSearch } from './deepLink'

describe('scanIdFromSearch', () => {
  it('主参数 id 选中该次扫描', () => {
    expect(scanIdFromSearch('?id=scan-9')).toBe('scan-9')
  })

  it('旧名 scanId 仍能选中', () => {
    expect(scanIdFromSearch('?scanId=scan-9')).toBe('scan-9')
  })

  it('没有 scanId 时不切换扫描', () => {
    expect(scanIdFromSearch('')).toBe('')
  })
})