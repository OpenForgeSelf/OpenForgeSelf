import { describe, expect, it } from 'vitest'
import { semsFocusFromSearch } from './deepLink'

describe('semsFocusFromSearch', () => {
  it('主参数 id 打开项目，pid 露出这次进程', () => {
    expect(semsFocusFromSearch('?id=4&pid=100')).toEqual({ projectId: 4, pid: 100 })
  })

  it('旧名 projectId 和 runId 仍能打开', () => {
    expect(semsFocusFromSearch('?projectId=4&runId=100')).toEqual({ projectId: 4, pid: 100 })
  })

  it('没有项目时不切换工作台', () => {
    expect(semsFocusFromSearch('?runId=100')).toEqual({ projectId: 0, pid: 100 })
  })
})
