import { test, expect } from '@playwright/test'
import { injectRealApiKey } from './helpers/real-auth'

/**
 * 待办追踪 E2E 测试 —— 对接真实后端 API（无 mock、真实认证）。
 *
 * 设计原则：
 * - **零 mock**：不拦截任何 /api/* 请求，全部走真实后端（http://localhost:7102）
 * - **真实认证**：注入 ForgeSetting.config 解密出的真实 API 密钥
 * - **数据隔离**：每个用例用唯一名称（E2E-Real-*）创建自己的待办，
 *   用例结束立即删除；绝不依赖/污染他人数据（fullyParallel 下互不干扰）
 * - **无 waitForTimeout**：全部用 auto-waiting + waitForResponse
 * - **语义化 selector**：优先 getByRole/getByText/getByPlaceholder
 *
 * 覆盖范围：
 * - /todo 页面 CRUD 流程（创建 → 查看 → 完成 → 重开 → 删除）
 * - /todo 页面状态过滤（真实后端分页查询）
 * - 首页 `/` 待办面板：展示最近待办、快捷添加、跳转、完成
 * - 控制台无报错、UI 截图
 */

// ============================================================
// 辅助函数
// ============================================================

const BACKEND_URL = 'http://localhost:7102'
const TODOS_BASE = `${BACKEND_URL}/api/todos`

/** 通过真实后端 API 创建待办，返回 id */
async function createTodo(title: string, remark = ''): Promise<number> {
  const res = await fetch(TODOS_BASE, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ title, remark }),
  })
  const json = (await res.json()) as { success: boolean; data?: { id: number } }
  if (!res.ok || !json.success || !json.data) {
    throw new Error(`创建待办失败: ${res.status} ${JSON.stringify(json)}`)
  }
  return json.data.id
}

/** 通过真实后端 API 标记完成（用于构造 Completed 测试数据） */
async function completeTodo(id: number): Promise<void> {
  const res = await fetch(`${TODOS_BASE}/${id}/complete`, { method: 'POST' })
  if (!res.ok) throw new Error(`标记完成失败: ${res.status}`)
}

/** 通过真实后端 API 删除测试待办（忽略 404） */
async function deleteTodo(id: number): Promise<void> {
  try {
    await fetch(`${TODOS_BASE}/${id}`, { method: 'DELETE' })
  } catch {
    // 清理阶段失败不影响用例结果，由 finally 兜底
  }
}

// ============================================================
// /todo 页面测试
// ============================================================

