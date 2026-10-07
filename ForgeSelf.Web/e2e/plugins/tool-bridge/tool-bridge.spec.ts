import type { Page } from '@playwright/test'
import { test, expect } from '../../fixtures/e2e'

/**
 * 工具桥插件（ToolBridge，id=tool-bridge）插件层 e2e —— 零 mock，走 globalSetup 全新宿主（真实前后端）。
 * 判据归口 PILOT-053 02-spec：AC14（完整一轮）/ AC12（匿名 401）/ AC15（版本徽标、空态、点即保存）/ BC-10（窄屏不裁字）。
 *
 * 覆盖：
 *  1. 页面装配：视图标题、版本徽标、工作根读数、初始指令含四个工具名、未粘贴时的空态引导；
 *  2. 完整一轮：粘贴混合 AI 风格文本（json 围栏 4 条 + 一句散文）→「解析并执行」→
 *     识别 4 / 执行 3 / 被拒 1 / 未知 1 / 未解析 1，结果区出现真实 stdout 与写后读回的内容原文，
 *     被拒与未解析条目也在回粘文本里（FR-4.3），台账落一条并可回看；
 *  3. 鉴权：无 token 直连 `/api/tool-bridge/prompt` 必须 401（铁律 17）；
 *  4. 视觉：1280×720 无横向溢出，且每张卡片完整容纳自身内容（铁律 8 的 flex 压扁回归守卫）。
 *
 * 运行：pnpm exec playwright test --config=playwright.config.ts e2e/plugins/tool-bridge
 * 环境前置（AGENTS.md §5.0）：NO_PROXY=localhost,127.0.0.1,::1；TEMP/TMP 指进仓库 .temp/tmp。
 */

const RUN_FILE = 'e2e-run.md'
const RUN_CONTENT = '# 工具桥 e2e 第二行'

/** 一段"像 AI 回复"的混合文本：json 围栏 5 条调用（其中一条清单外工具名、一条会被守卫拒）+ 两句散文。 */
const AI_STYLE_TEXT = [
  '我先写文件，再读回来确认，然后跑一条命令看看环境。',
  '```json',
  '{ "tool_calls": [',
  '  { "id": "1", "function": { "name": "write_file", "arguments": { "path": "' + RUN_FILE + '", "content": "' + RUN_CONTENT + '\\n追加一行" } } },',
  '  { "id": "2", "function": { "name": "read_file", "arguments": { "path": "' + RUN_FILE + '" } } },',
  '  { "id": "3", "function": { "name": "run_command", "arguments": { "command": "git --version" } } },',
  '  { "id": "4", "function": { "name": "run_command", "arguments": { "command": "rm -f important.txt" } } },',
  '  { "id": "5", "function": { "name": "delete_file", "arguments": { "path": "' + RUN_FILE + '" } } }',
  '] }',
  '```',
  '接下来我打算 write_file 一下别的文件。',
  '另外顺手列一下目录。',
].join('\n')

async function gotoToolBridge(page: Page) {
  await page.goto('/tool-bridge')
  for (let i = 0; i < 3; i++) {
    const err = page.locator('.plugin-view-state--error')
    if (await err.isVisible().catch(() => false)) {
      const retry = page.locator('.plugin-view-state--error button, .retry-btn')
      if (await retry.count().catch(() => 0)) {
        await retry.first().click()
        await page.waitForTimeout(800)
        continue
      }
    }
    break
  }
  await expect(page.getByTestId('tb-paste')).toBeVisible({ timeout: 20000 })
  // 远程加载 + 四个并发请求：必须等数据真的回来了再操作，否则测的是"首屏空壳"
  // （也正因为这条，页面装配早于 loadAll 完成时曾把用户已填的工作根输入框覆盖掉——已修）
  await expect(page.locator('[data-testid=tb-workspace] code')).not.toHaveText('加载中…', { timeout: 20000 })
  await expect(page.locator('.tb-version')).not.toHaveText(/加载中/)
}

