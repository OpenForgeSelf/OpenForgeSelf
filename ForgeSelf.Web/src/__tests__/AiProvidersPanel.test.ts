import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import AiProvidersPanel from '@/components/settings/AiProvidersPanel.vue'

/** 构造一条符合 AIProvider 类型的假数据 */
function makeProvider(overrides: Partial<Record<string, unknown>> = {}) {
  return {
    id: 1,
    name: 'P1',
    providerType: 'OpenAI',
    endpoint: 'https://x.com',
    supportedModels: [],
    isDefault: false,
    apiKeyMasked: '•••',
    timeoutSeconds: 120,
    visionModel: null,
    enableMultimodal: false,
    visionPromptTemplate: null,
    createTime: '',
    updateTime: '',
    ...overrides,
  }
}

function jsonResponse(data: unknown, ok = true, status = 200): Response {
  return { ok, status, json: async () => ({ data }) } as unknown as Response
}

describe('AiProvidersPanel - 多模态能力标注', () => {
  let fetchMock: ReturnType<typeof vi.fn>

  beforeEach(() => {
    setActivePinia(createPinia())
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
  })

  /** 挂载组件并注入提供方列表（GET /api/ai-providers 返回 providers） */
  async function mountWith(providers: unknown[]) {
    fetchMock.mockImplementation(async (url: string, opts?: RequestInit) => {
      const method = opts?.method ?? 'GET'
      if (url.includes('/api/ai-providers') && method === 'GET') {
        return jsonResponse(providers)
      }
      if (url.includes('/api/ai-models') && method === 'GET') {
        return jsonResponse({ groups: [] })
      }
      return jsonResponse({}, false, 404)
    })
    const wrapper = mount(AiProvidersPanel, { attachTo: document.body })
    await flushPromises()
    return wrapper
  }

  it('启用多模态且配置了视觉模型时，卡片显示「多模态」徽标', async () => {
    const wrapper = await mountWith([
      makeProvider({ id: 1, enableMultimodal: true, visionModel: 'default:qwen/qwen3-vl-4b' }),
    ])
    expect(wrapper.text()).toContain('多模态')
  })

  it('启用多模态但未配置视觉模型时，不显示「多模态」徽标', async () => {
    const wrapper = await mountWith([
      makeProvider({ id: 1, enableMultimodal: true, visionModel: null }),
    ])
    expect(wrapper.text()).not.toContain('多模态')
  })

  it('未启用多模态（即使填了视觉模型）也不显示「多模态」徽标', async () => {
    const wrapper = await mountWith([
      makeProvider({ id: 1, enableMultimodal: false, visionModel: 'default:qwen/qwen3-vl-4b' }),
    ])
    expect(wrapper.text()).not.toContain('多模态')
  })

  it('多个提供方中仅具备多模态能力者显示徽标', async () => {
    const wrapper = await mountWith([
      makeProvider({ id: 1, name: '视觉方', enableMultimodal: true, visionModel: 'default:qwen/qwen3-vl-4b' }),
      makeProvider({ id: 2, name: '纯文本方', enableMultimodal: true, visionModel: null }),
    ])
    // 整页含两卡片；只有视觉方应带「多模态」
    const tags = wrapper.findAll('.el-tag').map((t) => t.text())
    const multimodalCount = tags.filter((t) => t.includes('多模态')).length
    expect(multimodalCount).toBe(1)
  })
})

describe('AiProvidersPanel - 测试连接选定模型（输入3）', () => {
  let fetchMock: ReturnType<typeof vi.fn>
  let capturedTestBody: string | undefined

  const MODEL_A = {
    id: 101, providerId: 1, providerName: 'P1', upstreamModelId: 'gpt-a',
    chatModelId: 'P1:gpt-a', alias: null, capabilities: [], maxContext: null,
    publicAlias: null, enabled: true,
  }
  const MODEL_B = {
    id: 102, providerId: 1, providerName: 'P1', upstreamModelId: 'gpt-b',
    chatModelId: 'P1:gpt-b', alias: null, capabilities: [], maxContext: null,
    publicAlias: null, enabled: false,
  }

  const tick = () => new Promise((r) => setTimeout(r, 0))

  beforeEach(() => {
    setActivePinia(createPinia())
    fetchMock = vi.fn()
    capturedTestBody = undefined
    vi.stubGlobal('fetch', fetchMock)
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    vi.restoreAllMocks()
    document.body.innerHTML = ''
  })

  /** 挂载并展开 provider 1（展开触发 ai-models 懒加载），models 为该 provider 已登记模型 */
  async function mountExpanded(models: unknown[]) {
    fetchMock.mockImplementation(async (url: string, opts?: RequestInit) => {
      const method = opts?.method ?? 'GET'
      if (url.includes('/api/ai-providers') && method === 'GET') return jsonResponse([makeProvider({ id: 1 })])
      if (url.includes('/api/ai-models') && method === 'GET') {
        return jsonResponse({ groups: models.length ? [{ providerId: 1, models }] : [] })
      }
      if (url.includes('/test') && method === 'POST') {
        capturedTestBody = opts?.body as string | undefined
        return jsonResponse({ success: true, latencyMs: 5, message: '连接成功 · 模型回复“ok”', statusCode: 200 })
      }
      return jsonResponse({}, false, 404)
    })
    const wrapper = mount(AiProvidersPanel, { attachTo: document.body })
    await flushPromises()
    await wrapper.find('.el-card').trigger('click') // 展开 → watch 加载模型
    await flushPromises()
    return wrapper
  }

  it('决策①：无已登记模型时「测试连接」按钮禁用', async () => {
    const wrapper = await mountExpanded([])
    const btn = wrapper.find('[data-testid="provider-test-btn"]')
    expect(btn.exists()).toBe(true)
    expect(btn.attributes('disabled')).toBeDefined()
  })

  it('有已登记模型时按钮可用，点击弹出选模型对话框（列出全部已登记模型）', async () => {
    const wrapper = await mountExpanded([MODEL_A, MODEL_B])
    const btn = wrapper.find('[data-testid="provider-test-btn"]')
    expect(btn.attributes('disabled')).toBeUndefined()
    await btn.trigger('click')
    await flushPromises()
    await tick()
    expect(document.body.textContent).toContain('测试连接 · 选择模型')
    expect(document.body.textContent).toContain('gpt-a')
    expect(document.body.textContent).toContain('gpt-b')
  })

  it('取消不发任何测试请求', async () => {
    const wrapper = await mountExpanded([MODEL_A, MODEL_B])
    await wrapper.find('[data-testid="provider-test-btn"]').trigger('click')
    await flushPromises()
    await tick()
    const cancel = document.querySelector('[data-testid="provider-test-cancel"]') as HTMLElement | null
    expect(cancel).not.toBeNull()
    cancel!.click()
    await flushPromises()
    await tick()
    expect(capturedTestBody).toBeUndefined()
  })

  it('确定以选定模型 id 调 POST /test（body 带 modelId）', async () => {
    const wrapper = await mountExpanded([MODEL_A, MODEL_B])
    await wrapper.find('[data-testid="provider-test-btn"]').trigger('click')
    await flushPromises()
    await tick()
    // 默认预选第一个已启用模型 = MODEL_A(101)
    const confirm = document.querySelector('[data-testid="provider-test-confirm"]') as HTMLElement | null
    expect(confirm).not.toBeNull()
    confirm!.click()
    await flushPromises()
    await tick()
    expect(capturedTestBody).toBeDefined()
    expect(JSON.parse(capturedTestBody as string)).toEqual({ modelId: 101 })
  })
})

