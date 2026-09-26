import { test, expect, type Page, type Response } from '@playwright/test'
import { mkdirSync, writeFileSync, readFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { injectRealApiKey } from './helpers/real-auth'

/**
 * spec 010（plugin-frontend-runtime）US1 端到端验证：
 * 「插件界面远程加载」在浏览器里是否真的渲染出来。
 *
 * 铁律：**零 mock**。不拦截任何请求，全部对接真实后端（Vite proxy → 7102）。
 * 本文件只负责验证与取证，不修改任何业务代码。
 *
 * 覆盖点：
 * 1. /plugin-view/ai-agent 路由下插件根节点 .aiagent-plugin 是否渲染
 * 2. /plugins/ai-agent/web/dist/index.js 与 style.css 是否 200 且 MIME 正确
 * 3. 版本徽标是否显示（动态读取 plugin.json 的 version，避免硬编码与插件升版本后漂移不同步）
 * 4. /api/ai-models?enabledOnly=true 请求是否发出（模型可为空，不判失败）
 * 5. 控制台是否出现 Failed to resolve component / import map / export 缺失等报错
 * 6. 「点击我」计数是否递增（Vue 单实例、响应式未失效）
 */

/** 清单 route=/ai-agent（动态插件路由直接取 frontend.route 注册，勿硬编码 /plugin-view 命名空间）。 */
const PLUGIN_ROUTE = '/ai-agent'

/** 版本断言动态读取插件清单，避免硬编码与 plugin.json 漂移不同步。 */
const PLUGIN_MANIFEST = JSON.parse(
  readFileSync(fileURLToPath(new URL('../../Plugins/AIAgent/plugin.json', import.meta.url)), 'utf-8')
) as { Version: string }
const EXPECTED_VERSION = `v${PLUGIN_MANIFEST.Version}`

/** 远程入口 JS（清单 entry=web/dist/index.js）。 */
// 注意：网络证据行的格式是「状态码 方法 URL ct=content-type」，
// 因此 URL 之后不是行尾，后面还跟着 " ct=..."。边界必须用 (\?|\s|$)，
// 只写 (\?|$) 会因 $ 锚定整行末尾而永远匹配不到无查询串的请求（如 style.css）。
const ENTRY_RE = /\/plugins\/ai-agent\/web\/dist\/index\.js(\?|\s|$)/
/** 按「入口同目录 style.css」约定注入的样式。 */
const STYLE_RE = /\/plugins\/ai-agent\/web\/dist\/style\.css(\?|\s|$)/
/** 模型列表接口。 */
const MODELS_RE = /\/api\/ai-models\?enabledOnly=true/
/** 前端清单接口。 */
const MANIFEST_RE = /\/api\/plugin\/frontend-manifest/

/** 取证产物目录（截图 + 网络/控制台日志）。 */
const OUT_DIR = path.resolve(fileURLToPath(new URL('../screenshots/plugin-remote-view', import.meta.url)))

/** 采集到的证据。 */
interface Evidence {
  /** 命中的关键网络请求：`状态码 方法 URL ct=content-type`。 */
  network: string[]
  /** 全部控制台输出（含 warn/info），用于人工复核。 */
  consoleAll: string[]
  /** 仅 error 级控制台输出 + 未捕获异常。 */
  consoleErrors: string[]
}

/** 挂载监听器，采集网络与控制台证据。 */
function attachCollectors(page: Page): Evidence {
  const evidence: Evidence = { network: [], consoleAll: [], consoleErrors: [] }

  page.on('console', (msg) => {
    const line = `[${msg.type()}] ${msg.text()}`
    evidence.consoleAll.push(line)
    if (msg.type() === 'error') evidence.consoleErrors.push(msg.text())
  })
  page.on('pageerror', (err) => {
    const line = `[pageerror] ${err.message}`
    evidence.consoleErrors.push(line)
    evidence.consoleAll.push(line)
  })
  page.on('response', (resp: Response) => {
    const url = resp.url()
    if (ENTRY_RE.test(url) || STYLE_RE.test(url) || MODELS_RE.test(url) || MANIFEST_RE.test(url)) {
      evidence.network.push(
        `${resp.status()} ${resp.request().method()} ${url} ct=${resp.headers()['content-type'] ?? '-'}`
      )
    }
  })

  return evidence
}

/** 把证据落盘，便于汇报时引用（不依赖测试是否通过）。 */
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
  // 同时打到测试输出，便于直接在 CI 日志里看到
  console.log(`\n[evidence] ${file}\n${body}\n`)
}

