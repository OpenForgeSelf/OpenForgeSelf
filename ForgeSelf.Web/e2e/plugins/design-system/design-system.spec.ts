import { test, expect, type Page, type Response } from '@playwright/test'
import { mkdirSync, writeFileSync, readFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { injectRealApiKey } from '../../helpers/real-auth'

/**
 * 设计插件（design-system）端到端验证 —— 真实后端，零 mock。
 *
 * 复用 globalSetup 拉起的整套环境（宿主 7102 + 前端 dev 7002，临时数据目录全新）。
 * 路由动态读取 plugin.json 的 frontend.route（勿硬编码 /plugin-view 命名空间）。
 *
 * 覆盖点：
 * 1. 插件界面根节点渲染（远程加载成功，未落到错误占位）
 * 2. /plugins/design-system/web/dist/{index.js,style.css} 200 且 MIME 正确
 * 3. 无致命控制台报错（组件解析失败 / 导出缺失 / 模块加载失败）
 * 4. 设计工作台真实交互：输入需求 → 生成 → 结果渲染（标题/信息架构/用户流程）
 * 5. 六个结果分区切换（定位与架构 / 设计语言 / 页面规格 / 验收清单 / 线框预览 / 产出文件）
 * 6. 产出文件区列出 6 个文件，且可真实触发下载（design-spec.md）
 * 7. 持久化：刷新后仍保留上次产出的设计
 * 8. 其余标签页（设计令牌 / 组件库 / 控制台 UI Kit / 官网设计稿）可渲染
 * 9. 无横向溢出（scrollWidth <= clientWidth）
 * 10. 截图取证 → screenshots/e2e/design-system/
 */

/** 动态读取插件清单，避免硬编码与 plugin.json 漂移不同步（含 BOM 兜底）。 */
const PLUGIN_MANIFEST = JSON.parse(
  readFileSync(
    fileURLToPath(new URL('../../../../Plugins/DesignSystem/plugin.json', import.meta.url)),
    'utf-8',
  ).replace(/^\uFEFF/, ''),
) as { Version?: string; version?: string; frontend: { route: string } }

const PLUGIN_ROUTE = PLUGIN_MANIFEST.frontend.route
const EXPECTED_VERSION = PLUGIN_MANIFEST.Version ?? PLUGIN_MANIFEST.version ?? ''
const PLUGIN_ID = 'design-system'

/** 远程入口 JS / 样式（清单 entry=web/dist/index.js，样式按「入口同目录 style.css」约定）。 */
// 网络证据行格式：「状态码 方法 URL ct=content-type」，URL 后还跟着 ct，故边界用 (\?|\s|$)。
const ENTRY_RE = /\/plugins\/design-system\/web\/dist\/index\.js(\?|\s|$)/
const STYLE_RE = /\/plugins\/design-system\/web\/dist\/style\.css(\?|\s|$)/

/** 取证产物目录（截图 + 网络/控制台日志）。 */
const OUT_DIR = path.resolve(
  fileURLToPath(new URL('../../../screenshots/e2e/design-system', import.meta.url)),
)

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
    if (ENTRY_RE.test(url) || STYLE_RE.test(url)) {
      evidence.network.push(
        `${resp.status()} ${resp.request().method()} ${url} ct=${resp.headers()['content-type'] ?? '-'}`,
      )
    }
  })
  return evidence
}

function dumpEvidence(name: string, evidence: Evidence, extra: string[]): void {
  mkdirSync(OUT_DIR, { recursive: true })
  const file = path.join(OUT_DIR, `${name}.log`)
  const body = [
    `=== route: ${PLUGIN_ROUTE} ===`,
    '',
    '--- 关键网络请求 ---',
    ...(evidence.network.length ? evidence.network : ['(无)']),
    '',
    '--- 控制台 error / 未捕获异常 ---',
    ...(evidence.consoleErrors.length ? evidence.consoleErrors : ['(无)']),
    '',
    '--- 附加信息 ---',
    ...extra,
    '',
    '--- 控制台全量输出 ---',
    ...(evidence.consoleAll.length ? evidence.consoleAll : ['(无)']),
  ].join('\n')
  writeFileSync(file, body, 'utf8')
  console.log(`\n[evidence] ${file}\n${body}\n`)
}

