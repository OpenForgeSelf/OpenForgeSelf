/**
 * AIAgent 聊天助手 · 消息组件视觉回归（CT）
 *
 * 被测的是插件真实聊天链路的 MessageItem（工具卡 / 失败续跑 / 用户气泡），
 * 由宿主薄壳 `AIAgentMessageStoryHost.vue` 挂进画廊。
 *
 * 运行：pnpm run test:ct        更新基线：pnpm run test:ct:update
 */
import { test, expect } from '@playwright/test'
import { openStory } from './ct-support'

const host = (page: import('@playwright/test').Page) => page.locator('.aiw-story-host')
const bubble = (page: import('@playwright/test').Page) => page.locator('.aiw-msg__bubble')

test.describe('AIAgent 聊天消息 · 视觉与契约', () => {
  test('助手纯文本：气泡渲染 Markdown 结果、无工具卡', async ({ page }) => {
    await openStory(page, 'AIAgent/MessageText', '.aiw-msg')

    await expect(page.locator('.aiw-msg')).toHaveClass(/aiw-msg--bot/)
    await expect(bubble(page)).toContainText('目录扫描')
    await expect(page.locator('.aiw-tool')).toHaveCount(0)

    await expect(host(page)).toHaveScreenshot()
  })

  test('工具卡与正文共存：两者必须同时出现（回归守卫）', async ({ page }) => {
    await openStory(page, 'AIAgent/MessageToolAndText', '.aiw-msg')

    // 工具卡在
    await expect(page.locator('.aiw-tool')).toHaveCount(1)
    await expect(page.locator('.aiw-tool__name')).toHaveText('run_terminal_command')
    // 成功态不写状态词（2026-10-26 形态）：一次调用成功没有可读信息，写出来只是噪声
    await expect(page.locator('.aiw-tool__status')).toHaveCount(0)
    // 正文也在 —— 曾经的 v-if/v-else 会把它整段吞掉，只留一张工具卡
    await expect(bubble(page)).toContainText('命令已执行完毕')

    await expect(host(page)).toHaveScreenshot()
  })

  /* ---------- 左列对齐：图标槽（2026-10-26 用户亲指「悬浮多出箭头，左边不齐」） ----------
     几何断言是这个缺陷的根治手段：只看 DOM 结构看不出「多出来的那一个箭头」，
     但看得出了 0.5px 以上的位移。所以这里量真实 boundingBox。 */
  test('工具行左列对齐：图标同一竖线、与正文左缘对齐、悬浮不位移', async ({ page }) => {
    await openStory(page, 'AIAgent/MessagesToolRows', '.aiw-msg')

    const slots = page.locator('.aiw-tool__head .aiw-ico')
    const heads = page.locator('.aiw-tool__head')
    await expect(slots).toHaveCount(2)

    const first = await slots.nth(0).boundingBox()
    const second = await slots.nth(1).boundingBox()
    expect(first && second).toBeTruthy()
    // 1) 两条工具行的图标左缘在同一条竖线
    expect(Math.abs(first!.x - second!.x)).toBeLessThan(0.6)
    // 2) 图标左缘与正文文字左缘对齐（两侧都是 --aiw-space-5 = 12px 内边距）
    const box = await bubble(page).boundingBox()
    expect(Math.abs(first!.x - (box!.x + 12))).toBeLessThan(1)

    // 3) 悬浮第二行：图标槽原位不动（改前箭头是独立 flex 元素，会把图标顶右移 15px）
    await heads.nth(1).hover()
    const hovered = await slots.nth(1).boundingBox()
    expect(Math.abs(hovered!.x - second!.x)).toBeLessThan(0.6)

    // 4) 悬浮后是「同一格换皮」：图标淡出、箭头淡入（叠放切换，不新增元素）
    const glyph = heads.nth(1).locator('.aiw-ico__glyph')
    const chev = heads.nth(1).locator('.aiw-ico__chev')
    await expect.poll(async () => Number(await glyph.evaluate((el) => getComputedStyle(el).opacity))).toBe(0)
    expect(await chev.evaluate((el) => Number(getComputedStyle(el).opacity))).toBeGreaterThan(0)

    // 5) 已展开的第一行：箭头常驻（提示可收回）
    const openSlot = heads.nth(0).locator('.aiw-ico')
    await expect(openSlot).toHaveClass(/is-open/)
    await expect.poll(async () => Number(await openSlot.locator('.aiw-ico__chev').evaluate((el) => getComputedStyle(el).opacity))).toBeGreaterThan(0)

    await expect(host(page)).toHaveScreenshot()
  })

  /* ---------- 边框归属：只工具调用有框（2026-10-26 用户拍板） ---------- */
  test('边框归属：正文无框无阴影，折叠行无框，仅展开的工具体有框', async ({ page }) => {
    await openStory(page, 'AIAgent/MessagesToolRows', '.aiw-msg')

    const bubbleStyle = await bubble(page).evaluate((el) => {
      const s = getComputedStyle(el)
      return { border: s.borderTopWidth, shadow: s.boxShadow }
    })
    expect(bubbleStyle.border).toBe('0px')
    expect(bubbleStyle.shadow === 'none' || bubbleStyle.shadow === '').toBe(true)

    // 折叠态的工具行本身无框
    const foldedRow = await page.locator('.aiw-tool').nth(1).evaluate((el) => getComputedStyle(el).borderTopWidth)
    expect(foldedRow).toBe('0px')

    // 展开体的框是唯一保留的那个（输入 / 输出卡片）
    const bodyBorder = await page
      .locator('.aiw-tool')
      .nth(0)
      .locator('.aiw-tool__body')
      .evaluate((el) => getComputedStyle(el).borderTopWidth)
    expect(bodyBorder).toBe('1px')

    // 思考行也无框（过程行不再套框）
    const thinkBorder = await page.locator('.aiw-think').evaluate((el) => getComputedStyle(el).borderTopWidth)
    expect(thinkBorder).toBe('0px')
  })

  test('失败可续跑：显示失败原因 +「继续执行」与「重试」', async ({ page }) => {
    await openStory(page, 'AIAgent/MessageFailed', '.aiw-msg')

    const status = page.locator('.aiw-msg__status')
    await expect(status).toHaveClass(/is-failed/)
    await expect(status).toContainText('生成失败 · 连接中断')
    await expect(status.getByRole('button', { name: '继续执行' })).toBeVisible()
    await expect(status.getByRole('button', { name: '重试' })).toBeVisible()

    await expect(host(page)).toHaveScreenshot()
  })

  test('用户消息：右对齐、无机器人头像', async ({ page }) => {
    await openStory(page, 'AIAgent/MessageUser', '.aiw-msg')

    await expect(page.locator('.aiw-msg')).toHaveClass(/aiw-msg--user/)
    await expect(page.locator('.aiw-msg__avatar')).toHaveCount(0)
    await expect(bubble(page)).toContainText('看一下当前项目结构')

    await expect(host(page)).toHaveScreenshot()
  })
})
