import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import ApiServerPanel from '@/components/settings/ApiServerPanel.vue'

// ============================================================
// Mock 依赖 — 所有 vi.mock 引用的变量必须用 vi.hoisted 定义
// ============================================================

const { mockGetPortConfig, mockUpdatePortConfig, mockCheckPortAvailable } = vi.hoisted(() => ({
  mockGetPortConfig: vi.fn(),
  mockUpdatePortConfig: vi.fn(),
  mockCheckPortAvailable: vi.fn(),
}))
vi.mock('@/services/portConfigApi', () => ({
  portConfigApi: {
    getPortConfig: (...args: unknown[]) => mockGetPortConfig(...args),
    updatePortConfig: (...args: unknown[]) => mockUpdatePortConfig(...args),
    checkPortAvailable: (...args: unknown[]) => mockCheckPortAvailable(...args),
  },
}))

const { mockGetConfig, mockRestart } = vi.hoisted(() => ({
  mockGetConfig: vi.fn(),
  mockRestart: vi.fn(),
}))
vi.mock('@/services/apiServerApi', () => ({
  apiServerApi: {
    getConfig: (...args: unknown[]) => mockGetConfig(...args),
    restart: (...args: unknown[]) => mockRestart(...args),
    regenerateKey: vi.fn(),
  },
}))

const { mockPollForRestart, mockFormatPollDuration } = vi.hoisted(() => ({
  mockPollForRestart: vi.fn(),
  mockFormatPollDuration: vi.fn(),
}))
vi.mock('@/services/applicationRestart', () => ({
  pollForRestart: (...args: unknown[]) => mockPollForRestart(...args),
  formatPollDuration: (...args: unknown[]) => mockFormatPollDuration(...args),
}))

const { mockElMessage, mockElMessageBox } = vi.hoisted(() => ({
  mockElMessage: { success: vi.fn(), warning: vi.fn(), error: vi.fn(), info: vi.fn() },
  mockElMessageBox: { confirm: vi.fn() },
}))
vi.mock('element-plus', async (importOriginal) => {
  const actual = await importOriginal()
  return {
    ...(actual as object),
    ElMessage: mockElMessage,
    ElMessageBox: mockElMessageBox,
  }
})

// ============================================================
// Mock 数据
// ============================================================

const MOCK_PORT_CONFIG = { portNumber: 7102 }
const MOCK_API_CONFIG = {
  apiBaseUrl: 'http://localhost:7102',
  apiKeyPlain: 'sk-mock-key-12345',
  apiKeyMasked: 'sk-moc***345',
  authHeader: 'Bearer sk-mock-key-12345',
}

// ============================================================
// 测试
// ============================================================

