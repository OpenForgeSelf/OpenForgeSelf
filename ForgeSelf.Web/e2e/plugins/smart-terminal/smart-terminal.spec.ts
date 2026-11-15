import { expect, test } from '@playwright/test'
import { mkdirSync, readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import path from 'node:path'
import { injectRealApiKey } from '../../helpers/real-auth'

/**
 * 智能终端冒烟：仅 1 条主链路（plugin-development 铁律：每插件 1 冒烟 e2e）。
 * PC-034（2026-11-14）验收扩展：会话内发一条真实助手消息——
 * 「宿主对话模块不可用」（424，StHostGateway 拿错容器）零容忍；
 * 上游模型不在线时允许出现其他上游错误（气泡存在且不含 424 文案即接线健康）。
 */
const manifest = JSON.parse(
  readFileSync(fileURLToPath(new URL('../../../../Plugins/SmartTerminal/plugin.json', import.meta.url)), 'utf-8'),
)

const ROUTE = manifest.frontend?.route ?? '/smart-terminal'
const ENTRY_RE = /\/plugins\/smart-terminal\/web\/dist\/index\.js(\?|\s|$)/
const SHOTS = path.resolve(fileURLToPath(new URL('../../../screenshots/e2e/smart-terminal', import.meta.url)))
const HOST_MODULE_UNAVAILABLE = '宿主对话模块不可用'

test.describe('smart-terminal smoke', () => {
  test.describe.configure({ timeout: 120_000 })

  test('route loads plugin view with title and assistant round has no 424', async ({ page }) => {
    const errors: string[] = []
    page.on('pageerror', (e) => errors.push(String(e)))

    await injectRealApiKey(page)
    await page.goto('/')
    await page.waitForResponse((r) => r.url().includes('/api/plugin/frontend-manifest') && r.ok(), { timeout: 30000 }).catch(() => null)
    await page.goto(ROUTE)
    await page.waitForLoadState('networkidle')

    // 入口脚本被拉到（若插件已启用且 dist 存在）
    const entryHit = await page.evaluate((reSource) => {
      const re = new RegExp(reSource)
      return [...document.querySelectorAll('script')].some((s) => re.test(s.src))
    }, ENTRY_RE.source).catch(() => false)

    // 标题标记见 Plugins/SmartTerminal/web/src/SmartTerminalView.vue:1217 `<div class="h1">智能终端<span class="ver">…`
    const title = page.locator('.h1')
    await expect(title).toContainText(/智能终端/, { timeout: 20000 })

    mkdirSync(SHOTS, { recursive: true })
    await page.screenshot({ path: path.join(SHOTS, 'smoke-view.png'), fullPage: true })

    // —— PC-034 验收：建本地会话 → 助手消息往返 → 424 文案零容忍 ——
    await page.getByTestId('st-new-session').click()
    // 起始目录默认 "~"，在 e2e 宿主（publish cwd）下解析为不存在路径；置 "." = 宿主 cwd（勿写绝对盘符路径，提交卫生 B13）
    await page.locator('[role="dialog"] input').first().fill('.')
    await page.getByTestId('st-new-session-submit').click()
    // 会话标签出现且处于激活态（.stab.on）= 会话已建并选中
    await page.locator('.stab.on').first().waitFor({ state: 'visible', timeout: 30000 })
    // 助手聊天面板在右侧 rail（st-toggle-rail 开关；railTab 默认 'ai'）
    await page.getByTestId('st-toggle-rail').click()

    const chatInput = page.locator('textarea[placeholder*="向助手提问"]')
    await chatInput.waitFor({ state: 'visible', timeout: 20000 })
    await chatInput.fill('你好，请报一声当前目录')
    await page.getByTestId('st-chat-send').click()

    // thinking 出现（快失败时可能来不及可见，容忍）→ 消失 = 往返完成
    await page.locator('[data-testid="st-chat-thinking"]')
      .waitFor({ state: 'visible', timeout: 15000 })
      .catch(() => null)
    await page.locator('[data-testid="st-chat-thinking"]').waitFor({ state: 'detached', timeout: 60000 })

    const assistantTexts = await page.locator('[data-testid^="st-chat-assistant-"]').allInnerTexts()
    expect(assistantTexts.length, '助手气泡应已出现（真实回复或上游错误气泡）').toBeGreaterThan(0)
    expect(
      assistantTexts.join('\n'),
      `助手气泡内容：${assistantTexts.join(' | ')}`,
    ).not.toContain(HOST_MODULE_UNAVAILABLE)

    await page.screenshot({ path: path.join(SHOTS, 'smoke-chat.png'), fullPage: true })

    expect(errors.filter((e) => !e.includes('favicon')).length, errors.join('\n')).toBe(0)
    // entryHit 在未启用插件时可能为 false；标题断言是主判据
    void entryHit
  })
})
