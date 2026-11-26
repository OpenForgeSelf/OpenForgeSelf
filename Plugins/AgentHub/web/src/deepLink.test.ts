import { describe, expect, it } from 'vitest'
import { taskIdFromSearch } from './deepLink'

describe('taskIdFromSearch', () => {
  it('主参数 id 选中这一条任务', () => {
    expect(taskIdFromSearch('?id=5')).toBe(5)
  })

  it('旧名 taskId 仍能选中', () => {
    expect(taskIdFromSearch('?taskId=5')).toBe(5)
  })

  it('没有 taskId 时不切换任务', () => {
    expect(taskIdFromSearch('')).toBe(0)
  })
})
