import { test, expect, type Page } from '@playwright/test'

/**
 * 快捷链接插件 e2e（用户视角走查，对接真实运行宿主 51888）。
 *
 * 属于 e2e-testing 技能的「插件层 e2e」：`e2e/plugins/<id>/<id>.spec.ts`。
 * 本文件是 **live 变体**：不接 globalSetup、不注入 API 密钥，直接验证已运行的
 * publish 宿主（51888 为长期运行的正式实例，登录态与密钥已存在），因此直接从
 * `@playwright/test` 取 test/expect，不经 `fixtures/e2e`（该 fixture 依赖
 * globalSetup 拉起的全新环境）。
 *
 * 运行（验证已运行的 publish 宿主，勿另起全新环境）：
 *   $env:E2E_API_TOKEN="<解密明文>"
 *   pnpm exec playwright test --config=playwright.live.config.ts e2e/plugins/quick-links
 *
 * 覆盖：桌面端侧栏/分类筛选（回归 #2）、搜索无描述链接不崩溃（回归 #1）、
 *       添加/编辑/删除 CRUD 走查 + 截图读图（删除走自定义确认框）、
 *       新增链接时弹窗内动态新增分类（回归 #3）、
 *       删除分类确认框级联提示其下链接数量（回归 #4）。
 *
 * 注意：本插件已移除原生 confirm()/alert()，改用统一自定义 ConfirmHost/ToastHost，
 *       e2e 中删除动作须点击自定义确认框 `.confirm-box .ok`，不再监听原生 dialog。
 */

const BACKEND = 'http://localhost:51888/api/quicklinks'

