import { test, expect, type Page } from '@playwright/test'
import { mkdirSync, readFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { getRealApiKey, injectRealApiKey } from '../../helpers/real-auth'
import { backendUrl } from '../../helpers/e2e-env'

const MANIFEST = JSON.parse(
  readFileSync(fileURLToPath(new URL('../../../../Plugins/TodoTracker/plugin.json', import.meta.url)), 'utf-8'),
) as { Id: string; Version: string; frontend: { route: string; entry?: string } }
const BACKEND = backendUrl()
const API = `${BACKEND}/api/todos`
const AUTH = { Authorization: `Bearer ${getRealApiKey()}`, 'Content-Type': 'application/json' }
const SHOTS = path.resolve(fileURLToPath(new URL('../../../screenshots/e2e/todo-tracker', import.meta.url)))

async function api<T>(urlPath: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${BACKEND}${urlPath}`, { ...init, headers: { ...AUTH, ...(init?.headers ?? {}) } })
  const body = await response.text()
  if (!response.ok) throw new Error(`${init?.method ?? 'GET'} ${urlPath} → ${response.status} ${body.slice(0, 200)}`)
  return (JSON.parse(body) as { data: T }).data
}

async function removeTask(id: number): Promise<void> {
  await fetch(`${API}/${id}`, { method: 'DELETE', headers: AUTH })
}

async function openTodoPage(page: Page): Promise<void> {
  await injectRealApiKey(page)
  await page.goto(MANIFEST.frontend.route)
  await expect(page.getByRole('heading', { name: '待办任务' })).toBeVisible()
}

test.describe('TodoTracker 插件主链路冒烟', () => {
  test.describe.configure({ timeout: 120_000 })

  test('真实宿主加载、流水线筛选、看板流转与抽屉渐进披露', async ({ page }) => {
    const entry = MANIFEST.frontend.entry ?? ''
    expect(entry).toContain('web/dist/index.js')
    const fatal: string[] = []
    const entryResponses: string[] = []
    page.on('console', message => {
      if (message.type() === 'error' && /Failed to resolve component|does not provide an export named|Failed to (fetch|resolve) dynamically imported module|Failed to load module script/i.test(message.text())) {
        fatal.push(message.text())
      }
    })
    page.on('response', response => {
      if (response.url().includes(`/plugins/${MANIFEST.Id}/`)) {
        entryResponses.push(`${response.status()} ${response.headers()['content-type']} ${response.url()}`)
      }
    })

    const title = `冒烟-看板闭环-${Date.now()}`
    const task = await api<{ id: number }>('/api/todos', { method: 'POST', body: JSON.stringify({ title }) })
    try {
      await openTodoPage(page)
      await expect(page.locator('.tt-root')).toBeVisible()
      await expect(page.locator('.tt-version')).toHaveText(new RegExp(`^v${MANIFEST.Version.replace(/\./g, '\\.')}$`))
      await expect.poll(() => entryResponses.find(value => value.includes(entry)) ?? '').toMatch(/^200 .*javascript/)

      await page.locator('.tt-pipeline-step', { hasText: '草稿' }).click()
      await expect(page.locator('.tt-item', { hasText: title })).toBeVisible()
      // PILOT-085 身份锚定：列表行 #<id> 徽标与 REST 回读的 TodoDto.id 相等（agent 引用的就是该数字）
      const listRow = page.locator('.tt-item', { hasText: title })
      await expect(listRow.locator('.tt-tid')).toHaveText(`#${task.id}`)
      // 软提示：中性「下发前待补齐」，非危险色「还缺」冒充失败
      await expect(page.locator('.tt-item', { hasText: title }).locator('[data-test="list-soft-hint"]')).toContainText('下发前待补齐')
      await expect(page.locator('.tt-item', { hasText: title }).locator('[data-test="list-soft-hint"]')).not.toContainText(/^还缺/)

      await page.locator('.tt-view-switch').getByRole('button', { name: '列表' }).click()
      await page.locator('.tt-item', { hasText: title }).locator('select[aria-label="流转阶段"]').selectOption('Ready')
      await expect.poll(async () => (await api<{ stage: string }>(`/api/todos/${task.id}`)).stage).toBe('Ready')
      await page.locator('.tt-pipeline-step', { hasText: '草稿' }).click()

      await page.locator('.tt-view-switch').getByRole('button', { name: '看板' }).click()
      await expect(page.locator('.tt-pipeline-step').filter({ hasText: '就绪' }).locator('b')).toHaveText('1')
      const card = page.locator('.tt-board-card', { hasText: title })
      await expect(card).toBeVisible()
      // PILOT-085：看板卡同样锚定 #<id>
      await expect(card.locator('.tt-tid')).toHaveText(`#${task.id}`)
      await card.click()

      const drawer = page.locator('[data-test="task-drawer"]')
      await expect(drawer).toBeVisible()
      await expect(drawer.locator('#td-title')).toHaveValue(title)
      // PILOT-085：详情抽屉头部 #<id>（与 Guid taskKey 并排）
      await expect(drawer.locator('[data-test="td-tid"]')).toHaveText(`#${task.id}`)
      // 分类折叠头首屏可见；工作单元默认收起
      await expect(drawer.locator('[data-test="section-basic"] .td-section-head')).toBeVisible()
      await expect(drawer.locator('[data-test="section-workunit"] .td-section-head')).toBeVisible()
      await expect(drawer.locator('[data-test="section-dispatch"] .td-section-head')).toBeVisible()
      await expect(drawer.locator('[data-test="section-history"] .td-section-head')).toBeVisible()
      await expect(drawer.locator('#td-obj')).toHaveCount(0)

      // 展开下发与委派 → 点下发缺项 → 工作单元展开 + 点名
      await drawer.locator('[data-test="section-dispatch"] .td-section-head').click()
      await expect(drawer.locator('[data-test="delegate-engine"]')).toBeVisible()
      await drawer.locator('[data-test="dispatch-btn"]').click()
      await expect(drawer.locator('#td-obj')).toBeVisible()
      await expect(drawer.locator('[data-test="workunit-missing"]')).toContainText('下发前还缺')

      await drawer.locator('[data-test="section-history"] .td-section-head').click()
      await expect(drawer.locator('[data-test="execution-timeline"]')).toBeVisible()

      mkdirSync(SHOTS, { recursive: true })
      await page.screenshot({ path: path.join(SHOTS, 'todo-tracker-main-flow.png'), fullPage: false })
      expect(fatal, `插件入口加载异常：${fatal.join('；')}`).toEqual([])
    } finally {
      await removeTask(task.id)
    }
  })
})
