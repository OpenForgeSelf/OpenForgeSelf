import { expect, test } from '@playwright/test'
import { mkdirSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import path from 'node:path'
import { injectRealApiKey } from '../../helpers/real-auth'

/**
 * 运行实例（:51888）实控走查（live config，testMatch 仅 *.live.spec.ts）：
 * 1) token 前置校验（401 立即上报，不把空页当结论）
 * 2) 插件管理页：SmartTerminal v2.0.0 → 版本历史 → 激活侧载的 v2.0.1（PC-034 修复批）
 * 3) 智能终端页：版本徽标 v2.0.1 → 真实会话 + 助手往返 → 「宿主对话模块不可用」零容忍
 * 截图存档：ForgeSelf.Web/screenshots/live-51888/（用户授权的侧载更新，只动本插件版本）
 */
const SHOTS = path.resolve(fileURLToPath(new URL('../../../screenshots/live-51888', import.meta.url)))
const HOST_MODULE_UNAVAILABLE = '宿主对话模块不可用'

test.describe.configure({ timeout: 180_000 })

test('51888 侧载 smart-terminal 2.0.1 并实测助手链路', async ({ page }) => {
  const errors: string[] = []
  page.on('pageerror', (e) => errors.push(String(e)))
  mkdirSync(SHOTS, { recursive: true })

  await injectRealApiKey(page)
  await page.goto('/')
  await page.waitForLoadState('networkidle')

  // 0) token 前置校验：需要鉴权的只读端点必须 200
  const auth = await page.evaluate(async () => {
    const token = localStorage.getItem('forge_api_token')
    const resp = await fetch('/api/plugin/detail/smart-terminal', {
      headers: token ? { Authorization: `Bearer ${token}` } : undefined,
    })
    return { status: resp.status }
  })
  expect(auth.status, `token 鉴权返回 ${auth.status}（401=宿主轮换令牌，先解决 token 再走查）`).toBe(200)

  // 1) 插件管理页：版本状态截图；未激活则走「版本历史 → 激活 v2.0.1」（幂等：已激活则跳过激活步）
  await page.goto('/plugins')
  const card = page.locator('.plugin-card', { hasText: '智能终端' })
  const cardVersion = card.locator('.plugin-version')
  await expect(cardVersion).toHaveText(/v2\.0\.[01]/, { timeout: 30000 })
  await page.screenshot({ path: path.join(SHOTS, 'live-plugins-before.png'), fullPage: true })

  if ((await cardVersion.innerText()).trim() !== 'v2.0.1') {
    await card.locator('.version-btn').click()
    const row201 = page.locator('.version-row', { hasText: 'v2.0.1' })
    await expect(row201).toBeVisible({ timeout: 20000 })
    await page.screenshot({ path: path.join(SHOTS, 'live-versions-dialog.png') })
    await row201.getByRole('button', { name: '激活此版本' }).click()
    await page.locator('.el-message-box').getByRole('button', { name: '激活', exact: true }).click()
  }
  await expect(cardVersion).toHaveText('v2.0.1', { timeout: 60000 })
  await page.screenshot({ path: path.join(SHOTS, 'live-plugins-after.png'), fullPage: true })

  // 2) 智能终端页：版本徽标自证跑的是新版
  await page.goto('/smart-terminal')
  await page.waitForLoadState('networkidle')
  await expect(page.locator('.h1')).toContainText('智能终端', { timeout: 30000 })
  await expect(page.locator('.h1 .ver')).toHaveText(/v2\.0\.1/)
  await page.screenshot({ path: path.join(SHOTS, 'live-st-view-201.png'), fullPage: true })

  // 3) 真实会话 + 助手往返（用户实例已配模型 → 期望真实回复；无论如何 424 病文案零容忍）
  await page.getByTestId('st-new-session').click()
  await page.locator('[role="dialog"] input').first().fill('.')
  await page.getByTestId('st-new-session-submit').click()
  await page.locator('.stab.on').first().waitFor({ state: 'visible', timeout: 30000 })
  await page.getByTestId('st-toggle-rail').click()

  const chatInput = page.locator('textarea[placeholder*="向助手提问"]')
  await chatInput.waitFor({ state: 'visible', timeout: 20000 })
  await chatInput.fill('你好，请报一声当前目录')
  await page.getByTestId('st-chat-send').click()

  await page.locator('[data-testid="st-chat-thinking"]')
    .waitFor({ state: 'visible', timeout: 15000 })
    .catch(() => null)
  await page.locator('[data-testid="st-chat-thinking"]').waitFor({ state: 'detached', timeout: 90000 })

  const assistantTexts = await page.locator('[data-testid^="st-chat-assistant-"]').allInnerTexts()
  expect(assistantTexts.length, `助手气泡应出现，实际：${assistantTexts.join(' | ')}`).toBeGreaterThan(0)
  expect(assistantTexts.join('\n'), `助手气泡内容：${assistantTexts.join(' | ')}`).not.toContain(HOST_MODULE_UNAVAILABLE)

  await page.screenshot({ path: path.join(SHOTS, 'live-st-chat-201.png'), fullPage: true })

  expect(errors.filter((e) => !e.includes('favicon')), `页面错误：${errors.join('\n')}`).toHaveLength(0)
})
