import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { setActivePinia, createPinia } from 'pinia'
import PluginCard from '@/components/PluginCard.vue'
import { PluginState } from '@/types/plugin'
import type { PluginInfo } from '@/types/plugin'
import { usePluginStore } from '@/stores/plugin'

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

describe('PluginCard', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  const createMockPlugin = (overrides: Partial<PluginInfo> = {}): PluginInfo => ({
    id: 'test-plugin-1',
    name: '测试插件',
    version: '1.0.0',
    author: '测试作者',
    description: '这是一个测试插件的描述信息',
    iconUrl: undefined,
    state: PluginState.Running,
    isEnabled: true,
    category: '工具',
    tags: [],
    installCount: 0,
    rating: 0,
    ...overrides,
  })

  describe('渲染测试', () => {
    it('渲染插件名称', () => {
      const plugin = createMockPlugin({ name: '我的插件' })
      const wrapper = mount(PluginCard, {
        props: { plugin },
      })

      expect(wrapper.find('.plugin-name').text()).toBe('我的插件')
    })

    it('渲染插件版本', () => {
      const plugin = createMockPlugin({ version: '2.3.1' })
      const wrapper = mount(PluginCard, {
        props: { plugin },
      })

      expect(wrapper.find('.plugin-version').text()).toBe('v2.3.1')
    })

    it('渲染插件描述', () => {
      const plugin = createMockPlugin({ description: '这是一个非常有用的插件' })
      const wrapper = mount(PluginCard, {
        props: { plugin },
      })

      expect(wrapper.find('.plugin-description').text()).toBe('这是一个非常有用的插件')
    })

    it('渲染插件作者', () => {
      const plugin = createMockPlugin({ author: '张三' })
      const wrapper = mount(PluginCard, {
        props: { plugin },
      })

      expect(wrapper.find('.plugin-author').text()).toBe('作者: 张三')
    })

    it('渲染插件分类标签', () => {
      const plugin = createMockPlugin({ category: '效率工具' })
      const wrapper = mount(PluginCard, {
        props: { plugin },
      })

      expect(wrapper.find('.plugin-category').text()).toBe('效率工具')
    })

    it('没有图标时显示默认图标', () => {
      const plugin = createMockPlugin({ iconUrl: undefined })
      const wrapper = mount(PluginCard, {
        props: { plugin },
      })

      expect(wrapper.find('.icon-fallback').exists()).toBe(true)
      expect(wrapper.find('.icon-fallback').text()).toBe('📦')
    })

    it('有图标时显示图标图片', () => {
      const plugin = createMockPlugin({ iconUrl: 'https://example.com/icon.png' })
      const wrapper = mount(PluginCard, {
        props: { plugin },
      })

      const img = wrapper.find('.plugin-icon img')
      expect(img.exists()).toBe(true)
      expect(img.attributes('src')).toBe('https://example.com/icon.png')
      expect(img.attributes('alt')).toBe('测试插件')
    })

    it('渲染使用次数（当提供时）', () => {
      const plugin = createMockPlugin()
      const wrapper = mount(PluginCard, {
        props: { plugin, usageCount: 42 },
      })

      expect(wrapper.find('.plugin-usage').exists()).toBe(true)
      expect(wrapper.find('.plugin-usage').text()).toBe('使用 42 次')
    })

    it('不渲染使用次数（当未提供时）', () => {
      const plugin = createMockPlugin()
      const wrapper = mount(PluginCard, {
        props: { plugin },
      })

      expect(wrapper.find('.plugin-usage').exists()).toBe(false)
    })

    it('设置正确的 aria-label', () => {
      const plugin = createMockPlugin({
        name: '测试插件',
        version: '1.0.0',
        state: PluginState.Running,
      })
      const wrapper = mount(PluginCard, {
        props: { plugin },
      })

      expect(wrapper.find('.plugin-card').attributes('aria-label')).toBe('插件 测试插件，版本 1.0.0，状态 已启用')
    })
  })

  describe('状态显示测试', () => {
    it('已启用状态显示正确', () => {
      const plugin = createMockPlugin({ state: PluginState.Running, isEnabled: true })
      const wrapper = mount(PluginCard, {
        props: { plugin },
      })

      expect(wrapper.find('.state-text').text()).toBe('已启用')
      expect(wrapper.find('.plugin-state').classes()).toContain('state-Running')
    })

    it('已禁用状态显示正确', () => {
      const plugin = createMockPlugin({ state: PluginState.Stopped, isEnabled: false })
      const wrapper = mount(PluginCard, {
        props: { plugin },
      })

      expect(wrapper.find('.state-text').text()).toBe('已禁用')
      expect(wrapper.find('.plugin-state').classes()).toContain('state-Stopped')
    })

    it('错误状态显示正确', () => {
      const plugin = createMockPlugin({ state: PluginState.Error, isEnabled: false })
      const wrapper = mount(PluginCard, {
        props: { plugin },
      })

      expect(wrapper.find('.state-text').text()).toBe('错误')
      expect(wrapper.find('.plugin-state').classes()).toContain('state-Error')
      expect(wrapper.find('.plugin-card').classes()).toContain('error')
    })

    it('已安装状态显示正确', () => {
      const plugin = createMockPlugin({ state: PluginState.Loaded, isEnabled: false })
      const wrapper = mount(PluginCard, {
        props: { plugin },
      })

      expect(wrapper.find('.state-text').text()).toBe('已安装')
      expect(wrapper.find('.plugin-state').classes()).toContain('state-Loaded')
    })

    it('已禁用的插件添加 disabled 类', () => {
      const plugin = createMockPlugin({ isEnabled: false })
      const wrapper = mount(PluginCard, {
        props: { plugin },
      })

      expect(wrapper.find('.plugin-card').classes()).toContain('disabled')
    })

    it('已启用的插件不添加 disabled 类', () => {
      const plugin = createMockPlugin({ isEnabled: true })
      const wrapper = mount(PluginCard, {
        props: { plugin },
      })

      expect(wrapper.find('.plugin-card').classes()).not.toContain('disabled')
    })
  })

  describe('启用/禁用开关测试', () => {
    it('启用状态下开关有 active 类', () => {
      const plugin = createMockPlugin({ isEnabled: true })
      const wrapper = mount(PluginCard, {
        props: { plugin },
      })

      expect(wrapper.find('.toggle-switch').classes()).toContain('active')
    })

    it('禁用状态下开关没有 active 类', () => {
      const plugin = createMockPlugin({ isEnabled: false })
      const wrapper = mount(PluginCard, {
        props: { plugin },
      })

      expect(wrapper.find('.toggle-switch').classes()).not.toContain('active')
    })

    it('错误状态下开关被禁用', () => {
      const plugin = createMockPlugin({ state: PluginState.Error })
      const wrapper = mount(PluginCard, {
        props: { plugin },
      })

      expect(wrapper.find('.toggle-switch').attributes('disabled')).toBeDefined()
    })

    it('开关设置正确的 aria 属性', () => {
      const plugin = createMockPlugin({ isEnabled: true })
      const wrapper = mount(PluginCard, {
        props: { plugin },
      })

      const toggle = wrapper.find('.toggle-switch')
      expect(toggle.attributes('role')).toBe('switch')
      expect(toggle.attributes('aria-checked')).toBe('true')
      expect(toggle.attributes('aria-label')).toBe('禁用插件')
    })

    it('点击开关触发 toggle 事件（启用->禁用）', async () => {
      const plugin = createMockPlugin({ isEnabled: true, state: PluginState.Running })
      const store = usePluginStore()
      store.disablePlugin = vi.fn().mockResolvedValue(undefined)

      const wrapper = mount(PluginCard, {
        props: { plugin },
      })

      await wrapper.find('.toggle-switch').trigger('click')
      await flushPromises()

      expect(wrapper.emitted('toggle')).toBeTruthy()
      expect(wrapper.emitted('toggle')![0][0]).toEqual(plugin)
    })

    it('点击开关触发 toggle 事件（禁用->启用）', async () => {
      const plugin = createMockPlugin({ isEnabled: false, state: PluginState.Stopped })
      const store = usePluginStore()
      store.enablePlugin = vi.fn().mockResolvedValue(undefined)

      const wrapper = mount(PluginCard, {
        props: { plugin },
      })

      await wrapper.find('.toggle-switch').trigger('click')
      await flushPromises()

      expect(wrapper.emitted('toggle')).toBeTruthy()
      expect(wrapper.emitted('toggle')![0][0]).toEqual(plugin)
    })

    it('点击开关不触发卡片 click 事件', async () => {
      const plugin = createMockPlugin({ isEnabled: true })
      const store = usePluginStore()
      store.disablePlugin = vi.fn().mockResolvedValue(undefined)

      const wrapper = mount(PluginCard, {
        props: { plugin },
      })

      await wrapper.find('.toggle-switch').trigger('click')
      await flushPromises()

      expect(wrapper.emitted('click')).toBeFalsy()
    })

    it('错误状态下点击开关不触发事件', async () => {
      const plugin = createMockPlugin({ state: PluginState.Error, isEnabled: false })
      const store = usePluginStore()
      store.enablePlugin = vi.fn().mockResolvedValue(undefined)

      const wrapper = mount(PluginCard, {
        props: { plugin },
      })

      await wrapper.find('.toggle-switch').trigger('click')
      await flushPromises()

      expect(wrapper.emitted('toggle')).toBeFalsy()
      expect(store.enablePlugin).not.toHaveBeenCalled()
    })
  })

  describe('卡片点击测试', () => {
    it('点击卡片触发 click 事件', async () => {
      const plugin = createMockPlugin()
      const wrapper = mount(PluginCard, {
        props: { plugin },
      })

      await wrapper.find('.plugin-card').trigger('click')

      expect(wrapper.emitted('click')).toBeTruthy()
      expect(wrapper.emitted('click')![0][0]).toEqual(plugin)
    })

    it('按 Enter 键触发 click 事件', async () => {
      const plugin = createMockPlugin()
      const wrapper = mount(PluginCard, {
        props: { plugin },
      })

      await wrapper.find('.plugin-card').trigger('keydown', { key: 'Enter' })

      expect(wrapper.emitted('click')).toBeTruthy()
    })

    it('按 Space 键触发 click 事件', async () => {
      const plugin = createMockPlugin()
      const wrapper = mount(PluginCard, {
        props: { plugin },
      })

      await wrapper.find('.plugin-card').trigger('keydown', { key: ' ' })

      expect(wrapper.emitted('click')).toBeTruthy()
    })

    it('按其他键不触发 click 事件', async () => {
      const plugin = createMockPlugin()
      const wrapper = mount(PluginCard, {
        props: { plugin },
      })

      await wrapper.find('.plugin-card').trigger('keydown', { key: 'ArrowDown' })

      expect(wrapper.emitted('click')).toBeFalsy()
    })
  })

  describe('分类标签显示测试', () => {
    it('显示工具分类标签', () => {
      const plugin = createMockPlugin({ category: '工具' })
      const wrapper = mount(PluginCard, {
        props: { plugin },
      })

      expect(wrapper.find('.plugin-category').text()).toBe('工具')
    })

    it('显示效率分类标签', () => {
      const plugin = createMockPlugin({ category: '效率' })
      const wrapper = mount(PluginCard, {
        props: { plugin },
      })

      expect(wrapper.find('.plugin-category').text()).toBe('效率')
    })

    it('显示开发分类标签', () => {
      const plugin = createMockPlugin({ category: '开发' })
      const wrapper = mount(PluginCard, {
        props: { plugin },
      })

      expect(wrapper.find('.plugin-category').text()).toBe('开发')
    })
  })
})