import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import GithubBindPanel from '@/components/github/GithubBindPanel.vue'
import { githubAccountApi } from '@/services/githubAccountApi'
import { copyToClipboard } from '@/utils/clipboard'
import type { DeviceCodeStart } from '@/types/githubAccount'

vi.mock('@/services/githubAccountApi', () => ({
  githubAccountApi: {
    startBind: vi.fn(),
    pollBind: vi.fn(),
  },
}))

vi.mock('@/utils/clipboard', () => ({
  copyToClipboard: vi.fn().mockResolvedValue(true),
}))

vi.mock('element-plus', async (importOriginal) => {
  const actual = await importOriginal<typeof import('element-plus')>()
  return { ...actual, ElMessage: { success: vi.fn(), warning: vi.fn(), error: vi.fn() } }
})

const start = vi.mocked(githubAccountApi.startBind)
const pollBind = vi.mocked(githubAccountApi.pollBind)
const copy = vi.mocked(copyToClipboard)

const deviceStart: DeviceCodeStart = {
  deviceCode: 'DEV123',
  userCode: 'ABCD-1234',
  verificationUri: 'https://github.com/login/device',
  verificationUriPrefilled: 'https://github.com/login/device?user_code=ABCD-1234',
  expiresIn: 900,
  interval: 5,
}

function okEnvelope<T>(data: T) {
  return { success: true, data }
}

function failEnvelope(error: string) {
  return { success: false, error } as { success: false; error: string }
}

async function mountAndStart(): Promise<ReturnType<typeof mount>> {
  const wrapper = mount(GithubBindPanel)
  await wrapper.find('[data-test="bind-button"]').trigger('click')
  await flushPromises()
  return wrapper
}

