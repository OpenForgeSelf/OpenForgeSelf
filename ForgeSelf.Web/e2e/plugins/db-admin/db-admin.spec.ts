import { mkdirSync, rmSync, writeFileSync } from 'node:fs'
import path from 'node:path'
import type { Locator, Page } from '@playwright/test'
import { test, expect } from '../../fixtures/e2e'
import { getRealApiKey } from '../../helpers/real-auth'
import { E2E_TEMP_ROOT, backendUrl } from '../../helpers/e2e-env'

/**
 * DbAdmin 插件 e2e —— 恰 1 条冒烟主链路（e2e-testing 技能：禁止铺全流程 e2e）。
 *
 * 主链路（真实 publish 宿主 + 真实 API 密钥，零 mock）：
 *   建 SQLite 测试连接（库文件预建 0 字节 = 合法空库，落在 worktree .temp/e2e-db/；
 *   DbAdmin 不自动建库文件，见 DbConnectionService.EnsureSqliteFileExists）
 *   → 跑 CREATE TABLE（AskWrite → 审批弹窗 → 批准，非破坏性允许真执行）
 *   → schema 树见新表 → SELECT 见结果格
 *   → 跑 INSERT（审批弹窗 → 拒绝，语句绝不执行，再查证行数不变）。
 * 破坏性 SQL 全程不出现；拒绝路径即本用例的写安全验收。
 *
 * 运行：pnpm exec playwright test --config=playwright.config.ts e2e/plugins/db-admin
 * 截图：ForgeSelf.Web/screenshots/e2e/db-admin/
 */

const tid = (page: Page, id: string) => page.locator(`[data-testid="${id}"]`)
const SHOT_DIR = 'screenshots/e2e/db-admin'

/**
 * 实测元素的文字/背景 WCAG 对比度（PC-025 实例级守卫）。
 * 必须真取 computedStyle，不靠截图目测；背景半透明时直接报错，避免算错。
 */
async function measureContrast(locator: Locator): Promise<{ color: string; background: string; ratio: number }> {
  return locator.evaluate((el) => {
    const style = getComputedStyle(el)
    const color = style.color
    const background = style.backgroundColor
    const parse = (value: string) => {
      const match = value.match(/rgba?\(([^)]+)\)/)
      if (!match) throw new Error(`无法解析颜色: ${value}`)
      const parts = match[1].split(',').map((part) => Number.parseFloat(part.trim()))
      return { r: parts[0], g: parts[1], b: parts[2], a: parts.length > 3 ? parts[3] : 1 }
    }
    const fg = parse(color)
    const bg = parse(background)
    if (bg.a < 1) throw new Error(`背景为半透明，无法直接计算对比度: ${background}`)
    const luminance = (c: { r: number; g: number; b: number }) => {
      const [r, g, b] = [c.r, c.g, c.b].map((channel) => {
        const ratio = channel / 255
        return ratio <= 0.03928 ? ratio / 12.92 : ((ratio + 0.055) / 1.055) ** 2.4
      })
      return 0.2126 * r + 0.7152 * g + 0.0722 * b
    }
    const ratio = (Math.max(luminance(fg), luminance(bg)) + 0.05) / (Math.min(luminance(fg), luminance(bg)) + 0.05)
    return { color, background, ratio }
  })
}

