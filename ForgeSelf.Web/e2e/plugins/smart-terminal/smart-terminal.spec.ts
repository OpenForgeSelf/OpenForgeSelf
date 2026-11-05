import { expect, test } from '../../fixtures/e2e'
import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'

/**
 * 智能终端冒烟：仅 1 条主链路（plugin-development 铁律：每插件 1 冒烟 e2e）。
 */
const manifest = JSON.parse(
  readFileSync(fileURLToPath(new URL('../../../../Plugins/SmartTerminal/plugin.json', import.meta.url)), 'utf-8'),
)

const ROUTE = manifest.frontend?.route ?? '/smart-terminal'
const ENTRY_RE = /\/plugins\/smart-terminal\/web\/dist\/index\.js(\?|\s|$)/

test.describe('smart-terminal smoke', () => {
  test('route loads plugin view with title', async ({ page }) => {
    const errors: string[] = []
    page.on('pageerror', (e) => errors.push(String(e)))

    await page.goto('/')
    await page.waitForResponse((r) => r.url().includes('/api/plugin/frontend-manifest') && r.ok(), { timeout: 30000 }).catch(() => null)
    await page.goto(ROUTE)
    await page.waitForLoadState('networkidle')

    // 入口脚本被拉到（若插件已启用且 dist 存在）
    const entryHit = await page.evaluate((reSource) => {
      const re = new RegExp(reSource)
      return [...document.querySelectorAll('script')].some((s) => re.test(s.src))
    }, ENTRY_RE.source).catch(() => false)

    const title = page.locator('.view-title')
    await expect(title).toContainText(/智能终端/, { timeout: 20000 })
    expect(errors.filter((e) => !e.includes('favicon')).length, errors.join('\n')).toBe(0)
    // entryHit 在未启用插件时可能为 false；标题断言是主判据
    void entryHit
  })
})
