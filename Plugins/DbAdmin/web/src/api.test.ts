import { afterEach, describe, expect, it, vi } from 'vitest'
import { dbAdminApi, logQuery, testBody } from './api'

afterEach(() => vi.unstubAllGlobals())

describe('api 契约', () => {
  it('query：pageSize clamp ≤500；202 → awaiting_approval + 字符串 approvalId', async () => {
    vi.stubGlobal('localStorage', { getItem: () => null })
    const fetchMock = vi.fn(async () => new Response(JSON.stringify({ status: 'awaiting_approval', kind: 'write', destructive: true, approvalId: 9 }), { status: 202 }))
    vi.stubGlobal('fetch', fetchMock)
    const r = await dbAdminApi.query({ connectionId: '1', sql: 'DROP TABLE t', page: 1, pageSize: 100000 })
    expect(r).toMatchObject({ status: 'awaiting_approval', approvalId: '9', destructive: true, kind: 'write', httpStatus: 202 })
    const sent = JSON.parse((fetchMock.mock.calls as unknown as [string, RequestInit][])[0][1].body as string)
    expect(sent.pageSize).toBe(500)
  })
  it('agentEchoes 走 /audit?kind=agent 并透传 sessionId/status/source 过滤', async () => {
    vi.stubGlobal('localStorage', { getItem: () => null })
    const fetchMock = vi.fn(async () => new Response(JSON.stringify({ items: [{ id: 3, kind: 'agent', tool: 'db_query_readonly', status: 'ok', rowCount: 2, createTime: 't' }], total: 1, page: 1, pageSize: 50 }), { status: 200 }))
    vi.stubGlobal('fetch', fetchMock)
    const es = await dbAdminApi.agentEchoes({ sessionId: 's1', status: 'ok', source: 'agent' })
    const url = (fetchMock.mock.calls as unknown as [string][])[0][0]
    expect(url).toContain('/api/db-admin/audit?')
    expect(url).toContain('kind=agent')
    expect(url).toContain('sessionId=s1')
    expect(url).toContain('status=ok')
    expect(url).toContain('source=agent')
    expect(es[0]).toMatchObject({ id: '3', tool: 'db_query_readonly', rowCount: 2 })
  })
  it('logQuery clamp；testBody 仅数字 id 才带（后端 Int32?）', () => {
    expect(logQuery({ pageSize: 9999 })).toContain('pageSize=500')
    expect(testBody({ name: 'a', provider: 'sqlite', mode: 'form' }, '12')).toMatchObject({ id: 12 })
    expect('id' in testBody({ name: 'a', provider: 'sqlite', mode: 'form' }, 'sys:X')).toBe(false)
    expect('id' in testBody({ name: 'a', provider: 'sqlite', mode: 'form' }, null)).toBe(false)
  })
})
