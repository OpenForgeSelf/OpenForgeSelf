import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import HomeView from '@/views/HomeView.vue'

// ===== mock 首页聚合 store（可变引用，便于在不同用例中替换状态）=====
function createMockHomeStore(overrides: Record<string, any> = {}) {
  return {
    aiAgentStatus: { connected: true, todayConversations: 3, currentModel: 'Gemma 2B' },
    workflowStatus: { total: 0, running: 0 },
    scriptStatus: { total: 0, lastRunTime: null },
    systemMetrics: null,
    summaryText: '开始你的第一次锻造',
    todayToolCalls: 0,
    enabledSkillsCount: 0,
    forgeLevel: 1,
    forgeProgress: 0,
    recentActivities: [],
    // 待办相关：派生自 useTodoStore（Phase 5 T022 真实化）
    recentTodos: [],
    todoPendingTotal: 0,
    toggleTodo: vi.fn(),
    addTodo: vi.fn(),
    init: vi.fn(),
    ...overrides,
  }
}

const mockHomeStoreRef = { current: createMockHomeStore() }

vi.mock('@/stores/home', () => ({
  useHomeStore: vi.fn(() => mockHomeStoreRef.current),
}))

// ===== mock vue-router =====
const pushMock = vi.fn()
vi.mock('vue-router', () => ({
  useRouter: () => ({ push: pushMock }),
  useRoute: () => ({ query: {} }),
}))

function mountView() {
  return mount(HomeView, {
    attachTo: document.body,
  })
}

