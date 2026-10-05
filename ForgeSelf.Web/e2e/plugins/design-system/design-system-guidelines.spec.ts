import { test, expect, type Page } from '@playwright/test'
import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { injectRealApiKey } from '../../helpers/real-auth'
import { apiData, apiPost, attachCollectors, dumpEvidence, shot, type Evidence } from './design-system-helpers'

/**
 * 设计系统插件 M3 切片 B（UX 规范）端到端验证 · AC19 / AC20。
 *
 * 后端单测能证明"服务层写进去了"，证明不了这三件事，本文件专门盯它们：
 *  G1 = 第 15 个入口真的进得去，界面列出的条数与 REST 一致，而且 **chip 上那个数字就是令牌页的当前值**
 *       （不是前端自己算的第二个数，也不是写死的示例值）。
 *  G2 = 在界面改一条规范 → **后端库里回读得到同一份**；归档/恢复两条路径各测一次，
 *       并确认归档是软删（数据还在、能恢复、默认清单不显示）。
 *  G3 = 「重新生成默认规范」只补空：跑两次条数不翻倍，手改行被点名保护。
 *
 * 纪律与 M2/M3-A 一致：真实宿主零 mock、唯一项目码、截图前先让断言到位、收尾不硬删数据。
 */

const MANIFEST = JSON.parse(
  readFileSync(fileURLToPath(new URL('../../../../Plugins/DesignSystem/plugin.json', import.meta.url)), 'utf-8').replace(/^\uFEFF/, ''),
) as { Version: string; frontend: { route: string } }

const PLUGIN_ROUTE = MANIFEST.frontend.route
const RUN_CODE = `e2e-m3b-${Date.now().toString(36)}`

interface GuidelineRow {
  code: string
  title: string
  bodyRaw: string
  source: string
  status: string
  tokenRefs: string[]
  tokenValues: Record<string, string | null>
  valueTheme?: string | null
  rules: { id: string; level: string; text: string; textRaw: string }[]
}

interface ProjectRow {
  id: number
  code: string
}

/** MCP 网关的 JSON-RPC 信封（tools/list 用 result，非法工具名用 error） */
interface JsonRpcReply {
  result?: unknown
  error?: { code?: number; message?: string }
}

/** design_guide 的自述负载（G7 只取判据要用的字段，形状由 DesignTools.cs 决定） */
interface GuidePayload {
  version: string
  tools: { name: string; kind: string; summary: string }[]
  workflows: { id: string; title: string; steps: string[] }[]
  discovery: string
  access: { allowWrite: boolean }
}

const norm = (s: string): string => s.replace(/\s+/g, ' ').trim()
const guidelines = (page: Page, pid: number, status?: string): Promise<GuidelineRow[]> => {
  const base = '/api/design-system/projects/' + pid + '/guidelines'
  return apiData<GuidelineRow[]>(page, status ? base + '?status=' + status : base)
}
const projectField = async (page: Page, pid: number, field: 'status'): Promise<unknown> => {
  const url = '/api/design-system/projects/' + pid
  const p = await apiData<Record<string, unknown>>(page, url)
  return p[field]
}

async function enterWorkbench(page: Page): Promise<void> {
  await page.getByRole('tab', { name: '工作台', exact: true }).click()
  await expect(page.locator('.ds-nav')).toBeVisible({ timeout: 20_000 })
}

const nav = (page: Page, label: string) => page.getByRole('button', { name: label, exact: true })

/** 建一个"生成过"的项目：走后端 quick-create（apply=true），规范由生成链路自己种进库 */
async function createProject(page: Page): Promise<ProjectRow> {
  await apiPost<unknown>(page, '/api/design-system/projects/quick-create', {
    name: 'E2E 规范项目',
    code: RUN_CODE,
    kind: 'console',
    description: '运维监控告警平台',
    preset: 'admin-calm',
    apply: true,
  })
  const projects = await apiData<ProjectRow[]>(page, '/api/design-system/projects')
  const created = projects.find((p) => p.code === RUN_CODE)
  expect(created, 'quick-create 必须真的落库').toBeTruthy()
  return created!
}

