import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { setActivePinia, createPinia } from 'pinia'
import PluginDetailModal from '@/components/PluginDetailModal.vue'
import { PluginState, PluginPermission } from '@/types/plugin'
import type { PluginDetail } from '@/types/plugin'

vi.mock('@/services/pluginApi', () => ({
  pluginApi: {
    fetchPlugins: vi.fn(),
    fetchPluginDetail: vi.fn(),
    enablePlugin: vi.fn(),
    disablePlugin: vi.fn(),
    fetchMenuItems: vi.fn(),
    fetchToolFunctions: vi.fn(),
  },
}))

import { pluginApi } from '@/services/pluginApi'

describe('PluginDetailModal', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  const createMockPluginDetail = (overrides: Partial<PluginDetail> = {}): PluginDetail => ({
    id: 'test-plugin-1',
    name: '测试插件',
    version: '1.0.0',
    author: '测试作者',
    description: '这是一个测试插件的详细描述信息，包含了插件的主要功能和特点。',
    iconUrl: undefined,
    state: PluginState.Running,
    isEnabled: true,
    category: '工具',
    tags: [],
    installCount: 0,
    rating: 0,
    dependencies: ['依赖插件A', '依赖插件B'],
    permissions: [PluginPermission.ReadData, PluginPermission.WriteData, PluginPermission.NetworkAccess],
    extensionPoints: ['menu', 'tool', 'route'],
    screenshots: [],
    homepageUrl: '',
    repositoryUrl: '',
    license: '',
    releaseNotes: '',
    hasUpdate: false,
    usageStats: {
      totalUsage: 128,
      todayUsage: 0,
      averageDailyUsage: 0,
      lastUsedAt: '2024-01-15T10:30:00',
    },
    ...overrides,
  })

const getModalElement = () => document.querySelector('.modal-overlay') as HTMLElement | null

describe('渲染测试', () => {
    it('visible 为 false 时不显示弹窗', () => {
      const wrapper = mount(PluginDetailModal, {
        props: { visible: false },
        attachTo: document.body,
      })

      expect(getModalElement()).toBeNull()
      wrapper.unmount()
    })

    it('visible 为 true 时显示弹窗', async () => {
      const pluginDetail = createMockPluginDetail()
      vi.mocked(pluginApi.fetchPluginDetail).mockResolvedValue(pluginDetail)

      const wrapper = mount(PluginDetailModal, {
        props: { visible: true, pluginId: 'test-plugin-1' },
        attachTo: document.body,
      })

      await flushPromises()

      expect(getModalElement()).not.toBeNull()
      wrapper.unmount()
    })

    it('显示插件名称', async () => {
      const pluginDetail = createMockPluginDetail({ name: '超级工具插件' })
      vi.mocked(pluginApi.fetchPluginDetail).mockResolvedValue(pluginDetail)

      const wrapper = mount(PluginDetailModal, {
        props: { visible: true, pluginId: 'test-plugin-1' },
        attachTo: document.body,
      })

      await flushPromises()

      const modal = getModalElement()
      expect(modal?.querySelector('.plugin-name')?.textContent).toBe('超级工具插件')
      wrapper.unmount()
    })

    it('显示插件版本', async () => {
      const pluginDetail = createMockPluginDetail({ version: '2.5.0' })
      vi.mocked(pluginApi.fetchPluginDetail).mockResolvedValue(pluginDetail)

      const wrapper = mount(PluginDetailModal, {
        props: { visible: true, pluginId: 'test-plugin-1' },
        attachTo: document.body,
      })

      await flushPromises()

      const modal = getModalElement()
      expect(modal?.querySelector('.plugin-version')?.textContent).toBe('版本 2.5.0')
      wrapper.unmount()
    })

    it('显示插件作者', async () => {
      const pluginDetail = createMockPluginDetail({ author: '开发者小明' })
      vi.mocked(pluginApi.fetchPluginDetail).mockResolvedValue(pluginDetail)

      const wrapper = mount(PluginDetailModal, {
        props: { visible: true, pluginId: 'test-plugin-1' },
        attachTo: document.body,
      })

      await flushPromises()

      const modal = getModalElement()
      expect(modal?.querySelector('.plugin-author')?.textContent).toBe('作者: 开发者小明')
      wrapper.unmount()
    })

    it('显示插件描述', async () => {
      const pluginDetail = createMockPluginDetail({ description: '这是一个功能强大的插件' })
      vi.mocked(pluginApi.fetchPluginDetail).mockResolvedValue(pluginDetail)

      const wrapper = mount(PluginDetailModal, {
        props: { visible: true, pluginId: 'test-plugin-1' },
        attachTo: document.body,
      })

      await flushPromises()

      const modal = getModalElement()
      expect(modal?.querySelector('.plugin-description-full p')?.textContent).toBe('这是一个功能强大的插件')
      wrapper.unmount()
    })

    it('显示插件分类', async () => {
      const pluginDetail = createMockPluginDetail({ category: '效率工具' })
      vi.mocked(pluginApi.fetchPluginDetail).mockResolvedValue(pluginDetail)

      const wrapper = mount(PluginDetailModal, {
        props: { visible: true, pluginId: 'test-plugin-1' },
        attachTo: document.body,
      })

      await flushPromises()

      const modal = getModalElement()
      expect(modal?.querySelector('.category-tag')?.textContent).toBe('效率工具')
      wrapper.unmount()
    })

    it('显示使用统计', async () => {
      const pluginDetail = createMockPluginDetail({
        usageStats: {
          totalUsage: 256,
          todayUsage: 0,
          averageDailyUsage: 0,
          lastUsedAt: '2024-06-20T14:30:00',
        },
      })
      vi.mocked(pluginApi.fetchPluginDetail).mockResolvedValue(pluginDetail)

      const wrapper = mount(PluginDetailModal, {
        props: { visible: true, pluginId: 'test-plugin-1' },
        attachTo: document.body,
      })

      await flushPromises()

      const modal = getModalElement()
      const statItems = modal?.querySelectorAll('.stat-item')
      expect(statItems?.[0]?.querySelector('.stat-value')?.textContent).toBe('256')
      expect(statItems?.[0]?.querySelector('.stat-label')?.textContent).toBe('总使用次数')
      wrapper.unmount()
    })

    it('没有 lastUsedAt 时显示"从未使用"', async () => {
      const pluginDetail = createMockPluginDetail({
        usageStats: {
          totalUsage: 0,
          todayUsage: 0,
          averageDailyUsage: 0,
          lastUsedAt: undefined,
        },
      })
      vi.mocked(pluginApi.fetchPluginDetail).mockResolvedValue(pluginDetail)

      const wrapper = mount(PluginDetailModal, {
        props: { visible: true, pluginId: 'test-plugin-1' },
        attachTo: document.body,
      })

      await flushPromises()

      const modal = getModalElement()
      const statItems = modal?.querySelectorAll('.stat-item')
      expect(statItems?.[1]?.querySelector('.stat-value')?.textContent).toBe('从未使用')
      wrapper.unmount()
    })

    it('加载中显示加载状态', async () => {
      let resolveFn: (plugin: PluginDetail) => void
      const promise = new Promise<PluginDetail>((resolve) => {
        resolveFn = resolve
      })
      vi.mocked(pluginApi.fetchPluginDetail).mockReturnValue(promise)

      const wrapper = mount(PluginDetailModal, {
        props: { visible: true, pluginId: 'test-plugin-1' },
        attachTo: document.body,
      })

      await wrapper.vm.$nextTick()

      const modal = getModalElement()
      expect(modal?.querySelector('.modal-loading')).not.toBeNull()
      expect(modal?.querySelector('.loading-spinner')).not.toBeNull()

      resolveFn!(createMockPluginDetail())
      await flushPromises()
      wrapper.unmount()
    })

    it('加载失败显示错误状态', async () => {
      vi.mocked(pluginApi.fetchPluginDetail).mockRejectedValue(new Error('加载失败'))

      const wrapper = mount(PluginDetailModal, {
        props: { visible: true, pluginId: 'test-plugin-1' },
        attachTo: document.body,
      })

      await flushPromises()

      const modal = getModalElement()
      expect(modal?.querySelector('.modal-error')).not.toBeNull()
      expect(modal?.querySelector('.error-icon')?.textContent).toBe('⚠️')
      wrapper.unmount()
    })
  })

  describe('依赖列表显示测试', () => {
    it('有依赖时显示依赖列表', async () => {
      const pluginDetail = createMockPluginDetail({
        dependencies: ['plugin-a', 'plugin-b', 'plugin-c'],
      })
      vi.mocked(pluginApi.fetchPluginDetail).mockResolvedValue(pluginDetail)

      const wrapper = mount(PluginDetailModal, {
        props: { visible: true, pluginId: 'test-plugin-1' },
        attachTo: document.body,
      })

      await flushPromises()

      const modal = getModalElement()
      const deps = modal?.querySelectorAll('.dependency-item')
      expect(deps?.length).toBe(3)
      expect(deps?.[0]?.textContent).toBe('plugin-a')
      expect(deps?.[1]?.textContent).toBe('plugin-b')
      expect(deps?.[2]?.textContent).toBe('plugin-c')
      wrapper.unmount()
    })

    it('没有依赖时不显示依赖部分', async () => {
      const pluginDetail = createMockPluginDetail({ dependencies: [] })
      vi.mocked(pluginApi.fetchPluginDetail).mockResolvedValue(pluginDetail)

      const wrapper = mount(PluginDetailModal, {
        props: { visible: true, pluginId: 'test-plugin-1' },
        attachTo: document.body,
      })

      await flushPromises()

      const modal = getModalElement()
      expect(modal?.querySelector('.dependencies-list')).toBeNull()
      wrapper.unmount()
    })
  })

  describe('权限列表显示测试', () => {
    it('有权限时显示权限列表', async () => {
      const pluginDetail = createMockPluginDetail({
        permissions: [PluginPermission.ReadData, PluginPermission.WriteData],
      })
      vi.mocked(pluginApi.fetchPluginDetail).mockResolvedValue(pluginDetail)

      const wrapper = mount(PluginDetailModal, {
        props: { visible: true, pluginId: 'test-plugin-1' },
        attachTo: document.body,
      })

      await flushPromises()

      const modal = getModalElement()
      const perms = modal?.querySelectorAll('.permission-item')
      expect(perms?.length).toBe(2)
      wrapper.unmount()
    })

    it('显示权限描述', async () => {
      const pluginDetail = createMockPluginDetail({
        permissions: [PluginPermission.ReadData],
      })
      vi.mocked(pluginApi.fetchPluginDetail).mockResolvedValue(pluginDetail)

      const wrapper = mount(PluginDetailModal, {
        props: { visible: true, pluginId: 'test-plugin-1' },
        attachTo: document.body,
      })

      await flushPromises()

      const modal = getModalElement()
      expect(modal?.querySelector('.permission-name')?.textContent).toBe('读取应用数据')
      expect(modal?.querySelector('.permission-id')?.textContent).toBe('read:data')
      wrapper.unmount()
    })

    it('没有权限时不显示权限部分', async () => {
      const pluginDetail = createMockPluginDetail({ permissions: [] })
      vi.mocked(pluginApi.fetchPluginDetail).mockResolvedValue(pluginDetail)

      const wrapper = mount(PluginDetailModal, {
        props: { visible: true, pluginId: 'test-plugin-1' },
        attachTo: document.body,
      })

      await flushPromises()

      const modal = getModalElement()
      expect(modal?.querySelector('.permissions-list')).toBeNull()
      wrapper.unmount()
    })
  })

  describe('扩展点显示测试', () => {
    it('显示扩展点列表', async () => {
      const pluginDetail = createMockPluginDetail({
        extensionPoints: ['menu', 'tool'],
      })
      vi.mocked(pluginApi.fetchPluginDetail).mockResolvedValue(pluginDetail)

      const wrapper = mount(PluginDetailModal, {
        props: { visible: true, pluginId: 'test-plugin-1' },
        attachTo: document.body,
      })

      await flushPromises()

      const modal = getModalElement()
      const exts = modal?.querySelectorAll('.extension-item')
      expect(exts?.length).toBe(2)
      wrapper.unmount()
    })

    it('没有扩展点时不显示扩展点部分', async () => {
      const pluginDetail = createMockPluginDetail({ extensionPoints: [] })
      vi.mocked(pluginApi.fetchPluginDetail).mockResolvedValue(pluginDetail)

      const wrapper = mount(PluginDetailModal, {
        props: { visible: true, pluginId: 'test-plugin-1' },
        attachTo: document.body,
      })

      await flushPromises()

      const modal = getModalElement()
      expect(modal?.querySelector('.extensions-grid')).toBeNull()
      wrapper.unmount()
    })
  })

  describe('启用/禁用按钮测试', () => {
    it('已启用状态显示"禁用插件"按钮', async () => {
      const pluginDetail = createMockPluginDetail({ isEnabled: true, state: PluginState.Running })
      vi.mocked(pluginApi.fetchPluginDetail).mockResolvedValue(pluginDetail)

      const wrapper = mount(PluginDetailModal, {
        props: { visible: true, pluginId: 'test-plugin-1' },
        attachTo: document.body,
      })

      await flushPromises()

      const modal = getModalElement()
      const btn = modal?.querySelector('.toggle-btn')
      expect(btn?.textContent).toContain('禁用插件')
      expect(btn?.classList.contains('active')).toBe(true)
      wrapper.unmount()
    })

    it('已禁用状态显示"启用插件"按钮', async () => {
      const pluginDetail = createMockPluginDetail({ isEnabled: false, state: PluginState.Stopped })
      vi.mocked(pluginApi.fetchPluginDetail).mockResolvedValue(pluginDetail)

      const wrapper = mount(PluginDetailModal, {
        props: { visible: true, pluginId: 'test-plugin-1' },
        attachTo: document.body,
      })

      await flushPromises()

      const modal = getModalElement()
      const btn = modal?.querySelector('.toggle-btn')
      expect(btn?.textContent).toContain('启用插件')
      expect(btn?.classList.contains('active')).toBe(false)
      wrapper.unmount()
    })

    it('错误状态下按钮被禁用', async () => {
      const pluginDetail = createMockPluginDetail({ state: PluginState.Error })
      vi.mocked(pluginApi.fetchPluginDetail).mockResolvedValue(pluginDetail)

      const wrapper = mount(PluginDetailModal, {
        props: { visible: true, pluginId: 'test-plugin-1' },
        attachTo: document.body,
      })

      await flushPromises()

      const modal = getModalElement()
      const btn = modal?.querySelector('.toggle-btn')
      expect(btn?.hasAttribute('disabled')).toBe(true)
      wrapper.unmount()
    })

    it('点击按钮触发 toggle 事件', async () => {
      const pluginDetail = createMockPluginDetail({ isEnabled: true, state: PluginState.Running })
      vi.mocked(pluginApi.fetchPluginDetail).mockResolvedValue(pluginDetail)
      vi.mocked(pluginApi.disablePlugin).mockResolvedValue(undefined)

      const wrapper = mount(PluginDetailModal, {
        props: { visible: true, pluginId: 'test-plugin-1' },
        attachTo: document.body,
      })

      await flushPromises()

      const modal = getModalElement()
      const btn = modal?.querySelector('.toggle-btn') as HTMLElement | null
      btn?.click()
      await flushPromises()

      expect(wrapper.emitted('toggle')).toBeTruthy()
      wrapper.unmount()
    })
  })

  describe('关闭弹窗测试', () => {
    it('点击关闭按钮触发 close 事件', async () => {
      const pluginDetail = createMockPluginDetail()
      vi.mocked(pluginApi.fetchPluginDetail).mockResolvedValue(pluginDetail)

      const wrapper = mount(PluginDetailModal, {
        props: { visible: true, pluginId: 'test-plugin-1' },
        attachTo: document.body,
      })

      await flushPromises()

      const modal = getModalElement()
      const closeBtn = modal?.querySelector('.close-btn') as HTMLElement | null
      closeBtn?.click()
      await flushPromises()

      expect(wrapper.emitted('close')).toBeTruthy()
      wrapper.unmount()
    })

    it('点击遮罩层触发 close 事件', async () => {
      const pluginDetail = createMockPluginDetail()
      vi.mocked(pluginApi.fetchPluginDetail).mockResolvedValue(pluginDetail)

      const wrapper = mount(PluginDetailModal, {
        props: { visible: true, pluginId: 'test-plugin-1' },
        attachTo: document.body,
      })

      await flushPromises()

      const modal = getModalElement()
      modal?.click()
      await flushPromises()

      expect(wrapper.emitted('close')).toBeTruthy()
      wrapper.unmount()
    })

    it('按 ESC 键触发 close 事件', async () => {
      const pluginDetail = createMockPluginDetail()
      vi.mocked(pluginApi.fetchPluginDetail).mockResolvedValue(pluginDetail)

      const wrapper = mount(PluginDetailModal, {
        props: { visible: true, pluginId: 'test-plugin-1' },
        attachTo: document.body,
      })

      await flushPromises()

      const event = new KeyboardEvent('keydown', { key: 'Escape' })
      document.dispatchEvent(event)
      await wrapper.vm.$nextTick()

      expect(wrapper.emitted('close')).toBeTruthy()
      wrapper.unmount()
    })

    it('点击弹窗内容不触发 close 事件', async () => {
      const pluginDetail = createMockPluginDetail()
      vi.mocked(pluginApi.fetchPluginDetail).mockResolvedValue(pluginDetail)

      const wrapper = mount(PluginDetailModal, {
        props: { visible: true, pluginId: 'test-plugin-1' },
        attachTo: document.body,
      })

      await flushPromises()

      const modal = getModalElement() as HTMLElement | null
      const container = modal?.querySelector('.modal-container') as HTMLElement | null
      container?.click()
      await flushPromises()

      expect(wrapper.emitted('close')).toBeFalsy()
      wrapper.unmount()
    })
  })

  describe('状态显示测试', () => {
    it('已启用状态显示正确样式', async () => {
      const pluginDetail = createMockPluginDetail({ state: PluginState.Running })
      vi.mocked(pluginApi.fetchPluginDetail).mockResolvedValue(pluginDetail)

      const wrapper = mount(PluginDetailModal, {
        props: { visible: true, pluginId: 'test-plugin-1' },
        attachTo: document.body,
      })

      await flushPromises()

      const modal = getModalElement()
      const stateEl = modal?.querySelector('.plugin-meta-row .plugin-state')
      expect(stateEl?.classList.contains('state-Running')).toBe(true)
      expect(stateEl?.querySelector('.state-text')?.textContent).toBe('已启用')
      wrapper.unmount()
    })

    it('已禁用状态显示正确样式', async () => {
      const pluginDetail = createMockPluginDetail({ state: PluginState.Stopped })
      vi.mocked(pluginApi.fetchPluginDetail).mockResolvedValue(pluginDetail)

      const wrapper = mount(PluginDetailModal, {
        props: { visible: true, pluginId: 'test-plugin-1' },
        attachTo: document.body,
      })

      await flushPromises()

      const modal = getModalElement()
      const stateEl = modal?.querySelector('.plugin-meta-row .plugin-state')
      expect(stateEl?.classList.contains('state-Stopped')).toBe(true)
      expect(stateEl?.querySelector('.state-text')?.textContent).toBe('已禁用')
      wrapper.unmount()
    })
  })
})