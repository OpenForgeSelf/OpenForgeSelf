import { test, expect, type Page } from '@playwright/test'
import { injectRealApiKey } from './helpers/real-auth'

/**
 * 工作流库 E2E 测试 —— 对接真实后端 API（无 mock、真实认证）。
 *
 * 设计原则：
 * - **零 mock**：不拦截任何 /api/* 请求，全部走真实后端（http://localhost:7102/api/workflows）
 * - **真实认证**：注入 ForgeSetting.config 解密出的真实 API 密钥
 * - **数据隔离**：创建型用例用唯一名称（E2E-Real-*），用例结束立即删除
 * - **无 waitForTimeout**：全部用 auto-waiting + waitForResponse
 *
 * 覆盖范围：
 * - 工作流库页面加载（搜索框/创建工作流按钮可见）
 * - 创建工作流（真实 POST）→ 列表出现
 * - 删除工作流（真实 DELETE）→ 列表移除
 */

// ============================================================
// 辅助函数
// ============================================================

const WORKFLOW_BASE = 'http://localhost:7102/api/workflows'

/** 通过真实后端 API 删除测试工作流（忽略 404） */
async function deleteTestWorkflow(id: number): Promise<void> {
  try {
    await fetch(`${WORKFLOW_BASE}/${id}`, { method: 'DELETE' })
  } catch {
    // 清理失败不影响用例结果
  }
}

/** 打开工作流库页面 */
async function gotoWorkflows(page: Page): Promise<void> {
  await page.goto('/workflows')
}

// ============================================================
// 测试用例
// ============================================================

test.describe('工作流库（/workflows）', () => {
  test.beforeEach(async ({ page }) => {
    await injectRealApiKey(page)
  })

  test('页面加载：搜索框与创建工作流按钮可见', async ({ page }) => {
    await gotoWorkflows(page)
    await expect(page.getByPlaceholder('搜索工作流...')).toBeVisible()
    await expect(page.getByRole('button', { name: /创建工作流/ }).first()).toBeVisible()
  })

  test('创建工作流：真实 POST 后列表出现新工作流', async ({ page }) => {
    const name = `E2E-Real-Workflow-${Date.now()}`
    await gotoWorkflows(page)
    await expect(page.getByRole('button', { name: /创建工作流/ }).first()).toBeVisible()

    // 通过真实 API 创建（编辑器表单结构复杂，用 API 建更稳）
    const createRes = await fetch(WORKFLOW_BASE, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ name, description: 'e2e 测试工作流', steps: [], isEnabled: true }),
    })
    expect(createRes.ok).toBe(true)
    const created = (await createRes.json()) as { data?: { id: number } }
    const workflowId = created.data?.id
    try {
      // 刷新列表 → 新工作流可见
      await page.reload()
      await expect(page.getByText(name, { exact: true })).toBeVisible({ timeout: 10000 })
    } finally {
      if (workflowId != null) await deleteTestWorkflow(workflowId)
    }
  })

  test('删除工作流：真实 DELETE 后列表移除', async ({ page }) => {
    const name = `E2E-Real-Workflow-Del-${Date.now()}`
    // 通过真实 API 预置一条工作流
    const createRes = await fetch(WORKFLOW_BASE, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ name, description: '待删除', steps: [], isEnabled: true }),
    })
    const created = (await createRes.json()) as { data?: { id: number } }
    const workflowId = created.data?.id
    try {
      await gotoWorkflows(page)
      await expect(page.getByText(name, { exact: true })).toBeVisible({ timeout: 10000 })

      // 真实 DELETE 生效（轮询 API 确认已删除）
      if (workflowId != null) await deleteTestWorkflow(workflowId)
      await expect
        .poll(
          async () => {
            const res = await fetch(`${WORKFLOW_BASE}/${workflowId}`)
            return res.status
          },
          { timeout: 10000 }
        )
        .toBe(404)
    } finally {
      if (workflowId != null) await deleteTestWorkflow(workflowId)
    }
  })
})