async function pickProject(page: Page, code: string): Promise<void> {
  await nav(page, '项目与生成').click()
  // 行匹配必须**锚定到 code 结尾**：G5 建了一条 `<RUN_CODE>-bare`，用裸前缀会同时命中两行，
  // `row.isVisible()` 在严格模式下当场抛错（被下面的 catch 吞成 false），轮询就永远等不到。
  const esc = code.replace(/[.*+?^${}()|[\]\\]/g, String.raw`\$&`)
  const row = page.getByRole('row', { name: new RegExp(`^${esc}(?![\\w-])`) })
  // 判据不变：清单里必须**看得见这一行**。但 `Projects.vue` 的挂载读取条件是「清单为空才读」
  // （`onMounted(() => { if (!projects.value.length) void loadProjects() })`），
  // 同一库里只要已有别的项目，切进来就不会自动刷新 —— 本用例是**先经 REST 建项目再看界面**，
  // 所以在全目录串行跑（前面别的 spec 已建过项目）时会稳定看不见。用户侧的真实出口是表头的「重新读取」，
  // 这里就是点它；并把"点第几次才出现"记成读数，**不放宽判据**（见 05「E2E · 全目录回归」的 G1 归因）。
  const reload = page.getByRole('button', { name: /重新读取|读取中/ })
  let attempts = 0
  await expect
    .poll(
      async () => {
        attempts++
        await reload.first().click({ noWaitAfter: true }).catch(() => {})
        return row.isVisible().catch(() => false)
      },
      { timeout: 30_000, intervals: [300, 700, 1500] },
    )
    .toBe(true)
  console.log(`[guidelines] 项目清单刷新第 ${attempts} 次才看见 ${code}（>1 即「挂载不刷新」的读数）`)
  await expect(row).toBeVisible()
  // 判据是「这一行成了工作项目」，不是「必须点一次按钮」：工作台进页面会把清单第一条自动选为工作项目
  // （state.loadProjects），此时按钮变成禁用的「当前」，硬点「选为工作项目」只会等到超时。
  const current = row.getByRole('button', { name: '当前', exact: true })
  if (!(await current.isVisible().catch(() => false))) {
    await row.getByRole('button', { name: '选为工作项目', exact: true }).click()
  }
  await expect(current).toBeVisible({ timeout: 20_000 })
}

async function openGuidelines(page: Page): Promise<void> {
  await nav(page, 'UX 规范').click()
  await expect(page.locator('[data-guidelines]')).toBeVisible({ timeout: 20_000 })
  await expect(page.locator('[data-guideline-code]').first()).toBeVisible({ timeout: 20_000 })
}

/* ---------------- 证据：无论通过与否都落一份 ---------------- */

let sink: { evidence: Evidence; extra: string[] } | null = null

// eslint-disable-next-line no-empty-pattern
test.afterEach(({}, testInfo) => {
  if (!sink) return
  const status = testInfo.status === testInfo.expectedStatus ? 'passed' : 'FAILED'
  const key = testInfo.title.trim().slice(0, 2)
  dumpEvidence(`guideline-${key}-${status}`, sink.evidence, sink.extra)
})

test.describe.configure({ mode: 'serial', timeout: 300_000 })

let pid = 0

test('G1 第 15 个入口进得去：条数与 REST 一致，chip 数值等于令牌页当前值', async ({ page }) => {
  const evidence = attachCollectors(page)
  const extra: string[] = []
  sink = { evidence, extra }
  const mark = (s: string) => extra.push(s)

  await injectRealApiKey(page)
  await page.goto(PLUGIN_ROUTE)
  await expect(page.locator('.ds-root')).toBeVisible({ timeout: 30_000 })

  pid = (await createProject(page)).id
  const server = await guidelines(page, pid)
  expect(server.length, '生成链路必须把默认规范种进库').toBeGreaterThanOrEqual(14)
  mark(`REST 清单 ${server.length} 条`)

  await enterWorkbench(page)
  await pickProject(page, RUN_CODE)
  await openGuidelines(page)

  const items = page.locator('[data-guideline-code]')
  await expect(items).toHaveCount(server.length)
  const domCodes = (await items.evaluateAll((els) => els.map((e) => e.getAttribute('data-guideline-code') ?? ''))).sort()
  expect(domCodes).toEqual(server.map((g) => g.code).sort())

  // 入口的可用性 = 能力面的事实：meta.capabilities 有 guidelines，按钮就不该是禁用态
  const meta = await apiData<{ capabilities: string[] }>(page, '/api/design-system/meta')
  expect(meta.capabilities).toContain('guidelines')
  await expect(nav(page, 'UX 规范')).toBeEnabled()

  // chip 上的数字必须等于令牌页的值（后端 tokenValues 与 tokens/effective 同源，界面不再自己查一遍）
  const theme = server.find((g) => g.valueTheme)?.valueTheme ?? 'light'
  const effective = await apiData<{ items: { path: string; value: string }[] }>(
    page,
    `/api/design-system/projects/${pid}/tokens/effective?theme=${theme}`,
  )
  const byPath = new Map(effective.items.map((t) => [t.path, t.value]))
  const withRefs = server.filter((g) => g.tokenRefs.length > 0)
  expect(withRefs.length, '没有一条规范引用令牌 = 这条判据是空转').toBeGreaterThan(0)

  await page.locator(`[data-guideline-code="${withRefs[0]!.code}"]`).click()
  const chips = page.locator('[data-token-chip]')
  const chipCount = await chips.count()
  expect(chipCount).toBe(withRefs[0]!.tokenRefs.length)
  let checked = 0
  for (let i = 0; i < chipCount; i++) {
    const text = norm(await chips.nth(i).innerText())
    const [path, ...rest] = text.split(' = ')
    const value = rest.join(' = ')
    const serverValue = withRefs[0]!.tokenValues[path!]
    expect(serverValue, `chip ${path} 后端没给值，界面却在显示"${value}"`).toBeTruthy()
    expect(value, `chip ${path} 的值与后端 tokenValues 不一致`).toBe(norm(serverValue!))
    if (byPath.has(path!)) expect(norm(byPath.get(path!)!), `chip ${path} 与令牌页不同源`).toBe(value)
    checked++
  }
  expect(checked, '一个 chip 都没比对 = 判据空转').toBeGreaterThan(0)
  mark(`chip 比对 ${checked} 个（主题 ${theme}）`)

  // 正文里不许出现"这条规范说 24px"这种第二份真相：库里存的原文只该有路径
  const body = norm((await page.locator('[aria-label="规范正文"]').inputValue()) ?? '')
  const raw = withRefs[0]!.bodyRaw
  expect(body, '输入框必须回填库里原文（不是括注后的展示文本）').toBe(norm(raw))
  expect(/\d+(px|rem|ms)\b/.test(raw), `规范原文里出现了硬编码数值：${raw}`).toBe(false)

  await shot(page, 'guidelines-g1-list')
})

