import { describe, expect, it } from 'vitest'
import { todoIdFromSearch } from './deepLink'

describe('todoIdFromSearch', () => {
  it('带 todoId 时打开这一条待办', () => {
    expect(todoIdFromSearch('?todoId=12')).toBe(12)
  })

  it('缺参或非正整数时不打开', () => {
    expect(todoIdFromSearch('')).toBe(0)
    expect(todoIdFromSearch('?todoId=0')).toBe(0)
  })
})
