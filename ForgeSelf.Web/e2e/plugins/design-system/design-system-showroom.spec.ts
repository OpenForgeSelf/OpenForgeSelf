import { test, expect, type Page } from '@playwright/test'
import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { injectRealApiKey, getRealApiKey } from '../../helpers/real-auth'
import {
  apiData,
  apiPost,
  apiText,
  attachCollectors,
  canvasBg,
  contrastRatio,
  dumpEvidence,
  hexToRgb,
  relLuminance,
  shot,
  shotOf,
  type Evidence,
} from './design-system-helpers'

/**
 * 设计系统插件 M2（showroom-wizard）端到端验证 · 切片 A。
 *
 * 与既有 `design-system.spec.ts`（工作台全链路）并列，本文件只覆盖 M2 新增的「开始 / 展厅」两层：
 *   A1 = AC13：向导四步真落库（REST 回读令牌 >100、审计无 critical）；展厅里"预设衣服"与
 *        "由该预设创建的项目衣服"在 light 档的 `.ds-outfit` 计算底色都等于后端 `semantic.surface-bg`
 *        （逐位）——证明画布与导出**同源**，不是前端自算的一套假皮；微调不写库、保存才 +1。
 *   A2 = AC14：六个 A 片模特页逐页切换，各页 `data-mq-wear` 元素数与种类数达到 03-plan §M 下限；
 *        控制台无致命错误（组件解析 / 模块加载 / 未定义一类）与取色器空值告警。
 *
 * 纪律（03-plan §E）：真实宿主零 mock；唯一项目码 `e2e-m2-<时间戳>`；写开关等全局状态用例结束前还原；
 * **收尾不做任何清理动作**（既不硬删、也不归档）——本目录 e2e 走隔离实例（临时库随实例丢弃），
 * 没有"污染预览实例"的问题；且任何时候都**禁止硬删项目**（将来若确需清理，只允许软归档）。
 * 截图前先断言数据到位。
 */

const MANIFEST = JSON.parse(
  // 清单可能是 UTF-8 BOM 开头：用转义写法剥掉
  readFileSync(
    fileURLToPath(new URL('../../../../Plugins/DesignSystem/plugin.json', import.meta.url)),
    'utf-8',
  ).replace(/^\uFEFF/, ''),
) as { Version: string; frontend: { route: string } }

const PLUGIN_ROUTE = MANIFEST.frontend.route

/** 每次运行唯一项目码：数据隔离、可反复跑，不依赖上次残留 */
const RUN_CODE = `e2e-m2-${Date.now().toString(36)}`

interface ProjectRow {
  id: number
  code: string
  name: string
  status: string
  tokenCount: number
  /** 试穿/微调若落库必然改动它；用于"已有项目一行没动"的并发无关判据（闸门2 M-A） */
  updatedAt: string
}

/** 模式条页签（`role="tab"`，名即判据） */
const modeTab = (page: Page, name: string) => page.getByRole('tab', { name, exact: true })

/* ---------------- 证据：无论通过与否都落一份 ---------------- */

let sink: { evidence: Evidence; extra: string[] } | null = null

// Playwright 要求首个参数必须是解构模式；本钩子不需要任何 fixture
// eslint-disable-next-line no-empty-pattern
test.afterEach(({}, testInfo) => {
  if (!sink) return
  const status = testInfo.status === testInfo.expectedStatus ? 'passed' : 'FAILED'
  // 每用例一份：用例标题以 A1/A2 开头，用它当键，避免两条用例互相覆盖证据
  const key = testInfo.title.trim().slice(0, 2)
  dumpEvidence(`showroom-${key}-${status}`, sink.evidence, sink.extra)
})

/** 列出当前全部项目（真实后端事实） */
async function listProjects(page: Page): Promise<ProjectRow[]> {
  return apiData<ProjectRow[]>(page, '/api/design-system/projects')
}

/** 某主题下某令牌的有效值（后端已解析别名） */
async function effectiveValue(page: Page, pid: number, theme: string, tokenPath: string): Promise<string> {
  const view = await apiData<{ items: { path: string; value: string }[] }>(
    page,
    `/api/design-system/projects/${pid}/tokens/effective?theme=${theme}`,
  )
  return view.items.find((t) => t.path === tokenPath)?.value ?? ''
}

// WCAG 亮度/对比度与画布底色三个函数已上移到 `design-system-helpers.ts`（M3 V 片要用同一套公式，不留第二份）

/** AC14 判据：六个 A 片页面及其元素/种类下限（03-plan §M） */
const ADMIN_PAGES = [
  { id: 'admin-dashboard', label: '仪表盘', minWear: 14, minKinds: 5 },
  { id: 'admin-list', label: '列表', minWear: 18, minKinds: 6 },
  { id: 'admin-form', label: '表单', minWear: 12, minKinds: 5 },
  { id: 'admin-detail', label: '详情', minWear: 14, minKinds: 6 },
  { id: 'admin-settings', label: '设置', minWear: 12, minKinds: 6 },
] as const

const BOARD_PAGES = [{ id: 'status-board', label: '概览', minWear: 14, minKinds: 4 }] as const

/** AC14（B 片）：切片 B 新增的三个场景页及其元素/种类下限（03-plan §M） */
const B_PAGES = [
  { scene: '工具/工作台', sceneId: 'workbench', id: 'workbench-editor', label: '编辑器', minWear: 14, minKinds: 6 },
  { scene: '官网/落地页', sceneId: 'landing', id: 'landing-home', label: '首页', minWear: 16, minKinds: 5 },
  { scene: '移动端 H5', sceneId: 'mobile', id: 'mobile-home', label: '首页', minWear: 12, minKinds: 5 },
] as const