test('G2 界面改一条规范 → 后端回读一致；归档与恢复两条路径都走通', async ({ page }) => {
  const evidence = attachCollectors(page)
  const extra: string[] = []
  sink = { evidence, extra }
  const mark = (s: string) => extra.push(s)

  await injectRealApiKey(page)
  await page.goto(PLUGIN_ROUTE)
  await enterWorkbench(page)
  await pickProject(page, RUN_CODE)
  await openGuidelines(page)

  const code = 'forms'
  await page.locator(`[data-guideline-code="${code}"]`).click()
  const newTitle = `E2E 改过的表单规范 ${Date.now().toString(36)}`
  const newBody = '表单错误文案必须写「原因 + 怎么改」，聚焦态用 `component.input.border-focus`。'
  await page.locator('[aria-label="规范标题"]').fill(newTitle)
  await page.locator('[aria-label="规范正文"]').fill(newBody)
  await expect(page.getByText('有未保存的改动')).toBeVisible()

  await page.getByRole('button', { name: '保存规范', exact: true }).click()
  await expect(page.getByText(/已保存规范 forms/)).toBeVisible({ timeout: 20_000 })

  const after = (await guidelines(page, pid)).find((g) => g.code === code)!
  expect(after.title, '界面写的标题后端必须读得到').toBe(newTitle)
  expect(after.bodyRaw, '界面写的正文后端必须原样读得到').toBe(newBody)
  expect(after.source, '用户改过的行必须标 manual（否则下次重新生成会把它冲掉）').toBe('manual')
  mark(`回读 title=${after.title} source=${after.source}`)

  // 重新生成不得覆盖手改行（G3 再验一次总数，这里先验它点名保护）
  await page.getByRole('button', { name: '重新生成默认规范', exact: true }).click()
  await page.getByRole('button', { name: '确认重新生成', exact: true }).click()
  await expect(page.getByText(/重新生成完成/)).toBeVisible({ timeout: 25_000 })
  await expect(page.getByText(/手改受保护 1|未被覆盖/)).toBeVisible()
  expect((await guidelines(page, pid)).find((g) => g.code === code)!.title).toBe(newTitle)

  // 归档：页内二次确认，取消不改状态
  await page.locator(`[data-guideline-code="${code}"]`).click()
  await page.getByRole('button', { name: '归档', exact: true }).click()
  await expect(page.getByRole('group', { name: '归档或恢复确认' })).toBeVisible()
  await page.getByRole('button', { name: '取消', exact: true }).click()
  expect((await guidelines(page, pid, 'all')).find((g) => g.code === code)!.status).toBe('adopted')

  await page.getByRole('button', { name: '归档', exact: true }).click()
  await page.locator('[data-confirm="archive"]').click()
  await expect(page.getByText(/已归档 forms/)).toBeVisible({ timeout: 20_000 })
  const defaultList = await guidelines(page, pid)
  expect(defaultList.some((g) => g.code === code), '归档后默认清单必须读不到它').toBe(false)
  expect((await guidelines(page, pid, 'all')).some((g) => g.code === code), '归档是软删：库里必须还在').toBe(true)
  expect(await projectField(page, pid, 'status')).not.toBe('archived')
  mark(`归档后：默认清单 ${defaultList.length} 条、all 口径含 forms`)

  // 恢复：勾「显示已归档」→ 选中该行 → 恢复 → 确认
  await page.locator('[data-show-archived]').check()
  await expect(page.locator(`[data-guideline-code="${code}"]`)).toBeVisible({ timeout: 20_000 })
  await page.locator(`[data-guideline-code="${code}"]`).click()
  await page.getByRole('button', { name: '恢复', exact: true }).click()
  await page.locator('[data-confirm="restore"]').click()
  await expect(page.getByText(/已恢复 forms/)).toBeVisible({ timeout: 20_000 })
  expect((await guidelines(page, pid)).find((g) => g.code === code)!.status).toBe('adopted')

  await shot(page, 'guidelines-g2-saved')
})

