import { test, expect, type Page } from '@playwright/test'
import { injectRealApiKey } from './helpers/real-auth'

/**
 * AI Agent 聊天 E2E 测试 —— 对接真实后端 API（无 mock、真实认证）。
 *
 * 设计原则：
 * - **零 mock**：不拦截任何 /api/* 与 /chat/* 请求，全部走真实后端（http://localhost:7102）
 * - **真实认证**：注入 ForgeSetting.config 解密出的真实 API 密钥
 * - **无 waitForTimeout**：全部用 auto-waiting + waitForResponse
 *
 * 覆盖范围：
 * - /ai-agent 页面加载（聊天主区/输入框/发送按钮可见）
 * - 输入消息后发送按钮可用
 * - 发送消息：真实 POST /chat/message 请求发出
 */

// ============================================================
// 辅助函数
// ============================================================

/** 打开 AI Agent 页面 */
async function gotoAgent(page: Page): Promise<void> {
  await page.goto('/ai-agent')
}

// ============================================================
// 测试用例
// ============================================================

test.describe('AI Agent 聊天（/ai-agent）', () => {
  test.beforeEach(async ({ page }) => {
    await injectRealApiKey(page)
  })

  test('页面加载：聊天主区与输入框可见', async ({ page }) => {
    await gotoAgent(page)

    // 聊天主区与输入框
    await expect(page.locator('.panel-center')).toBeVisible({ timeout: 10000 })
    await expect(page.getByPlaceholder('输入消息...')).toBeVisible()
  })

  test('输入消息后发送按钮可用', async ({ page }) => {
    await gotoAgent(page)
    await expect(page.getByPlaceholder('输入消息...')).toBeVisible({ timeout: 10000 })

    // 初始发送按钮（有内容时可用；此处验证输入后消息发出）
    const sendBtn = page.getByRole('button', { name: '发送' })
    await page.getByPlaceholder('输入消息...').fill('你好，请回复 OK')
    await expect(sendBtn).toBeEnabled()
  })

  test('发送消息：真实 POST /chat/message 请求发出', async ({ page }) => {
    await gotoAgent(page)
    const input = page.getByPlaceholder('输入消息...')
    await expect(input).toBeVisible({ timeout: 10000 })

    // 等待真实聊天请求（chatApi.sendMessage → POST /api/chat）
    const chatResponsePromise = page.waitForResponse(
      (resp) => resp.request().method() === 'POST' && /\/api\/chat\/?$/.test(resp.url()),
      { timeout: 30000 }
    )
    await input.fill('你好，请回复 OK')
    await page.getByRole('button', { name: '发送' }).click()
    const chatResponse = await chatResponsePromise
    expect(chatResponse.status()).toBe(200)
  })
})
