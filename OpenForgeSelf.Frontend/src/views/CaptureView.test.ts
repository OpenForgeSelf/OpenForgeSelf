import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import CaptureView from '@/views/CaptureView.vue'
import type { ListenerConfig, CaptureSessionSummary } from '@/types/capture'

vi.mock('@/services/captureApi', () => ({
  captureApi: {
    fetchListeners: vi.fn(),
    createListener: vi.fn(),
    updateListener: vi.fn(),
    deleteListener: vi.fn(),
    startListener: vi.fn(),
    stopListener: vi.fn(),
    fetchSessions: vi.fn(),
    fetchSessionDetail: vi.fn(),
    clearSessions: vi.fn(),
    fetchCaCert: vi.fn()
  }
}))

import { captureApi } from '@/services/captureApi'

const mockApi = captureApi as unknown as Record<
  keyof typeof captureApi,
  ReturnType<typeof vi.fn>
>

function makeListener(partial: Partial<ListenerConfig> = {}): ListenerConfig {
  return {
    id: 1,
    name: '测试监听',
    listenAddress: '0.0.0.0',
    listenPort: 8080,
    targetHost: '127.0.0.1',
    targetPort: 9000,
    enabled: true,
    description: '转发到本地服务',
    createdAt: '2026-08-19T08:00:00Z',
    updatedAt: '2026-08-19T08:00:00Z',
    isRunning: true,
    ...partial
  }
}

function makeSession(partial: Partial<CaptureSessionSummary> = {}): CaptureSessionSummary {
  return {
    id: 101,
    listenerId: 1,
    timestamp: '2026-08-19T08:01:00Z',
    protocol: 'HTTP',
    clientIp: '127.0.0.1',
    target: '127.0.0.1:9000',
    method: 'GET',
    url: 'http://myapp.local/api/items',
    statusCode: 200,
    requestBytes: 512,
    responseBytes: 1024,
    durationMs: 23,
    forwarded: true,
    ...partial
  }
}

const stub = {
  stubs: {
    teleport: true,
    // Element Plus ElSelect 在 jsdom 下存在空值归一化的无限递归问题（EP 已知行为），
    // 本页测试不涉及下拉交互，直接 stub 掉，聚焦页面自身逻辑。
    ElSelect: true,
    ElOption: true
  }
}

describe('CaptureView 抓包代理主页面', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockApi.fetchListeners.mockResolvedValue([makeListener()])
    mockApi.fetchSessions.mockResolvedValue({
      items: [makeSession()],
      total: 1,
      page: 1,
      pageSize: 50
    })
    mockApi.fetchSessionDetail.mockResolvedValue({
      ...makeSession(),
      localEndpoint: '0.0.0.0:8080',
      httpVersion: 'HTTP/1.1',
      requestHeaders: 'Host: myapp.local\nUser-Agent: curl',
      requestBody: '{"q":1}',
      responseHeaders: 'Content-Type: application/json',
      responseBody: '{"ok":true}'
    })
    mockApi.fetchCaCert.mockResolvedValue({
      pem: '-----BEGIN CERTIFICATE-----',
      installHint: '安装到受信任根证书颁发机构',
      certPath: 'x'
    })
  })

  afterEach(() => {
    document.body.innerHTML = ''
  })

  it('渲染监听器卡片与抓包记录行', async () => {
    const wrapper = mount(CaptureView, { global: stub })
    await flushPromises()

    expect(mockApi.fetchListeners).toHaveBeenCalled()
    expect(mockApi.fetchSessions).toHaveBeenCalled()
    expect(wrapper.text()).toContain('测试监听')
    expect(wrapper.text()).toContain('0.0.0.0:8080')
    expect(wrapper.text()).toContain('127.0.0.1:9000')
    expect(wrapper.text()).toContain('http://myapp.local/api/items')
    expect(wrapper.text()).toContain('共 1 条')
  })

  it('点击「新建监听器」打开新建弹窗', async () => {
    const wrapper = mount(CaptureView, { global: stub })
    await flushPromises()

    const newBtn = wrapper.findAll('button').find((b) => b.text().includes('新建监听器'))!
    await newBtn.trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain('新建监听器')
    expect(wrapper.text()).toContain('监听地址')
  })

  it('点击抓包记录行打开详情抽屉并加载详情', async () => {
    const wrapper = mount(CaptureView, { global: stub })
    await flushPromises()

    const row = wrapper.find('.el-table__row')
    expect(row.exists()).toBe(true)
    await row.trigger('click')
    await flushPromises()

    expect(mockApi.fetchSessionDetail).toHaveBeenCalledWith(101)
    expect(wrapper.text()).toContain('抓包详情')
  })

  it('运行中的监听器点击「停止监听」调用 stopListener', async () => {
    const wrapper = mount(CaptureView, { global: stub })
    await flushPromises()

    const stopBtn = wrapper.find('button[title="停止监听"]')
    expect(stopBtn.exists()).toBe(true)
    await stopBtn.trigger('click')
    await flushPromises()

    expect(mockApi.stopListener).toHaveBeenCalledWith(1)
  })

  it('已停止的监听器点击「启动监听」调用 startListener', async () => {
    mockApi.fetchListeners.mockResolvedValue([makeListener({ isRunning: false })])
    const wrapper = mount(CaptureView, { global: stub })
    await flushPromises()

    const startBtn = wrapper.find('button[title="启动监听"]')
    expect(startBtn.exists()).toBe(true)
    await startBtn.trigger('click')
    await flushPromises()

    expect(mockApi.startListener).toHaveBeenCalledWith(1)
  })
})
