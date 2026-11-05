import { mkdirSync, rmSync, writeFileSync } from 'node:fs'
import path from 'node:path'
import type { Page } from '@playwright/test'
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

test.describe('DbAdmin · 冒烟主链路', () => {
  test('建连→建表（批准）→schema→SELECT→INSERT（拒绝）', async ({ page }) => {
    test.setTimeout(180_000)
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
    await connItem.click()
    const editor = tid(page, 'sql-editor')
    await expect(editor).toBeEnabled({ timeout: 30_000 })
    await editor.fill(`CREATE TABLE e2e_items (id INTEGER PRIMARY KEY, name TEXT)`)
    await tid(page, 'run').click()
    await expect(tid(page, 'approval-modal')).toBeVisible({ timeout: 30_000 })
    await expect(tid(page, 'approval-sql')).toContainText('CREATE TABLE')
    await page.screenshot({ path: `${SHOT_DIR}/03-approval-create.png`, fullPage: true })
    await tid(page, 'approval-approve').click()
    await expect(tid(page, 'approval-modal')).toBeHidden({ timeout: 30_000 })
    await expect(tid(page, 'result-grid').or(tid(page, 'result-stats'))).toBeVisible({ timeout: 30_000 })

        // ④ schema 树即时刷新（R1 修复断言）：批准建表执行完成后，schema 树**不经整页刷新**即见新表
    // （resolveApproval finally 触发 loadTables(force)；修复前此处必须 page.reload() 才能见新表）
    const tableNode = page.locator(`[data-testid^="table-"]`, { hasText: 'e2e_items' }).first()
    try {
      await tableNode.waitFor({ state: 'visible', timeout: 8_000 })
    } catch {
      await connItem.click() // 连接节点若未展开，展开一次（数据层已是新表集合）
    }
    await expect(tableNode).toBeVisible({ timeout: 30_000 })
    await page.locator(`[data-testid^="table-"]`, { hasText: 'e2e_items' }).first().click()
    await expect(page.locator('[data-testid^="col-"]').first()).toBeVisible({ timeout: 30_000 })
    await page.screenshot({ path: `${SHOT_DIR}/04-schema.png`, fullPage: true })

    // ⑤ SELECT：结果格可见（空表 0 行）
    await editor.fill('SELECT * FROM e2e_items')
    await tid(page, 'run').click()
    await expect(tid(page, 'result-grid')).toBeVisible({ timeout: 30_000 })
    await page.screenshot({ path: `${SHOT_DIR}/05-select.png`, fullPage: true })

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
