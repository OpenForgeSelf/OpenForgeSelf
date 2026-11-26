import { describe, expect, it } from 'vitest'
import { semsFocusFromSearch } from './deepLink'

describe('semsFocusFromSearch', () => {
  it('带 projectId 和 runId 时打开该项目并露出这次运行', () => {
    expect(semsFocusFromSearch('?projectId=4&runId=100')).toEqual({ projectId: 4, runId: 100 })
  })

  it('没有项目时不切换工作台', () => {
    expect(semsFocusFromSearch('?runId=100')).toEqual({ projectId: 0, runId: 100 })
  })
})