test.describe('DbAdmin · 冒烟主链路', () => {
  test('建连→建表（批准）→schema→SELECT→INSERT（拒绝）', async ({ page }) => {
    test.setTimeout(180_000)
    // 主题固定为 light 作基线（system 在 CI/Playwright 默认解析为 light，显式写入避免环境差异）
    await page.addInitScript(() => localStorage.setItem('forgeself.theme.mode', 'light'))
    const stamp = Date.now()
    const connName = `E2E-DB-${stamp}`
    const dbDir = path.join(E2E_TEMP_ROOT, '..', 'e2e-db')
    mkdirSync(dbDir, { recursive: true })
    const dbFile = path.join(dbDir, `db-admin-smoke-${stamp}.db`)
    writeFileSync(dbFile, '') // 0 字节 = 合法空 SQLite 库

    // ① 进入 /db-admin：正常态（版本徽标 + 连接树 + 空引导其一）
    await page.goto('/db-admin')
    await expect(tid(page, 'db-admin-root')).toBeVisible()
    await expect(tid(page, 'version-badge')).toBeVisible({ timeout: 30_000 })
    await expect(tid(page, 'conn-tree')).toBeVisible()
    await page.screenshot({ path: `${SHOT_DIR}/01-loaded.png`, fullPage: true })

    // ② 新建连接：SQLite 表单 + 测试连接（恒 200，绿态）+ 保存
    await tid(page, 'new-connection').click()
    await expect(tid(page, 'conn-form')).toBeVisible()
    // 驱动预检关闭（2026-11-06 用户输入2）：provider 下拉不再标「需驱动」
    const providerOptions = await tid(page, 'cf-provider').locator('option').allTextContents()
    expect(providerOptions.join('|')).not.toContain('需驱动')
    await expect(tid(page, 'cf-needs-driver')).toHaveCount(0)
    await page.screenshot({ path: `${SHOT_DIR}/02-provider-no-driver-tag.png`, fullPage: true })
    await tid(page, 'cf-name').fill(connName)
    await tid(page, 'cf-provider').selectOption('sqlite')
    await tid(page, 'cf-path').fill(dbFile)
    await tid(page, 'cf-test').click()
    await expect(tid(page, 'test-result')).toContainText('成功', { timeout: 30_000 })
    await page.screenshot({ path: `${SHOT_DIR}/02-test-ok.png`, fullPage: true })
    await tid(page, 'cf-save').click()
    await expect(tid(page, 'conn-form')).toBeHidden({ timeout: 30_000 })
    const connItem = page.locator('.node.conn', { hasText: connName }).first() // 修：前缀撞 conn-tree 容器（hasText 匹配容器内子文本→first() 选容器→click 无效、selectConnection 从未触发）
    await expect(connItem).toBeVisible({ timeout: 30_000 })

    // ③ 选中连接，跑 CREATE TABLE（写语句 → AskWrite 审批弹窗 → 批准执行）
    // 运行态 loading 守卫：延迟一次真实 /tables 请求（不 mock 数据），确保局部 loading 能被观察到
    await page.route(/\/api\/db-admin\/connections\/[^/]+\/tables$/, async (route) => {
      await new Promise((resolve) => setTimeout(resolve, 800))
      await route.continue()
    }, { times: 1 })
    await connItem.click()
    await expect(page.locator('[data-testid^="tables-loading-"]')).toBeVisible({ timeout: 5_000 })
    await expect(page.locator('.node.column.muted', { hasText: '（无表）' })).toHaveCount(0)
    await expect(page.locator('[data-testid^="tables-loading-"]')).toHaveCount(0, { timeout: 30_000 })
    // PC-025 实例级守卫：选中行（primary-light-8 底）文字在 light/dark 两态都必须可读。
    // 默认主题只覆盖 light，必须显式切 html.dark 才测得到暗色（2026-11-06 暗色连接名 1.10:1 逃逸根因）。
    const lightContrast = await measureContrast(connItem)
    expect(
      lightContrast.ratio,
      `light 选中行对比度 ${lightContrast.ratio.toFixed(2)}:1（${lightContrast.color} on ${lightContrast.background}）`,
    ).toBeGreaterThanOrEqual(4.5)
    await page.screenshot({ path: `${SHOT_DIR}/03a-selected-light.png`, fullPage: true })
    await page.evaluate(() => document.documentElement.classList.add('dark'))
    const darkContrast = await measureContrast(connItem)
    expect(
      darkContrast.ratio,
      `dark 选中行对比度 ${darkContrast.ratio.toFixed(2)}:1（${darkContrast.color} on ${darkContrast.background}）`,
    ).toBeGreaterThanOrEqual(4.5)
    await page.screenshot({ path: `${SHOT_DIR}/03b-selected-dark.png`, fullPage: true })
    console.log(`[db-admin] 选中行对比度 light=${lightContrast.ratio.toFixed(2)}:1 dark=${darkContrast.ratio.toFixed(2)}:1`)
    await page.evaluate(() => document.documentElement.classList.remove('dark'))
    // 连接未配默认表 → 点连接只展开加载全部表，不自动开标签（AC-12）；显式新建空白查询后才有编辑器
    await expect(tid(page, 'workspace-empty')).toBeVisible()
    await tid(page, 'empty-new-query').click()
    const editor = tid(page, 'sql-editor')
    await expect(editor).toBeEnabled({ timeout: 30_000 })
    await editor.fill(`CREATE TABLE e2e_items (id INTEGER PRIMARY KEY, name TEXT)`)
    await tid(page, 'run').click()
    await expect(tid(page, 'approval-modal')).toBeVisible({ timeout: 30_000 })
    await expect(tid(page, 'approval-sql')).toContainText('CREATE TABLE')
    await page.screenshot({ path: `${SHOT_DIR}/03-approval-create.png`, fullPage: true })
    await tid(page, 'approval-approve').click()
    await expect(tid(page, 'approval-modal')).toBeHidden({ timeout: 30_000 })
    await expect(tid(page, 'result-stats')).toBeVisible({ timeout: 30_000 })

    // ④ schema 树即时刷新（R1 修复断言）：批准建表执行完成后，schema 树**不经整页刷新**即见新表
    // （resolveApproval finally 触发 loadTables(force)；修复前此处必须 page.reload() 才能见新表）
    const tableNode = page.locator(`[data-testid^="table-"]`, { hasText: 'e2e_items' }).first()
    try {
      await tableNode.waitFor({ state: 'visible', timeout: 8_000 })
    } catch {
      await connItem.click() // 连接节点若未展开，展开一次（数据层已是新表集合）
    }
    await expect(tableNode).toBeVisible({ timeout: 30_000 })
    // 表名直接显示（2026-11-06 输入3：不再加「表」前缀；视图仍保留「视图」标识）
    await expect(page.locator('[data-testid^="table-"] .name', { hasText: 'e2e_items' }).first()).toHaveText('e2e_items')
    await page.locator(`[data-testid^="table-"]`, { hasText: 'e2e_items' }).first().click()
    await expect(page.locator('[data-testid^="col-"]').first()).toBeVisible({ timeout: 30_000 })
    await page.screenshot({ path: `${SHOT_DIR}/04-schema.png`, fullPage: true })

    // ④.0 多标签工作区（FR-1~4 / AC-1~4 / AC-13）：点表 → 预填 SELECT * + 展开列；再点同表聚焦不新增、不重置
    await expect(tid(page, 'query-tabs')).toBeVisible()
    await expect(editor).toHaveValue('SELECT * FROM e2e_items')
    const tableTab = page.locator('.qtab', { hasText: 'e2e_items' }).first()
    const manualTab = page.locator('.qtab', { hasText: '新查询' }).first()
    const tabCount = page.locator('[data-testid^="qtab-"]')
    await expect(tabCount).toHaveCount(2)
    // 手写标签里改 SQL → 切到表标签再切回，内容不被覆盖
    await manualTab.click()
    await editor.fill('SELECT count(*) AS n FROM e2e_items')
    await page.locator(`[data-testid^="table-"]`, { hasText: 'e2e_items' }).first().click()
    await expect(tabCount).toHaveCount(2)
    await expect(editor).toHaveValue('SELECT * FROM e2e_items')
    await manualTab.click()
    await expect(editor).toHaveValue('SELECT count(*) AS n FROM e2e_items')
    await tableTab.click()
    await expect(editor).toHaveValue('SELECT * FROM e2e_items')
    await page.screenshot({ path: `${SHOT_DIR}/04b-query-tabs.png`, fullPage: true })
    // 关闭手写标签：仅剩表标签，编辑器回到表标签
    await manualTab.locator('[data-testid="close-tab"]').click()
    await expect(page.locator('.qtab', { hasText: '新查询' })).toHaveCount(0)
    await expect(editor).toHaveValue('SELECT * FROM e2e_items')

    // ④.1 表过滤（AC-21）：本地过滤表名；无匹配显示「无匹配表」（不发请求）
    await tid(page, 'table-filter').fill('e2e_items')
    await expect(page.locator(`[data-testid^="table-"]`, { hasText: 'e2e_items' }).first()).toBeVisible()
    await tid(page, 'table-filter').fill('no-such-table')
    await expect(tid(page, 'table-filter-empty')).toContainText('无匹配表')
    await tid(page, 'table-filter').fill('')
    // 同类修复（2026-11-06 oracle 批暴露）：前缀选择器会命中恒渲染的 table-filter 输入框（table-filter-empty 同理），
    // 收紧为表行节点 .node.table[data-testid^="table-"]
    await expect(page.locator('.node.table[data-testid^="table-"]').first()).toBeVisible()

    // ④.2 默认表（FR-8~10 / AC-9~12）：连接可配「打开即查询的表」，留空则点连接只展开加载全部表
    const connEditorEntry = connItem.locator('button[title="编辑连接"]')
    await connItem.hover()
    await connEditorEntry.click()
    await expect(tid(page, 'conn-form')).toBeVisible()
    await expect(tid(page, 'cf-default-table')).toHaveValue('')
    await tid(page, 'cf-default-table').fill('e2e_items')
    await tid(page, 'cf-save').click()
    await expect(tid(page, 'conn-form')).toBeHidden({ timeout: 30_000 })
    // 关掉全部标签 → 工作区空态
    for (let i = 0; i < 5; i += 1) {
      const closeBtn = page.locator('[data-testid="close-tab"]').first()
      if ((await closeBtn.count()) === 0) break
      await closeBtn.click()
    }
    await expect(tid(page, 'workspace-empty')).toBeVisible()
    // 连接当前是展开状态：先点一次收起，再点一次展开 → 展开动作才带出默认表标签（FR-7）
    await connItem.click()
    await expect(page.locator('.node.table[data-testid^="table-"]')).toHaveCount(0)
    await connItem.click()
    await expect(tid(page, 'query-tabs')).toBeVisible({ timeout: 30_000 })
    await expect(editor).toHaveValue('SELECT * FROM e2e_items')
    await page.screenshot({ path: `${SHOT_DIR}/04c-default-table.png`, fullPage: true })

    // ④.3 插入一行并批准：让后续 SELECT 有真实数据行，用于断言「表头与内容列对齐」
    await editor.fill("INSERT INTO e2e_items (id, name) VALUES (1, 'e2e_row')")
    await tid(page, 'run').click()
    await expect(tid(page, 'approval-modal')).toBeVisible({ timeout: 30_000 })
    await tid(page, 'approval-approve').click()
    await expect(tid(page, 'approval-modal')).toBeHidden({ timeout: 30_000 })
    // 等批准后的 INSERT 真正执行完成（避免 SELECT 抢跑拿到 0 行，列对齐断言无行可测）
    await expect(tid(page, 'result-stats')).toContainText('影响 1 行', { timeout: 30_000 })

    // ⑤ SELECT：结果格可见且有 1 行数据
    await editor.fill('SELECT * FROM e2e_items')
    await tid(page, 'run').click()
    await expect(tid(page, 'result-grid')).toBeVisible({ timeout: 30_000 })
    const resultTable = tid(page, 'result-grid').locator('table')
    await expect(resultTable).toBeVisible({ timeout: 30_000 })
    // 防 CSS 类名与宿主 Tailwind 工具类冲突（2026-11-06 输入3：.grid → display:grid 让表头/内容错列）
    expect(await resultTable.evaluate((el) => getComputedStyle(el).display)).toBe('table')
    // 表头与第一行数据逐列左边界对齐（误差 ≤1px）
    const colLeftDiffs = await resultTable.evaluate((el) => {
      const ths = [...el.querySelectorAll('thead th')]
      const tds = [...(el.querySelector('tbody tr')?.querySelectorAll('td') ?? [])]
      return ths.map((th, i) => Math.abs(th.getBoundingClientRect().left - (tds[i]?.getBoundingClientRect().left ?? 0)))
    })
    expect(Math.max(...colLeftDiffs)).toBeLessThanOrEqual(1)
    await page.screenshot({ path: `${SHOT_DIR}/05-select.png`, fullPage: true })

    // ⑤.1 多语句执行（FR-15~19 / AC-15~19）：两条语句 → 结果标签条，切换不串数据
    await editor.fill('SELECT count(*) AS n FROM e2e_items;\nSELECT * FROM e2e_items;')
    await tid(page, 'run').click()
    await expect(tid(page, 'result-tabs')).toBeVisible({ timeout: 30_000 })
    await expect(tid(page, 'result-tab-0')).toContainText('结果 1')
    await expect(tid(page, 'result-tab-1')).toContainText('结果 2')
    await expect(tid(page, 'result-tab-0')).toHaveClass(/on/)
    await expect(tid(page, 'result-grid')).not.toContainText('e2e_row')
    await tid(page, 'result-tab-1').click()
    await expect(tid(page, 'result-tab-1')).toHaveClass(/on/)
    await expect(tid(page, 'result-grid')).toContainText('e2e_row', { timeout: 30_000 })
    await page.screenshot({ path: `${SHOT_DIR}/05b-multi-result.png`, fullPage: true })

    // ⑤.2 选区执行（AC-14）：只跑选中的那条 → 单结果、无结果标签条
    await tid(page, 'sql-editor').evaluate((el) => {
      const ta = el as HTMLTextAreaElement
      const needle = 'SELECT * FROM e2e_items;'
      const start = ta.value.indexOf(needle)
      ta.focus()
      ta.setSelectionRange(start, start + needle.length)
      ta.dispatchEvent(new Event('select', { bubbles: true }))
    })
    await expect(tid(page, 'selection-hint')).toContainText('仅执行选中')
    await tid(page, 'run').click()
    await expect(tid(page, 'result-grid')).toContainText('e2e_row', { timeout: 30_000 })
    await expect(tid(page, 'result-tabs')).toHaveCount(0)

    // ⑤.3 结果工具（AC-22~24）：双击单元格看详情、导出当前页 CSV（文件名含页码）
    await tid(page, 'cell-0-1').dblclick()
    await expect(tid(page, 'cell-detail')).toBeVisible()
    await expect(tid(page, 'cell-detail-value')).toContainText('e2e_row')
    await page.keyboard.press('Escape')
    await expect(tid(page, 'cell-detail')).toHaveCount(0)
    const [download] = await Promise.all([page.waitForEvent('download'), tid(page, 'export-csv').click()])
    expect(download.suggestedFilename()).toMatch(/^e2e_items-r1-p1-\d{10}\.csv$/)
    await page.screenshot({ path: `${SHOT_DIR}/05c-result-tools.png`, fullPage: true })

    // ⑥ INSERT：审批弹窗 → 拒绝 → 语句不执行（查证无该行）
    await editor.fill(`INSERT INTO e2e_items (name) VALUES ('e2e_rejected_row')`)
    await tid(page, 'run').click()
    await expect(tid(page, 'approval-modal')).toBeVisible({ timeout: 30_000 })
    await page.screenshot({ path: `${SHOT_DIR}/06-approval-insert.png`, fullPage: true })
    await tid(page, 'approval-reject').click()
    await expect(tid(page, 'approval-modal')).toBeHidden({ timeout: 30_000 })
    // 拒绝路径 UI：顶部 warn 横幅（resolveApproval: banner = 已拒绝：语句未执行），非 result-error 红条
    await expect(tid(page, 'banner')).toContainText('已拒绝', { timeout: 30_000 })
    await editor.fill('SELECT * FROM e2e_items')
    await tid(page, 'run').click()
    await expect(tid(page, 'result-grid')).toBeVisible({ timeout: 30_000 })
    await expect(tid(page, 'result-grid')).not.toContainText('e2e_rejected_row')
    await page.screenshot({ path: `${SHOT_DIR}/07-rejected-clean.png`, fullPage: true })

    // ⑦ 清理：删本用例自建连接（仅删配置）；库文件尽力删除（宿主占用则留给 .temp 惰性清理）
    try {
      const res = await page.request.get(`${backendUrl()}/api/db-admin/connections`, {
        headers: { Authorization: `Bearer ${getRealApiKey()}` },
      })
      const body = (await res.json()) as { data?: { id: string; name: string }[] }
      const mine = (body.data ?? []).find((c) => c.name === connName)
      if (mine) {
        await page.request.delete(`${backendUrl()}/api/db-admin/connections/${mine.id}`, {
          headers: { Authorization: `Bearer ${getRealApiKey()}` },
        })
      }
    } catch { /* 清理尽力而为，失败不掩盖主链路结论 */ }
    try { rmSync(dbFile, { force: true }) } catch { /* 宿主连接占用时留 .temp */ }
  })
})
