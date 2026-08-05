import { test, expect } from '@playwright/test'
import { startMockUpstream, type MockUpstream } from './helpers/mock-upstream'
import { getRealApiKey } from './helpers/real-auth'

/**
 * OpenForgeSelf API 网关 E2E 测试 —— Playwright 官方 request fixture 最佳实践。
 *
 * 测试分层（对齐测试金字塔）：
 *   xUnit 单元/集成测试 → 覆盖控制器逻辑 + provider 前缀剥离 + stream_options 透传（TestAIProvider 记录请求）
 *   Playwright E2E（本文件） → 覆盖真实 HTTP 全链路：认证中间件 → 路由 → 控制器 → 响应序列化
 *
 * 环境变量：
 *   - OPENFORGE_API_KEY（可选）：API 网关 Bearer Token；缺省时从 ForgeSetting.config
 *     运行时解密当前真实密钥（真实认证，密钥轮换后自动跟随）
 *   - OPENFORGE_BACKEND_URL（可选）：后端地址，默认 http://localhost:7102
 *   - OPENFORGE_E2E_MOCK_PROVIDER（可选）：触发测试组 2，值为后端预配置的 provider 名（如 e2e-mock）
 *
 * 测试组 2 前置条件：
 *   后端需预配置一个名为 `${OPENFORGE_E2E_MOCK_PROVIDER}` 的 provider，
 *   其 Endpoint 指向 http://localhost:18080/v1/chat/completions（mock upstream 监听端口）。
 *   说明：mock upstream 模拟的是外部 LLM 上游（网关 → 上游请求在后端进程内发起，
 *   浏览器拦截不到），属外部依赖模拟，非本项目后端 API mock；测试组 1 全走真实后端。
 *
 * 运行方式：
 *   pnpm test:e2e e2e/api-gateway.spec.ts
 *   OPENFORGE_API_KEY=sk-xxx OPENFORGE_E2E_MOCK_PROVIDER=e2e-mock pnpm test:e2e e2e/api-gateway.spec.ts
 */

const API_KEY = process.env.OPENFORGE_API_KEY ?? getRealApiKey()
const BACKEND_URL = process.env.OPENFORGE_BACKEND_URL ?? 'http://localhost:7102'
const MOCK_PROVIDER = process.env.OPENFORGE_E2E_MOCK_PROVIDER

// 公共：带 API key 的 headers 构造器
const authHeaders = () => ({ Authorization: `Bearer ${API_KEY}` })

// ============================================================
// 测试组 1：API 网关冒烟测试（不依赖上游，默认运行）
// 验证：认证中间件、路由、请求校验、基本响应格式
// ============================================================
test.describe('API 网关冒烟测试', () => {
  test.skip(!API_KEY, '需要环境变量 OPENFORGE_API_KEY')

  test('GET /api/health 返回 200 + healthy 状态', async ({ request }) => {
    const resp = await request.get(`${BACKEND_URL}/api/health`)
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(body.status).toBe('healthy')
    expect(body).toHaveProperty('timestamp')
  })

  test('无 Authorization 头访问 /v1/models 返回 401', async ({ request }) => {
    const resp = await request.get(`${BACKEND_URL}/v1/models`)
    expect(resp.status()).toBe(401)
  })

  test('错误 API key 访问 /v1/models 返回 401', async ({ request }) => {
    const resp = await request.get(`${BACKEND_URL}/v1/models`, {
      headers: { Authorization: 'Bearer sk-invalid-key' },
    })
    expect(resp.status()).toBe(401)
  })

  test('有效 API key 访问 /v1/models 返回 200 + list 结构', async ({ request }) => {
    const resp = await request.get(`${BACKEND_URL}/v1/models`, {
      headers: authHeaders(),
    })
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(body.object).toBe('list')
    expect(Array.isArray(body.data)).toBe(true)
  })

  test('POST /v1/chat/completions 缺 model 返回 400', async ({ request }) => {
    const resp = await request.post(`${BACKEND_URL}/v1/chat/completions`, {
      headers: authHeaders(),
      data: {
        messages: [{ role: 'user', content: 'hi' }],
      },
    })
    expect(resp.status()).toBe(400)
    const body = await resp.json()
    expect(body.error).toBeDefined()
    expect(body.error.type).toBe('invalid_request_error')
  })

  test('POST /v1/chat/completions 缺 messages 返回 400', async ({ request }) => {
    const resp = await request.post(`${BACKEND_URL}/v1/chat/completions`, {
      headers: authHeaders(),
      data: {
        model: 'gpt-4',
      },
    })
    expect(resp.status()).toBe(400)
    const body = await resp.json()
    expect(body.error).toBeDefined()
    expect(body.error.type).toBe('invalid_request_error')
  })

  test('POST /v1/chat/completions 空 messages 数组返回 400', async ({ request }) => {
    const resp = await request.post(`${BACKEND_URL}/v1/chat/completions`, {
      headers: authHeaders(),
      data: {
        model: 'gpt-4',
        messages: [],
      },
    })
    expect(resp.status()).toBe(400)
  })

  test('未知 provider 前缀返回错误响应（provider 路由失败）', async ({ request }) => {
    const resp = await request.post(`${BACKEND_URL}/v1/chat/completions`, {
      headers: authHeaders(),
      data: {
        model: 'nonexistent-provider:gpt-4',
        messages: [{ role: 'user', content: 'hi' }],
      },
    })
    // 真实后端行为：未知 provider 前缀回退 GetProviderByModel 默认 provider，
    // 上游不可用时（如 LM Studio 未就绪）网关将上游错误包装为 5xx。
    // 只断言响应是确定的错误响应（4xx/5xx 且含 error 结构），不锁死状态码，避免环境差异导致 flaky。
    expect(resp.status()).toBeGreaterThanOrEqual(400)
    expect(resp.status()).toBeLessThan(600)
    const body = await resp.json()
    expect(body.error).toBeDefined()
  })
})