/** 工作台「生成设计系统」按钮。 */
const generateBtn = (page: Page) => page.getByRole('button', { name: '生成设计系统' })

test.describe('设计插件（design-system）· 远程加载 + 工作台真实使用', () => {
  test('插件界面渲染 + 工作台全流程 + 产出文件下载 + 刷新持久化', async ({ page }) => {
    mkdirSync(OUT_DIR, { recursive: true })
    const evidence = attachCollectors(page)
    await injectRealApiKey(page)

    await page.goto(PLUGIN_ROUTE)

    // ---- 断言 1：插件根节点渲染（未落到错误占位）----
    const pluginRoot = page.locator('.ds')
    const errorPanel = page.locator('.plugin-view-state--error')
    const loadingPanel = page.locator('.plugin-view-state--loading')
    await expect(pluginRoot.or(errorPanel).or(loadingPanel)).toBeVisible({ timeout: 30000 })
    await expect(pluginRoot.or(errorPanel)).toBeVisible({ timeout: 30000 })

    const rendered = (await pluginRoot.count()) > 0
    const errorText = rendered ? '(未渲染错误占位)' : await errorPanel.innerText()
    expect(rendered, `插件界面未渲染，错误占位内容：${errorText}`).toBe(true)

    // ---- 断言 2：远程入口 JS 与 style.css 均 200 且 MIME 正确 ----
    const entryHit = await expect
      .poll(() => evidence.network.find((l) => ENTRY_RE.test(l)) ?? '', {
        message: `未捕获到 /plugins/${PLUGIN_ID}/web/dist/index.js 请求`,
        timeout: 15000,
      })
      .toMatch(/^200 /)
      .then(() => evidence.network.find((l) => ENTRY_RE.test(l)) as string)

    const styleHit = await expect
      .poll(() => evidence.network.find((l) => STYLE_RE.test(l)) ?? '', {
        message: `未捕获到 /plugins/${PLUGIN_ID}/web/dist/style.css 请求`,
        timeout: 15000,
      })
      .toMatch(/^200 /)
      .then(() => evidence.network.find((l) => STYLE_RE.test(l)) as string)

    expect(entryHit).toContain('javascript')
    expect(styleHit).toContain('text/css')

    // ---- 断言 3：版本与清单一致（清单已升级到 1.2.1，产物同步）----
    expect(EXPECTED_VERSION).toBe('1.2.1')

    await page.screenshot({ path: path.join(OUT_DIR, '01-workspace-empty.png'), fullPage: true })

    // ---- 断言 4：工作台真实交互：填需求 → 生成 ----
    const textarea = page.getByLabel('需求描述')
    await expect(textarea, '需求描述输入框未渲染').toBeVisible({ timeout: 15000 })
    await textarea.fill('我要做一个面向 SRE 团队的《星尘控制台 E2E》，用于管理微服务集群：健康总览、P99 与吞吐指标、依赖拓扑、参数配置与发布部署。')
    await expect(generateBtn(page), '需求合法后生成按钮应可用').toBeEnabled()
    await generateBtn(page).click()

    // 结果头部应出现产品名（从需求中抽取）
    await expect(page.getByText('星尘控制台 E2E', { exact: false }).first()).toBeVisible({ timeout: 15000 })
    // 行业画像识别结果应出现在结果头部的「… · 色相 …」行（select 选项的「开发者工具」不可见，故断言头部专属的「色相」）
    await expect(page.getByText('色相', { exact: false }).first()).toBeVisible({ timeout: 15000 })
    // 概览页：品牌与生成种子（可复现）渲染
    await expect(page.getByText('品牌', { exact: false }).first()).toBeVisible()
    await expect(page.getByText('生成种子', { exact: false }).first()).toBeVisible()

    await page.screenshot({ path: path.join(OUT_DIR, '02-generated-overview.png'), fullPage: true })

    // ---- 断言 5：结果分区均可切换且内容非空 ----
    const tabNames = ['品牌与颜色', '类型与间距', '组件', '应用示例', '实时预览', '产出文件']
    for (const name of tabNames) {
      await page.getByRole('tab', { name }).click()
      const panel = page.locator('[role="tabpanel"]')
      await expect(panel, `分区「${name}」未渲染内容`).not.toBeEmpty()
      await page.screenshot({
        path: path.join(OUT_DIR, `03-tab-${name}.png`),
        fullPage: true,
      })
    }

    // ---- 断言 6：产出文件区列出 6 个文件，并真实下载 design-system.md ----
    await page.getByRole('tab', { name: '产出文件' }).click()
    for (const f of [
      'design-system.md',
      'preview.html',
      'design-system.json',
      'tokens.json',
      'tokens.css',
      'tailwind.tokens.js',
    ]) {
      await expect(page.getByText(f, { exact: true }).first(), `产出文件 ${f} 未列出`).toBeVisible({ timeout: 10000 })
    }

    const downloadPromise = page.waitForEvent('download', { timeout: 20000 })
    // 在「design-system.md」所在卡片内点下载
    const specCard = page.locator('.ds-art').filter({ hasText: 'design-system.md' }).first()
    await specCard.getByRole('button', { name: '下载' }).click()
    const download = await downloadPromise
    expect(download.suggestedFilename(), '下载文件名不符').toBe('design-system.md')
    const stream = await download.createReadStream()
    const chunks: Buffer[] = []
    for await (const chunk of stream) chunks.push(Buffer.from(chunk))
    const specContent = Buffer.concat(chunks).toString('utf8')
    expect(specContent, '下载的规格文档内容为空').toContain('# ')
    expect(specContent, '下载的规格文档缺少自检章节').toContain('设计自检')
    expect(specContent, '下载的规格文档缺少需求原文').toContain('星尘控制台 E2E')

    await page.screenshot({ path: path.join(OUT_DIR, '04-artifacts.png'), fullPage: true })

    // ---- 断言 7：刷新后设计仍保留（localStorage 持久化）----
    await page.reload()
    await expect(page.locator('.ds').first()).toBeVisible({ timeout: 30000 })
    await expect(page.getByText('星尘控制台 E2E', { exact: false }).first()).toBeVisible({ timeout: 15000 })
    await expect(page.getByText('已从本地恢复', { exact: false }).first()).toBeVisible({ timeout: 15000 })
    // 历史区出现一条记录
    await expect(page.getByText('设计历史', { exact: false }).first()).toBeVisible()

    await page.screenshot({ path: path.join(OUT_DIR, '05-after-reload.png'), fullPage: true })

    // ---- 断言 8：其余四个主标签页可渲染 ----
    for (const name of ['设计令牌', '组件库', '控制台 UI Kit', '官网设计稿']) {
      await page.getByRole('button', { name }).click()
      await expect(page.locator('.ds').first(), `主标签「${name}」渲染失败`).toBeVisible({ timeout: 15000 })
      await page.screenshot({ path: path.join(OUT_DIR, `06-main-${name}.png`), fullPage: true })
    }

    // ---- 断言 9：无横向溢出 ----
    const overflow = await page.evaluate(() => {
      const d = document.documentElement
      return { scrollWidth: d.scrollWidth, clientWidth: d.clientWidth }
    })
    expect(
      overflow.scrollWidth,
      `页面出现横向溢出：scrollWidth=${overflow.scrollWidth} > clientWidth=${overflow.clientWidth}`,
    ).toBeLessThanOrEqual(overflow.clientWidth + 1)

    // ---- 断言 10：无致命控制台报错 ----
    const fatalPatterns = [
      /Failed to resolve component/i,
      /does not provide an export named/i,
      /Failed to (fetch|resolve) dynamically imported module/i,
      /Failed to load module script/i,
      /is not defined/i,
    ]
    const fatal = evidence.consoleErrors.filter((l) => fatalPatterns.some((re) => re.test(l)))
    dumpEvidence('design-system', evidence, [
      `渲染状态: ${rendered ? '插件根节点已渲染' : '渲染失败占位'}`,
      `横向溢出检查: scrollWidth=${overflow.scrollWidth} clientWidth=${overflow.clientWidth}`,
      `下载的 design-system.md 长度: ${specContent.length}`,
    ])
    expect(fatal, `控制台出现致命报错：\n${fatal.join('\n')}`).toEqual([])
  })
})
