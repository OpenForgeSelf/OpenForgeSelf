import { afterEach, describe, expect, it, vi } from 'vitest'
import { archiveSession, deleteSession, fetchSessions } from './http'

/**
 * 会话管理前端契约单测（防 T3 回归 + 锁归档语义）。
 *
 * 历史 bug：切回历史会话时用「时间戳前缀」查询 → 0 条（存储的是「时间戳-随机后缀」全 id）。
 * 根治在后端（聚合/删除按全 id 精确匹配）+ 前端（切换/归档一律回传全 id）。
 * 本测试锁死前端 http 层三件事：
 *  1. fetchSessions 打对端点，且**默认只取未归档**（agent 页天然不展示已归档会话）；
 *  2. archiveSession 走 PUT，全 id 原样进 URL，body 为 { archived }；
 *  3. deleteSession（会话管理页硬删用）仍按全 id 原样透传。
 */

/** 构造一个返回固定 JSON 的假 Response（走 request() 解包）。 */
function fakeJsonResponse(body: unknown): Response {
  return {
    ok: true,
    status: 200,
    body: null,
    json: async () => body,
  } as unknown as Response
}

/** 记录调用 URL 与 init 的假 fetch。 */
function stubFetch(body: unknown): { url: () => string; init: () => RequestInit | undefined } {
  let calledUrl = ''
  let calledInit: RequestInit | undefined
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, init?: RequestInit) => {
      calledUrl = url
      calledInit = init
      return fakeJsonResponse(body)
    }),
  )
  return { url: () => calledUrl, init: () => calledInit }
}

describe('会话管理 API — 全 id 透传 + 归档筛选（杜绝 T3 前缀 bug）', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('fetchSessions 默认取未归档（archived=active），端点与参数都对', async () => {
    const sample = [
      { sessionId: 's1', title: 't', messageCount: 1, lastTime: '2026-01-01T00:00:00', archived: false },
    ]
    const spy = stubFetch(sample)

    const res = await fetchSessions()

    // 显式带筛选：agent 页只展示未归档会话（默认值即 active）
    expect(spy.url()).toBe('/api/ai-agent/chat/sessions?archived=active')
    expect(res).toEqual(sample)
  })

  it('fetchSessions 可切到 all / archived（会话管理页按归档筛选）', async () => {
    const spy = stubFetch([])

    await fetchSessions('all')
    expect(spy.url()).toBe('/api/ai-agent/chat/sessions?archived=all')

    await fetchSessions('archived')
    expect(spy.url()).toBe('/api/ai-agent/chat/sessions?archived=archived')
  })

  it('archiveSession 走 PUT，全 id 原样进 URL，body 为 { archived: true }', async () => {
    // 真实形态：时间戳 base36 - 随机 6 位后缀（与前端 newSessionId 一致）
    const fullId = 'mu7p581h-6oscch'
    const spy = stubFetch({ success: true })

    await archiveSession(fullId)

    expect(spy.url()).toBe(`/api/ai-agent/chat/session/${fullId}/archive`)
    expect(spy.init()?.method).toBe('PUT')
    expect(spy.init()?.body).toBe(JSON.stringify({ archived: true }))
    // 关键守卫：绝不能只发前缀（否则会归档到别的会话）
    expect(spy.url()).not.toContain('/session/mu7p581h/archive')
  })

  it('archiveSession 支持取消归档（archived: false）', async () => {
    const spy = stubFetch({ success: true })

    await archiveSession('sess-x', false)

    expect(spy.init()?.body).toBe(JSON.stringify({ archived: false }))
  })

  it('archiveSession 对含特殊字符的 id 仍精确编码不丢失', async () => {
    const idWithSpace = 'sess with space-abc'
    const spy = stubFetch({ success: true })

    await archiveSession(idWithSpace)

    expect(spy.url()).toBe(
      `/api/ai-agent/chat/session/${encodeURIComponent(idWithSpace)}/archive`,
    )
  })

  it('deleteSession 用全 id 原样进 URL（含随机后缀，不截断前缀）', async () => {
    const fullId = 'mu7p581h-6oscch'
    const spy = stubFetch({ success: true, message: '会话消息已删除' })

    await deleteSession(fullId)

    // encodeURIComponent 对字母数字与 - 不改变，全 id 必须完整出现在 URL 中
    expect(spy.url()).toBe(`/api/ai-agent/chat/session/${fullId}`)
    expect(spy.url()).toContain('mu7p581h-6oscch')
    // 关键守卫：绝不能只发前缀
    expect(spy.url()).not.toBe('/api/ai-agent/chat/session/mu7p581h')
  })

  it('deleteSession 对含特殊字符的 id 仍精确编码不丢失', async () => {
    // 退化形态：若未来 id 含空格/中文，encodeURIComponent 必须保留（后端按全 id 精确匹配）
    const idWithSpace = 'sess with space-abc'
    const spy = stubFetch({ success: true })

    await deleteSession(idWithSpace)

    expect(spy.url()).toBe(`/api/ai-agent/chat/session/${encodeURIComponent(idWithSpace)}`)
  })
})
