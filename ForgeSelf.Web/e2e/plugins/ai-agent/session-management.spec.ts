import { test, expect, type APIRequestContext } from '@playwright/test'
import { mkdirSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { randomUUID } from 'node:crypto'
import { getRealApiKey } from '../../helpers/real-auth'

/**
 * AIAgent 会话管理（切换 / 归档 / 收起）插件层 e2e —— 走 e2e-testing 技能统一 globalSetup 全新宿主。
 *
 * 零 mock，对接真实后端 + 前端 dev。覆盖 T4 会话管理改造的端到端行为：
 *  1. 经真实后端 API 创建两个会话（自带全 id，杜绝 T3 前缀 bug 回归）；
 *  2. UI 历史会话分组**默认收起**（不渲染会话项）；
 *  3. 展开后列出会话项；归档图标**默认 opacity 0、悬浮才显示**（真实 getComputedStyle 值，非 CSS 文本断言）；
 *  4. 点击切换 → 当前项高亮 + 历史消息加载（用户消息可见，全 id 命中）；
 *  5. 点归档 → **先弹二次确认**（回归守卫：用户反馈「点一下就归档了」）；
 *     取消 → 会话仍在列表且后端仍未归档（一个请求都不发）；
 *     确认 → 该会话从 agent 页列表消失；后端 `?archived=active` 不含、`?archived=all` 含且 archived=true，
 *     且其历史消息**仍在**（软标记 ≠ 删除）；
 *  6. 截图读图（视觉检查）；收尾经 API 硬删两个测试会话清理现场。
 *
 * 会话创建经 POST /api/ai-agent/chat：用户消息在 agent 循环前已落库，故即便本地 LLM 不可用，
 * 会话仍被创建（仅 assistant 回复缺失），本用例不依赖可用模型。
 *
 * 运行：
 *   pnpm exec playwright test --config=playwright.config.ts e2e/plugins/ai-agent/session-management.spec.ts
 */
const PLUGIN_ROUTE = '/ai-agent'

const OUT_DIR = path.resolve(
  fileURLToPath(new URL('../../../screenshots/e2e/ai-agent', import.meta.url)),
)
mkdirSync(OUT_DIR, { recursive: true })

/** 经真实后端创建会话（自带全 id）。容忍 agent 循环超时/失败：用户消息已先落库。 */
async function seedSession(
  request: APIRequestContext,
  auth: Record<string, string>,
  sessionId: string,
  message: string,
): Promise<void> {
  try {
    await request.post('/api/ai-agent/chat', {
      headers: { ...auth, 'Content-Type': 'application/json' },
      data: { message, sessionId },
      timeout: 30_000,
    })
  } catch {
    // agent 循环可能超时/失败，但用户消息在循环前已落库 → 会话必已创建，忽略异常。
  }
}

/** 读取某端点返回的会话 id 列表（可带 archived 筛选）。 */
async function sessionIds(
  request: APIRequestContext,
  auth: Record<string, string>,
  archived: string,
): Promise<string[]> {
  const res = await request.get(`/api/ai-agent/chat/sessions?archived=${archived}`, { headers: auth })
  const list = (await res.json()) as Array<{ sessionId: string }>
  return list.map((s) => s.sessionId)
}

test.describe('AIAgent 会话管理（收起/切换/归档）实跑宿主 e2e', () => {
  const token = getRealApiKey()
  const auth = { Authorization: `Bearer ${token}` }

  test('默认收起 → 展开 → 悬浮显归档图标 → 切换加载历史 → 归档后从列表消失且消息仍在', async ({
    page,
    request,
  }) => {
    const serverErrors: string[] = []
    const pageErrors: string[] = []
    page.on('pageerror', (e) => pageErrors.push(e.message))
    page.on('response', (r) => {
      if (r.status() >= 500 && r.url().includes('/api/ai-agent')) serverErrors.push(`${r.status()} ${r.url()}`)
    })

    // 1. 创建两个会话（自带全 id）
    const sessionA = `e2e-a-${randomUUID()}`
    const sessionB = `e2e-b-${randomUUID()}`
    await seedSession(request, auth, sessionA, '你好，这是会话A的测试消息')
    await seedSession(request, auth, sessionB, '你好，这是会话B的测试消息')

    // 2. 真实后端：未归档列表含两个全 id
    const activeIds = await sessionIds(request, auth, 'active')
    expect(activeIds, `未归档列表应含两全 id: ${JSON.stringify(activeIds)}`).toContain(sessionA)
    expect(activeIds).toContain(sessionB)

    // 3. 打开插件 UI：历史会话分组默认收起 → 不渲染任何会话项
    await page.goto(PLUGIN_ROUTE)
    await expect(page.locator('.sess__head-toggle')).toBeVisible({ timeout: 20000 })
    await expect(page.locator('.sess__item')).toHaveCount(0)
    await expect(page.locator('.sess__chevron--collapsed')).toHaveCount(1)

    // 4. 展开 → 两个会话项出现
    await page.locator('.sess__head-toggle').click()
    const itemA = page.locator(`.sess__item[title="${sessionA}"]`)
    const itemB = page.locator(`.sess__item[title="${sessionB}"]`)
    await expect(itemA).toBeVisible()
    await expect(itemB).toBeVisible()

    // 5. 归档图标：默认不可见（opacity 0），鼠标悬浮整行才显示（真实计算样式）
    const archiveBtn = itemA.locator('.sess__item-archive')
    expect(await archiveBtn.evaluate((el) => getComputedStyle(el).opacity)).toBe('0')
    expect(await archiveBtn.evaluate((el) => getComputedStyle(el).width)).toBe('18px')
    await itemA.hover()
    await expect
      .poll(async () => archiveBtn.evaluate((el) => getComputedStyle(el).opacity), {
        message: '悬浮会话项后归档图标应可见',
      })
      .toBe('1')

    // 6. 切换 sessionA → 当前项高亮 + 历史消息加载（用户消息可见，证明全 id 命中）
    await itemA.click()
    await expect(page.locator(`.sess__item--active[title="${sessionA}"]`)).toBeVisible()
    await expect(page.getByText('你好，这是会话A的测试消息')).toBeVisible()

    // 7. 归档**先弹二次确认**（回归守卫：用户反馈「点击归档没有确认，直接归档了」）
    await itemA.hover()
    await archiveBtn.click()
    const confirmBox = page.locator('.el-message-box')
    await expect(confirmBox).toBeVisible({ timeout: 10000 })
    await expect(confirmBox).toContainText('归档确认')
    // 文案必须如实说明「软标记」语义，不能让人误以为等于删除
    await expect(confirmBox).toContainText('消息会完整保留')

    // 8. 取消 → 会话仍在列表，且后端仍未归档（取消不得发起归档请求）
    await confirmBox.getByRole('button', { name: '取消' }).click()
    await expect(confirmBox).toHaveCount(0)
    await expect(page.locator(`.sess__item[title="${sessionA}"]`)).toBeVisible()
    expect(
      await sessionIds(request, auth, 'active'),
      '取消确认后不得归档（回归：点一下就归档）',
    ).toContain(sessionA)

    // 9. 再点归档并确认 → 从 agent 页列表消失
    await itemA.hover()
    await archiveBtn.click()
    await expect(confirmBox).toBeVisible()
    await confirmBox.getByRole('button', { name: '归档' }).click()
    await expect(page.locator(`.sess__item[title="${sessionA}"]`)).toHaveCount(0, { timeout: 10000 })
    await expect(itemB).toBeVisible()

    // 10. 截图读图（视觉检查）
    await page.screenshot({ path: path.join(OUT_DIR, 'session-archive.png'), fullPage: false })

    // 11. 真实后端复核：active 不含 A；all 含 A 且 archived=true；A 的历史消息仍在（软标记）
    const activeAfter = await sessionIds(request, auth, 'active')
    expect(activeAfter, `归档后未归档列表不应含 A: ${JSON.stringify(activeAfter)}`).not.toContain(sessionA)
    expect(activeAfter).toContain(sessionB)

    const allRes = await request.get('/api/ai-agent/chat/sessions?archived=all', { headers: auth })
    const allList = (await allRes.json()) as Array<{ sessionId: string; archived: boolean }>
    const rowA = allList.find((s) => s.sessionId === sessionA)
    expect(rowA, `全部列表应含已归档的 A: ${JSON.stringify(allList)}`).toBeTruthy()
    expect(rowA!.archived).toBe(true)

    const historyRes = await request.get(
      `/api/ai-agent/chat/history/${encodeURIComponent(sessionA)}?limit=50`,
      { headers: auth },
    )
    const history = (await historyRes.json()) as Array<{ content?: string }>
    expect(
      history.some((m) => (m.content ?? '').includes('会话A的测试消息')),
      '归档是软标记，不该删除消息',
    ).toBe(true)

    // 12. 收尾：经 API 硬删两个测试会话（同时验证删除端点仍可用），不留脏数据
    for (const id of [sessionA, sessionB]) {
      const del = await request.delete(`/api/ai-agent/chat/session/${encodeURIComponent(id)}`, {
        headers: auth,
      })
      expect(del.ok(), `清理会话失败: ${id} -> ${del.status()}`).toBe(true)
    }

    // 13. 无未捕获 JS 异常；ai-agent 端点无 500
    expect(pageErrors, `未捕获 JS 异常: ${pageErrors.join(' | ')}`).toHaveLength(0)
    expect(serverErrors, `ai-agent 端点服务端错误: ${serverErrors.join(' | ')}`).toHaveLength(0)
  })
})
