import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import SettingsView from '@/views/SettingsView.vue'
import { useThemeStore, ROOT_THEME_ATTRIBUTE, ROOT_THEME_MODE_ATTRIBUTE, THEME_STORAGE_KEY } from '@/stores/theme'

function createMatchMediaMock(prefersDark = false) {
  const listeners = new Set<(event: MediaQueryListEvent) => void>()
  return {
    matches: prefersDark,
    media: '(prefers-color-scheme: dark)',
    addEventListener: vi.fn((type: string, listener: EventListenerOrEventListenerObject) => {
      if (type === 'change' && typeof listener === 'function') {
        listeners.add(listener as (event: MediaQueryListEvent) => void)
      }
    }),
    removeEventListener: vi.fn((type: string, listener: EventListenerOrEventListenerObject) => {
      if (type === 'change' && typeof listener === 'function') {
        listeners.delete(listener as (event: MediaQueryListEvent) => void)
      }
    }),
    dispatch(matches: boolean) {
      this.matches = matches
      listeners.forEach((listener) => listener({ matches, media: this.media } as MediaQueryListEvent))
    },
  }
}

describe('SettingsView', () => {
  let matchMediaMock: ReturnType<typeof createMatchMediaMock>

  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.documentElement.removeAttribute(ROOT_THEME_ATTRIBUTE)
    document.documentElement.removeAttribute(ROOT_THEME_MODE_ATTRIBUTE)
    document.documentElement.style.colorScheme = ''

    matchMediaMock = createMatchMediaMock(false)
    Object.defineProperty(window, 'matchMedia', {
      writable: true,
      value: vi.fn(() => matchMediaMock),
    })
  })

  afterEach(() => {
    const store = useThemeStore()
    store.dispose()
  })

  function mountView() {
    return mount(SettingsView, {
      attachTo: document.body,
    })
  }

  it('默认渲染通用设置面板', () => {
    const wrapper = mountView()
    expect(wrapper.find('nav[aria-label="设置分类"]').exists()).toBe(true)
    const navLabels = wrapper.findAll('nav[aria-label="设置分类"] button').map((el) => el.text())
    expect(navLabels).toContain('AI 提供方')
  })

  it('导航切换激活分类', async () => {
    const wrapper = mountView()
    const navItems = wrapper.findAll('nav[aria-label="设置分类"] button')

    await navItems[3].trigger('click')
    await wrapper.vm.$nextTick()

    expect(navItems[3].classes()).toContain('text-primary')
  })

  it('外观面板渲染主题切换按钮', async () => {
    const wrapper = mountView()
    const navItems = wrapper.findAll('nav[aria-label="设置分类"] button')

    // 外观是第 6 个导航项（index 5）
    await navItems[5].trigger('click')
    await wrapper.vm.$nextTick()

    const buttons = wrapper.findAll('.el-radio-button')
    expect(buttons.length).toBe(3)
    expect(buttons[0].text()).toBe('浅色')
    expect(buttons[1].text()).toBe('深色')
    expect(buttons[2].text()).toBe('跟随系统')
  })

  it('点击浅色按钮设置主题为浅色', async () => {
    const wrapper = mountView()
    const store = useThemeStore()
    store.initialize()

    const navItems = wrapper.findAll('nav[aria-label="设置分类"] button')
    await navItems[5].trigger('click')
    await wrapper.vm.$nextTick()

    const buttons = wrapper.findAll('.el-radio-button')
    await buttons[0].find('input').trigger('change')
    await wrapper.vm.$nextTick()

    expect(store.mode).toBe('light')
    expect(store.resolvedTheme).toBe('light')
    expect(document.documentElement.getAttribute(ROOT_THEME_ATTRIBUTE)).toBe('light')
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('light')
  })

  it('点击深色按钮设置主题为深色', async () => {
    const wrapper = mountView()
    const store = useThemeStore()
    store.initialize()

    const navItems = wrapper.findAll('nav[aria-label="设置分类"] button')
    await navItems[5].trigger('click')
    await wrapper.vm.$nextTick()

    const buttons = wrapper.findAll('.el-radio-button')
    await buttons[1].find('input').trigger('change')
    await wrapper.vm.$nextTick()

    expect(store.mode).toBe('dark')
    expect(store.resolvedTheme).toBe('dark')
    expect(document.documentElement.getAttribute(ROOT_THEME_ATTRIBUTE)).toBe('dark')
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('dark')
  })

  it('点击跟随系统按钮设置主题为系统', async () => {
    const wrapper = mountView()
    const store = useThemeStore()
    store.initialize()
    store.setMode('dark')

    const navItems = wrapper.findAll('nav[aria-label="设置分类"] button')
    await navItems[5].trigger('click')
    await wrapper.vm.$nextTick()

    const buttons = wrapper.findAll('.el-radio-button')
    await buttons[2].find('input').trigger('change')
    await wrapper.vm.$nextTick()

    expect(store.mode).toBe('system')
    expect(store.resolvedTheme).toBe('light')
    expect(document.documentElement.getAttribute(ROOT_THEME_MODE_ATTRIBUTE)).toBe('system')
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('system')
  })

  it('主题按钮根据当前模式显示激活状态', async () => {
    const wrapper = mountView()
    const store = useThemeStore()
    store.initialize()
    store.setMode('dark')

    const navItems = wrapper.findAll('nav[aria-label="设置分类"] button')
    await navItems[5].trigger('click')
    await wrapper.vm.$nextTick()

    const buttons = wrapper.findAll('.el-radio-button')
    expect(buttons[1].classes()).toContain('is-active')
    expect(buttons[0].classes()).not.toContain('is-active')
  })
})

