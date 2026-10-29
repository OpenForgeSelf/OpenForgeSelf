import { test, expect, type Page } from '@playwright/test'
import { mkdirSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { injectRealApiKey } from '../../helpers/real-auth'

/**
 * 运行实例（:51888）只读/主链路面验收：AIAgent 工具卡修复批。
 *
 * 驱动方式：playwright.live.config.ts（baseURL=http://localhost:51888，不另起宿主、不起 webServer），
 * 与隔离实例的 ai-agent.spec.ts 分开，靠文件名 *.live.spec.ts 区分：
 *   - 默认 playwright.config.ts 已 testIgnore 本文件（否则会拿 7102 隔离实例跑，靶子不对）；
 *   - live 配置只 testMatch 本文件。
 *
 * 环境变量：
 *   E2E_API_TOKEN  运行实例的明文 token（node scripts/get-forge-token.cjs 输出）
 *   LIVE_CHAT_MODEL 可选，缺省 sensenova:sensenova-6.8-flash-lite（2026-10-26 实测上游可用）
 *
 * 覆盖（对应用户 7 条验收中的 1/2/3/4/5/6/7）：
 *   1) 历史回放工具卡 + 正文并存（刷新、切会话再切回）—— 删除 ChatPanel 后的回归
 *   2) 流式工具卡（执行中 → 完成/耗时）且正文照常显示 —— v-if="hasBlocks"/v-else 回归
 *   3) plan 模式单张计划卡逐步推进，不堆多张
 *   4) 断流 → 失败态 + 「重试」/「继续执行」按钮的实况（故障注入：已建连后流中断）
 *   5) 401 → 「登录状态已失效，请刷新页面重新登录」
 *   6) 服务端不再发帧 → 45s 后「响应超时」（故障注入：已建连后静默）
 *
 * 红线：本文件是唯一驱动入口；截图存 screenshots/live-51888 并读图核对。
 */

const OUT_DIR = path.resolve(fileURLToPath(new URL('../../../screenshots/live-51888', import.meta.url)))
const ROUTE = '/ai-agent'
const MODEL = process.env.LIVE_CHAT_MODEL ?? 'sensenova:sensenova-6.8-flash-lite'
/**
 * 运行实例上已存在的历史会话（2026-10-26 经 /api/ai-agent/chat/sessions + /chat/history 核实）：
 *   HISTORY_ID 标题「查看当前项目结构」· 5 条 · 含 list_files×2 + read_file 工具轨迹（断言靶子）
 *   OTHER_ID   标题「测试下读取工具」  · 5 条 · 用于「切走再切回」
 * 必须按 sessionId 定位：历史列表里有两条同名「查看当前项目结构」（16:37 三条 / 10:08 五条），
 * 按标题 + .first() 会点到 16:37 那条新会话，据此断言 `toolCards>=3` 会误报「工具卡全丢」。
 */
const HISTORY_ID = 'mvom92l0-asu9gt'
const OTHER_ID = 'mvezyak4-g0u0ma'

function requireToken(): string {
  const t = process.env.E2E_API_TOKEN?.trim()
  if (!t) throw new Error('缺少 E2E_API_TOKEN：先跑 node scripts/get-forge-token.cjs 取运行实例明文 token')
  return t
}

async function shot(page: Page, name: string, fullPage = true): Promise<void> {
  mkdirSync(OUT_DIR, { recursive: true })
  const file = path.join(OUT_DIR, `${name}.png`)
  await page.screenshot({ path: file, fullPage })
  console.log(`[shot] ${file}`)
}

interface BootOpts {
  /** 注入的会话 id（localStorage）；不传 = 沿用页面上次的会话 */
  sessionId?: string
  /** 额外 addInitScript（故障注入用） */
  init?: () => void
}

/** 打开运行实例插件页并注入 token / 模型（模型塞进 localStorage，页面 loadModels 会认它）。 */
async function boot(page: Page, opts: BootOpts = {}): Promise<void> {
  requireToken()
  await injectRealApiKey(page)
  await page.addInitScript(
    (o: { model: string; sessionId?: string }) => {
      localStorage.setItem('forgeself-agent-current-model', o.model)
      if (o.sessionId) localStorage.setItem('forgeself-agent-session-id', o.sessionId)
    },
    { model: MODEL, sessionId: opts.sessionId },
  )
  if (opts.init) await page.addInitScript(opts.init)
  /* 首屏 loadHistory 是异步的，落地时会 hydrate(messages) 覆盖消息区。不等它就开始发送，
     它返回的空历史会把刚发出的用户消息与流式内容一起清掉（验收 4 曾因此误判「半截回答丢失」）。
     不限定 sessionId：新会话也会请求一次（返回空数组）。 */
  const historyWait = page
    .waitForResponse((r) => r.url().includes('/api/ai-agent/chat/history/'), { timeout: 60_000 })
    .catch(() => null)
  await page.goto(ROUTE)
  await expect(page.locator('.agent'), '插件根节点未渲染').toBeVisible({ timeout: 60_000 })
  await historyWait
  await expect(page.locator('.aiw-input textarea'), '组件输入框未渲染').toBeVisible({ timeout: 60_000 })
  /* history 响应回来 ≠ onMounted 结束：mounted 末尾还有一次 hydrate(messages)，
     会直接覆盖消息区。紧跟着发消息会被它清掉（用户消息+流式内容全没）。
     等一个 settle 窗口让它落定 —— 这里宁可慢 2 秒，也不要时序假红。 */
  await page.waitForTimeout(2_000)
}

/** 展开左栏「历史会话」分组（默认收起）。 */
async function openHistory(page: Page) {
  const grp = page.locator('.sess__grp').filter({ hasText: '历史会话' })
  const toggle = grp.locator('.sess__head-toggle').first()
  if ((await toggle.getAttribute('aria-expanded')) !== 'true') await toggle.click()
  await expect(toggle).toHaveAttribute('aria-expanded', 'true')
}

/** 按 sessionId 精确点选历史会话（`.sess__item` 的 title 就是 sessionId），并等历史接口回来再返回。 */
async function pickSession(page: Page, sessionId: string) {
  await openHistory(page)
  const item = page.locator(`.sess__item[title="${sessionId}"]`)
  await expect(item, `历史会话列表里找不到 sessionId=${sessionId}`).toBeVisible({ timeout: 20_000 })
  // 点完立刻统计会撞上「正在加载对话」空窗：等 history 接口真的回来
  await Promise.all([
    page.waitForResponse((r) => r.url().includes(`/chat/history/${sessionId}`) && r.status() === 200, {
      timeout: 30_000,
    }),
    item.click(),
  ])
  await expect(page.locator('.aiw-msg--bot').first()).toBeVisible({ timeout: 30_000 })
  await expect(page.locator('.sess__name'), `左栏当前会话不是 ${sessionId}`).toHaveAttribute('title', sessionId)
}

interface MsgProbe {
  bots: number
  toolCards: number
  withToolAndText: number
  sample: string[]
}

/** 逐条助手消息量工具卡与正文（正文被吞 = 有卡无字）。 */
async function probeMessages(page: Page, label: string, minBots = 1): Promise<MsgProbe> {
  // 历史 hydrate 是异步的：等到预期条数到位再统计，否则会读到空态/欢迎语
  await expect
    .poll(async () => page.locator('.aiw-msg--bot').count(), { timeout: 30_000, message: `${label}：助手消息未到位` })
    .toBeGreaterThanOrEqual(minBots)
  const bots = page.locator('.aiw-msg--bot')
  const n = await bots.count()
  let toolCards = 0
  let withToolAndText = 0
  const sample: string[] = []
  for (let i = 0; i < n; i++) {
    const m = bots.nth(i)
    const tools = await m.locator('.aiw-tool').count()
    const text = (await m.locator('.aiw-msg__bubble').innerText().catch(() => '')).trim()
    toolCards += tools
    if (tools > 0) sample.push(`bot#${i} tools=${tools} textLen=${text.length}`)
    if (tools > 0 && text.length > 0) withToolAndText++
  }
  console.log(`[${label}] bots=${n} toolCards=${toolCards} withToolAndText=${withToolAndText}`)
  if (sample.length) console.log(`[${label}] ${sample.join(' | ')}`)
  return { bots: n, toolCards, withToolAndText, sample }
}

async function send(page: Page, text: string): Promise<void> {
  const input = page.locator('.aiw-input textarea')
  await input.fill(text)
  await input.press('Enter')
  await expect(page.locator('.aiw-msg--user', { hasText: text.slice(0, 12) }).first()).toBeVisible({
    timeout: 15_000,
  })
}

test.describe('运行实例 :51888 · AIAgent 工具卡修复批验收', () => {
  test.describe.configure({ timeout: 300_000 })

  test('验收 2/7：历史回放工具卡与正文并存（刷新 / 切会话再切回）', async ({ page }) => {
    // 直接用目标 sessionId 打开（不经列表点选），避开同名会话歧义
    await boot(page, { sessionId: HISTORY_ID })
    await expect(page.locator('.sess__name'), `未打开目标历史会话 ${HISTORY_ID}（sessionId 注入未生效）`).toHaveAttribute(
      'title',
      HISTORY_ID,
    )

    const first = await probeMessages(page, '历史-初次', 2)
    expect(first.bots, '历史会话没有任何助手消息').toBeGreaterThan(0)
    expect(first.toolCards, '删除 ChatPanel 后历史工具卡应仍在（toolEventsToBlocks）').toBeGreaterThanOrEqual(3)
    expect(first.withToolAndText, '历史回放出现「只有工具卡没有正文」= 正文被藏').toBeGreaterThanOrEqual(1)
    await page.locator('.aiw-msg--bot').first().scrollIntoViewIfNeeded()
    await shot(page, 'history-toolcards')

    // —— 刷新同一会话：工具卡与正文都还在 ——
    // 刷新同样走首屏 loadHistory → hydrate，必须等接口回来再统计（否则只读到欢迎语）
    const reloadWait = page
      .waitForResponse((r) => r.url().includes(`/chat/history/${HISTORY_ID}`), { timeout: 60_000 })
      .catch(() => null)
    await page.reload()
    await reloadWait
    const afterReload = await probeMessages(page, '历史-刷新后', first.bots)
    expect(afterReload.toolCards, '刷新后历史工具卡消失').toBe(first.toolCards)
    expect(afterReload.withToolAndText, '刷新后正文/工具卡不再共存').toBeGreaterThanOrEqual(1)
    await shot(page, 'history-toolcards-after-reload')

    // —— 切走再切回 ——
    await pickSession(page, OTHER_ID)
    await probeMessages(page, '历史-切到另一个会话')
    await pickSession(page, HISTORY_ID)
    const backAgain = await probeMessages(page, '历史-切回来后', first.bots)
    expect(backAgain.toolCards, '切走再切回后工具卡丢失').toBe(first.toolCards)
    expect(backAgain.withToolAndText, '切走再切回后正文丢失').toBeGreaterThanOrEqual(1)
    await shot(page, 'history-toolcards-after-switch-back')
  })

  test('验收 1：自由对话流式工具卡（执行中 → 完成/耗时）且正文照常显示', async ({ page }) => {
    test.setTimeout(240_000)
    await boot(page)
    await page.locator('.sess__icon-btn[aria-label="新会话"]').click()
    await send(page, '请调用 list_files 工具列出当前项目根目录，然后用一句话总结你看到了什么')

    // 流式期间：先看执行中（工具可能很快，抓不到不算失败，但记录实况）
    // 初始值永远读不到（try/catch 两支都会赋值），声明时不赋初值，否则 lint 报 no-useless-assignment
    let sawRunning: boolean
    try {
      await expect(page.locator('.aiw-tool--running').first()).toBeVisible({ timeout: 90_000 })
      sawRunning = true
    } catch {
      sawRunning = false
    }
    if (sawRunning) {
      const status = (await page.locator('.aiw-tool--running .aiw-tool__status').first().innerText()).trim()
      console.log(`[stream] 抓到执行中态，状态文案=${status}`)
      await shot(page, 'stream-tool-running')
    } else {
      console.log('[stream] 未抓到执行中态（工具太快），继续等完成态')
    }

    await expect(page.locator('.aiw-tool--ok').first(), '工具卡未进入完成态').toBeVisible({ timeout: 180_000 })
    const toolStatus = (await page.locator('.aiw-tool--ok .aiw-tool__status').first().innerText()).trim()
    const dur = (await page.locator('.aiw-tool--ok .aiw-tool__dur').first().innerText().catch(() => '')).trim()
    console.log(`[stream] 完成态 文案=${toolStatus} 耗时=${dur}`)
    expect(toolStatus, '工具卡完成态文案应为「完成」').toBe('完成')

    // 同一条消息：工具卡 + 正文共存（v-if="hasBlocks"/v-else 回归）
    const last = page.locator('.aiw-msg--bot').last()
    await expect(last.locator('.aiw-tool').first(), '最后一条助手消息里没有工具卡').toBeVisible()
    const bodyText = (await last.locator('.aiw-msg__bubble').innerText()).trim()
    console.log(`[stream] 正文长度=${bodyText.length} 摘要=${bodyText.slice(0, 80).replace(/\n/g, ' ')}`)
    expect(bodyText.length, '工具卡出现后正文被整块藏掉').toBeGreaterThan(10)
    await last.scrollIntoViewIfNeeded()
    await shot(page, 'stream-tool-and-text')

    // 刷新后应仍在（与历史回放同一套映射）
    // 注意：本会话只有一轮对话 → bot 数=1 与空态「欢迎语」无法区分，必须等工具卡本身回放出来
    page.on('pageerror', (e) => console.log('[reload][pageerror]', e.message))
    page.on('console', (m) => {
      if (m.type() === 'error') console.log('[reload][console-error]', m.text())
    })
    const sid = await page.evaluate(() => localStorage.getItem('forgeself-agent-session-id'))
    console.log(`[reload] localStorage sessionId=${sid}`)
    const reloadWait = page.waitForResponse((r) => r.url().includes('/chat/history/'), { timeout: 60_000 }).catch(() => null)
    await page.waitForTimeout(1_000) // 等本轮落库（done 回调写 ToolCallsJson）
    await page.reload()
    const reloadResp = await reloadWait
    if (reloadResp) {
      const body = await reloadResp.json().catch(() => null)
      const n = Array.isArray(body) ? body.length : 'n/a'
      console.log(`[reload] history ${reloadResp.status()} n=${n} url=${reloadResp.url()}`)
    } else {
      console.log('[reload] 未捕获到 /chat/history 请求')
    }
    await expect(page.locator('.aiw-tool').first(), '刷新后本次流式的工具卡未回放').toBeVisible({ timeout: 60_000 })
    const afterReload = await probeMessages(page, '流式会话-刷新后')
    expect(afterReload.toolCards, '刷新后本次流式的工具卡消失').toBeGreaterThanOrEqual(1)
    expect(afterReload.withToolAndText, '刷新后工具卡与正文不再共存').toBeGreaterThanOrEqual(1)
    await shot(page, 'stream-tool-after-reload')
  })

  test('验收 3：plan 模式单张计划卡逐步 pending → running → done', async ({ page }) => {
    test.setTimeout(300_000)
    await boot(page)

    const name = `live-e2e-plan-${Date.now()}`
    await page.locator('.sess__add').click()
    const dialog = page.locator('.el-dialog .aed')
    await expect(dialog).toBeVisible({ timeout: 15_000 })
    await dialog.locator('input[placeholder="Agent 名称"]').fill(name)
    await dialog.locator('.aed__mode-opt', { hasText: '计划驱动' }).click()
    await page.locator('.el-dialog__footer button', { hasText: '创建' }).click()
    await expect(dialog).not.toBeVisible({ timeout: 15_000 })
    const row = page.locator('.sess__agent', { hasText: name })
    await expect(row).toBeVisible({ timeout: 15_000 })
    await row.click()

    try {
      await send(page, '分两步：先用 list_files 看项目根目录，再用一句话总结。')

      // 计划卡出现
      const plan = page.locator('.aiw-plan')
      await expect(plan.first(), 'plan 模式未出现计划卡').toBeVisible({ timeout: 180_000 })

      // 不堆多张：采样期间始终 1 张
      const seen: string[] = []
      let maxCards = 0
      for (let i = 0; i < 12; i++) {
        const cards = await plan.count()
        maxCards = Math.max(maxCards, cards)
        const statuses = await page.locator('.aiw-plan__list li').evaluateAll((els) =>
          els.map((e) => (e as HTMLElement).className.replace('is-', '')),
        )
        seen.push(`#${i} cards=${cards} steps=[${statuses.join(',')}]`)
        if (statuses.includes('done') && !statuses.includes('running') && !statuses.includes('pending')) break
        await page.waitForTimeout(3000)
      }
      console.log(`[plan] ${seen.join(' | ')}`)
      expect(maxCards, '计划卡堆成了多张（同 id 未原地替换）').toBe(1)
      const steps = await page.locator('.aiw-plan__list li').count()
      expect(steps, '计划卡没有步骤').toBeGreaterThanOrEqual(1)
      expect(seen.join(' '), '整轮采样都没看到步骤推进（pending/running/done 之一）').toMatch(/pending|running|done/)
      await page.locator('.aiw-plan').first().scrollIntoViewIfNeeded()
      await shot(page, 'plan-card')
    } finally {
      // 清理：删除本用例创建的 Agent
      const edit = page.locator('.sess__agent', { hasText: name }).locator('.sess__agent-edit')
      if (await edit.count()) {
        await edit.click()
        await expect(dialog).toBeVisible({ timeout: 10_000 })
        await page.locator('.el-dialog__footer button', { hasText: '删除' }).click()
        const mb = page.locator('.el-message-box')
        if (await mb.isVisible()) await mb.locator('button', { hasText: '删除' }).click()
        await expect(page.locator('.sess__agent', { hasText: name })).not.toBeVisible({ timeout: 15_000 })
      }
    }
  })

  test('验收 5：token 失效 → 「登录状态已失效，请刷新页面重新登录」', async ({ page }) => {
    test.setTimeout(120_000)
    /* 真实链路复现不了：本实例 /api/ai-agent/** 不校验 token
       （2026-10-26 实测：GET /chat/sessions 无 token / 错 token 均 200）→
       改为让 SSE 端点返回真 401，验证前端 httpError 的文案映射。
       ⚠这是故障注入，不能证明「真实 token 过期」链路 —— 证据与汇报里必须标注。 */
    const notAuthorized = () => {
      const orig = window.fetch.bind(window)
      window.fetch = ((input: RequestInfo | URL, init?: RequestInit) => {
        const url = typeof input === 'string' ? input : input instanceof URL ? input.href : input.url
        if (!url.includes('/api/ai-agent/chat/stream')) return orig(input, init)
        return Promise.resolve(
          new Response(JSON.stringify({ message: 'Unauthorized' }), {
            status: 401,
            headers: { 'content-type': 'application/json' },
          }),
        )
      }) as typeof window.fetch
    }
    await boot(page, { init: notAuthorized })
    await send(page, '这条消息应当因鉴权失效而失败')

    const err = page.locator('.aiw-status .aiw-error').first()
    await expect(err, '发送失败后未出现错误条').toBeVisible({ timeout: 45_000 })
    const text = (await err.innerText()).trim()
    console.log(`[401] 错误条文案=${text}`)
    expect(text, '401 未翻译成人话').toContain('登录状态已失效，请刷新页面重新登录')
    expect(text, '不应把裸 401 抛给用户').not.toContain('请求失败(401)')
    await shot(page, 'unauthorized-401')
  })

  test('验收 6：服务端建立连接后不再发帧 → 45s 报「响应超时」而不是无限转圈', async ({ page }) => {
    test.setTimeout(150_000)
    // 故障注入：仅拦这一个 SSE 端点，返回「已建连但永不发 data 帧」的流（其余请求照常走真实后端）
    const stallStream = () => {
      const orig = window.fetch.bind(window)
      window.fetch = ((input: RequestInfo | URL, init?: RequestInit) => {
        const url = typeof input === 'string' ? input : input instanceof URL ? input.href : input.url
        if (!url.includes('/api/ai-agent/chat/stream')) return orig(input, init)
        const enc = new TextEncoder()
        const body = new ReadableStream<Uint8Array>({
          start(controller) {
            controller.enqueue(enc.encode(': keep-alive\n\n'))
            // 之后不再 enqueue、也不 close：模拟模型服务挂死
          },
        })
        return Promise.resolve(
          new Response(body, { status: 200, headers: { 'content-type': 'text/event-stream' } }),
        )
      }) as typeof window.fetch
    }
    await boot(page, { init: stallStream })

    const t0 = Date.now()
    await send(page, '这条消息用来验证 45 秒空闲超时')
    const err = page.locator('.aiw-status .aiw-error').first()
    await expect(err, '静默 45s 后仍未报超时（无限转圈）').toBeVisible({ timeout: 90_000 })
    const elapsed = Date.now() - t0
    const text = (await err.innerText()).trim()
    console.log(`[timeout] 等待 ${(elapsed / 1000).toFixed(1)}s 后文案=${text}`)
    expect(text, '超时文案不对').toContain('响应超时')
    expect(elapsed, '过早报错，说明没等到 45s 空闲阈值').toBeGreaterThan(40_000)
    // 失败态也要落到这条消息自己身上（不是只有全局条）
    await expect(page.locator('.aiw-msg__status.is-failed').last(), '失败态未落到消息上').toBeVisible({
      timeout: 15_000,
    })
    await shot(page, 'timeout-45s')
  })

  test('验收 4：断流 → 失败态 + 可继续/重试按钮的实况（自由对话 vs plan 续跑）', async ({ page }) => {
    test.setTimeout(180_000)
    // 故障注入：先吐一段正文，再让流报错（等价「中途切断网络」在客户端看到的现象）
    const cutMidStream = () => {
      const orig = window.fetch.bind(window)
      window.fetch = ((input: RequestInfo | URL, init?: RequestInit) => {
        const url = typeof input === 'string' ? input : input instanceof URL ? input.href : input.url
        if (!url.includes('/api/ai-agent/chat/stream')) return orig(input, init)
        const enc = new TextEncoder()
        const body = new ReadableStream<Uint8Array>({
          start(controller) {
            /* 只吐一帧正文就关流、不发 done —— 这才是「中途切断网络」在客户端的样子：
               SSE 正常结束但没有完成事件。（controller.error 会让浏览器清空未读队列，
               连已发的那帧都会丢，那验的就不是断开而是乱码。） */
            controller.enqueue(enc.encode('data: {"type":"content","content":"半截回答，"}\n\n'))
            setTimeout(() => controller.close(), 2500)
          },
        })
        return Promise.resolve(
          new Response(body, { status: 200, headers: { 'content-type': 'text/event-stream' } }),
        )
      }) as typeof window.fetch
    }
    await boot(page, { init: cutMidStream })
    await send(page, '这条消息用来验证中途断流')
    // 中间态证据：流还没关时页面是否已渲染那帧正文（用于区分「帧没到」与「失败时被清空」）
    await page.waitForTimeout(1_200)
    const midText = (await page.locator('.aiw-msg--bot').last().innerText().catch(() => '')).trim()
    console.log(`[cut] 流中气泡文本=${JSON.stringify(midText.slice(0, 60))} len=${midText.length}`)
    const dump = await page
      .locator('.aiw-msg')
      .evaluateAll((els) =>
        els.map((e) => ({ cls: String((e as HTMLElement).className), t: (e.textContent || '').trim().slice(0, 30) })),
      )
    console.log(`[cut] 流中消息=${JSON.stringify(dump)}`)
    await shot(page, 'stream-cut-midstream')

    const status = page.locator('.aiw-msg__status.is-failed').last()
    await expect(status, '断流后消息未进入失败态').toBeVisible({ timeout: 60_000 })
    const statusText = (await status.innerText()).trim()
    const retryCount = await status.locator('.aiw-msg__retry', { hasText: '重试' }).count()
    const resumeCount = await status.locator('.aiw-msg__retry', { hasText: '继续执行' }).count()
    const partial = (
      await page.locator('.aiw-msg--bot').last().locator('.aiw-msg__bubble').innerText().catch(() => '')
    ).trim()
    console.log(
      `[cut] 状态=${statusText.replace(/\n/g, ' / ')} 重试按钮=${retryCount} 继续执行按钮=${resumeCount} 已保留正文=${partial.length}`,
    )
    await shot(page, 'stream-cut-failed')

    // 事实断言：失败态 + 已吐内容保留（重启/断流不得把半截回答丢掉）
    expect(statusText, '失败态缺少失败说明').toContain('生成失败')
    expect(partial.length, '断流后已生成内容被丢弃').toBeGreaterThan(0)
    // 自由对话链路没有服务端 runId ⇒ 按设计只能「重试」，没有「继续执行」——
    // 这条与用户期望表（断网应出现「继续执行」）不一致，如实记录在日志/证据里。
    expect(retryCount, '自由对话失败态缺少「重试」').toBeGreaterThanOrEqual(1)
    console.log(
      `[cut] 结论：自由对话链路 resumable=${resumeCount > 0}（agentBridge free 分支的 StreamInterruptedError 不带 runId）`,
    )
  })
})
