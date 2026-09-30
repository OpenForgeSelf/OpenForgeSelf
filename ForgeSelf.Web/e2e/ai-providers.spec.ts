import { test, expect, type Page } from '@playwright/test'
import { injectRealApiKey, getRealApiKey } from './helpers/real-auth'

/**
 * AI 提供方管理 E2E 测试 —— 对接真实后端 API（无 mock、真实认证）。
 *
 * 设计原则：
 * - **零 mock**：不拦截任何 /api/* 请求，全部走真实后端（http://localhost:7102）
 * - **真实认证**：启动时解密 ForgeSetting.config 中的真实 API 密钥并注入
 *   localStorage，前端请求携带真实 Authorization 头
 * - **数据隔离**：创建型用例使用唯一名称（E2E-Real-*）的测试 provider，
 *   用例结束立即删除；绝不动用户的真实 provider（如 default）
 * - **无 waitForTimeout**：全部用 auto-waiting + waitForResponse
 * - **语义化 selector**：优先 getByRole/getByText/getByPlaceholder；
 *   卡片用 .el-card（Element Plus 渲染 div.el-card，非 <el-card> 标签）
 *
 * 覆盖范围：
 * - 侧栏导航无「模型列表」入口
 * - Provider 卡片加载（真实列表）
 * - 卡片展开/折叠展示模型列表
 * - 编辑 Provider 回填真实信息
 * - 删除 Provider（自包含：创建 → 删除）
 * - 新增 Provider + 测试连接（真实请求不可达端点 → 真实失败提示）
 * - 表单必填校验（纯前端，不发请求）
 */

// ============================================================
// 辅助函数
// ============================================================

const BACKEND_URL = process.env.E2E_BACKEND_URL ?? 'http://localhost:7102'
const API_BASE = `${BACKEND_URL}/api/ai-providers`

/** 认证请求头（真实密钥） */
function authHeaders(): Record<string, string> {
  return { Authorization: `Bearer ${getRealApiKey()}` }
}

interface TestProvider {
  id: number
  name: string
}

/** 通过真实后端 API 创建测试 provider，返回 id 与名称 */
async function createTestProvider(name: string, endpoint: string): Promise<TestProvider> {
  const res = await fetch(API_BASE, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', ...authHeaders() },
    body: JSON.stringify({
      name,
      providerType: 'OpenAI',
      endpoint,
      apiKey: 'sk-e2e-real-test-key',
      supportedModels: [],
      isDefault: false,
      timeoutSeconds: 10,
      enableMultimodal: false,
    }),
  })
  const json = (await res.json()) as { success: boolean; data?: { id: number } }
  if (!res.ok || !json.success || !json.data) {
    throw new Error(`创建测试 provider 失败: ${res.status} ${JSON.stringify(json)}`)
  }
  return { id: json.data.id, name }
}

/** 通过真实后端 API 删除测试 provider（忽略 404） */
async function deleteTestProvider(id: number): Promise<void> {
  try {
    await fetch(`${API_BASE}/${id}`, { method: 'DELETE', headers: authHeaders() })
  } catch {
    // 清理阶段失败不影响用例结果，由 finally 兜底
  }
}

/** 导航到 AI 提供方面板（auto-waiting） */
async function navigateToAiProviders(page: Page): Promise<void> {
  await page.goto('/settings')
  await page.getByRole('button', { name: 'AI 提供方' }).click()
  await expect(page.getByRole('heading', { name: 'AI 提供者' })).toBeVisible()
}

/** 在卡片区域内定位第 index 个含 icon 的按钮（卡片操作按钮为 text 型，只有 icon 无文字） */
function iconButtonInCard(card: ReturnType<Page['locator']>, page: Page, index: number) {
  return card.locator('button').filter({ has: page.locator('.el-icon') }).nth(index)
}

/** 抽屉内按 placeholder 定位输入框（label 无 for 关联，getByLabel 不可用） */
function drawerInput(drawer: ReturnType<Page['locator']>, placeholder: string) {
  return drawer.getByPlaceholder(placeholder)
}

// ============================================================
// 测试用例
// ============================================================

