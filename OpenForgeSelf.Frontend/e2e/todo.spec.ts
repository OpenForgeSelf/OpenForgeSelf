import { test, expect, type Page, type Route } from '@playwright/test'

/**
 * 待办追踪 E2E 测试 —— Playwright 官方最佳实践。
 *
 * 设计原则：
 * - **完全 mock 后端 API**：用 page.route() 拦截所有 /api/todos* 请求，
 *   不依赖真实后端、无网络副作用。
 * - **无 waitForTimeout**：全部用 Playwright auto-waiting（expect(locator).toBeVisible()）
 *   或 waitForResponse 等待 API 完成。
 * - **数据隔离**：每个用例独立 mock 数据，无状态依赖，可并行。
 * - **语义化 selector**：优先 getByRole/getByText/getByLabel，避免依赖 CSS class。
 *
 * 覆盖范围：
 * - /todo 页面 CRUD 流程（创建 → 查看 → 完成 → 重开 → 删除）
 * - /todo 页面状态过滤
 * - 首页 `/` 待办面板：展示最近待办、快捷添加、跳转
 * - 控制台无报错、UI 截图
 */

// ============================================================
// 类型与 mock 数据
// ============================================================

type TodoStatus = 'Pending' | 'Completed'

interface TodoItem {
  id: number
  title: string
  remark?: string | null
  status: TodoStatus
  dueDate?: string | null
  createdAt: string
  updatedAt: string
  completedAt?: string | null
}

interface CreateTodoRequest {
  title: string
  remark?: string
  dueDate?: string
}

interface UpdateTodoRequest {
  title?: string
  remark?: string
  dueDate?: string
}

interface MockState {
  todos: TodoItem[]
  nextId: number
  /** 收到的请求日志（断言用） */
  requests: Array<{ method: string; url: string; body: unknown }>
}

function createMockState(): MockState {
  const now = new Date().toISOString()
  return {
    todos: [
      {
        id: 1,
        title: '整理本周周报',
        remark: '涵盖前端、后端进展',
        status: 'Pending',
        dueDate: null,
        createdAt: now,
        updatedAt: now,
        completedAt: null,
      },
      {
        id: 2,
        title: '完成 E2E 测试',
        remark: null,
        status: 'Pending',
        dueDate: null,
        createdAt: now,
        updatedAt: now,
        completedAt: null,
      },
      {
        id: 3,
        title: '历史已完成项',
        remark: '昨天完成',
        status: 'Completed',
        dueDate: null,
        createdAt: now,
        updatedAt: now,
        completedAt: now,
      },
    ],
    nextId: 100,
    requests: [],
  }
}

/** 包装为后端 ApiResponse<T> 成功响应体 */
function ok<T>(data: T) {
  return { code: 0, message: 'ok', success: true, data }
}

// ============================================================
// Mock 路由
// ============================================================

