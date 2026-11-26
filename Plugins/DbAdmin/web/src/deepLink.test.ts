import { describe, expect, it } from 'vitest'
import { approvalIdFromSearch } from './deepLink'

describe('approvalIdFromSearch', () => {
  it('带 approvalId 时打开这一条待批', () => {
    expect(approvalIdFromSearch('?approvalId=44')).toBe('44')
  })

  it('没有参数时不指定待批', () => {
    expect(approvalIdFromSearch('')).toBe('')
  })
})