test.describe('spec 010 US1：插件界面远程加载（真实后端，零 mock）', () => {
  test('远程插件界面渲染 + 资源 200 + 版本徽标 + 响应式可用', async ({ page }) => {
    // 截图目录先建好，保证断言失败时截图与证据也能落盘
    mkdirSync(OUT_DIR, { recursive: true })
    const evidence = attachCollectors(page)

    await injectRealApiKey(page)
    await page.goto(PLUGIN_ROUTE)

    // 三态之一必须出现：插件根节点 / 加载失败占位 / 加载中占位
    // 界面已重构为「三栏完整聊天界面」（对齐设计原型），根节点 class 为 .agent
const pluginRoot = page.locator('.agent')
    const errorPanel = page.locator('.plugin-view-state--error')
    const loadingPanel = page.locator('.plugin-view-state--loading')

    await expect(pluginRoot.or(errorPanel).or(loadingPanel)).toBeVisible({ timeout: 30000 })
    // 给异步加载 + 接口调用留出收敛时间（15s 加载超时 + 接口往返）
    await expect(pluginRoot.or(errorPanel)).toBeVisible({ timeout: 30000 })

    const rendered = (await pluginRoot.count()) > 0
    const errorText = rendered ? '(未渲染错误占位)' : await errorPanel.innerText()

    await page.screenshot({ path: path.join(OUT_DIR, 'plugin-remote-view.png'), fullPage: true })

    // 诊断：列出 head 中与插件样式相关的 link，用于定位「样式是否被注入/被请求」
    const headLinks = await page.evaluate(() =>
      Array.from(document.head.querySelectorAll('link[data-plugin-style]')).map(
        (el) => `${(el as HTMLLinkElement).href} | rel=${el.getAttribute('rel')}`
      )
    )
    // 诊断：确认插件界面是否真的带上了样式（取根节点计算样式）
    const pluginBg = await page.evaluate(() => {
      const el = document.querySelector('.agent')
      if (!el) return '(no plugin root)'
      const s = getComputedStyle(el as Element)
      return `display=${s.display} color=${s.color}`
    })

    const extra = [
      `渲染状态: ${rendered ? '插件根节点已渲染' : '渲染失败占位'}`,
      `错误占位内容: ${errorText.replace(/\n/g, ' | ')}`,
      `插件样式 link: ${headLinks.length ? headLinks.join(' ;; ') : '(head 中无 data-plugin-style link)'}`,
      `插件根节点计算样式: ${pluginBg}`,
    ]
    dumpEvidence('plugin-remote-view', evidence, extra)

    // ---- 断言 1：插件界面根节点真的渲染出来 ----
    expect(rendered, `插件界面未渲染，错误占位内容：${errorText}`).toBe(true)

    // ---- 断言 2：远程入口 JS 与 style.css 均 200 且 MIME 为 JS/CSS ----
    // 说明：资源请求是浏览器异步发起的（样式由加载器插入 <link> 后触发），
    // 断言时可能早于请求完成 —— 用 poll 等待收敛，断言语义不变（仍要求真实捕获到 200 请求）。
    const entryHit = await expect
      .poll(() => evidence.network.find((l) => ENTRY_RE.test(l)) ?? '', {
        message: '未捕获到 /plugins/ai-agent/web/dist/index.js 请求',
        timeout: 15000,
      })
      .toMatch(/^200 /)
      .then(() => evidence.network.find((l) => ENTRY_RE.test(l)) as string)

    const styleHit = await expect
      .poll(() => evidence.network.find((l) => STYLE_RE.test(l)) ?? '', {
        message: '未捕获到 /plugins/ai-agent/web/dist/style.css 请求',
        timeout: 15000,
      })
      .toMatch(/^200 /)
      .then(() => evidence.network.find((l) => STYLE_RE.test(l)) as string)

    expect(entryHit).toContain('javascript')
    expect(styleHit).toContain('text/css')

    // ---- 断言 3：版本徽标（动态读取 plugin.json 的 version） ----
    await expect(page.locator('.chat__version')).toHaveText(EXPECTED_VERSION)

    // ---- 断言 3b：三栏结构全部渲染（对齐设计原型的左/中/右三栏） ----
    // 这是界面重构后新增的强断言：只有三栏都在，才说明真的按设计原型落地。
    await expect(page.locator('.ctx'), '左栏「AI 上下文」未渲染').toBeVisible({ timeout: 15000 })
    await expect(page.locator('.chat'), '中栏「聊天区」未渲染').toBeVisible({ timeout: 15000 })
    await expect(page.locator('.sess'), '右栏「会话统计」未渲染').toBeVisible({ timeout: 15000 })

    // ---- 断言 4：模型接口已发出（模型可为 0 条，不算失败） ----
    // 同为异步请求，用 poll 等待收敛
    await expect
      .poll(
        () => evidence.network.some((l) => MODELS_RE.test(l) && l.startsWith('200 ')),
        { message: '未捕获到 /api/ai-models?enabledOnly=true 的 200 请求', timeout: 15000 }
      )
      .toBe(true)

    // ---- 断言 5：无模块解析 / 组件解析 / 导出缺失类报错 ----
    const fatalPatterns = [
      /Failed to resolve component/i,
      /does not provide an export named/i,
      /Failed to (fetch|resolve) dynamically imported module/i,
      /Failed to load module script/i,
      /is not defined/i,
    ]
    const fatal = evidence.consoleErrors.filter((line) => fatalPatterns.some((re) => re.test(line)))
    expect(fatal, `控制台出现致命报错：\n${fatal.join('\n')}`).toEqual([])

    // ---- 断言 6：响应式未失效（Vue 单实例）----
    // 用真实交互验证：输入框为空时发送按钮禁用，输入内容后变为启用。
    // 若 Vue 出现双实例或响应式失效，v-model 与 :disabled 均不会联动，此断言必红。
    const textarea = page.locator('.chat__textarea')
    const sendBtn = page.locator('.chat__send')
    await expect(textarea, '聊天输入框未渲染').toBeVisible({ timeout: 15000 })
    await expect(sendBtn, '初始状态下发送按钮应为禁用（输入为空）').toBeDisabled()
    await textarea.fill('这是一条用于验证响应式的测试消息')
    await expect(sendBtn, '输入内容后发送按钮应变为可用（证明响应式正常）').toBeEnabled()

    await page.screenshot({ path: path.join(OUT_DIR, 'plugin-remote-view-after-click.png'), fullPage: true })
  })
})