async function installMocks(page: Page): Promise<MockState> {
  const state = createMockState()

  await page.route('**/api/todos**', (route: Route) => {
    const request = route.request()
    const method = request.method()
    const url = request.url()
    const bodyText = request.postData() ?? ''
    let body: unknown = null
    if (bodyText) {
      try {
        body = JSON.parse(bodyText)
      } catch {
        body = bodyText
      }
    }

    // GET /api/todos  → 分页列表
    if (method === 'GET' && /\/api\/todos\/?(\?.*)?$/.test(url)) {
      const statusMatch = url.match(/status=([^&]+)/)
      const pageMatch = url.match(/page=(\d+)/)
      const sizeMatch = url.match(/pageSize=(\d+)/)
      const status = statusMatch ? decodeURIComponent(statusMatch[1]) : undefined
      const pageNum = pageMatch ? Number(pageMatch[1]) : 1
      const pageSize = sizeMatch ? Number(sizeMatch[1]) : 20

      let filtered = state.todos
      if (status === 'Pending' || status === 'Completed') {
        filtered = filtered.filter(t => t.status === status)
      }
      const total = filtered.length
      const start = (pageNum - 1) * pageSize
      const items = filtered.slice(start, start + pageSize)

      state.requests.push({ method, url, body })
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(ok({ items, total, page: pageNum, pageSize })),
      })
    }

    // GET /api/todos/{id}  → 详情
    const getMatch = url.match(/\/api\/todos\/(\d+)\/?$/)
    if (method === 'GET' && getMatch) {
      const id = Number(getMatch[1])
      const found = state.todos.find(t => t.id === id)
      if (!found) {
        return route.fulfill({
          status: 404,
          contentType: 'application/json',
          body: JSON.stringify({ code: 404, message: '待办不存在', success: false }),
        })
      }
      state.requests.push({ method, url, body })
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(ok(found)),
      })
    }

    // POST /api/todos  → 新增
    if (method === 'POST' && /\/api\/todos\/?$/.test(url)) {
      const req = body as CreateTodoRequest
      if (!req?.title || !req.title.trim()) {
        return route.fulfill({
          status: 400,
          contentType: 'application/json',
          body: JSON.stringify({ code: 400, message: '标题不能为空', success: false }),
        })
      }
      const now = new Date().toISOString()
      const newTodo: TodoItem = {
        id: state.nextId++,
        title: req.title.trim(),
        remark: req.remark ?? null,
        status: 'Pending',
        dueDate: req.dueDate ?? null,
        createdAt: now,
        updatedAt: now,
        completedAt: null,
      }
      state.todos.unshift(newTodo)
      state.requests.push({ method, url, body })
      return route.fulfill({
        status: 201,
        contentType: 'application/json',
        body: JSON.stringify(ok(newTodo)),
      })
    }

    // PUT /api/todos/{id}  → 更新
    const putMatch = url.match(/\/api\/todos\/(\d+)\/?$/)
    if (method === 'PUT' && putMatch) {
      const id = Number(putMatch[1])
      const idx = state.todos.findIndex(t => t.id === id)
      if (idx < 0) {
        return route.fulfill({
          status: 404,
          contentType: 'application/json',
          body: JSON.stringify({ code: 404, message: '待办不存在', success: false }),
        })
      }
      const req = body as UpdateTodoRequest
      const now = new Date().toISOString()
      state.todos[idx] = {
        ...state.todos[idx],
        title: req.title ?? state.todos[idx].title,
        remark: req.remark !== undefined ? req.remark : state.todos[idx].remark,
        dueDate: req.dueDate !== undefined ? req.dueDate : state.todos[idx].dueDate,
        updatedAt: now,
      }
      state.requests.push({ method, url, body })
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(ok(state.todos[idx])),
      })
    }

    // DELETE /api/todos/{id}  → 删除（204 NoContent）
    const deleteMatch = url.match(/\/api\/todos\/(\d+)\/?$/)
    if (method === 'DELETE' && deleteMatch) {
      const id = Number(deleteMatch[1])
      const idx = state.todos.findIndex(t => t.id === id)
      if (idx < 0) {
        return route.fulfill({
          status: 404,
          contentType: 'application/json',
          body: JSON.stringify({ code: 404, message: '待办不存在', success: false }),
        })
      }
      state.todos.splice(idx, 1)
      state.requests.push({ method, url, body })
      return route.fulfill({ status: 204 })
    }

    // POST /api/todos/{id}/complete  → 标记完成
    if (method === 'POST' && /\/api\/todos\/(\d+)\/complete\/?$/.test(url)) {
      const m = url.match(/\/api\/todos\/(\d+)\/complete\/?$/)!
      const id = Number(m[1])
      const idx = state.todos.findIndex(t => t.id === id)
      if (idx < 0) {
        return route.fulfill({
          status: 404,
          contentType: 'application/json',
          body: JSON.stringify({ code: 404, message: '待办不存在', success: false }),
        })
      }
      const now = new Date().toISOString()
      state.todos[idx] = {
        ...state.todos[idx],
        status: 'Completed',
        completedAt: now,
        updatedAt: now,
      }
      state.requests.push({ method, url, body })
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(ok(state.todos[idx])),
      })
    }

    // POST /api/todos/{id}/reopen  → 重新打开
    if (method === 'POST' && /\/api\/todos\/(\d+)\/reopen\/?$/.test(url)) {
      const m = url.match(/\/api\/todos\/(\d+)\/reopen\/?$/)!
      const id = Number(m[1])
      const idx = state.todos.findIndex(t => t.id === id)
      if (idx < 0) {
        return route.fulfill({
          status: 404,
          contentType: 'application/json',
          body: JSON.stringify({ code: 404, message: '待办不存在', success: false }),
        })
      }
      const now = new Date().toISOString()
      state.todos[idx] = {
        ...state.todos[idx],
        status: 'Pending',
        completedAt: null,
        updatedAt: now,
      }
      state.requests.push({ method, url, body })
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(ok(state.todos[idx])),
      })
    }

    return route.fulfill({
      status: 404,
      contentType: 'application/json',
      body: JSON.stringify({ success: false, message: `Mock 未覆盖: ${method} ${url}` }),
    })
  })

  return state
}

