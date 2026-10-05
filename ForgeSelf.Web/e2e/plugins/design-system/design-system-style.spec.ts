import { test, expect, type Page } from '@playwright/test'
import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { injectRealApiKey } from '../../helpers/real-auth'
import {
  apiData,
  apiPost,
  apiText,
  attachCollectors,
  canvasBg,
  contrastRatio,
  dumpEvidence,
  relLuminance,
  shot,
  shotOf,
  type Evidence,
} from './design-system-helpers'

/**
 * 设计系统插件 M3 切片 A（风格轴 + 预设库）+ 切片 C 视觉矩阵 端到端验证 · AC11 / AC21。
 *
 * 这些判据都是"后端单测证明不了"的那一类：
 *  S1 = 衣柜里两件轴取值不同的衣服，**注入到舞台的交付 CSS 真的不一样**（`--ds-font-display` 有/无、
 *       卡片 `box-shadow` 形状不同），且「更多风格选项」的控件条数与取值文案全部来自 `GET meta.styleAxes`
 *       （界面没抄词表）。舞台文本只在**逐条等于这件衣服自己的交付 CSS** 时才采信
 *       （`outfitCssText` 注释记了两轮实测假读）。**注**：标题的 computed `font-family` 目前仍等于正文字族——
 *       既有排版令牌投影缺口，见 05-evidence「Known Limitations #1」，本文件把那半条如实拍下而不是假装成立。
 *  S2 = 在微调面板改一条轴，改的值**必须进到"保存为新设计"发出去的请求体**，并且落库后交付 CSS 里
 *       真的是那个值（展厅微调本身没有实时预览，M2 既有缺口 → 判据落在"请求体 + 落库产物"这两处可证的地方）。
 *  S3 = 用 `editorial-serif` 走完向导创建 → 后端 `tokens/effective` 里**真有** `font.display`
 *       （产物落库，不是前端自己拼的假变量）。
 *  V1/V2/V3 = 视觉 QA 矩阵（FR16/AC21）：13 预设 × 明暗、5 件新预设 × 第二三类场景、
 *       7 条轴的每个非默认取值逐张拍图并与默认档对照逐变量核差异。详见文件末尾 V 片注释。
 *
 * 纪律同 M2：真实宿主零 mock；唯一项目码 `e2e-m3-<时间戳>`；截图前先断言数据到位；收尾不清理、不硬删。
 */

const MANIFEST = JSON.parse(
  readFileSync(
    fileURLToPath(new URL('../../../../Plugins/DesignSystem/plugin.json', import.meta.url)),
    'utf-8',
  ).replace(/^\uFEFF/, ''),
) as { Version: string; frontend: { route: string } }

const PLUGIN_ROUTE = MANIFEST.frontend.route
const RUN_CODE = `e2e-m3-${Date.now().toString(36)}`

interface StyleAxisInfo {
  axis: string
  field: string
  kind: string
  label: string
  default: string | number
  values?: string[]
  valueLabels?: Record<string, string>
  /** 数值轴（`kind === 'number'`）才有：V 片按"两端点"取非默认值 */
  min?: number
  max?: number
  step?: number
}

/** `GET meta` 的风格轴清单（后端 `StyleAxes` 唯一真源；本文件也用它当判据，不写死七条） */
async function metaAxes(page: Page): Promise<StyleAxisInfo[]> {
  const meta = await apiData<{ styleAxes?: StyleAxisInfo[] }>(page, '/api/design-system/meta')
  return meta.styleAxes ?? []
}

const titleFamily = (page: Page) =>
  page.locator('[data-stage] .mq-page__title').first().evaluate((el) => getComputedStyle(el).fontFamily)

/**
 * 注入到舞台里的那段后端 CSS → 变量名到值（只取声明行）。
 * 为什么比声明行而不是整串字节：`OutfitScope` 用 `scopeCssToSkin` 把 `:root` 改写成
 * `[data-outfit="…"]`，正文声明行不变（V3 的 `stageDecls` 先用上这条口径，现提到文件头共用）。
 */
function cssDecls(css: string): Map<string, string> {
  const out = new Map<string, string>()
  for (const line of css.split('\n')) {
    const m = /^\s*(--[\w-]+)\s*:\s*(.+?)\s*;?\s*$/.exec(line)
    if (m) out.set(m[1] as string, m[2] as string)
  }
  return out
}

/** 舞台此刻的注入文本（直接查 DOM，不走 locator 的自动等待——到位判据在 `outfitCssText` 里自己轮询） */
const stageCssText = (page: Page, outfitId: string): Promise<string> =>
  page.evaluate((id) => {
    const el = document.querySelector(`[data-stage] [data-outfit="${id}"] style`)
    return el?.textContent ?? ''
  }, outfitId)

/** `POST presets/recommend` 命中项（本文件只消费这两个键） */
interface PresetMatchLite {
  id: string
  request?: Record<string, unknown>
}

/** 预设 request 的进程内缓存（一次全量拿；见 `restCssForPreset` 为什么要它） */
let presetMatches: PresetMatchLite[] | null = null

/**
 * 某件预设衣服在指定明暗档下的**权威交付 CSS**：`request`（`POST presets/recommend`，`GET presets` 不带它）
 * → `POST generate/preview-css`（内存投影，零写库），也就是展厅自己取数走的那条链。
 *
 * 字段表逐条对齐 `outfits.ts` 的 `toPreviewInput`：多一列或少一列，"舞台 == 交付"这条判据就会
 * 变成假红或假绿，所以这里宁肯写全也不含糊展开。
 */
const restCssForPreset = async (page: Page, presetId: string, theme: string): Promise<string> => {
  if (!presetMatches) {
    presetMatches = await apiPost<PresetMatchLite[]>(page, '/api/design-system/presets/recommend', { limit: 999 })
  }
  const hit = presetMatches.find((m) => m.id === presetId)
  expect(hit, `presets/recommend 必须返回 ${presetId} 及其 request（预设生成入参的唯一出口）`).toBeTruthy()
  const r = hit!.request ?? {}
  const out = await apiPost<{ css?: string }>(page, '/api/design-system/generate/preview-css', {
    brief: r.brief ?? undefined,
    seedColor: r.seedColor ?? undefined,
    hue: r.hue ?? undefined,
    chroma: r.chroma ?? undefined,
    density: 'default',
    typeRatio: r.typeRatio ?? undefined,
    typeBasePx: r.typeBasePx ?? undefined,
    radiusBase: r.radiusBase ?? undefined,
    motionScale: r.motionScale ?? undefined,
    brandName: r.brandName ?? undefined,
    industry: r.industry ?? undefined,
    accentHueOffset: r.accentHueOffset ?? undefined,
    shadowStyle: r.shadowStyle ?? undefined,
    shadowStrength: r.shadowStrength ?? undefined,
    borderStrength: r.borderStrength ?? undefined,
    neutralTemp: r.neutralTemp ?? undefined,
    fontPairing: r.fontPairing ?? undefined,
    radiusStyle: r.radiusStyle ?? undefined,
    accentStrategy: r.accentStrategy ?? undefined,
    theme,
  })
  expect(out.css, `${presetId}/${theme} 的 preview-css 必须回 CSS 文本`).toBeTruthy()
  return out.css as string
}