describe('HomeView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    pushMock.mockReset()
    localStorage.clear()
    // 重置为默认 mock store
    mockHomeStoreRef.current = createMockHomeStore()
  })

  afterEach(() => {
    document.body.innerHTML = ''
  })

  // ===== 1. 路由与渲染 =====
  describe('路由与渲染', () => {
    it('渲染 HomeView 主要区段', () => {
      const wrapper = mountView()
      expect(wrapper.find('.hero-section').exists()).toBe(true)
      expect(wrapper.find('.quick-grid').exists()).toBe(true)
      expect(wrapper.find('.dashboard').exists()).toBe(true)
      expect(wrapper.find('.dashboard-layout').exists()).toBe(true)
      expect(wrapper.find('.right-rail').exists()).toBe(true)
      expect(wrapper.find('.bottom-bar').exists()).toBe(true)
      wrapper.unmount()
    })
  })

  // ===== 2. Hero 区时段问候 =====
  describe('Hero 区时段问候', () => {
    beforeEach(() => {
      vi.useFakeTimers()
    })

    afterEach(() => {
      vi.useRealTimers()
    })

    it('上午显示早安问候', () => {
      // 上午 9 点
      vi.setSystemTime(new Date('2024-06-01T09:00:00'))
      const wrapper = mountView()
      expect(wrapper.find('.hero-title').text()).toContain('早安')
      wrapper.unmount()
    })

    it('下午显示下午好问候', () => {
      // 下午 14 点
      vi.setSystemTime(new Date('2024-06-01T14:00:00'))
      const wrapper = mountView()
      expect(wrapper.find('.hero-title').text()).toContain('下午好')
      wrapper.unmount()
    })

    it('晚上显示晚上好问候', () => {
      // 晚上 20 点
      vi.setSystemTime(new Date('2024-06-01T20:00:00'))
      const wrapper = mountView()
      expect(wrapper.find('.hero-title').text()).toContain('晚上好')
      wrapper.unmount()
    })
  })

  // ===== 3. 快速提问条 =====
  describe('快速提问条', () => {
    it('输入内容并提交后跳转到 /ai-agent 并携带 query', async () => {
      const wrapper = mountView()
      const input = wrapper.find('.quick-ask-input')
      await input.setValue('整理周报')
      // 回车提交
      await input.trigger('keydown.enter')
      expect(pushMock).toHaveBeenCalledWith({
        path: '/ai-agent',
        query: { q: '整理周报' },
      })
      wrapper.unmount()
    })

    it('点击发送按钮也触发跳转', async () => {
      const wrapper = mountView()
      const input = wrapper.find('.quick-ask-input')
      await input.setValue('运行健康检查')
      await wrapper.find('.quick-ask-button').trigger('click')
      expect(pushMock).toHaveBeenCalledWith({
        path: '/ai-agent',
        query: { q: '运行健康检查' },
      })
      wrapper.unmount()
    })

    it('空内容提交不跳转', async () => {
      const wrapper = mountView()
      // 不输入任何内容，直接点击发送
      await wrapper.find('.quick-ask-button').trigger('click')
      expect(pushMock).not.toHaveBeenCalled()
      // 输入纯空格也不跳转
      await wrapper.find('.quick-ask-input').setValue('   ')
      await wrapper.find('.quick-ask-button').trigger('click')
      expect(pushMock).not.toHaveBeenCalled()
      wrapper.unmount()
    })
  })

  // ===== 4. 功能快捷网格 =====
  describe('功能快捷网格', () => {
    it('渲染至少 8 个快捷入口', () => {
      const wrapper = mountView()
      const entries = wrapper.findAll('.quick-entry')
      expect(entries.length).toBeGreaterThanOrEqual(8)
      wrapper.unmount()
    })

    it('点击入口跳转到对应路由', async () => {
      const wrapper = mountView()
      // 点击 AI Agent 入口（title="AI Agent"）
      const aiAgentEntry = wrapper
        .findAll('.quick-entry')
        .find((b) => b.attributes('title') === 'AI Agent')
      expect(aiAgentEntry).toBeDefined()
      await aiAgentEntry!.trigger('click')
      expect(pushMock).toHaveBeenCalledWith('/ai-agent')
      wrapper.unmount()
    })

    it('使用频率统计基于 localStorage', async () => {
      // 预设 skills 为高频，ai-agent 为低频
      localStorage.setItem(
        'forge-quick-usage',
        JSON.stringify({ skills: 5, 'ai-agent': 1 }),
      )
      const wrapper = mountView()
      // onMounted 中读取 localStorage 并更新 usageCounts，触发的重渲染需等待 nextTick
      await wrapper.vm.$nextTick()
      const entries = wrapper.findAll('.quick-entry')
      // skills（技能管理）应排在最前
      expect(entries[0].attributes('title')).toBe('技能管理')
      // 高频入口应带 hot class
      expect(entries[0].classes()).toContain('quick-entry--hot')
      wrapper.unmount()
    })

    it('点击入口会记录使用频率到 localStorage', async () => {
      const wrapper = mountView()
      const aiAgentEntry = wrapper
        .findAll('.quick-entry')
        .find((b) => b.attributes('title') === 'AI Agent')
      await aiAgentEntry!.trigger('click')
      // localStorage 应被更新，ai-agent 计数 > 0
      const stored = JSON.parse(localStorage.getItem('forge-quick-usage') || '{}')
      expect(stored['ai-agent']).toBeGreaterThan(0)
      wrapper.unmount()
    })
  })

  // ===== 5. Widget 数据（mock store）=====
  describe('Widget 数据', () => {
    it('AI Agent Widget 显示真实连接状态（就绪）', () => {
      mockHomeStoreRef.current = createMockHomeStore({
        aiAgentStatus: { connected: true, todayConversations: 3, currentModel: 'Gemma 2B' },
      })
      const wrapper = mountView()
      const aiAgentCard = wrapper
        .findAll('.widget-card')
        .find((c) => c.find('.widget-card-title').text() === 'AI Agent')
      expect(aiAgentCard).toBeDefined()
      expect(aiAgentCard!.text()).toContain('就绪')
      wrapper.unmount()
    })

    it('AI Agent Widget 显示真实连接状态（离线）', () => {
      mockHomeStoreRef.current = createMockHomeStore({
        aiAgentStatus: { connected: false, todayConversations: 0, currentModel: 'Gemma 2B' },
      })
      const wrapper = mountView()
      const aiAgentCard = wrapper
        .findAll('.widget-card')
        .find((c) => c.find('.widget-card-title').text() === 'AI Agent')
      expect(aiAgentCard).toBeDefined()
      expect(aiAgentCard!.text()).toContain('离线')
      wrapper.unmount()
    })

    it('工作流 Widget 无数据时显示空状态', () => {
      mockHomeStoreRef.current = createMockHomeStore({
        workflowStatus: { total: 0, running: 0 },
      })
      const wrapper = mountView()
      const workflowCard = wrapper
        .findAll('.widget-card')
        .find((c) => c.find('.widget-card-title').text() === '工作流')
      expect(workflowCard).toBeDefined()
      expect(workflowCard!.text()).toContain('暂无工作流')
      wrapper.unmount()
    })

    it('系统监控 Widget 未加载时显示骨架', () => {
      mockHomeStoreRef.current = createMockHomeStore({
        systemMetrics: null,
      })
      const wrapper = mountView()
      // 系统监控 widget 内应有 .skeleton-bar
      const sysCard = wrapper
        .findAll('.widget-card')
        .find((c) => c.find('.widget-card-title').text() === '系统监控')
      expect(sysCard).toBeDefined()
      expect(sysCard!.findAll('.skeleton-bar').length).toBeGreaterThan(0)
      // 全局也存在 skeleton-bar
      expect(wrapper.find('.skeleton-bar').exists()).toBe(true)
      wrapper.unmount()
    })

    it('待办无数据时显示空状态', () => {
      mockHomeStoreRef.current = createMockHomeStore({
        recentTodos: [],
      })
      const wrapper = mountView()
      const todoPanel = wrapper.find('.todo-panel')
      expect(todoPanel.exists()).toBe(true)
      expect(todoPanel.text()).toContain('暂无待处理待办')
      wrapper.unmount()
    })

    it('最近活动无数据时显示空状态', () => {
      mockHomeStoreRef.current = createMockHomeStore({
        recentActivities: [],
      })
      const wrapper = mountView()
      const activityPanel = wrapper.find('.activity-panel')
      expect(activityPanel.exists()).toBe(true)
      expect(activityPanel.text()).toContain('暂无最近活动')
      wrapper.unmount()
    })
  })

  // ===== 6. 底部状态栏 =====
  describe('底部状态栏', () => {
    it('底部状态栏包含锻层和时钟', () => {
      const wrapper = mountView()
      const bottomBar = wrapper.find('.bottom-bar')
      expect(bottomBar.exists()).toBe(true)
      // 包含"锻层"文本
      expect(bottomBar.text()).toContain('锻层')
      // 包含时钟元素
      expect(bottomBar.find('.bottom-bar-clock').exists()).toBe(true)
      wrapper.unmount()
    })

    it('底部状态栏不包含堆砌统计', () => {
      const wrapper = mountView()
      const bottomBar = wrapper.find('.bottom-bar')
      expect(bottomBar.exists()).toBe(true)
      // 不包含"今日工具调用"
      expect(bottomBar.text()).not.toContain('今日工具调用')
      // 不包含"技能已注册"
      expect(bottomBar.text()).not.toContain('技能已注册')
      wrapper.unmount()
    })
  })
})