// ============================================================
// /todo 页面测试
// ============================================================

test.describe('待办追踪 /todo 页面', () => {
  test.beforeEach(async ({ page }) => {
    await installMocks(page)
    await page.goto('/todo')
    // 等待列表加载完成：第一个待办标题可见
    await expect(page.getByText('整理本周周报')).toBeVisible()
  })

  test('T1: 页面加载并展示待办列表', async ({ page }) => {
    test.info().annotations.push({ type: 'scenario', description: '访问 /todo，应展示 3 条初始待办' })

    await expect(page.getByRole('heading', { name: '待办追踪' })).toBeVisible()
    await expect(page.getByText('整理本周周报')).toBeVisible()
    await expect(page.getByText('完成 E2E 测试')).toBeVisible()
    await expect(page.getByText('历史已完成项')).toBeVisible()

    // 共 3 条
    await expect(page.getByText('共 3 条')).toBeVisible()
  })

  test('T2: 状态过滤 - 仅看待处理', async ({ page }) => {
    test.info().annotations.push({ type: 'scenario', description: '点击「待处理」单选按钮 → 列表只剩 Pending 项' })

    // 设置 response wait 之后再触发点击，避免 race condition
    const responsePromise = page.waitForResponse(r =>
      r.url().includes('status=Pending') && r.status() === 200
    )
    // 点击 label 内的可见文字（Element Plus radio input 被内层 span 拦截 pointer events）
    await page.locator('.el-radio-button').filter({ hasText: '待处理' }).click()
    await responsePromise

    await expect(page.getByText('整理本周周报')).toBeVisible()
    await expect(page.getByText('完成 E2E 测试')).toBeVisible()
    await expect(page.getByText('历史已完成项')).toBeHidden()
  })

  test('T3: 通过对话框创建新待办', async ({ page }) => {
    test.info().annotations.push({ type: 'scenario', description: '点击「新建待办」→ 输入标题与备注 → 创建后出现在列表顶部' })

    await page.getByRole('button', { name: '新建待办' }).click()

    const dialog = page.locator('.el-dialog').filter({ hasText: '新建待办' })
    await expect(dialog).toBeVisible()

    await dialog.getByLabel('标题').fill('E2E 创建的待办')
    await dialog.getByLabel('备注').fill('来自 Playwright')

    const createResponse = page.waitForResponse(r =>
      r.url().endsWith('/api/todos') && r.request().method() === 'POST' && r.status() === 201
    )
    await dialog.getByRole('button', { name: '创建' }).click()
    await createResponse

    // 对话框关闭
    await expect(dialog).toBeHidden()
    // 列表顶部出现新项
    await expect(page.getByText('E2E 创建的待办')).toBeVisible()
  })

  test('T4: 勾选 Pending 待办 → 标记完成', async ({ page }) => {
    test.info().annotations.push({ type: 'scenario', description: '勾选「整理本周周报」→ 该项显示「已完成」Tag，标题带删除线' })

    // 定位第一个 todo 项的 checkbox（aria-label="标记完成状态"）
    const firstItem = page.locator('.todo-item').filter({ hasText: '整理本周周报' })
    const checkbox = firstItem.getByRole('checkbox', { name: '标记完成状态' })

    const completeResponse = page.waitForResponse(r =>
      r.url().includes('/complete') && r.request().method() === 'POST' && r.status() === 200
    )
    await checkbox.click()
    await completeResponse

    // 该项出现「已完成」标签
    await expect(firstItem.getByText('已完成', { exact: true })).toBeVisible()
  })

  test('T5: 勾选 Completed 待办 → 重新打开', async ({ page }) => {
    test.info().annotations.push({ type: 'scenario', description: '勾选「历史已完成项」→ 该项「已完成」Tag 消失' })

    const completedItem = page.locator('.todo-item').filter({ hasText: '历史已完成项' })
    // 确认初始有「已完成」标签
    await expect(completedItem.getByText('已完成', { exact: true })).toBeVisible()

    const checkbox = completedItem.getByRole('checkbox', { name: '标记完成状态' })
    const reopenResponse = page.waitForResponse(r =>
      r.url().includes('/reopen') && r.request().method() === 'POST' && r.status() === 200
    )
    await checkbox.click()
    await reopenResponse

    // 「已完成」标签消失
    await expect(completedItem.getByText('已完成', { exact: true })).toBeHidden()
  })

  test('T6: 删除待办 - 取消删除', async ({ page }) => {
    test.info().annotations.push({ type: 'scenario', description: '点击删除 → 弹出确认框 → 点「取消」→ 待办仍在列表中' })

    const targetItem = page.locator('.todo-item').filter({ hasText: '整理本周周报' })
    await targetItem.getByRole('button', { name: '删除待办' }).click()

    // 确认框出现
    const msgBox = page.locator('.el-message-box')
    await expect(msgBox).toBeVisible()
    await msgBox.getByRole('button', { name: '取消' }).click()

    // 待办仍存在
    await expect(page.getByText('整理本周周报')).toBeVisible()
  })

  test('T7: 删除待办 - 确认删除', async ({ page }) => {
    test.info().annotations.push({ type: 'scenario', description: '点击删除 → 弹出确认框 → 点「删除」→ 待办消失' })

    const targetItem = page.locator('.todo-item').filter({ hasText: '整理本周周报' })
    await targetItem.getByRole('button', { name: '删除待办' }).click()

    const msgBox = page.locator('.el-message-box')
    await expect(msgBox).toBeVisible()

    const deleteResponse = page.waitForResponse(r =>
      r.url().match(/\/api\/todos\/\d+\/?$/) !== null && r.request().method() === 'DELETE' && r.status() === 204
    )
    await msgBox.getByRole('button', { name: '删除' }).click()
    await deleteResponse

    // 待办消失
    await expect(page.getByText('整理本周周报')).toBeHidden()
  })

  test('T8: 截图与控制台无报错', async ({ page, browserName }) => {
    test.info().annotations.push({ type: 'scenario', description: '页面加载后截图，控制台不应出现 error 级别日志' })

    const errors: string[] = []
    page.on('console', msg => {
      if (msg.type() === 'error') {
        errors.push(msg.text())
      }
    })
    page.on('pageerror', err => {
      errors.push(`pageerror: ${err.message}`)
    })

    // 重新加载页面以捕获所有 console 输出
    await page.reload()
    await expect(page.getByText('整理本周周报')).toBeVisible()

    // 截图（按浏览器区分，便于排查）
    await page.screenshot({
      path: `test-results/todo-${browserName}-list.png`,
      fullPage: true,
    })

    // 允许 404 mock 路由产生的网络错误日志，但不应有 JS 运行时错误
    const realErrors = errors.filter(e =>
      !e.includes('404') &&
      !e.includes('Failed to load resource') &&
      !e.includes('Mock 未覆盖')
    )
    expect(realErrors, `控制台报错: ${realErrors.join('\n')}`).toEqual([])
  })
})