test('G3 重新生成只补空：跑两次条数不翻倍；规则行与来源徽标都在', async ({ page }) => {
  const evidence = attachCollectors(page)
  const extra: string[] = []
  sink = { evidence, extra }
  const mark = (s: string) => extra.push(s)

  await injectRealApiKey(page)
  await page.goto(PLUGIN_ROUTE)
  await enterWorkbench(page)
  await pickProject(page, RUN_CODE)
  await openGuidelines(page)

  const before = await guidelines(page, pid)
  await page.getByRole('button', { name: '重新生成默认规范', exact: true }).click()
  await page.getByRole('button', { name: '确认重新生成', exact: true }).click()
  await expect(page.getByText(/重新生成完成/)).toBeVisible({ timeout: 25_000 })
  const after = await guidelines(page, pid)
  expect(after.length, '重复生成把条数翻倍 = 界面会出现两套同 code 规范').toBe(before.length)
  mark(`条数 ${before.length} → ${after.length}`)

  // 规则行与来源徽标：证明"每条规范都有可编辑的规则 + 手改/生成来源可辨"
  const rows = page.locator('[data-rule-level]')
  await expect(rows.first()).toBeVisible()
  expect(await rows.count()).toBeGreaterThan(0)
  await expect(page.locator('[data-source]').first()).toBeVisible()
  const sources = await page.locator('[data-guideline-code] [data-source]').evaluateAll((els) => els.map((e) => e.getAttribute('data-source')))
  expect(sources.length, '每条清单项都要带来源徽标（否则手改/生成无法分辨）').toBe(after.length)
  expect(sources.filter((s) => s === 'generated').length).toBeGreaterThanOrEqual(1)

  // 新建规范：草稿不落库，保存后才进库（防"看起来建好了其实没有"）
  const code = `e2e-extra-${Date.now().toString(36)}`
  await page.getByRole('button', { name: '新建规范', exact: true }).click()
  await page.locator('[aria-label="规范标识"]').fill(code)
  await page.locator('[data-confirm-create]').click()
  // 草稿必须真的打开编辑器：只弹一个标识输入框、右边仍是空面板 = 「新建规范」是个摆设
  await expect(page.locator('[aria-label="规范标题"]')).toBeVisible({ timeout: 10_000 })
  expect((await guidelines(page, pid, 'all')).some((g) => g.code === code), '只点新建就落库 = 草稿语义没接上').toBe(false)
  await page.locator('[aria-label="规范标题"]').fill('E2E 附加规范')
  await page.locator('[aria-label="规则文本"]').first().fill('附加规范必须由 E2E 建的规则')
  await page.getByRole('button', { name: '保存规范', exact: true }).click()
  await expect(page.getByText(/已新增规范/)).toBeVisible({ timeout: 20_000 })
  const created = (await guidelines(page, pid)).find((g) => g.code === code)
  expect(created, '保存后 REST 必须读得到').toBeTruthy()
  expect(created!.rules.map((r) => r.textRaw)).toContain('附加规范必须由 E2E 建的规则')
  await expect(page.locator(`[data-guideline-code="${code}"] [data-source]`)).toHaveAttribute('data-source', 'manual')
  mark(`新建 ${code}：规则 ${created!.rules.length} 条、来源徽标 manual`)

  await shot(page, 'guidelines-g3-created')
})

/**
 * G4 = 03-plan §G8「界面 DOM 契约」**逐项**走查（也是插件维护闭环第④步：隔离实例按用户视角点一遍 + 截图读图）。
 *
 * 为什么要单独立一条：G1–G3 各自断言了契约里**它那条用例用得上的**几项，
 * 但"§G8 列出的每一项在真实宿主里都定位得到、而且真是可交互控件"这句话此前只有我读代码（grep）说过，没跑过。
 * 本条把契约逐项落成断言：列表条数 / 来源徽标取值 / 三个详情控件可编辑 / 正文必须是 textarea（规范正文走
 * `v-html` 就是越界）/ 分类与级别下拉的取值来自 `meta` 词表 / 规则行三件套 / 「添加规则」真加一行 /
 * chip 文本形状 `path = 值` / 三个动作按钮在位。
 *
 * **全程零写入**：从进入本 section 起，任何非 GET 的设计系统请求都会让它红——
 * 走查不该变成"第二次改数据"（G3 已经改过一轮了）。
 */
