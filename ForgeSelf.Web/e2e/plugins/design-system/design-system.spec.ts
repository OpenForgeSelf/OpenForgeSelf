import { test, expect, type Page, type Response } from '@playwright/test'
import { mkdirSync, writeFileSync, readFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { injectRealApiKey } from '../../helpers/real-auth'

/**
 * 设计系统插件（design-system）端到端验证 —— 真实宿主 + 真实插件库，零 mock。
 *
 * v1 这份用例只证明"页面能渲染出一套前端自己算的假设计系统"（还把 version=1.2.1 钉死在断言里，
 * 一升级就假失败）。v2 换成验证**闭环是否真的闭合**：
 *   建项目 → 后端生成三层令牌（预览不落库 / 确认才落库）→ 工作台改一条并回读 →
 *   切主题/密度后同一令牌有效值真的变 → 审计落库并报门禁 → 导出产物来自后端投影 →
 *   发布不可变快照（哈希可核）→ 内置图标库真的进库 → 组件库/展示页换肤 →
 *   声明的能力面（assets/fonts/screens）生成有种子、界面写得进 → 归档两条路径。
 *
 * 断言原则（本项目 WBS 治理）：
 * - 版本用**自洽判据**：plugin.json.Version == 后端 meta.modelVersion == 界面徽标，不钉历史数字；
 * - 数量只设"下限 + 语义"（令牌 >100、内置图标 ≥24），不钉当前实现刚好产生的条数；
 * - 关键状态用 API 复核后端事实，而不是只看 DOM 消失；
 * - 每一项 meta.capabilities 都要有"读得到 + 写得进"的断言，防止界面摆一个永远空的面板；
 * - 危险动作（归档）取消/确认两条路径各测一次（只测确认会漏"根本没接二次确认"这类回归）。
 */

const MANIFEST = JSON.parse(
  // 清单可能是 UTF-8 BOM 开头（Windows 上编辑过就会）：用转义写法剥掉，别把 U+FEFF 当字面量写进源码
  readFileSync(fileURLToPath(new URL('../../../../Plugins/DesignSystem/plugin.json', import.meta.url)), 'utf-8').replace(/^\uFEFF/, ''),
) as { Version: string; frontend: { route: string; views: string[]; entry: string } }

const PLUGIN_ROUTE = MANIFEST.frontend.route
const PLUGIN_ID = 'design-system'
// 网络证据行格式「状态码 方法 URL ct=…」，URL 后还跟着 ct，故边界用 (\?|\s|$)
const ENTRY_RE = new RegExp(`/plugins/${PLUGIN_ID}/web/dist/index\\.js(\\?|\\s|$)`)
const STYLE_RE = new RegExp(`/plugins/${PLUGIN_ID}/web/dist/style\\.css(\\?|\\s|$)`)

const OUT_DIR = path.resolve(fileURLToPath(new URL('../../../screenshots/e2e/design-system', import.meta.url)))

/** 每次运行一套唯一项目代码：数据隔离、可反复跑，不依赖上次残留 */
const RUN_CODE = `e2e-${Date.now().toString(36)}`.slice(0, 24)

interface Evidence {
  network: string[]
  consoleAll: string[]
  consoleErrors: string[]
}

function attachCollectors(page: Page): Evidence {
  const evidence: Evidence = { network: [], consoleAll: [], consoleErrors: [] }
  page.on('console', (msg) => {
    const line = `[${msg.type()}] ${msg.text()}`
    evidence.consoleAll.push(line)
    if (msg.type() === 'error') evidence.consoleErrors.push(msg.text())
  })
  page.on('pageerror', (err) => {
    evidence.consoleErrors.push(`[pageerror] ${err.message}`)
    evidence.consoleAll.push(`[pageerror] ${err.message}`)
  })
  page.on('response', (resp: Response) => {
    const url = resp.url()
    if (ENTRY_RE.test(url) || STYLE_RE.test(url) || /\/api\/design-system\//.test(url)) {
      evidence.network.push(`${resp.status()} ${resp.request().method()} ${url} ct=${resp.headers()['content-type'] ?? '-'}`)
    }
  })
  return evidence
}

function dumpEvidence(name: string, evidence: Evidence, extra: string[]): void {
  mkdirSync(OUT_DIR, { recursive: true })
  const file = path.join(OUT_DIR, `${name}.log`)
  const body = [
    `=== route: ${PLUGIN_ROUTE} | project: ${RUN_CODE} ===`,
    '',
    '--- 关键网络请求（插件产物 + 设计系统 API）---',
    ...(evidence.network.length ? evidence.network : ['(无)']),
    '',
    '--- 控制台 error / 未捕获异常 ---',
    ...(evidence.consoleErrors.length ? evidence.consoleErrors : ['(无)']),
    '',
    '--- 附加信息 ---',
    ...(extra.length ? extra : ['(无)']),
    '',
    '--- 控制台全量输出 ---',
    ...(evidence.consoleAll.length ? evidence.consoleAll : ['(无)']),
  ].join('\n')
  writeFileSync(file, body, 'utf8')
  console.log(`\n[evidence] ${file}\n${body}\n`)
}

/**
 * 证据落盘**不能只在用例末尾做**（上一版就是这么写的）：中间任何一步失败，网络/控制台证据就全丢了，
 * 于是"切主题没重取投影"这类失败只能靠猜（请求没发 / 发了没回 / 回了没注入，三者在没有证据时不可区分）。
 * 现在挂 afterEach：无论通过与否都落一份，文件名带状态，失败时的第一手材料就是这个 log。
 */
let sink: { evidence: Evidence; extra: string[] } | null = null

// Playwright 要求首个参数必须是解构模式（`_fixtures` 这种写法在用例收集期就报错），本钩子不需要任何 fixture。
// eslint-disable-next-line no-empty-pattern
test.afterEach(({ }, testInfo) => {
  if (!sink) return
  const status = testInfo.status === testInfo.expectedStatus ? 'passed' : 'FAILED'
  dumpEvidence(`design-system-v2-${status}`, sink.evidence, sink.extra)
})

async function shot(page: Page, name: string): Promise<void> {
  mkdirSync(OUT_DIR, { recursive: true })
  await page.screenshot({ path: path.join(OUT_DIR, `${name}.png`), fullPage: true })
}

/**
 * 借页面里的真实 token 直取 API，并解 { success, data } 信封。
 *
 * 对 SQLite `code = Busy` 这类 500 按真实客户端的做法重试（最多 3 次）——宿主 DAL 在并发读写下
 * 偶发锁冲突是**已记录的宿主级缺陷**（见 TODO.md「宿主 SQLite 并发 BUSY」），不该让整条业务链用例
 * 卡在这一处；其它错误一律原样抛出，绝不吞。
 */
async function apiData<T>(page: Page, url: string): Promise<T> {
  return page.evaluate(async (u) => {
    const token = localStorage.getItem('forge_api_token') ?? ''
    let last = ''
    for (let attempt = 0; attempt < 3; attempt++) {
      if (attempt > 0) await new Promise((r) => setTimeout(r, 1200))
      const res = await fetch(u, { headers: { Authorization: `Bearer ${token}` } })
      const text = await res.text()
      if (res.ok) {
        try {
          const body = JSON.parse(text) as { success?: boolean; data?: unknown }
          return (body.data ?? body) as T
        } catch {
          throw new Error(`${res.status} 响应不是 JSON：${text.slice(0, 160)}`)
        }
      }
      last = `${res.status} ${text.slice(0, 160)}`
      if (!/code = Busy|database is locked/.test(text)) break
    }
    throw new Error(last)
  }, url)
}

/** 导出端点回文件原文（不是信封），单独一条通道；同样只重试 Busy 一类 */
async function apiText(page: Page, url: string): Promise<string> {
  return page.evaluate(async (u) => {
    const token = localStorage.getItem('forge_api_token') ?? ''
    for (let attempt = 0; attempt < 3; attempt++) {
      if (attempt > 0) await new Promise((r) => setTimeout(r, 1200))
      const res = await fetch(u, { headers: { Authorization: `Bearer ${token}` } })
      const text = await res.text()
      if (res.ok) return text
      if (!/code = Busy|database is locked/.test(text)) return `${res.status} ${text}`
    }
    return 'Busy 重试 3 次仍失败'
  }, url)
}

/**
 * 写端点：走页面里的真实 token。**Busy 一律不重试**（重复落库比红屏更糟，与 `http.ts` 同规矩），
 * 失败时把后端原文抛出来，让断言失败信息直接指向原因。
 */
async function apiPost<T>(page: Page, url: string, body: unknown): Promise<T> {
  return page.evaluate(async ({ u, b }) => {
    const token = localStorage.getItem('forge_api_token') ?? ''
    const res = await fetch(u, {
      method: 'POST',
      headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
      body: JSON.stringify(b),
    })
    const text = await res.text()
    if (!res.ok) throw new Error(`${res.status} ${text.slice(0, 200)}`)
    const json = JSON.parse(text) as { data?: unknown }
    return (json.data ?? json) as unknown
  }, { u: url, b: body }) as Promise<T>
}

/** 某主题下某令牌的有效值（后端解析过别名，界面显示的就是它） */
async function effectiveValue(page: Page, pid: number, theme: string, tokenPath: string): Promise<string> {
  const view = await apiData<{ items: { path: string; value: string }[] }>(
    page, `/api/design-system/projects/${pid}/tokens/effective?theme=${theme}`)
  return view.items.find((t) => t.path === tokenPath)?.value ?? ''
}

async function projectField(page: Page, pid: number, field: 'tokenCount' | 'componentCount' | 'status' | 'version'): Promise<unknown> {
  const p = await apiData<Record<string, unknown>>(page, `/api/design-system/projects/${pid}`)
  return p[field]
}

const nav = (page: Page, label: string) => page.getByRole('button', { name: label, exact: true })

/**
 * 进入「工作台」模式（M2 四模式外壳起，插件默认落在 开始/展厅：无项目→开始、有项目→展厅，
 * 14 入口导航只在工作台模式渲染）。幂等：重复调用只是再点一次模式条。
 * 调用点：goto 之后、任何依赖 .ds-nav / 14 入口的断言之前；page.reload() 之后同样要再进一次。
 */
async function enterWorkbench(page: Page): Promise<void> {
  await page.getByRole('tab', { name: '工作台', exact: true }).click()
  await expect(page.locator('.ds-nav')).toBeVisible({ timeout: 20_000 })
}

/** 令牌里的 hex 换成浏览器计算样式写法，用于"预览底色 == 后端令牌值"的逐位比对 */
function hexToRgb(hex: string): string | null {
  const m = /^#?([0-9a-f]{2})([0-9a-f]{2})([0-9a-f]{2})$/i.exec(hex.trim())
  return m ? `rgb(${Number.parseInt(m[1], 16)}, ${Number.parseInt(m[2], 16)}, ${Number.parseInt(m[3], 16)})` : null
}

test.describe('设计系统插件 v2 · 库驱动工作台全链路', () => {
  /**
   * 预算挂在 describe 上，不是用例体内。`test.setTimeout()` 要等用例体开始执行才生效，
   * 而 `page` fixture（起浏览器 + 首次导航到"现编译 290 kB 插件产物的 dev server"）本身就要吃掉十几秒 ——
   * 挂在体内时它仍按默认 30s 被掐，实测红过一次：`Test timeout of 30000ms exceeded while setting up "page"`。
   */
  test.describe.configure({ timeout: 420_000 })

  test('远程加载 + 版本自洽 + 建项目→生成→改值→切主题→审计→导出→发布→图标→换肤→归档', async ({ page }) => {
    mkdirSync(OUT_DIR, { recursive: true })
    const evidence = attachCollectors(page)
    sink = { evidence, extra: [] }
    const mark = (line: string): void => { sink?.extra.push(line) }
    await injectRealApiKey(page)
    await page.goto(PLUGIN_ROUTE)

    // ---- 1. 远程产物真实加载（入口 JS + 同目录 style.css 都 200）----
    await expect(page.locator('.ds-root')).toBeVisible({ timeout: 30_000 })
    const entryHit = await expect
      .poll(() => evidence.network.find((l) => ENTRY_RE.test(l)) ?? '', { timeout: 20_000, message: '未捕获插件入口 JS' })
      .toMatch(/^200 /)
      .then(() => evidence.network.find((l) => ENTRY_RE.test(l)) as string)
    const styleHit = await expect
      .poll(() => evidence.network.find((l) => STYLE_RE.test(l)) ?? '', { timeout: 20_000, message: '未捕获插件样式 CSS' })
      .toMatch(/^200 /)
      .then(() => evidence.network.find((l) => STYLE_RE.test(l)) as string)
    expect(entryHit).toContain('javascript')
    expect(styleHit).toContain('text/css')

    // ---- 2. 版本自洽：清单 == 后端 meta == 界面徽标 ----
    const meta = await apiData<Record<string, string>>(page, '/api/design-system/meta')
    expect(meta.pluginId).toBe(PLUGIN_ID)
    expect(meta.modelVersion).toBe(MANIFEST.Version)
    expect(meta.generatorVersion).toBe(meta.modelVersion)
    expect(meta.projectionVersion).toBe(meta.modelVersion)
    await expect(page.locator('.ds-badge')).toContainText(`模型 ${meta.modelVersion}`)

    // M2 四模式外壳：先进入「工作台」模式，14 入口导航才渲染（默认落 开始/展厅）
    await enterWorkbench(page)

    // ---- 3. 导航按能力面出现，14 个入口都在 ----
    const navLabels = [
      '项目与生成', '令牌工作台', '色彩实验室', '排版标度', '尺度与密度', '阴影与动效',
      '主题实验室', '图标库', '审计与门禁', '导出交付', '版本与对比', '组件库', '品牌展示页', '品牌资产',
    ]
    for (const label of navLabels) await expect(nav(page, label)).toBeVisible()
    await shot(page, '01-shell-loaded')

    // ---- 4. 建项目：界面建，后端必须有 ----
    await page.getByPlaceholder('如 console-ui').fill(RUN_CODE)
    await page.getByPlaceholder('如 铸己匣控制台').fill('E2E 设计系统')
    await page.getByRole('button', { name: '创建项目' }).click()
    await expect(page.getByText(RUN_CODE).first()).toBeVisible({ timeout: 25_000 })
    const projects = await apiData<{ id: number; code: string }[]>(page, '/api/design-system/projects')
    const created = projects.find((p) => p.code === RUN_CODE)
    expect(created, '项目必须真的进了后端库').toBeTruthy()
    const pid = created!.id

    // ---- 5. 生成：预览不落库、确认才落库（这条区分"真生成"和"前端画个样子"）----
    await page.getByPlaceholder('hex（如 aabbcc，可省 #）或 oklch(0.6 0.18 265)').fill('7c3aed')
    await page.getByRole('button', { name: '① 预览（不落库）' }).click()
    await expect(page.getByText(/种子|seed/i).first()).toBeVisible({ timeout: 40_000 })
    expect(Number(await projectField(page, pid, 'tokenCount'))).toBe(0)

    await page.getByRole('button', { name: '② 确认写入（generate）' }).click()
    await expect
      .poll(async () => Number(await projectField(page, pid, 'tokenCount')), { timeout: 90_000 })
      .toBeGreaterThan(100)
    // 组件层令牌要落成组件目录（否则"组件库"页面对新生成的项目永远是 0 条 = 半玩具）
    await expect
      .poll(async () => Number(await projectField(page, pid, 'componentCount')), { timeout: 60_000 })
      .toBeGreaterThanOrEqual(10)
    await shot(page, '02-generated')

    // ---- 6. 种子色必须逐位进色阶（否则"种子"只是装饰）----
    expect((await effectiveValue(page, pid, 'light', 'color.brand.600')).toLowerCase()).toBe('#7c3aed')

    // ---- 7. 工作台改一条 → 回读自证 ----
    await nav(page, '令牌工作台').click()
    await expect(page.locator('.ts__table').first()).toBeVisible({ timeout: 30_000 })
    const row = page.locator('tr').filter({ hasText: 'color.brand.600' }).first()
    await expect(row).toBeVisible()
    await row.getByRole('button', { name: '改' }).click()
    const editor = page.locator('.ts__editor')
    await editor.fill('#6d28d9')
    await page.getByRole('button', { name: '保存' }).click()
    await expect
      .poll(async () => await effectiveValue(page, pid, 'light', 'color.brand.600'), { timeout: 25_000 })
      .toBe('#6d28d9')
    await shot(page, '03-token-studio')

    // ---- 8. 主题与密度是两条轴：切档后同一令牌有效值必须真的不同 ----
    const lightBg = await effectiveValue(page, pid, 'light', 'semantic.surface-bg')
    const darkBg = await effectiveValue(page, pid, 'dark', 'semantic.surface-bg')
    expect(darkBg).toBeTruthy()
    expect(darkBg).not.toBe(lightBg)
    const lightGap = await effectiveValue(page, pid, 'light', 'space.4')
    const compactGap = await effectiveValue(page, pid, 'compact', 'space.4')
    expect(compactGap).toBeTruthy()
    expect(compactGap).not.toBe(lightGap)

    // 主题条显示的是主题名（中文），故按"名字或代码"匹配，不硬编码语言
    await page.locator('.ds-chip').filter({ hasText: /深色|dark/i }).first().click()

    /**
     * 8a′. 在**非预览页**切档：投影不会立刻重取（外壳要保持中性），但界面必须说清画布现在算哪一档。
     * 这条状态可见性是 v2.6.8 补的根因证据：暗色在令牌工作台被选中后，画布要等进入预览页才取，
     * 而"取到没有"当时没有任何信号 —— 于是同一步骤的截图两次跑出来一暗一亮（读图 QA 抓到的）。
     * 本轮跑到这里实测是 `unloaded`（本次会话建完第一个项目后还没进过预览页，压根没取过），
     * 不是 `pending`：所以"待重取"和"未取"必须是两个状态，混成一句文案就是说谎。
     */
    await expect
      .poll(() => page.locator('.ds-skinstate').getAttribute('data-skin-state'),
        { timeout: 15_000, message: '切档后主题条没进入「未取/待重取」状态' })
      .toBe('unloaded')
    await expect(page.locator('.ds-skinstate')).toContainText('未取')
    mark(`[skin] 令牌工作台切 dark：主题条状态=${await page.locator('.ds-skinstate').innerText()}`)

    await nav(page, '色彩实验室').click()
    await expect(page.locator('.cl__ramp').first()).toBeVisible({ timeout: 30_000 })
    await shot(page, '04-colorlab-dark')

    /**
     * 8b. 尺度档位序由后端词表供给：界面上 space./radius. 的档名顺序必须等于 `meta.scaleOrders`。
     * 钉的是"前端不再自己列一份档名"——后端 `ScaleGenerators` 改档序时，界面必须跟着动。
     */
    await nav(page, '尺度与密度').click()
    const scaleMeta = await apiData<{ scaleOrders: Record<string, string[]> }>(page, '/api/design-system/meta')
    for (const prefix of ['space', 'radius']) {
      const order = scaleMeta.scaleOrders[prefix] ?? []
      expect(order.length, `词表里必须有 ${prefix} 的档位序`).toBeGreaterThan(0)
      // 只看"该前缀那张表"里的路径列（形如 `radius.xs`）：值列写成 `radius.xs = 2.1px`，是说明不是档名
      const pathCol = new RegExp(`^${prefix}\\.[^\\s]+$`)
      const block = page.locator('.ds-stack').filter({ has: page.getByRole('heading', { name: new RegExp(`${prefix}\\.\\*`) }) }).first()
      await expect(block).toBeVisible({ timeout: 30_000 })
      const rank = (p: string): number => {
        const i = order.indexOf(p.slice(prefix.length + 1))
        return i < 0 ? Number.MAX_SAFE_INTEGER : i
      }
      const observed = (await block.locator('.ds-mono').allInnerTexts())
        .map((t) => t.trim()).filter((t) => pathCol.test(t))
      expect(observed.length, `${prefix}.* 那张表里必须列出档位行`).toBeGreaterThan(0)
      expect(observed.join(' '), `${prefix}.* 的界面顺序必须等于后端词表序（${order.join('/')}）`)
        .toEqual([...observed].sort((a, b) => rank(a) - rank(b)).join(' '))
      mark(`尺度与密度页 ${prefix}.* 界面序 == meta.scaleOrders（${order.join('/')}）`)
    }

    // 后端说明列必须真的填上：这一列取自原行 `description`，永远显示 `—` 就是"数据取到了却没显示"
    const spaceBlock = page.locator('.ds-stack').filter({ has: page.getByRole('heading', { name: /space\.\*/ }) }).first()
    const firstDescCell = spaceBlock.locator('tbody tr').first().locator('td').last()
    await expect
      .poll(async () => (await firstDescCell.innerText()).trim(), { timeout: 30_000, message: '后端说明列一直是 — = description 没显示出来' })
      .not.toBe('—')
    mark(`间距表首行的后端说明：${(await firstDescCell.innerText()).trim()}`)
    await shot(page, '05-density-order')

    /**
     * 8c. 词表收尾（v2.6.6）：色族序 / 层级序 / 审计类别序 / 状态档位序 —— 四个界面位置的顺序
     * 必须等于后端 `/meta` 的四张表。这些地方过去各存一份手抄数组（`TIER_ORDER`、`colorFamilies`
     * 里的 brand/accent/…、`AuditBoard.KINDS`）：抄的那天一致，之后各自漂移就是"界面与产物顺序不同"
     * "后端加了一类界面筛不出来"。判据只有一条：界面显示的顺序 == 词表顺序。
     */
    const vocab = await apiData<{
      colorFamilies: string[]; tiers: string[]; auditKinds: string[]; entities: string[]
      variantAxes: { axis: string; values: string[] }[]
    }>(page, '/api/design-system/meta')
    for (const key of ['colorFamilies', 'tiers', 'auditKinds', 'entities'] as const)
      expect(vocab[key].length, `/meta 必须出 ${key} 词表`).toBeGreaterThan(0)
    const axisValues = (name: string): string[] => vocab.variantAxes.find((a) => a.axis === name)?.values ?? []
    expect(vocab.variantAxes.length, '/meta 必须出变体轴清单（轴 → 档位序）').toBeGreaterThan(0)

    const rankBy = (order: string[]) => (v: string): number => {
      const i = order.indexOf(v)
      return i < 0 ? Number.MAX_SAFE_INTEGER : i
    }
    const sameOrderAsVocabulary = (observed: string[], order: string[], where: string): void => {
      expect(observed.length, `${where} 必须真的列出条目`).toBeGreaterThan(0)
      expect(observed.join(' '), `${where} 的顺序必须等于后端词表序（${order.join('/')}）`)
        .toEqual([...observed].sort((a, b) => rankBy(order)(a) - rankBy(order)(b)).join(' '))
    }

    await nav(page, '色彩实验室').click()
    await expect(page.locator('.cl__ramp').first()).toBeVisible({ timeout: 30_000 })
    const rampFamilies = (await page.locator('.cl__family').allInnerTexts()).map((t) => t.trim()).filter(Boolean)
    sameOrderAsVocabulary(rampFamilies, vocab.colorFamilies, '色彩实验室的色阶条带族序')
    mark(`色阶条带族序：${rampFamilies.join(' → ')}`)

    await nav(page, '令牌工作台').click()
    const tierOptions = (await page.locator('select[aria-label="层级"] option').allInnerTexts())
      .map((t) => t.trim()).filter((t) => t && t !== '全部层级')
    sameOrderAsVocabulary(tierOptions, vocab.tiers, '令牌工作台层级下拉')

    await nav(page, '审计与门禁').click()
    const kindRows = (await page.locator('.ab__kind').allInnerTexts()).map((t) => t.trim()).filter(Boolean)
    expect(kindRows.join(' '), '门禁说明清单必须整张列出后端词表里的每一类')
      .toEqual(vocab.auditKinds.join(' '))

    await nav(page, '组件库').click()
    // 先等库里的组件真的到了再断言/截图：半加载状态截出来的图看不出真假（v2.6.5 读图抓到的同一课）
    await expect(page.locator('.cg__card').first()).toBeVisible({ timeout: 30_000 })

    /**
     * 8c-画布. 进入预览页后，投影必须**真的到货**并且与后端的 dark 令牌逐位对上，才有资格被截图。
     * 画布身份（`data-skin-theme`）由生效的那次取数写入，迟到的旧响应不许改它（单测已钉，这里在真 HTTP 上再钉一次）。
     */
    const skin = page.locator('.ds-skin').first()
    await expect
      .poll(() => skin.getAttribute('data-skin-theme'),
        { timeout: 40_000, message: '进入预览页后画布投影仍不是 dark（截图明暗不可复现的根因）' })
      .toBe('dark')
    const strip = page.locator('.ds-skin__meta')
    await expect(strip).toContainText('画布投影 dark', { timeout: 15_000 })
    const stripBgText = ((await strip.innerText()).match(/surface-bg\s+(\S+)/)?.[1] ?? '').trim()
    const stripRgb = hexToRgb(stripBgText)
    expect(stripRgb, `角标里的 surface-bg「${stripBgText}」必须是可解析的颜色`).toBeTruthy()
    const darkRgbEarly = hexToRgb(darkBg)
    if (darkRgbEarly) {
      expect(stripRgb, '角标底色必须等于后端 dark 主题 semantic.surface-bg（角标不是装饰）').toBe(darkRgbEarly)
      expect(await skin.evaluate((el) => getComputedStyle(el).backgroundColor),
        '角标显示的底色必须就是画布真正渲染出来的底色').toBe(darkRgbEarly)
    }
    mark(`[skin] 进入组件库：画布投影=dark，角标 surface-bg=${stripBgText}，画布 computed=${await skin.evaluate((el) => getComputedStyle(el).backgroundColor)}`)
    const stateOptions = (await page.locator('select[aria-label="交互态"] option').allInnerTexts())
      .map((t) => t.trim()).filter((t) => t && !t.startsWith('自定义'))
    sameOrderAsVocabulary(stateOptions, axisValues('state'), '组件库补格子的状态下拉')
    // 状态下拉必须是"选"而不是"敲"：词表内的取值只能从选项里来（手打 `hove` 造幽灵状态的老路要堵住）
    await expect(page.locator('input[aria-label="自定义状态名"]')).toHaveCount(0)
    await page.locator('select[aria-label="交互态"]').selectOption('__custom__')
    await expect(page.locator('input[aria-label="自定义状态名"]')).toBeVisible()
    // 逃生口要能收回去（否则"自定义"变成单向门，用户选错就退不回词表）
    await page.locator('select[aria-label="交互态"]').selectOption('default')
    await expect(page.locator('input[aria-label="自定义状态名"]')).toHaveCount(0)

    /**
     * 8d. 变体轴同样"先选后拼"（v2.6.7）：轴下拉 = `meta.variantAxes` 的声明序，档位下拉 = 该轴的词表，
     * 拼出来的 variantJson 由界面生成且只读 —— 用户不再敲 `{"size":"md"}` 这种能拼错的东西；
     * 多轴组合留一个显式的"直接写 JSON"开关（前端不发明第二套序列化）。
     */
    const optionTexts = async (label: string): Promise<string[]> =>
      (await page.locator(`select[aria-label="${label}"] option`).allInnerTexts())
        .map((t) => t.trim()).filter((t) => t && !t.startsWith('选择') && !t.startsWith('自定义'))
    expect((await optionTexts('变体轴')).join(' '), '轴下拉必须等于 meta.variantAxes 的声明序')
      .toEqual(vocab.variantAxes.map((a) => a.axis).join(' '))
    // 「组件 *」不能永远停在占位符：清单是异步到的，到货后要自动选第一项（用户仍可改）
    await expect(page.locator('select[aria-label="变体组件"]')).not.toHaveValue('')
    await page.locator('select[aria-label="变体轴"]').selectOption('role')
    expect((await optionTexts('变体档位')).join(' '), '档位下拉必须等于该轴的词表序')
      .toEqual(axisValues('role').join(' '))
    await page.locator('select[aria-label="变体档位"]').selectOption('primary')
    await expect(page.locator('input[aria-label="拼好的 variantJson"]')).toHaveValue('{"role":"primary"}')
    const jsonToggle = page.locator('button[aria-label="切换 variantJson 输入方式"]')
    await jsonToggle.click()
    await expect(page.locator('input[aria-label="variantJson"]')).toBeEditable()
    await jsonToggle.click()
    await expect(page.locator('input[aria-label="拼好的 variantJson"]')).toHaveValue('{"role":"primary"}')

    // 截图要真的拍到被断言的那控件（表单在首屏之下，不滚就只是一张"看不出真假"的图）
    await page.locator('select[aria-label="交互态"]').scrollIntoViewIfNeeded()
    await shot(page, '05c-vocabulary-order')
    mark(`词表：${vocab.tiers.length} 层级 / ${vocab.colorFamilies.length} 色族 / ${vocab.auditKinds.length} 审计类别 / `
      + `${vocab.variantAxes.length} 条变体轴（state ${axisValues('state').length} 档）/ ${vocab.entities.length} 类逻辑实体`)

    // ---- 9. 审计：跑一次并落库，门禁结论可见 ----
    await nav(page, '审计与门禁').click()
    await page.getByRole('button', { name: /跑审计/ }).click()
    await expect(page.getByText(/critical|门禁|通过/).first()).toBeVisible({ timeout: 60_000 })
    const auditView = await apiData<{ summary: { total: number } }>(page, `/api/design-system/projects/${pid}/audit`)
    expect(auditView.summary.total).toBeGreaterThan(0)

    /**
     * 9b. 门禁必须盯"库里的现值"，不是盯生成器当初算得对不对（v2.6.0 新维度）。
     * 把按钮 sm 档可点高度压到 18px → 跑审计必须出现 target-size warning；改回 28px 再跑，warning 必须消失。
     * 这一段钉的是"手改不会被放行"，18 这个数字本身没有意义。
     */
    const setMinHeight = async (px: string): Promise<void> => {
      await apiPost(page, `/api/design-system/projects/${pid}/tokens/batch`, {
        overwrite: true,
        items: [{ path: 'component.button.sm.min-height', tier: 'component', type: 'dimension', value: px }],
      })
    }
    const targetWarnings = async (): Promise<number> => {
      const v = await apiData<{ items: { targetPath: string; severity: string }[] }>(
        page, `/api/design-system/projects/${pid}/audit?kind=target-size&passed=false`)
      return v.items.filter((a) => a.targetPath === 'component.button.sm.min-height' && a.severity === 'warning').length
    }

    await setMinHeight('18px')
    await page.getByRole('button', { name: /跑审计/ }).click()
    await expect.poll(targetWarnings, { timeout: 30_000, message: '低于 24px 的可点高度没被抓到 = 门禁只盯生成器、不盯手改' }).toBe(1)
    await shot(page, '05b-audit-target-size')

    await setMinHeight('28px')
    await page.getByRole('button', { name: /跑审计/ }).click()
    await expect.poll(targetWarnings, { timeout: 30_000, message: '改回 28px 后 warning 仍在 = 审计读了旧值' }).toBe(0)

    // 四类新维度都要在界面上说清在查什么（不许只剩一个英文 kind 名）
    for (const kind of ['target-size', 'ramp-monotonic', 'naming', 'lifecycle-ref']) {
      await expect(page.locator('.ab__kindmap-row').filter({ hasText: kind }), `门禁口径表里缺 ${kind}`).toBeVisible()
    }
    await shot(page, '05-audit')

    /**
     * 9c. 对比度这一维必须覆盖"库里的全部前景/背景对"，禁用态按 WCAG 1.4.3 豁免（v2.6.2）。
     * 钉两层：① 写死清单时代没人查的组件（dialog/tooltip/select）现在端点里必须有结论行；
     * ② `-disabled` 对必须带着读数落库且永不为 critical —— 豁免写成跳过=没人知道它多差，
     * 豁免写成拦截=第一次就把整套门禁的信任赔掉。
     */
    const contrast = (await apiData<{ items: { targetPath: string; rule: string; severity: string }[] }>(
      page, `/api/design-system/projects/${pid}/audit?kind=contrast`)).items
    const checkedFgs = new Set(contrast.map((a) => a.targetPath.split(':')[1] ?? ''))
    for (const name of ['component.dialog.foreground', 'component.tooltip.foreground', 'component.select.foreground'])
      expect(checkedFgs.has(name), `对比度没查 ${name} = 覆盖面还是那张写死的八对清单`).toBe(true)
    const exempt = contrast.filter((a) => a.rule.endsWith('-exempt'))
    expect(exempt.length, '禁用态整族没落库 = 豁免被写成了跳过').toBeGreaterThan(0)
    expect(exempt.filter((a) => a.severity === 'critical'), '1.4.3 豁免项不得为 critical').toEqual([])
    mark(`对比度结论 ${contrast.length} 行 / 前景对覆盖 ${checkedFgs.size} 个 / 禁用态豁免 ${exempt.length} 行（无 critical）`)

    /**
     * 判不成的对象必须自己报"无法判定"，不许从审计里静默消失：
     * 把 `component.card.foreground` 别名到尺度令牌（space.4 解析得出来，但它不是颜色）→ 跑审计必须出现 unresolved 行；
     * 改回真颜色别名 → 必须消失。这一段钉的是"没判成"和"没问题"在界面上必须长得不一样。
     */
    const setCardFg = async (alias: string): Promise<void> => {
      await apiPost(page, `/api/design-system/projects/${pid}/tokens/batch`, {
        overwrite: true,
        items: [{ path: 'component.card.foreground', tier: 'component', type: 'color', aliasPath: alias }],
      })
    }
    const unresolved = async (): Promise<number> => {
      const v = await apiData<{ items: { targetPath: string; rule: string }[] }>(
        page, `/api/design-system/projects/${pid}/audit?kind=contrast&passed=false`)
      return v.items.filter((a) => a.rule.endsWith('-unresolved') && a.targetPath.endsWith('component.card.foreground')).length
    }

    await setCardFg('space.4')
    await page.getByRole('button', { name: /跑审计/ }).click()
    await expect.poll(unresolved, { timeout: 30_000, message: '解析不出颜色却被静默跳过 = 界面上看不出这里有个没判成的对象' }).toBeGreaterThan(0)
    await setCardFg('semantic.text-1')
    await page.getByRole('button', { name: /跑审计/ }).click()
    await expect.poll(unresolved, { timeout: 30_000, message: '改回真颜色后 unresolved 仍在 = 审计读了旧值' }).toBe(0)

    // 界面上也必须说清"豁免不拦发布"，否则用户把它读成漏判
    await expect(page.locator('.ab__kindmap-row').filter({ hasText: 'contrast' })).toContainText('1.4.3')

    // ---- 10. 导出：产物来自后端投影，复合令牌不得是空声明 ----
    await nav(page, '导出交付').click()
    await expect(page.locator('.ds-main')).toBeVisible()

    /**
     * 10a. 十类逻辑实体要在界面上看得见（v2.6.7）：行 = `meta.entities`（后端 `ExportService.StardustEntities`），
     * 每行的「行数」是本页按当前主题**真的去读** `GET api/design-system/{id}/{entity}.json` 拿回的 `total` ——
     * 只摆链接不读，就还是"看起来能用"。
     */
    const entityMeta = await apiData<{ entities: string[] }>(page, '/api/design-system/meta')
    await expect
      .poll(async () => (await page.locator('.ec__table tbody tr td:last-child').allInnerTexts())
        .filter((t) => /^\d+$/.test(t.trim())).length,
      { timeout: 40_000, message: '实体表没把十类都读回行数（卡在"读取中…"或报了错的都不算合格）' })
      .toBe(entityMeta.entities.length)
    const shownEntities = (await page.locator('.ec__table tbody tr td:first-child').allInnerTexts()).map((t) => t.trim())
    expect(shownEntities.join(' '), '导出台列出的实体必须等于 meta.entities').toEqual(entityMeta.entities.join(' '))
    const shownTotals = (await page.locator('.ec__table tbody tr td:last-child').allInnerTexts()).map((t) => t.trim())
    const shownUrls = (await page.locator('.ec__table tbody tr a.ec__link').allInnerTexts()).map((t) => t.trim())
    expect(shownUrls.every((u) => u.includes(`/${pid}/`)), '每条 url 都要指向当前项目（不是模板占位）').toBe(true)
    mark(`十类实体读回行数：${shownEntities.map((e, i) => `${e}=${shownTotals[i]}`).join(' ')}`)
    // 拍到被断言的那张表本身（它在格式卡片之下，不滚就只拍到上面那排下载按钮）
    await page.locator('.ec__entities').scrollIntoViewIfNeeded()
    await shot(page, '08c-entity-sockets')

    /**
     * 10b. 导入 / 回流（M13）：界面走完「选文件 → 后端算差异 → 确认写入 → 库里读得到」，
     * 再用 HTTP 端点做 round-trip（导出 → 导入 → 再导出逐字一致）与反例（成环整批不写）。
     * 判据一律落在**库里的事实**上，不落在"页面提示成功"——那正是"看起来能用"的形状。
     */
    const importDoc = {
      color: { imported: { $type: 'color', demo: { $value: '#123456' } } },
      semantic: { 'imported-demo': { $type: 'color', $value: '{color.imported.demo}', $description: 'e2e 回流样本' } },
    }
    await page.locator('.ec__import').scrollIntoViewIfNeeded()
    /**
     * 导入写到哪一档，由这一页自己的主题下拉决定（`load()` 会把它同步到全局当前档）。
     * 断言必须跟着**页面实际用的那一档**走，而不是测试假设的 light ——
     * 本轮第一次红就是栽在这：界面在 dark 档，导入的行落在 dark 覆盖层，用 light 的有效视图去读当然是空。
     * 取 `select` 的 value 而不是页面文本：文本被 `.ds-micro` 的 `text-transform: uppercase` 显示成 `DARK`，
     * 拿它当主题码去查只是碰巧没坏（SQLite 的字符串比较不分大小写），换个后端就是假绿。
     */
    const importTheme = await page.locator('select[aria-label="导出主题"]').inputValue()
    expect(['shared', ...await page.locator('select[aria-label="导出主题"] option').allInnerTexts()].includes(importTheme),
      `页面用的主题档 ${importTheme} 必须在它自己的下拉里`).toBe(true)
    mark(`[import] 导入将写入的主题档（取自 select 的 value）=${importTheme}`)
    await page.locator('input[aria-label="选择 DTCG 文件"]').setInputFiles({
      name: 'e2e-import.json',
      mimeType: 'application/json',
      buffer: Buffer.from(JSON.stringify(importDoc), 'utf8'),
    })
    await expect(page.locator('[data-import-counts]'), '预览必须把"将写入几条"摆在界面上').toContainText('将写入 2 条', { timeout: 30_000 })
    expect(await effectiveValue(page, pid, importTheme, 'semantic.imported-demo'),
      '预览阶段就落库 = "先看差异"这一步是假的').toBe('')
    await page.locator('[data-import-confirm]').click()
    await expect(page.locator('[data-import-result]')).toContainText('新增 2', { timeout: 40_000 })
    await expect
      .poll(async () => await effectiveValue(page, pid, importTheme, 'color.imported.demo'),
        { timeout: 30_000, message: '导入的字面值没进库' })
      .toBe('#123456')
    expect(await effectiveValue(page, pid, importTheme, 'semantic.imported-demo'), '别名要顺到字面值').toBe('#123456')
    await shot(page, '13-import')

    type ImportResp = { created?: number; updated?: number; skippedProtected?: number; diagnostics?: { path: string; message: string }[] }
    const dtcgBefore = await apiText(page, `/api/design-system/projects/${pid}/export?format=dtcg&theme=light`)
    const roundTrip = await apiPost<ImportResp>(page, `/api/design-system/projects/${pid}/import?theme=light&overwrite=true`,
      JSON.parse(dtcgBefore) as unknown)
    expect(roundTrip.created ?? 0, '导入自己的导出文件不该新建任何路径（新建 = 把令牌搬家了）').toBe(0)
    const dtcgAfter = await apiText(page, `/api/design-system/projects/${pid}/export?format=dtcg&theme=light`)
    expect(dtcgAfter, 'export → import → export 必须逐字一致（不一致就是第二套真相）').toBe(dtcgBefore)

    const effCount = async (): Promise<number> =>
      (await apiData<{ count: number }>(page, `/api/design-system/projects/${pid}/tokens/effective?theme=light`)).count
    const beforeRing = await effCount()
    const ring = await apiPost<ImportResp>(page, `/api/design-system/projects/${pid}/import`, {
      semantic: { ringA: { $type: 'color', $value: '{semantic.ringB}' }, ringB: { $type: 'color', $value: '{semantic.ringA}' } },
    })
    const ringDiags = ring.diagnostics ?? []
    expect(ringDiags.length, '别名成环必须回诊断').toBeGreaterThan(0)
    expect((ring.created ?? 0) + (ring.updated ?? 0), '成环必须整批不写').toBe(0)
    expect(await effCount(), '整批回滚后有效值数量不许变').toBe(beforeRing)
    mark(`导入回流：UI 写入 2 条（别名顺到 #123456）· round-trip ${dtcgBefore.length} 字节逐字一致 · 成环拒写（诊断 ${ringDiags.length} 条）`)

    const css = await apiText(page, `/api/design-system/projects/${pid}/export?format=css&theme=light`)
    expect(css).toContain('--ds-semantic-surface-bg')
    expect(css).toMatch(/--ds-shadow-elevation-\d+:\s*\S/)
    expect(css).toContain('@media (prefers-reduced-motion: reduce)')
    const dtcg = await apiText(page, `/api/design-system/projects/${pid}/export?format=dtcg&theme=light`)
    expect(dtcg).toContain('$value')
    expect(dtcg).toContain('$type')
    // 交付体积要有真数字（规格 U1 / task T302 一直"未实测"）：单个工件在这里量（css / dtcg / data.sql）；
    // 整包的体积与构建耗时在后端用例里打点（`ExportProjectionTests` 打印 `[T302]`）。
    // 整包在浏览器侧的下载路径**暂不在本用例断言**：试过 `page.evaluate(fetch)`（Failed to fetch）
    // 与 `<a download>` 下载事件（`download.path: canceled`），两种取法都取不到 zip，根因待查（见 TODO）。
    const sql = await apiText(page, `/api/design-system/projects/${pid}/export?format=stardust-sql&theme=light`)
    expect(sql).toMatch(/INSERT INTO/i)
    expect(sql.length, 'data.sql 不得是空壳').toBeGreaterThan(css.length)

    /**
     * Element Plus 换肤接缝（spec U3）：宿主界面本身就是 EP 写的，"能为系统进行设计"要能真的把
     * `--el-*` 接到我们的令牌上。判据用自洽那条 —— 接缝引用的每个 `--ds-*` 必须由**同主题**的 tokens.css 真定义，
     * 引用不存在的变量会让整条声明在 computed-value 阶段失效（本仓踩过一次，表现是"预览突然全透明"）。
     */
    const elp = await apiText(page, `/api/design-system/projects/${pid}/export?format=element-plus&theme=light`)
    const definedVars = new Set([...css.matchAll(/(--ds-[a-z0-9-]+)\s*:/g)].map((m) => m[1]))
    const elpRefs = [...elp.matchAll(/var\((--ds-[a-z0-9-]+)\)/g)].map((m) => m[1])
    expect(elpRefs.length, '接缝一个引用都没有 = 它没在给 EP 上色').toBeGreaterThan(0)
    expect(elpRefs.filter((v) => !definedVars.has(v)), '接缝引用了 tokens.css 里没有的变量').toEqual([])
    expect(elp).toMatch(/--el-color-primary:\s*var\(--ds-semantic-brand\)/)
    expect(elp).toMatch(/--el-component-size:\s*var\(--ds-component-button-md-min-height\)/)
    expect(elp).not.toMatch(/color-mix\([^;]*,\s*--ds-/)
    expect(elp).not.toMatch(/#[0-9a-fA-F]{6}\b/)
    const elpKeys = [...elp.matchAll(/^\s*(--el-[a-z0-9-]+):/gm)].map((m) => m[1])
    expect(elpKeys.length, 'EP 接缝覆盖的变量数').toBeGreaterThanOrEqual(60)
    mark(`element-plus 接缝 ${elpKeys.length} 个变量 / 引用 ${elpRefs.length} 条令牌（全部由 tokens.css 定义）`)

    // 界面上要看得见这个工件：导出格式清单来自后端 `export/formats`，不是前端写死的列表
    const elpCard = page.locator('.ec__card').filter({ has: page.getByRole('heading', { name: 'element-plus' }) })
    await expect(elpCard, '导出中心没有 element-plus 这张卡片 = 格式清单没从后端读全')
      .toContainText('--el-color-primary', { timeout: 20_000 })

    /**
     * 10c. 接缝的**功能**证据。上面那些正则只证明"文件写得对"，不证明"接缝真的流到 EP 组件层"——
     * 而接缝声明的正是后者（"EP 组件即按本设计系统上色"）。宿主本身就是 Element Plus 应用，所以零 mock：
     * 把两份产物注进当前页面，放一个真 `.el-button--primary` 探针，读 EP 自己用来上色的那条变量
     * （`--el-button-bg-color`）是否解析成后端给的品牌色；最终像素值一并记录（见下面的职责边界说明）。
     */
    const brandHex = (await effectiveValue(page, pid, 'light', 'semantic.brand')).trim()
    expect(brandHex, '取不到 light 主题品牌色，无法做逐位比对').toMatch(/^#[0-9a-fA-F]{6}$/)
    const probe = (inject: boolean) => page.evaluate(async ({ tokens, elp: seam, on }) => {
      for (const id of ['e2e-ds-tokens', 'e2e-ds-elp']) document.getElementById(id)?.remove()
      if (on) {
        for (const [id, text] of [['e2e-ds-tokens', tokens], ['e2e-ds-elp', seam]] as [string, string][]) {
          const s = document.createElement('style')
          s.id = id
          s.textContent = text
          document.head.appendChild(s)
        }
      }
      await new Promise((r) => requestAnimationFrame(() => r(null)))
      const root = getComputedStyle(document.documentElement).getPropertyValue('--el-color-primary').trim()
      let el = document.getElementById('e2e-ds-probe')
      if (!el) {
        el = document.createElement('button')
        el.id = 'e2e-ds-probe'
        el.className = 'el-button el-button--primary'
        el.textContent = 'probe'
        document.body.appendChild(el)
      }
      const btn = getComputedStyle(el).backgroundColor
      // 诊断：递归进 @layer / @media / @supports，把"命中这个探针且声明了 background"的规则全找出来。
      // 不递归会漏掉 Tailwind v4 的 @layer —— 而宿主正是 Tailwind 写的，最终上色那条很可能藏在层里。
      const cs = getComputedStyle(el)
      const bgRules: string[] = []
      const walk = (rules: CSSRuleList | CSSRule[]) => {
        for (const r of Array.from(rules)) {
          const nested = (r as CSSGroupingRule).cssRules
          if (nested?.length) { walk(nested); continue }
          const sr = r as CSSStyleRule
          if (!sr.selectorText || !/background(?!-image|-blend|-origin|-repeat|-size|-position)/.test(sr.cssText)) continue
          try { if (el.matches(sr.selectorText)) bgRules.push(`${sr.selectorText}{${(sr.cssText.match(/\{[\s\S]*\}/)?.[0] ?? '').slice(0, 150)}}`) } catch { /* 非法选择器跳过 */ }
        }
      }
      for (const sh of Array.from(document.styleSheets)) {
        try { walk(sh.cssRules ?? []) } catch { /* 跨源样式表读不到，忽略 */ }
      }
      const rule = bgRules.slice(0, 5).join(' || ')
      const hasRule = bgRules.length > 0
      return {
        root,
        btn,
        hasRule,
        atEl: cs.getPropertyValue('--el-color-primary').trim(),
        bgVar: cs.getPropertyValue('--el-button-bg-color').trim(),
        rule,
      }
    }, { tokens: css, elp, on: inject })

    const before = await probe(false)
    const after = await probe(true)
    expect(after.root, '注入后 `--el-color-primary` 必须等于本主题品牌色').toBe(brandHex)
    expect(before.root, '注入前必须是 EP 自己的默认色，否则这条证明没有对照').not.toBe(brandHex)
    /**
     * 功能证据落在**EP 自己的组件层变量**上：`.el-button--primary` 的底色由 `--el-button-bg-color` 决定，
     * 它必须解析成我们的品牌色 —— 这才叫"接缝喂进了组件层"。
     * 至于最终 `background-color` 像素值：宿主自己有用直接规则钉按钮底色的地方（任何主题都盖不过它，
     * 包括宿主自带的琥珀主题），那属宿主侧样式，不是这份产物的职责边界 → 只记录、不当作接缝失败。
     */
    expect(after.atEl, '探针按钮处解析到的品牌变量必须是我们的色（接缝没流到组件层）').toBe(brandHex)
    expect(after.bgVar, 'EP 组件层变量 `--el-button-bg-color` 必须等于本主题品牌色').toBe(brandHex)
    expect(before.bgVar, '注入前 `--el-button-bg-color` 不该已经是我们的色（没有对照就证明不了因果）').not.toBe(brandHex)
    mark(`EP 接缝实测 --el-color-primary：${before.root || '（空）'} → ${after.root}（目标 ${brandHex}）；` +
      `组件层 --el-button-bg-color：${before.bgVar} → ${after.bgVar}；最终 background-color=${after.btn}` +
      `（命中规则：${after.rule.slice(0, 300) || '未找到声明 background 的命中规则'}）`)
    await page.evaluate(() => {
      document.getElementById('e2e-ds-probe')?.remove()
      for (const id of ['e2e-ds-tokens', 'e2e-ds-elp']) document.getElementById(id)?.remove()
    })
    await shot(page, '06-export')

    // ---- 11. 发布：不可变快照 + 哈希可核 + 项目版本回写 ----
    await nav(page, '版本与对比').click()
    const versionInput = page.getByLabel(/版本号/).first()
    await expect(versionInput).toBeVisible({ timeout: 20_000 })
    await versionInput.fill('1.0.0')
    await page.getByRole('button', { name: /创建不可变快照/ }).click()
    await expect
      .poll(async () => (await apiData<{ version: string }[]>(page, `/api/design-system/projects/${pid}/releases`)).length,
        { timeout: 40_000 })
      .toBeGreaterThan(0)
    const releases = await apiData<{ version: string; tokensHash: string; tokenCount: number; snapshotAvailable: boolean }[]>(
      page, `/api/design-system/projects/${pid}/releases`)
    expect(releases[0].version).toBe('1.0.0')
    expect(releases[0].tokensHash).toMatch(/^[0-9a-f]{64}$/)
    expect(releases[0].tokenCount).toBeGreaterThan(0)
    expect(releases[0].snapshotAvailable).toBe(true)
    expect(await projectField(page, pid, 'version')).toBe('1.0.0')
    await shot(page, '07-release')

    // ---- 12. 内置图标库：≥24 枚、零许可证负担、真画出了图形 ----
    await nav(page, '图标库').click()
    await expect(page.locator('svg').first()).toBeVisible({ timeout: 30_000 })
    const icons = await apiData<{ license: string; svgBody: string | null; collection: string }[]>(
      page, `/api/design-system/icons?projectId=${pid}`)
    const builtin = icons.filter((i) => i.collection === 'forge')
    expect(builtin.length).toBeGreaterThanOrEqual(24)
    expect(builtin.every((i) => i.license === 'Owned')).toBe(true)
    expect(builtin.every((i) => /<(path|circle|rect|ellipse)/.test(i.svgBody ?? ''))).toBe(true)
    await shot(page, '08-icons')

    // ---- 13. 换肤：预览底色必须等于后端该主题令牌值，且切主题真的重注入对应投影 ----
    // （`skin` / `strip` 定位器已在 8c-画布 声明，这里复用同一个"画布"身份，不再另起一份）
    await nav(page, '组件库').click()
    await expect(skin).toBeVisible({ timeout: 30_000 })
    const skinBg = () => skin.evaluate((el) => getComputedStyle(el).backgroundColor)
    const skinStyle = () =>
      page.locator('.ds-skin style').evaluateAll((els) => els.map((e) => e.textContent ?? '').join('\n'))

    /**
     * 点主题 → 先确认界面态真的切了档（页脚显示当前主题码），再等后端投影跟上（注入的 CSS 头部
     * 导出注释里带 theme=…）。两段分开，失败时能指出断在哪一环：状态没变 vs 没重取导出。
     */
    async function pickThemeAndReadBg(code: string, label: RegExp): Promise<string> {
      const before = (await skinStyle()).slice(0, 96)
      mark(`[skin] 切到 ${code} 之前：注入 CSS 头部=${before}`)
      await page.locator('.ds-chip').filter({ hasText: label }).first().click()
      await expect(page.locator('.ds-footer'), `点击后当前主题应为 ${code}`).toContainText(code, { timeout: 15_000 })
      mark(`[skin] 界面档位已到 ${code}（页脚已确认），等待后端投影重取…`)
      await expect
        .poll(async () => await skinStyle(), { timeout: 40_000, message: `未注入 theme=${code} 的导出 CSS` })
        .toContain(`theme=${code} `)
      await expect
        .poll(async () => await skin.getAttribute('data-skin-theme'), { timeout: 40_000, message: `画布角标未认账 theme=${code}` })
        .toBe(code)
      const bg = await skinBg()
      expect(bg).not.toBe('rgba(0, 0, 0, 0)')
      mark(`[skin] 切到 ${code} 完成：底色=${bg}`)
      return bg
    }

    const lightSkinBg = await pickThemeAndReadBg('light', /浅色|light/i)
    const lightRgb = hexToRgb(lightBg)
    if (lightRgb) expect(lightSkinBg, '预览底色须等于后端 semantic.surface-bg').toBe(lightRgb)
    // 角标必须跟着换档（它是"画布现在是谁"的唯一可见信号，截图靠它才读得说明暗属于哪一档）
    await expect(strip).toContainText('画布投影 light', { timeout: 15_000 })
    await expect(strip).toContainText(`surface-bg ${lightBg}`)
    // 组件样张必须真在渲染（目录来自生成，不是空面板）
    await expect
      .poll(async () => await page.locator('.cg__card').count(), { timeout: 25_000, message: '组件库没有渲染出任何样张' })
      .toBeGreaterThanOrEqual(10)
    // 变体矩阵同样不得是空的：格子由 component.* 令牌推导（历史坑：目录有规格但矩阵 0 条 = 半个空壳）
    await expect
      .poll(async () => await page.locator('.cg__vars li').count(), { timeout: 25_000, message: '组件变体矩阵仍为空' })
      .toBeGreaterThanOrEqual(20)
    const comps = await apiData<{ code: string }[]>(page, `/api/design-system/projects/${pid}/components`)
    expect(comps.some((c) => c.code === 'button'), '按钮必须在组件目录里').toBe(true)
    const cells = await apiData<{ state: string; tokenRefsJson: string; variantJson?: string }[]>(
      page, `/api/design-system/projects/${pid}/components/button/variants`)
    expect(cells.length).toBeGreaterThan(0)
    expect(cells.map((v) => v.state)).toContain('hover')
    expect(cells.every((v) => /\[.+\]/.test(v.tokenRefsJson ?? '')), '每格都要挂着真令牌清单').toBe(true)
    /**
     * 状态序只有一个真源：后端词表 `meta.variantAxes` 里的 state 轴（与 DESIGN.md / registry 同一张 `VariantAxes` 表）。
     * 历史坑：三处各排各的（前端 `.sort()`、读路径按 State 字母序、导出按字母序），
     * 同一份库在界面看到 `default、disabled、hover、active`，在 DESIGN.md 里又是另一套。
     * 本轮就是被这条断言抓出来的 —— 所以它必须留在 e2e 里。
     */
    const metaInfo = await apiData<{ variantAxes: { axis: string; values: string[] }[] }>(page, '/api/design-system/meta')
    const stateOrder = metaInfo.variantAxes.find((a) => a.axis === 'state')?.values ?? []
    expect(stateOrder[0], '词表由后端供给且 default 打头').toBe('default')
    const vocabRank = (s: string): number => {
      const i = stateOrder.indexOf(s)
      return i < 0 ? Number.MAX_SAFE_INTEGER : i
    }
    const wantStates = [...new Set(cells.map((v) => v.state))].sort((a, b) => vocabRank(a) - vocabRank(b)).join('、')
    const stateChips = (await page.locator('.cg__card')
      .filter({ has: page.locator('.cg__code', { hasText: /^button$/ }) })
      .locator('.cg__state-chip').allInnerTexts()).join('、')
    expect(stateChips, '界面状态序必须等于后端词表的档位序（前端不许多一套排序）').toBe(wantStates)
    const mdLines = (await apiText(page, `/api/design-system/projects/${pid}/export?format=design-md`)).split('\n')
    const mdFrom = mdLines.findIndex((l) => l.startsWith('- `button`（'))
    expect(mdFrom, 'DESIGN.md 里必须真有按钮这一条').toBeGreaterThan(-1)
    const mdStates = mdLines.slice(mdFrom + 1).find((l) => l.includes('状态：'))?.split('状态：')[1]?.trim()
    expect(mdStates, 'DESIGN.md 的状态序必须等于同一张词表的档位序').toBe(wantStates)
    // 尺寸轴要在界面上看得见（size=sm/md/lg），否则"矩阵"只是角色×状态的薄薄一层
    const matrixText = (await page.locator('.cg__vars').allInnerTexts()).join('\n')
    expect(matrixText, '组件矩阵里要出现尺寸轴').toMatch(/size=(sm|md|lg)/)
    /**
     * 变体**分组序**也要按轴档位序（v2.6.7）：`ListVariants` 以前按 `VariantKey` 排，而那是 JSON 文本的字典序 ——
     * `{"role":"danger"}` 会排在 `{"role":"primary"}` 前面。矩阵是设计师核对档位的那张表，字母序凑它等于没有序。
     * 这里只检"同轴的值按词表序出现"这一条性质（不把后端的排序算法在测试里再实现一遍）。
     */
    for (const axis of ['role', 'size']) {
      const order = metaInfo.variantAxes.find((a) => a.axis === axis)?.values ?? []
      expect(order.length, `词表里必须有 ${axis} 轴`).toBeGreaterThan(0)
      const seen = [...new Set(cells.map((c) => {
        try {
          const v: unknown = (JSON.parse(c.variantJson ?? '{}') as Record<string, unknown>)[axis]
          return typeof v === 'string' ? v : null
        } catch { return null }
      }).filter((v): v is string => v !== null))]
      if (seen.length < 2) continue
      expect(seen.join(' '), `${axis} 轴的分组序必须等于词表序（${order.join('/')}）`)
        .toEqual(order.filter((v) => seen.includes(v)).join(' '))
      mark(`按钮矩阵 ${axis} 轴分组序：${seen.join(' → ')}`)
    }
    await shot(page, '09-components')

    const darkSkinBg = await pickThemeAndReadBg('dark', /深色|dark/i)
    expect(darkSkinBg).not.toBe(lightSkinBg)
    const darkRgb = hexToRgb(darkBg)
    if (darkRgb) expect(darkSkinBg, '预览底色须等于后端 semantic.surface-bg').toBe(darkRgb)
    await expect(strip).toContainText('画布投影 dark', { timeout: 15_000 })

    /**
     * 竞态守卫（真实环境版）。历史故障：连点主题时多个 `export?format=css` 同时在途，
     * "await 完直接赋值"会让**后到的旧主题响应覆盖新主题** —— 表现是"点了浅色但预览还是深色"，
     * 同一份代码一跑失败一跑通过（单测 `web/src/state.test.ts` 用受控 promise 稳定复现，这里在真 HTTP 上再钉一次）。
     * 下面故意连续三次换档、中间不等待：最后一次是 light，最终停留的投影就必须是 light。
     */
    const chip = (label: RegExp) => page.locator('.ds-chip').filter({ hasText: label }).first()
    await chip(/浅色|light/i).click()
    await chip(/深色|dark/i).click()
    await chip(/浅色|light/i).click()
    await expect(page.locator('.ds-footer')).toContainText('light', { timeout: 15_000 })
    await expect
      .poll(async () => await skinStyle(), { timeout: 60_000, message: '连点三次后最终停留的投影不是最后一次选择的 light' })
      .toContain('theme=light ')
    expect(await skinBg(), '连点后底色必须落在最后一次选择的 light').toBe(lightRgb ?? lightSkinBg)
    await expect(strip, '连点三次后角标必须认账最终档').toContainText('画布投影 light', { timeout: 15_000 })
    await shot(page, '10b-skin-rapid-switch')

    await nav(page, '品牌展示页').click()
    await expect(skin).toBeVisible({ timeout: 30_000 })
    // 换页也要认账：投影随选中的档重取，品牌展示页的截图才知道自己是哪一档
    await expect
      .poll(async () => await skin.getAttribute('data-skin-theme'), { timeout: 40_000, message: '品牌展示页画布投影不是 light' })
      .toBe('light')
    await shot(page, '10-showcase')

    /**
     * 13c. 「待重取 → 到货」跨页闭环（v2.6.8 修的正是这条链路）。画布此刻是 light：
     * 在中性外壳页切 dark → 主题条必须显式报 `pending`（画布仍是 light、界面已选 dark，这个差值过去看不见）；
     * 回到预览页 → 必须自动应用 dark，且角标底色与后端 dark 令牌逐位对上。以前只能靠肉眼看截图明暗。
     */
    await nav(page, '令牌工作台').click()
    await expect(page.locator('.ts__table').first()).toBeVisible({ timeout: 30_000 })
    await chip(/深色|dark/i).click()
    await expect
      .poll(() => page.locator('.ds-skinstate').getAttribute('data-skin-state'),
        { timeout: 15_000, message: '外壳页切档没进入 pending（界面与画布的档位差必须看得见）' })
      .toBe('pending')
    await expect(page.locator('.ds-skinstate')).toContainText('待重取 light → dark')
    mark(`[skin] 外壳页切 dark：${await page.locator('.ds-skinstate').innerText()}`)
    await nav(page, '组件库').click()
    await expect
      .poll(() => skin.getAttribute('data-skin-theme'), { timeout: 40_000, message: '回到预览页后画布没应用 dark' })
      .toBe('dark')
    await expect(strip).toContainText('画布投影 dark', { timeout: 15_000 })
    if (darkRgb) expect(await skinBg(), '跨页待重取后画布底色必须等于 dark 令牌').toBe(darkRgb)
    await shot(page, '10c-skin-cross-page')
    await chip(/浅色|light/i).click()
    await expect
      .poll(() => skin.getAttribute('data-skin-theme'), { timeout: 40_000, message: '在预览页切回 light 后画布没跟上' })
      .toBe('light')

    // ---- 13b. 品牌资产：meta.capabilities 声明了 assets/fonts/screens，就必须真拿得到、也写进去 ----
    // （历史缺陷：这三项只有 GET、也没人种数据 → 页面永远是"无…"，属于"声明了却拿不到"的假能力）
    await nav(page, '品牌资产').click()
    const [brandAssets, brandFonts, brandScreens] = await Promise.all([
      apiData<{ code: string; svgBody: string | null; license: string | null }[]>(page, `/api/design-system/projects/${pid}/assets`),
      apiData<{ family: string; license: string | null }[]>(page, `/api/design-system/projects/${pid}/fonts`),
      apiData<{ code: string; route: string }[]>(page, `/api/design-system/projects/${pid}/screens`),
    ])
    expect(brandAssets.length, '生成至少要落 logo + motif 两条资产').toBeGreaterThanOrEqual(2)
    expect(brandAssets.every((a) => /<(path|circle|rect|ellipse)/.test(a.svgBody ?? '')), '资产要真有图形本体').toBe(true)
    expect(brandAssets.every((a) => !/#[0-9a-f]{3,8}/i.test(a.svgBody ?? '')), '资产图形不得烤死色值（换肤会失效）').toBe(true)
    expect(brandFonts.length, '字体栈要登记进库').toBeGreaterThan(0)
    expect(brandFonts.every((f) => (f.license ?? '').trim().length > 0), '字体许可证不得留空').toBe(true)
    expect(brandScreens.length, '页面清单要落库').toBeGreaterThan(0)
    // 界面不得还停在空态文案上
    for (const emptyText of ['无资产', '无字体登记', '无页面清单']) {
      await expect(page.getByText(emptyText), `品牌资产页仍显示空态「${emptyText}」`).toHaveCount(0)
    }
    // 写入口要闭环：从界面登记一条页面 → 后端库回读得到（只测读等于没测这条能力）
    const screenCode = `${RUN_CODE}-scr`.slice(0, 24)
    const screenPanel = page.locator('.ba__panel').filter({ hasText: '页面清单' })
    await expect(screenPanel.getByRole('button', { name: '登记页面' })).toBeVisible({ timeout: 20_000 })
    await screenPanel.getByLabel('code *').fill(screenCode)
    await screenPanel.getByLabel('route *').fill('/e2e-brand')
    await screenPanel.getByRole('button', { name: '登记页面' }).click()
    let screensAfterWrite: { code: string; route: string }[] = []
    await expect
      .poll(async () => {
        screensAfterWrite = await apiData<{ code: string; route: string }[]>(page, `/api/design-system/projects/${pid}/screens`)
        return screensAfterWrite.find((s) => s.code === screenCode)?.route ?? ''
      }, { timeout: 30_000, message: '界面登记的页面没有进后端库' })
      .toBe('/e2e-brand')

    /**
     * M10 / spec FR13：Stardust 清单里每行的 url 必须**真能取到同样多的行**。
     * 缺陷的历史形状是"清单写死 0 行 / 明细是空数组"，而库里明明有图标、字体、起手屏、组件目录；
     * 且 url 指向的路由过去根本不存在。这里走真实宿主（内置图标库由插件启动时种进库）。
     */
    const getJson = async <T,>(u: string): Promise<T> => {
      const raw = await apiText(page, u)
      try { return JSON.parse(raw) as T } catch { throw new Error(`${u} 取回的正文不是 JSON：${raw.slice(0, 200)}`) }
    }
    const stardust = await getJson<{ entities: { entity: string; url: string; total: number }[] }>(
      `/api/design-system/projects/${pid}/export?format=stardust-json`)
    expect(stardust.entities.length, '十类逻辑实体都要在清单里').toBe(10)
    for (const e of stardust.entities) {
      const detail = await getJson<{ entity?: string; total?: number; data?: unknown[] }>(e.url)
      expect(detail.data, `${e.entity} 的 url（${e.url}）没取到明细`).toBeInstanceOf(Array)
      expect(detail.entity, `${e.entity} 明细回的实体名要对得上`).toBe(e.entity)
      expect(detail.data!.length, `${e.entity} 清单说 ${e.total} 行，明细就得给几行`).toBe(e.total)
    }
    const totalOf = (name: string) => stardust.entities.find((x) => x.entity === name)?.total ?? -1
    const catalogComponents = await apiData<{ code: string }[]>(page, `/api/design-system/projects/${pid}/components`)
    expect(totalOf('design-component'), 'design-component 数的必须是目录里的组件数（过去数的是令牌条数）')
      .toBe(catalogComponents.length)
    expect(totalOf('design-icon'), '内置图标随项目可用，清单不能再写死 0').toBeGreaterThan(0)
    expect(totalOf('design-font-face'), '字体登记有行，清单不能再写死 0').toBeGreaterThan(0)
    expect(totalOf('design-screen'), `界面刚登记的 ${screenCode} 必须算进页面实体`).toBeGreaterThanOrEqual(5)

    // 组件规格要能在 registry 工件里读到本体（解剖 / 状态 / a11y 约束），不是只有一串令牌路径
    const registry = await getJson<{
      items: { name: string; meta?: { a11yNotes?: string; anatomy?: string[]; states?: string[]; variants?: { state: string; axes: Record<string, string> }[]; unresolvedTokenRefs?: string[] } }[]
    }>(`/api/design-system/projects/${pid}/export?format=registry`)
    const button = registry.items.find((i) => i.name === 'button')
    expect(button?.meta?.anatomy?.length ?? 0, 'registry 里按钮没有解剖清单').toBeGreaterThan(0)
    expect(button?.meta?.a11yNotes ?? '', 'registry 里按钮没有可达性约束').not.toHaveLength(0)
    for (const state of ['default', 'hover', 'active', 'disabled']) {
      expect(button?.meta?.states ?? [], `按钮状态里缺 ${state}`).toContain(state)
    }
    expect(button?.meta?.variants?.some((v) => v.axes?.size === 'lg'), '尺寸轴没进产物').toBe(true)
    expect(registry.items.every((i) => (i.meta?.unresolvedTokenRefs ?? []).length === 0),
      '产物里出现"引用了工件中不存在的令牌"= 规格与令牌两份真相').toBe(true)
    mark(`Stardust 实体行数 ${stardust.entities.map((x) => `${x.entity}=${x.total}`).join(' ')}`)
    mark(`registry button 解剖=${button?.meta?.anatomy?.length ?? 0} 状态=${(button?.meta?.states ?? []).join('/')} 格子=${button?.meta?.variants?.length ?? 0}`)

    /**
     * M9：界面写进去的东西必须能在**两版 diff 里现形**。
     * 快照与 diff 过去只覆盖令牌 —— 换了 logo、改了字体许可证、加删一个起手屏，两版之间看不出来，
     * 同版本号重发还会被当成"内容一致"幂等放行；那条幂等路径由后端用例钉，这里钉"用户看得见的这一条"。
     */
    await apiPost(page, `/api/design-system/projects/${pid}/releases`, { version: '1.1.0', notes: 'e2e 品牌写入后' })
    const rels = await apiData<{ id: number; version: string }[]>(page, `/api/design-system/projects/${pid}/releases`)
    const v1 = rels.find((r) => r.version === '1.0.0')
    const v2 = rels.find((r) => r.version === '1.1.0')
    expect(v1 && v2, '两次发布都要能回读到').toBeTruthy()
    const specDiff = await apiData<{ specsComparable: boolean; specsAdded: { kind: string; key: string }[]; total: number }>(
      page, `/api/design-system/projects/${pid}/releases/diff?from=${v1!.id}&to=${v2!.id}`)
    expect(specDiff.specsComparable, '两版都是 schema 2 快照，规格节必须可比').toBe(true)
    expect(specDiff.specsAdded.some((s) => s.kind === 'screen' && s.key === screenCode),
      `界面写入的页面 ${screenCode} 必须出现在两版 diff 的新增里`).toBe(true)
    expect(specDiff.total, '只改品牌时合计也不能报 0').toBeGreaterThan(0)

    // 界面侧也要看得见这一节（只在 API 里对得上、界面上没有 = 用户仍然看不见变化）
    await nav(page, '版本与对比').click()
    const pickRelease = async (nth: number, want: string): Promise<void> => {
      const sel = page.locator('.rb__diff-pick select').nth(nth)
      // Vue 的 `:value="o.id"` 设的是 DOM **属性**不是 attribute，getAttribute('value') 会是 null
      const value = await sel.locator('option').filter({ hasText: want }).first()
        .evaluate((el) => (el as HTMLOptionElement).value)
      await sel.selectOption(value)
    }
    await pickRelease(0, '1.0.0')
    await pickRelease(1, '1.1.0')
    const compare = page.getByRole('button', { name: '对比', exact: true })
    await expect(compare, '选了两个版本后「对比」仍不可用 = 版本没选上').toBeEnabled({ timeout: 10_000 })
    await compare.click()
    await expect(page.locator('.rb__specs'), '对比面板里没有「品牌与组件规格」这一节').toContainText(screenCode, { timeout: 30_000 })
    await shot(page, '11b-release-spec-diff')
    await nav(page, '品牌资产').click()
    /**
     * 图标 code 要按库里的大小写**原样**显示（v2.6.7）：这一行原先整条套在 `.ds-micro`（`text-transform: uppercase`）里，
     * 屏幕上是 `DASHBOARD`、库里是 `dashboard` —— 用户照屏幕抄回接口就 404，标识的可抄性比版式重要。
     */
    const screens = await apiData<{ code: string; iconCode?: string | null }[]>(page, `/api/design-system/projects/${pid}/screens`)
    const withIcon = screens.find((s) => (s.iconCode ?? '').length > 0)
    expect(withIcon, '页面清单里要至少有一条带图标的记录，否则这条判据没有证据').toBeTruthy()
    const iconShown = (await page.locator('.ba__row').filter({ hasText: withIcon!.code }).first()
      .locator('.ds-mono').last().innerText()).trim()
    expect(iconShown, '界面上的图标 code 必须与库里逐字一致（不能被 text-transform 改成大写）').toBe(withIcon!.iconCode)
    mark(`图标 code 原样显示：${iconShown}`)
    await shot(page, '11-brand-assets')

    // ---- 14. 长内容不得撑破容器 ----
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)
    expect(overflow).toBeLessThanOrEqual(2)

    // ---- 15. 归档（危险动作）两条路径：取消不得改后端状态，确认才改 ----
    await nav(page, '项目与生成').click()
    page.once('dialog', (d) => void d.dismiss())
    await page.getByRole('button', { name: '归档本项目' }).click()
    await new Promise((r) => setTimeout(r, 800))
    expect(await projectField(page, pid, 'status')).not.toBe('archived')

    page.once('dialog', (d) => void d.accept())
    await page.getByRole('button', { name: '归档本项目' }).click()
    await expect
      .poll(async () => await projectField(page, pid, 'status'), { timeout: 20_000 })
      .toBe('archived')
    await shot(page, '12-archived')

    // ---- 16. 刷新后仍从库里读得到（库驱动，不靠 localStorage）----
    await page.reload()
    await expect(page.locator('.ds-root')).toBeVisible({ timeout: 30_000 })
    await enterWorkbench(page)
    await expect(page.getByText(RUN_CODE).first()).toBeVisible({ timeout: 30_000 })

    // ---- 17. 无致命控制台错误（组件解析/模块加载一类）----
    const fatalPatterns = [
      /Failed to resolve component/i,
      /does not provide an export named/i,
      /Failed to (fetch|resolve) dynamically imported module/i,
      /Failed to load module script/i,
      /is not defined/i,
    ]
    const fatal = evidence.consoleErrors.filter((l) => fatalPatterns.some((re) => re.test(l)))
    mark(`project=${RUN_CODE} id=${pid}`)
    mark(`plugin.json Version=${MANIFEST.Version} meta.modelVersion=${meta.modelVersion}`)
    mark(`light.surface-bg=${lightBg} dark.surface-bg=${darkBg}`)
    mark(`light.space.4=${lightGap} compact.space.4=${compactGap}`)
    mark(`release=${releases[0].version} hash=${releases[0].tokensHash.slice(0, 12)}… tokens=${releases[0].tokenCount}`)
    mark(`内置图标=${builtin.length} 导出CSS=${(css.length / 1024).toFixed(1)}KiB data.sql=${(sql.length / 1024).toFixed(1)}KiB DTCG=${(dtcg.length / 1024).toFixed(1)}KiB`)
    mark(`品牌资产=${brandAssets.length} 字体=${brandFonts.length} 页面=${brandScreens.length}（种子计数，取于写入前）`)
    mark(`界面写入页面 ${screenCode} 回读后 页面=${screensAfterWrite.length}`)
    mark(`预览容器背景 浅色=${lightSkinBg}（期望 ${lightRgb ?? lightBg}）深色=${darkSkinBg}（期望 ${darkRgb ?? darkBg}）`)
    // 一类已被踩过的实现错误：给 <input type="color"> 塞空串（浏览器会告警，且色板显示成默认黑）
    const colorNoise = evidence.consoleAll.filter((l) => /does not conform to the required format/i.test(l))
    expect(colorNoise, `控制台出现取色器空值告警 ${colorNoise.length} 条`).toEqual([])
    expect(fatal, `控制台出现致命报错：\n${fatal.join('\n')}`).toEqual([])
  })
})
