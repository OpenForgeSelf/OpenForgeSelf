import { afterEach, describe, expect, it, vi } from 'vitest'
import { ApiError, apiGet, extractErrorCode } from './http'

const store: Record<string, string> = {}
vi.stubGlobal('localStorage', { getItem: (k: string) => store[k] ?? null, setItem: (k: string, v: string) => { store[k] = v } })

afterEach(() => vi.unstubAllGlobals())

describe('http', () => {
  it('03 错误体 {code,message} → ApiError.code', async () => {
    vi.stubGlobal('localStorage', { getItem: () => 'tkn' })
    const fetchMock = vi.fn(async () => new Response(JSON.stringify({ code: 'readonly_connection', message: '只读' }), { status: 400 }))
    vi.stubGlobal('fetch', fetchMock)
    const err = await apiGet('/api/db-admin/query').catch(e => e)
    expect(err).toBeInstanceOf(ApiError)
    expect(err.code).toBe('readonly_connection')
    expect(err.message).toBe('只读')
    const calls = fetchMock.mock.calls as unknown as [string, RequestInit][]
    expect((calls[0][1].headers as Record<string, string>).Authorization).toBe('Bearer tkn')
  })
  it('401 无 code 补 unauthorized', async () => {
    vi.stubGlobal('localStorage', { getItem: () => null })
    vi.stubGlobal('fetch', vi.fn(async () => new Response('', { status: 401, statusText: 'Unauthorized' })))
    const err = await apiGet('/x').catch(e => e)
    expect(err.code).toBe('unauthorized')
  })
  it('拆信封', async () => {
    vi.stubGlobal('localStorage', { getItem: () => null })
    vi.stubGlobal('fetch', vi.fn(async () => new Response(JSON.stringify({ success: true, code: 0, data: [1] }), { status: 200 })))
    expect(await apiGet('/x')).toEqual([1])
  })
  it('extractErrorCode 兼容 data.code', () => {
    expect(extractErrorCode({ data: { code: 'driver_missing' } })).toBe('driver_missing')
    expect(extractErrorCode({ code: 0 })).toBeUndefined()
  })
})

describe('http 状态与错误体', () => {
  it('apiPostWithStatus 带回 202', async () => {
    const { apiPostWithStatus } = await import('./http')
    vi.stubGlobal('localStorage', { getItem: () => null })
    vi.stubGlobal('fetch', vi.fn(async () => new Response(JSON.stringify({ status: 'awaiting_approval', approvalId: 5, code: null }), { status: 202 })))
    const r = await apiPostWithStatus<{ approvalId: number }>('/api/db-admin/query', {})
    expect(r.status).toBe(202)
    expect(r.data?.approvalId).toBe(5)
  })
  it('409 / 424：ApiError 保留 code 与完整 body', async () => {
    const { apiPostWithStatus } = await import('./http')
    vi.stubGlobal('localStorage', { getItem: () => null })
    vi.stubGlobal('fetch', vi.fn(async () => new Response(JSON.stringify({ code: 'confirmation_required', message: '需二次确认' }), { status: 409 })))
    const e1 = await apiPostWithStatus('/x').catch(e => e)
    expect([e1.status, e1.code]).toEqual([409, 'confirmation_required'])
    vi.stubGlobal('fetch', vi.fn(async () => new Response(JSON.stringify({ status: 'error', code: 'driver_missing', message: '缺 Npgsql' }), { status: 424 })))
    const e2 = await apiPostWithStatus('/x').catch(e => e)
    expect([e2.status, e2.code, (e2.body as { status: string }).status]).toEqual([424, 'driver_missing', 'error'])
  })
})