test('G4 §G8 DOM 契约逐项走查：控件定位得到、可交互，且全程零写入', async ({ page }) => {
  const evidence = attachCollectors(page)
  const extra: string[] = []
  sink = { evidence, extra }
  const mark = (s: string) => extra.push(s)

  await injectRealApiKey(page)
  await page.goto(PLUGIN_ROUTE)
  await enterWorkbench(page)
  await pickProject(page, RUN_CODE)
  await openGuidelines(page)

  const writes: string[] = []
  const reads: string[] = []
  page.on('request', (r) => {
    if (!r.url().includes('/api/design-system/')) return
    if (r.method() === 'GET') reads.push(r.url())
    else writes.push(`${r.method()} ${new URL(r.url()).pathname}`)
  })

  const meta = await apiData<{ guidelineCategories?: string[]; guidelineLevels?: string[]; sources?: string[] }>(
    page,
    '/api/design-system/meta',
  )
  const cats = meta.guidelineCategories ?? []
  const levels = meta.guidelineLevels ?? []
  expect(cats.length, 'meta 没回分类词表 = 下面的比对是空转').toBeGreaterThan(0)
  expect(levels.length, 'meta 没回级别词表 = 下面的比对是空转').toBeGreaterThan(0)

  // ① 列表：条数 == REST；每行一个来源徽标，取值只能是后端词表里的 generated|manual
  const server = await guidelines(page, pid)
  const items = page.locator('[data-guideline-code]')
  await expect(items).toHaveCount(server.length)
  const badges = page.locator('[data-guideline-code] [data-source]')
  await expect(badges).toHaveCount(server.length)
  const sources = await badges.evaluateAll((els) => els.map((e) => e.getAttribute('data-source') ?? ''))
  const knownSources = ['generated', 'manual']
  expect(sources.filter((s) => !knownSources.includes(s)), `来源徽标出现词表外的取值：${sources.join(',')}`).toEqual([])
  mark(`列表 ${server.length} 条；来源 manual ${sources.filter((s) => s === 'manual').length} / generated ${sources.filter((s) => s === 'generated').length}`)

  // ② 详情三控件全部可定位且可编辑；正文必须是 textarea（不是 v-html 注入的展示块）
  await items.first().click()
  for (const label of ['规范标题', '规范摘要', '规范正文']) {
    const ctl = page.locator(`[aria-label="${label}"]`)
    await expect(ctl, `§G8 要求的详情控件「${label}」定位不到`).toBeVisible({ timeout: 10_000 })
    await expect(ctl).toBeEditable()
  }
  expect(await page.locator('[aria-label="规范正文"]').evaluate((el) => el.tagName), '规范正文不是表单控件 = 疑似 v-html 渲染').toBe('TEXTAREA')

  // ③ 分类下拉的取值 == meta.guidelineCategories（界面没自己存一份分类）
  const catOptions = (await page.locator('[aria-label="规范分类"] option').evaluateAll((els) => els.map((e) => e.textContent ?? ''))).map(norm).sort()
  expect(catOptions, '分类下拉与后端词表不一致（第二份真相）').toEqual([...cats].sort())

  // ④ 规则行三件套（级别下拉 + 文本框 + 删除按钮），条数 == REST 的 rules；级别取值 == meta.guidelineLevels
  const firstCode = (await items.first().getAttribute('data-guideline-code')) ?? ''
  const first = server.find((g) => g.code === firstCode)
  expect(first, `REST 里读不到清单第一行 ${firstCode}`).toBeTruthy()
  const ruleRows = page.locator('[data-rule-level]')
  await expect(ruleRows, '规则行数必须等于 REST 给的 rules 数').toHaveCount(first!.rules.length)
  await expect(ruleRows.first().locator('[aria-label="规则级别"]')).toBeVisible()
  await expect(ruleRows.first().locator('[aria-label="规则文本"]')).toBeVisible()
  await expect(ruleRows.first().locator('[aria-label="删除这条规则"]')).toBeVisible()
  const rowLevels = await ruleRows.evaluateAll((els) => els.map((e) => e.getAttribute('data-rule-level') ?? ''))
  expect(rowLevels.filter((l) => !levels.includes(l)), `规则行级别属性出现词表外取值：${rowLevels.join(',')}`).toEqual([])
  // 级别下拉的候选必须**含**后端词表（还允许一条"当前值不在词表时补显示"的兜底 option，见 Guidelines.vue 的 v-if）
  const levelValues = await ruleRows.first().locator('[aria-label="规则级别"] option').evaluateAll((els) => els.map((e) => (e as HTMLOptionElement).value))
  const rowLevel = first!.rules[0]!.level
  expect(levels.filter((l) => !levelValues.includes(l)), `级别下拉缺后端词表项：现有 ${levelValues.join(',')}`).toEqual([])
  expect(levelValues.filter((v) => !levels.includes(v) && v !== rowLevel), '级别下拉出现了词表外的候选（界面自己加了一份级别）').toEqual([])

  // ⑤ 「添加规则」不是摆设：点一次真的多一行（纯前端草稿态，不保存就不该有写请求）
  await page.getByRole('button', { name: '添加规则', exact: true }).click()
  await expect(ruleRows).toHaveCount(first!.rules.length + 1)
  mark(`添加规则：${first!.rules.length} → ${first!.rules.length + 1}（未保存）`)

  // ⑥ 令牌 chip：文本形状必须是 `path = 值`，值取自后端 tokenValues（界面没自己查一遍）
  const withRefs = server.filter((g) => g.tokenRefs.length > 0)
  expect(withRefs.length, '整个 section 没有一条规范引用令牌 = 这条判据空转').toBeGreaterThan(0)
  await page.locator(`[data-guideline-code="${withRefs[0]!.code}"]`).click()
  const chips = page.locator('[data-token-chip]')
  await expect(chips).toHaveCount(withRefs[0]!.tokenRefs.length)
  const chipTexts = (await chips.evaluateAll((els) => els.map((e) => e.textContent ?? ''))).map(norm)
  for (const t of chipTexts) {
    const m = /^([\w.-]+) = (.+)$/.exec(t)
    expect(m, `chip 文本不合契约形状（应为 "path = 值"）：${t}`).toBeTruthy()
    expect(norm(withRefs[0]!.tokenValues[m![1]!] ?? ''), `chip ${m![1]} 显示的值后端没给`).toBe(m![2]!)
  }
  mark(`chip ${chipTexts.length} 个，首个「${chipTexts[0]}」`)

  // ⑦ 三个动作按钮在位（归档/恢复按选中行状态出现，这里选中的是未归档行）
  await expect(page.getByRole('button', { name: '保存规范', exact: true })).toBeVisible()
  await expect(page.getByRole('button', { name: '重新生成默认规范', exact: true })).toBeVisible()
  await expect(page.getByRole('button', { name: '归档', exact: true })).toBeVisible()

  // 先证明这条判据**不是空转**：同一个匹配器确实看到了 GET（否则"零写请求"只是没在听）
  expect(reads.length, '一个设计系统 GET 都没抓到 = 收集器没在工作，下面的"零写入"就无从谈起').toBeGreaterThan(0)
  expect(writes, `走查不许改数据，但发出了写请求：${writes.join(' ; ')}`).toEqual([])
  mark(`收集器看到 ${reads.length} 条 GET、${writes.length} 条写请求`)
  await shot(page, 'guidelines-g4-walkthrough')
})

