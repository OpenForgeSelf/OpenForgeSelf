import { describe, it, expect, vi, afterEach, beforeEach } from 'vitest'
import { captureApi, parseResponse } from '@/services/captureApi'

/** 构造 ApiResponse 包装响应 */
function apiResponse<T>(data: T, extra: Record<string, unknown> = {}): Response {
  return new Response(JSON.stringify({ code: 0, message: 'ok', success: true, data, ...extra }), {
    status: 200,
    headers: { 'Content-Type': 'application/json' }
  })
}

describe('captureApi 服务层', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('fetchListeners 请求 /api/capture/listeners 并解包 data', async () => {
    const mock = vi.fn().mockResolvedValue(
      apiResponse([
        { id: 1, name: '测试监听', listenAddress: '0.0.0.0', listenPort: 8080, isRunning: false }
      ])
    )
    vi.stubGlobal('fetch', mock)

    const result = await captureApi.fetchListeners()

    expect(mock).toHaveBeenCalledWith('/api/capture/listeners')
    expect(result).toHaveLength(1)
    expect(result[0].name).toBe('测试监听')
  })

  it('createListener 以 POST + JSON body 提交', async () => {
    const mock = vi.fn().mockResolvedValue(apiResponse({ id: 1, name: '新监听' }))
    vi.stubGlobal('fetch', mock)

    await captureApi.createListener({
      name: '新监听',
      listenAddress: '127.0.0.1',
      listenPort: 9090,
      targetHost: 'example.com',
      targetPort: 80,
      enabled: true
    })

    expect(mock).toHaveBeenCalledTimes(1)
    const [url, init] = mock.mock.calls[0] as [string, RequestInit]
    expect(url).toBe('/api/capture/listeners')
    expect(init.method).toBe('POST')
    expect(init.headers).toEqual({ 'Content-Type': 'application/json' })
    const body = JSON.parse(init.body as string)
    expect(body.name).toBe('新监听')
    expect(body.targetHost).toBe('example.com')
  })

  it('fetchSessions 拼接查询参数（listenerId/protocol/page/pageSize）', async () => {
    const mock = vi.fn().mockResolvedValue(
      apiResponse({ items: [], total: 0, page: 1, pageSize: 20 })
    )
    vi.stubGlobal('fetch', mock)

    await captureApi.fetchSessions({ listenerId: 3, protocol: 'HTTP', page: 2, pageSize: 50 })

    const url = mock.mock.calls[0][0] as string
    expect(url).toBe('/api/capture/sessions?listenerId=3&protocol=HTTP&page=2&pageSize=50')
  })

  it('fetchSessions 无参数时不带查询串', async () => {
    const mock = vi.fn().mockResolvedValue(
      apiResponse({ items: [], total: 0, page: 1, pageSize: 20 })
    )
    vi.stubGlobal('fetch', mock)

    await captureApi.fetchSessions()

    const url = mock.mock.calls[0][0] as string
    expect(url).toBe('/api/capture/sessions')
  })

  it('fetchSessionDetail 请求 /api/capture/sessions/{id}', async () => {
    const mock = vi.fn().mockResolvedValue(apiResponse({ id: 42, protocol: 'HTTPS' }))
    vi.stubGlobal('fetch', mock)

    const detail = await captureApi.fetchSessionDetail(42)

    expect(mock).toHaveBeenCalledWith('/api/capture/sessions/42')
    expect(detail.id).toBe(42)
  })

  it('clearSessions 以 DELETE 提交且可带 listenerId', async () => {
    const mock = vi.fn().mockResolvedValue(apiResponse(null))
    vi.stubGlobal('fetch', mock)

    await captureApi.clearSessions(7)

    const [url, init] = mock.mock.calls[0] as [string, RequestInit]
    expect(url).toBe('/api/capture/sessions?listenerId=7')
    expect(init.method).toBe('DELETE')
  })

  it('parseResponse 对非 ok 响应抛出后端 message', async () => {
    const resp = new Response(JSON.stringify({ code: 500, message: '监听器不存在', success: false }), {
      status: 404
    })
    await expect(parseResponse<void>(resp)).rejects.toThrow('监听器不存在')
  })

  it('startListener / stopListener 以 POST 提交对应端点', async () => {
    // mockImplementation 保证每次调用生成新的 Response（body 只能读一次）
    const mock = vi.fn().mockImplementation(() => Promise.resolve(apiResponse(null)))
    vi.stubGlobal('fetch', mock)

    await captureApi.startListener(1)
    expect(mock).toHaveBeenCalledWith('/api/capture/listeners/1/start', { method: 'POST' })

    mock.mockClear()
    await captureApi.stopListener(1)
    expect(mock).toHaveBeenCalledWith('/api/capture/listeners/1/stop', { method: 'POST' })
  })

  it('fetchCaCert 请求 /api/capture/ca-cert 并返回 PEM', async () => {
    const mock = vi
      .fn()
      .mockResolvedValue(apiResponse({ pem: '-----BEGIN CERTIFICATE-----', installHint: '安装提示', certPath: 'x' }))
    vi.stubGlobal('fetch', mock)

    const info = await captureApi.fetchCaCert()

    expect(mock).toHaveBeenCalledWith('/api/capture/ca-cert')
    expect(info.pem).toContain('BEGIN CERTIFICATE')
  })
})