/**
 * 某件衣服**实际注入到舞台里的那段后端 CSS 原文**，并**先证明它逐条等于这件衣服自己的交付 CSS** 才返回。
 *
 * 判据为什么必须是"等于权威源"，而不是"非空"或"和上一次不一样"（两轮实测红账）：
 *  ① `<style>` 节点先渲染、`loadCss()` 的取数随后才填进来 → 读到空串（半加载态，自查表 #28 同族）；
 *  ② 换装瞬间 `Stage.vue` 把**新衣服 id + 旧的 css 文本**一起交给 `OutfitScope`
 *     （`Showroom.vue` 的 `loadCss()` 取数期间不清空 `css`，Vue 又原地复用同一个 div），
 *     于是节点上是「挂着新 id 的上一件皮肤」——非空判据与"变化"判据都挡不住它
 *     （S1 实测：读 `preset:tech-crisp` 拿到的是上一件的青绿强调色）。
 *     产品侧根因已记 TODO（`showroom/Showroom.vue` + `showroom/Stage.vue` 不在 04-task Allowed 名单）。
 * 只有"声明行逐条等于这件衣服的交付 CSS"同时挡住这两种假读，并且顺手正面证明 03-plan 的同源纪律
 * （画布上看到的就是导出交付的那一份）。
 */
const outfitCssText = async (page: Page, outfitId: string, expectedRestCss: string): Promise<string> => {
  const rest = cssDecls(expectedRestCss)
  expect(rest.size, `${outfitId} 的交付 CSS 必须有变量（否则判据无从谈起）`).toBeGreaterThan(20)
  await expect
    .poll(
      async () => {
        const dom = cssDecls(await stageCssText(page, outfitId))
        if (dom.size !== rest.size) return false
        for (const [k, v] of rest) if (dom.get(k) !== v) return false
        return true
      },
      {
        timeout: 30_000,
        message: `${outfitId} 的舞台注入必须逐条等于它自己的交付 CSS（不等 = 还在取数 / 装了上一件 / 取数失败被吞）`,
      },
    )
    .toBe(true)
  return stageCssText(page, outfitId)
}
const cardShadow = (page: Page) =>
  page.locator('[data-stage] .mq-card').first().evaluate((el) => getComputedStyle(el).boxShadow)

const stage = (page: Page) => page.locator('[data-stage]')

async function wearPreset(page: Page, presetId: string): Promise<void> {
  await expect(page.locator('[data-showroom]')).toBeVisible({ timeout: 30_000 })
  const option = page.locator(`[role="option"][data-outfit-id="preset:${presetId}"]`)
  await expect(option).toBeVisible({ timeout: 30_000 })
  await option.click()
  await expect(stage(page)).toBeVisible({ timeout: 30_000 })
  // 换装后必须等画布真的有了标题元素（截图/取值都要拍到"已就绪"，不是拍到半加载态）
  await expect(page.locator('[data-stage] .mq-page__title')).toBeVisible({ timeout: 30_000 })
  await expect(page.locator('[data-stage] .mq-card').first()).toBeVisible()
}

/* ---------------- 证据：无论通过与否都落一份 ---------------- */

let sink: { evidence: Evidence; extra: string[] } | null = null

// eslint-disable-next-line no-empty-pattern
test.afterEach(({}, testInfo) => {
  if (!sink) return
  const status = testInfo.status === testInfo.expectedStatus ? 'passed' : 'FAILED'
  const key = testInfo.title.trim().slice(0, 2)
  dumpEvidence(`style-${key}-${status}`, sink.evidence, sink.extra)
})

test.describe.configure({ mode: 'serial', timeout: 300_000 })

test('S1 两件轴取值不同的衣服在舞台上真的不一样，且轴控件全部来自 meta', async ({ page }) => {
  const evidence = attachCollectors(page)
  const extra: string[] = []
  sink = { evidence, extra }
  const mark = (s: string) => extra.push(s)

  await injectRealApiKey(page)
  await page.goto(PLUGIN_ROUTE)

  // 读 meta 必须在导航之后：`apiData` 借页面里的 token 打接口，`about:blank` 上读不到 localStorage
  const axes = await metaAxes(page)
  expect(axes.length, 'meta.styleAxes 必须出清单（后端 StyleAxes 是唯一真源）').toBeGreaterThan(0)
  expect(axes.every((a) => a.kind === 'enum' || a.kind === 'number'), '每条轴都要声明 kind').toBe(true)
  mark(`meta.styleAxes 条数=${axes.length}：${axes.map((a) => `${a.field}(${a.kind})`).join(' ')}`)

  await page.getByRole('tab', { name: '展厅', exact: true }).click()

  // ---- 控件面板：条数 == meta 条数；取值文案用 meta 的 valueLabels（界面没另译一份）----
  const panel = page.locator('[data-style-axes]')
  await expect(panel).toBeVisible({ timeout: 30_000 })
  await panel.locator('summary').click()
  await expect(panel.locator('[data-axis]')).toHaveCount(axes.length)

  const enumAxis = axes.find((a) => a.kind === 'enum' && (a.values?.length ?? 0) > 1)
  expect(enumAxis, 'meta 里应至少有一条枚举轴').toBeTruthy()
  const group = panel.locator(`[data-axis="${enumAxis!.field}"]`)
  await expect(group.locator('[data-axis-value]')).toHaveCount(enumAxis!.values!.length)
  const firstValue = enumAxis!.values![0]
  const chipText = (await group.locator(`[data-axis-value="${firstValue}"]`).innerText()).trim()
  expect(chipText, '芯片显示的是 meta 给的中文说法，不是界面自己译的').toBe(
    enumAxis!.valueLabels?.[firstValue] ?? firstValue,
  )
  mark(`轴 ${enumAxis!.field}：芯片「${chipText}」= meta.valueLabels["${firstValue}"]`)

  // ---- editorial-serif：注入的交付 CSS 里必须真定义了展示字族 ----
  await wearPreset(page, 'editorial-serif')
  const editorialRest = await restCssForPreset(page, 'editorial-serif', 'light')
  const editorialCss = await outfitCssText(page, 'preset:editorial-serif', editorialRest)
  const editorialShadow = await cardShadow(page)
  const editorialFamily = await titleFamily(page)
  expect(editorialCss, 'editorial-serif 注入舞台的 CSS 必须定义 --ds-font-display（字体搭配轴的真产物）').toMatch(
    /--ds-font-display\s*:\s*"[^"]*Noto Serif SC/i,
  )
  mark(
    `editorial-serif 注入 CSS 长度=${editorialCss.length}，含 --ds-font-display；` +
      `与 preview-css 交付文本声明行逐条相等（${cssDecls(editorialRest).size} 个变量）；标题计算字族=${editorialFamily}`,
  )
  await shot(page, 's1-outfit-editorial-serif')

  // ---- tech-crisp：同一位置的字族轴取值不同 → 注入的 CSS 里没有这条变量 ----
  await wearPreset(page, 'tech-crisp')
  const techRest = await restCssForPreset(page, 'tech-crisp', 'light')
  const techCss = await outfitCssText(page, 'preset:tech-crisp', techRest)
  const techShadow = await cardShadow(page)
  expect(techCss, 'tech-crisp 不带 editorial，它的 CSS 里不该出现 --ds-font-display').not.toMatch(/--ds-font-display\s*:/)
  expect(techShadow, '两套阴影必须真的不同（flat 环线 vs crisp 单层锐利）').not.toBe(editorialShadow)
  expect(techShadow, '卡片不能没有阴影（有变量但计算值为 none 说明链路断了）').not.toBe('none')
  mark(
    `tech-crisp 注入 CSS 长度=${techCss.length}（同样 == 交付 CSS，${cssDecls(techRest).size} 个变量）；卡片阴影=${techShadow}`,
  )

  // ---- 已知缺口如实记录（不在本批偷偷改默认产物）----
  // 见 05-evidence「Known Limitations」：复合 typography 令牌导出时 fontFamily 被丢弃，
  // 因此"标题在展厅里看起来是衬线"这条**还不成立**；此处把事实拍下来，不假装它成立。
  expect(editorialFamily, '当前导出把复合排版令牌的 fontFamily 丢了：标题字族仍等于正文字族（缺口，非断言目标）').toBe(
    await titleFamily(page),
  )
})