test.describe('M2 展厅 · A 片（向导 / 展厅 / 模特）', () => {
  test('A1 AC13：向导四步落库 + 画布与后端同源（预设/项目各一）+ 微调不写库、保存才落库', async ({ page }) => {
    const evidence = attachCollectors(page)
    const extra: string[] = []
    sink = { evidence, extra }
    const mark = (s: string) => extra.push(s)

    await injectRealApiKey(page)
    await page.goto(PLUGIN_ROUTE)

    // ---- 1. 模式条切「开始」，走完四步向导（真实宿主可能已有项目，默认落点不一定是开始）----
    await modeTab(page, '开始').click()
    await expect(page.locator('[data-start]')).toBeVisible({ timeout: 30_000 })

    await page.locator('[data-scene="admin"]').click()
    await page.getByRole('button', { name: '下一步', exact: true }).click()

    // ② 风格：等推荐回来，选第一款并记下它的 id（后面用它验证同源）
    await expect(page.locator('[data-preset]').first()).toBeVisible({ timeout: 30_000 })
    const presetId = await page.locator('[data-preset]').first().getAttribute('data-preset')
    expect(presetId, '预设卡必须带 data-preset 判据').toBeTruthy()
    await page.locator('[data-preset]').first().click()
    await page.getByRole('button', { name: '就选它', exact: true }).click()

    // ③ 微调：跳过
    await page.getByRole('button', { name: '下一步', exact: true }).click()

    // ④ 命名与创建（项目码走「高级设置」，便于后面按 code 定位）
    await page.getByLabel('设计系统名称').fill(`E2E 展厅 ${RUN_CODE}`)
    await page.getByRole('button', { name: '高级设置', exact: true }).click()
    await page.getByLabel('项目代码').fill(RUN_CODE)
    await page.getByRole('button', { name: '创建我的设计系统', exact: true }).click()

    await expect(page.locator('[data-wizard-done]')).toBeVisible({ timeout: 60_000 })

    // 后端回读：项目真存在、令牌 >100、审计无 critical（不是只看 DOM 说成功）
    const created = (await listProjects(page)).find((p) => p.code === RUN_CODE)
    expect(created, `向导应创建出项目 ${RUN_CODE}`).toBeTruthy()
    const pid = created!.id
    expect(created!.tokenCount, '向导创建必须真生成令牌（>100）').toBeGreaterThan(100)
    const audit = await apiData<{ summary: { critical: number } }>(
      page,
      `/api/design-system/projects/${pid}/audit`,
    )
    expect(audit.summary.critical, '新项目审计不应有 critical 项').toBe(0)
    mark(`向导创建 code=${RUN_CODE} pid=${pid} tokenCount=${created!.tokenCount} auditCritical=${audit.summary.critical}`)
    await shot(page, 'a1-wizard-done')

    // ---- 2. 去展厅：项目衣服底色必须 == 后端 light 主题 semantic.surface-bg（逐位）----
    await page.getByRole('button', { name: '去展厅看看', exact: true }).click()
    await expect(page.locator('[data-showroom]')).toBeVisible({ timeout: 30_000 })

    const projectOpt = page.locator(`[data-outfit-id="project:${RUN_CODE}"]`)
    await expect(projectOpt).toBeVisible({ timeout: 30_000 })
    await projectOpt.click()

    // 明暗统一到「浅色」：项目与预设同档才可逐位比对
    await page
      .locator('[data-stage] [role="radiogroup"][aria-label="明暗"]')
      .getByRole('radio', { name: '浅色', exact: true })
      .click()
    await expect(page.locator('[data-stage]')).toHaveAttribute('data-theme', 'light')

    const lightBg = await effectiveValue(page, pid, 'light', 'semantic.surface-bg')
    const lightRgb = hexToRgb(lightBg)
    expect(lightRgb, `后端 light semantic.surface-bg「${lightBg}」必须可解析为颜色`).toBeTruthy()
    await expect
      .poll(() => canvasBg(page), { timeout: 30_000, message: '项目画布底色必须等于后端导出值' })
      .toBe(lightRgb)
    mark(`项目 light surface-bg=${lightBg} → 画布=${await canvasBg(page)}`)
    await shot(page, 'a1-stage-project')

    // ---- 3. 预设衣服（同一预设 + light 档）：底色必须与"由它创建的项目"一致 —— 预览与导出同源 ----
    const presetOpt = page.locator(`[data-outfit-id="preset:${presetId}"]`)
    await expect(presetOpt).toBeVisible({ timeout: 15_000 })
    await presetOpt.click()
    await expect(page.locator('[data-stage]')).toHaveAttribute('data-theme', 'light')
    await expect
      .poll(() => canvasBg(page), {
        timeout: 30_000,
        message: '预设画布底色必须与同预设创建的项目一致（预览与导出同源）',
      })
      .toBe(lightRgb)
    mark(`预设 ${presetId} 画布=${await canvasBg(page)}（与项目 ${RUN_CODE} 同值）`)
    await shot(page, 'a1-stage-preset')

    // ---- 4. 微调不写库；「保存为新设计」才落库 ----
    // 反作弊口径（闸门2 M-A 修订，2026-10-03）：**不再用「全库项目总数」，也不用「全库 id 集合」**。
    // 原因：e2e 目录 `fullyParallel: true`、本地 workers 不限、多 spec 共用同一后端实例，
    // 同目录其它用例会**并发新建项目** —— 计数与 id 集合都会被它们改掉（实测默认并行下
    // 计数版假红 `Expected 2 / Received 3`，串行才 19 passed）。
    // 换成两条**与并发无关**的判据：
    //   ① 微调期间**本页不发任何写请求**（POST/PUT/PATCH/DELETE）——页级判据，别人的写请求不算在本页头上；
    //   ② 两次快照里**都在的已有项目**，其 tokenCount/updatedAt 一行没动（不比对"是否新增项目"：
    //      "本页不新建"已由 ① 蕴含——本页要新建项目必须发写请求）。
    const fingerprint = (rows: { id: number; tokenCount: number; updatedAt: string }[]) =>
      new Map(rows.map((p) => [p.id, `${p.tokenCount}|${p.updatedAt}`]))
    const before = fingerprint(await listProjects(page))
    const writes: string[] = []
    const onRequest = (r: { method(): string; url(): string }) => {
      if (/^(POST|PUT|PATCH|DELETE)$/.test(r.method()) && /\/api\/design-system\//.test(r.url()))
        writes.push(`${r.method()} ${r.url()}`)
    }
    page.on('request', onRequest)

    const brand = page.locator('[data-tune] input[aria-label="品牌色"]')
    await expect(brand).toBeEnabled()
    await brand.fill('#ff0000')
    await expect(brand).toHaveValue('#ff0000')
    await expect(page.getByRole('button', { name: '还原', exact: true })).toBeEnabled()
    await page.waitForTimeout(400) // 给潜在写请求一个露头窗口（微调防抖 300ms 之后）
    page.off('request', onRequest)
    expect(writes, '试穿/微调不得发任何写请求').toEqual([])
    const touched = (await listProjects(page))
      .filter((p) => before.has(p.id) && before.get(p.id) !== `${p.tokenCount}|${p.updatedAt}`)
      .map((p) => p.code)
    expect(touched, '试穿/微调不得改动任何已有项目').toEqual([])

    await page.getByRole('button', { name: '保存为新设计', exact: true }).click()
    // 落库口径：`quick-create` 新建一个 code 由后端生成的项目，名字是「<衣服名> 微调」
    // （A1 是整套 e2e 里唯一会这么建的用例）。同样不用全库计数，改为轮询
    // 「出现一个 id 不在 before 里、且名字以 `微调` 结尾的项目」。
    await expect
      .poll(
        async () =>
          (await listProjects(page)).filter((p) => !before.has(p.id) && p.name.endsWith('微调')).length,
        { timeout: 60_000, message: '「保存为新设计」应落库一个新的「…微调」项目' },
      )
      .toBeGreaterThanOrEqual(1)
    mark(`微调期间写请求=${writes.length} 条；已有项目未改动=${touched.length === 0}；保存后新增「…微调」项目`)
    await shot(page, 'a1-tuned-saved')

    // ---- 5. 无致命控制台错误 ----
    const fatalPatterns = [
      /Failed to resolve component/i,
      /does not provide an export named/i,
      /Failed to (fetch|resolve) dynamically imported module/i,
      /Failed to load module script/i,
      /is not defined/i,
    ]
    const fatal = evidence.consoleErrors.filter((l) => fatalPatterns.some((re) => re.test(l)))
    expect(fatal, `控制台出现致命报错：\n${fatal.join('\n')}`).toEqual([])
  })

  test('A2 AC14：六个模特页真渲染（元素/种类下限）且控制台无致命错误', async ({ page }) => {
    const evidence = attachCollectors(page)
    const extra: string[] = []
    sink = { evidence, extra }
    const mark = (s: string) => extra.push(s)

    await injectRealApiKey(page)
    await page.goto(PLUGIN_ROUTE)

    // 进展厅并选中第一件衣服（衣柜里至少有内置预设，不依赖宿主是否已有项目）
    await modeTab(page, '展厅').click()
    await expect(page.locator('[data-showroom]')).toBeVisible({ timeout: 30_000 })
    const firstOpt = page.locator('[role="option"][data-outfit-id]').first()
    await expect(firstOpt).toBeVisible({ timeout: 30_000 })
    await firstOpt.click()
    await expect(page.locator('[data-stage]')).toBeVisible({ timeout: 30_000 })
    // 等画布真上色（后端文本已应用），避免"元素在但没穿衣服"
    await expect.poll(() => canvasBg(page), { timeout: 30_000 }).not.toBe('rgba(0, 0, 0, 0)')

    const sceneTab = page.locator('[data-stage] [role="tablist"][aria-label="场景"]')
    const pageTab = page.locator('[data-stage] [role="tablist"][aria-label="页面"]')

    const assertPages = async (
      pages: readonly { id: string; label: string; minWear: number; minKinds: number }[],
    ): Promise<void> => {
      for (const p of pages) {
        await pageTab.getByRole('tab', { name: p.label, exact: true }).click()
        await expect(page.locator('[data-stage]')).toHaveAttribute('data-page', p.id)
        const root = page.locator(`[data-mq-page="${p.id}"]`)
        await expect(root, `模特页 ${p.id} 必须渲染出来`).toBeVisible({ timeout: 20_000 })

        const wears = root.locator('[data-mq-wear]')
        const count = await wears.count()
        const kinds = new Set(await wears.evaluateAll((els) => els.map((e) => e.getAttribute('data-mq-wear') ?? '')))
        expect(count, `${p.id} 关键元素数应 ≥ ${p.minWear}（实际 ${count}）`).toBeGreaterThanOrEqual(p.minWear)
        expect(kinds.size, `${p.id} 关键元素种类应 ≥ ${p.minKinds}（实际 ${[...kinds].join(',')}）`).toBeGreaterThanOrEqual(
          p.minKinds,
        )
        mark(`${p.id}: wear=${count} kinds=${kinds.size}(${[...kinds].sort().join('|')})`)
        await shot(page, `a2-${p.id}`)
      }
    }

    // 后台/中台：五个页面
    await sceneTab.getByRole('tab', { name: '后台/中台', exact: true }).click()
    await expect(page.locator('[data-stage]')).toHaveAttribute('data-scene', 'admin')
    await assertPages(ADMIN_PAGES)

    // 状态板：一个页面
    await sceneTab.getByRole('tab', { name: '状态板', exact: true }).click()
    await expect(page.locator('[data-stage]')).toHaveAttribute('data-scene', 'board')
    await assertPages(BOARD_PAGES)

    // 控制台：无致命错误、无取色器空值告警
    const fatalPatterns = [
      /Failed to resolve component/i,
      /does not provide an export named/i,
      /Failed to (fetch|resolve) dynamically imported module/i,
      /Failed to load module script/i,
      /is not defined/i,
    ]
    const fatal = evidence.consoleErrors.filter((l) => fatalPatterns.some((re) => re.test(l)))
    const colorNoise = evidence.consoleAll.filter((l) => /does not conform to the required format/i.test(l))
    expect(colorNoise, `控制台出现取色器空值告警 ${colorNoise.length} 条`).toEqual([])
    expect(fatal, `控制台出现致命报错：\n${fatal.join('\n')}`).toEqual([])
  })
})

test.describe('M2 展厅 · B 片（其余场景 / 设备框 / 并排对比）', () => {
  test('B1 AC15：五类场景注册表 + 三档设备框宽度 + 移动端强制手机框', async ({ page }) => {
    const evidence = attachCollectors(page)
    const extra: string[] = []
    sink = { evidence, extra }
    const mark = (s: string) => extra.push(s)

    await injectRealApiKey(page)
    await page.goto(PLUGIN_ROUTE)
    await modeTab(page, '展厅').click()
    await expect(page.locator('[data-showroom]')).toBeVisible({ timeout: 30_000 })

    const firstOpt = page.locator('[role="option"][data-outfit-id]').first()
    await expect(firstOpt).toBeVisible({ timeout: 30_000 })
    await firstOpt.click()
    const stage = page.locator('[data-stage]')
    await expect(stage).toBeVisible({ timeout: 30_000 })
    await expect.poll(() => canvasBg(page), { timeout: 30_000 }).not.toBe('rgba(0, 0, 0, 0)')

    // 场景页签名恰为五类（集合等价，不依赖顺序 —— 注册表顺序为 A 片追加式）
    const sceneTab = stage.locator('[role="tablist"][aria-label="场景"]')
    const sceneNames = (await sceneTab.getByRole('tab').allTextContents()).map((s) => s.trim())
    expect(sceneNames.slice().sort()).toEqual(['后台/中台', '官网/落地页', '工具/工作台', '移动端 H5', '状态板'].sort())
    mark(`场景页签=${sceneNames.join('|')}`)

    // 三档设备框宽度（读设备框内联宽度，唯一真源是 scenes.ts 的 DEVICES）
    const deviceGroup = stage.locator('[role="radiogroup"][aria-label="设备"]')
    const frameWidth = () => stage.locator('[data-stage-frame]').evaluate((el) => (el as HTMLElement).style.width)
    const DEVICE_CASES = [
      { name: '桌面', code: 'desktop', w: 1280 },
      { name: '平板', code: 'tablet', w: 820 },
      { name: '手机', code: 'mobile', w: 390 },
    ] as const
    for (const d of DEVICE_CASES) {
      await deviceGroup.getByRole('radio', { name: d.name, exact: true }).click()
      await expect(stage).toHaveAttribute('data-device', d.code)
      await expect.poll(frameWidth).toBe(`${d.w}px`)
      mark(`设备 ${d.code} → data-device=${d.code} 框宽=${await frameWidth()}`)
    }

    // 移动端场景强制手机框：先切回桌面档，再点「移动端 H5」场景，设备应被场景改写为手机
    await deviceGroup.getByRole('radio', { name: '桌面', exact: true }).click()
    await expect(stage).toHaveAttribute('data-device', 'desktop')
    await sceneTab.getByRole('tab', { name: '移动端 H5', exact: true }).click()
    await expect(stage).toHaveAttribute('data-scene', 'mobile')
    await expect(stage).toHaveAttribute('data-device', 'mobile')
    await expect.poll(frameWidth).toBe('390px')
    mark(`mobile 场景强制：data-device=mobile 框宽=${await frameWidth()}`)
    await shot(page, 'b1-mobile-forced')
  })

  test('B2 AC16：并排对比两作用域互不污染 + 差异条与各自后端文本一致', async ({ page }) => {
    const evidence = attachCollectors(page)
    const extra: string[] = []
    sink = { evidence, extra }
    const mark = (s: string) => extra.push(s)

    await injectRealApiKey(page)
    await page.goto(PLUGIN_ROUTE)
    await modeTab(page, '展厅').click()
    await expect(page.locator('[data-showroom]')).toBeVisible({ timeout: 30_000 })

    const toggles = page.locator('[data-compare-toggle]')
    await expect(toggles.first()).toBeVisible({ timeout: 30_000 })
    // 预设是稳定的内置 8 件、且经 `preview-css` 取数（另一条数据路径）；等它们进衣柜再挑，
    // 避免在衣柜尚未填充时就抓取（项目为空的新实例会只剩 1 件而挑不满两件）。
    await expect(page.locator('[data-compare-toggle^="preset:"]').first()).toBeVisible({ timeout: 30_000 })
    const ids = await toggles.evaluateAll((els) => els.map((e) => e.getAttribute('data-compare-toggle') ?? ''))
    expect(ids.length, '衣柜至少要两件衣服才能对比').toBeGreaterThanOrEqual(2)
    // 固定取两件**内置预设**（不再用"首件 / 末件"）——闸门2 复验实测假红：
    // e2e 目录并行跑且共用同一实例，同目录其它用例会先建出项目，于是"首件"变成**项目衣服**，
    // 而它与"末件预设"可能撞成同一底色（实测两帧都是 rgb(245,247,249)，`not.toBe` 判红）。
    // 这两件预设的表面底色解析值**确定不同**（A=rgb(245,247,249)、B=rgb(244,247,248)，见 AC16 行），
    // 于是"两帧必须不等"恢复为确定性判据；项目取数路径（`export?format=css`）由 A1/AC13 覆盖。
    const PRESET_A = 'preset:admin-calm'
    const PRESET_B = 'preset:mobile-fresh'
    const idA = ids.includes(PRESET_A) ? PRESET_A : (ids.find((id) => id.startsWith('preset:')) ?? ids[0])
    const idB = ids.includes(PRESET_B)
      ? PRESET_B
      : ([...ids].reverse().find((id) => id.startsWith('preset:') && id !== idA) ?? ids[ids.length - 1])
    expect(idA, '对比需要两件不同的衣服').not.toBe(idB)
    mark(`衣柜候选=${ids.length} 件：${ids.join(',')}；取 A=${idA} B=${idB}`)

    await page.locator(`[data-compare-toggle="${idA}"]`).click()
    await page.locator(`[data-compare-toggle="${idB}"]`).click()
    await expect(page.locator('[data-compare]')).toBeVisible({ timeout: 30_000 })

    const frames = page.locator('[data-compare] [data-stage-frame]')
    await expect(frames, '对比区必须恰两个设备框').toHaveCount(2)

    const frameBg = (i: number) =>
      frames.nth(i).locator('.ds-outfit').evaluate((el) => getComputedStyle(el).backgroundColor)
    const frameCss = (i: number) => frames.nth(i).locator('.ds-outfit style').evaluate((el) => el.textContent ?? '')

    // 等两帧画布都真上色（后端文本已应用）
    await expect.poll(() => frameBg(0), { timeout: 30_000 }).not.toBe('rgba(0, 0, 0, 0)')
    await expect.poll(() => frameBg(1), { timeout: 30_000 }).not.toBe('rgba(0, 0, 0, 0)')

    const DIFF_VARS = [
      '--ds-semantic-surface-bg',
      '--ds-semantic-text-1',
      '--ds-semantic-brand',
      '--ds-radius-lg',
      '--ds-font-sans',
    ]
    /** 从注入的 CSS 文本里取某变量的字面值（纯文本，不换算） */
    const varOf = (css: string, name: string): string => {
      const m = new RegExp(`${name}\\s*:\\s*([^;]+);`).exec(css)
      return m ? m[1].trim() : ''
    }

    const cssA = await frameCss(0)
    const cssB = await frameCss(1)

    // 差异条：5 个变量值与各自 CSS 文本里的字面值逐字一致
    const diff = page.locator('[data-compare-diff]')
    await expect(diff).toBeVisible()
    let litBgA = ''
    let litBgB = ''
    for (const v of DIFF_VARS) {
      const row = page.locator(`[data-compare-var="${v}"]`)
      await expect(row, `差异条应有变量 ${v}`).toHaveCount(1)
      const shownA = ((await row.locator('[data-compare-side="a"]').textContent()) ?? '').trim()
      const shownB = ((await row.locator('[data-compare-side="b"]').textContent()) ?? '').trim()
      const rawA = varOf(cssA, v)
      const rawB = varOf(cssB, v)
      expect(rawA, `A 帧注入的 CSS 必须定义 ${v}`).not.toBe('')
      expect(rawB, `B 帧注入的 CSS 必须定义 ${v}`).not.toBe('')
      expect(shownA, `差异条 A 侧 ${v} 必须等于 A 帧后端字面值`).toBe(rawA)
      expect(shownB, `差异条 B 侧 ${v} 必须等于 B 帧后端字面值`).toBe(rawB)
      if (v === '--ds-semantic-surface-bg') {
        litBgA = rawA
        litBgB = rawB
      }
    }

    // 两帧画布 computed 底色各自等于各自后端值；两件皮肤不同 → 底色必须不同（后注入的没覆盖前一帧）。
    // 不能把 CSS 字面值直接 hex→rgb：后端注入的变量可能是 var(--ds-primitive-*) 引用链而非 hex。
    // 改成「在该帧自己的作用域内探针解析变量为 computed 颜色」，再与该帧 .ds-outfit 的实际底色比对。
    const probeColor = (i: number, name: string) =>
      frames
        .nth(i)
        .evaluate((frame, varName) => {
          const scope = frame.querySelector('.ds-outfit')
          if (!scope) return ''
          const probe = document.createElement('span')
          probe.style.color = `var(${varName})`
          scope.appendChild(probe)
          const value = getComputedStyle(probe).color
          probe.remove()
          return value
        }, name)
    const bgA = await frameBg(0)
    const bgB = await frameBg(1)
    const resolvedA = await probeColor(0, '--ds-semantic-surface-bg')
    const resolvedB = await probeColor(1, '--ds-semantic-surface-bg')
    expect(resolvedA, 'A 帧作用域必须解析出 surface-bg').not.toBe('')
    expect(resolvedB, 'B 帧作用域必须解析出 surface-bg').not.toBe('')
    expect(bgA, 'A 帧画布底色必须等于 A 帧自身作用域解析出的后端 surface-bg').toBe(resolvedA)
    expect(bgB, 'B 帧画布底色必须等于 B 帧自身作用域解析出的后端 surface-bg').toBe(resolvedB)
    // 两件衣服 id 不同却底色相同 = 后注入的皮肤覆盖了前一帧（作用域失效）→ 只有真正的互不污染才能保证不等。
    expect(bgA, '两帧画布底色必须不同（作用域互不污染）').not.toBe(bgB)

    mark(`对比 ${idA} vs ${idB}：A bg=${bgA}(变量解析=${resolvedA},字面=${litBgA})；B bg=${bgB}(变量解析=${resolvedB},字面=${litBgB})`)
    // 展厅是三列 + 内部滚动容器，对比区在首屏之下；切到手机框让两帧并排入镜（桌面档单帧 1280px 会把第二帧挤出视口）
    await page.locator('[role="radiogroup"][aria-label="设备"]').getByRole('radio', { name: '手机', exact: true }).click()
    await page.setViewportSize({ width: 1440, height: 1400 })
    await page.locator('[data-compare]').scrollIntoViewIfNeeded()
    await shot(page, 'b2-compare')

    // 关闭对比后对比区消失
    await page.getByRole('button', { name: '关闭对比', exact: true }).click()
    await expect(page.locator('[data-compare]')).toHaveCount(0)
  })

  test('B3 AC14：B 片三个模特页真渲染（元素/种类下限）且控制台无致命错误', async ({ page }) => {
    const evidence = attachCollectors(page)
    const extra: string[] = []
    sink = { evidence, extra }
    const mark = (s: string) => extra.push(s)

    await injectRealApiKey(page)
    await page.goto(PLUGIN_ROUTE)
    await modeTab(page, '展厅').click()
    await expect(page.locator('[data-showroom]')).toBeVisible({ timeout: 30_000 })

    const firstOpt = page.locator('[role="option"][data-outfit-id]').first()
    await expect(firstOpt).toBeVisible({ timeout: 30_000 })
    await firstOpt.click()
    await expect(page.locator('[data-stage]')).toBeVisible({ timeout: 30_000 })
    await expect.poll(() => canvasBg(page), { timeout: 30_000 }).not.toBe('rgba(0, 0, 0, 0)')

    const stage = page.locator('[data-stage]')
    const sceneTab = stage.locator('[role="tablist"][aria-label="场景"]')
    const pageTab = stage.locator('[role="tablist"][aria-label="页面"]')

    for (const p of B_PAGES) {
      await sceneTab.getByRole('tab', { name: p.scene, exact: true }).click()
      await expect(stage).toHaveAttribute('data-scene', p.sceneId)
      await pageTab.getByRole('tab', { name: p.label, exact: true }).click()
      await expect(stage).toHaveAttribute('data-page', p.id)

      const root = page.locator(`[data-mq-page="${p.id}"]`)
      await expect(root, `模特页 ${p.id} 必须渲染出来`).toBeVisible({ timeout: 20_000 })

      const wears = root.locator('[data-mq-wear]')
      const count = await wears.count()
      const kinds = new Set(await wears.evaluateAll((els) => els.map((e) => e.getAttribute('data-mq-wear') ?? '')))
      expect(count, `${p.id} 关键元素数应 ≥ ${p.minWear}（实际 ${count}）`).toBeGreaterThanOrEqual(p.minWear)
      expect(kinds.size, `${p.id} 关键元素种类应 ≥ ${p.minKinds}（实际 ${[...kinds].join(',')}）`).toBeGreaterThanOrEqual(
        p.minKinds,
      )
      mark(`${p.id}: wear=${count} kinds=${kinds.size}(${[...kinds].sort().join('|')})`)
      await shot(page, `b3-${p.id}`)
    }

    const fatalPatterns = [
      /Failed to resolve component/i,
      /does not provide an export named/i,
      /Failed to (fetch|resolve) dynamically imported module/i,
      /Failed to load module script/i,
      /is not defined/i,
    ]
    const fatal = evidence.consoleErrors.filter((l) => fatalPatterns.some((re) => re.test(l)))
    expect(fatal, `控制台出现致命报错：\n${fatal.join('\n')}`).toEqual([])
  })
})

/* ================================================================== */
/* 切片 C：交付与接入 / 深链 / 可达性（AC18–AC23）                      */
/* ================================================================== */

interface McpConfigView {
  port: number
  listenHost: string
  listenUrl: string
  hasToken: boolean
  tokenMasked: string
  isRunning: boolean
}
interface AgentToolView {
  name: string
  summary: string
  readOnly: boolean
}
interface AgentAccessView {
  allowWrite: boolean
}
/** 审查汇总（后端 `DesignReviewer.ReviewSummary`：结构体，不是字符串） */
interface ReviewSummaryView {
  files: number
  declarations: number
  tokenized: number
  hardcoded: number
  tokenCoverage: number | null
  errors: number
  warnings: number
  infos: number
  passed: boolean
  strict: boolean
}
/** 单条发现（后端 `DesignReviewer.ReviewFinding`：结构体，不是字符串） */
interface ReviewFindingView {
  file: string
  line: number
  column: number
  rule: string
  severity: string
  property: string
  found: string
  message: string
}
interface ReviewResultView {
  error: string | null
  summary: ReviewSummaryView | null
  findings: ReviewFindingView[]
  skipped: unknown[]
  truncated: boolean
  notes: string
  theme: string
  themeNote: string | null
}

/**
 * 顶部项目下拉当前选中的项目 id（交付页的规则/说明书/文件/审查四卡都依赖它）。
 * `loadProjects` 完成前下拉是 0（占位项），必须等到自动选中首个项目后再取，否则测试自身会对
 * `projects/0` 发请求并拿到 500（PILOT-ds-m2 输入14 定位到的竞态）。
 */
async function selectedProjectId(page: Page): Promise<number> {
  const sel = page.locator('header .ds-select')
  await expect.poll(async () => Number(await sel.inputValue()), { timeout: 30_000 }).toBeGreaterThan(0)
  return Number(await sel.inputValue())
}

/** 当前选中主题（页脚 `当前主题 <code>`），导出/审查比对都按它取同一档 */
async function currentThemeCode(page: Page): Promise<string> {
  return ((await page.locator('.ds-footer strong').textContent()) ?? '').trim()
}

/** 保证实例里至少有一个非归档项目（否则交付页四个项目卡不渲染）；项目为空则建一个并重载 */
async function ensureProject(page: Page, tag: string): Promise<void> {
  const list = await apiData<ProjectRow[]>(page, '/api/design-system/projects')
  if (list.some((p) => p.status !== 'archived')) return
  await apiPost<ProjectRow>(page, '/api/design-system/projects', {
    code: `e2e-m2c-${tag}`.slice(0, 60),
    name: `E2E 交付 ${tag}`,
  })
  await page.reload()
}

/** 切到「交付与接入」并等 MCP 卡 + 工具表取数完成（工具表 tbody 首行出现才算就绪） */
async function gotoDelivery(page: Page): Promise<void> {
  await modeTab(page, '交付与接入').click()
  await expect(page.locator('[data-delivery]')).toBeVisible({ timeout: 30_000 })
  await expect(page.locator('[data-dv="mcp"]')).toBeVisible({ timeout: 30_000 })
  await expect(page.locator('[data-dv="tools"] tbody tr').first()).toBeVisible({ timeout: 30_000 })
}

test.describe('M2 展厅 · C 片（交付与接入 / 深链 / 可达性）', () => {
  // C 片每条用例都是「建/选项目 → 切交付页 → 取数 → 真发 REST → 比对」的长链路，默认 30s 不够
  // （PILOT-ds-m2 输入14：C4 曾因测试级超时被判 flaky）。
  test.describe.configure({ timeout: 120_000 })
  test('C1 AC18：交付页网关地址与令牌安全（片段占位 / 页面不含真实令牌 / 未运行提示）', async ({ page }) => {
    const evidence = attachCollectors(page)
    const extra: string[] = []
    sink = { evidence, extra }
    const mark = (s: string) => extra.push(s)

    await injectRealApiKey(page)
    await page.goto(PLUGIN_ROUTE)
    await ensureProject(page, `c1-${Date.now().toString(36)}`)
    await gotoDelivery(page)

    const cfg = await apiData<McpConfigView>(page, '/api/mcp-center/config')
    const expectedUrl = cfg.listenUrl ? `${cfg.listenUrl.replace(/\/+$/, '')}/mcp` : ''
    const shownUrl = ((await page.locator('[data-mcp-url]').textContent()) ?? '').trim()
    expect(shownUrl, '网关地址必须 == listenUrl + /mcp').toBe(expectedUrl || '（未运行，暂无地址）')

    // SECURITY：页面 HTML 不得出现真实令牌明文，也不得出现掩码（掩码含真令牌首尾片段）
    const html = await page.content()
    expect(html.includes(getRealApiKey()), '页面 HTML 出现了真实令牌明文').toBe(false)
    if (cfg.tokenMasked) expect(html.includes(cfg.tokenMasked), '连掩码也不许渲染').toBe(false)

    const tokenFlag = await page.locator('[data-mcp-token]').getAttribute('data-mcp-token')
    expect(tokenFlag, '令牌状态只报已配置/未配置').toBe(cfg.hasToken ? 'configured' : 'none')

    const snippet = ((await page.locator('[data-dv="mcp"] pre').textContent()) ?? '').trim()
    const parsed = JSON.parse(snippet) as {
      mcpServers: Record<string, { url: string; headers?: Record<string, string> }>
    }
    expect(parsed.mcpServers['forge-design'].url).toBe(expectedUrl)
    if (cfg.hasToken) {
      expect(parsed.mcpServers['forge-design'].headers?.Authorization, '片段里只写占位符').toBe('Bearer <你的令牌>')
    } else {
      expect(parsed.mcpServers['forge-design'].headers).toBeUndefined()
    }

    if (cfg.isRunning) await expect(page.locator('[data-mcp-stopped]')).toHaveCount(0)
    else await expect(page.locator('[data-mcp-stopped]')).toBeVisible()

    mark(`listenUrl=${cfg.listenUrl} isRunning=${cfg.isRunning} hasToken=${cfg.hasToken} 地址=${shownUrl}`)
    await shot(page, 'c1-delivery-mcp')
  })

  test('C2 AC19：规则/说明书 <pre> == REST 导出原文；复制失败回退为选中文本 + 提示', async ({ page }) => {
    const evidence = attachCollectors(page)
    const extra: string[] = []
    sink = { evidence, extra }
    const mark = (s: string) => extra.push(s)

    await injectRealApiKey(page)
    // 让剪贴板 API 不可用（确定性地触发回退路径，不赌环境权限）
    await page.addInitScript(() => {
      Object.defineProperty(navigator, 'clipboard', {
        configurable: true,
        value: { writeText: () => Promise.reject(new Error('clipboard-denied')) },
      })
    })
    await page.goto(PLUGIN_ROUTE)
    await ensureProject(page, `c2-${Date.now().toString(36)}`)
    await gotoDelivery(page)

    const pid = await selectedProjectId(page)
    const theme = await currentThemeCode(page)
    const rules = await apiText(page, `/api/design-system/projects/${pid}/export?format=agent-rules&theme=${encodeURIComponent(theme)}`)
    const brief = await apiText(page, `/api/design-system/projects/${pid}/export?format=brief&theme=${encodeURIComponent(theme)}`)

    const rulesPre = page.locator('[data-dv-text="agent-rules"]')
    const briefPre = page.locator('[data-dv-text="brief"]')
    await expect(rulesPre).toBeVisible({ timeout: 30_000 })
    await expect(briefPre).toBeVisible({ timeout: 30_000 })
    expect(await rulesPre.textContent(), '规则 <pre> 必须 == REST 原文').toBe(rules)
    expect(await briefPre.textContent(), '说明书 <pre> 必须 == REST 原文').toBe(brief)

    // 复制（规则卡）：剪贴板不可用 → 选中文本 + 提示，不静默失败
    await page.locator('[data-dv="rules"]').getByRole('button', { name: '复制', exact: true }).click()
    await expect(page.locator('[data-dv="rules"] [data-copy-fallback]')).toBeVisible()
    const selected = ((await page.evaluate(() => window.getSelection()?.toString() ?? '')) ?? '').trim()
    expect(selected.length, '回退路径应已选中文本').toBeGreaterThan(20)

    mark(`rules=${rules.length} 字节 brief=${brief.length} 字节；回退选中 ${selected.length} 字符`)
    await shot(page, 'c2-delivery-docs')
  })

  test('C3 AC20：工具表 == agent/tools == meta.agentTools；写开关 PUT 往返一致且刷新保持（收尾还原）', async ({ page }) => {
    const evidence = attachCollectors(page)
    const extra: string[] = []
    sink = { evidence, extra }
    const mark = (s: string) => extra.push(s)

    await injectRealApiKey(page)
    await page.goto(PLUGIN_ROUTE)
    await ensureProject(page, `c3-${Date.now().toString(36)}`)
    await gotoDelivery(page)

    const uiNames = (await page.locator('[data-dv="tools"] tbody tr td:first-child').allTextContents())
      .map((s) => s.trim())
      .sort()
    const apiTools = await apiData<AgentToolView[]>(page, '/api/design-system/agent/tools')
    const meta = await apiData<{ agentTools?: string[] }>(page, '/api/design-system/meta')
    const apiNames = apiTools.map((t) => t.name).sort()
    expect(uiNames, '工具表 == GET agent/tools').toEqual(apiNames)
    expect(apiNames.length).toBeGreaterThan(0)
    expect((meta.agentTools ?? []).slice().sort(), 'meta.agentTools == GET agent/tools').toEqual(apiNames)

    // 写开关：PUT 往返一致 → 刷新保持 → 收尾还原原值
    const original = (await apiData<AgentAccessView>(page, '/api/design-system/agent-access')).allowWrite
    const sw = page.locator('[data-dv="tools"] [role="switch"]')
    await expect(sw).toHaveAttribute('aria-checked', String(original))
    await sw.click()
    await expect(sw).toHaveAttribute('aria-checked', String(!original))
    await expect
      .poll(async () => (await apiData<AgentAccessView>(page, '/api/design-system/agent-access')).allowWrite)
      .toBe(!original)

    await page.reload()
    await gotoDelivery(page)
    const sw2 = page.locator('[data-dv="tools"] [role="switch"]')
    await expect(sw2, '刷新后应保持上一个值').toHaveAttribute('aria-checked', String(!original))

    await sw2.click()
    await expect(sw2).toHaveAttribute('aria-checked', String(original))
    await expect
      .poll(async () => (await apiData<AgentAccessView>(page, '/api/design-system/agent-access')).allowWrite)
      .toBe(original)

    mark(`工具=${uiNames.join(',')}；写开关 ${original} → ${!original} → 还原 ${original}`)
    await shot(page, 'c3-delivery-tools')
  })

  test('C4 AC21：试审查与 POST review 同源；>200KB 客户端拦截不发请求', async ({ page }) => {
    const evidence = attachCollectors(page)
    const extra: string[] = []
    sink = { evidence, extra }
    const mark = (s: string) => extra.push(s)

    await injectRealApiKey(page)
    await page.goto(PLUGIN_ROUTE)
    await ensureProject(page, `c4-${Date.now().toString(36)}`)

    // 只数发往 `POST projects/{id}/review` 的请求（用于证明超限时"一个都没发"）
    let reviewPosts = 0
    page.on('request', (r) => {
      if (r.method() === 'POST' && /\/api\/design-system\/projects\/\d+\/review$/.test(r.url())) reviewPosts++
    })

    await gotoDelivery(page)

    const pid = await selectedProjectId(page)
    const theme = await currentThemeCode(page)
    const SAMPLE = '.card {\n  color: #ff0000;\n  padding: 13px;\n}\n.title { color: red; }'
    const card = page.locator('[data-dv="review"]')

    await card.locator('textarea[aria-label="待审查代码"]').fill(SAMPLE)
    await card.locator('select[aria-label="代码语言"]').selectOption('css')
    await card.getByRole('button', { name: '开始审查', exact: true }).click()
    await expect(card.locator('[data-review-result]')).toBeVisible({ timeout: 30_000 })

    const api = await apiPost<ReviewResultView>(page, `/api/design-system/projects/${pid}/review`, {
      files: [{ path: 'snippet.css', content: SAMPLE, language: 'css' }],
      theme,
      maxFindings: 20,
    })
    // 界面把后端结构体原样渲染（Vue 对对象做 JSON.stringify）——判据 = 「界面解析回来 == REST 对象」
    const uiSummaryRaw = ((await card.locator('[data-review-result] p').first().textContent()) ?? '')
      .replace(/^结论[:：]\s*/, '')
      .trim()
    expect(JSON.parse(uiSummaryRaw), '界面 summary 必须与 POST review 同源').toEqual(api.summary)
    const uiFindings = (await card.locator('[data-review-result] li').allTextContents()).map((s) =>
      JSON.parse(s.trim()),
    )
    expect(uiFindings, '界面 findings 必须与 POST review 同源').toEqual(api.findings)

    const before = reviewPosts
    const big = 'a'.repeat(200 * 1024 + 1)
    await card.locator('textarea[aria-label="待审查代码"]').fill(big)
    await card.getByRole('button', { name: '开始审查', exact: true }).click()
    await expect(card.locator('p[role="alert"]')).toContainText('字节')
    expect(reviewPosts, '超限必须客户端拦截、不发请求').toBe(before)

    mark(
      `summary=${JSON.stringify(api.summary)} findings=${api.findings.length} 条；超限拦截 ok（请求数保持 ${before}）`,
    )
    await shot(page, 'c4-delivery-review')
  })

  test('C5 AC22：深链落到对应场景/页面/衣服/主题/设备，并把选择写回哈希', async ({ page }) => {
    const evidence = attachCollectors(page)
    const extra: string[] = []
    sink = { evidence, extra }
    const mark = (s: string) => extra.push(s)

    await injectRealApiKey(page)
    // 诊断（输入14）：记录导航前 hash 与每次 replace/pushState 的入参，判定宿主是否抹掉深链 fragment
    await page.addInitScript(() => {
      const w = window as unknown as { __nav?: string[] }
      w.__nav = [`init:${location.pathname}${location.search}${location.hash}`]
      const rs = history.replaceState.bind(history)
      const ps = history.pushState.bind(history)
      history.replaceState = ((...a: Parameters<typeof rs>) => {
        w.__nav!.push(`replace:${String(a[2] ?? '')}`)
        return rs(...a)
      }) as typeof rs
      history.pushState = ((...a: Parameters<typeof ps>) => {
        w.__nav!.push(`push:${String(a[2] ?? '')}`)
        return ps(...a)
      }) as typeof ps
    })
    await page.goto(`${PLUGIN_ROUTE}#/showroom/admin-dashboard?outfit=preset:admin-calm&theme=dark&device=mobile`)
    await expect(page.locator('[data-showroom]')).toBeVisible({ timeout: 30_000 })
    const navSeq = await page.evaluate(() => (window as unknown as { __nav?: string[] }).__nav ?? [])
    const wardrobe = await page
      .locator('[data-outfit-id]')
      .evaluateAll((els) => els.map((e) => (e as HTMLElement).dataset.outfitId))
    mark(`导航序列=${JSON.stringify(navSeq)}`)
    mark(`当前 href=${await page.evaluate(() => location.href)}`)
    mark(`衣柜项=${JSON.stringify(wardrobe)}`)

    const opt = page.locator('[data-outfit-id="preset:admin-calm"]')
    await expect(opt, '深链指定的预设衣服应被选中').toBeVisible({ timeout: 30_000 })
    await expect(opt).toHaveAttribute('aria-selected', 'true')

    const stage = page.locator('[data-stage]')
    await expect(stage).toBeVisible({ timeout: 30_000 })
    await expect(stage).toHaveAttribute('data-scene', 'admin')
    await expect(stage).toHaveAttribute('data-page', 'admin-dashboard')
    await expect(stage).toHaveAttribute('data-device', 'mobile')
    await expect(stage).toHaveAttribute('data-theme', 'dark')

    // 写回哈希（replaceState，不新增历史条目）
    await expect.poll(async () => page.evaluate(() => location.hash)).toContain('outfit=preset:admin-calm')
    const hash = await page.evaluate(() => location.hash)
    expect(hash).toContain('theme=dark')
    expect(hash).toContain('device=mobile')

    mark(`深链 hash=${hash}`)
    await shot(page, 'c5-deeplink')
  })

  test('C6 AC23：衣柜/模式条方向键可选，键盘焦点可见，横向溢出 ≤2px', async ({ page }) => {
    const evidence = attachCollectors(page)
    const extra: string[] = []
    sink = { evidence, extra }
    const mark = (s: string) => extra.push(s)

    await injectRealApiKey(page)
    await page.goto(PLUGIN_ROUTE)
    await modeTab(page, '展厅').click()
    await expect(page.locator('[data-showroom]')).toBeVisible({ timeout: 30_000 })

    // 衣柜方向键：选中首件后 ArrowDown → 第二件被选
    const opts = page.locator('[role="option"][data-outfit-id]')
    await expect(opts.first()).toBeVisible({ timeout: 30_000 })
    await expect.poll(() => opts.count()).toBeGreaterThanOrEqual(2)
    await opts.first().click()
    await expect(opts.first()).toHaveAttribute('aria-selected', 'true')
    await page.keyboard.press('ArrowDown')
    await expect(opts.nth(1)).toHaveAttribute('aria-selected', 'true')

    // 键盘驱动的焦点应有可见轮廓（:focus-visible）
    const focus = await page.evaluate(() => {
      const el = document.activeElement as HTMLElement | null
      if (!el) return null
      const cs = getComputedStyle(el)
      return { cls: el.className, outlineStyle: cs.outlineStyle, outlineWidth: cs.outlineWidth }
    })
    expect(focus?.outlineStyle, `键盘焦点应有可见轮廓：${JSON.stringify(focus)}`).not.toBe('none')

    // 模式条方向键：工作台 → 右移 → 交付与接入
    await modeTab(page, '工作台').click()
    await expect(modeTab(page, '工作台')).toHaveAttribute('aria-selected', 'true')
    await modeTab(page, '工作台').focus()
    await page.keyboard.press('ArrowRight')
    await expect(modeTab(page, '交付与接入')).toHaveAttribute('aria-selected', 'true')

    const overflow = await page.evaluate(
      () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
    )
    expect(overflow, `横向溢出 ${overflow}px 应 ≤2px`).toBeLessThanOrEqual(2)

    mark(`衣柜方向键 ok；焦点轮廓=${focus?.outlineStyle}；模式条方向键 ok；overflow=${overflow}px`)
    await shot(page, 'c6-a11y')
  })
})

/**
 * 步骤 13 / AC24：视觉 QA 矩阵。
 *
 * 3 个预设 × 5 类场景 × 明/暗 共 30 张舞台截图，逐张读图核对「间距/颜色/对齐/留白/遮挡/溢出」。
 * 判据（03-plan §E + 自查表 #28）：**每张截图前先断言数据到位**——模特页已挂载（`[data-mq-page]` 恰 1 个）
 * 且画布已由后端文本上色（`.ds-outfit` computed 底色非透明），否则会拍到"空白假图"。
 * 拍**舞台滚动视口**（`.ds-stage__viewport`）= **用户实际看到的画面**，而不是整页或框元素：
 * 框元素宽 1280（桌面档）但被滚动容器裁切，拍框元素会把"相邻的微调面板像素"也框进来（复合图，误导读图）；
 * 拍整页则 30 张里大半是外壳控件。视口图干净、就是用户所见，且每张同时记下可见宽度，
 * 让"桌面档在窄舞台下只能看到左半（需横向滚动）"这一限制**被量化**而不是被图悄悄藏掉。
 */
test.describe('M2 展厅 · D 片（AC24 视觉 QA 矩阵）', () => {
  // 30 张截图 × 每次切换的取数/渲染，长于默认 30s
  test.describe.configure({ timeout: 420_000 })
  test.use({ viewport: { width: 2200, height: 1000 } })

  const QA_PRESETS = ['preset:admin-calm', 'preset:workbench-focus', 'preset:finance-trust'] as const
  const QA_SCENES = [
    { id: 'admin', label: '后台/中台' },
    { id: 'board', label: '状态板' },
    { id: 'workbench', label: '工具/工作台' },
    { id: 'landing', label: '官网/落地页' },
    { id: 'mobile', label: '移动端 H5' },
  ] as const
  const QA_THEMES = [
    { code: 'light', label: '浅色' },
    { code: 'dark', label: '深色' },
  ] as const

  test('D1 AC24：3 预设 × 5 场景 × 明/暗 舞台截图矩阵', async ({ page }) => {
    const evidence = attachCollectors(page)
    const extra: string[] = []
    sink = { evidence, extra }
    const mark = (s: string) => extra.push(s)

    await injectRealApiKey(page)
    await page.goto(`${PLUGIN_ROUTE}#/showroom`)
    await expect(page.locator('[data-showroom]')).toBeVisible({ timeout: 30_000 })
    // 衣柜里的预设经 preview-css 取数，等它们真进衣柜再开始（项目衣服可能只有 1 件）
    await expect(page.locator('[data-compare-toggle^="preset:admin-calm"]')).toBeVisible({ timeout: 30_000 })

    const stage = page.locator('[data-stage]')
    await expect(stage).toBeVisible({ timeout: 30_000 })
    const sceneTab = stage.locator('[role="tablist"][aria-label="场景"]')
    const themeGroup = stage.locator('[role="radiogroup"][aria-label="明暗"]')
    const mqPage = stage.locator('[data-mq-page]')
    const canvas = stage.locator('.ds-outfit')
    const frame = stage.locator('[data-stage-frame]')
    const viewport = stage.locator('.ds-stage__viewport')

    let shotCount = 0
    let minTitleRatio = Number.POSITIVE_INFINITY
    let minCardTitleRatio = Number.POSITIVE_INFINITY
    const lowContrast: string[] = []
    for (const preset of QA_PRESETS) {
      const opt = page.locator(`[data-outfit-id="${preset}"]`)
      await expect(opt, `衣柜应有预设 ${preset}`).toBeVisible({ timeout: 30_000 })
      await opt.click()
      await expect(stage).toHaveAttribute('data-outfit', preset)

      for (const scene of QA_SCENES) {
        await sceneTab.getByRole('tab', { name: scene.label, exact: true }).click()
        await expect(stage).toHaveAttribute('data-scene', scene.id)

        for (const theme of QA_THEMES) {
          await themeGroup.getByRole('radio', { name: theme.label, exact: true }).click()
          await expect(stage).toHaveAttribute('data-theme', theme.code)

          // 数据到位（自查表 #28）：模特页恰 1 个且已渲染
          await expect(mqPage, `${preset}/${scene.id}/${theme.code} 舞台应恰一个模特页`).toHaveCount(1)
          await expect(mqPage).toBeVisible({ timeout: 30_000 })

          const name = `qa-${preset.replace('preset:', '')}-${scene.id}-${theme.code}`
          // 主题**真生效**才算数据到位（闸门2 M-B 修订，2026-10-03）：
          // 只断言 `data-theme` 属性会在"切换瞬间"就通过，而主题重绘发生在之后的帧里 —— 紧接着截图会
          // 拍到上一档的旧色。实测 `qa-admin-calm-admin-dark.png` 均值 (232,235,237) 与它自己的
          // 浅色档 (232,234,237) 几乎相同（每预设的**首张**深色图最易中招，另两预设因先跑过浅色而入稳态）。
          // 判据：画布不透明 **且** 底色亮度方向与所选明暗一致（dark 档必须比中灰暗、light 档必须比中灰亮）。
          await expect
            .poll(
              async () => {
                const c = await canvas.evaluate((el) => getComputedStyle(el).backgroundColor)
                if (c === 'rgba(0, 0, 0, 0)') return 'transparent'
                return relLuminance(c) < 0.5 ? 'dark' : 'light'
              },
              { timeout: 30_000, message: `${name} 画布底色必须已切到 ${theme.code}（防拍到上一档）` },
            )
            .toBe(theme.code)
          // 再放两帧：`getComputedStyle` 已是新值，但合成器仍可能停在上一帧
          await page.evaluate(
            () => new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(() => r(null)))),
          )
          await shotOf(viewport, name)
          shotCount++
          const bg = await canvasBg(page)
          const frameW = await frame.evaluate((el) => Math.round(el.getBoundingClientRect().width))
          const viewW = await viewport.evaluate((el) => el.clientWidth)
          // 读图辅助（AC24）：把"标题/正文的颜色与最近的底"量出来。深色档下标题一旦取不到
          // 主题令牌就会退回宿主/外壳的静态深色，在深底上几乎看不见 —— 这种缺陷在浅色档
          // 看不出来，所以两种明暗都必须记，且必须记到"变量本身解析成了什么"，才能区分
          // "令牌没定义（变量为空）"与"令牌定义了但值不对"。
          const typo = await mqPage.evaluate((el) => {
            const title = el.querySelector<HTMLElement>('.mq-page__title, .mq-hero__title')
            const card = el.querySelector<HTMLElement>('.mq-card')
            const cardTitle = el.querySelector<HTMLElement>('.mq-card__title')
            const cs = getComputedStyle(el)
            return {
              titleColor: title ? getComputedStyle(title).color : '',
              cardColor: card ? getComputedStyle(card).color : '',
              cardBg: card ? getComputedStyle(card).backgroundColor : '',
              cardTitleColor: cardTitle ? getComputedStyle(cardTitle).color : '',
              text1: cs.getPropertyValue('--ds-semantic-text-1').trim(),
              cardFg: cs.getPropertyValue('--ds-component-card-foreground').trim(),
              cardBgVar: cs.getPropertyValue('--ds-component-card-background').trim(),
            }
          })
          // 标题/正文对最近底色的对比度：这是"换肤后文字还看得见吗"的机器判据，
          // 比读图更硬（M2 读图曾漏掉深色档标题恒为 #0f172a 的低对比缺陷）。
          const titleRatio = typo.titleColor ? contrastRatio(typo.titleColor, bg) : Number.NaN
          const cardTitleRatio =
            typo.cardTitleColor && typo.cardBg ? contrastRatio(typo.cardTitleColor, typo.cardBg) : Number.NaN
          if (!Number.isNaN(titleRatio)) minTitleRatio = Math.min(minTitleRatio, titleRatio)
          if (!Number.isNaN(cardTitleRatio)) minCardTitleRatio = Math.min(minCardTitleRatio, cardTitleRatio)
          if (!Number.isNaN(titleRatio) && titleRatio < 4.5) lowContrast.push(`${name} 页标题=${titleRatio.toFixed(2)}`)
          if (!Number.isNaN(cardTitleRatio) && cardTitleRatio < 4.5)
            lowContrast.push(`${name} 卡标题=${cardTitleRatio.toFixed(2)}`)
          mark(
            `${name}：page=${await mqPage.getAttribute('data-mq-page')} wear=${await mqPage.locator('[data-mq-wear]').count()} 底色=${bg} 框宽=${frameW} 可见宽=${viewW}` +
              ` 标题色=${typo.titleColor || 'n/a'}（对比 ${Number.isNaN(titleRatio) ? 'n/a' : titleRatio.toFixed(2)}）` +
              ` 卡标题色=${typo.cardTitleColor || 'n/a'}（对比 ${Number.isNaN(cardTitleRatio) ? 'n/a' : cardTitleRatio.toFixed(2)}）` +
              ` var(text-1)=${typo.text1 || '<空>'} var(card.foreground)=${typo.cardFg || '<空>'} var(card.background)=${typo.cardBgVar || '<空>'}`,
          )
        }
      }
    }

    mark(`矩阵完成：共 ${shotCount} 张（${QA_PRESETS.length} 预设 × ${QA_SCENES.length} 场景 × ${QA_THEMES.length} 明暗）`)
    mark(
      `标题对比度最小：页标题对画布底=${Number.isFinite(minTitleRatio) ? minTitleRatio.toFixed(2) : 'n/a'}，` +
        `卡标题对卡底=${Number.isFinite(minCardTitleRatio) ? minCardTitleRatio.toFixed(2) : 'n/a'}；` +
        `低于 4.5:1 的档位=${lowContrast.length ? lowContrast.join(' | ') : '无'}`,
    )
    expect(shotCount).toBe(QA_PRESETS.length * QA_SCENES.length * QA_THEMES.length)
    // 标题必须跟着"这件衣服"走：外壳 base.css 的 `.ds h1..h4 { color: var(--ds-fg-1) }` 曾把标题
    // 钉死在工作台的静态深色（light 固定 #0f172a）上 —— 深色档就是深底深字。读图时只在浅色档
    // 看不出问题，这条断言让该缺陷无法回潮。
    expect(lowContrast, `标题对比度低于 4.5:1 的档位`).toEqual([])
  })
})