test.describe('待办追踪 /todo 页面（真实后端）', () => {
  test.beforeEach(async ({ page }) => {
    await injectRealApiKey(page)
  })

  test('T1: 页面加载并展示真实创建的待办', async ({ page }) => {
    const title = `E2E-Real-T1-${Date.now()}`
    const id = await createTodo(title, '来自真实后端的测试数据')
    try {
      await page.goto('/todo')
      await expect(page.getByRole('heading', { name: '待办追踪' })).toBeVisible()
      // 真实创建的待办出现在列表中（含备注）
      await expect(page.getByText(title, { exact: true })).toBeVisible()
      await expect(page.getByText('来自真实后端的测试数据')).toBeVisible()
    } finally {
      await deleteTodo(id)
    }
  })

  test('T2: 状态过滤 - 仅看待处理', async ({ page }) => {
    const pendingTitle = `E2E-Real-T2P-${Date.now()}`
    const completedTitle = `E2E-Real-T2C-${Date.now()}`
    const pendingId = await createTodo(pendingTitle)
    const completedId = await createTodo(completedTitle)
    await completeTodo(completedId)
    try {
      await page.goto('/todo')
      await expect(page.getByText(pendingTitle, { exact: true })).toBeVisible()
      await expect(page.getByText(completedTitle, { exact: true })).toBeVisible()

      // 点击「待处理」→ 列表只剩 Pending 项
      const responsePromise = page.waitForResponse(
        (r) => r.url().includes('status=Pending') && r.status() === 200
      )
      await page.locator('.el-radio-button').filter({ hasText: '待处理' }).click()
      await responsePromise

      await expect(page.getByText(pendingTitle, { exact: true })).toBeVisible()
      await expect(page.getByText(completedTitle, { exact: true })).toBeHidden()
    } finally {
      await deleteTodo(pendingId)
      await deleteTodo(completedId)
    }
  })

  test('T3: 通过对话框创建新待办', async ({ page }) => {
    await page.goto('/todo')
    await expect(page.getByRole('heading', { name: '待办追踪' })).toBeVisible()

    const title = `E2E-Real-T3-${Date.now()}`

    await page.getByRole('button', { name: '新建待办' }).click()
    const dialog = page.locator('.el-dialog').filter({ hasText: '新建待办' })
    await expect(dialog).toBeVisible()

    await dialog.getByPlaceholder('请输入待办标题').fill(title)
    await dialog.getByPlaceholder('可选：备注说明').fill('来自 Playwright 真实创建')

    const createResponse = page.waitForResponse(
      (r) => r.url().endsWith('/api/todos') && r.request().method() === 'POST' && r.status() === 201
    )
    await dialog.getByRole('button', { name: '创建' }).click()
    await createResponse

    // 对话框关闭，列表顶部出现新项
    await expect(dialog).toBeHidden()
    await expect(page.getByText(title, { exact: true })).toBeVisible()

    // 记录 id 供清理
    const list = (await (await fetch(`${TODOS_BASE}?page=1&pageSize=20`)).json()) as {
      data: { items: { id: number; title: string }[] }
    }
    const createdId = list.data.items.find((t) => t.title === title)?.id ?? null
    if (createdId != null) await deleteTodo(createdId)
  })

  test('T4: 勾选 Pending 待办 → 标记完成', async ({ page }) => {
    const title = `E2E-Real-T4-${Date.now()}`
    const id = await createTodo(title)
    try {
      await page.goto('/todo')
      const firstItem = page.locator('.todo-item').filter({ hasText: title })
      await expect(firstItem).toBeVisible()

      const checkbox = firstItem.getByRole('checkbox', { name: '标记完成状态' })
      const completeResponse = page.waitForResponse(
        (r) => r.url().includes('/complete') && r.request().method() === 'POST' && r.status() === 200
      )
      await checkbox.click()
      await completeResponse

      // 该项出现「已完成」标签（真实后端状态更新）
      await expect(firstItem.getByText('已完成', { exact: true })).toBeVisible()
    } finally {
      await deleteTodo(id)
    }
  })

  test('T5: 勾选 Completed 待办 → 重新打开', async ({ page }) => {
    const title = `E2E-Real-T5-${Date.now()}`
    const id = await createTodo(title)
    await completeTodo(id)
    try {
      await page.goto('/todo')
      const completedItem = page.locator('.todo-item').filter({ hasText: title })
      // 初始有「已完成」标签
      await expect(completedItem.getByText('已完成', { exact: true })).toBeVisible()

      const checkbox = completedItem.getByRole('checkbox', { name: '标记完成状态' })
      const reopenResponse = page.waitForResponse(
        (r) => r.url().includes('/reopen') && r.request().method() === 'POST' && r.status() === 200
      )
      await checkbox.click()
      await reopenResponse

      // 「已完成」标签消失（真实后端状态更新）
      await expect(completedItem.getByText('已完成', { exact: true })).toBeHidden()
    } finally {
      await deleteTodo(id)
    }
  })

  test('T6: 删除待办 - 取消删除', async ({ page }) => {
    const title = `E2E-Real-T6-${Date.now()}`
    const id = await createTodo(title)
    try {
      await page.goto('/todo')
      const targetItem = page.locator('.todo-item').filter({ hasText: title })
      await expect(targetItem).toBeVisible()

      await targetItem.getByRole('button', { name: '删除待办' }).click()
      const msgBox = page.locator('.el-message-box')
      await expect(msgBox).toBeVisible()
      await msgBox.getByRole('button', { name: '取消' }).click()

      // 待办仍存在（真实删除未发生）
      await expect(page.getByText(title, { exact: true })).toBeVisible()
    } finally {
      await deleteTodo(id)
    }
  })

  test('T7: 删除待办 - 确认删除', async ({ page }) => {
    const title = `E2E-Real-T7-${Date.now()}`
    const id = await createTodo(title)
    try {
      await page.goto('/todo')
      const targetItem = page.locator('.todo-item').filter({ hasText: title })
      await expect(targetItem).toBeVisible()

      await targetItem.getByRole('button', { name: '删除待办' }).click()
      const msgBox = page.locator('.el-message-box')
      await expect(msgBox).toBeVisible()

      const deleteResponse = page.waitForResponse(
        (r) =>
          r.url().match(/\/api\/todos\/\d+\/?$/) !== null &&
          r.request().method() === 'DELETE' &&
          r.status() === 204
      )
      await msgBox.getByRole('button', { name: '删除' }).click()
      await deleteResponse

      // 待办消失（真实删除生效）
      await expect(page.getByText(title, { exact: true })).toBeHidden()
    } finally {
      await deleteTodo(id)
    }
  })

  test('T8: 截图与控制台无报错', async ({ page, browserName }) => {
    const title = `E2E-Real-T8-${Date.now()}`
    const id = await createTodo(title)
    try {
      const errors: string[] = []
      page.on('console', (msg) => {
        if (msg.type() === 'error') errors.push(msg.text())
      })
      page.on('pageerror', (err) => {
        errors.push(`pageerror: ${err.message}`)
      })

      await page.goto('/todo')
      await expect(page.getByText(title, { exact: true })).toBeVisible()

      await page.screenshot({
        path: `test-results/todo-${browserName}-list.png`,
        fullPage: true,
      })

      // 允许资源加载类网络错误日志，但不应有 JS 运行时错误
      const realErrors = errors.filter(
        (e) => !e.includes('Failed to load resource') && !e.includes('404')
      )
      expect(realErrors, `控制台报错: ${realErrors.join('\n')}`).toEqual([])
    } finally {
      await deleteTodo(id)
    }
  })
})

