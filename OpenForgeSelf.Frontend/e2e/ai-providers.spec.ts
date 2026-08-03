import { test, expect, type Page, type Route } from '@playwright/test'

/**
 * AI 提供方管理 E2E 测试 —— Playwright 官方最佳实践。
 *
 * 设计原则：
 * - **完全 mock 后端 API**：用 page.route() 拦截所有 /api/ai-providers* 和 /api/ai-models* 请求，
 *   不依赖真实后端、不依赖真实 LM Studio、无网络副作用。
 * - **无 waitForTimeout**：全部用 Playwright auto-waiting（expect(locator).toBeVisible() 等）
 *   或 waitForResponse 等待 API 完成。
 * - **数据隔离**：每个用例独立 mock 数据，无状态依赖，允许并行（去掉 serial 模式）。
 * - **语义化 selector**：优先 getByRole/getByText/getByLabel，避免依赖 CSS class。
 * - **无硬编码真实凭证**：mock 数据用占位值。
 *
 * 覆盖原 T0xx 编号语义 + 新增表单校验和测试失败用例。
 */

// ============================================================
// Mock 数据
// ============================================================

interface MockProvider {
  id: number
  name: string
  providerType: 'OpenAI' | 'Anthropic' | 'Custom'
  endpoint: string
  apiKeyMasked: string
  isDefault: boolean
  timeoutSeconds: number
  visionModel: string | null
  enableMultimodal: boolean
  supportedModels: string[]
  createTime: string
  updateTime: string
}

/** 后端 AIProviderRequest DTO（含 apiKey 明文，与 MockProvider 的 apiKeyMasked 区分） */
interface ProviderRequest {
  name?: string
  providerType?: MockProvider['providerType']
  endpoint?: string
  apiKey?: string
  supportedModels?: string[]
  isDefault?: boolean
  timeoutSeconds?: number
  visionModel?: string | null
  enableMultimodal?: boolean
}

const MOCK_PROVIDER_1: MockProvider = {
  id: 1,
  name: 'Ollama-Local',
  providerType: 'OpenAI',
  endpoint: 'http://localhost:11434/v1',
  apiKeyMasked: 'sk-****1234',
  isDefault: true,
  timeoutSeconds: 120,
  visionModel: null,
  enableMultimodal: false,
  supportedModels: ['llama3'],
  createTime: '2026-07-01T00:00:00Z',
  updateTime: '2026-07-01T00:00:00Z',
}

const MOCK_PROVIDER_2: MockProvider = {
  id: 2,
  name: 'OpenAI-Cloud',
  providerType: 'OpenAI',
  endpoint: 'https://api.openai.com/v1',
  apiKeyMasked: 'sk-****5678',
  isDefault: false,
  timeoutSeconds: 60,
  visionModel: 'gpt-4o',
  enableMultimodal: true,
  supportedModels: ['gpt-4o', 'gpt-4o-mini'],
  createTime: '2026-07-02T00:00:00Z',
  updateTime: '2026-07-02T00:00:00Z',
}

/** 默认列表响应（两个 provider） */
function defaultListResponse(): MockProvider[] {
  return [structuredClone(MOCK_PROVIDER_1), structuredClone(MOCK_PROVIDER_2)]
}

/** 包装为后端成功响应体 */
function ok<T>(data: T) {
  return { success: true, data }
}

// ============================================================
// Mock 路由 helper
// ============================================================

interface MockState {
  providers: MockProvider[]
  /** 下一次 create 返回的 id */
  nextId: number
  /** test 接口返回的成功/失败 */
  testResult: { success: boolean; message: string; latencyMs: number }
  /** fetch-models 返回的统计 */
  fetchResult: { fetched: number; added: number; updated: number; kept: number }
  /** 模型列表（按 providerId） */
  modelsByProvider: Record<number, Array<{ id: number; upstreamModelId: string; chatModelId: string; alias: string | null; enabled: boolean; capabilities: string[]; maxContext: number; providerId: number }>>
  /** 记录所有收到的请求（用于断言） */
  requests: Array<{ method: string; url: string; body: unknown }>
}

