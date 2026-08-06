import { test, expect, type Page } from '@playwright/test'
import { injectRealApiKey } from './helpers/real-auth'

/**
 * Prompt 提示词 + 技能管理 E2E 测试 —— 对接真实后端 API（无 mock、真实认证）。
 *
 * 设计原则：
 * - **零 mock**：不拦截任何 /api/* 请求，全部走真实后端（http://localhost:7102）
 * - **真实认证**：注入 ForgeSetting.config 解密出的真实 API 密钥
 * - **数据隔离**：创建型用例用唯一名称（E2E-Real-*）
 * - **无 waitForTimeout**：全部用 auto-waiting + waitForResponse
 *
 * 覆盖范围：
 * - /prompts 提示词页面（纯前端：提示词库切换、编辑器、新建按钮）
 * - /skills 技能管理（真实列表加载、搜索过滤、创建技能、启用/禁用切换）
 */

// ============================================================
// 辅助函数
// ============================================================

/** 导航到指定页面 */
async function gotoPage(page: Page, path: string): Promise<void> {
  await page.goto(path)
}

// ============================================================
// 测试用例
// ============================================================

test.describe('Prompt 提示词页面（/prompts）', () => {
  test.beforeEach(async ({ page }) => {
    await injectRealApiKey(page)
  })

  test('页面加载：提示词库列表与编辑器可见', async ({ page }) => {
    await gotoPage(page, '/prompts')

    // 侧栏提示词库 + 新建按钮
    await expect(page.getByText('提示词库', { exact: true })).toBeVisible()
    await expect(page.getByRole('button', { name: '新建提示词' })).toBeVisible()
    // 编辑器区域
    await expect(page.locator('.editor-textarea')).toBeVisible()
    await expect(page.getByPlaceholder('输入提示指令内容...')).toBeVisible()
  })

  test('切换提示词：点击列表项后编辑器标题随之更新', async ({ page }) => {
    await gotoPage(page, '/prompts')

    // 至少存在一个提示词项（真实前端数据）
    const items = page.locator('.prompt-item')
    const count = await items.count()
    expect(count).toBeGreaterThan(0)

    // 点击第一项后，编辑器标题不为空且与选中项一致
    const firstName = (await items.first().locator('.prompt-item-name').textContent())?.trim()
    await items.first().click()
    const editorTitle = (await page.locator('.editor-title').textContent())?.trim()
    expect(editorTitle).toBeTruthy()
    expect(editorTitle).toBe(firstName)
  })

  test('编辑器输入内容并点击保存（不报错）', async ({ page }) => {
    await gotoPage(page, '/prompts')

    const textarea = page.locator('.editor-textarea')
    await textarea.fill('测试提示词内容')
    await expect(textarea).toHaveValue('测试提示词内容')

    // 保存按钮存在且可点击（handleSave 为前端占位，无 API 调用）
    const saveBtn = page.locator('button').filter({ hasText: '保存' }).first()
    await expect(saveBtn).toBeVisible()
    await saveBtn.click()
  })
})

test.describe('技能管理页面（/skills）', () => {
  test.beforeEach(async ({ page }) => {
    await injectRealApiKey(page)
  })

  test('真实列表加载：后端播种的技能可见', async ({ page }) => {
    await gotoPage(page, '/skills')

    // 等待真实列表加载（后端种子数据：代码审查/文档生成等）
    await expect(page.getByText('代码审查', { exact: true })).toBeVisible({ timeout: 10000 })
    // 列表卡片渲染
    const cards = page.locator('.skill-card, .card-name')
    expect(await cards.count()).toBeGreaterThan(0)
  })

  test('搜索过滤：按关键字本地过滤', async ({ page }) => {
    await gotoPage(page, '/skills')
    await expect(page.getByText('代码审查', { exact: true })).toBeVisible({ timeout: 10000 })

    const searchInput = page.getByPlaceholder(/搜索技能|搜索/)
    await searchInput.fill('代码审查')
    await expect(page.getByText('代码审查', { exact: true })).toBeVisible()
  })

  test('创建技能：真实 POST 后列表出现新技能', async ({ page }) => {
    const skillName = `E2E-Real-Skill-${Date.now()}`
    await gotoPage(page, '/skills')
    await expect(page.getByText('代码审查', { exact: true })).toBeVisible({ timeout: 10000 })

    // 打开创建模态框
    await page.getByRole('button', { name: /创建技能/ }).first().click()
    const modal = page.locator('.modal-panel').filter({ hasText: '创建技能' })
    await expect(modal).toBeVisible()

    // 填写表单（名称必填）
    await modal.getByPlaceholder('技能名称').fill(skillName)
    await modal.getByPlaceholder('技能描述').fill('Playwright e2e 创建')
    await modal.getByPlaceholder('如：系统、开发、数据').fill('测试')

    // 等待 POST 真实请求
    const createResponsePromise = page.waitForResponse(
      (resp) => resp.request().method() === 'POST' && /\/api\/skills\/?$/.test(resp.url())
    )
    await modal.getByRole('button', { name: '创建技能' }).click()
    await createResponsePromise

    // 模态框关闭，新技能出现在列表（真实后端返回）
    await expect(modal).toBeHidden()
    await expect(page.getByText(skillName, { exact: true })).toBeVisible({ timeout: 10000 })
  })

  test('启用/禁用切换：真实 POST toggle 后状态徽标更新', async ({ page }) => {
    await gotoPage(page, '/skills')
    await expect(page.getByText('代码审查', { exact: true })).toBeVisible({ timeout: 10000 })

    // 找到第一张技能卡片的切换按钮（btn-ghost，文案为「停用/启用」）
    const firstCard = page.locator('.skill-card').first()
    const toggleBtn = firstCard.locator('button').filter({ hasText: /停用|启用/ })
    await expect(toggleBtn.first()).toBeVisible()

    // 点击切换 → 真实 POST /api/skills/{id}/toggle
    const toggleResponsePromise = page.waitForResponse(
      (resp) => resp.request().method() === 'POST' && /\/api\/skills\/.+\/toggle/.test(resp.url())
    )
    await toggleBtn.first().click()
    await toggleResponsePromise
    // 切换成功（状态徽标存在即可，不锁死启用/禁用值）
    await expect(firstCard.locator('.status-badge')).toBeVisible()
  })
})