test.describe('AI 提供方管理（真实后端）', () => {
  test.beforeEach(async ({ page }) => {
    await injectRealApiKey(page)
  })

  test('T022: 侧栏无独立「模型列表」入口', async ({ page }) => {
    await navigateToAiProviders(page)
    const navButtons = page.locator('nav[aria-label="设置分类"] button')
    const labels = await navButtons.allTextContents()
    expect(labels).not.toContain('模型列表')
    expect(labels).toContain('AI 提供方')
  })

  test('真实后端列表加载：默认 provider 卡片可见', async ({ page }) => {
    await navigateToAiProviders(page)

    // 真实后端至少包含 default provider（由后端播种）
    await expect(page.locator('.el-card').filter({ hasText: 'default' }).first()).toBeVisible()
    // 页头显示提供方数量标签
    await expect(page.getByText(/个提供方/)).toBeVisible()
  })

  test('T019: Provider 卡片展开/折叠展示模型列表', async ({ page }) => {
    await navigateToAiProviders(page)

    const firstCard = page.locator('.el-card').filter({ hasText: 'default' }).first()
    await firstCard.click()

    // 展开后出现「模型列表」标题（真实 ai-models 接口数据）
    await expect(firstCard.getByText('模型列表', { exact: true })).toBeVisible()

    // 再次点击卡片头部折叠（展开区域有 @click.stop，不能点内容区）
    await firstCard.getByText('default', { exact: true }).click()
    await expect(firstCard.getByText('模型列表', { exact: true })).toBeHidden()
  })

  test('T021: 编辑 Provider 回填真实信息', async ({ page }) => {
    await navigateToAiProviders(page)

    const firstCard = page.locator('.el-card').filter({ hasText: 'default' }).first()
    // 第一个 text button 是 Edit
    await iconButtonInCard(firstCard, page, 0).click()

    const drawer = page.locator('.el-drawer').filter({ hasText: '编辑提供方' })
    await expect(drawer).toBeVisible()

    // 回填真实名称（default）与真实 endpoint
    await expect(drawerInput(drawer, '如 Ollama (本地)')).toHaveValue('default')
    await expect(drawerInput(drawer, 'http://localhost:11434/v1')).toHaveValue(
      'http://localhost:1234/v1/chat/completions'
    )

    await drawer.getByRole('button', { name: '取消' }).click()
    await expect(drawer).toBeHidden()
  })

  test('T020: 删除 Provider（自包含：创建 → 删除）', async ({ page }) => {
    const testName = `E2E-Real-Delete-${Date.now()}`
    const created = await createTestProvider(testName, 'http://localhost:18081/v1/chat/completions')
    try {
      await navigateToAiProviders(page)

      // 新创建的测试 provider 出现在列表中
      const card = page.locator('.el-card').filter({ hasText: testName })
      await expect(card).toBeVisible()

      // 点击删除按钮（第二个 text button：Delete）
      await iconButtonInCard(card, page, 1).click()

      // 确认弹窗出现
      const msgbox = page.locator('.el-message-box').filter({ hasText: '删除提供方' })
      await expect(msgbox).toBeVisible()

      // 等待 DELETE 请求真实发出并返回 204
      const deleteResponsePromise = page.waitForResponse(
        (resp) =>
          resp.request().method() === 'DELETE' &&
          new RegExp(`/api/ai-providers/${created.id}`).test(resp.url())
      )
      await msgbox.getByRole('button', { name: '删除' }).click()
      await deleteResponsePromise

      // 卡片移除（真实删除生效）
      await expect(card).toBeHidden()
    } finally {
      await deleteTestProvider(created.id)
    }
  })

  test('T018: 新增 Provider + 测试连接（真实请求不可达端点 → 真实失败提示）', async ({ page }) => {
    const testName = `E2E-Real-Create-${Date.now()}`
    let createdId: number | null = null
    try {
      await navigateToAiProviders(page)

      // 点击「新建提供方」
      await page.getByRole('button', { name: '新建提供方' }).click()
      const drawer = page.locator('.el-drawer').filter({ hasText: '新增提供方' })
      await expect(drawer).toBeVisible()

      // 填写表单：endpoint 指向未监听端口 18081，测试连接必然真实失败
      await drawerInput(drawer, '如 Ollama (本地)').fill(testName)
      await drawerInput(drawer, 'http://localhost:11434/v1').fill(
        'http://localhost:18081/v1/chat/completions'
      )
      await drawerInput(drawer, 'sk-...').fill('sk-e2e-real-key')

      const createResponsePromise = page.waitForResponse(
        (resp) => resp.request().method() === 'POST' && /\/api\/ai-providers\/?$/.test(resp.url())
      )
      await drawer.getByRole('button', { name: '保存' }).click()
      await createResponsePromise

      // 抽屉关闭，新 provider 出现在列表
      await expect(drawer).toBeHidden()
      const newCard = page.locator('.el-card').filter({ hasText: testName })
      await expect(newCard).toBeVisible()

      // 读取真实 id 供清理
      const list = (await (
        await fetch(API_BASE, { headers: authHeaders() })
      ).json()) as { data: { id: number; name: string }[] }
      createdId = list.data.find((p) => p.name === testName)?.id ?? null

      // 展开卡片 → 测试连接按钮可见
      await newCard.click()
      const testBtn = newCard.getByRole('button', { name: '测试连接' })
      await expect(testBtn).toBeVisible()

      // 点击测试连接：真实请求 /{id}/test，不可达端点 → 后端返回「无法连接」
      const testResponsePromise = page.waitForResponse(
        (resp) => resp.request().method() === 'POST' && /\/api\/ai-providers\/\d+\/test/.test(resp.url())
      )
      await testBtn.click()
      await testResponsePromise

      // 卡片上展示真实失败结果（无法连接）
      await expect(newCard.getByText(/无法连接/)).toBeVisible()
    } finally {
      if (createdId != null) await deleteTestProvider(createdId)
    }
  })

  test('表单校验：名称为空时显示错误且不发请求', async ({ page }) => {
    await navigateToAiProviders(page)

    let postCreateCalled = false
    page.on('request', (req) => {
      if (req.method() === 'POST' && /\/api\/ai-providers\/?$/.test(req.url())) {
        postCreateCalled = true
      }
    })

    await page.getByRole('button', { name: '新建提供方' }).click()
    const drawer = page.locator('.el-drawer').filter({ hasText: '新增提供方' })
    await expect(drawer).toBeVisible()

    await drawerInput(drawer, 'http://localhost:11434/v1').fill('http://localhost:18081/v1')
    await drawerInput(drawer, 'sk-...').fill('sk-e2e-real-key')

    await drawer.getByRole('button', { name: '保存' }).click()

    await expect(drawer.locator('.el-alert').filter({ hasText: '名称不能为空' })).toBeVisible()
    await expect(drawer).toBeVisible()
    expect(postCreateCalled).toBe(false)
  })

  test('表单校验：接入地址为空时显示错误', async ({ page }) => {
    await navigateToAiProviders(page)

    await page.getByRole('button', { name: '新建提供方' }).click()
    const drawer = page.locator('.el-drawer').filter({ hasText: '新增提供方' })
    await expect(drawer).toBeVisible()

    await drawerInput(drawer, '如 Ollama (本地)').fill('Test')
    await drawerInput(drawer, 'sk-...').fill('sk-e2e-real-key')

    await drawer.getByRole('button', { name: '保存' }).click()

    await expect(drawer.locator('.el-alert').filter({ hasText: '接入地址不能为空' })).toBeVisible()
    await expect(drawer).toBeVisible()
  })

  test('表单校验：新增时 API Key 为空时显示错误', async ({ page }) => {
    await navigateToAiProviders(page)

    await page.getByRole('button', { name: '新建提供方' }).click()
    const drawer = page.locator('.el-drawer').filter({ hasText: '新增提供方' })
    await expect(drawer).toBeVisible()

    await drawerInput(drawer, '如 Ollama (本地)').fill('Test')
    await drawerInput(drawer, 'http://localhost:11434/v1').fill('http://localhost:18081/v1')

    await drawer.getByRole('button', { name: '保存' }).click()

    await expect(drawer.locator('.el-alert').filter({ hasText: '新增时 API Key 不能为空' })).toBeVisible()
    await expect(drawer).toBeVisible()
  })

  test('测试连接失败时显示错误提示（真实不可达端点）', async ({ page }) => {
    const testName = `E2E-Real-Fail-${Date.now()}`
    const created = await createTestProvider(testName, 'http://localhost:18081/v1/chat/completions')
    try {
      await navigateToAiProviders(page)

      const card = page.locator('.el-card').filter({ hasText: testName })
      await card.click()

      const testBtn = card.getByRole('button', { name: '测试连接' })
      await expect(testBtn).toBeVisible()

      const testResponsePromise = page.waitForResponse(
        (resp) => resp.request().method() === 'POST' && /\/api\/ai-providers\/\d+\/test/.test(resp.url())
      )
      await testBtn.click()
      await testResponsePromise

      // 真实后端对不可达端点返回「无法连接」错误
      await expect(card.getByText(/无法连接/)).toBeVisible()
    } finally {
      await deleteTestProvider(created.id)
    }
  })
})