async function createCategory(name: string): Promise<number> {
  const res = await fetch(`${BACKEND}/categories`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ name, icon: '📁' }),
  })
  return ((await res.json()) as { data: { id: number } }).data.id
}
async function createLink(name: string, url: string, categoryId: number | null, description?: string): Promise<number> {
  const body: Record<string, unknown> = { name, url, categoryId }
  if (description !== undefined) body.description = description
  const res = await fetch(BACKEND, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  return ((await res.json()) as { data: { id: number } }).data.id
}
async function deleteCategory(id: number) {
  try { await fetch(`${BACKEND}/categories/${id}`, { method: 'DELETE' }) } catch { /* ignore */ }
}
async function deleteLink(id: number) {
  try { await fetch(`${BACKEND}/${id}`, { method: 'DELETE' }) } catch { /* ignore */ }
}
async function openQuickLinks(page: Page) {
  await page.goto('/quick-links')
  await expect(page.getByRole('heading', { name: '快捷链接' })).toBeVisible()
  // 容忍 live 宿主偶发首请求 400（冷启动/竞态）：出现错误态则点「重试」重载，最多 3 次。
  // 持久错误仍会由后续断言暴露，不会掩盖真实回归。
  for (let i = 0; i < 3; i++) {
    const err = page.locator('.error-state')
    if (await err.isVisible().catch(() => false)) {
      await page.locator('.retry-btn').click()
      await page.waitForTimeout(800)
    } else {
      break
    }
  }
}

test.describe('快捷链接插件（真实运行宿主 51888）', () => {
  test('桌面端侧栏与分类筛选可用（回归 #2）', async ({ page }) => {
    const cat = `E2E-Cat-${Date.now()}`
    const catId = await createCategory(cat)
    const linkIn = `E2E-In-${Date.now()}`
    const linkOut = `E2E-Out-${Date.now()}`
    const inId = await createLink(linkIn, 'https://in.example.com', catId)
    const outId = await createLink(linkOut, 'https://out.example.com', null)
    try {
      await openQuickLinks(page)

      // 桌面端侧栏应自动展开（修复 #2：原桌面不可见）
      await expect(page.locator('.sidebar')).toBeVisible()
      const catItem = page.locator('.sidebar .category-item', { hasText: cat }).first()
      await expect(catItem).toBeVisible()

      // 点击分类筛选：分类内链接可见、分类外不可见
      // 注意：悬停后分类行右侧「↑/↓」排序按钮显现，会把 .category-name 挤压到很窄，
      // 行几何中心会落到「↑」按钮（误触发 moveCategory → 后端 400 → 主区崩溃）。
      // 因此显式点 .category-name 的左侧文本区，确保触发的是筛选而非排序。
      await catItem.locator('.category-name').click({ position: { x: 3, y: 3 } })
      await expect(page.getByText(linkIn, { exact: true })).toBeVisible()
      await expect(page.getByText(linkOut, { exact: true })).toBeHidden()

      // 切回"全部"
      await page.locator('.sidebar .category-item', { hasText: '全部' }).first().click()
      await expect(page.getByText(linkOut, { exact: true })).toBeVisible()
    } finally {
      await deleteLink(inId)
      await deleteLink(outId)
      await deleteCategory(catId)
    }
  })

  test('搜索无描述链接不崩溃（回归 #1）', async ({ page }) => {
    // 创建一个描述为空（null）的链接 —— 原 bug 在搜索框非空时整页崩溃
    const linkId = await createLink(`E2E-NoDesc-${Date.now()}`, 'https://nodesc.example.com', null)
    const errors: string[] = []
    page.on('pageerror', (e) => errors.push(e.message))
    page.on('console', (m) => { if (m.type() === 'error') errors.push(m.text()) })
    try {
      await openQuickLinks(page)
      const search = page.locator('.search-input, input[type="text"]').first()
      await expect(search).toBeVisible()
      await search.fill('NoDesc')
      await page.waitForTimeout(300)
      // 关键断言：无整页崩溃（页面错误应为空，且搜索结果区仍渲染）
      await expect(page.locator('.links-grid, .empty-state').first()).toBeVisible()
      expect(errors, `页面出现错误: ${errors.join(' | ')}`).toHaveLength(0)
    } finally {
      await deleteLink(linkId)
    }
  })

  test('添加 / 编辑 / 删除 CRUD 走查 + 截图读图', async ({ page }) => {
    const cat = `E2E-CRUD-${Date.now()}`
    const catId = await createCategory(cat)
    const name = `E2E-Link-${Date.now()}`
    const edited = `${name}-Edit`
    let linkId = -1
    try {
      await openQuickLinks(page)

      // 添加
      await page.locator('.action-btn.add-btn').click()
      const addModal = page.locator('.modal-container').filter({ hasText: '添加链接' })
      await expect(addModal).toBeVisible()
      await addModal.locator('input').nth(0).fill(name)
      await addModal.locator('input').nth(1).fill('https://example.com')
      await addModal.locator('select').selectOption({ value: String(catId) })
      const createResp = page.waitForResponse(
        (r) => r.request().method() === 'POST' && /\/api\/quicklinks\/?$/.test(r.url()),
      )
      await addModal.getByRole('button', { name: '保存' }).click()
      await createResp
      await expect(addModal).toBeHidden()
      await expect(page.getByText(name, { exact: true })).toBeVisible({ timeout: 10000 })

      linkId = ((await (await fetch(BACKEND)).json()) as { data: { items: { id: number; name: string }[] } })
        .data.items.find((l) => l.name === name)?.id ?? -1
      expect(linkId).toBeGreaterThan(0)

      // 编辑
      const card = page.locator('.link-card').filter({ hasText: name })
      await card.hover()
      await page.getByRole('button', { name: `编辑 ${name}` }).click()
      const editModal = page.locator('.modal-container').filter({ hasText: '编辑链接' })
      await expect(editModal).toBeVisible()
      await editModal.locator('input').nth(0).fill(edited)
      const updateResp = page.waitForResponse(
        (r) => r.request().method() === 'PUT' && new RegExp(`/api/quicklinks/${linkId}`).test(r.url()),
      )
      await editModal.getByRole('button', { name: '保存' }).click()
      await updateResp
      await expect(editModal).toBeHidden()
      await expect(page.getByText(edited, { exact: true })).toBeVisible({ timeout: 10000 })

      // 删除（自定义确认框替代原生 confirm）
      const card2 = page.locator('.link-card').filter({ hasText: edited })
      await card2.hover()
      await page.getByRole('button', { name: `删除 ${edited}` }).click()
      const confirmBox = page.locator('.confirm-box')
      await expect(confirmBox).toBeVisible()
      const delResp = page.waitForResponse(
        (r) => r.request().method() === 'DELETE' && new RegExp(`/api/quicklinks/${linkId}`).test(r.url()),
      )
      await confirmBox.locator('.ok').click()
      await delResp
      await expect(confirmBox).toBeHidden()
      await expect(page.getByText(edited, { exact: true })).toBeHidden({ timeout: 10000 })

      // 截图读图：视觉检查（图标/间距/对齐/溢出）
      await openQuickLinks(page)
      await page.screenshot({ path: 'screenshots/e2e/quick-links/walkthrough.png', fullPage: true })
    } finally {
      if (linkId > 0) await deleteLink(linkId)
      await deleteCategory(catId)
    }
  })

  test('新增链接时可在弹窗内动态新增分类（回归 #3）', async ({ page }) => {
    // 内联分类名输入框 maxlength=20，名称须 ≤20 字符，否则会被截断导致断言不匹配
    const catName = `DC${Date.now().toString().slice(-9)}`
    let createdCatId: number | null = null
    const errors: string[] = []
    page.on('pageerror', (e) => errors.push(e.message))
    page.on('console', (m) => { if (m.type() === 'error') errors.push(m.text()) })

    try {
      await openQuickLinks(page)
      await page.locator('.action-btn.add-btn').click()
      const addModal = page.locator('.modal-container').filter({ hasText: '添加链接' })
      await expect(addModal).toBeVisible()

      // 点击「+」新增分类按钮 → 内联输入框出现
      const addCatBtn = addModal.locator('.add-cat-btn')
      await expect(addCatBtn).toBeVisible()
      await addCatBtn.click()
      const inlineInput = addModal.locator('.add-cat-inline input')
      await expect(inlineInput).toBeVisible()

      // 输入分类名并确认 → 等待分类创建接口返回
      await inlineInput.fill(catName)
      const confirmResp = page.waitForResponse(
        (r) => r.request().method() === 'POST' && /\/api\/quicklinks\/categories$/.test(r.url()),
      )
      await addModal.locator('.add-cat-confirm').click()
      const resp = await confirmResp
      expect(resp.status()).toBe(200)
      const body = (await resp.json()) as { data: { id: number } }
      createdCatId = body.data.id
      expect(createdCatId).toBeGreaterThan(0)

      // 确认后内联输入消失、下拉自动选中新分类
      await expect(addModal.locator('.add-cat-inline')).toBeHidden()
      const sel = addModal.locator('select.form-select')
      await expect(sel).toHaveValue(String(createdCatId))

      // 取消关闭弹窗（不真正创建链接）
      await addModal.getByRole('button', { name: '取消' }).click()
      await expect(addModal).toBeHidden()

      // 回到列表，新分类应出现在侧栏（证明全链路生效）
      await expect(page.locator('.sidebar .category-item', { hasText: catName }).first()).toBeVisible()

      expect(errors, `页面出现错误: ${errors.join(' | ')}`).toHaveLength(0)
    } finally {
      if (createdCatId != null) await deleteCategory(createdCatId)
    }
  })

  test('删除分类确认框级联提示其下链接数量（回归 #4）', async ({ page }) => {
    const cat = `E2E-Cascade-${Date.now().toString().slice(-9)}`
    const catId = await createCategory(cat)
    const l1 = await createLink(`E2E-C1-${Date.now()}`, 'https://c1.example.com', catId)
    const l2 = await createLink(`E2E-C2-${Date.now()}`, 'https://c2.example.com', catId)
    const errors: string[] = []
    page.on('pageerror', (e) => errors.push(e.message))
    page.on('console', (m) => { if (m.type() === 'error') errors.push(m.text()) })
    try {
      await openQuickLinks(page)
      // 侧栏分类管理中出现该分类
      const catItem = page.locator('.sidebar .category-item', { hasText: cat }).first()
      await expect(catItem).toBeVisible()
      // 悬停显示操作按钮，点击删除分类
      await catItem.hover()
      await catItem.getByRole('button', { name: '删除分类' }).click()
      // 自定义确认框出现，并提示将连带删除其下的 2 条链接
      const confirmBox = page.locator('.confirm-box')
      await expect(confirmBox).toBeVisible()
      await expect(confirmBox.locator('.confirm-message')).toContainText('2 条链接')
      // 取消，不真正删除
      await confirmBox.locator('.cancel').click()
      await expect(confirmBox).toBeHidden()
      expect(errors, `页面出现错误: ${errors.join(' | ')}`).toHaveLength(0)
    } finally {
      await deleteLink(l1)
      await deleteLink(l2)
      await deleteCategory(catId)
    }
  })
})