function createMockState(): MockState {
  return {
    providers: defaultListResponse(),
    nextId: 100,
    testResult: { success: true, message: '连接成功', latencyMs: 42 },
    fetchResult: { fetched: 2, added: 2, updated: 0, kept: 0 },
    modelsByProvider: {
      1: [
        { id: 11, upstreamModelId: 'llama3', chatModelId: 'ollama:llama3', alias: null, enabled: true, capabilities: ['stream'], maxContext: 8192, providerId: 1 },
      ],
      2: [
        { id: 21, upstreamModelId: 'gpt-4o', chatModelId: 'openai:gpt-4o', alias: 'GPT 4o', enabled: true, capabilities: ['vision', 'stream'], maxContext: 128000, providerId: 2 },
        { id: 22, upstreamModelId: 'gpt-4o-mini', chatModelId: 'openai:gpt-4o-mini', alias: null, enabled: false, capabilities: ['vision'], maxContext: 128000, providerId: 2 },
      ],
    },
    requests: [],
  }
}

/** 记录请求并返回 [status, payload] 元组 */
function recordAndJson(state: MockState, method: string, url: string, body: unknown, payload: unknown, status = 200): [number, unknown] {
  state.requests.push({ method, url, body })
  return [status, payload]
}

/**
 * 安装 mock 路由。返回 MockState，测试中可修改 state 字段以定制行为。
 */