// ============================================================
// 首页待办面板测试
// ============================================================

test.describe('首页 / 待办面板（真实后端）', () => {
  test.beforeEach(async ({ page }) => {
    await injectRealApiKey(page)
  })

  test('H1: 首页展示最近待处理待办，不展示已完成项', async ({ page }) => {
    const pendingTitle = `E2E-Real-H1P-${Date.now()}`
    const completedTitle = `E2E-Real-H1C-${Date.now()}`
    const pendingId = await createTodo(pendingTitle)
    const completedId = await createTodo(completedTitle)
    await completeTodo(completedId)
    try {
      await page.goto('/')
      const todoPanel = page.locator('.todo-panel')
      await expect(todoPanel.getByText('待办', { exact: true })).toBeVisible()

      // 首页面板只显示 Pending 项
      await expect(todoPanel.getByText(pendingTitle, { exact: true })).toBeVisible()
      await expect(todoPanel.getByText(completedTitle, { exact: true })).toBeHidden()
    } finally {
      await deleteTodo(pendingId)
      await deleteTodo(completedId)
    }
  })

  test('H2: 点击「新建待办」打开快捷添加对话框并创建', async ({ page }) => {
    await page.goto('/')
    const todoPanel = page.locator('.todo-panel')
    await expect(todoPanel.getByText('待办', { exact: true })).toBeVisible()

    await todoPanel.getByRole('button', { name: '新建待办' }).click()
    const dialog = page.locator('.el-dialog').filter({ hasText: '新建待办' })
    await expect(dialog).toBeVisible()

    const title = `E2E-Real-H2-${Date.now()}`
    await dialog.getByPlaceholder('请输入待办标题').fill(title)
    const createResponse = page.waitForResponse(
      (r) => r.url().endsWith('/api/todos') && r.request().method() === 'POST' && r.status() === 201
    )
    await dialog.getByRole('button', { name: '创建' }).click()
    await createResponse

    // 对话框关闭，新待办出现在首页面板
    await expect(dialog).toBeHidden()
    await expect(todoPanel.getByText(title, { exact: true })).toBeVisible()

    // 清理：从列表读取 id 并删除
    const list = (await (await fetch(`${TODOS_BASE}?page=1&pageSize=20`)).json()) as {
      data: { items: { id: number; title: string }[] }
    }
    const created = list.data.items.find((t) => t.title === title)
    if (created) await deleteTodo(created.id)
  })

  test('H3: 点击「查看全部」跳转到 /todo', async ({ page }) => {
    await page.goto('/')
    const todoPanel = page.locator('.todo-panel')
    await expect(todoPanel.getByText('待办', { exact: true })).toBeVisible()

    await todoPanel.getByRole('button', { name: '查看全部待办' }).click()
    await expect(page).toHaveURL(/\/todo$/)
    await expect(page.getByRole('heading', { name: '待办追踪' })).toBeVisible()
  })

  test('H4: 勾选首页待办 → 调用 complete 接口并消失', async ({ page }) => {
    const title = `E2E-Real-H4-${Date.now()}`
    const id = await createTodo(title)
    try {
      await page.goto('/')
      const todoPanel = page.locator('.todo-panel')
      const item = todoPanel.locator('.todo-item').filter({ hasText: title })
      await expect(item).toBeVisible()

      const completeResponse = page.waitForResponse(
        (r) => r.url().includes('/complete') && r.request().method() === 'POST' && r.status() === 200
      )
      // 首页面板 checkbox 是原生 input（无 aria-label），按类型定位
      await item.locator('input[type="checkbox"]').click()
      await completeResponse

      // 标记完成后，该 Pending 项不再出现在首页（首页只显示 Pending）
      await expect(item).toBeHidden()
    } finally {
      await deleteTodo(id)
    }
  })

  test('H5: 首页截图与控制台无报错', async ({ page, browserName }) => {
    const errors: string[] = []
    page.on('pageerror', (err) => {
      errors.push(`pageerror: ${err.message}`)
    })

    await page.goto('/')
    await expect(page.locator('.todo-panel')).toBeVisible()

    await page.screenshot({
      path: `test-results/todo-${browserName}-home.png`,
      fullPage: true,
    })

    expect(errors, `JS 运行时错误: ${errors.join('\n')}`).toEqual([])
  })
})