/**
 * G5 = 02-spec「边界条件·存量项目无规范：界面空态引导『重新生成默认规范』」这一格的证据。
 * 此前只有实现（`Guidelines.vue` 的 `state==='ready' && !rows.length` 分支）没有人测过：
 * 空态是不是真的按项目出现、文案有没有给下一步动作、以及**生成之后它必须消失**（否则就是一块常驻装饰）。
 */
test('G5 零规范项目：空态说清下一步，生成之后空态必须消失', async ({ page }) => {
  const evidence = attachCollectors(page)
  const extra: string[] = []
  sink = { evidence, extra }
  const mark = (s: string) => extra.push(s)
  const bareCode = `${RUN_CODE}-bare`

  await injectRealApiKey(page)
  await page.goto(PLUGIN_ROUTE)

  // 只建项目、不走生成链路：POST projects 是 DesignProjectService.Create，不碰 SeedGuidelines ⇒ 规范必须为零
  await apiPost<unknown>(page, '/api/design-system/projects', { code: bareCode, name: 'E2E 空态项目' })
  const bare = (await apiData<ProjectRow[]>(page, '/api/design-system/projects')).find((p) => p.code === bareCode)
  expect(bare, '空态项目没落库 = 下面测的不是零规范项目').toBeTruthy()
  const beforeList = await guidelines(page, bare!.id)
  expect(beforeList.length, `新建项目本该零规范，REST 却回了 ${beforeList.length} 条（前提不成立，这条判据会空转）`).toBe(0)

  await enterWorkbench(page)
  await pickProject(page, bareCode)
  await nav(page, 'UX 规范').click()
  await expect(page.locator('[data-guidelines]')).toBeVisible({ timeout: 20_000 })

  const empty = page.locator('.ps--empty')
  await expect(empty, '零规范项目没出空态（要么被错误态顶替，要么渲染条件写错）').toBeVisible({ timeout: 20_000 })
  const title = norm((await empty.locator('.ps__title').textContent()) ?? '')
  expect(title, `空态标题不是那句：${title}`).toBe('这个项目还没有 UX 规范')
  const hint = norm((await empty.locator('.ps__hint').textContent()) ?? '')
  expect(hint, `空态没给下一步动作，用户对着空白面板：${hint}`).toContain('重新生成默认规范')
  mark(`空态命中：REST ${beforeList.length} 条、标题「${title}」`)

  // 反向自证（条件渲染不是常驻装饰）：点一次生成之后，空态必须消失、行必须出现且与 REST 同数
  await page.locator('[data-regenerate]').click()
  await expect(page.locator('[aria-label="重新生成确认"]')).toBeVisible()
  await page.locator('[data-confirm-generate]').click()
  await expect(page.locator('[data-guideline-code]').first()).toBeVisible({ timeout: 30_000 })
  await expect(empty).toHaveCount(0)
  const afterList = await guidelines(page, bare!.id)
  expect(afterList.length, '生成后 REST 仍是空 = 界面那列是前端自己凑的').toBeGreaterThan(0)
  await expect(page.locator('[data-guideline-code]')).toHaveCount(afterList.length)
  const domCount = await page.locator('[data-guideline-code]').count()
  mark(`生成后：界面 ${domCount} 条、REST ${afterList.length} 条、空态面板 ${await empty.count()} 个`)
  await shot(page, 'guidelines-g5-empty-state')
})

/**
 * G6 = 02-spec「边界条件·窄屏：第 15 个 section 的列表/详情纵向堆叠」。
 * 判据取 computed 几何而不是截图肉眼：`.gl__cols` 在 ≤900px 必须只剩一根网格轨道，
 * 且详情块落在列表**下方**（不是被横向挤出去），并且这一区不出现横向溢出。
 */