async function installMocks(page: Page): Promise<MockState> {
  const state = createMockState()

  await page.route('**/api/ai-providers**', (route: Route) => {
    const request = route.request()
    const method = request.method()
    const url = request.url()
    const bodyText = request.postData() ?? ''

    let body: unknown = null
    if (bodyText) {
      try {
        body = JSON.parse(bodyText)
      } catch {
        body = bodyText
      }
    }

    // GET /api/ai-providers  → 列表
    if (method === 'GET' && /\/api\/ai-providers\/?$/.test(url)) {
      const [, payload] = recordAndJson(state, method, url, body, ok(state.providers.map(p => structuredClone(p))))
      return route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(payload) })
    }

    // GET /api/ai-providers/{id}  → 详情
    const getMatch = url.match(/\/api\/ai-providers\/(\d+)\/?$/)
    if (method === 'GET' && getMatch) {
      const id = Number(getMatch[1])
      const found = state.providers.find(p => p.id === id)
      if (!found) return route.fulfill({ status: 404, contentType: 'application/json', body: JSON.stringify({ success: false, message: 'Provider not found' }) })
      const [, payload] = recordAndJson(state, method, url, body, ok(structuredClone(found)))
      return route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(payload) })
    }

    // POST /api/ai-providers  → 新增
    if (method === 'POST' && /\/api\/ai-providers\/?$/.test(url)) {
      const req = body as ProviderRequest
      if (!req.apiKey) {
        return route.fulfill({ status: 400, contentType: 'application/json', body: JSON.stringify({ success: false, message: 'ApiKey 不能为空' }) })
      }
      const newProvider: MockProvider = {
        id: state.nextId++,
        name: req.name ?? '',
        providerType: (req.providerType as MockProvider['providerType']) ?? 'OpenAI',
        endpoint: req.endpoint ?? '',
        apiKeyMasked: 'sk-****mock',
        isDefault: req.isDefault ?? false,
        timeoutSeconds: req.timeoutSeconds ?? 120,
        visionModel: req.visionModel ?? null,
        enableMultimodal: req.enableMultimodal ?? true,
        supportedModels: req.supportedModels ?? [],
        createTime: new Date().toISOString(),
        updateTime: new Date().toISOString(),
      }
      state.providers.push(newProvider)
      const [, payload] = recordAndJson(state, method, url, body, ok(structuredClone(newProvider)))
      return route.fulfill({ status: 201, contentType: 'application/json', body: JSON.stringify(payload) })
    }

    // PUT /api/ai-providers/{id}  → 更新
    const putMatch = url.match(/\/api\/ai-providers\/(\d+)\/?$/)
    if (method === 'PUT' && putMatch) {
      const id = Number(putMatch[1])
      const idx = state.providers.findIndex(p => p.id === id)
      if (idx < 0) return route.fulfill({ status: 404, contentType: 'application/json', body: JSON.stringify({ success: false, message: 'Provider not found' }) })
      const req = body as ProviderRequest
      const updated: MockProvider = {
        ...state.providers[idx],
        name: req.name ?? state.providers[idx].name,
        providerType: (req.providerType as MockProvider['providerType']) ?? state.providers[idx].providerType,
        endpoint: req.endpoint ?? state.providers[idx].endpoint,
        isDefault: req.isDefault ?? state.providers[idx].isDefault,
        timeoutSeconds: req.timeoutSeconds ?? state.providers[idx].timeoutSeconds,
        visionModel: req.visionModel ?? state.providers[idx].visionModel,
        enableMultimodal: req.enableMultimodal ?? state.providers[idx].enableMultimodal,
        supportedModels: req.supportedModels ?? state.providers[idx].supportedModels,
        updateTime: new Date().toISOString(),
      }
      state.providers[idx] = updated
      const [, payload] = recordAndJson(state, method, url, body, ok(structuredClone(updated)))
      return route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(payload) })
    }

    // DELETE /api/ai-providers/{id}  → 删除（204 NoContent）
    const deleteMatch = url.match(/\/api\/ai-providers\/(\d+)\/?$/)
    if (method === 'DELETE' && deleteMatch) {
      const id = Number(deleteMatch[1])
      const idx = state.providers.findIndex(p => p.id === id)
      if (idx < 0) return route.fulfill({ status: 404, contentType: 'application/json', body: JSON.stringify({ success: false, message: 'Provider not found' }) })
      state.providers.splice(idx, 1)
      state.requests.push({ method, url, body })
      return route.fulfill({ status: 204 })
    }

    // POST /api/ai-providers/{id}/test  → 测试连接
    if (method === 'POST' && /\/api\/ai-providers\/(\d+)\/test\/?$/.test(url)) {
      const [, payload] = recordAndJson(state, method, url, body, ok(state.testResult))
      return route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(payload) })
    }

    // POST /api/ai-providers/{id}/fetch-models  → 拉取模型
    if (method === 'POST' && /\/api\/ai-providers\/(\d+)\/fetch-models\/?$/.test(url)) {
      const [, payload] = recordAndJson(state, method, url, body, ok(state.fetchResult))
      return route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(payload) })
    }

    return route.fulfill({ status: 404, contentType: 'application/json', body: JSON.stringify({ success: false, message: `Mock 未覆盖: ${method} ${url}` }) })
  })

  await page.route('**/api/ai-models**', (route: Route) => {
    const request = route.request()
    const method = request.method()
    const url = request.url()
    const bodyText = request.postData() ?? ''
    let body: unknown = null
    if (bodyText) {
      try { body = JSON.parse(bodyText) } catch { body = bodyText }
    }

    // GET /api/ai-models?providerId=...  → 按 provider 分组返回模型
    if (method === 'GET' && /\/api\/ai-models\/?(\?.*)?$/.test(url)) {
      const match = url.match(/providerId=(\d+)/)
      const providerId = match ? Number(match[1]) : 0
      const models = state.modelsByProvider[providerId] ?? []
      const group = { providerId, providerName: state.providers.find(p => p.id === providerId)?.name ?? 'unknown', models }
      const [status, payload] = recordAndJson(state, method, url, body, { success: true, data: { groups: [group] } })
      return route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(payload) })
    }

    return route.fulfill({ status: 404, contentType: 'application/json', body: JSON.stringify({ success: false, message: `Mock 未覆盖: ${method} ${url}` }) })
  })

  return state
}