// ============================================================
// 首页待办面板测试
// ============================================================

test.describe('首页 / 待办面板', () => {
  test.beforeEach(async ({ page }) => {
    await installMocks(page)
    // mock /api/skills 防止 home.init() 失败抛错
    await page.route('**/api/skills**', route => {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(ok([])),
      })
    })
    await page.goto('/')
    // 等待首页待办面板加载（至少看到「待办」标题）
    await expect(page.locator('.todo-panel').getByText('待办', { exact: true })).toBeVisible()
  })

  test('H1: 首页展示最近待处理待办', async ({ page }) => {
    test.info().annotations.push({ type: 'scenario', description: '首页右栏待办面板应展示最近 Pending 待办，不含 Completed 项' })

    const todoPanel = page.locator('.todo-panel')
    // 初始 mock 数据有 2 条 Pending（整理本周周报、完成 E2E 测试）+ 1 条 Completed
    await expect(todoPanel.getByText('整理本周周报')).toBeVisible()
    await expect(todoPanel.getByText('完成 E2E 测试')).toBeVisible()
    // Completed 项不应出现在首页面板
    await expect(todoPanel.getByText('历史已完成项')).toBeHidden()
  })

  test('H2: 点击「+」打开快捷添加对话框', async ({ page }) => {
    test.info().annotations.push({ type: 'scenario', description: '点击待办面板右上角「+」→ 弹出新建待办对话框' })

    const todoPanel = page.locator('.todo-panel')
    await todoPanel.getByRole('button', { name: '新建待办' }).click()

    const dialog = page.locator('.el-dialog').filter({ hasText: '新建待办' })
    await expect(dialog).toBeVisible()

    await dialog.getByLabel('标题').fill('首页快速添加的待办')
    const createResponse = page.waitForResponse(r =>
      r.url().endsWith('/api/todos') && r.request().method() === 'POST' && r.status() === 201
    )
    await dialog.getByRole('button', { name: '创建' }).click()
    await createResponse

    // 对话框关闭，新待办出现在首页面板顶部
    await expect(dialog).toBeHidden()
    await expect(todoPanel.getByText('首页快速添加的待办')).toBeVisible()
  })

  test('H3: 点击「查看全部」跳转到 /todo', async ({ page }) => {
    test.info().annotations.push({ type: 'scenario', description: '点击待办面板右上角「>」箭头 → 跳转到 /todo 页面' })

    await page.locator('.todo-panel').getByRole('button', { name: '查看全部待办' }).click()

    await expect(page).toHaveURL(/\/todo$/)
    await expect(page.getByRole('heading', { name: '待办追踪' })).toBeVisible()
  })

  test('H4: 勾选首页待办 → 调用 complete 接口', async ({ page }) => {
    test.info().annotations.push({ type: 'scenario', description: '在首页勾选某条待办 → 触发 complete API → 该项从首页面板消失' })

    const todoPanel = page.locator('.todo-panel')
    const itemCheckbox = todoPanel.locator('.todo-item input[type="checkbox"]').first()

    const completeResponse = page.waitForResponse(r =>
      r.url().includes('/complete') && r.request().method() === 'POST' && r.status() === 200
    )
    await itemCheckbox.click()
    await completeResponse

    // 标记完成后，该 Pending 项不再出现在首页（首页只显示 Pending）
    // 由于刷新逻辑依赖 store 重新查询，等待列表更新
    await expect(todoPanel.getByText('整理本周周报')).toBeHidden()
  })

  test('H5: 截图与控制台无报错', async ({ page, browserName }) => {
    test.info().annotations.push({ type: 'scenario', description: '首页加载后截图，控制台不应出现 JS 运行时错误' })

    const errors: string[] = []
    page.on('pageerror', err => {
      errors.push(`pageerror: ${err.message}`)
    })

    await page.reload()
    await expect(page.locator('.todo-panel')).toBeVisible()

    await page.screenshot({
      path: `test-results/todo-${browserName}-home.png`,
      fullPage: true,
    })

    expect(errors, `JS 运行时错误: ${errors.join('\n')}`).toEqual([])
  })
})
