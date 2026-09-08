import { test as base, expect } from '@playwright/test'
import { injectRealApiKey } from '../helpers/real-auth'

/**
 * 插件层 e2e 统一 fixture。
 * 自动在每次用例前注入真实 API 密钥（来自 globalSetup 的 E2E_API_TOKEN，
 * 或回退解密 ForgeSetting.config），使前端请求携带真实认证、零 mock。
 *
 * 用法：
 *   import { test, expect } from '../fixtures/e2e'
 *   test('...', async ({ page }) => { await page.goto('/plugin-view/sems') ... })
 */
export const test = base.extend<object>({})

test.beforeEach(async ({ page }) => {
  await injectRealApiKey(page)
})

export { expect }
