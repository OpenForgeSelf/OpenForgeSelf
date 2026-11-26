import { describe, expect, it } from 'vitest'
import {
  dateOrUndefined,
  pageMeta,
  scriptStatusFromApi,
  unwrapApiBody,
  workflowStatusFromApi,
} from '@/services/apiEnvelope'

describe('apiEnvelope', () => {
  it('详情接口的 data 包一层时展开实体', () => {
    const row = unwrapApiBody<Record<string, unknown>>({
      data: { scriptName: '实况长睡', status: 1 },
      success: true,
    })
    expect(row.scriptName).toBe('实况长睡')
    expect(scriptStatusFromApi(row.status)).toBe('running')
  })

  it('已经是实体时不改', () => {
    expect(scriptStatusFromApi('failed')).toBe('failed')
    expect(workflowStatusFromApi(2)).toBe('running')
    expect(workflowStatusFromApi(5)).toBe('failed')
    expect(workflowStatusFromApi(0)).toBeUndefined()
  })

  it('分页字段缺失时用行数', () => {
    expect(pageMeta({ items: [{}, {}] }, [{}, {}])).toEqual({ total: 2, page: 1, pageSize: 2 })
    expect(pageMeta([{ id: 1 }], [{ id: 1 }]).total).toBe(1)
  })

  it('未结束的占位时间不当作结束', () => {
    expect(dateOrUndefined('0001-01-01T00:00:00')).toBeUndefined()
    expect(dateOrUndefined('2026-11-26T04:11:04')).toBeInstanceOf(Date)
  })
})
