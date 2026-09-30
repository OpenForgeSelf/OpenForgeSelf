import { test, expect } from '@playwright/test'
import { injectRealApiKey } from './helpers/real-auth'

/**
 * 记忆系统 E2E 测试 —— 对接真实后端 API（无 mock、真实认证）。
 *
 * 设计原则：
 * - **零 mock**：不拦截任何 /api/* 请求，全部走真实后端（http://localhost:7102）
 * - **真实认证**：注入 ForgeSetting.config 解密出的真实 API 密钥
 * - **数据隔离**：创建型用例用唯一标题（E2E-Real-*），用例结束立即删除
 * - **无 waitForTimeout**：全部用 auto-waiting + waitForResponse
 *
 * 覆盖范围：
 * - 记忆页面加载（搜索框/新建按钮/统计）
 * - 新建记忆（真实 POST）→ 列表出现
 * - 删除记忆（真实 DELETE）→ 列表移除
 */

// ============================================================
// 辅助函数
// ============================================================

const MEMORY_BASE = `${process.env.E2E_BACKEND_URL ?? 'http://localhost:7102'}/api/memory`

/** 通过真实后端 API 删除测试记忆（忽略 404） */
async function deleteTestMemory(id: number): Promise<void> {
  try {
    await fetch(`${MEMORY_BASE}/${id}`, { method: 'DELETE' })
  } catch {
    // 清理失败不影响用例结果
  }
}

/** 通过真实后端 API 查找测试记忆 id（按标题） */
async function findMemoryIdByTitle(title: string): Promise<number | null> {
  const res = await fetch(`${MEMORY_BASE}/search?keyword=${encodeURIComponent(title)}&page=1&pageSize=20`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: '{}',
  })
  if (!res.ok) return null
  const json = (await res.json()) as { data?: { items?: { id: number; title: string }[] } }
  return json.data?.items?.find((m) => m.title === title)?.id ?? null
}

// ============================================================
// 测试用例
// ============================================================

test.describe('记忆系统（/memory）', () => {
  test.beforeEach(async ({ page }) => {
    await injectRealApiKey(page)
  })

  test('页面加载：搜索框与新建记忆按钮可见', async ({ page }) => {
    await page.goto('/memory')
    await expect(page.getByPlaceholder('搜索记忆...')).toBeVisible()
    await expect(page.getByRole('button', { name: '新建记忆' })).toBeVisible()
  })

  test('新建记忆：真实 POST 后列表出现新记忆', async ({ page }) => {
    const title = `E2E-Real-Memory-${Date.now()}`
    await page.goto('/memory')
    await expect(page.getByRole('button', { name: '新建记忆' })).toBeVisible()

    // 打开新建模态框并填写
    await page.getByRole('button', { name: '新建记忆' }).click()
    const modal = page.locator('.modal-overlay').filter({ hasText: '新建记忆' })
    await expect(modal).toBeVisible()
    await modal.getByPlaceholder('输入记忆标题').fill(title)
    await modal.getByPlaceholder('输入记忆内容...').fill('Playwright e2e 创建的记忆')

    // 等待 POST 真实请求
    const createResponsePromise = page.waitForResponse(
      (resp) => resp.request().method() === 'POST' && /\/api\/memory\/?$/.test(resp.url())
    )
    await modal.getByRole('button', { name: /保存|创建/ }).click()
    await createResponsePromise

    // 模态框关闭，新记忆出现在列表
    await expect(modal).toBeHidden()
    await expect(page.getByText(title, { exact: true })).toBeVisible({ timeout: 10000 })

    // 清理
    const id = await findMemoryIdByTitle(title)
    if (id != null) await deleteTestMemory(id)
  })

  test('删除记忆：真实 DELETE 后列表移除', async ({ page }) => {
    const title = `E2E-Real-Memory-Del-${Date.now()}`
    // 通过真实 API 预置一条记忆
    const createRes = await fetch(MEMORY_BASE, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ title, content: '待删除的测试记忆', importance: 1 }),
    })
    const created = (await createRes.json()) as { id: number }
    const memoryId = created.id
    try {
      await page.goto('/memory')
      await expect(page.getByText(title, { exact: true })).toBeVisible({ timeout: 10000 })

      // 打开记忆详情并删除（handleDeleteMemory 用原生 confirm，需 accept）
      await page.getByText(title, { exact: true }).click()
      await expect(page.locator('.memory-detail-drawer')).toBeVisible()
      page.once('dialog', (dialog) => dialog.accept())
      const deleteBtn = page.locator('.memory-detail-drawer').getByRole('button', { name: /删除/ })
      await deleteBtn.click()
      // 等真实 DELETE 生效（轮询 API 确认已删除）
      await expect
        .poll(async () => {
          const res = await fetch(`${MEMORY_BASE}/${memoryId}`)
          return res.status
        }, { timeout: 10000 })
        .toBe(404)
    } finally {
      await deleteTestMemory(memoryId)
    }
  })
})
