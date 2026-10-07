import { test, expect, type Page } from '@playwright/test'
import { getRealApiKey, injectRealApiKey } from './helpers/real-auth'
import { backendUrl } from './helpers/e2e-env'

/**
 * 首页待办面板 E2E（应用层视角）—— 对接真实后端 API（无 mock、真实认证）。
 *
 * 归属说明（PILOT-054）：`/todo` 页面本身的用例已随界面迁移改由插件层 e2e 负责
 * （`e2e/plugins/todo-tracker/todo-tracker.spec.ts`）；本文件只保留**首页面板**这一应用层视角 ——
 * 它测的是 Home 插件如何消费 todo-tracker 的公开 API，属于跨插件集成，不归任一插件自己的 e2e。
 *
 * 设计原则：
 * - **零 mock**：不拦截任何 /api/* 请求，全部走真实后端
 * - **真实认证**：注入 ForgeSetting.config 解密出的真实 API 密钥
 * - **直连 API 也必须带 token**：PILOT-054 起 `api/todos*` 全部加了 `[Authorize("ApiKeyPolicy")]`
 *   （铁律 17），浏览器里的界面由宿主注入 token 所以照常工作，但**本文件里自己发的 fetch 不会**——
 *   不带就是 401 空响应体，`res.json()` 会炸成 "Unexpected end of JSON input" 把成因糊掉。
 *   因此统一走 `todoJson()`：非 JSON 响应直接把状态码 + 原文抛出来。
 * - **数据隔离**：每个用例用唯一名称（E2E-Real-*）创建自己的待办，用例结束立即删除
 * - **地址取自 e2e-env**：端口由 globalSetup 动态派生（PILOT-050 起禁止硬编码 7102/7002）
 *
 * 覆盖范围：
 * - 首页 `/` 待办面板：展示最近待办、快捷添加、跳转 /todo、勾选完成
 * - 控制台无报错、UI 截图
 */

// ============================================================
// 辅助函数
// ============================================================

const BACKEND_URL = backendUrl()
const TODOS_BASE = `${BACKEND_URL}/api/todos`
const AUTH_HEADER = { Authorization: `Bearer ${getRealApiKey()}` }

/** 读 JSON，且把"非 JSON 的失败响应"原样端出来（401/404 的空体最容易糊掉成因）。 */
async function todoJson<T>(res: Response): Promise<T> {
  const text = await res.text()
  if (!text) throw new Error(`${res.status} 空响应体（需鉴权的端点没带 token 就是这个形态）`)
  try {
    return JSON.parse(text) as T
  } catch {
    throw new Error(`${res.status} 响应不是 JSON：${text.slice(0, 200)}`)
  }
}

/** 通过真实后端 API 创建待办，返回 id */
async function createTodo(title: string, remark = ''): Promise<number> {
  const res = await fetch(TODOS_BASE, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', ...AUTH_HEADER },
    body: JSON.stringify({ title, remark }),
  })
  const json = await todoJson<{ success: boolean; data?: { id: number } }>(res)
  if (!res.ok || !json.success || !json.data) {
    throw new Error(`创建待办失败: ${res.status} ${JSON.stringify(json)}`)
  }
  return json.data.id
}

/** 通过真实后端 API 标记完成（用于构造 Completed 测试数据） */
async function completeTodo(id: number): Promise<void> {
  const res = await fetch(`${TODOS_BASE}/${id}/complete`, { method: 'POST', headers: AUTH_HEADER })
  if (!res.ok) throw new Error(`标记完成失败: ${res.status} ${await res.text()}`)
}

/** 通过真实后端 API 删除测试待办（忽略 404） */
async function deleteTodo(id: number): Promise<void> {
  try {
    await fetch(`${TODOS_BASE}/${id}`, { method: 'DELETE', headers: AUTH_HEADER })
  } catch {
    // 清理阶段失败不影响用例结果，由 finally 兜底
  }
}

/**
 * 记录**浏览器里**首页真实打过的后端响应。
 * 「暂无待处理待办」这一种界面表现至少有三种成因（401 鉴权 / 500 / 200 但空表），
 * 只看 DOM 分不出来；而"一次请求都没发"是第四种成因（首页聚合根本没跑起来），
 * 所以这里收的是**全部 /api/ 响应**，不只 todos —— 用来区分"被拒"和"根本没发起"。
 */
function trackApiRequests(page: Page, sink: string[]): void {
  page.on('response', r => {
    const url = r.url()
    if (!url.includes('/api/')) return
    r.text()
      .then(body => sink.push(`${r.request().method()} ${r.status()} ${url.slice(url.indexOf('/api/')).slice(0, 90)} ⇒ ${body.slice(0, 120)}`))
      .catch(() => sink.push(`${r.request().method()} ${r.status()} ${url.slice(url.indexOf('/api/')).slice(0, 90)} （响应体读取失败）`))
  })
}

