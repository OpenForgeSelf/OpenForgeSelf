import { expect, test } from '../../fixtures/e2e'
import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { getRealApiKey } from '../../helpers/real-auth'

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

  // 死会话恢复（宿主重启后 S-R 恢复的会话 id 服务端已不存在）：
  // 首个 404 → 停轮询 + banner 一次 + 标签置灰 ✕；此后 404 计数零新增。
  test('dead session restored: tab marked dead, polling stops with zero new 404s', async ({ page }) => {
    const deadSessionId = 'e2e-dead-sess'
    const snapshot = {
      version: 1,
      savedAt: new Date().toISOString(),
      activeId: deadSessionId,
      tabs: [
        {
          sessionId: deadSessionId,
          title: '死会话',
          shellId: 'pwsh',
          shellDisplayName: 'PowerShell',
          cwd: 'C:\\',
          permissionMode: 'AskWrite',
          status: 'idle',
        },
      ],
      chunksBySession: { [deadSessionId]: [{ index: 0, stream: 'stdout', text: 'legacy output' }] },
      commandsBySession: {},
      nextIndexBySession: { [deadSessionId]: 1 },
    }
    await page.addInitScript((snap) => {
      sessionStorage.setItem('forge.self.smart-terminal.session-restore.v1', JSON.stringify(snap))
    }, snapshot)

    let notFoundCount = 0
    page.on('response', (r) => {
      if (r.status() === 404 && r.url().includes('/api/smart-terminal/')) notFoundCount += 1
    })

    await page.goto('/')
    await page.waitForResponse((r) => r.url().includes('/api/plugin/frontend-manifest') && r.ok(), { timeout: 30000 }).catch(() => null)
    await page.goto(ROUTE)
    await page.waitForLoadState('networkidle')

    const tab = page.locator(`[data-testid="tab-bar"] [data-session-id="${deadSessionId}"]`)
    await expect(tab).toBeVisible()
    await expect(page.getByTestId('banner-dead')).toBeVisible()
    await expect(tab).toHaveClass(/dead/)
    await expect(tab.locator('.tab-title')).toContainText('✕')

    // 视觉证据：死标签置灰 ✕ + 失效 banner（e2e-testing 技能：截图读图常规化）
    await page.screenshot({ path: 'screenshots/e2e/smart-terminal/dead-session.png', fullPage: true })

    const settled = notFoundCount
    await page.waitForTimeout(3000)
    expect(notFoundCount, '修复前此断言红：轮询持续 404；修复后停止轮询').toBe(settled)
  })
})

/**
 * 主链冒烟（2026-11-05 立，e2e-testing 技能「冒烟必须触达执行链」条款）：
 * 建会话(POST) → 执行命令(POST run) → 断言 stdout 与 exitCode——真实执行链，
 * 页面加载型断言不算冒烟（今夜 PermissionMode 枚举 400 / 执行链 P0 / 持久化 P1 三缺陷皆因此漏过）。
 */
test.describe.configure({ timeout: 120_000 })

test('main chain: create session → run echo → assert stdout/exit', async ({ page, request }) => {
  const marker = `e2e-st-${Date.now()}`
  const auth = { Authorization: `Bearer ${getRealApiKey()}` }
  // 统一读响应：非 2xx/空体抛「状态码 + 原文」，禁止裸 res.json()（401/400 不得伪装成 JSON 解析错）
  const readJson = async <T>(resp: { status: () => number; text: () => Promise<string> }): Promise<T> => {
    const status = resp.status()
    const text = await resp.text()
    if (status < 200 || status >= 300) throw new Error(`HTTP ${status}: ${text.slice(0, 400)}`)
    if (!text) throw new Error(`HTTP ${status}: empty body`)
    return JSON.parse(text) as T
  }

  // ① 建会话（permissionMode 以字符串名交换——昨夜 STJ 只收数字致 400 的回归锚）
  const created = await readJson<{ success: boolean; data: { sessionId: string } }>(
    await request.post('/api/smart-terminal/sessions', {
      headers: auth,
      data: { title: marker, permissionMode: 'AskWrite' },
      timeout: 30_000,
    }),
  )
  expect(created.success, JSON.stringify(created)).toBe(true)
  const sessionId = created.data?.sessionId
  expect(sessionId, `sessionId 非空: ${JSON.stringify(created).slice(0, 200)}`).toBeTruthy()

  // ② 执行命令（echo=Read 级，AskWrite 下自动放行）
  const run = await readJson<{ success: boolean; data: { commandId: string; status: string } }>(
    await request.post(`/api/smart-terminal/sessions/${sessionId}/run`, {
      headers: auth,
      data: { command: `echo ${marker}`, autoApproveIfAllowed: true },
      timeout: 30_000,
    }),
  )
  expect(run.success, JSON.stringify(run)).toBe(true)
  expect(run.data?.commandId, `commandId 非空: ${JSON.stringify(run).slice(0, 200)}`).toBeTruthy()

  // ③ 轮询输出直到 marker 出现（进程异步回填）
  let stdoutHit = false
  const deadline = Date.now() + 20_000
  while (Date.now() < deadline && !stdoutHit) {
    await page.waitForTimeout(800)
    const snap = await readJson<{ data: { chunks: { stream: string; text: string }[] } }>(
      await request.get(`/api/smart-terminal/sessions/${sessionId}/output`, { headers: auth, timeout: 15_000 }),
    )
    stdoutHit = (snap.data?.chunks ?? []).some((c) => c.text.includes(marker))
  }
  expect(stdoutHit, `20s 内未见 stdout 含 ${marker}`).toBe(true)

  // ④ 命令记录：exitCode=0 且 completed（exit 码真源，防「跑了但不知道成败」）
  const cmds = await readJson<{ data: { commandRedacted: string; status: string; exitCode: number | null }[] }>(
    await request.get(`/api/smart-terminal/sessions/${sessionId}/commands`, { headers: auth, timeout: 15_000 }),
  )
  const rec = (cmds.data ?? []).find((c) => c.commandRedacted.includes(marker))
  expect(rec, `命令记录中应有 ${marker}: ${JSON.stringify(cmds.data).slice(0, 300)}`).toBeTruthy()
  expect(rec!.status, JSON.stringify(rec)).toBe('completed')
  expect(rec!.exitCode, JSON.stringify(rec)).toBe(0)
})