// ============================================================
// 测试用例
// ============================================================

test.describe('AI 提供方管理', () => {
  test.beforeEach(async ({ page }) => {
    await installMocks(page)
    await page.goto('/settings')
    // 点击侧栏「AI 提供方」导航项（button 文案）
    await page.getByRole('button', { name: 'AI 提供方' }).click()
    // 等待列表加载完成：列表头部标题「AI 提供者」可见
    await expect(page.getByRole('heading', { name: 'AI 提供者' })).toBeVisible()
  })

  test('T022: 侧栏无独立「模型列表」入口', async ({ page }) => {
    test.info().annotations.push({ type: 'scenario', description: '验证设置页侧栏导航项中没有「模型列表」入口' })
    const navButtons = page.locator('nav[aria-label="设置分类"] button')
    const labels = await navButtons.allTextContents()
    expect(labels).not.toContain('模型列表')
    // 同时确认「AI 提供方」入口存在
    expect(labels).toContain('AI 提供方')
  })

  test('T019: Provider 卡片展开/折叠展示模型列表', async ({ page }) => {
    test.info().annotations.push({ type: 'scenario', description: '点击 Provider 卡片展开 → 模型列表区域出现 → 再次点击折叠' })

    // 列表应渲染两个卡片（mock 数据 2 个 provider）
    const providerCards = page.locator('el-card').filter({ hasText: /Ollama-Local|OpenAI-Cloud/ })
    await expect(providerCards).toHaveCount(2)

    // 点击第一张卡片（Ollama-Local）展开
    const firstCard = providerCards.first()
    await firstCard.click()

    // 展开后应出现「模型列表」标题文字（操作栏 + 模型列表区域）
    await expect(firstCard.getByText('模型列表', { exact: true })).toBeVisible()
    // 模型项应出现：llama3
    await expect(firstCard.getByText('llama3', { exact: true })).toBeVisible()

    // 再次点击同一卡片折叠
    await firstCard.click()
    // 折叠后「模型列表」标题应消失
    await expect(firstCard.getByText('模型列表', { exact: true })).toBeHidden()
  })

  test('T021: 编辑 Provider 回填信息', async ({ page }) => {
    test.info().annotations.push({ type: 'scenario', description: '点击编辑按钮 → 抽屉弹出且表单回填 → 关闭抽屉' })

    // 点击第一张卡片的编辑按钮（el-button text + Edit icon）
    const firstCard = page.locator('el-card').first()
    // 编辑按钮是 text 类型按钮，含 el-icon Edit；用 within card 的 button 定位
    // el-button text 按钮没有可访问名称（只有 icon），用第 N 个 text button 定位
    // 第一个 text button 是 Edit，第二个是 Delete
    const editBtn = firstCard.locator('button').filter({ has: page.locator('.el-icon') }).first()
    await editBtn.click()

    // 抽屉弹出，标题为「编辑提供方」
    const drawer = page.locator('.el-drawer').filter({ hasText: '编辑提供方' })
    await expect(drawer).toBeVisible()

    // 验证表单回填：名称字段应为「Ollama-Local」
    const nameInput = drawer.getByLabel('显示名称')
    await expect(nameInput).toHaveValue('Ollama-Local')
    // endpoint 字段应为 mock 的 endpoint
    const endpointInput = drawer.getByLabel('接入地址 (Endpoint)')
    await expect(endpointInput).toHaveValue('http://localhost:11434/v1')

    // 关闭抽屉：点击「取消」按钮
    await drawer.getByRole('button', { name: '取消' }).click()
    await expect(drawer).toBeHidden()
  })

  test('T020: 删除 Provider', async ({ page }) => {
    test.info().annotations.push({ type: 'scenario', description: '点击删除 → 确认弹窗 → 确认 → 卡片移除' })

    const initialCardCount = await page.locator('el-card').filter({ hasText: /Ollama-Local|OpenAI-Cloud/ }).count()
    expect(initialCardCount).toBe(2)

    // 点击第一张卡片的删除按钮（第二个 text button）
    const firstCard = page.locator('el-card').first()
    const textButtons = firstCard.locator('button').filter({ has: page.locator('.el-icon') })
    const deleteBtn = textButtons.nth(1)
    await deleteBtn.click()

    // ElMessageBox 弹窗出现，标题「删除提供方」
    const msgbox = page.locator('.el-message-box').filter({ hasText: '删除提供方' })
    await expect(msgbox).toBeVisible()

    // 等待 DELETE 请求发出 + 列表刷新，先注册响应等待
    const deleteResponsePromise = page.waitForResponse(resp => resp.request().method() === 'DELETE' && /\/api\/ai-providers\/\d+/.test(resp.url()))
    const listResponsePromise = page.waitForResponse(resp => resp.request().method() === 'GET' && /\/api\/ai-providers\/?$/.test(resp.url()))

    // 点击确认删除（el-button danger 类型，文案「删除」）
    await msgbox.getByRole('button', { name: '删除' }).click()

    await deleteResponsePromise
    await listResponsePromise

    // 卡片数应减 1
    await expect(page.locator('el-card').filter({ hasText: /Ollama-Local|OpenAI-Cloud/ })).toHaveCount(1)
  })

  test('T018: 新增 Provider + 测试连接', async ({ page }) => {
    test.info().annotations.push({ type: 'scenario', description: '点击新建 → 填表单 → 保存 → 展开卡片 → 测试连接成功' })

    // 点击「新建提供方」按钮（页头 primary button）
    await page.getByRole('button', { name: '新建提供方' }).click()

    // 抽屉弹出，标题「新增提供方」
    const drawer = page.locator('.el-drawer').filter({ hasText: '新增提供方' })
    await expect(drawer).toBeVisible()

    // 填写表单
    await drawer.getByLabel('显示名称').fill('E2E-Mock-Provider')
    await drawer.getByLabel('接入地址 (Endpoint)').fill('http://localhost:18080/v1/chat/completions')
    await drawer.getByLabel('API Key').fill('sk-mock-key')

    // 等待 POST create + GET list 响应
    const createResponsePromise = page.waitForResponse(resp => resp.request().method() === 'POST' && /\/api\/ai-providers\/?$/.test(resp.url()))
    const listResponsePromise = page.waitForResponse(resp => resp.request().method() === 'GET' && /\/api\/ai-providers\/?$/.test(resp.url()))

    // 点击保存
    await drawer.getByRole('button', { name: '保存' }).click()

    await createResponsePromise
    await listResponsePromise

    // 抽屉关闭
    await expect(drawer).toBeHidden()

    // 新 Provider 出现在列表中
    await expect(page.locator('el-card').filter({ hasText: 'E2E-Mock-Provider' })).toBeVisible()

    // 点击新卡片展开
    const newCard = page.locator('el-card').filter({ hasText: 'E2E-Mock-Provider' })
    await newCard.click()
    // 展开后操作栏「测试连接」按钮可见
    const testBtn = newCard.getByRole('button', { name: '测试连接' })
    await expect(testBtn).toBeVisible()

    // 等待 test + fetch-models + list 响应
    const testResponsePromise = page.waitForResponse(resp => resp.request().method() === 'POST' && /\/api\/ai-providers\/\d+\/test/.test(resp.url()))
    const fetchResponsePromise = page.waitForResponse(resp => resp.request().method() === 'POST' && /\/api\/ai-providers\/\d+\/fetch-models/.test(resp.url()))

    // 点击测试连接
    await testBtn.click()

    await testResponsePromise
    await fetchResponsePromise

    // 测试结果区域出现「连接成功」（mock 返回 success: true, message: 连接成功）
    await expect(newCard.getByText('连接成功', { exact: true })).toBeVisible()
  })

  test('表单校验：名称为空时显示错误且不发请求', async ({ page }) => {
    test.info().annotations.push({ type: 'scenario', description: '新建抽屉打开 → 不填名称直接点保存 → 显示 el-alert 错误 → 无 POST 请求' })

    // 注册 POST 请求计数器（若被调用即失败）
    let postCreateCalled = false
    page.on('request', req => {
      if (req.method() === 'POST' && /\/api\/ai-providers\/?$/.test(req.url())) {
        postCreateCalled = true
      }
    })

    await page.getByRole('button', { name: '新建提供方' }).click()
    const drawer = page.locator('.el-drawer').filter({ hasText: '新增提供方' })
    await expect(drawer).toBeVisible()

    // 仅填 endpoint + apiKey，不填 name
    await drawer.getByLabel('接入地址 (Endpoint)').fill('http://localhost:18080/v1')
    await drawer.getByLabel('API Key').fill('sk-mock-key')

    // 点击保存
    await drawer.getByRole('button', { name: '保存' }).click()

    // 应显示 el-alert 错误，文案为「名称不能为空」
    await expect(drawer.locator('.el-alert').filter({ hasText: '名称不能为空' })).toBeVisible()

    // 抽屉仍打开（未关闭即说明保存未成功）
    await expect(drawer).toBeVisible()
    // 断言未发出 POST 创建请求
    expect(postCreateCalled).toBe(false)
  })

  test('表单校验：接入地址为空时显示错误', async ({ page }) => {
    await page.getByRole('button', { name: '新建提供方' }).click()
    const drawer = page.locator('.el-drawer').filter({ hasText: '新增提供方' })
    await expect(drawer).toBeVisible()

    await drawer.getByLabel('显示名称').fill('Test')
    await drawer.getByLabel('API Key').fill('sk-mock-key')
    // 不填 endpoint

    await drawer.getByRole('button', { name: '保存' }).click()

    await expect(drawer.locator('.el-alert').filter({ hasText: '接入地址不能为空' })).toBeVisible()
    await expect(drawer).toBeVisible()
  })

  test('表单校验：新增时 API Key 为空时显示错误', async ({ page }) => {
    await page.getByRole('button', { name: '新建提供方' }).click()
    const drawer = page.locator('.el-drawer').filter({ hasText: '新增提供方' })
    await expect(drawer).toBeVisible()

    await drawer.getByLabel('显示名称').fill('Test')
    await drawer.getByLabel('接入地址 (Endpoint)').fill('http://localhost:18080/v1')
    // 不填 apiKey

    await drawer.getByRole('button', { name: '保存' }).click()

    await expect(drawer.locator('.el-alert').filter({ hasText: '新增时 API Key 不能为空' })).toBeVisible()
    await expect(drawer).toBeVisible()
  })

  test('测试连接失败时显示错误提示', async ({ page }) => {
    test.info().annotations.push({ type: 'scenario', description: 'mock test 接口返回 success:false → 卡片显示错误消息' })

    // 修改 mock state 让 test 返回失败 —— 但 state 在 installMocks 闭包内无法直接改
    // 改用：重新注册一个覆盖路由，只覆盖 test 接口
    await page.unroute('**/api/ai-providers/*/test')
    await page.route('**/api/ai-providers/*/test', route => {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(ok({ success: false, message: '连接被拒绝', latencyMs: 0, statusCode: null })),
      })
    })

    // 展开第一张卡片
    const firstCard = page.locator('el-card').first()
    await firstCard.click()

    const testBtn = firstCard.getByRole('button', { name: '测试连接' })
    await expect(testBtn).toBeVisible()

    const testResponsePromise = page.waitForResponse(resp => resp.request().method() === 'POST' && /\/api\/ai-providers\/\d+\/test/.test(resp.url()))
    await testBtn.click()
    await testResponsePromise

    // 应显示错误消息「连接被拒绝」（红色文字）
    await expect(firstCard.getByText('连接被拒绝', { exact: true })).toBeVisible()
  })
})
