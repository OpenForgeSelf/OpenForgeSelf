import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { setActivePinia, createPinia } from 'pinia'
import PluginStore from '@/views/PluginStore.vue'
import { PluginState } from '@/types/plugin'
import type { PluginInfo, PluginVersionInfo } from '@/types/plugin'

vi.mock('@/services/pluginApi', () => ({
  pluginApi: {
    fetchPlugins: vi.fn(),
    fetchPluginDetail: vi.fn(),
    enablePlugin: vi.fn(),
    disablePlugin: vi.fn(),
    fetchMenuItems: vi.fn(),
    fetchToolFunctions: vi.fn(),
    fetchCategories: vi.fn().mockResolvedValue([]),
    fetchPluginVersions: vi.fn(),
  },
}))

import { pluginApi } from '@/services/pluginApi'

describe('PluginStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  const createMockPlugins = (): PluginInfo[] => [
    {
      id: 'plugin-1',
      name: '快捷链接',
      version: '1.0.0',
      author: '张三',
      description: '快速访问常用网站和链接',
      state: PluginState.Running,
      isEnabled: true,
      category: '效率',
      tags: [],
      installCount: 0,
      rating: 0,
    },
    {
      id: 'plugin-2',
      name: '文本工具',
      version: '2.1.0',
      author: '李四',
      description: '各种文本处理工具，包括格式化、编码转换等',
      state: PluginState.Stopped,
      isEnabled: false,
      category: '开发',
      tags: [],
      installCount: 0,
      rating: 0,
    },
    {
      id: 'plugin-3',
      name: '图片处理',
      version: '1.5.2',
      author: '王五',
      description: '图片压缩、格式转换等工具',
      state: PluginState.Running,
      isEnabled: true,
      category: '工具',
      tags: [],
      installCount: 0,
      rating: 0,
    },
    {
      id: 'plugin-4',
      name: '代码片段',
      version: '0.9.0',
      author: '张三',
      description: '管理和搜索常用代码片段',
      state: PluginState.Loaded,
      isEnabled: false,
      category: '开发',
      tags: [],
      installCount: 0,
      rating: 0,
    },
  ]

  describe('加载状态显示测试', () => {
    it('加载中显示加载状态', async () => {
      let resolveFn: (plugins: PluginInfo[]) => void
      const promise = new Promise<PluginInfo[]>((resolve) => {
        resolveFn = resolve
      })
      vi.mocked(pluginApi.fetchPlugins).mockReturnValue(promise)

      const wrapper = mount(PluginStore, {
        attachTo: document.body,
      })

      await wrapper.vm.$nextTick()

      expect(wrapper.find('.loading-state').exists()).toBe(true)
      expect(wrapper.find('.loading-spinner').exists()).toBe(true)

      resolveFn!([])
      await flushPromises()
      wrapper.unmount()
    })

    it('加载完成后隐藏加载状态', async () => {
      vi.mocked(pluginApi.fetchPlugins).mockResolvedValue(createMockPlugins())

      const wrapper = mount(PluginStore, {
        attachTo: document.body,
      })

      await flushPromises()

      expect(wrapper.find('.loading-state').exists()).toBe(false)
      wrapper.unmount()
    })

    it('页面加载时调用 loadPlugins', async () => {
      vi.mocked(pluginApi.fetchPlugins).mockResolvedValue([])

      mount(PluginStore)

      await flushPromises()

      expect(pluginApi.fetchPlugins).toHaveBeenCalledTimes(1)
    })
  })

  describe('空状态显示测试', () => {
    it('没有插件时显示空状态', async () => {
      vi.mocked(pluginApi.fetchPlugins).mockResolvedValue([])

      const wrapper = mount(PluginStore, {
        attachTo: document.body,
      })

      await flushPromises()

      expect(wrapper.find('.empty-state').exists()).toBe(true)
      expect(wrapper.find('.empty-state h3').text()).toBe('没有找到插件')
      expect(wrapper.find('.empty-state p').text()).toBe('暂无可用插件')
      wrapper.unmount()
    })

    it('有筛选条件时显示提示信息', async () => {
      vi.mocked(pluginApi.fetchPlugins).mockResolvedValue(createMockPlugins())

      const wrapper = mount(PluginStore, {
        attachTo: document.body,
      })

      await flushPromises()

      const searchInput = wrapper.find('.search-input')
      await searchInput.setValue('不存在的插件')
      vi.advanceTimersByTime(300)
      await flushPromises()

      const emptyState = wrapper.find('.empty-state')
      expect(emptyState.find('p').text()).toBe('试试调整搜索条件或筛选条件')
      wrapper.unmount()
    })
  })

  describe('插件卡片渲染数量测试', () => {
    it('渲染正确数量的插件卡片', async () => {
      vi.mocked(pluginApi.fetchPlugins).mockResolvedValue(createMockPlugins())

      const wrapper = mount(PluginStore, {
        attachTo: document.body,
      })

      await flushPromises()

      expect(wrapper.findAll('.plugin-card')).toHaveLength(4)
      wrapper.unmount()
    })

    it('显示正确的结果数量', async () => {
      vi.mocked(pluginApi.fetchPlugins).mockResolvedValue(createMockPlugins())

      const wrapper = mount(PluginStore, {
        attachTo: document.body,
      })

      await flushPromises()

      expect(wrapper.find('.results-info').text()).toContain('共 4 个插件')
      wrapper.unmount()
    })
  })

  describe('搜索功能测试', () => {
    it('按名称搜索过滤插件', async () => {
      vi.mocked(pluginApi.fetchPlugins).mockResolvedValue(createMockPlugins())

      const wrapper = mount(PluginStore, {
        attachTo: document.body,
      })

      await flushPromises()

      const searchInput = wrapper.find('.search-input')
      await searchInput.setValue('文本')
      vi.advanceTimersByTime(300)
      await flushPromises()

      const cards = wrapper.findAll('.plugin-card')
      expect(cards).toHaveLength(1)
      wrapper.unmount()
    })

    it('按描述搜索过滤插件', async () => {
      vi.mocked(pluginApi.fetchPlugins).mockResolvedValue(createMockPlugins())

      const wrapper = mount(PluginStore, {
        attachTo: document.body,
      })

      await flushPromises()

      const searchInput = wrapper.find('.search-input')
      await searchInput.setValue('压缩')
      vi.advanceTimersByTime(300)
      await flushPromises()

      const cards = wrapper.findAll('.plugin-card')
      expect(cards).toHaveLength(1)
      wrapper.unmount()
    })

    it('按作者搜索过滤插件', async () => {
      vi.mocked(pluginApi.fetchPlugins).mockResolvedValue(createMockPlugins())

      const wrapper = mount(PluginStore, {
        attachTo: document.body,
      })

      await flushPromises()

      const searchInput = wrapper.find('.search-input')
      await searchInput.setValue('张三')
      vi.advanceTimersByTime(300)
      await flushPromises()

      const cards = wrapper.findAll('.plugin-card')
      expect(cards).toHaveLength(2)
      wrapper.unmount()
    })

    it('搜索不区分大小写', async () => {
      vi.mocked(pluginApi.fetchPlugins).mockResolvedValue(createMockPlugins())

      const wrapper = mount(PluginStore, {
        attachTo: document.body,
      })

      await flushPromises()

      const searchInput = wrapper.find('.search-input')
      await searchInput.setValue('TEXT')
      vi.advanceTimersByTime(300)
      await flushPromises()

      const cards = wrapper.findAll('.plugin-card')
      expect(cards).toHaveLength(0)
      wrapper.unmount()
    })

    it('清除搜索后显示所有插件', async () => {
      vi.mocked(pluginApi.fetchPlugins).mockResolvedValue(createMockPlugins())

      const wrapper = mount(PluginStore, {
        attachTo: document.body,
      })

      await flushPromises()

      const searchInput = wrapper.find('.search-input')
      await searchInput.setValue('快捷')
      vi.advanceTimersByTime(300)
      await flushPromises()

      expect(wrapper.findAll('.plugin-card')).toHaveLength(1)

      const clearBtn = wrapper.find('.clear-btn')
      await clearBtn.trigger('click')
      vi.advanceTimersByTime(300)
      await flushPromises()

      expect(wrapper.findAll('.plugin-card')).toHaveLength(4)
      wrapper.unmount()
    })

    it('有搜索内容时显示清除按钮', async () => {
      vi.mocked(pluginApi.fetchPlugins).mockResolvedValue(createMockPlugins())

      const wrapper = mount(PluginStore, {
        attachTo: document.body,
      })

      await flushPromises()

      expect(wrapper.find('.clear-btn').exists()).toBe(false)

      const searchInput = wrapper.find('.search-input')
      await searchInput.setValue('test')
      await wrapper.vm.$nextTick()

      expect(wrapper.find('.clear-btn').exists()).toBe(true)
      wrapper.unmount()
    })
  })

  describe('版本历史弹窗测试', () => {
    it('点击版本按钮不刷新整个列表（弹窗加载期间列表保持显示）', async () => {
      vi.mocked(pluginApi.fetchPlugins).mockResolvedValue(createMockPlugins())

      // 版本历史请求挂起（模拟加载中），若 store 误用列表级 isLoading 就会整列表闪加载态
      let resolveVersions!: (v: PluginVersionInfo[]) => void
      const pendingVersions = new Promise<PluginVersionInfo[]>((resolve) => {
        resolveVersions = resolve
      })
      vi.mocked(pluginApi.fetchPluginVersions).mockReturnValue(pendingVersions)

      const wrapper = mount(PluginStore, {
        attachTo: document.body,
      })
      await flushPromises()

      expect(wrapper.find('.plugins-grid').exists()).toBe(true)

      await wrapper.find('.version-btn').trigger('click')
      await wrapper.vm.$nextTick()

      // 回归守卫：打开版本历史弹窗时，卡片网格不能被「加载插件中...」占位替换
      expect(wrapper.find('.loading-state').exists()).toBe(false)
      expect(wrapper.find('.plugins-grid').exists()).toBe(true)

      resolveVersions!([])
      await flushPromises()
      wrapper.unmount()
    })
  })

  describe('页面标题测试', () => {
    it('显示正确的页面标题', async () => {
      vi.mocked(pluginApi.fetchPlugins).mockResolvedValue([])

      const wrapper = mount(PluginStore, {
        attachTo: document.body,
      })

      await flushPromises()

      expect(wrapper.find('.page-title').text()).toBe('全部插件')
      wrapper.unmount()
    })
  })
})