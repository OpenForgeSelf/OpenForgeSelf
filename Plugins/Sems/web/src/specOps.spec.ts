import { describe, expect, it } from 'vitest'
import {
  buildCaseBody,
  buildCreateBody,
  buildLinkBody,
  buildVersionBody,
  coveragePhrase,
  createWriteQueue,
  listPhase,
  parseCriteria,
} from './specOps'

describe('parseCriteria', () => {
  it('空文本不传 criteria', () => {
    expect(parseCriteria('  \n')).toEqual({ ok: true })
  })

  it('id 重复（不区分大小写）在发请求前拒绝', () => {
    const result = parseCriteria('AC1 第一条\nac1 第二条')
    expect(result.ok).toBe(false)
    if (!result.ok) expect(result.message).toContain('重复')
  })

  it('只有 id 没有文案时拒绝', () => {
    expect(parseCriteria('AC1').ok).toBe(false)
  })
})

describe('buildCreateBody', () => {
  it('标题为空时不产出请求体', () => {
    const result = buildCreateBody({
      title: '  ',
      content: '正文',
      author: '',
      note: '',
      criteriaText: '',
    })
    expect(result).toEqual({ ok: false, message: 'title 必填' })
  })

  it('带一条验收标准', () => {
    const result = buildCreateBody({
      title: '登录',
      content: '正文',
      author: '',
      note: '初稿',
      criteriaText: 'AC1 能看到标题',
    })
    expect(result.ok).toBe(true)
    if (result.ok) {
      expect(result.body.title).toBe('登录')
      expect(result.body.author).toBeUndefined()
      expect(result.body.criteria).toEqual([{ id: 'AC1', text: '能看到标题' }])
    }
  })
})

describe('buildVersionBody', () => {
  it('默认沿用上一版时不带 criteria', () => {
    const result = buildVersionBody({
      content: '新正文',
      author: 'me',
      note: '',
      criteriaText: 'AC1 不该发出去',
      keepPrevious: true,
    })
    expect(result.ok).toBe(true)
    if (result.ok) {
      expect(result.body).not.toHaveProperty('criteria')
      expect(result.body.author).toBe('me')
    }
  })

  it('取消沿用且文本为空时发送空数组', () => {
    const result = buildVersionBody({
      content: '新正文',
      author: '',
      note: '',
      criteriaText: '',
      keepPrevious: false,
    })
    expect(result.ok).toBe(true)
    if (result.ok) expect(result.body.criteria).toEqual([])
  })

  it('正文为空时拒绝', () => {
    const result = buildVersionBody({
      content: '',
      author: '',
      note: '',
      criteriaText: '',
      keepPrevious: true,
    })
    expect(result).toEqual({ ok: false, message: 'content 必填' })
  })
})

describe('buildLinkBody / buildCaseBody', () => {
  it('关联缺 ref 时拒绝', () => {
    expect(buildLinkBody({ version: '1', kind: 'commit', ref: ' ' }).ok).toBe(false)
  })

  it('用例体使用 case 字段', () => {
    const result = buildCaseBody({ version: '2', criterionId: 'AC1', caseRef: 'a.spec.ts::x' })
    expect(result.ok).toBe(true)
    if (result.ok) expect(result.body).toEqual({ version: 2, criterionId: 'AC1', case: 'a.spec.ts::x' })
  })
})

describe('coveragePhrase / listPhase', () => {
  it('已登记文案只计条数', () => {
    expect(coveragePhrase(true, ['a'])).toBe('已登记 1 条')
    expect(coveragePhrase(true, ['a'])).not.toContain('已通过')
    expect(coveragePhrase(false, [])).toBe('未登记')
  })

  it('无数据时区分加载、失败、空；有数据时保持列表', () => {
    expect(listPhase(true, '', 0)).toBe('loading')
    expect(listPhase(false, '请求失败(503): 不可用', 0)).toBe('error')
    expect(listPhase(false, '', 0)).toBe('empty')
    expect(listPhase(false, '后台失败', 2)).toBe('list')
  })
})

describe('createWriteQueue', () => {
  it('按发起顺序执行，前一个失败后后面的仍执行', async () => {
    const enqueue = createWriteQueue()
    const order: string[] = []
    const first = enqueue(async () => {
      order.push('a')
      throw new Error('fail')
    })
    const second = enqueue(async () => {
      order.push('b')
      return 'ok'
    })
    await expect(first).rejects.toThrow('fail')
    await expect(second).resolves.toBe('ok')
    expect(order).toEqual(['a', 'b'])
  })
})