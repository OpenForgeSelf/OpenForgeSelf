import { test, expect, type Page } from '@playwright/test'
import { injectRealApiKey } from './helpers/real-auth'

/**
 * 快捷链接 CRUD E2E 测试 —— 对接真实后端 API（无 mock、真实认证）。
 *
 * 设计原则：
 * - **零 mock**：不拦截任何 /api/* 请求，全部走真实后端（http://localhost:7102/api/quicklinks）
 * - **真实认证**：注入 ForgeSetting.config 解密出的真实 API 密钥
 * - **数据隔离**：创建型用例用唯一名称（E2E-Real-*）的分类与链接，用例结束立即删除
 * - **无 waitForTimeout**：全部用 auto-waiting + waitForResponse
 *
 * 覆盖范围：
 * - 快捷链接页面加载（/quick-links 路由）
 * - 添加链接：创建分类 → UI 表单填写 → 保存 → 列表可见
 * - 编辑链接：UI 编辑 → 名称更新 → 列表刷新
 * - 删除链接：UI 删除（confirm）→ 列表移除
 * - 清理：删除测试链接与分类（真实 API）
 */

// ============================================================
// 辅助函数
// ============================================================

const BACKEND = 'http://localhost:7102/api/quicklinks'

/** 通过真实后端 API 创建测试分类，返回 id */
async function createTestCategory(name: string): Promise<number> {
  const res = await fetch(`${BACKEND}/categories`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ name, icon: '📁' }),
  })
  const json = (await res.json()) as { data: { id: number } }
  return json.data.id
}

/** 通过真实后端 API 删除测试分类（忽略 404） */
async function deleteTestCategory(id: number): Promise<void> {
  try {
    await fetch(`${BACKEND}/categories/${id}`, { method: 'DELETE' })
  } catch {
    // 清理失败不影响用例结果
  }
}

/** 通过真实后端 API 删除测试链接（忽略 404） */
async function deleteTestLink(id: number): Promise<void> {
  try {
    await fetch(`${BACKEND}/${id}`, { method: 'DELETE' })
  } catch {
    // 清理失败不影响用例结果
  }
}

/** 查询测试链接 id（按名称） */
async function findLinkIdByName(name: string): Promise<number | null> {
  const res = await fetch(BACKEND)
  const json = (await res.json()) as { data: { items: { id: number; name: string }[] } }
  return json.data.items.find((l) => l.name === name)?.id ?? null
}

/** 打开快捷链接页面 */
async function navigateToQuickLinks(page: Page): Promise<void> {
  await page.goto('/quick-links')
  await expect(page.getByRole('heading', { name: '快捷链接' })).toBeVisible()
}

// ============================================================
// 测试用例
// ============================================================