describe('ApiServerPanel — 端口配置编辑与检测', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    setActivePinia(createPinia())

    // 默认 mock：API 服务正常
    mockGetConfig.mockResolvedValue(MOCK_API_CONFIG)
    mockGetPortConfig.mockResolvedValue(MOCK_PORT_CONFIG)
    mockRestart.mockResolvedValue(undefined)
    mockPollForRestart.mockResolvedValue(true)
    mockFormatPollDuration.mockReturnValue('5秒')
    mockElMessageBox.confirm.mockResolvedValue(undefined)
  })

  function mountPanel() {
    return mount(ApiServerPanel, { attachTo: document.body })
  }

  it('挂载后加载配置并显示端口', async () => {
    const wrapper = mountPanel()
    await flushPromises()

    expect(mockGetConfig).toHaveBeenCalled()
    expect(mockGetPortConfig).toHaveBeenCalled()
    expect(wrapper.text()).toContain('端口配置')
    expect(wrapper.text()).toContain('http://localhost:7102/v1')
  })

  it('端口号超出范围时显示错误并阻止后续流程', async () => {
    const wrapper = mountPanel()
    await flushPromises()

    // 进入编辑模式
    const editButton = wrapper.find('.el-card').findAll('button')[0]
    await editButton.trigger('click')
    await wrapper.vm.$nextTick()

    // 设置无效端口号
    const input = wrapper.find('input[type="number"]')
    await input.setValue(0)
    await wrapper.vm.$nextTick()

    // 点击保存
    const saveButton = wrapper.find('.el-card').findAll('button')[0]
    await saveButton.trigger('click')
    await flushPromises()

    // 验证错误提示且不调用 API
    expect(mockElMessage.error).toHaveBeenCalledWith(
      expect.objectContaining({ message: '端口号必须在 1-65535 之间' }),
    )
    expect(mockCheckPortAvailable).not.toHaveBeenCalled()
  })

  it('端口号未改变时提示并退出编辑', async () => {
    const wrapper = mountPanel()
    await flushPromises()

    // 进入编辑模式
    const editButton = wrapper.find('.el-card').findAll('button')[0]
    await editButton.trigger('click')
    await wrapper.vm.$nextTick()

    // 保存时端口仍是 7102（未修改）
    const input = wrapper.find('input[type="number"]')
    await input.setValue(7102)
    await wrapper.vm.$nextTick()

    const saveButton = wrapper.find('.el-card').findAll('button')[0]
    await saveButton.trigger('click')
    await flushPromises()

    expect(mockElMessage.info).toHaveBeenCalledWith(
      expect.objectContaining({ message: '端口号未改变' }),
    )
    expect(mockCheckPortAvailable).not.toHaveBeenCalled()
  })

  it('端口不可用时显示错误提示', async () => {
    mockCheckPortAvailable.mockResolvedValue({ port: 9000, available: false, message: '端口已被占用' })

    const wrapper = mountPanel()
    await flushPromises()

    const editButton = wrapper.find('.el-card').findAll('button')[0]
    await editButton.trigger('click')
    await wrapper.vm.$nextTick()

    const input = wrapper.find('input[type="number"]')
    await input.setValue(9000)
    await wrapper.vm.$nextTick()

    const saveButton = wrapper.find('.el-card').findAll('button')[0]
    await saveButton.trigger('click')
    await flushPromises()

    expect(mockCheckPortAvailable).toHaveBeenCalledWith(9000)
    expect(mockElMessage.error).toHaveBeenCalledWith(
      expect.objectContaining({ message: '端口已被占用' }),
    )
    // 不应继续保存流程
    expect(mockElMessageBox.confirm).not.toHaveBeenCalled()
  })

  it('端口检测 API 异常时显示错误提示', async () => {
    mockCheckPortAvailable.mockRejectedValue(new Error('连接超时'))

    const wrapper = mountPanel()
    await flushPromises()

    const editButton = wrapper.find('.el-card').findAll('button')[0]
    await editButton.trigger('click')
    await wrapper.vm.$nextTick()

    const input = wrapper.find('input[type="number"]')
    await input.setValue(8080)
    await wrapper.vm.$nextTick()

    const saveButton = wrapper.find('.el-card').findAll('button')[0]
    await saveButton.trigger('click')
    await flushPromises()

    expect(mockElMessage.error).toHaveBeenCalledWith(
      expect.objectContaining({ message: '检查端口失败：连接超时' }),
    )
    expect(mockElMessageBox.confirm).not.toHaveBeenCalled()
  })

  it('端口可用且确认后，完整保存流程成功', async () => {
    mockCheckPortAvailable.mockResolvedValue({ port: 8080, available: true })
    mockUpdatePortConfig.mockResolvedValue({ portNumber: 8080 })

    const wrapper = mountPanel()
    await flushPromises()

    // 进入编辑模式
    const editButton = wrapper.find('.el-card').findAll('button')[0]
    await editButton.trigger('click')
    await wrapper.vm.$nextTick()

    // 设置新端口
    const input = wrapper.find('input[type="number"]')
    await input.setValue(8080)
    await wrapper.vm.$nextTick()

    // 点击保存
    const saveButton = wrapper.find('.el-card').findAll('button')[0]
    await saveButton.trigger('click')
    await flushPromises()

    // 端口检查通过
    expect(mockCheckPortAvailable).toHaveBeenCalledWith(8080)
    // 确认弹窗显示
    expect(mockElMessageBox.confirm).toHaveBeenCalledWith(
      expect.stringContaining('修改端口号需要重启服务'),
      '确认修改端口',
      expect.any(Object),
    )
    // 保存配置
    expect(mockUpdatePortConfig).toHaveBeenCalledWith(8080)
    // 重启服务
    expect(mockRestart).toHaveBeenCalled()
    // 轮询新端口
    expect(mockPollForRestart).toHaveBeenCalledWith(8080, expect.any(Object))
    // 成功消息
    expect(mockElMessage.success).toHaveBeenCalledWith(
      expect.stringContaining('服务已在新端口 8080 重新启动'),
    )
  })

  it('用户取消确认弹窗时中止保存流程', async () => {
    mockCheckPortAvailable.mockResolvedValue({ port: 8080, available: true })
    // 用户取消确认
    mockElMessageBox.confirm.mockRejectedValue(new Error('cancel'))

    const wrapper = mountPanel()
    await flushPromises()

    const editButton = wrapper.find('.el-card').findAll('button')[0]
    await editButton.trigger('click')
    await wrapper.vm.$nextTick()

    const input = wrapper.find('input[type="number"]')
    await input.setValue(8080)
    await wrapper.vm.$nextTick()

    const saveButton = wrapper.find('.el-card').findAll('button')[0]
    await saveButton.trigger('click')
    await flushPromises()

    // 端口检查通过，但弹窗取消后中止
    expect(mockCheckPortAvailable).toHaveBeenCalledWith(8080)
    expect(mockElMessageBox.confirm).toHaveBeenCalled()
    expect(mockUpdatePortConfig).not.toHaveBeenCalled()
    expect(mockRestart).not.toHaveBeenCalled()
  })

  it('编辑后取消，端口恢复显示原始值', async () => {
    const wrapper = mountPanel()
    await flushPromises()

    // 进入编辑模式
    const editButton = wrapper.find('.el-card').findAll('button')[0]
    await editButton.trigger('click')
    await wrapper.vm.$nextTick()

    // 修改端口然后取消
    const input = wrapper.find('input[type="number"]')
    await input.setValue(9999)
    await wrapper.vm.$nextTick()

    // 点取消按钮（编辑模式有 保存 + 取消两个按钮）
    const buttons = wrapper.find('.el-card').findAll('button')
    await buttons[buttons.length - 1].trigger('click')
    await wrapper.vm.$nextTick()

    // 恢复显示原始端口
    expect(wrapper.text()).toContain('http://localhost:7102/v1')
  })
})