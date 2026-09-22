import { afterEach, describe, expect, it, vi } from 'vitest'
import { archiveSessionWithConfirm } from './sessionArchive'

/**
 * 归档二次确认单测 —— 锁死用户反馈的核心语义（2026-09-22：「点击归档没有确认，直接就归档了」）：
 *  1. 确认动作必须发生在请求之前（顺序），且用户取消时**一个请求都不发**；
 *  2. 只有确认后才打 PUT /archive（全 id 原样进 URL，不裁前缀）；
 *  3. 请求失败如实返回 failed + 错误文案（不吞错、不谎报成功）。
 *
 * 确认动作由本测试注入假实现，因此这里完全不依赖 element-plus / jsdom 弹窗能力。
 */

/** 记录调用 URL 与 init 的假 fetch（沿用 http.session.test.ts 的桩法）。 */
function stubFetch(body: unknown): { url: () => string; init: () => RequestInit | undefined } {
  let calledUrl = ''
  let calledInit: RequestInit | undefined
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, init?: RequestInit) => {
      calledUrl = url
      calledInit = init
      return { ok: true, status: 200, body: null, json: async () => body } as unknown as Response
    }),
  )
  return { url: () => calledUrl, init: () => calledInit }
}

/** fetch 是否被调用过（取消路径断言用）。 */
function fetchCalled(): boolean {
  return vi.mocked(fetch).mock.calls.length > 0
}

describe('归档二次确认（取消不落库 / 确认才请求）', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('用户取消：不发任何请求，结果为 cancelled', async () => {
    stubFetch({ success: true })
    const confirm = vi.fn(async () => false)

    const res = await archiveSessionWithConfirm('mu7p581h-6oscch', confirm, '会话一')

    expect(res.outcome).toBe('cancelled')
    expect(fetchCalled(), '取消时不得发起归档请求（回归：点一下就归档）').toBe(false)
  })

  it('用户确认：先确认后请求，PUT 全 id + body { archived: true }', async () => {
    const spy = stubFetch({ success: true })
    const order: string[] = []
    const confirm = vi.fn(async () => {
      order.push('confirm')
      return true
    })

    const res = await archiveSessionWithConfirm('mu7p581h-6oscch', confirm, '会话一')

    expect(res.outcome).toBe('archived')
    expect(order).toEqual(['confirm'])
    expect(spy.url()).toBe('/api/ai-agent/chat/session/mu7p581h-6oscch/archive')
    expect(spy.init()?.method).toBe('PUT')
    expect(spy.init()?.body).toBe(JSON.stringify({ archived: true }))
    // 关键守卫：确认文案拿到标题，且全 id 不被裁成前缀
    expect(confirm).toHaveBeenCalledWith('mu7p581h-6oscch', '会话一')
    expect(spy.url()).not.toContain('/session/mu7p581h/archive')
  })

  it('确认前不得先行请求（顺序守卫）', async () => {
    stubFetch({ success: true })
    let releaseConfirm: ((v: boolean) => void) | undefined
    const confirm = () =>
      new Promise<boolean>((resolve) => {
        releaseConfirm = resolve
      })

    const pending = archiveSessionWithConfirm('sess-1', confirm)

    // 确认框还挂着：此时不该有任何请求发出
    expect(fetchCalled()).toBe(false)

    releaseConfirm?.(true)
    const res = await pending

    expect(res.outcome).toBe('archived')
    expect(fetchCalled()).toBe(true)
  })

  it('请求失败：返回 failed + 错误文案，不谎报成功', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => {
        throw new Error('网络断了')
      }),
    )

    const res = await archiveSessionWithConfirm('sess-2', async () => true)

    expect(res.outcome).toBe('failed')
    expect(res.error).toContain('网络断了')
  })

  it('标题缺失时原样传给确认动作（由确认实现自行回退到 id）', async () => {
    stubFetch({ success: true })
    const confirm = vi.fn(async () => true)

    await archiveSessionWithConfirm('sess-3', confirm)

    expect(confirm).toHaveBeenCalledWith('sess-3', undefined)
  })
})