/* ================================================================== */
/* E 片：预览视图档（输入22 · 2026-10-04）                              */
/* ================================================================== */

/**
 * 用户现场原话：「展厅展示，应该缩放，或者可以最大化，或者可以自由调尺寸，并且有滚动条，不能只看到部分」。
 * 实测成因（`:51888`，视口 1372x768）：桌面档 1280px 的稿被 1:1 塞进 648px 的列，右侧卡片被裁一半，
 * 纵向只剩约 434px 可见 —— 见 `docs/ai/pilot/2026-10-04-showroom-preview-fit/mini-task.md`。
 *
 * 六条常驻判据 = 一句用户话一条，且每条都先**断言前提成立**再断言结果（"稿宽 < 可用宽"式的白捡证据不接受）：
 *   E1 缩放：适应档把整幅缩进可用宽，右缘不越界、横向溢出 ≤2px；
 *   E2 滚动条：1:1 下必然溢出（前提），且横滚到末端后**稿的右边缘进入可见区**（可达，不只是可滚）；
 *   E3 最大化：两侧栏让位成单列，画布可用宽比三列时宽出 ≥1.5×；
 *   E4 自由调尺寸：真拖右下角（原生 `resize`），拖窄后缩放比读数跟着变小（证明是重算，不是写死）；
 *   E5 记忆：切档刷新不丢（`ds.showroom.view`），脏值回落「适应」；
 *   E6 宽窗口（1920x1080）：展厅栏不再与工作台共用 1240 封顶 ⇒ 中列宽到 1280，「适应」档即 1:1 全幅、最大化更宽。
 * 反向探针（记 05-evidence）：把 `fitScale` 改成恒返回 1 ⇒ E1 的"右缘不越界"与 E4 的"读数变小"必须转红。
 */
