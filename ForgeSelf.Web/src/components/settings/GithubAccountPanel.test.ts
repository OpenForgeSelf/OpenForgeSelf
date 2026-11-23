import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import GithubAccountPanel from '@/components/settings/GithubAccountPanel.vue'
import { githubAccountApi } from '@/services/githubAccountApi'
import type { GitHubBindingStatus } from '@/types/githubAccount'

vi.mock('@/services/githubAccountApi', () => ({
  githubAccountApi: {
    status: vi.fn(),
    unbind: vi.fn(),
    setAutoAttach: vi.fn(),
    startBind: vi.fn(),
    pollBind: vi.fn(),
  },
}))

vi.mock('element-plus', async (importOriginal) => {
  const actual = await importOriginal<typeof import('element-plus')>()
  return {
    ...actual,
    ElMessage: { success: vi.fn(), warning: vi.fn(), error: vi.fn() },
    ElMessageBox: { confirm: vi.fn().mockResolvedValue(true) },
  }
})

const statusApi = vi.mocked(githubAccountApi.status)
const unbindApi = vi.mocked(githubAccountApi.unbind)
const setAutoAttachApi = vi.mocked(githubAccountApi.setAutoAttach)

function makeStatus(overrides: Partial<GitHubBindingStatus> = {}): GitHubBindingStatus {
  return {
    bound: false,
    accountLogin: '',
    scopes: '',
    boundAt: null,
    autoAttachToken: true,
    ...overrides,
  }
}

/** ElSwitch 在 jsdom 根为 div：统一 stub 成带内层 checkbox 的最小实现，便于 setValue 驱动 change */
const ElSwitchStub = {
  props: { modelValue: { type: Boolean, default: false } },
  emits: ['change'],
  template: `<label class="el-switch-stub"><input type="checkbox" :checked="modelValue" @change="$emit('change', $event.target.checked)" /></label>`,
}

function mountPanel() {
  return mount(GithubAccountPanel, { global: { stubs: { ElSwitch: ElSwitchStub } } })
}

describe('GithubAccountPanel - 设置页绑定面板', () => {
  beforeEach(() => {
    statusApi.mockReset().mockResolvedValue({ success: true, data: makeStatus() })
    unbindApi.mockReset()
    setAutoAttachApi.mockReset()
  })

  afterEach(() => {
    vi.restoreAllMocks()
    document.body.innerHTML = ''
  })

  it('未绑定：展示绑定入口与说明', async () => {
    const wrapper = mountPanel()
    await flushPromises()
    expect(wrapper.find('[data-test="unbound-desc"]').exists()).toBe(true)
    expect(wrapper.find('[data-test="bind-button"]').exists()).toBe(true)
  })

  it('已绑定：展示账号、授权范围、解绑与自动附凭据开关', async () => {
    statusApi.mockResolvedValue({
      success: true,
      data: makeStatus({ bound: true, accountLogin: 'octocat', scopes: 'repo workflow', boundAt: '2026-11-22T10:00:00' }),
    })
    const wrapper = mountPanel()
    await flushPromises()
    expect(wrapper.find('[data-test="account-login"]').text()).toBe('octocat')
    expect(wrapper.find('[data-test="account-scopes"]').text()).toContain('repo workflow')
    expect(wrapper.find('[data-test="unbind-button"]').exists()).toBe(true)
    expect(wrapper.find('[data-test="auto-attach-switch"]').exists()).toBe(true)
  })

  it('status 拉取失败：不渲染绑定点，面板保持空态', async () => {
    statusApi.mockResolvedValue({ success: false, error: '网络错误' })
    const wrapper = mountPanel()
    await flushPromises()
    expect(wrapper.find('[data-test="unbound-desc"]').exists()).toBe(true)
  })

  it('解绑成功：回到未绑定态', async () => {
    statusApi.mockResolvedValue({ success: true, data: makeStatus({ bound: true, accountLogin: 'octocat' }) })
    unbindApi.mockResolvedValue({ success: true, data: makeStatus() })
    const wrapper = mountPanel()
    await flushPromises()
    await wrapper.find('[data-test="unbind-button"]').trigger('click')
    await flushPromises()
    expect(unbindApi).toHaveBeenCalledTimes(1)
    expect(wrapper.find('[data-test="bind-button"]').exists()).toBe(true)
  })

  it('解绑被取消（确认框 reject）：不调用解绑接口', async () => {
    const { ElMessageBox } = await import('element-plus')
    vi.mocked(ElMessageBox.confirm).mockRejectedValueOnce('cancel')
    statusApi.mockResolvedValue({ success: true, data: makeStatus({ bound: true, accountLogin: 'octocat' }) })
    const wrapper = mountPanel()
    await flushPromises()
    await wrapper.find('[data-test="unbind-button"]').trigger('click')
    await flushPromises()
    expect(unbindApi).not.toHaveBeenCalled()
  })

  it('自动附凭据开关切换：调用接口并以返回值刷新', async () => {
    statusApi.mockResolvedValue({ success: true, data: makeStatus({ bound: true, accountLogin: 'octocat', autoAttachToken: true }) })
    setAutoAttachApi.mockResolvedValue({ success: true, data: makeStatus({ bound: true, accountLogin: 'octocat', autoAttachToken: false }) })
    const wrapper = mountPanel()
    await flushPromises()
    await wrapper.find('[data-test="auto-attach-switch"] input').setValue(false)
    await flushPromises()
    expect(setAutoAttachApi).toHaveBeenCalledWith(false)
    expect(wrapper.find('[data-test="auto-attach-row"]').exists()).toBe(true)
  })

  it('自动附凭据设置失败：开关回滚为原值', async () => {
    statusApi.mockResolvedValue({ success: true, data: makeStatus({ bound: true, accountLogin: 'octocat', autoAttachToken: true }) })
    setAutoAttachApi.mockResolvedValue({ success: false, error: '设置失败' })
    const wrapper = mountPanel()
    await flushPromises()
    await wrapper.find('[data-test="auto-attach-switch"] input').setValue(false)
    await flushPromises()
    // 面板仍渲染绑定行（回滚成功，未丢绑定态）
    expect(wrapper.find('[data-test="account-login"]').exists()).toBe(true)
  })
})