test('G6 窄屏 820px：列表与详情纵向堆叠，规范区不横向溢出', async ({ page }) => {
  const evidence = attachCollectors(page)
  const extra: string[] = []
  sink = { evidence, extra }
  const mark = (s: string) => extra.push(s)

  await page.setViewportSize({ width: 820, height: 1100 })
  await injectRealApiKey(page)
  await page.goto(PLUGIN_ROUTE)
  await enterWorkbench(page)
  await pickProject(page, RUN_CODE)
  await openGuidelines(page)

  // 详情列是 `v-if="draft && (selected || isNew)"`：不先选一行就没有第二列，堆叠判据会退化成单列自比
  await page.locator('[data-guideline-code]').first().click()
  const cols = page.locator('.gl__cols > .gl__col')
  await expect(cols, '窄屏下详情列没出现（选了一行还是单列）').toHaveCount(2)

  const tracks = await page.locator('.gl__cols').evaluate((el) => getComputedStyle(el).gridTemplateColumns.split(' ').filter(Boolean).length)
  expect(tracks, `窄屏 .gl__cols 仍是 ${tracks} 根轨道（媒体查询没生效）`).toBe(1)

  const boxes = await cols.evaluateAll((els) => els.map((e) => {
    const r = e.getBoundingClientRect()
    return { y: r.y, h: r.height, w: r.width }
  }))
  expect(boxes[1]!.y, `详情块没有排在列表下方（列表底 ${boxes[0]!.y + boxes[0]!.h}，详情顶 ${boxes[1]!.y}）`)
    .toBeGreaterThanOrEqual(boxes[0]!.y + boxes[0]!.h - 1)
  expect(Math.min(boxes[0]!.w, boxes[1]!.w), `两列宽度异常：${boxes.map((b) => Math.round(b.w)).join(' / ')}px`).toBeGreaterThan(300)

  const overflow = await page.locator('[data-guidelines]').evaluate((el) => el.scrollWidth - el.clientWidth)
  expect(overflow, `窄屏这一区横向溢出 ${overflow}px`).toBeLessThanOrEqual(0)
  // 整页溢出只记读数不断言：宿主外壳的侧栏有自己的响应式规则，不归本插件管
  const docOverflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)
  mark(`820px：轨道 ${tracks} 根，列表 y=${Math.round(boxes[0]!.y)} h=${Math.round(boxes[0]!.h)}，详情 y=${Math.round(boxes[1]!.y)} h=${Math.round(boxes[1]!.h)}；本区溢出 ${overflow}px，整页 ${docOverflow}px`)
  await shot(page, 'guidelines-g6-narrow')
})

/**
 * G7 出货文本向外部 MCP 客户端承诺的那条链：`universal_tool` → `list_tools` 枚举 design_* → 按名调到 `design_guide`。
 *
 * 为什么单独一条：design_* 的后端单测**全部**是 `new DesignGuideTool(kit)` 直构对象，一次都没经过宿主
 * `IToolRegistry`；McpCenter 侧的 `UniversalToolForwarderTests` 用的是 mock 注册表，证明的是「按名分发」这件
 * 通用行为。于是「插件把工具交给注册表」与「网关真能调到 design_guide」这两跳此前**只有日志**
 * （e2e 宿主日志「设计系统插件已注册 8 个工具函数」+「MCP 网关已启动 …」），没有任何断言。
 * 而这句话是印在产物里的：`DesignTools.cs` 的 `discovery` 字段直接让外部客户端去走它 —— 承诺就要实测。
 *
 * 三条判据各自的分工：
 *  ① 注册表实际有的 design-system 工具 == REST `meta.agentTools`（声明与实际必须相等，少一件就是漏注册）；
 *  ② 经网关调到的 design_guide 内容 == 插件自己的版本/工具/工作流（M3 FR13 的 guideline 工作流必须真出得来）；
 *  ③ 两条反向腿：不存在的工具名必须 `isError`；把 `design_guide` 当对外工具名直接 tools/call 必须被拒
 *     —— 证明 ① ② 不是"网关对任何输入都回一大坨"造成的假绿。
 *
 * 端口不硬编码：`GET /api/mcp-center/config` 的 `listenUrl` 就是**被测实例自己**的真源
 * （e2e 多实例靠 `FORGESELF_MCP_GATEWAY_PORT` 错开端口，见 McpGatewayConfig 注释）。全程只读，不建数据。
 */