test.describe('快捷链接 CRUD（真实后端）', () => {
  test.beforeEach(async ({ page }) => {
    await injectRealApiKey(page)
  })

  test('添加链接：创建分类 → UI 表单保存 → 列表可见', async ({ page }) => {
    const catName = `E2E-Real-Cat-${Date.now()}`
    const linkName = `E2E-Real-Link-${Date.now()}`
    const catId = await createTestCategory(catName)
    try {
      await navigateToQuickLinks(page)

      // 添加链接（页头按钮；空状态也有"添加链接"按钮，用 .add-btn 精确定位）
      await page.locator('.action-btn.add-btn').click()
      const modal = page.locator('.modal-container').filter({ hasText: '添加链接' })
      await expect(modal).toBeVisible()
      await modal.locator('input').nth(0).fill(linkName)
      await modal.locator('input').nth(1).fill('https://example.com')
      await modal.locator('select').selectOption({ value: String(catId) })

      // 等待 POST 真实请求完成
      const createResponsePromise = page.waitForResponse(
        (resp) =>
          resp.request().method() === 'POST' && /\/api\/quicklinks\/?$/.test(resp.url())
      )
      await modal.getByRole('button', { name: '保存' }).click()
      await createResponsePromise
      await expect(modal).toBeHidden()

      // 链接出现在列表中（真实后端返回）
      await expect(page.getByText(linkName, { exact: true })).toBeVisible({ timeout: 10000 })
    } finally {
      const linkId = await findLinkIdByName(linkName)
      if (linkId != null) await deleteTestLink(linkId)
      await deleteTestCategory(catId)
    }
  })

  test('编辑链接：UI 编辑 → 名称更新 → 列表刷新', async ({ page }) => {
    const catName = `E2E-Real-Cat-${Date.now()}`
    const linkName = `E2E-Real-Link-${Date.now()}`
    const newName = `${linkName}-Edited`
    const catId = await createTestCategory(catName)

    // 通过真实 API 预置一条链接
    const createRes = await fetch(BACKEND, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        name: linkName,
        url: 'https://example.com',
        categoryId: catId,
      }),
    })
    const created = (await createRes.json()) as { data: { id: number } }
    const linkId = created.data.id
    try {
      await navigateToQuickLinks(page)
      await expect(page.getByText(linkName, { exact: true })).toBeVisible({ timeout: 10000 })

      // 编辑/删除按钮是 hover 才显示（v-show="isHovered"），先悬停卡片
      const card = page.locator('.link-card').filter({ hasText: linkName })
      await card.hover()

      // 点击卡片编辑按钮
      await page.getByRole('button', { name: `编辑 ${linkName}` }).click()
      const modal = page.locator('.modal-container').filter({ hasText: '编辑链接' })
      await expect(modal).toBeVisible()

      // 修改名称
      const nameInput = modal.locator('input').nth(0)
      await nameInput.fill(newName)

      const updateResponsePromise = page.waitForResponse(
        (resp) =>
          resp.request().method() === 'PUT' &&
          new RegExp(`/api/quicklinks/${linkId}`).test(resp.url())
      )
      await modal.getByRole('button', { name: '保存' }).click()
      await updateResponsePromise
      await expect(modal).toBeHidden()

      // 新名称出现在列表，旧名称消失
      await expect(page.getByText(newName, { exact: true })).toBeVisible({ timeout: 10000 })
      await expect(page.getByText(linkName, { exact: true })).toBeHidden()
    } finally {
      await deleteTestLink(linkId)
      await deleteTestCategory(catId)
    }
  })

  test('删除链接：UI 删除（confirm）→ 列表移除', async ({ page }) => {
    const catName = `E2E-Real-Cat-${Date.now()}`
    const linkName = `E2E-Real-Link-${Date.now()}`
    const catId = await createTestCategory(catName)

    const createRes = await fetch(BACKEND, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        name: linkName,
        url: 'https://example.com',
        categoryId: catId,
      }),
    })
    const created = (await createRes.json()) as { data: { id: number } }
    const linkId = created.data.id
    try {
      await navigateToQuickLinks(page)
      await expect(page.getByText(linkName, { exact: true })).toBeVisible({ timeout: 10000 })

      // 处理原生 confirm 弹窗
      page.once('dialog', (dialog) => dialog.accept())

      // 编辑/删除按钮是 hover 才显示（v-show="isHovered"），先悬停卡片
      const card = page.locator('.link-card').filter({ hasText: linkName })
      await card.hover()

      // 点击卡片删除按钮
      await page.getByRole('button', { name: `删除 ${linkName}` }).click()

      // 等待 DELETE 真实请求完成
      const deleteResponsePromise = page.waitForResponse(
        (resp) =>
          resp.request().method() === 'DELETE' &&
          new RegExp(`/api/quicklinks/${linkId}`).test(resp.url())
      )
      await deleteResponsePromise

      // 链接从列表移除
      await expect(page.getByText(linkName, { exact: true })).toBeHidden({ timeout: 10000 })
    } finally {
      await deleteTestLink(linkId)
      await deleteTestCategory(catId)
    }
  })
})
