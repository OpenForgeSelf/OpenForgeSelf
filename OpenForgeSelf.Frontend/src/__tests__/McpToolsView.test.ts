import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import McpToolsView from '@/views/McpToolsView.vue'
import type { McpServerDto, McpToolDto, McpTestResultDto } from '@/types/mcp'

vi.mock('@/services/mcpApi', () => ({
  mcpApi: {
    getServers: vi.fn(),
    getTools: vi.fn(),
    toggleTool: vi.fn(),
    testTool: vi.fn(),
  },
}))

import { mcpApi } from '@/services/mcpApi'

function createMockServers(): McpServerDto[] {
  return [
    {
      id: 'server_1',
      name: 'Filesystem',
      description: '文件系统服务器',
      status: 'connected',
      toolCount: 3,
    },
    {
      id: 'server_2',
      name: 'GitHub Tools',
      description: 'GitHub 操作服务器',
      status: 'connected',
      toolCount: 2,
    },
    {
      id: 'server_3',
      name: 'Database Server',
      description: '数据库服务器',
      status: 'disconnected',
      toolCount: 0,
    },
  ]
}

function createMockTools(serverId = 'server_1'): McpToolDto[] {
  return [
    {
      id: 'tool_1',
      name: 'read_file',
      description: '读取文件内容',
      serverId,
      serverName: 'Filesystem',
      category: '文件',
      isEnabled: true,
    },
    {
      id: 'tool_2',
      name: 'write_file',
      description: '写入文件内容',
      serverId,
      serverName: 'Filesystem',
      category: '文件',
      isEnabled: false,
    },
    {
      id: 'tool_3',
      name: 'search_web',
      description: '搜索网络内容',
      serverId,
      serverName: 'Filesystem',
      category: '网络',
      isEnabled: true,
    },
  ]
}

function mountView() {
  return mount(McpToolsView, {
    attachTo: document.body,
    global: {
      stubs: {
        Teleport: true,
        ForgeSwitch: {
          props: ['modelValue', 'disabled'],
          emits: ['update:modelValue'],
          template: '<div class="forge-switch-stub" :class="{ \'forge-switch-stub--on\': modelValue }" @click="$emit(\'update:modelValue\', !modelValue)">{{ modelValue ? \'ON\' : \'OFF\' }}</div>',
        },
      },
    },
  })
}

