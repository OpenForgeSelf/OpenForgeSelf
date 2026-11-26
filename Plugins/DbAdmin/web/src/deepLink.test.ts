import { describe, expect, it } from 'vitest'
import { approvalIdFromSearch } from './deepLink'

describe('approvalIdFromSearch', () => {
  it('主参数 id 打开这一条待批', () => {
    expect(approvalIdFromSearch('?id=44')).toBe('44')
  })

  it('旧名 approvalId 仍能打开', () => {
    expect(approvalIdFromSearch('?approvalId=44')).toBe('44')
  })

  it('没有参数时不指定待批', () => {
    expect(approvalIdFromSearch('')).toBe('')
  })
})