test('S2 微调面板选一条非默认轴：它真的进入生成请求并落进交付 CSS', async ({ page }) => {
  const evidence = attachCollectors(page)
  const extra: string[] = []
  sink = { evidence, extra }
  const mark = (s: string) => extra.push(s)

  await injectRealApiKey(page)
  await page.goto(PLUGIN_ROUTE)

  const axes = await metaAxes(page)
  const shadowAxis = axes.find((a) => a.field === 'shadowStyle')
  expect(shadowAxis, 'meta 必须有 shadowStyle 轴').toBeTruthy()
  // 点名 crisp（不是"随便一个非默认值"）：crisp 的期望值是**人工核算出来的公式结果**，
  // 与后端单测 AC3_crisp阴影公式 那条逐字同源——两条门禁在同一串数字上对上，才算轴真的通到底
  const nonDefault = 'crisp'
  expect(shadowAxis!.values, `meta 的阴影轴取值里应有 ${nonDefault}`).toContain(nonDefault)

  const projectsBefore = await apiData<unknown[]>(page, '/api/design-system/projects')

  await page.getByRole('tab', { name: '展厅', exact: true }).click()
  await wearPreset(page, 'admin-calm')

  const panel = page.locator('[data-style-axes]')
  await panel.locator('summary').click()

  // 点之前先确认控件显示的是"这件衣服的当前值"（默认档），点之后必须换成新值 —— 否则等于点了个摆设
  const chipSelector = `[data-axis="shadowStyle"] [data-axis-value="${nonDefault}"]`
  await expect(panel.locator(chipSelector)).toHaveCount(1)
  await expect(panel.locator(chipSelector)).not.toHaveClass(/ds-chip--on/)
  await panel.locator(chipSelector).click()
  await expect(panel.locator(chipSelector)).toHaveClass(/ds-chip--on/)
  mark(`已点选 shadowStyle=${nonDefault}`)

  // 轴必须进到"保存为新设计"发出去的请求体里（这是微调面板唯一的出口，也是它是否真接上生成的唯一判据）
  const requestPromise = page.waitForRequest(
    (r) => r.url().endsWith('/api/design-system/projects/quick-create') && r.method() === 'POST',
  )
  // 响应也要抓：批 C 实测出现过"宿主日志已写『项目已创建 ds-0cdb81』、而紧接着的 GET projects 查不到它"
  // ——只有把**同一瞬间的 200 响应**也记进证据，才能把"写失败"与"写成功但读侧滞后"这两种解释分开。
  const responsePromise = page.waitForResponse(
    (r) => r.url().endsWith('/api/design-system/projects/quick-create') && r.request().method() === 'POST',
  )
  await page.getByRole('button', { name: '保存为新设计', exact: true }).click()
  const body = JSON.parse((await requestPromise).postData() ?? '{}') as { request?: Record<string, unknown> }
  const resp = await responsePromise
  const respText = await resp.text()
  expect(resp.status(), `quick-create 必须回 200（实回 ${resp.status()}：${respText.slice(0, 160)}）`).toBe(200)
  expect(body.request?.shadowStyle, '微调选的轴必须写进生成请求（不能只留在界面状态里）').toBe(nonDefault)
  mark(`quick-create 请求体 shadowStyle=${JSON.stringify(body.request?.shadowStyle)}；响应 ${resp.status()} ${respText.slice(0, 100)}`)

  // 落库后回读：这件"新设计"的阴影确实与默认档不同（证明轴不是装饰）
  await expect(page.locator('[data-outfit-id^="project:"]').first()).toBeVisible({ timeout: 60_000 })
  const beforeCodes = new Set((projectsBefore as { code: string }[]).map((p) => p.code))
  let created: { code: string; id: number }[] = []
  let listReads = 0
  await expect
    .poll(
      async () => {
        listReads++
        const all = await apiData<{ code: string; id: number }[]>(page, '/api/design-system/projects')
        created = all.filter((x) => !beforeCodes.has(x.code))
        mark(`第 ${listReads} 次读项目清单：共 ${all.length} 条、相对保存前新增 ${created.length} 条`)
        return created.length
      },
      {
        timeout: 30_000,
        intervals: [400, 800, 1500],
        message: '保存为新设计必须真的新增一个项目（读不到 = 列表端点也读到旧视图；逐次条数见证据行，quick-create 的 200 已先记下）',
      },
    )
    .toBe(1)
  if (listReads > 1) mark(`列表读侧滞后实锤：第 ${listReads} 次才读到新增的那条（写侧已回 200）`)
  const exportUrl = `/api/design-system/projects/${created[0].id}/export?format=css&theme=light`
  /**
   * 到位判据：**落库导出的变量集必须等于"同一个生成请求的内存预览"**（`generate/preview-css` 零写库、当场跑生成器）。
   *
   * 为什么把判据从"整族齐全"换成这条（两条实测数据把它否掉了）：
   *  ① 同一份导出在批上下文里出现过 **7628 / 9086 / 17074** 字符三种长度，7628 那次 `--ds-shadow-*` **整族为零**；
   *  ② 更要命的是 **`tokens/effective` 自己也会读到旧视图**：同一项目在批上下文读到 `count=199 / 导出 200 个变量`，
   *     在子集里读到 `count=309 / 导出 310 个`——**两个读路径一起陈旧时，"导出 == effective"照样成立，产物却仍是残缺的**。
   *  ⇒ 判据必须挂在不吃这条"写→读"链的权威源上：同一 request 的 `preview-css`（AC2/AC8 已证内存预览与落库导出同源）。
   *     变量**名**集合必须一致（同一生成器，结构相同；值随预设不同，所以不比值）。
   *  轮询到一致才放行，并把每次尝试的字符数/变量数记进证据文件：`attempts > 1` 就是"写后立读滞后"的读数。
   */
  const preview = await apiPost<{ css?: string }>(page, '/api/design-system/generate/preview-css', {
    ...(body.request ?? {}),
    density: 'default',
    theme: 'light',
  })
  const wantVars = cssDecls(preview.css ?? '')
  expect(wantVars.size, 'preview-css 必须回一份完整变量集（否则下面的等式判据是空转）').toBeGreaterThan(200)
  let attempts = 0
  let css = ''
  await expect
    .poll(
      async () => {
        attempts++
        css = await apiText(page, exportUrl)
        const got = cssDecls(css)
        mark(`第 ${attempts} 次读导出：${css.length} 字符、变量 ${got.size} 个（同请求 preview-css = ${wantVars.size} 个）`)
        // 只断"一条都不许少"，不断"条数必须相等"：落库导出允许比预览多几层兼容别名（`--el-*`），
        // 而缺陷的长相恰恰是**少给**（实测少了 110 条，含整族 `--ds-shadow-*`）。
        return [...wantVars.keys()].every((k) => got.has(k))
      },
      {
        timeout: 30_000,
        intervals: [400, 800, 1500],
        message: '落库导出必须在 30s 内给出与"同请求 preview-css"同一套变量（少一个 = 交付物不完整）',
      },
    )
    .toBe(true)
  mark(
    attempts === 1
      ? `一次就读到完整产物（${css.length} 字符）`
      : `写后立读滞后实锤：第 ${attempts} 次才读到完整产物，前 ${attempts - 1} 次都是残缺视图`,
  )
  // G15 硬判据（2026-10-03 输入48 收紧）：上面那个轮询是**宽容**判据（轮询到完整就放行），
  // 它只能证明"最终能完整"，证不了"第一次就读得到完整" —— 而后者才是产品承诺
  // （用户新建/生成后马上点导出、下载 bundle，不该看到残缺产物）。
  // ⇒ 轮询保底 + 首次即完整的硬断言并存：**修复前这里红**（attempts>1），修复后必须 attempts==1。
  expect(
    attempts,
    '写成功后**第一次**读导出就必须完整（G15：交付物不许随「什么时候读」变化；轮询到第 N 次才完整即红）',
  ).toBe(1)
  expect(
    listReads,
    '写成功后第一次读项目清单就必须看到新增项（同一个读侧滞后，列表端点同样中招）',
  ).toBe(1)

  const elev3 = (/--ds-shadow-elevation-3:[^;]*/.exec(css) ?? ['(导出里没有 elevation-3)'])[0]
  const view = await apiData<{ count: number }>(
    page,
    `/api/design-system/projects/${created[0].id}/tokens/effective?theme=light`,
  )
  mark(
    `新项目(${created[0].code} id=${created[0].id}) 最终导出 ${css.length} 字符 / 变量 ${cssDecls(css).size} 个；` +
      `tokens/effective count=${view.count}；elevation-3 行：${elev3}`,
  )
  // crisp = 锐利**单层**：默认 soft 在 level≥3 会叠第二层（带逗号），所以"只有一个逗号分隔层 + spread 0px"
  // 是 crisp 独有的形状；7.9px/22% 则是 §A3 公式的人工核算结果（3^1.35×1.8=7.93→7.9；0.07+0.05×3=0.22）
  expect(elev3, '非默认阴影档必须真的改变导出 CSS 的阴影形状').toContain('0px 3px 7.9px 0px')
  // 每一层恰好一个颜色函数：数 `color-mix(` 而不是数逗号（逗号会撞在 color-mix 自己的参数里，实测差点把断言写错）
  expect((elev3.match(/color-mix\(/g) ?? []).length, 'crisp 是单层，不该出现 soft 的第二层').toBe(1)
  expect(elev3, 'alpha 必须按 crisp 公式（0.07+0.05×3）').toContain('22%')

  await shot(page, 's2-tune-shadow-axis')
})

test('S3 用 editorial-serif 走向导：后端令牌里真有 font.display', async ({ page }) => {
  const evidence = attachCollectors(page)
  const extra: string[] = []
  sink = { evidence, extra }
  const mark = (s: string) => extra.push(s)

  await injectRealApiKey(page)
  await page.goto(PLUGIN_ROUTE)
  await page.getByRole('tab', { name: '开始', exact: true }).click()
  await expect(page.locator('[data-start]')).toBeVisible({ timeout: 30_000 })

  await page.locator('[data-scene="admin"]').click()
  await page.getByRole('button', { name: '下一步', exact: true }).click()

  await expect(page.locator('[data-preset]').first()).toBeVisible({ timeout: 30_000 })
  // 「全部风格」里选 editorial-serif（M3 新增的衣服，推荐位不一定把它排进 Top-3）
  const all = page.getByRole('button', { name: /全部风格/, exact: false })
  if (await all.isVisible()) await all.click()
  const card = page.locator('[data-preset="editorial-serif"]')
  await expect(card, '衣柜里必须能选到 editorial-serif（§P 新增预设）').toBeVisible({ timeout: 30_000 })
  await card.click()
  await page.getByRole('button', { name: '就选它', exact: true }).click()

  // ③ 微调：确认轴控件确实带着这件预设的轴取值（editorial 被选中）
  const panel = page.locator('[data-style-axes]')
  await expect(panel).toBeVisible({ timeout: 30_000 })
  await panel.locator('summary').click()
  await expect(panel.locator('[data-axis="fontPairing"] [data-axis-value="editorial"]')).toHaveCount(1)
  await page.getByRole('button', { name: '下一步', exact: true }).click()

  await page.getByLabel('设计系统名称').fill(`E2E 风格轴 ${RUN_CODE}`)
  await page.getByRole('button', { name: '高级设置', exact: true }).click()
  await page.getByLabel('项目代码').fill(RUN_CODE)
  await page.getByRole('button', { name: '创建我的设计系统', exact: true }).click()
  await expect(page.locator('[data-wizard-done]')).toBeVisible({ timeout: 90_000 })

  // 后端回读：不是看界面说了什么，而是查库里真的产出了展示字族
  const created = (await apiData<{ code: string; id: number }[]>(page, '/api/design-system/projects')).find(
    (p) => p.code === RUN_CODE,
  )
  expect(created, `向导应创建出项目 ${RUN_CODE}`).toBeTruthy()
  const view = await apiData<{ count: number; items: { path: string; value?: string | null }[]; diagnostics: unknown[] }>(
    page,
    `/api/design-system/projects/${created!.id}/tokens/effective?theme=light`,
  )
  const display = view.items.find((t) => t.path === 'font.display')
  expect(display, 'editorial-serif 落库后必须有 font.display 令牌').toBeTruthy()
  expect(display!.value, 'font.display 要有真字族栈（不能是空值）').toMatch(/serif/i)
  expect(view.items.find((t) => t.path === 'font.sans')?.value, '正文字族仍是现状那串').toMatch(/Inter/i)
  mark(`向导创建 ${RUN_CODE} pid=${created!.id} count=${view.count} font.display=${display!.value}`)
  expect(view.diagnostics, '有效令牌视图不应带解析诊断（换轴后不该有悬空别名）').toEqual([])

  await shot(page, 's3-wizard-editorial-created')
})

/* ---------------- V 片（FR16 / AC21 视觉 QA 矩阵）----------------
 *
 * 后端单测能证明"轴改了哪些令牌"，证明不了两件事，这块专门盯它们：
 *  V1/V2 = 13 件预设（以及 5 件新预设在第二、三类场景）在真实展厅里每张都拍得到，
 *          且标题对最近底色都达到 4.5:1（M2 深色档标题恒为深字那一类缺陷的复发守卫）；
 *  V3    = 每条轴的每个非默认取值都有一张图，并且与"默认轴对照图"相比，交付 CSS 里确实有
 *          该轴负责的那批变量变了（前缀取自后端真实令牌路径，不是猜的）。
 * 预设清单、轴清单、取值清单一律来自 `GET presets` / `GET meta.styleAxes`——本文件不写死词表。
 */

const V_STAMP = Date.now().toString(36)

const QA_THEMES = [
  { code: 'light', label: '浅色' },
  { code: 'dark', label: '深色' },
] as const

const QA_SCENES = {
  admin: { label: '后台/中台', pageId: 'admin-dashboard' },
  landing: { label: '官网/落地页', pageId: 'landing-home' },
  mobile: { label: '移动端 H5', pageId: 'mobile-home' },
} as const

/** M3 新增的五件预设（名实核对用；目录本身仍从 `GET presets` 取） */
const NEW_PRESETS = ['editorial-serif', 'flat-minimal', 'warm-craft', 'tech-crisp', 'kids-playful']

/**
 * 轴 → 该轴动手后应当在交付 CSS 里看得见的变量前缀。依据是后端真实令牌路径：
 * `shadow.*` / `border.*`（ScaleGenerators）、`radius.*` + `component.*`（圆角轴决定"组件选哪一档"）、
 * `color.neutral.*`（中性色温走 NeutralRamp）、`color.accent.*` + `color.info.*`（两族都吃强调色偏移）、`font.*`。
 */
const AXIS_VAR_HINTS: Record<string, string[]> = {
  shadowStyle: ['--ds-shadow-'],
  shadowStrength: ['--ds-shadow-'],
  borderStrength: ['--ds-border-'],
  neutralTemp: ['--ds-color-neutral-'],
  radiusStyle: ['--ds-radius-', '--ds-component-'],
  accentStrategy: ['--ds-color-accent-', '--ds-color-info-'],
  fontPairing: ['--ds-font-'],
}

/** 两版声明里不一样的变量名（含只在一边出现的，如 editorial 才有的 `--ds-font-display`） */
function changedVars(base: Map<string, string>, other: Map<string, string>): string[] {
  const names = new Set([...base.keys(), ...other.keys()])
  return [...names].filter((k) => base.get(k) !== other.get(k))
}

test.describe('M3 · V 片（FR16/AC21 视觉 QA 矩阵）', () => {
  // 26 / 10 / 21 张：每张都要换装、切档、等重绘稳定，远超默认 30s
  test.describe.configure({ timeout: 900_000 })
  test.use({ viewport: { width: 2200, height: 1000 } })

  const stageEl = (page: Page) => page.locator('[data-stage]')
  const viewportEl = (page: Page) => page.locator('.ds-stage__viewport')

  const openShowroom = async (page: Page): Promise<void> => {
    await injectRealApiKey(page)
    await page.goto(`${PLUGIN_ROUTE}#/showroom`)
    await expect(page.locator('[data-showroom]')).toBeVisible({ timeout: 30_000 })
    await expect(page.locator('[data-outfit-id="preset:admin-calm"]')).toBeVisible({ timeout: 60_000 })
  }

  /**
   * 让某件衣服出现在衣柜里再点它。两个真实坑都在这里兜住：
   * ① 项目衣服按 `updatedAt` 倒序、默认只显示 `OUTFIT_LIMIT=12` 件（`outfits.ts:67`）——V3 一次建 20 件，
   *    最早的对照品不在首屏，必须点「更多」展开；
   * ② 项目清单是**异步加载**的，先查一次"看不见"就当不存在会误判（M2 闸门2 修 D14 时同族教训）。
   * 用 `expect.poll` 反复"看不到就点「更多」"，60s 内谁先就位算谁。
   */
  const ensureOutfit = async (page: Page, outfitId: string): Promise<void> => {
    const opt = page.locator(`[role="option"][data-outfit-id="${outfitId}"]`)
    await expect
      .poll(
        async () => {
          if (await opt.isVisible().catch(() => false)) return true
          const more = page.getByRole('button', { name: /^更多/ })
          if (await more.isVisible().catch(() => false)) {
            await more.click().catch(() => undefined)
          }
          return false
        },
        { timeout: 60_000, message: `衣柜里应能显示 ${outfitId}（必要时点「更多」展开项目清单）` },
      )
      .toBe(true)
  }

  const wear = async (page: Page, outfitId: string): Promise<void> => {
    await ensureOutfit(page, outfitId)
    await page.locator(`[role="option"][data-outfit-id="${outfitId}"]`).click()
    await expect(stageEl(page)).toHaveAttribute('data-outfit', outfitId, { timeout: 30_000 })
    await expect(page.locator('[data-stage] [data-mq-page]')).toHaveCount(1, { timeout: 30_000 })
    await expect(page.locator('[data-stage] [data-mq-wear]').first()).toBeVisible({ timeout: 30_000 })
  }

  const pickScene = async (page: Page, scene: { label: string; pageId: string }): Promise<void> => {
    await stageEl(page)
      .locator('[role="tablist"][aria-label="场景"]')
      .getByRole('tab', { name: scene.label, exact: true })
      .click()
    await expect(stageEl(page)).toHaveAttribute('data-page', scene.pageId, { timeout: 30_000 })
  }

  /** 切明暗并等画布真的重绘（闸门2 实测：只断言 `data-theme` 会拍到上一档旧色） */
  const pickTheme = async (page: Page, theme: { code: string; label: string }, name: string): Promise<void> => {
    await stageEl(page)
      .locator('[role="radiogroup"][aria-label="明暗"]')
      .getByRole('radio', { name: theme.label, exact: true })
      .click()
    await expect(stageEl(page)).toHaveAttribute('data-theme', theme.code, { timeout: 30_000 })
    await expect
      .poll(
        async () => {
          const c = await canvasBg(page)
          if (c === 'rgba(0, 0, 0, 0)') return 'transparent'
          return relLuminance(c) < 0.5 ? 'dark' : 'light'
        },
        { timeout: 30_000, message: `${name} 画布底色必须已切到 ${theme.code}（防拍到上一档）` },
      )
      .toBe(theme.code)
    await page.evaluate(() => new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(() => r(null)))))
  }

  /**
   * 两档文字对比度（与 D1 同一口径，覆盖"有的模特页没有页标题"的情况）：
   * ① 页标题 / hero 标题 对**画布底**；② 卡片标题 对**卡片底**。取元素本身再 `querySelector`
   * （不用 locator 等类名）：`mobile-home` 只有 `.mq-card__title`，locator 白等 30s 只是空转。
   */
  const textContrast = async (
    page: Page,
  ): Promise<{ bg: string; title: string; titleRatio: number; cardTitle: string; cardBg: string; cardRatio: number }> => {
    const bg = await canvasBg(page)
    const m = await page
      .locator('[data-mq-page]')
      .first()
      .evaluate((el) => {
        const t = el.querySelector<HTMLElement>('.mq-page__title, .mq-hero__title')
        const card = el.querySelector<HTMLElement>('.mq-card')
        const cardTitle = el.querySelector<HTMLElement>('.mq-card__title')
        return {
          title: t ? getComputedStyle(t).color : '',
          cardTitle: cardTitle ? getComputedStyle(cardTitle).color : '',
          cardBg: card ? getComputedStyle(card).backgroundColor : '',
        }
      })
    const cardRatio = m.cardTitle && m.cardBg ? contrastRatio(m.cardTitle, m.cardBg) : Number.NaN
    return {
      bg,
      title: m.title,
      titleRatio: m.title ? contrastRatio(m.title, bg) : Number.NaN,
      cardTitle: m.cardTitle,
      cardBg: m.cardBg,
      cardRatio,
    }
  }

  /** 拍一张并把两档对比度记进 annotations（读图时要能在报告里对上号） */
  const shoot = async (page: Page, name: string, low: string[]): Promise<void> => {
    await shotOf(viewportEl(page), name)
    const c = await textContrast(page)
    const r1 = Number.isNaN(c.titleRatio) ? 'n/a' : c.titleRatio.toFixed(2)
    const r2 = Number.isNaN(c.cardRatio) ? 'n/a' : c.cardRatio.toFixed(2)
    test.info().annotations.push({
      type: name,
      description: `页标题色=${c.title || 'n/a'} 底=${c.bg} 对比=${r1}｜卡标题色=${c.cardTitle || 'n/a'} 卡底=${c.cardBg || 'n/a'} 对比=${r2}`,
    })
    if (!Number.isNaN(c.titleRatio) && c.titleRatio < 4.5) low.push(`${name} 页标题=${r1}`)
    if (!Number.isNaN(c.cardRatio) && c.cardRatio < 4.5) low.push(`${name} 卡标题=${r2}`)
  }

  test('V1 13 预设 × 后台/中台 × 明暗（26 张）', async ({ page }) => {
    const evidence = attachCollectors(page)
    const extra: string[] = []
    sink = { evidence, extra }
    const mark = (s: string) => extra.push(s)

    await openShowroom(page)
    const presets = await apiData<{ id: string; name?: string }[]>(page, '/api/design-system/presets')
    expect(presets.length, '预设目录必须真的是 13 件（M3 8→13）').toBe(13)
    expect(presets.filter((p) => NEW_PRESETS.includes(p.id)).length, '五件新预设必须在目录里').toBe(NEW_PRESETS.length)

    await pickScene(page, QA_SCENES.admin)
    const low: string[] = []
    let shots = 0
    for (const p of presets) {
      await wear(page, `preset:${p.id}`)
      for (const theme of QA_THEMES) {
        const name = `m3v1-${p.id}-admin-${theme.code}`
        await pickTheme(page, theme, name)
        await shoot(page, name, low)
        // 拍完再核"舞台装的确实是这件衣服这一档的交付 CSS"（防把上一件/上一档的颜色记进这张图的证据行）
        const css = await outfitCssText(page, `preset:${p.id}`, await restCssForPreset(page, p.id, theme.code))
        mark(`${name}：注入 CSS ${css.length} 字符（== preview-css 交付文本，声明行逐条相等）；卡片阴影=${await cardShadow(page)}`)
        shots++
      }
    }
    mark(`V1 完成：${shots} 张（${presets.length} 预设 × 2 明暗 × 1 场景）`)
    expect(shots, '截图张数必须等于预设数 × 明暗数').toBe(presets.length * QA_THEMES.length)
    expect(low, '标题对最近底色低于 4.5:1 的档位').toEqual([])
  })

  test('V2 5 件新预设 × 官网落地页 / 移动端 H5 × 明（10 张）', async ({ page }) => {
    const evidence = attachCollectors(page)
    const extra: string[] = []
    sink = { evidence, extra }
    const mark = (s: string) => extra.push(s)

    await openShowroom(page)
    const low: string[] = []
    const light = QA_THEMES[0] as { code: string; label: string }
    let shots = 0
    for (const id of NEW_PRESETS) {
      await wear(page, `preset:${id}`)
      for (const scene of [QA_SCENES.landing, QA_SCENES.mobile]) {
        await pickScene(page, scene)
        const name = `m3v2-${id}-${scene.pageId}-light`
        await pickTheme(page, light, name)
        await shoot(page, name, low)
        mark(`${name}：wear=${await page.locator('[data-stage] [data-mq-wear]').count()} 底色=${await canvasBg(page)}`)
        shots++
      }
    }
    mark(`V2 完成：${shots} 张（${NEW_PRESETS.length} 新预设 × 2 场景 × 明）`)
    expect(shots).toBe(NEW_PRESETS.length * 2)
    expect(low, '标题对最近底色低于 4.5:1 的档位').toEqual([])
  })

  test('V3 每条轴的每个非默认取值：一张图 + 与默认档对照的变量差', async ({ page }) => {
    const evidence = attachCollectors(page)
    const extra: string[] = []
    sink = { evidence, extra }
    const mark = (s: string) => extra.push(s)

    // 先落到真实页面再取 meta：`apiData` 借的是文档里的 localStorage，
    // 在 about:blank 上读它会 SecurityError（V3 第一版就是这么红的）
    await openShowroom(page)
    const axes = await metaAxes(page)
    expect(axes.length, 'meta.styleAxes 必须是 7 条轴').toBe(7)

    /**
     * 建一件"只改这条轴"的项目衣服：其余参数与对照件逐字相同，差异只有轴本身。
     * 项目 code 用序号而不是"轴名-取值"——后端 contract 是 `^[a-z0-9][a-z0-9-]{0,39}$`（**上限 40**），
     * `e2e-m3v-<时间戳>-accentstrategy-analogous` 实测被 400 拒（第一次跑就红在这）。
     * 可读名留在截图文件名与证据行里，那两处没有长度契约。
     */
    const createOutfitProject = async (seq: number, field: string, value: string | number): Promise<{ code: string; id: number }> => {
      const code = `e2e-m3v-${V_STAMP}-${seq}`
      await apiPost<unknown>(page, '/api/design-system/projects/quick-create', {
        name: `E2E 视觉矩阵 ${seq}`,
        code,
        kind: 'console',
        description: '运维监控告警平台',
        preset: 'admin-calm',
        request: field ? { [field]: value } : {},
        apply: true,
      })
      const projects = await apiData<{ code: string; id: number }[]>(page, '/api/design-system/projects')
      const created = projects.find((p) => p.code === code)
      expect(created, `quick-create 必须真的建出 ${code}`).toBeTruthy()
      return created!
    }

    /**
     * 取"舞台此刻真正装上的那份后端 CSS"的变量表：判据在 `outfitCssText` 里
     * （注入文本的**声明行必须逐条等于 `export?format=css`**，不等就是"舞台还停在上一件 / 取数失败被吞"）。
     * 为什么要这么绕：注入文本是 `scopeCssToSkin` 改写过的（`:root` → `[data-outfit=…]`），只能比声明行；
     * 而"两条轴的产物没差别"这种长相，既可能是轴没生效，也可能是舞台装了上一件——
     * 必须先让舞台文本归到这件衣服自己名下，再去和对照件比变量差（V3 第四跑红在 `changed=0` 就是没区分这两件事）。
     */
    const stageDecls = async (outfit: { code: string; id: number }): Promise<Map<string, string>> => {
      const rest = await apiText(page, `/api/design-system/projects/${outfit.id}/export?format=css&theme=light`)
      await outfitCssText(page, `project:${outfit.code}`, rest)
      return cssDecls(rest)
    }

    await injectRealApiKey(page)
    const base = await createOutfitProject(0, '', '')
    const plan: { axis: string; value: string; outfit: { code: string; id: number } }[] = []
    for (const ax of axes) {
      const values: { show: string; send: string | number }[] =
        ax.kind === 'number'
          ? [
              { show: String(ax.min ?? 0), send: Number(ax.min ?? 0) },
              { show: String(ax.max ?? 2), send: Number(ax.max ?? 2) },
            ]
          : (ax.values ?? [])
              .filter((v) => v !== String(ax.default))
              .map((v) => ({ show: v, send: v }))
      expect(values.length, `轴 ${ax.axis} 的非默认取值清单不应为空（否则这条轴没被拍）`).toBeGreaterThan(0)
      for (const v of values) {
        plan.push({
          axis: ax.axis,
          value: v.show,
          outfit: await createOutfitProject(plan.length + 1, ax.field, v.send),
        })
      }
    }
    const total = plan.length
    mark(`待拍矩阵：1 件对照（${base.code}）+ ${total} 件单轴取值 = ${total + 1} 个项目衣服`)

    // 必须**真 reload**：上面 openShowroom 已经停在 `#/showroom`，再 `goto` 同一地址只是同文档 hash 导航，
    // 应用不会重新拉项目清单 → 衣柜里只有 13 件预设、零件项目衣服（V3 第四跑就是这么红的：
    // 快照里衣柜 13 个 option 全是"预设"，顶栏项目选择器还写着「（还没有项目）」）。
    await page.reload({ waitUntil: 'load' })
    await expect(page.locator('[data-showroom]')).toBeVisible({ timeout: 30_000 })
    await expect(page.locator('[data-outfit-id="preset:admin-calm"]')).toBeVisible({ timeout: 60_000 })
    await pickScene(page, QA_SCENES.admin)
    const light = QA_THEMES[0] as { code: string; label: string }

    const baseName = 'm3v3-baseline-admin-light'
    await wear(page, `project:${base.code}`)
    await pickTheme(page, light, baseName)
    const baseDecls = await stageDecls(base)
    const low: string[] = []
    await shoot(page, baseName, low)
    mark(
      `对照件 ${baseName}：变量 ${baseDecls.size} 个（舞台注入 == 交付 CSS 已逐条核对），卡片阴影=${await cardShadow(page)}，标题字族=${await titleFamily(page)}`,
    )

    let shots = 1
    for (const item of plan) {
      await wear(page, `project:${item.outfit.code}`)
      const name = `m3v3-${item.axis}-${item.value.toLowerCase().replace(/[^a-z0-9-]/g, '')}-admin-light`
      await pickTheme(page, light, name)
      const hints = AXIS_VAR_HINTS[item.axis] ?? []
      // 先核"轴落到交付产物"（REST 权威源），再核"舞台装的就是这份产物"（stageDecls 内含）
      const restDecls = cssDecls(
        await apiText(page, `/api/design-system/projects/${item.outfit.id}/export?format=css&theme=light`),
      )
      const changed = changedVars(baseDecls, restDecls)
      const mine = changed.filter((k) => hints.some((h) => k.startsWith(h)))
      expect(changed.length, `轴 ${item.axis}=${item.value} 的交付 CSS 与对照件一个变量都没变 = 这条轴是装饰`).toBeGreaterThan(0)
      expect(
        mine,
        `轴 ${item.axis}=${item.value} 变了 ${changed.length} 个变量，但没有一个落在该轴负责的 ${hints.join('/')} 上（前 6 个变化：${changed.slice(0, 6).join(', ')}）`,
      ).not.toHaveLength(0)
      await stageDecls(item.outfit)
      await shoot(page, name, low)
      shots++
      mark(
        `${name}：变化变量 ${changed.length} 个（该轴的：${mine.slice(0, 4).join(', ')}）` +
          ` 卡片阴影=${await cardShadow(page)} 标题字族=${await titleFamily(page)}`,
      )
    }

    mark(`V3 完成：${shots} 张（1 对照 + ${total} 个非默认取值）`)
    expect(shots, '截图张数必须等于 1 张对照 + 每个非默认取值一张').toBe(total + 1)
    expect(low, '标题对画布底低于 4.5:1 的档位').toEqual([])
  })

  /**
   * V4 = 插件**自己的控制面**（`meta.styleAxes` 驱动的轴面板 + 衣柜）的视觉走查。
   * 为什么单独一条：V1–V3 拍的全是"被试穿的设计系统"（舞台画布），G4 拍的是工作台第 15 区，
   * 而 M3 新增给用户操作的那 7 条轴控件**一张图都没进过证据**——历史上"界面两处真实缺陷（#6/#7）"
   * 恰恰只有看截图才抓得到，DOM 断言全绿也照样漏。
   * 判据全部自洽、不埋阈值：① 选中态必须"aria-checked 与视觉类一致 + 计算样式与非选中真的不同"
   * （界面不许用同一种颜色假装"当前选的是这档"）；② 滑块上下限必须逐字等于 meta 的 min/max（同源）；
   * ③ 面板不得在自己的容器里横向溢出（`.ds-tune__row` 是 flex-wrap，没有设计成横向滚动条，
   * 所以 scrollWidth>clientWidth 就是裁切，不是"可以滚"）。三个视口宽度都量，1280/2200 还核右缘不越界。
   */
  test('V4 轴面板与衣柜：控制面真画出来了（选中态看得见、范围来自 meta、不横向裁切）', async ({ page }) => {
    const evidence = attachCollectors(page)
    const extra: string[] = []
    sink = { evidence, extra }
    const mark = (s: string) => extra.push(s)

    await openShowroom(page)
    await wear(page, 'preset:admin-calm')
    const axes = await metaAxes(page)
    expect(axes.filter((a) => a.kind === 'number').length, 'meta 里应有数值轴（shadowStrength），否则 ② 无从判').toBeGreaterThan(0)

    const panel = page.locator('[data-style-axes]')
    await expect(panel, '展厅必须渲染出「更多风格选项」轴面板').toBeVisible({ timeout: 30_000 })
    await panel.locator('summary').click()
    await expect(panel.locator('[data-axis]')).toHaveCount(axes.length)

    // ① 选中态看得见
    const enumAxis = axes.find((a) => a.kind === 'enum' && (a.values?.length ?? 0) > 1)
    expect(enumAxis, 'meta 里应至少有一条枚举轴').toBeTruthy()
    const group = panel.locator(`[data-axis="${enumAxis!.field}"]`)
    const checked = group.locator('[role="radio"][aria-checked="true"]')
    await expect(checked, `轴 ${enumAxis!.field} 必须恰好一枚选中态`).toHaveCount(1)
    await expect(checked, 'aria-checked 的那枚必须同时带视觉选中类（两者不一致＝界面在说假话）').toHaveClass(/ds-chip--on/)
    const unchecked = group.locator('[role="radio"][aria-checked="false"]').first()
    const styleOf = (loc: typeof checked) =>
      loc.evaluate((el) => {
        const s = getComputedStyle(el as HTMLElement)
        return `${s.backgroundColor}|${s.color}|${s.borderColor}`
      })
    const [onStyle, offStyle] = await Promise.all([styleOf(checked), styleOf(unchecked)])
    mark(`枚举轴 ${enumAxis!.field}：选中样式=${onStyle}｜非选中样式=${offStyle}`)
    expect(onStyle === offStyle, `选中态与非选中的计算样式完全相同（${onStyle}）= 用户看不出当前是哪一档`).toBe(false)

    // ①b 选中态必须指向**这件衣服的当前值**（权威源=预设目录里的 request，缺字段才回落 meta 默认）。
    //    只证"有一枚选中且看得见"是不够的：面板若永远默认选第一档，上面两条照样绿。
    //    注意 `request` 只有 **POST** presets/recommend 才带（GET presets 不带）——首跑按 GET 打这里吃了 405。
    if (!presetMatches) {
      presetMatches = await apiPost<PresetMatchLite[]>(page, '/api/design-system/presets/recommend', { limit: 999 })
    }
    const worn = presetMatches.find((p) => p.id === 'admin-calm')
    expect(worn, 'presets/recommend 必须返回 admin-calm 及其 request（选中态的唯一权威源）').toBeTruthy()
    const wornRequest = worn!.request ?? {}
    const wantEnum = String(wornRequest[enumAxis!.field] ?? enumAxis!.default)
    await expect(checked, `穿的是 admin-calm，轴 ${enumAxis!.field} 的选中档必须等于它的真实值 ${wantEnum}`).toHaveAttribute(
      'data-axis-value',
      wantEnum,
    )
    mark(
      `选中档与衣服真值同源：${enumAxis!.field}=${wantEnum}` +
        `（来源=${wornRequest[enumAxis!.field] !== undefined ? '预设 request 字段' : 'meta 默认'}）`,
    )

    // ② 数值轴：范围与当前值都来自 meta，不是界面自己写的
    const numAxis = axes.find((a) => a.kind === 'number')!
    const numRow = panel.locator(`[data-axis="${numAxis.field}"]`)
    const range = numRow.locator('input[type="range"]')
    await expect(range, `数值轴 ${numAxis.field} 必须真的画出滑块`).toBeVisible()
    const bounds = await range.evaluate((el) => ({
      min: (el as HTMLInputElement).min,
      max: (el as HTMLInputElement).max,
      step: (el as HTMLInputElement).step,
      value: (el as HTMLInputElement).value,
    }))
    expect(Number(bounds.min), `滑块下限必须等于 meta 的 min(${numAxis.min})`).toBe(numAxis.min)
    expect(Number(bounds.max), `滑块上限必须等于 meta 的 max(${numAxis.max})`).toBe(numAxis.max)
    const wantNum = String(wornRequest[numAxis.field] ?? numAxis.default)
    expect(Number(bounds.value), `滑块位置必须停在 ${numAxis.field} 的真实值 ${wantNum} 上`).toBe(Number(wantNum))
    const shownLabel = (await numRow.locator('.ds-tune__label').first().innerText()).trim()
    expect(shownLabel, `数值轴当前值必须显示在标签里（滑块值=${bounds.value}）`).toContain(bounds.value)
    mark(`数值轴 ${numAxis.field}：meta 区间 ${numAxis.min}~${numAxis.max}，滑块值=${bounds.value}（真值 ${wantNum}），界面标注「${shownLabel}」`)

    // ③ 控制面不横向裁切（三个宽度都量；≥1280 还核右缘不越出视口）
    for (const w of [2200, 1280, 1024]) {
      await page.setViewportSize({ width: w, height: 1000 })
      await page.evaluate(() => new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(() => r(null)))))
      const m = await panel.evaluate((el) => {
        const node = el as HTMLElement
        const box = node.getBoundingClientRect()
        return {
          scrollW: node.scrollWidth,
          clientW: node.clientWidth,
          right: Math.round(box.right),
          innerW: window.innerWidth,
          height: Math.round(box.height),
        }
      })
      mark(`宽度 ${w}：面板 scrollWidth=${m.scrollW} clientWidth=${m.clientW}｜右缘=${m.right}/${m.innerW}｜展开高=${m.height}`)
      expect(m.scrollW, `${w}px 下轴面板内容横向溢出自己的宽度（${m.scrollW}>${m.clientW}）＝取值芯片被裁掉`).toBeLessThanOrEqual(m.clientW + 1)
      if (w >= 1280) {
        expect(m.right, `${w}px 下轴面板右缘超出视口（${m.right}>${m.innerW}）＝控件在屏幕外点不到`).toBeLessThanOrEqual(m.innerW + 1)
      }
    }

    await page.setViewportSize({ width: 1280, height: 1000 })
    await shotOf(panel, 'm3v4-axes-panel-1280')
    await shotOf(page.locator('.ds-wardrobe'), 'm3v4-wardrobe-1280')
    await page.setViewportSize({ width: 2200, height: 1000 })
    await shotOf(panel, 'm3v4-axes-panel-2200')
    const wardrobeOptions = await page.locator('[role="option"][data-outfit-id]').count()
    mark(`V4 完成：3 张（轴面板 1280 / 轴面板 2200 / 衣柜 1280）；此刻衣柜里衣服数=${wardrobeOptions}`)
    expect(wardrobeOptions, '衣柜里必须真列出衣服（预设 13 件起步）').toBeGreaterThanOrEqual(13)
  })
})