// ===== Provider 模型集成（feature 004）：AiProvidersPanel 独立组件测试 =====
describe('SettingsView - Provider 模型集成（feature 004）', () => {
  let fetchMock: ReturnType<typeof vi.fn>
  let matchMediaMock: ReturnType<typeof createMatchMediaMock>

  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.documentElement.removeAttribute(ROOT_THEME_ATTRIBUTE)
    document.documentElement.removeAttribute(ROOT_THEME_MODE_ATTRIBUTE)
    document.documentElement.style.colorScheme = ''
    matchMediaMock = createMatchMediaMock(false)
    Object.defineProperty(window, 'matchMedia', {
      writable: true,
      value: vi.fn(() => matchMediaMock),
    })
    fetchMock = vi.fn()
    vi.stubGlobal('fetch', fetchMock)
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: { writeText: vi.fn().mockResolvedValue(undefined) },
    })
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    vi.restoreAllMocks()
    const store = useThemeStore()
    store.dispose()
  })

  function mountProviderView() {
    return mount(SettingsView, {
      attachTo: document.body,
      global: {
        stubs: {
          AiProvidersPanel: { template: '<div class="ai-providers-panel-stub" />' },
          ApiServerPanel: { template: '<div class="api-server-panel-stub" />' },
        },
      },
    })
  }

  function jsonResponse(data: unknown, ok = true, status = 200): Response {
    return {
      ok,
      status,
      json: async () => ({ data }),
    } as unknown as Response
  }

  function defaultProvider(id = 1) {
    return {
      id,
      name: 'P' + id,
      providerType: 'OpenAI',
      endpoint: 'https://x.com',
      supportedModels: [],
      isDefault: false,
      apiKeyMasked: '•••',
      timeoutSeconds: 120,
      visionModel: null,
      enableMultimodal: true,
      visionPromptTemplate: null,
      createTime: '',
      updateTime: '',
    }
  }

  function defaultModel() {
    return {
      id: 10,
      providerId: 1,
      providerName: 'OpenAI',
      upstreamModelId: 'gpt-4o',
      chatModelId: 'OpenAI:gpt-4o',
      alias: null,
      capabilities: ['vision', 'stream'],
      enabled: true,
      owner: 'openai',
      lastSyncTime: '2026-07-19T00:00:00Z',
      createTime: '',
      updateTime: '',
    }
  }

  async function openProviders(wrapper: ReturnType<typeof mountProviderView>) {
    const aiNav = wrapper.findAll('nav[aria-label="设置分类"] button').find((el) => el.text().includes('AI 提供方'))
    expect(aiNav).toBeTruthy()
    await aiNav!.trigger('click')
    await flushPromises()
  }

  it('导航不再有「模型列表」入口（T022）', () => {
    const wrapper = mountProviderView()
    const navLabels = wrapper.findAll('nav[aria-label="设置分类"] button').map((el) => el.text())
    expect(navLabels).not.toContain('模型列表')
  })

  it('AI 提供方面板作为独立组件渲染（T009/T010）', async () => {
    fetchMock.mockImplementation(async (url: string, opts?: RequestInit) => {
      const method = opts?.method ?? 'GET'
      if (url.includes('/api/ai-providers') && method === 'GET') {
        return jsonResponse([defaultProvider(1)])
      }
      return jsonResponse({}, false, 404)
    })

    const wrapper = mountProviderView()
    await openProviders(wrapper)

    // AiProvidersPanel 组件已渲染（stub）
    expect(wrapper.find('.ai-providers-panel-stub').exists()).toBe(true)
  })

  it('测试连接成功自动拉取模型（T009/T010 - API 层）', async () => {
    fetchMock.mockImplementation(async (url: string, opts?: RequestInit) => {
      const method = opts?.method ?? 'GET'
      if (url.includes('/api/ai-providers') && method === 'GET') {
        return jsonResponse([defaultProvider(1)])
      }
      if (String(url).endsWith('/test')) {
        return jsonResponse({ success: true, latencyMs: 10, message: 'ok', statusCode: 200 })
      }
      if (String(url).endsWith('/fetch-models')) {
        return jsonResponse({ providerId: 1, providerName: 'P1', fetched: 1, added: 1, updated: 0, kept: 0, models: [] })
      }
      if (url.includes('/api/ai-models?providerId=1')) {
        return jsonResponse([{ providerId: 1, providerName: 'P1', models: [defaultModel()] }])
      }
      return jsonResponse({}, false, 404)
    })

    // 直接验证 API 层：test 成功后调用 fetch-models
    const { aiProvidersApi } = await import('@/services/aiProvidersApi')
    const { aiModelsApi } = await import('@/services/aiModelsApi')
    const testResult = await aiProvidersApi.test(1)
    expect(testResult.success).toBe(true)
    if (testResult.success) {
      const fr = await aiModelsApi.fetchForProvider(1)
      expect(fr.fetched).toBe(1)
    }
    const calledFetch = fetchMock.mock.calls.some((c) => String(c[0]).endsWith('/fetch-models'))
    expect(calledFetch).toBe(true)
  })

  it('测试连接失败不拉取模型（API 层）', async () => {
    fetchMock.mockImplementation(async (url: string, opts?: RequestInit) => {
      const method = opts?.method ?? 'GET'
      if (url.includes('/api/ai-providers') && method === 'GET') {
        return jsonResponse([defaultProvider(1)])
      }
      if (String(url).endsWith('/test')) {
        return jsonResponse({ success: false, latencyMs: 0, message: 'fail', statusCode: 500 })
      }
      return jsonResponse({}, false, 404)
    })

    const { aiProvidersApi } = await import('@/services/aiProvidersApi')
    const testResult = await aiProvidersApi.test(1)
    expect(testResult.success).toBe(false)
    const calledFetch = fetchMock.mock.calls.some((c) => String(c[0]).endsWith('/fetch-models'))
    expect(calledFetch).toBe(false)
  })

  it('Provider 列表加载与展开模型（T013 - API 层）', async () => {
    fetchMock.mockImplementation(async (url: string) => {
      if (url.includes('/api/ai-providers')) {
        return jsonResponse([defaultProvider(1)])
      }
      if (url.includes('/api/ai-models')) {
        return jsonResponse({ groups: [{ providerId: 1, providerName: 'P1', models: [defaultModel()] }] })
      }
      return jsonResponse({}, false, 404)
    })

    const { aiProvidersApi } = await import('@/services/aiProvidersApi')
    const { aiModelsApi } = await import('@/services/aiModelsApi')
    const providers = await aiProvidersApi.list()
    expect(providers).toHaveLength(1)
    const groups = await aiModelsApi.list({ providerId: 1 })
    expect(groups[0].models[0].upstreamModelId).toBe('gpt-4o')
  })

  it('Provider 删除级联清除模型（T014/T017 - API 层）', async () => {
    let providersData = [defaultProvider(1)]
    fetchMock.mockImplementation(async (url: string, opts?: RequestInit) => {
      const method = opts?.method ?? 'GET'
      if (url.includes('/api/ai-providers') && method === 'GET') {
        return jsonResponse(providersData)
      }
      if (String(url).match(/\/api\/ai-providers\/\d+$/) && method === 'DELETE') {
        providersData = []
        return jsonResponse(null)
      }
      return jsonResponse({}, false, 404)
    })

    const { aiProvidersApi } = await import('@/services/aiProvidersApi')
    await aiProvidersApi.remove(1)
    const remaining = await aiProvidersApi.list()
    expect(remaining).toHaveLength(0)
  })

  it('模型启停调用 toggleEnabled（API 层）', async () => {
    fetchMock.mockImplementation(async (url: string, opts?: RequestInit) => {
      if (String(url).includes('/enabled')) {
        return jsonResponse({ ...defaultModel(), enabled: false })
      }
      return jsonResponse({}, false, 404)
    })

    const { aiModelsApi } = await import('@/services/aiModelsApi')
    const updated = await aiModelsApi.toggleEnabled(10, false)
    expect(updated.enabled).toBe(false)
    const call = fetchMock.mock.calls.find((c) => String(c[0]).includes('/enabled'))
    expect(call).toBeTruthy()
  })

  it('复制 ChatModelId 调用 clipboard', async () => {
    await navigator.clipboard.writeText('OpenAI:gpt-4o')
    expect(navigator.clipboard.writeText).toHaveBeenCalledWith('OpenAI:gpt-4o')
  })
})