describe('GithubBindPanel - 绑定 UX（PILOT-083 + 二轮）', () => {
  /** window.open 调用记录（stubGlobal 避免 spyOn 的交叉类型体操） */
  let openCalls: Array<[string | undefined, string | undefined, string | undefined]>

  beforeEach(() => {
    vi.useFakeTimers()
    start.mockReset().mockResolvedValue(okEnvelope(deviceStart))
    pollBind.mockReset().mockResolvedValue(okEnvelope({ status: 'pending', accountLogin: null, error: null }))
    copy.mockClear().mockResolvedValue(true)
    openCalls = []
    vi.stubGlobal('open', (url?: string, target?: string, features?: string) => {
      openCalls.push([url, target, features])
      return null
    })
  })

  afterEach(() => {
    vi.useRealTimers()
    vi.unstubAllGlobals()
    document.body.innerHTML = ''
  })

  it('空闲态只渲染绑定入口按钮', () => {
    const wrapper = mount(GithubBindPanel)
    expect(wrapper.find('[data-test="bind-button"]').exists()).toBe(true)
    expect(wrapper.find('[data-test="code-block"]').exists()).toBe(false)
  })

  it('① 点击后进入准备态：显示「正在准备设备码，请稍候…」', async () => {
    start.mockImplementation(() => new Promise(() => {})) // 挂起，模拟后端等待
    const wrapper = mount(GithubBindPanel)
    await wrapper.find('[data-test="bind-button"]').trigger('click')
    await flushPromises()
    expect(wrapper.find('[data-test="preparing-text"]').text()).toContain('正在准备设备码，请稍候…')
  })

  it('① 准备期单飞：按钮被整块 loading 替换，startBind 只发一次', async () => {
    let resolveStart!: (v: ReturnType<typeof okEnvelope<DeviceCodeStart>>) => void
    start.mockImplementation(() => new Promise((r) => { resolveStart = r }))
    const wrapper = mount(GithubBindPanel)
    await wrapper.find('[data-test="bind-button"]').trigger('click')
    // 进入准备态后入口按钮被移除（结构性防连点），准备文案出现
    expect(wrapper.find('[data-test="bind-button"]').exists()).toBe(false)
    expect(wrapper.find('[data-test="preparing-text"]').exists()).toBe(true)
    resolveStart(okEnvelope(deviceStart))
    await flushPromises()
    expect(start).toHaveBeenCalledTimes(1)
  })

  it('② 出码：显示设备码 + 「设备码已复制到剪贴板」横幅，并自动复制', async () => {
    const wrapper = await mountAndStart()
    expect(wrapper.find('[data-test="user-code"]').text()).toBe('ABCD-1234')
    expect(wrapper.find('[data-test="copied-banner"]').text()).toContain('设备码已复制到剪贴板')
    expect(copy).toHaveBeenCalledWith('ABCD-1234')
  })

  it('② 复制失败时提示手动复制', async () => {
    copy.mockResolvedValue(false)
    const wrapper = await mountAndStart()
    expect(wrapper.find('[data-test="copy-fail-banner"]').exists()).toBe(true)
    expect(wrapper.find('[data-test="copied-banner"]').exists()).toBe(false)
  })

  it('② 出码 3 秒后自动打开预填授权页', async () => {
    await mountAndStart()
    expect(openCalls).toHaveLength(0)
    await vi.advanceTimersByTimeAsync(3100)
    expect(openCalls[0]).toEqual(['https://github.com/login/device?user_code=ABCD-1234', '_blank', 'noopener'])
  })

  it('② 手动复制按钮再次复制设备码', async () => {
    const wrapper = await mountAndStart()
    copy.mockClear()
    await wrapper.find('[data-test="copy-button"]').trigger('click')
    expect(copy).toHaveBeenCalledWith('ABCD-1234')
  })

  it('③ pending：保持出码态继续轮询', async () => {
    pollBind.mockResolvedValue(okEnvelope({ status: 'pending', accountLogin: null, error: null }))
    await mountAndStart()
    await vi.advanceTimersByTimeAsync(3100)
    expect(pollBind).toHaveBeenCalledTimes(0) // 倒计时 3s 尚未到轮询点（间隔 5s）
    await vi.advanceTimersByTimeAsync(2000)
    expect(pollBind).toHaveBeenCalledTimes(1)
    await vi.advanceTimersByTimeAsync(5000)
    expect(pollBind).toHaveBeenCalledTimes(2)
  })

  it('③ slow_down：不终止轮询，退避后继续', async () => {
    pollBind.mockResolvedValueOnce(okEnvelope({ status: 'slow_down', accountLogin: null, error: null }))
      .mockResolvedValue(okEnvelope({ status: 'pending', accountLogin: null, error: null }))
    await mountAndStart()
    await vi.advanceTimersByTimeAsync(5000)
    expect(pollBind).toHaveBeenCalledTimes(1)
    await vi.advanceTimersByTimeAsync(10000) // slow_down +5s → 10s 后下一次
    expect(pollBind).toHaveBeenCalledTimes(2)
  })

  it('③ bound：发射 bound 事件并回到空闲态', async () => {
    pollBind.mockResolvedValue(okEnvelope({ status: 'bound', accountLogin: 'octocat', error: null }))
    const wrapper = await mountAndStart()
    await vi.advanceTimersByTimeAsync(5000)
    await flushPromises()
    expect(wrapper.emitted('bound')).toEqual([['octocat']])
    expect(wrapper.find('[data-test="bind-button"]').exists()).toBe(true)
  })

  it('④ expired：终态错误块透传上游原因 + 重试入口', async () => {
    pollBind.mockResolvedValue(okEnvelope({ status: 'expired', accountLogin: null, error: '设备码已过期' }))
    const wrapper = await mountAndStart()
    await vi.advanceTimersByTimeAsync(5000)
    await flushPromises()
    expect(wrapper.find('[data-test="error-block"]').exists()).toBe(true)
    expect(wrapper.find('[data-test="error-text"]').text()).toContain('设备码已过期')
    expect(wrapper.find('[data-test="retry-button"]').exists()).toBe(true)
  })

  it('④ denied：拒绝授权进终态错误块', async () => {
    pollBind.mockResolvedValue(okEnvelope({ status: 'denied', accountLogin: null, error: '用户已拒绝授权' }))
    const wrapper = await mountAndStart()
    await vi.advanceTimersByTimeAsync(5000)
    await flushPromises()
    expect(wrapper.find('[data-test="error-block"]').exists()).toBe(true)
    expect(wrapper.find('[data-test="error-text"]').text()).toContain('用户已拒绝授权')
  })

  it('④ 未知失败：错误文案含上游 reason', async () => {
    pollBind.mockResolvedValue(okEnvelope({ status: 'failed', accountLogin: null, error: '设备码无效 · bad_verification_code' }))
    const wrapper = await mountAndStart()
    await vi.advanceTimersByTimeAsync(5000)
    await flushPromises()
    expect(wrapper.find('[data-test="error-text"]').text()).toContain('bad_verification_code')
  })

  it('① 设备码申请失败：error 原因透传进错误块', async () => {
    start.mockResolvedValue(failEnvelope('client_id is not valid'))
    const wrapper = mount(GithubBindPanel)
    await wrapper.find('[data-test="bind-button"]').trigger('click')
    await flushPromises()
    expect(wrapper.find('[data-test="error-text"]').text()).toContain('client_id is not valid')
  })
})
