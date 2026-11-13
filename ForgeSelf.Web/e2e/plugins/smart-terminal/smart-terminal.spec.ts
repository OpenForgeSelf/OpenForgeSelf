import { expect, test } from '@playwright/test'
import { mkdirSync, readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import path from 'node:path'
import { injectRealApiKey } from '../../helpers/real-auth'

/**
 * 智能终端冒烟：仅 1 条主链路（plugin-development 铁律：每插件 1 冒烟 e2e）。
 */
const manifest = JSON.parse(
  readFileSync(fileURLToPath(new URL('../../../../Plugins/SmartTerminal/plugin.json', import.meta.url)), 'utf-8'),
)

const ROUTE = manifest.frontend?.route ?? '/smart-terminal'
const ENTRY_RE = /\/plugins\/smart-terminal\/web\/dist\/index\.js(\?|\s|$)/
const SHOTS = path.resolve(fileURLToPath(new URL('../../../screenshots/e2e/smart-terminal', import.meta.url)))

test.describe('smart-terminal smoke', () => {
  test.describe.configure({ timeout: 120_000 })

  test('route loads plugin view with title', async ({ page }) => {
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

    expect(errors.filter((e) => !e.includes('favicon')).length, errors.join('\n')).toBe(0)
    // entryHit 在未启用插件时可能为 false；标题断言是主判据
    void entryHit
  })
})