test.describe('M2 展厅 · E 片（预览视图档：缩放 / 1:1 / 最大化 / 拖拽 / 记忆）', () => {
  test.describe.configure({ timeout: 180_000 })
  // 按用户报障的那一档窗口跑：判据只有在"看不全"的尺寸上成立才有意义
  test.use({ viewport: { width: 1372, height: 768 } })

  /** 深链钉死"桌面档 + 后台仪表盘 + 内置预设"：不依赖实例里有没有项目，也不受衣柜顺序影响 */
  const SHOWROOM_LINK = `${PLUGIN_ROUTE}#/showroom/admin-dashboard?outfit=preset:admin-calm&theme=light&device=desktop`

  const stageOf = (page: Page) => page.locator('[data-stage]')
  const viewOf = (page: Page) => page.locator('[data-stage-viewport]')
  const groupBox = (page: Page) => stageOf(page).locator('[role="radiogroup"][aria-label="视图"]')

  /** 缩放比读数（`[data-stage-zoom]` 是唯一出口，不读内部变量） */
  async function zoomPct(page: Page): Promise<number> {
    const raw = ((await stageOf(page).locator('[data-stage-zoom]').textContent()) ?? '').replace('%', '').trim()
    return Number(raw)
  }

  interface Box {
    left: number
    right: number
    width: number
    height: number
    clientWidth: number
    clientHeight: number
    offsetWidth: number
    offsetHeight: number
    scrollWidth: number
    scrollHeight: number
    scrollLeft: number
    scrollTop: number
  }
  const sizeOf = (page: Page): Promise<Box> =>
    viewOf(page).evaluate((el: HTMLElement) => {
      const b = el.getBoundingClientRect()
      return {
        left: b.left,
        right: b.right,
        width: b.width,
        height: b.height,
        clientWidth: el.clientWidth,
        clientHeight: el.clientHeight,
        offsetWidth: el.offsetWidth,
        offsetHeight: el.offsetHeight,
        scrollWidth: el.scrollWidth,
        scrollHeight: el.scrollHeight,
        scrollLeft: el.scrollLeft,
        scrollTop: el.scrollTop,
      }
    })
  /** 设备框（稿）自己的右缘：`zoom` 会参与布局，这里读到的就是屏幕上真实画到的位置 */
  const frameRight = (page: Page): Promise<number> =>
    stageOf(page).locator('[data-stage-frame]').evaluate((el) => el.getBoundingClientRect().right)
  const frameLeft = (page: Page): Promise<number> =>
    stageOf(page).locator('[data-stage-frame]').evaluate((el) => el.getBoundingClientRect().left)

  /** 预览里被试穿的"图形"是否真渲染：柱高 + 柱底色。只量高度会放过"有骨架没皮肤"（G17 取数窗口的空图） */
  const barBoxes = (page: Page): Promise<{ h: number; bg: string }[]> =>
    page.locator('[data-mq-page] .mq-bar').evaluateAll((els) =>
      els.map((e) => ({
        h: (e as HTMLElement).getBoundingClientRect().height,
        bg: getComputedStyle(e).backgroundColor,
      })),
    )

  /** 进展厅 → 等模特页上色 → 返回视图控件 */
  async function openStage(page: Page): Promise<void> {
    await injectRealApiKey(page)
    await page.goto(SHOWROOM_LINK)
    await expect(stageOf(page)).toBeVisible({ timeout: 30_000 })
    await expect(stageOf(page)).toHaveAttribute('data-device', 'desktop')
    await expect.poll(() => canvasBg(page), { timeout: 30_000 }).not.toBe('rgba(0, 0, 0, 0)')
    await expect(stageOf(page).locator('[data-mq-page]')).toHaveCount(1)
  }

  test('E1 缩放：适应档整幅缩进可用宽，右侧不再被裁', async ({ page }) => {
    const evidence = attachCollectors(page)
    const extra: string[] = []
    sink = { evidence, extra }
    const mark = (s: string) => extra.push(s)

    await openStage(page)
    // 无存储值时默认就是「适应」（用户第一眼必须看到不被裁的那一档）
    await expect(groupBox(page).getByRole('radio', { name: '适应', exact: true })).toHaveAttribute(
      'aria-checked',
      'true',
    )

    const b = await sizeOf(page)
    const k = await zoomPct(page)
    // 前提：桌面稿宽 1280 必须**放不下**当前可用宽，否则"没被裁"是白捡的、不构成证据
    expect(b.clientWidth, `可用宽应 < 桌面稿宽 1280（实际 ${b.clientWidth}）`).toBeLessThan(1280)
    expect(k, `适应档应真缩了（读数 ${k}%）`).toBeLessThan(100)
    expect(k, `缩放比不得压到看不见（读数 ${k}%）`).toBeGreaterThan(30)

    const overflow = b.scrollWidth - b.clientWidth
    expect(overflow, `适应档横向溢出 ${overflow}px 应 ≤2px`).toBeLessThanOrEqual(2)
    expect(
      await frameRight(page),
      `框右缘 ${await frameRight(page)} 不得越过容器内容区右缘 ${b.left + b.clientWidth}`,
    ).toBeLessThanOrEqual(b.left + b.clientWidth + 2)
    // 缩完还要"用满"：框应贴着可用宽，而不是缩成一个居中小区块
    expect(
      (await frameRight(page)) - (await frameLeft(page)),
      '框渲染宽应基本等于可用宽（缩放后不留大片空白）',
    ).toBeGreaterThanOrEqual(b.clientWidth * 0.9)

    mark(
      `E1 适应：可用宽=${b.clientWidth} 稿宽=1280 读数=${k}% 横向溢出=${overflow}px 纵向溢出=${b.scrollHeight - b.clientHeight}px` +
        `（盒高 ${b.clientHeight}/${b.scrollHeight}）框右缘=${await frameRight(page)}`,
    )
    // 缩放不许把图形压没，也不许"有骨架没皮肤"：4 根柱既要有高度，也要有后端皮肤给的底色
    const bars = await barBoxes(page)
    expect(bars.length, '仪表盘模特页应有 4 根柱').toBe(4)
    expect(Math.min(...bars.map((x) => x.h)), `适应档下柱高不应为 0（实测 ${bars.map((x) => x.h.toFixed(0)).join('/')}）`).toBeGreaterThan(8)
    expect(
      bars.filter((x) => x.bg === 'rgba(0, 0, 0, 0)').length,
      `柱子底色不得为透明（有高度没颜色＝皮肤还没注入的骨架，G17 同族）：${bars.map((x) => x.bg).join(' | ')}`,
    ).toBe(0)
    // 适应档横向不溢出 ⇒ "画布外还有东西"的提示行不该出现（提示只在 1:1 / 最大化档才有意义）
    await expect(page.locator('[data-stage-hint]')).toHaveCount(0)
    await shotOf(viewOf(page), 'e1-fit')
  })

  test('E2 滚动条：1:1 溢出可滚，且横滚到末端后稿右缘进入可见区', async ({ page }) => {
    const evidence = attachCollectors(page)
    const extra: string[] = []
    sink = { evidence, extra }
    const mark = (s: string) => extra.push(s)

    await openStage(page)
    await groupBox(page).getByRole('radio', { name: '1:1', exact: true }).click()
    await expect.poll(() => zoomPct(page), { timeout: 10_000 }).toBe(100)

    const b = await sizeOf(page)
    // 前提：1:1 下 1280 的稿必须真放不下（溢出为 0 的话下面的"可达"判据就是空的）
    expect(b.scrollWidth, `1:1 下应横向溢出（scrollWidth=${b.scrollWidth} clientWidth=${b.clientWidth}）`).toBeGreaterThan(
      b.clientWidth,
    )
    // 「并且有滚动条」为什么钉"可达 + 提示行"，不钉"条占位"：
    // 本机 Chrome（e2e 用 `channel: 'chrome'`）走**浮层滚动条** —— 连 `overflow:scroll` 的空白 div
    // 都量出 `offsetWidth-clientWidth = 0`；给画布容器写 `::-webkit-scrollbar{width:40px;background:#f0f}`
    // 也**一个像素都没画**（2026-10-05 实测，见本行 mark 与日记输入22）⇒ 自定义条样式被浏览器策略忽略，
    // "条常驻"不是本插件能兑现的承诺。于是这里钉两件真能兑现的：溢出末端可达（下面三条）+ 提示行在场。
    const uaGutter = await viewOf(page).evaluate(() => {
      const probe = document.createElement('div')
      probe.style.cssText = 'width:100px;height:100px;overflow:scroll'
      document.body.appendChild(probe)
      const w = probe.offsetWidth - probe.clientWidth
      const h = probe.offsetHeight - probe.clientHeight
      probe.remove()
      return `w=${w} h=${h}`
    })
    mark(
      `E2 条厚探针：UA条宽 ${uaGutter}（0=浮层条，故不断言"条占位"）；` +
        `容器 offsetWidth-clientWidth=${b.offsetWidth - b.clientWidth}px offsetHeight-clientHeight=${b.offsetHeight - b.clientHeight}px`,
    )
    // 提示行：1:1 与最大化档要告诉用户"画布外还有东西、怎么到达"
    await expect(page.locator('[data-stage-hint]')).toBeVisible()

    await viewOf(page).evaluate((el) => {
      el.scrollLeft = el.scrollWidth
      el.scrollTop = el.scrollHeight
    })
    const r = await sizeOf(page)
    expect(r.scrollLeft, '横向滚动条应可达右端（scrollLeft 停在 0）').toBeGreaterThan(0)
    expect(
      r.scrollLeft + r.clientWidth,
      `横向末端未到达：scrollLeft=${r.scrollLeft} + clientWidth=${r.clientWidth} < scrollWidth=${r.scrollWidth}`,
    ).toBeGreaterThanOrEqual(r.scrollWidth - 2)
    expect(
      r.scrollTop + r.clientHeight,
      `纵向末端未到达：scrollTop=${r.scrollTop} + clientHeight=${r.clientHeight} < scrollHeight=${r.scrollHeight}`,
    ).toBeGreaterThanOrEqual(r.scrollHeight - 2)
    // 「不能只看到部分」的硬判据：滚到最右后，稿的右边缘就在可见区内
    expect(
      await frameRight(page),
      `滚到最右后框右缘 ${await frameRight(page)} 应进入可见区（容器左缘 ${r.left} + 可用宽 ${r.clientWidth}）`,
    ).toBeLessThanOrEqual(r.left + r.clientWidth + 2)

    mark(
      `E2 1:1：clientWidth=${b.clientWidth} scrollWidth=${b.scrollWidth} 滚动条占位=${b.offsetWidth - b.clientWidth}px；` +
        `滚到末端 scrollLeft=${r.scrollLeft} scrollTop=${r.scrollTop} 框右缘=${await frameRight(page)}`,
    )
    await shotOf(viewOf(page), 'e2-actual-scrolled-right')
  })

  test('E3 最大化：两侧栏让位成单列，画布可用宽宽出 ≥1.5×', async ({ page }) => {
    const evidence = attachCollectors(page)
    const extra: string[] = []
    sink = { evidence, extra }
    const mark = (s: string) => extra.push(s)

    await openStage(page)
    const threeCol = (await sizeOf(page)).clientWidth

    await groupBox(page).getByRole('radio', { name: '最大化', exact: true }).click()
    await expect(page.locator('[data-showroom-layout]')).toHaveClass(/ds-showroom__layout--max/)
    const cols = await page.locator('[data-showroom-layout]').evaluate((el) => getComputedStyle(el).gridTemplateColumns)
    expect(cols.trim().split(/\s+/).length, `最大化后应只剩一列（实测 ${cols}）`).toBe(1)

    await expect
      .poll(async () => (await sizeOf(page)).clientWidth, { timeout: 10_000, message: '最大化后画布应变宽' })
      .toBeGreaterThanOrEqual(threeCol * 1.5)
    expect(await zoomPct(page), '最大化不靠缩小换宽度，读数应为 100%').toBe(100)

    const b = await sizeOf(page)
    mark(`E3 最大化：三列可用宽=${threeCol} → 单列=${b.clientWidth}（${(b.clientWidth / threeCol).toFixed(2)}×）列=${cols}`)
    await shotOf(stageOf(page), 'e3-max')
  })

  test('E4 自由调尺寸：拖窄画布盒后缩放比跟着重算', async ({ page }) => {
    const evidence = attachCollectors(page)
    const extra: string[] = []
    sink = { evidence, extra }
    const mark = (s: string) => extra.push(s)

    await openStage(page)
    await viewOf(page).scrollIntoViewIfNeeded()
    let box = await viewOf(page).boundingBox()
    expect(box, '画布容器应有可拖的右下角').toBeTruthy()
    // 右下角若落在折屏之外，先把页面滚上来，保证拖点真的在视口内
    if (box!.y + box!.height > 700) {
      await page.mouse.wheel(0, box!.y + box!.height - 560)
      box = await viewOf(page).boundingBox()
    }
    const before = { w: box!.width, k: await zoomPct(page) }
    const grip = { x: box!.x + box!.width - 3, y: box!.y + box!.height - 3 }

    await page.mouse.move(grip.x, grip.y)
    await page.mouse.down()
    await page.mouse.move(grip.x - 220, grip.y, { steps: 12 })
    await page.mouse.up()

    const after = await sizeOf(page)
    const kAfter = await zoomPct(page)
    expect(after.width, `拖拽应改小画布盒宽度：${before.w} → ${after.width}`).toBeLessThanOrEqual(before.w - 40)
    expect(kAfter, `可用宽变窄后缩放比应跟着重算：${before.k}% → ${kAfter}%`).toBeLessThan(before.k)
    expect(
      after.scrollWidth - after.clientWidth,
      `缩完仍不得裁切（溢出 ${after.scrollWidth - after.clientWidth}px）`,
    ).toBeLessThanOrEqual(2)

    mark(`E4 拖拽：宽 ${before.w.toFixed(0)}→${after.width.toFixed(0)}，读数 ${before.k}%→${kAfter}%`)
    await shotOf(viewOf(page), 'e4-resized-narrow')
  })

  test('E6 宽窗口（1920x1080）：画布跟着窗口变宽，适应档几乎 1:1 且无裁切', async ({ page }) => {
    const evidence = attachCollectors(page)
    const extra: string[] = []
    sink = { evidence, extra }
    const mark = (s: string) => extra.push(s)

    await openStage(page)
    const narrow = await sizeOf(page)
    await page.setViewportSize({ width: 1920, height: 1080 })
    // 放两帧让 ResizeObserver 与 `zoom` 重排落地，再读（不靠固定 sleep）
    await page.evaluate(
      () => new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(() => r(null)))),
    )
    const b = await sizeOf(page)
    const k = await zoomPct(page)
    // 这一条钉的是"屏幕更大 → 画布更大，大到 1:1 装得下整幅稿"：展厅栏的 `max-width` 曾与工作台共用
    // 1240，于是 1920 窗口下中列仍只有 648（读数 51%）。现放宽到 1872（= 1280 稿 + 两侧栏 2×240
    // + 两条间距 2×24 + 左右内衬 2×32），实测 1920 下中列恰为 **1280**、读数 100%（不放大也不裁）。
    // 宽度判据留 10px 余量：宿主自身内衬/滚动条会让可用宽略小于算式值；用户可见的那个数（读数）不留。
    expect(b.clientWidth, `1920 下中列应容得下 1280 的稿（1372 时是 ${narrow.clientWidth}，现在是 ${b.clientWidth}）`).toBeGreaterThanOrEqual(
      1270,
    )
    expect(k, `宽屏富余时不该放大（读数 ${k}%）`).toBeLessThanOrEqual(100)
    expect(k, `宽屏下「适应」档就该是 1:1（读数 ${k}%）`).toBe(100)
    expect(b.scrollWidth - b.clientWidth, '适应档不得有横向溢出').toBeLessThanOrEqual(2)
    expect(await frameRight(page)).toBeLessThanOrEqual(b.left + b.clientWidth + 2)
    mark(
      `E6 1920 适应：可用宽 ${narrow.clientWidth} → ${b.clientWidth} 读数=${k}% 溢出=${b.scrollWidth - b.clientWidth}px`,
    )
    await shotOf(viewOf(page), 'e6-fit-1920')

    // 最大化：单列更宽，1280 的稿整幅放下
    await groupBox(page).getByRole('radio', { name: '最大化', exact: true }).click()
    await expect
      .poll(async () => (await sizeOf(page)).clientWidth, { timeout: 10_000 })
      .toBeGreaterThanOrEqual(b.clientWidth)
    const m = await sizeOf(page)
    expect(await zoomPct(page), '最大化档不靠缩小换宽度').toBe(100)
    expect(m.scrollWidth - m.clientWidth, `最大化 1:1 全幅应无溢出（${m.scrollWidth - m.clientWidth}px）`).toBeLessThanOrEqual(2)
    expect(await frameRight(page)).toBeLessThanOrEqual(m.left + m.clientWidth + 2)
    mark(`E6 1920 最大化：可用宽=${m.clientWidth} 溢出=${m.scrollWidth - m.clientWidth}px 框右缘=${await frameRight(page)}`)
    await shotOf(viewOf(page), 'e6-max-1920')
  })

  test('E5 记忆：切到 1:1 刷新后仍是 1:1', async ({ page }) => {
    const evidence = attachCollectors(page)
    const extra: string[] = []
    sink = { evidence, extra }
    const mark = (s: string) => extra.push(s)

    await openStage(page)
    await groupBox(page).getByRole('radio', { name: '1:1', exact: true }).click()
    await expect(groupBox(page).getByRole('radio', { name: '1:1', exact: true })).toHaveAttribute('aria-checked', 'true')
    expect(await page.evaluate(() => localStorage.getItem('ds.showroom.view'))).toBe('actual')

    await page.reload()
    await expect(stageOf(page)).toBeVisible({ timeout: 30_000 })
    // 刷新后先等画布**被后端文本上色**再判/再拍：皮肤未注入时的骨架图没有卡片底色与柱色，
    // 拿它当"最终状态"取证会拍到一张空图（本用例首版读图就拍到过，见 05-evidence）。
    await expect
      .poll(() => canvasBg(page), { timeout: 30_000, message: '刷新后画布必须已被后端皮肤上色' })
      .not.toBe('rgba(0, 0, 0, 0)')
    await expect
      .poll(() => zoomPct(page), { timeout: 30_000, message: '刷新后应仍是 1:1（读数回到适应档那种 <100% 即为失忆）' })
      .toBe(100)
    await expect(groupBox(page).getByRole('radio', { name: '1:1', exact: true })).toHaveAttribute('aria-checked', 'true')
    // 脏值不崩：手改存储成未知档位时应回落「适应」，而不是让舞台消失
    await page.evaluate(() => localStorage.setItem('ds.showroom.view', 'maximise'))
    await page.reload()
    await expect(stageOf(page)).toBeVisible({ timeout: 30_000 })
    await expect
      .poll(() => canvasBg(page), { timeout: 30_000, message: '脏值回落后的画布也必须真上色' })
      .not.toBe('rgba(0, 0, 0, 0)')
    await expect(groupBox(page).getByRole('radio', { name: '适应', exact: true })).toHaveAttribute('aria-checked', 'true')

    // 读图时这张曾出现"柱子是空的"：机器判据同时钉"有高度"和"有皮肤色"（只量高度会放过骨架图）
    await expect
      .poll(async () => {
        const bars = await barBoxes(page)
        if (bars.length !== 4) return -1
        const painted = bars.filter((x) => x.bg !== 'rgba(0, 0, 0, 0)').length
        return painted === 4 ? Math.min(...bars.map((x) => x.h)) : -1
      }, { timeout: 15_000, message: '回落适应档后 4 根柱要既有高度又有底色（-1＝还没皮肤/柱数不对）' })
      .toBeGreaterThan(8)
    const final = await barBoxes(page)
    mark(`E5 记忆：1:1 刷新保持；脏值回落适应档；回落柱=${final.map((x) => `${x.h.toFixed(0)}px/${x.bg}`).join(' ')}`)
    await shotOf(viewOf(page), 'e5-persist')
  })
})