describe('McpToolsView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  afterEach(() => {
    document.body.innerHTML = ''
  })

  describe('加载与渲染', () => {
    it('页面加载时显示加载状态', async () => {
      let resolveServers: (servers: McpServerDto[]) => void
      const serverPromise = new Promise<McpServerDto[]>((resolve) => {
        resolveServers = resolve
      })
      vi.mocked(mcpApi.getServers).mockReturnValue(serverPromise)
      vi.mocked(mcpApi.getTools).mockResolvedValue([])

      const wrapper = mountView()
      await wrapper.vm.$nextTick()

      expect(wrapper.find('.sidebar-loading').exists()).toBe(true)

      resolveServers!(createMockServers())
      await flushPromises()
      wrapper.unmount()
    })

    it('加载完成后渲染服务器列表和工具列表', async () => {
      vi.mocked(mcpApi.getServers).mockResolvedValue(createMockServers())
      vi.mocked(mcpApi.getTools).mockResolvedValue(createMockTools())

      const wrapper = mountView()
      await flushPromises()

      expect(wrapper.findAll('.server-item')).toHaveLength(3)
      expect(wrapper.findAll('.table-row')).toHaveLength(3)
      expect(wrapper.find('.page-title').text()).toBe('MCP 工具管理')
      wrapper.unmount()
    })

    it('默认选中第一个服务器', async () => {
      vi.mocked(mcpApi.getServers).mockResolvedValue(createMockServers())
      vi.mocked(mcpApi.getTools).mockResolvedValue(createMockTools())

      const wrapper = mountView()
      await flushPromises()

      const firstServer = wrapper.findAll('.server-item')[0]
      expect(firstServer.classes()).toContain('active')
      expect(mcpApi.getTools).toHaveBeenCalledWith('server_1', undefined, undefined)
      wrapper.unmount()
    })

    it('没有服务器时显示空状态', async () => {
      vi.mocked(mcpApi.getServers).mockResolvedValue([])

      const wrapper = mountView()
      await flushPromises()

      expect(wrapper.find('.sidebar-empty').exists()).toBe(true)
      expect(wrapper.find('.sidebar-empty').text()).toBe('暂无服务器')
      wrapper.unmount()
    })

    it('加载失败时显示错误信息', async () => {
      vi.mocked(mcpApi.getServers).mockRejectedValue(new Error('连接失败'))

      const wrapper = mountView()
      await flushPromises()

      expect(wrapper.find('.table-error').exists()).toBe(true)
      expect(wrapper.find('.table-error').text()).toContain('连接失败')
      wrapper.unmount()
    })
  })

  describe('服务器选择', () => {
    it('点击服务器切换选中状态并加载工具', async () => {
      vi.mocked(mcpApi.getServers).mockResolvedValue(createMockServers())
      vi.mocked(mcpApi.getTools).mockResolvedValue(createMockTools())

      const wrapper = mountView()
      await flushPromises()

      const servers = wrapper.findAll('.server-item')
      await servers[1].trigger('click')
      await flushPromises()

      expect(servers[1].classes()).toContain('active')
      expect(mcpApi.getTools).toHaveBeenLastCalledWith('server_2', undefined, undefined)
      wrapper.unmount()
    })

    it('切换服务器时重置搜索和分类筛选', async () => {
      vi.mocked(mcpApi.getServers).mockResolvedValue(createMockServers())
      vi.mocked(mcpApi.getTools).mockResolvedValue(createMockTools())

      const wrapper = mountView()
      await flushPromises()

      const searchInput = wrapper.find('.search-input')
      await searchInput.setValue('file')

      const tabs = wrapper.findAll('.filter-tab')
      await tabs[2].trigger('click')
      await flushPromises()

      const servers = wrapper.findAll('.server-item')
      await servers[2].trigger('click')
      await flushPromises()

      expect(wrapper.find('.search-input').element.value).toBe('')
      expect(wrapper.findAll('.filter-tab')[0].classes()).toContain('active')
      wrapper.unmount()
    })

    it('切换服务器后工具列表为空时显示空状态', async () => {
      vi.mocked(mcpApi.getServers).mockResolvedValue(createMockServers())
      vi.mocked(mcpApi.getTools).mockResolvedValue([])

      const wrapper = mountView()
      await flushPromises()

      await wrapper.findAll('.server-item')[1].trigger('click')
      await flushPromises()

      expect(wrapper.findAll('.table-row')).toHaveLength(0)
      expect(wrapper.find('.table-empty').text()).toBe('暂无工具数据')
      wrapper.unmount()
    })
  })

  describe('搜索过滤', () => {
    it('按名称本地过滤工具', async () => {
      vi.mocked(mcpApi.getServers).mockResolvedValue(createMockServers())
      vi.mocked(mcpApi.getTools).mockResolvedValue(createMockTools())

      const wrapper = mountView()
      await flushPromises()

      const searchInput = wrapper.find('.search-input')
      await searchInput.setValue('read')
      await wrapper.vm.$nextTick()

      expect(wrapper.findAll('.table-row')).toHaveLength(1)
      expect(wrapper.find('.tool-name-text').text()).toBe('read_file')
      wrapper.unmount()
    })

    it('按描述本地过滤工具', async () => {
      vi.mocked(mcpApi.getServers).mockResolvedValue(createMockServers())
      vi.mocked(mcpApi.getTools).mockResolvedValue(createMockTools())

      const wrapper = mountView()
      await flushPromises()

      const searchInput = wrapper.find('.search-input')
      await searchInput.setValue('写入')
      await wrapper.vm.$nextTick()

      expect(wrapper.findAll('.table-row')).toHaveLength(1)
      expect(wrapper.find('.tool-name-text').text()).toBe('write_file')
      wrapper.unmount()
    })

    it('搜索无结果时显示空状态', async () => {
      vi.mocked(mcpApi.getServers).mockResolvedValue(createMockServers())
      vi.mocked(mcpApi.getTools).mockResolvedValue(createMockTools())

      const wrapper = mountView()
      await flushPromises()

      await wrapper.find('.search-input').setValue('不存在的工具')
      await wrapper.vm.$nextTick()

      expect(wrapper.findAll('.table-row')).toHaveLength(0)
      expect(wrapper.find('.table-empty').exists()).toBe(true)
      wrapper.unmount()
    })
  })

  describe('分类筛选', () => {
    it('点击分类标签本地筛选工具', async () => {
      vi.mocked(mcpApi.getServers).mockResolvedValue(createMockServers())
      vi.mocked(mcpApi.getTools).mockResolvedValue(createMockTools())

      const wrapper = mountView()
      await flushPromises()

      const tabs = wrapper.findAll('.filter-tab')
      await tabs[2].trigger('click')
      await wrapper.vm.$nextTick()

      expect(tabs[2].classes()).toContain('active')
      expect(wrapper.findAll('.table-row')).toHaveLength(2)
      wrapper.unmount()
    })

    it('分类和搜索条件同时本地生效', async () => {
      vi.mocked(mcpApi.getServers).mockResolvedValue(createMockServers())
      vi.mocked(mcpApi.getTools).mockResolvedValue(createMockTools())

      const wrapper = mountView()
      await flushPromises()

      await wrapper.find('.search-input').setValue('read')
      await wrapper.findAll('.filter-tab')[2].trigger('click')
      await wrapper.vm.$nextTick()

      expect(wrapper.findAll('.table-row')).toHaveLength(1)
      expect(wrapper.find('.tool-name-text').text()).toBe('read_file')
      wrapper.unmount()
    })
  })

  describe('工具状态切换', () => {
    it('点击开关调用 API 并更新状态', async () => {
      const tools = createMockTools()
      vi.mocked(mcpApi.getServers).mockResolvedValue(createMockServers())
      vi.mocked(mcpApi.getTools).mockResolvedValue(tools)
      vi.mocked(mcpApi.toggleTool).mockResolvedValue({ ...tools[1], isEnabled: true })

      const wrapper = mountView()
      await flushPromises()

      const switches = wrapper.findAll('.forge-switch-stub')
      expect(switches.length).toBe(3)
      expect(switches[1].classes()).not.toContain('forge-switch-stub--on')

      await switches[1].trigger('click')
      await flushPromises()

      expect(mcpApi.toggleTool).toHaveBeenCalledWith('tool_2')
      expect(wrapper.findAll('.forge-switch-stub')[1].classes()).toContain('forge-switch-stub--on')
      wrapper.unmount()
    })
  })

  describe('工具测试', () => {
    it('点击测试按钮显示成功结果', async () => {
      const tools = createMockTools()
      vi.mocked(mcpApi.getServers).mockResolvedValue(createMockServers())
      vi.mocked(mcpApi.getTools).mockResolvedValue(tools)
      vi.mocked(mcpApi.testTool).mockResolvedValue({
        success: true,
        message: '测试通过',
        durationMs: 120,
      } as McpTestResultDto)

      const wrapper = mountView()
      await flushPromises()

      const testButtons = wrapper.findAll('.btn-test')
      await testButtons[0].trigger('click')
      await flushPromises()

      expect(mcpApi.testTool).toHaveBeenCalledWith('tool_1')
      expect(wrapper.find('.test-toast').exists()).toBe(true)
      expect(wrapper.find('.toast-title').text()).toBe('测试通过')
      expect(wrapper.find('.toast-duration').text()).toContain('120ms')
      wrapper.unmount()
    })

    it('点击测试按钮显示失败结果', async () => {
      const tools = createMockTools()
      vi.mocked(mcpApi.getServers).mockResolvedValue(createMockServers())
      vi.mocked(mcpApi.getTools).mockResolvedValue(tools)
      vi.mocked(mcpApi.testTool).mockRejectedValue(new Error('连接超时'))

      const wrapper = mountView()
      await flushPromises()

      const testButtons = wrapper.findAll('.btn-test')
      await testButtons[0].trigger('click')
      await flushPromises()

      expect(wrapper.find('.test-toast').exists()).toBe(true)
      expect(wrapper.find('.toast-title').text()).toBe('测试失败')
      expect(wrapper.find('.toast-body').text()).toContain('连接超时')
      wrapper.unmount()
    })

    it('点击关闭按钮隐藏测试结果', async () => {
      const tools = createMockTools()
      vi.mocked(mcpApi.getServers).mockResolvedValue(createMockServers())
      vi.mocked(mcpApi.getTools).mockResolvedValue(tools)
      vi.mocked(mcpApi.testTool).mockResolvedValue({
        success: true,
        message: '测试通过',
        durationMs: 120,
      } as McpTestResultDto)

      const wrapper = mountView()
      await flushPromises()

      await wrapper.findAll('.btn-test')[0].trigger('click')
      await flushPromises()

      expect(wrapper.find('.test-toast').exists()).toBe(true)

      await wrapper.find('.toast-close').trigger('click')
      await wrapper.vm.$nextTick()

      expect(wrapper.find('.test-toast').exists()).toBe(false)
      wrapper.unmount()
    })
  })
})
