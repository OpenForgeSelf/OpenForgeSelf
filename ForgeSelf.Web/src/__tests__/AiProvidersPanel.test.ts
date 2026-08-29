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
