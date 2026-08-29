import { test, expect, type Page } from '@playwright/test'
import { injectRealApiKey, getRealApiKey } from './helpers/real-auth'

/**
 * AI 提供者 → 模型列表 → 聊天请求 E2E 测试 —— 对接真实后端与真实 LM Studio。
 *
 * 设计原则：
 * - **零 mock**：不拦截任何 /api/* 与 /v1/* 请求，全部走真实后端（http://localhost:7102）
 * - **真实认证**：注入 ForgeSetting.config 解密出的真实 API 密钥
 * - **真实上游**：provider 指向本地 LM Studio（http://localhost:1234），
 *   fetch-models 真实拉取上游 /models，聊天请求真实经网关转发到上游
 * - **数据隔离**：用例创建 E2E-* 测试 provider，finally 删除，绝不动真实 provider
 * - **模型选择**：使用 LM Studio 实际可加载的模型（llama-3.2-1b-instruct）
 *   —— qwythos-9b-v2 在 LM Studio 中报 "Failed to load model"，不能用于真实聊天
 *
 * 覆盖范围：
 * - 设置页添加 AI 提供者（真实 LM Studio 配置）
 * - 展开卡片 → 获取模型（fetch-models 真实拉取）→ 模型列表展示提供者的模型
 * - 通过 API 服务展示的接口（/v1/chat/completions）用新模型真实请求聊天并得到回复
 */

// ============================================================
// 真实环境常量（与 appsettings.json 的 AI 配置一致）
// ============================================================

const LM_ENDPOINT = 'http://localhost:1234/v1/chat/completions'
const LM_KEY = 'sk-lm-4YQwPb7k:aOLZ6rcZOAiPBo3LKd33'
/** LM Studio 实际可加载的模型（fetch-models 拉取后模型列表可见，且可真实聊天） */
const LM_MODEL = 'llama-3.2-1b-instruct'

const BACKEND_URL = 'http://localhost:7102'
const API_BASE = `${BACKEND_URL}/api/ai-providers`

// ============================================================
// 辅助函数
// ============================================================

function authHeaders(): Record<string, string> {
  return { Authorization: `Bearer ${getRealApiKey()}` }
}

/** 通过真实后端 API 创建测试 provider，返回 id */
async function createTestProvider(name: string): Promise<number> {
  const res = await fetch(API_BASE, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', ...authHeaders() },
    body: JSON.stringify({
      name,
      providerType: 'OpenAI',
      endpoint: LM_ENDPOINT,
      apiKey: LM_KEY,
      supportedModels: [LM_MODEL],
      isDefault: false,
      timeoutSeconds: 120,
      enableMultimodal: false,
    }),
  })
  const json = (await res.json()) as { success: boolean; data?: { id: number } }
  if (!res.ok || !json.success || !json.data) {
    throw new Error(`创建测试 provider 失败: ${res.status} ${JSON.stringify(json)}`)
  }
  return json.data.id
}

/** 通过真实后端 API 删除测试 provider（忽略 404） */
async function deleteTestProvider(id: number): Promise<void> {
  try {
    await fetch(`${API_BASE}/${id}`, { method: 'DELETE', headers: authHeaders() })
  } catch {
    // 清理失败不影响用例结果
  }
}

/** 导航到 AI 提供方面板 */
async function navigateToAiProviders(page: Page): Promise<void> {
  await page.goto('/settings')
  await page.getByRole('button', { name: 'AI 提供方' }).click()
  await expect(page.getByRole('heading', { name: 'AI 提供者' })).toBeVisible()
}

// ============================================================
// 测试用例
// ============================================================

test.describe('AI 提供者 → 模型列表 → 聊天请求（真实后端 + LM Studio）', () => {
  test.beforeEach(async ({ page }) => {
    await injectRealApiKey(page)
  })

  test('添加 AI 提供者：模型列表能看到提供者提供的模型（fetch-models 真实拉取）', async ({ page }) => {
    const testName = `E2E-Provider-${Date.now()}`
    const createdId = await createTestProvider(testName)
    try {
      await navigateToAiProviders(page)

      // 新 provider 卡片出现
      const card = page.locator('.el-card').filter({ hasText: testName })
      await expect(card).toBeVisible()

      // 展开卡片
      await card.click()
      await expect(card.getByText('模型列表', { exact: true })).toBeVisible()

      // 点击「获取模型」→ 真实请求 fetch-models，从 LM Studio 拉取模型入库
      const fetchResponsePromise = page.waitForResponse(
        (resp) =>
          resp.request().method() === 'POST' &&
          new RegExp(`/api/ai-providers/${createdId}/fetch-models`).test(resp.url())
      )
      await card.getByRole('button', { name: '获取模型' }).click()
      await fetchResponsePromise

      // 模型列表展示提供者提供的模型（LM Studio 真实模型名）
      await expect(card.getByText(LM_MODEL, { exact: true })).toBeVisible({ timeout: 15000 })
    } finally {
      await deleteTestProvider(createdId)
    }
  })

  test('通过 API 服务展示的接口：用新加模型的 chatModelId 真实请求聊天并得到回复', async () => {
    const testName = `E2E-Chat-${Date.now()}`
    const createdId = await createTestProvider(testName)
    try {
      // fetch-models 真实拉取，拿到新模型的 chatModelId（如 E2E-Chat-xxx:llama-3.2-1b-instruct）
      const fetchRes = await fetch(`${API_BASE}/${createdId}/fetch-models`, {
        method: 'POST',
        headers: authHeaders(),
      })
      const fetchJson = (await fetchRes.json()) as {
        data?: { models?: { chatModelId: string }[] }
      }
      const chatModelId = fetchJson.data?.models?.find((m) =>
        m.chatModelId.includes(LM_MODEL)
      )?.chatModelId
      expect(chatModelId, 'fetch-models 应返回新模型的 chatModelId').toBeDefined()

      // 通过 API 服务展示的接口（/v1/chat/completions）真实请求聊天
      const chatRes = await fetch(`${BACKEND_URL}/v1/chat/completions`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', ...authHeaders() },
        body: JSON.stringify({
          model: chatModelId,
          messages: [{ role: 'user', content: '请回复 OK' }],
          max_tokens: 20,
          stream: false,
        }),
        signal: AbortSignal.timeout(60000),
      })

      expect(chatRes.status).toBe(200)
      const chatBody = (await chatRes.json()) as {
        choices?: { message?: { content?: string } }[]
      }
      expect(chatBody.choices?.length).toBeGreaterThan(0)
      const content = chatBody.choices?.[0]?.message?.content ?? ''
      expect(content.trim().length).toBeGreaterThan(0)
    } finally {
      await deleteTestProvider(createdId)
    }
  })
})