test('G7 外部 MCP 客户端这条链走得通：universal_tool 枚举到全部 design_*，并真调到 design_guide', async ({ page, request }) => {
  const evidence = attachCollectors(page)
  const extra: string[] = []
  sink = { evidence, extra }
  const mark = (s: string) => extra.push(s)

  await injectRealApiKey(page)
  await page.goto(PLUGIN_ROUTE)
  await expect(page.locator('.ds-root')).toBeVisible({ timeout: 30_000 })

  const meta = await apiData<{ modelVersion: string; agentTools: string[] }>(page, '/api/design-system/meta')
  expect(meta.agentTools.length, 'meta.agentTools 为空 = 本条判据在空转').toBeGreaterThan(0)

  const gw = await apiData<{ listenUrl: string; port: number; hasToken: boolean; isRunning: boolean }>(
    page,
    '/api/mcp-center/config',
  )
  expect(gw.isRunning, `MCP 网关没在跑（config 报 isRunning=false），本条判据无从执行`).toBe(true)
  if (gw.hasToken) {
    throw new Error(`网关 ${gw.listenUrl} 配了令牌而令牌不回显明文 —— 用例无法鉴权；请清掉该实例 mcp-center/config.json 的 token（或给它注入 FORGESELF_MCP_GATEWAY_TOKEN=""）`)
  }
  mark(`网关 ${gw.listenUrl}（isRunning=${gw.isRunning}，hasToken=${gw.hasToken}）`)

  const rpc = async (id: number, method: string, params?: Record<string, unknown>): Promise<JsonRpcReply> => {
    const res = await request.post(`${gw.listenUrl}/mcp`, {
      data: { jsonrpc: '2.0', id, method, ...(params ? { params } : {}) },
      headers: { 'Content-Type': 'application/json' },
    })
    expect(res.status(), `JSON-RPC ${method} 回 ${res.status()}（网关 ${gw.listenUrl}）`).toBe(200)
    return (await res.json()) as JsonRpcReply
  }
  /** 唯一对外工具 → 内部按名转发；返回转发层的原文与 isError */
  const call = async (tool: string, parameters: Record<string, unknown>): Promise<{ text: string; isError: boolean }> => {
    const resp = await rpc(2, 'tools/call', { name: 'universal_tool', arguments: { tool, parameters } })
    expect(resp.error, `tools/call ${tool} 被 JSON-RPC 层拒：${resp.error?.message ?? ''}`).toBeUndefined()
    const r = resp.result as { content: { type: string; text: string }[]; isError: boolean }
    expect(r.content.length, `${tool} 没有 text 内容块`).toBeGreaterThan(0)
    return { text: r.content[0]!.text, isError: r.isError }
  }
  /** design_* 用 {success,data} 封套，宿主自带工具（list_tools）直接回裸对象 —— 两种都是真实形态，不是含糊放过 */
  const unwrap = <T>(text: string): T => {
    const body = JSON.parse(text) as { success?: boolean; error?: string; data?: T }
    expect(body.success, `工具回失败：${(body.error ?? text).slice(0, 200)}`).not.toBe(false)
    return (body.data ?? body) as T
  }

  // 对外面只有一件工具（这是 discovery 那句话的前提：外部客户端只能走 universal_tool）
  const listed = await rpc(1, 'tools/list')
  const exposed = (listed.result as { tools: { name: string }[] }).tools.map((t) => t.name)
  expect(exposed, `网关对外工具面不是「只有 universal_tool」：${exposed.join(', ')}`).toEqual(['universal_tool'])

  // ① 注册表实际注册的 design-system 工具 == 插件自述的 meta.agentTools
  const enumText = await call('list_tools', { keyword: 'design' })
  expect(enumText.isError, `list_tools 走网关失败：${enumText.text.slice(0, 200)}`).toBe(false)
  const rows = unwrap<{ total: number; tools: { name: string; pluginId?: string }[] }>(enumText.text).tools
  const registryDs = rows.filter((t) => t.pluginId === 'design-system').map((t) => t.name).sort()
  expect(registryDs.length, `按名过滤到 0 件 design-system 工具（关键字判据空转）`).toBeGreaterThan(0)
  expect(registryDs, `注册表里的 design_*（${registryDs.length} 件）与 meta.agentTools（${meta.agentTools.length} 件）不一致`).toEqual(
    [...meta.agentTools].sort(),
  )
  mark(`list_tools 命中 ${rows.length} 件，其中 design-system ${registryDs.length} 件：${registryDs.join(', ')}`)

  // ② 经网关真调到 design_guide，内容就是当前这份插件自述
  const guide = await call('design_guide', {})
  expect(guide.isError, `design_guide 走网关失败：${guide.text.slice(0, 200)}`).toBe(false)
  const g = unwrap<GuidePayload>(guide.text)
  expect(g.version, `网关调到的 design_guide 版本 ${g.version} != REST meta.modelVersion ${meta.modelVersion}`).toBe(meta.modelVersion)
  expect(g.tools.map((t) => t.name).sort(), 'design_guide 自己列出的工具与 meta.agentTools 不一致').toEqual([...meta.agentTools].sort())
  const guidelineWf = g.workflows.find((w) => w.id === 'guideline')
  expect(guidelineWf, `design_guide 的 workflows 里没有 M3 的 guideline 工作流：${g.workflows.map((w) => w.id).join(', ')}`).toBeTruthy()
  expect(guidelineWf!.steps.join(' '), 'guideline 工作流没把改交互要求的那步指到 design_edit action=guideline').toContain(
    'design_edit action=guideline',
  )
  expect(g.discovery, `discovery 不再指向 universal_tool，但 G7 就是照它走的：${g.discovery}`).toContain('universal_tool')
  expect(typeof g.access.allowWrite, 'access.allowWrite 不是布尔（写开关面在网关路径上失真）').toBe('boolean')
  mark(`design_guide 经网关：version=${g.version}，tools=${g.tools.length}，workflows=${g.workflows.map((w) => w.id).join('/')}，allowWrite=${g.access.allowWrite}`)

  // ③ 两条反向腿
  const nope = await call('design_nope_e2e', {})
  expect(nope.isError, `不存在的工具名没有报错，说明上面的断言可能恒真：${nope.text.slice(0, 160)}`).toBe(true)
  expect(nope.text, '未知工具的错误语义不是 unknown tool 那条').toContain('unknown tool')
  const bypass = await rpc(3, 'tools/call', { name: 'design_guide', arguments: {} })
  expect(bypass.error?.message ?? '', `网关允许把 design_guide 当对外工具名直接调（绕过 universal_tool）：${JSON.stringify(bypass)}`).toContain(
    'universal_tool',
  )
})