// ============================================================
// 测试组 2：API 网关行为测试（mock upstream，需预配置 provider）
// 验证：provider 前缀剥离、stream_options 透传
// 这是本次会话修复的两个 bug 的 E2E 回归保护
// ============================================================
test.describe('API 网关行为测试（mock upstream）', () => {
  test.skip(!MOCK_PROVIDER, '需要环境变量 OPENFORGE_E2E_MOCK_PROVIDER 指向预配置的 provider')
  test.skip(!API_KEY, '需要环境变量 OPENFORGE_API_KEY')

  let mock: MockUpstream

  test.beforeAll(async () => {
    mock = await startMockUpstream(18080)
  })

  test.afterAll(async () => {
    await mock.close()
  })

  test.beforeEach(() => {
    mock.reset()
  })

  test('provider 前缀剥离：客户端发 "provider:model"，上游收到 "model"', async ({ request }) => {
    test.info().annotations.push({
      type: 'regression',
      description: '回归保护：本次会话修复的 provider 前缀剥离 bug（OpenAIChatController.ResolveProviderAndModel）',
    })

    const resp = await request.post(`${BACKEND_URL}/v1/chat/completions`, {
      headers: authHeaders(),
      data: {
        model: `${MOCK_PROVIDER}:mock-model`,
        messages: [{ role: 'user', content: 'hi' }],
        stream: false,
      },
    })

    expect(resp.status()).toBe(200)

    const upstreamReq = mock.lastRequest()
    // 上游 mock server 应收到请求
    expect(upstreamReq).not.toBeNull()
    // 上游收到的 model 必须剥离 provider 前缀
    expect(upstreamReq!.body.model).toBe('mock-model')
    expect(upstreamReq!.body.model).not.toContain(':')
  })

  test('stream_options 透传：客户端发 include_usage=true，上游收到相同字段', async ({ request }) => {
    test.info().annotations.push({
      type: 'regression',
      description: '回归保护：本次会话修复的 stream_options 透传 bug（3 层 DTO + 2 处转换方法）',
    })

    const resp = await request.post(`${BACKEND_URL}/v1/chat/completions`, {
      headers: authHeaders(),
      data: {
        model: `${MOCK_PROVIDER}:mock-model`,
        messages: [{ role: 'user', content: 'hi' }],
        stream: true,
        stream_options: { include_usage: true },
      },
      // 流式响应：Playwright request 会等待完整 body，mock 返回短小 SSE，可正常接收
      maxRedirects: 0,
    })

    // 流式响应状态码应为 200（即使最终是 SSE）
    expect(resp.status()).toBe(200)

    const upstreamReq = mock.lastRequest()
    // 上游 mock server 应收到请求
    expect(upstreamReq).not.toBeNull()
    // stream_options 必须透传到上游
    expect(upstreamReq!.body.stream_options).toBeDefined()
    // include_usage=true 必须透传
    expect(upstreamReq!.body.stream_options.include_usage).toBe(true)
    // stream=true 必须透传
    expect(upstreamReq!.body.stream).toBe(true)
  })

  test('非流式请求无 stream_options 时，上游也不应收到 stream_options', async ({ request }) => {
    const resp = await request.post(`${BACKEND_URL}/v1/chat/completions`, {
      headers: authHeaders(),
      data: {
        model: `${MOCK_PROVIDER}:mock-model`,
        messages: [{ role: 'user', content: 'hi' }],
        stream: false,
      },
    })

    expect(resp.status()).toBe(200)

    const upstreamReq = mock.lastRequest()
    expect(upstreamReq).not.toBeNull()
    // 非流式 + 无 stream_options：上游收到的 stream_options 应为 null/undefined
    expect(upstreamReq!.body.stream_options ?? null).toBeNull()
  })
})