/** 从收集到的响应里挑出 todos 相关的那几条（失败消息太长会淹没重点）。 */
function todosOnly(sink: string[]): string {
  const hits = sink.filter(l => l.includes('/api/todos'))
  return hits.length ? hits.join(' | ') : `（无 todos 请求；首页共发了 ${sink.length} 个 /api/ 请求）`
}

// ============================================================
// /todo 页面本身的用例已随 PILOT-054 迁到插件层 e2e：
//   e2e/plugins/todo-tracker/todo-tracker.spec.ts
// （界面已从宿主 src/views 迁到 Plugins/TodoTracker/web/，本文件只保留首页面板这一应用层视角。）
// ============================================================


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
    const seen: string[] = []
    trackApiRequests(page, seen)
    try {
      await page.goto('/')
      const todoPanel = page.locator('.todo-panel')
      await expect(todoPanel.getByText('待办', { exact: true })).toBeVisible()

      // 首页面板只显示 Pending 项
      await expect(todoPanel.getByText(pendingTitle, { exact: true }),
        `面板没列出新建的待办；浏览器实际响应：${todosOnly(seen)}`).toBeVisible()
      await expect(todoPanel.getByText(completedTitle, { exact: true })).toBeHidden()
    } finally {
      await deleteTodo(pendingId)
      await deleteTodo(completedId)
    }
  })

  test('H2: 点击「新建待办」打开快捷添加对话框并创建', async ({ page }) => {
    const seen: string[] = []
    trackApiRequests(page, seen)
    await page.goto('/')
    const todoPanel = page.locator('.todo-panel')
    await expect(todoPanel.getByText('待办', { exact: true })).toBeVisible()

    await todoPanel.getByRole('button', { name: '新建待办' }).click()
    // 弹窗是 Home 插件自带的 `.todo-modal`（role=dialog，占位符「待办标题」，提交按钮「保存」）；
    // 本用例原先按**宿主旧版**写死 `.el-dialog` + 「请输入待办标题」+「创建」，Home 界面迁到插件 web 后就一直是陈旧红
    // （证据：`Plugins/Home/web/src/HomeView.vue:569-597` 的文案与旧选择器不一致，且本批未改 `Plugins/Home/**`）。
    const dialog = page.getByRole('dialog', { name: '新建待办' })
    await expect(dialog).toBeVisible()

    const title = `E2E-Real-H2-${Date.now()}`
    await dialog.getByPlaceholder('待办标题').fill(title)
    const createResponse = page.waitForResponse(
      (r) => r.url().endsWith('/api/todos') && r.request().method() === 'POST' && r.status() === 201
    )
    await dialog.getByRole('button', { name: '保存' }).click()
    await createResponse

    // 对话框关闭，新待办出现在首页面板
    await expect(dialog).toBeHidden()
    await expect(todoPanel.getByText(title, { exact: true })).toBeVisible()

    // 清理：从列表读取 id 并删除
    const list = await todoJson<{ data: { items: { id: number; title: string }[] } }>(
      await fetch(`${TODOS_BASE}?page=1&pageSize=20`, { headers: AUTH_HEADER }),
    )
    const created = list.data.items.find((t) => t.title === title)
    if (created) await deleteTodo(created.id)
  })

  test('H3: 点击「查看全部」跳转到 /todo', async ({ page }) => {
    await page.goto('/')
    const todoPanel = page.locator('.todo-panel')
    await expect(todoPanel.getByText('待办', { exact: true })).toBeVisible()

    await todoPanel.getByRole('button', { name: '查看全部待办' }).click()
    await expect(page).toHaveURL(/\/todo$/)
    // /todo 自 PILOT-054 起由插件自带界面渲染（宿主内置页已删），标题以插件页面为准
    await expect(page.getByRole('heading', { name: '待办任务' })).toBeVisible()
  })

  test('H4: 勾选首页待办 → 调用 complete 接口并消失', async ({ page }) => {
    const title = `E2E-Real-H4-${Date.now()}`
    const id = await createTodo(title)
    const seen: string[] = []
    trackApiRequests(page, seen)
    try {
      await page.goto('/')
      const todoPanel = page.locator('.todo-panel')
      const item = todoPanel.locator('.todo-item').filter({ hasText: title })
      await expect(item, `面板没有这条待办；浏览器实际响应：${todosOnly(seen)}`).toBeVisible()

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