test.describe('工具桥插件（ToolBridge）实跑宿主 e2e', () => {
  test('页面装配：标题 / 版本徽标 / 工作根 / 初始指令 / 未粘贴空态', async ({ page }) => {
    const serverErrors: string[] = []
    page.on('response', r => { if (r.status() >= 500) serverErrors.push(`${r.status()} ${r.url()}`) })

    await gotoToolBridge(page)

    await expect(page.locator('.view-title')).toHaveText('工具桥')
    // 铁律 13：版本徽标必须显示插件自己的版本（来自宿主 /api/plugin），不能停在"加载中/未知版本"
    await expect(page.locator('.tb-version')).toHaveText(/^v(?!\s*$)\S.*$/)

    // 工作根读数来自真实后端
    const root = page.locator('[data-testid=tb-workspace] code')
    await expect(root).toBeVisible()
    await expect(root).toContainText('tool-bridge')

    // 初始指令与后端 ToolSpec 同源：四个工具名逐个必须在文本里
    const prompt = page.locator('#tb-prompt-text')
    await expect(prompt).toBeVisible()
    const promptText = await prompt.innerText()
    for (const tool of ['read_file', 'write_file', 'list_dir', 'run_command']) {
      expect(promptText, `初始指令缺工具 ${tool}`).toContain(tool)
    }
    // FR-1.3：初始指令会被粘进外部站点，绝不能带本机绝对路径
    expect(promptText).not.toMatch(/[A-Za-z]:[\\/]/)

    // §3.4 之 4：空态分级——还没粘贴时给的是引导而不是"暂无数据"
    await expect(page.getByTestId('tb-empty')).toHaveText(/粘贴/)

    expect(serverErrors, `服务端 5xx: ${serverErrors.join(' | ')}`).toHaveLength(0)
  })

  test('完整一轮：解析 → 真执行 → 结果原样可复制 → 台账可回看（AC14）', async ({ page }) => {
    await gotoToolBridge(page)

    await page.getByTestId('tb-paste-input').fill(AI_STYLE_TEXT)
    await page.getByTestId('tb-turn').click()

    const stats = page.getByTestId('tb-stats').first()
    await expect(stats).toContainText('识别 4', { timeout: 40000 })
    await expect(stats).toContainText('执行 3')
    await expect(stats).toContainText('被拒 1')
    // 未知 1：delete_file 是清单外工具名，进 unknown 而不是被"就近执行"
    // 未解析 2：两句散文各成一块——散文不是调用，插件绝不推断意图（BC-1）
    await expect(stats).toContainText('未知 1')
    await expect(stats).toContainText('未解析 2')

    // 回粘文本必须带真实观察数据：命令 stdout 原文 + 写后读回的内容原文
    const resultText = await page.getByTestId('tb-result-text').innerText()
    expect(resultText).toContain('[tool-bridge-result]')
    expect(resultText).toContain('git version')
    expect(resultText).toContain('# 工具桥 e2e')
    // FR-4.3：被拒与未解析也必须在这段文本里，否则 AI 只会看到"什么都没有"
    expect(resultText).toContain('command_rejected')
    expect(resultText).toContain('delete_file')
    // json 模式用英文键名（AI 读得懂、可解析），中文原因在 reason 字段里
    expect(resultText).toContain('"unrecognized"')
    expect(resultText).toContain('"unparsed"')
    expect(resultText).toContain('疑似工具名')
    // 越界防护的另一面：回粘文本里不得出现本机绝对路径
    expect(resultText).not.toMatch(/[A-Za-z]:[\\/]/)

    // 真写盘证据：界面显示的是后端真实执行结果，不是前端拼的样子货
    await expect(page.locator('.tb-item__head').filter({ hasText: 'write_file 已执行' })).toBeVisible()

    // 切换 plain 模式后仍是原文（stdout 段不被 JSON 转义）
    await page.getByTestId('tb-mode-plain').click()
    const plain = await page.getByTestId('tb-result-text').innerText()
    expect(plain).toContain('mode=plain')
    expect(plain).toContain('---- stdout ----')
    expect(plain).toContain('git version')

    // 台账：本轮已落一条，点击可回看（载入原文，不自动重跑）
    const turnRow = page.getByTestId('tb-turn-row').first()
    await expect(turnRow).toBeVisible()
    await turnRow.click()
    await expect(page.getByTestId('tb-hint')).toContainText('已载入回合')
    await expect(page.getByTestId('tb-paste-input')).toHaveValue(/tool_calls/)
  })

  test('只解析 → 单独执行已解析的调用：不落台账（POST execute 的真实消费者）', async ({ page }) => {
    await gotoToolBridge(page)

    await page.getByTestId('tb-paste-input').fill(AI_STYLE_TEXT)
    await page.getByTestId('tb-parse').click()
    await expect(page.getByTestId('tb-stats').first()).toContainText('识别 4', { timeout: 20000 })
    // 只解析不得产生任何结果区（零副作用的可观测面）
    await expect(page.getByTestId('tb-result')).toHaveCount(0)
    const turnsBefore = await page.getByTestId('tb-turn-row').count()

    await page.getByTestId('tb-execute').click()
    await expect(page.getByTestId('tb-result')).toBeVisible({ timeout: 30000 })
    const text = await page.getByTestId('tb-result-text').innerText()
    expect(text).toContain('git version')
    expect(text).toContain('command_rejected')
    await expect(page.getByTestId('tb-hint')).toContainText('未落台账')

    const turnsAfter = await page.getByTestId('tb-turn-row').count()
    expect(turnsAfter, 'execute 不产生回合记录；要留档必须走 turn').toBe(turnsBefore)
  })

  // 2026-10-07 用户要求：第二部分（识别结果）参照第三部分做展开/收起，默认收起。
  // 判据不能只看"点了有反应"，要钉住三件事：默认收起、点开展开的内容正确、再点收起。
  test('第二部分识别结果默认收起，点头部展开参数、再点收起', async ({ page }) => {
    await gotoToolBridge(page)

    await page.getByTestId('tb-paste-input').fill(AI_STYLE_TEXT)
    await page.getByTestId('tb-parse').click()
    await expect(page.getByTestId('tb-stats').first()).toContainText('识别 4', { timeout: 20000 })

    // ① 默认收起：头部可见，正文一律不存在（不是"隐藏"，是未渲染）
    const head0 = page.getByTestId('tb-call-head-0')
    await expect(head0).toBeVisible()
    await expect(page.getByTestId('tb-call-body-0')).toHaveCount(0)
    await expect(page.getByTestId('tb-unknown-body-0')).toHaveCount(0)
    await expect(page.getByTestId('tb-unparsed-body-0')).toHaveCount(0)
    // 收起态也必须自带信息量：头部要能看出这条带几个参数
    await expect(head0).toContainText(/个参数 · 点展开/)

    // ② 点开：正文出现，内容是真解析出来的参数（不是占位文本）
    await head0.click()
    const body0 = page.getByTestId('tb-call-body-0')
    await expect(body0).toBeVisible()
    await expect(body0).toContainText(RUN_FILE)
    await expect(head0).toContainText('收起')

    // ③ 再点收起；另一条独立展开（折叠态按条目各自记，不能一荣俱荣）
    await head0.click()
    await expect(page.getByTestId('tb-call-body-0')).toHaveCount(0)
    await page.getByTestId('tb-call-head-1').click()
    await expect(page.getByTestId('tb-call-body-1')).toBeVisible()
    await expect(page.getByTestId('tb-call-body-0')).toHaveCount(0)

    // ④ 成因不收起：未识别条目把 reason 露在外面（否则用户只能猜为什么少了一条）
    await expect(page.getByTestId('tb-unparsed-head-0')).toBeVisible()
  })

  test('工作根点即保存：非法值被拒并保留原值，危险根须确认后生效（AC15 / BC-11）', async ({ page }) => {
    await gotoToolBridge(page)

    const input = page.getByTestId('tb-workspace-input')
    const rootLabel = page.locator('[data-testid=tb-workspace] code')
    const before = await rootLabel.innerText()
    const save = page.getByTestId('tb-workspace-save')

    // ① 相对路径必须被拒（实测缺陷回归：后端 GetFullPath 会把相对路径补成"当前目录下的绝对路径"并保存成功）
    await input.fill('relative/should-be-rejected')
    await Promise.all([
      page.waitForResponse(r => r.url().includes('/api/tool-bridge/workspace') && r.request().method() === 'PUT'),
      save.click(),
    ])
    expect(await input.inputValue(), 'fill 之后输入框必须真的是相对路径，否则下面断言无意义')
      .toBe('relative/should-be-rejected')
    await expect(page.getByTestId('tb-error')).toContainText('绝对路径')
    await expect(rootLabel).toHaveText(before)
    await page.getByRole('button', { name: '知道了' }).click()

    // ② BC-11：e2e 宿主的数据根就在这台仓库树内，切到它的兄弟目录属"危险根"，未确认必须拒
    const target = `${before.replace(/[\\/]+$/, '')}-e2e`
    await input.fill(target)
    await Promise.all([
      page.waitForResponse(r => r.url().includes('/api/tool-bridge/workspace') && r.request().method() === 'PUT'),
      save.click(),
    ])
    await expect(page.getByTestId('tb-error')).toContainText('危险工作根')
    await expect(rootLabel).toHaveText(before)
    await page.getByRole('button', { name: '知道了' }).click()

    // ③ 勾选确认后点即保存，并回读一致（界面状态 = 持久化状态）
    await page.getByText('我确认使用危险根').click()
    await Promise.all([
      page.waitForResponse(r => r.url().includes('/api/tool-bridge/workspace') && r.request().method() === 'PUT'),
      save.click(),
    ])
    await expect(rootLabel).toHaveText(target)
    await expect(page.getByTestId('tb-hint')).toContainText('回读一致')

    // ④ 复原默认根，避免把这台实例的工作根留在测试目录（只改指针，不删任何数据）
    await input.fill(before)
    await Promise.all([
      page.waitForResponse(r => r.url().includes('/api/tool-bridge/workspace') && r.request().method() === 'PUT'),
      save.click(),
    ])
    await expect(rootLabel).toHaveText(before)
  })

  test('鉴权：无 token 访问管理面必须 401（AC12 / 铁律 17）', async ({ request }) => {
    for (const path of ['/api/tool-bridge/prompt', '/api/tool-bridge/workspace', '/api/tool-bridge/turns']) {
      const resp = await request.get(path)
      expect(resp.status(), `${path} 未带令牌应 401`).toBe(401)
    }
    const post = await request.post('/api/tool-bridge/turn', { data: { text: 'read_file path=x.txt' } })
    expect(post.status(), '匿名执行端点必须 401').toBe(401)
  })

  test('视觉：1280×720 无横向溢出，卡片完整容纳自身内容（BC-10 / 铁律 8）', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 720 })
    await gotoToolBridge(page)

    await page.getByTestId('tb-paste-input').fill(AI_STYLE_TEXT)
    await page.getByTestId('tb-turn').click()
    await expect(page.getByTestId('tb-stats').first()).toContainText('识别 4', { timeout: 40000 })

    // 铁律 8 守卫：滚动容器的子区块若被 flex 压扁，内容高度会超出盒子（文字被裁）
    const cards = page.locator('.tb-card')
    const count = await cards.count()
    expect(count).toBeGreaterThanOrEqual(4)
    for (let i = 0; i < count; i++) {
      const card = cards.nth(i)
      const box = await card.boundingBox()
      const inner = await card.evaluate(el => (el as HTMLElement).scrollHeight)
      expect(box, `第 ${i + 1} 张卡片应可见`).toBeTruthy()
      expect(
        inner,
        `第 ${i + 1} 张卡片的高度应容纳其内容（scrollHeight ${inner} vs 盒子高 ${box!.height}）`,
      ).toBeLessThanOrEqual(box!.height + 2)
    }

    const overflow = await page.evaluate(
      () => document.documentElement.scrollWidth > document.documentElement.clientWidth + 1,
    )
    expect(overflow, '不应出现横向滚动条').toBe(false)

    await page.screenshot({ path: 'screenshots/e2e/tool-bridge/alignment.png', fullPage: true })
  })
})
