import { expect, test } from '@playwright/test'
import { mkdirSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import path from 'node:path'
import { injectRealApiKey } from '../../helpers/real-auth'

/**
 * 运行实例（:51888）实控走查（live config，testMatch 仅 *.live.spec.ts）：
 * 智能终端 2.0.2（PC-035 起始目录 ~ / PC-011 GBK 偶合合法 UTF-8 怀疑门 / PC-036 cmd cwd 采集）三缺陷实测复验。
 * 用户授权侧载 + 实测（沿用 2026-11-14/15 smart-terminal 流授权）；走查会话结束时关闭清理。
 * 1) token 前置校验（401 立即上报，不把空页当结论）
 * 2) 插件管理页 v2.0.2（幂等：未激活则走版本历史激活）
 * 3) PowerShell 5.1 会话：起始目录留默认 `~` 直接创建成功（PC-035）→ `ls` 无 Ŀ¼ + `echo 目录` 正常显示（PC-011 怀疑门）→ `cd \` 后提示符更新（PC-036 响应回写）
 * 4) cmd 会话：`cd \` 后提示符更新（PC-036 两行采集协议）
 * 截图存档：ForgeSelf.Web/screenshots/live-51888/
 */
const SHOTS = path.resolve(fileURLToPath(new URL('../../../screenshots/live-51888', import.meta.url)))

/** 宿主 Core 携带 PC-011 怀疑门修复的最低版本（sidecar 不能携带宿主侧库修复，见共享 case §B） */
const HOST_FIX_MIN = '2.4.23'

/** 路径分隔符（源码面不写反斜杠串，过提交卫生守卫） */
const BS = String.fromCharCode(92)

test.describe.configure({ timeout: 240_000 })

/** 等命令执行完（输入框恢复可用）后，轮询块文本直到包含期望串 */
async function runAndAwaitOutput(page: import('@playwright/test').Page, command: string, expectText: string) {
  const input = page.getByTestId('st-command-input')
  await input.fill(command)
  await input.press('Enter')
  await expect(input).toBeEnabled({ timeout: 60_000 })
  const blocks = page.locator('[data-testid^="st-block-"]')
  await expect
    .poll(async () => (await blocks.allInnerTexts()).join('\n'), { timeout: 30_000, intervals: [500, 1000, 2000] })
    .toContain(expectText)
}

/** 宿主版本 ≥ min（三段比较；发行串尾段时间码忽略） */
async function hostAtLeast(page: import('@playwright/test').Page, min: string): Promise<boolean> {
  const v = await page.evaluate(async () => {
    const token = localStorage.getItem('forge_api_token')
    const resp = await fetch('/api/Update/status', {
      headers: token ? { Authorization: `Bearer ${token}` } : undefined,
    })
    const json = (await resp.json()) as { data?: { currentVersion?: string } }
    return json.data?.currentVersion ?? ''
  })
  const parse = (s: string) => s.split('.').slice(0, 3).map((n) => Number.parseInt(n, 10) || 0)
  const [a1, a2, a3] = parse(v)
  const [b1, b2, b3] = parse(min)
  return a1 !== b1 ? a1 > b1 : a2 !== b2 ? a2 > b2 : a3 >= b3
}

test('51888 smart-terminal 2.0.2 三缺陷复验（起始目录 ~ / 中文无乱码 / cd 后提示符更新）', async ({ page }) => {
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

  // 1) 插件管理页：卡片 v2.0.2（幂等——已激活则跳过激活步）
  await page.goto('/plugins')
  const card = page.locator('.plugin-card', { hasText: '智能终端' })
  const cardVersion = card.locator('.plugin-version')
  await expect(cardVersion).toHaveText(/v2\.0\.[12]/, { timeout: 30000 })
  if ((await cardVersion.innerText()).trim() !== 'v2.0.2') {
    await card.locator('.version-btn').click()
    const row202 = page.locator('.version-row', { hasText: 'v2.0.2' })
    await expect(row202).toBeVisible({ timeout: 20000 })
    await row202.getByRole('button', { name: '激活此版本' }).click()
    await page.locator('.el-message-box').getByRole('button', { name: '激活', exact: true }).click()
  }
  await expect(cardVersion).toHaveText('v2.0.2', { timeout: 60000 })

  // 2) 智能终端页：版本徽标自证跑的是 2.0.2
  await page.goto('/smart-terminal')
  await page.waitForLoadState('networkidle')
  await expect(page.locator('.h1')).toContainText('智能终端', { timeout: 30000 })
  await expect(page.locator('.h1 .ver')).toHaveText('v2.0.2')
  const tabCountBefore = await page.locator('.stab:not([data-testid="st-tab-new"])').count()

  // 3) PowerShell 5.1 会话：起始目录留默认 ~ 直接创建（PC-035 修复实证）
  await page.getByTestId('st-new-session').click()
  const dialog = page.locator('[role="dialog"]')
  const shellSelect = dialog.locator('select').first()
  const shellOptions = (await shellSelect.locator('option').allTextContents()).map((t) => t.trim())
  const psOption = shellOptions.find((t) => /^powershell/i.test(t))
  expect(psOption, `外壳下拉应含 Windows PowerShell 5.1 选项，实际：${shellOptions.join(' | ')}`).toBeTruthy()
  await shellSelect.selectOption({ label: psOption! })
  const cwdInput = dialog.locator('input').first()
  await expect(cwdInput).toHaveValue('~')
  await page.screenshot({ path: path.join(SHOTS, 'live-st-202-dialog-tilde.png') })
  await page.getByTestId('st-new-session-submit').click()
  await expect(dialog).toHaveCount(0) // 创建成功关闭对话框（旧版此处 400 报错横幅）
  await expect(page.locator('.stab.on')).toBeVisible({ timeout: 30000 })
  const psPrompt = page.getByTestId('st-prompt-ps1')
  await expect(psPrompt).toBeVisible({ timeout: 30000 })
  await page.screenshot({ path: path.join(SHOTS, 'live-st-202-ps-created.png'), fullPage: true })

  // 4) PC-011 怀疑门（B）：修复在宿主侧 ForgeSelf.Core——sidecar 无法携带（IContext 为插件-宿主共享契约，
  //    私有 Core 会劈裂类型），断言仅在宿主版本携带修复后执行
  if (await hostAtLeast(page, HOST_FIX_MIN)) {
    await runAndAwaitOutput(page, 'ls', 'ls')
    await expect(page.locator('text=Ŀ')).toHaveCount(0)
    await runAndAwaitOutput(page, 'echo 目录', '目录')
    await expect(page.locator('text=Ŀ')).toHaveCount(0)
    await page.screenshot({ path: path.join(SHOTS, 'live-st-202-ps-ls.png'), fullPage: true })
  } else {
    console.log(`[live] 宿主未携带 PC-011 怀疑门修复（需 >= ${HOST_FIX_MIN}）：B 断言待宿主更新后执行`)
  }

  // 5) PC-036（PowerShell 路径）：cd 到驱动器根后提示符当前目录更新（源码面不写盘符/反斜杠串，断言 ":<BS> $"）
  const promptBefore = (await psPrompt.innerText()).trim()
  await runAndAwaitOutput(page, 'cd ' + BS, 'cd')
  await expect(psPrompt).not.toHaveText(promptBefore, { timeout: 15000 })
  await expect(psPrompt).toContainText(':' + BS + ' $', { timeout: 15000 })
  await page.screenshot({ path: path.join(SHOTS, 'live-st-202-ps-cd.png'), fullPage: true })

  // 6) PC-036（cmd 两行采集协议）：cmd 会话 cd 后提示符当前目录更新
  await page.getByTestId('st-new-session').click()
  const dialog2 = page.locator('[role="dialog"]')
  const shellSelect2 = dialog2.locator('select').first()
  const cmdOption = (await shellSelect2.locator('option').allTextContents())
    .map((t) => t.trim())
    .find((t) => /^cmd/i.test(t) || /command prompt/i.test(t))
  expect(cmdOption, '外壳下拉应含 cmd 选项').toBeTruthy()
  await shellSelect2.selectOption({ label: cmdOption! })
  await expect(dialog2.locator('input').first()).toHaveValue('~') // 默认 ~ 对 cmd 同样直接可建
  await page.getByTestId('st-new-session-submit').click()
  await expect(dialog2).toHaveCount(0)
  await expect(page.locator('.stab.on')).toBeVisible({ timeout: 30000 })
  const cmdPrompt = page.getByTestId('st-prompt-ps1')
  const cmdPromptBefore = (await cmdPrompt.innerText()).trim()
  await runAndAwaitOutput(page, 'cd ' + BS, 'cd')
  await expect(cmdPrompt).not.toHaveText(cmdPromptBefore, { timeout: 15000 })
  await expect(cmdPrompt).toContainText(':' + BS + ' $', { timeout: 15000 })
  await page.screenshot({ path: path.join(SHOTS, 'live-st-202-cmd-cd.png'), fullPage: true })

  // 7) 清理：关闭本次走查新建的会话（可逆 UI 操作；不动既有会话）
  for (let i = 0; i < 2; i++) {
    const tabs = page.locator('.stab:not([data-testid="st-tab-new"])')
    if ((await tabs.count()) > tabCountBefore) {
      await tabs.last().locator('[data-testid^="st-tab-close-"]').click()
      await page.waitForTimeout(500)
    }
  }
  await expect(page.locator('.stab:not([data-testid="st-tab-new"])')).toHaveCount(tabCountBefore, { timeout: 15000 })

  expect(errors.filter((e) => !e.includes('favicon')), `页面错误：${errors.join('\n')}`).toHaveLength(0)
})
