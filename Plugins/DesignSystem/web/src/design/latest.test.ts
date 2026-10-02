/**
 * 序号守卫单测：任何"await 后写共享 ref"都带序号守卫，旧响应后到不得覆盖新状态。
 * 用受控 promise 直接证明竞态语义（03-plan Test Plan 第 3 条）。
 */
import { describe, expect, it } from 'vitest'
import { createLatest } from './latest'

describe('createLatest 序号守卫', () => {
  it('后创建的实例作废先创建的实例', () => {
    const a = createLatest()
    expect(a.isCurrent()).toBe(true)
    const b = createLatest()
    expect(b.isCurrent()).toBe(true)
    expect(a.isCurrent()).toBe(false)
  })

  it('旧响应后到不覆盖（受控 promise）', async () => {
    let resolveA!: () => void
    const pa = new Promise<void>((r) => (resolveA = r))
    let applied: string | null = null
    // A 请求在途：完成时会写"共享状态"（带守卫）
    const runA = createLatest()
    void pa.then(() => {
      if (runA.isCurrent()) applied = 'A'
    })
    // A 还没回来时用户又发起 B 并已完成
    const runB = createLatest()
    if (runB.isCurrent()) applied = 'B'
    // 现在 A 才返回 —— 它是陈旧响应，必须被丢弃
    resolveA()
    await pa
    expect(applied).toBe('B')
  })

  it('独自运行（没有竞争者）时始终是最新', () => {
    const a = createLatest()
    expect(a.isCurrent()).toBe(true)
    expect(a.isCurrent()).toBe(true)
  })
})
