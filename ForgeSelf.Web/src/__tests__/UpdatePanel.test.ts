import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { mount, flushPromises, type VueWrapper } from '@vue/test-utils'
import { ElMessageBox } from 'element-plus'
import UpdatePanel from '@/components/settings/UpdatePanel.vue'

/**
 * 输入51 回归测试：设置页「版本更新」面板三个按钮的 loading 状态解耦。
 * 缺陷：检查更新/下载更新按钮 `:loading="xxx || busy()"` 共用全局 stage，
 * 弹窗确认重启（stage=applying）后无关按钮全部转圈。
 * 修复：各按钮 loading 仅由自身动作状态（checking/downloading/applying）驱动，
 * busy() 只用于 :disabled 防并发。
 */

const updateApiMock = vi.hoisted(() => ({
  getStatus: vi.fn(),
  getConfig: vi.fn(),
  check: vi.fn(),
  download: vi.fn(),
  getProgress: vi.fn(),
  saveConfig: vi.fn(),
  apply: vi.fn(),
}))

vi.mock('@/services/updateApi', () => ({ updateApi: updateApiMock }))

vi.mock('element-plus', async (importOriginal) => {
  const actual = await importOriginal()
  return {
    ...(actual as object),
    ElMessage: { success: vi.fn(), warning: vi.fn(), error: vi.fn(), info: vi.fn() },
    ElMessageBox: { confirm: vi.fn() },
  }
})

const hasUpdateCheck = {
  currentVersion: 'v0.0.0',
  latestVersion: 'v1.0.0',
  latestVersionTag: 'v1.0.0',
  hasUpdate: true,
  downloadUrl: null,
  releaseNotes: null,
  packageHash: null,
  packageSize: 1024 * 1024,
  checkTime: '2026-09-30T00:00:00Z',
  errorMessage: null,
  isSuccess: true,
}

const defaultConfig = {
  provider: 'github',
  serverUrl: '',
  githubRepo: 'OpenForgeSelf/OpenForgeSelf',
  giteeRepo: null,
  localDir: null,
  channel: 'stable',
  checkIntervalMinutes: 60,
  checkTimeoutSeconds: 30,
  downloadTimeoutSeconds: 600,
}

function statusWith(stageStatus: string, check: unknown = null) {
  return {
    currentVersion: 'v0.0.0',
    provider: 'github',
    githubRepo: 'OpenForgeSelf/OpenForgeSelf',
    channel: 'stable',
    state: { status: stageStatus, progress: 0, check },
  }
}

function findButton(wrapper: VueWrapper, text: string) {
  const btn = wrapper.findAllComponents({ name: 'ElButton' }).find((b) => b.text() === text)
  expect(btn, `未找到按钮「${text}」`).toBeTruthy()
  return btn!
}

describe('UpdatePanel — 三个更新按钮 loading 状态独立（输入51）', () => {
  let wrapper: VueWrapper

  beforeEach(() => {
    vi.clearAllMocks()
    updateApiMock.getConfig.mockResolvedValue(defaultConfig)
  })

  afterEach(() => {
    wrapper?.unmount() // 触发 onBeforeUnmount(stopPolling)，清理轮询定时器
  })

  it('弹窗确认重启后（stage=applying）仅「重启并更新」转圈，检查/下载按钮只禁用不转圈', async () => {
    updateApiMock.getStatus.mockResolvedValue(statusWith('ready', hasUpdateCheck))
    updateApiMock.apply.mockResolvedValue({ status: 'applying', progress: 0, message: '正在重启' })
    ;(ElMessageBox.confirm as ReturnType<typeof vi.fn>).mockResolvedValue('confirm')

    wrapper = mount(UpdatePanel, { attachTo: document.body })
    await flushPromises()

    const restartBtn = findButton(wrapper, '重启并更新')
    await restartBtn.trigger('click')
    await flushPromises()

    expect(restartBtn.props('loading')).toBe(true) // 真正的动作：转圈
    const checkBtn = findButton(wrapper, '检查更新')
    expect(checkBtn.props('loading')).toBe(false) // 无关按钮不得转圈
    expect(checkBtn.props('disabled')).toBe(true) // 但应禁用防并发
    const downloadBtn = findButton(wrapper, '下载更新')
    expect(downloadBtn.props('loading')).toBe(false)
    expect(downloadBtn.props('disabled')).toBe(true)
  })

  it('下载阶段（stage=downloading）检查/下载按钮均为禁用态且不转圈', async () => {
    updateApiMock.getStatus.mockResolvedValue(statusWith('downloading', hasUpdateCheck))

    wrapper = mount(UpdatePanel, { attachTo: document.body })
    await flushPromises()

    const checkBtn = findButton(wrapper, '检查更新')
    expect(checkBtn.props('loading')).toBe(false)
    expect(checkBtn.props('disabled')).toBe(true)
    const downloadBtn = findButton(wrapper, '下载更新')
    expect(downloadBtn.props('loading')).toBe(false)
    expect(downloadBtn.props('disabled')).toBe(true)
  })

  it('检查更新进行中只有自身按钮转圈，完成后恢复', async () => {
    let resolveCheck!: (v: unknown) => void
    updateApiMock.getStatus.mockResolvedValue(statusWith('idle'))
    updateApiMock.check.mockReturnValue(new Promise((res) => { resolveCheck = res }))

    wrapper = mount(UpdatePanel, { attachTo: document.body })
    await flushPromises()

    const checkBtn = findButton(wrapper, '检查更新')
    await checkBtn.trigger('click')
    await wrapper.vm.$nextTick()

    expect(checkBtn.props('loading')).toBe(true) // 自身动作转圈
    expect(wrapper.findAllComponents({ name: 'ElButton' }).some((b) => b.text() === '下载更新')).toBe(false)

    resolveCheck(hasUpdateCheck)
    await flushPromises()

    expect(checkBtn.props('loading')).toBe(false) // 完成后自身转圈停止
    const downloadBtn = findButton(wrapper, '下载更新')
    expect(downloadBtn.props('loading')).toBe(false)
  })
})
