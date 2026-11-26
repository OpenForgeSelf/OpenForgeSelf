import { describe, expect, it } from 'vitest'
import { taskIdFromSearch } from './deepLink'

describe('taskIdFromSearch', () => {
  it('带 taskId 时选中这一条任务', () => {
    expect(taskIdFromSearch('?taskId=5')).toBe(5)
  })

  it('没有 taskId 时不切换任务', () => {
    expect(taskIdFromSearch('')).toBe(0)
  })
})