describe('AiProvidersPanel - 提供方编辑弹窗', () => {
  let fetchMock: ReturnType<typeof vi.fn>
  let updateBody: Record<string, unknown> | undefined
  let host: HTMLDivElement

  beforeEach(() => {
    setActivePinia(createPinia())
    fetchMock = vi.fn()
    updateBody = undefined
    host = document.createElement('div')
    host.className = 'app-layout has-bg-image'
    document.body.appendChild(host)
    fetchMock.mockImplementation(async (url: string, opts?: RequestInit) => {
      const method = opts?.method ?? 'GET'
      if (url.includes('/api/ai-providers') && method === 'GET') {
        return jsonResponse([makeProvider({ id: 1, name: '本地模型', endpoint: 'http://localhost:1234/v1', supportedModels: ['gpt-4o', 'gpt-4o-mini'] })])
      }
      if (url.endsWith('/api/ai-providers/1') && method === 'PUT') {
        updateBody = JSON.parse(String(opts?.body)) as Record<string, unknown>
        return jsonResponse(makeProvider({ id: 1, ...updateBody }))
      }
      if (url.includes('/api/ai-models') && method === 'GET') return jsonResponse({ groups: [] })
      return jsonResponse({}, false, 404)
    })
    vi.stubGlobal('fetch', fetchMock)
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    vi.restoreAllMocks()
    host.remove()
    document.body.innerHTML = ''
  })

  async function openEditDialog() {
    const wrapper = mount(AiProvidersPanel, { attachTo: host })
    await flushPromises()
    await wrapper.find('.el-card .el-button').trigger('click')
    await flushPromises()
    const title = [...document.querySelectorAll('.el-dialog__title')]
      .find((item) => item.textContent?.trim() === '编辑提供方')
    expect(title).toBeDefined()
    const dialog = title?.closest('.el-dialog')
    expect(dialog).not.toBeNull()
    return { wrapper, dialog: dialog as HTMLElement }
  }

  it('在设置容器之外打开弹窗并回填字段，API Key 留空提示保留原密钥', async () => {
    const { dialog } = await openEditDialog()
    expect(host.contains(dialog)).toBe(false)
    expect((dialog.querySelector('input') as HTMLInputElement | null)?.value).toBe('本地模型')
    expect(dialog.textContent).toContain('留空 = 保留原密钥')
    expect((dialog.querySelector('input[type="password"]') as HTMLInputElement | null)?.value).toBe('')
  })

  it('取消不更新；保存使用原有更新接口且 API Key 留空', async () => {
    const { dialog } = await openEditDialog()
    const cancel = [...dialog.querySelectorAll<HTMLButtonElement>('button')].find((button) => button.textContent?.includes('取消'))
    expect(cancel).toBeDefined()
    cancel?.click()
    await flushPromises()
    expect(updateBody).toBeUndefined()

    const editButton = document.querySelector('.el-card .el-button') as HTMLButtonElement | null
    expect(editButton).not.toBeNull()
    editButton?.click()
    await flushPromises()
    const save = [...document.querySelectorAll<HTMLButtonElement>('.el-dialog button')].find((button) => button.textContent?.includes('保存'))
    expect(save).toBeDefined()
    save?.click()
    await flushPromises()
    expect(updateBody).toMatchObject({ name: '本地模型', endpoint: 'http://localhost:1234/v1' })
    expect(updateBody?.apiKey).toBeUndefined()
  })
})
