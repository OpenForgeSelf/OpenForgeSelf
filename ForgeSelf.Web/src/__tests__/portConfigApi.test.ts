import { describe, it, expect, beforeEach, vi } from 'vitest'

/**
 * portConfigApi 单元测试 —— 测试 API 客户端的数据解析逻辑。
 * 独立文件，不与其他 mock 冲突。
 */

// Mock request 模块
const mockRequest = vi.fn()
vi.mock('@/services/request', () => ({
  request: mockRequest,
  getStoredToken: vi.fn(() => 'mock-token'),
  setStoredToken: vi.fn(),
  clearStoredToken: vi.fn(),
  STORAGE_KEY: 'forge_api_token',
}))

describe('portConfigApi — 端口检测 API 客户端', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('checkPortAvailable 解析后端 camelCase 响应为可用', async () => {
    mockRequest.mockResolvedValue({
      data: { portNumber: 8080, isAvailable: true },
    })
    const { portConfigApi } = await import('@/services/portConfigApi')
    const result = await portConfigApi.checkPortAvailable(8080)
    expect(result).toEqual({ port: 8080, available: true, message: undefined })
    expect(mockRequest).toHaveBeenCalledWith('/api/portconfiguration/check/8080')
  })

  it('checkPortAvailable 解析后端 camelCase 响应为不可用（含消息）', async () => {
    mockRequest.mockResolvedValue({
      data: { portNumber: 9000, isAvailable: false, message: '端口已被占用' },
    })
    const { portConfigApi } = await import('@/services/portConfigApi')
    const result = await portConfigApi.checkPortAvailable(9000)
    expect(result).toEqual({ port: 9000, available: false, message: '端口已被占用' })
  })

  it('checkPortAvailable 抛出异常时透传错误', async () => {
    mockRequest.mockRejectedValue(new Error('Network Error'))
    const { portConfigApi } = await import('@/services/portConfigApi')
    await expect(portConfigApi.checkPortAvailable(8080)).rejects.toThrow('Network Error')
  })

  it('getPortConfig 返回端口配置', async () => {
    mockRequest.mockResolvedValue({ data: { portNumber: 7102 } })
    const { portConfigApi } = await import('@/services/portConfigApi')
    const result = await portConfigApi.getPortConfig()
    expect(result).toEqual({ portNumber: 7102 })
    expect(mockRequest).toHaveBeenCalledWith('/api/portconfiguration')
  })

  it('updatePortConfig 发送 POST 请求并返回新配置', async () => {
    mockRequest.mockResolvedValue({ data: { portNumber: 8080 } })
    const { portConfigApi } = await import('@/services/portConfigApi')
    const result = await portConfigApi.updatePortConfig(8080)
    expect(result).toEqual({ portNumber: 8080 })
    expect(mockRequest).toHaveBeenCalledWith('/api/portconfiguration', {
      method: 'POST',
      body: JSON.stringify({ PortNumber: 8080 }),
    })
  })
})