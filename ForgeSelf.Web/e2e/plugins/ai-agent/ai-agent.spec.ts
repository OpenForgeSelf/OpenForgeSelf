import { test, expect, type Page, type Response } from '@playwright/test'
import { mkdirSync, writeFileSync, readFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { injectRealApiKey } from '../../helpers/real-auth'

/**
 * 统一 e2e（插件层）样例：ai-agent 插件界面远程加载（真实后端，零 mock）。
 * 复用 globalSetup 拉起的整套环境（宿主 7102 + 前端 dev 7002，临时数据目录全新）。
 *
 * 铁律：零 mock，全部对接真实后端（vite 代理 → 7102）。本文件只验证与取证，不改业务代码。
 * 覆盖点：
 * 1. /plugin-view/ai-agent 路由下插件根节点 .agent 是否渲染
 * 2. /plugins/ai-agent/web/dist/index.js 是否 200 且 MIME 为 JS
 * 3. 三栏结构（左 .sess / 中 .chat / 右 .ctx）是否齐全（2026-09-09 布局重构：左右两栏互换——
 *    会话/Agent 列表居左对齐 ChatGPT/Claude 侧栏，AI 上下文居右对齐 Cursor 上下文面板）
 * 4. 版本徽标 .chat__version 是否显示（动态读取 plugin.json 的 version）
 * 5. 控制台是否出现 Failed to resolve component / export 缺失等致命报错
 * 6. 截图 + 读图视觉检查（重点：左上角已知问题——图标/间距/对齐/遮挡/溢出）
 *
 * 路由说明：ai-agent 的 plugin.json.frontend.route=/ai-agent，前端 dynamicPlugins 按此直接注册
 * （仅与宿主静态路由冲突才回退 /plugin-view/<id>）。本样例与 sems 一致动态读取 frontend.route，
 * 避免硬编码 /plugin-view 命名空间（实测 /plugin-view/ai-agent 在当前 app 未注册、main 为空）。
 */
/** 动态读取插件清单，避免硬编码与 plugin.json 漂移不同步。 */
const PLUGIN_MANIFEST = JSON.parse(
  readFileSync(
    fileURLToPath(new URL('../../../../ForgeSelf.Api/Plugins/AIAgent/plugin.json', import.meta.url)),
    'utf-8',
  ).replace(/^\uFEFF/, ''),
) as { version?: string; Version?: string; frontend: { route: string } }
/** 路由取 plugin.json.frontend.route（ai-agent=/ai-agent）；前端 dynamicPlugins 按此直接注册，仅与静态路由冲突才回退 /plugin-view/<id>。 */
const PLUGIN_ROUTE = PLUGIN_MANIFEST.frontend.route
/** 兼容 plugin.json 版本键大小写差异：sems 用 `version`，ai-agent 用 `Version`（PascalCase）。 */
const EXPECTED_VERSION = `v${PLUGIN_MANIFEST.version ?? PLUGIN_MANIFEST.Version ?? ''}`

/** 远程入口 JS（清单 entry=web/dist/index.js）。 */
const ENTRY_RE = /\/plugins\/ai-agent\/web\/dist\/index\.js(\?|\s|$)/

/** 取证产物目录（截图 + 网络/控制台日志）。 */
const OUT_DIR = path.resolve(fileURLToPath(new URL('../../../screenshots/e2e/ai-agent', import.meta.url)))

interface Evidence {
  network: string[]
  consoleAll: string[]
  consoleErrors: string[]
  /** 服务端 5xx 响应（真实后端报错，供子代理追查根因）。 */
  serverErrors: string[]
}

function attachCollectors(page: Page): Evidence {
  const evidence: Evidence = { network: [], consoleAll: [], consoleErrors: [], serverErrors: [] }
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
    if (ENTRY_RE.test(url)) {
      evidence.network.push(
        `${resp.status()} ${resp.request().method()} ${url} ct=${resp.headers()['content-type'] ?? '-'}`,
      )
    }
    if (resp.status() >= 500) {
      evidence.serverErrors.push(
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
    '--- 服务端 5xx 响应（真实后端报错）---',
    ...(evidence.serverErrors.length ? evidence.serverErrors : ['(无)']),
    '',
    '--- 附加信息（含左上角诊断）---',
    ...extra,
    '',
    '--- 控制台全量输出 ---',
    ...(evidence.consoleAll.length ? evidence.consoleAll : ['(无)']),
  ].join('\n')
  writeFileSync(file, body, 'utf8')
  console.log(`\n[evidence] ${file}\n${body}\n`)
}

/** 取某元素的计算样式 + 文本摘要，用于左上角视觉诊断。 */
async function describeEl(page: Page, selector: string): Promise<string> {
  return page.evaluate((sel: string) => {
    const el = document.querySelector(sel) as Element | null
    if (!el) return `(no element: ${sel})`
    const s = getComputedStyle(el)
    const box = el.getBoundingClientRect()
    const fontEl = (el.matches('[class]') ? el : el.firstElementChild) as Element
    const fs = getComputedStyle(fontEl)
    return [
      `sel=${sel}`,
      `tag=${el.tagName}`,
      `cls=${el.className}`,
      `text=${(el.textContent ?? '').trim().slice(0, 80)}`,
      `box=x${Math.round(box.x)} y${Math.round(box.y)} w${Math.round(box.width)} h${Math.round(box.height)}`,
      `color=${s.color} bg=${s.backgroundColor} border=${s.border} display=${s.display}`,
      `font=${fs.fontSize}/${fs.fontWeight} family=${fs.fontFamily.slice(0, 40)}`,
      `padding=${s.padding} margin=${s.margin}`,
    ].join(' | ')
  }, selector)
}

test.describe('统一 e2e（插件层）：ai-agent 界面远程加载（真实后端，零 mock）', () => {
  test('远程插件界面渲染 + 入口 JS 200 + 三栏 + 版本徽标 + 左上角诊断', async ({ page }) => {
    mkdirSync(OUT_DIR, { recursive: true })
    const evidence = attachCollectors(page)

    await injectRealApiKey(page)
    await page.goto(PLUGIN_ROUTE)

    // 三态之一必须出现：插件根节点 / 加载失败占位 / 加载中占位
    const pluginRoot = page.locator('.agent')
    const errorPanel = page.locator('.plugin-view-state--error')
    const loadingPanel = page.locator('.plugin-view-state--loading')

    await expect(pluginRoot.or(errorPanel).or(loadingPanel)).toBeVisible({ timeout: 30000 })
    await expect(pluginRoot.or(errorPanel)).toBeVisible({ timeout: 30000 })

    const rendered = (await pluginRoot.count()) > 0
    const errorText = rendered ? '(未渲染错误占位)' : await errorPanel.innerText()

    await page.screenshot({ path: path.join(OUT_DIR, 'ai-agent.png'), fullPage: true })

    // ---- 左上角专项诊断（已知问题区域）----
    // 整页顶部截图无法聚焦，单独裁出左上角矩形，并 dump 全局顶栏与左栏头部结构，供子代理读图定位。
    await page.screenshot({
      path: path.join(OUT_DIR, 'ai-agent-topleft.png'),
      clip: { x: 0, y: 0, width: 560, height: 460 },
    })
    const topLeftReport: string[] = []
    // 全局顶栏（若有）：nav/header
    for (const sel of ['header', '.app-header', '.navbar', 'nav.el-menu']) {
      const exists = await page.evaluate((s) => !!document.querySelector(s), sel)
      if (exists) topLeftReport.push(`[顶栏] ${await describeEl(page, sel)}`)
    }
    // 左栏（布局重构后 = SessionPanel .sess，会话与统计）头部
    if (await page.evaluate(() => !!document.querySelector('.sess'))) {
      topLeftReport.push(`[左栏] ${await describeEl(page, '.sess')}`)
      const sessHeader = '.sess__head, .sess .panel-header, .sess h3, .sess .card-header'
      if (await page.evaluate((s) => !!document.querySelector(s), sessHeader)) {
        topLeftReport.push(`[左栏头部] ${await describeEl(page, sessHeader)}`)
      }
    }
    // 版本徽标（位于中栏顶部，常是「左上角」视觉焦点）
    if (await page.evaluate(() => !!document.querySelector('.chat__version'))) {
      topLeftReport.push(`[版本徽标] ${await describeEl(page, '.chat__version')}`)
    }

    const rootStyle = await page.evaluate(() => {
      const el = document.querySelector('.agent')
      if (!el) return '(no plugin root)'
      const s = getComputedStyle(el as Element)
      return `background=${s.backgroundColor} color=${s.color}`
    })

    const extra = [
      `渲染状态: ${rendered ? '插件根节点已渲染' : '渲染失败占位'}`,
      `错误占位内容: ${errorText.replace(/\n/g, ' | ')}`,
      `插件根节点计算样式: ${rootStyle}`,
      ...topLeftReport,
    ]
    dumpEvidence('ai-agent', evidence, extra)

    // ---- 断言 1：插件界面根节点真的渲染出来 ----
    expect(rendered, `插件界面未渲染，错误占位内容：${errorText}`).toBe(true)

    // ---- 断言 2：远程入口 JS 200 且 MIME 为 JS ----
    const entryHit = await expect
      .poll(() => evidence.network.find((l) => ENTRY_RE.test(l)) ?? '', {
        message: '未捕获到 /plugins/ai-agent/web/dist/index.js 请求',
        timeout: 15000,
      })
      .toMatch(/^200 /)
      .then(() => evidence.network.find((l) => ENTRY_RE.test(l)) as string)
    expect(entryHit).toContain('javascript')

    // ---- 断言 3：三栏结构全部渲染（对齐设计原型的左/中/右三栏）----
    await expect(page.locator('.sess'), '左栏「会话统计」未渲染').toBeVisible({ timeout: 15000 })
    await expect(page.locator('.chat'), '中栏「聊天区」未渲染').toBeVisible({ timeout: 15000 })
    await expect(page.locator('.ctx'), '右栏「AI 上下文」未渲染').toBeVisible({ timeout: 15000 })

    // ---- 断言 4：版本徽标（动态读取 plugin.json 的 version）----
    await expect(page.locator('.chat__version')).toHaveText(EXPECTED_VERSION, { timeout: 15000 })

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

    // ---- 断言 6：ai-agent 自有 API 不得出现 5xx（验证 PluginAwareControllerActivator scope 修复）----
    // 回归保护：此前宿主原生控制器（AIModelController/ChatController）因 activator fallback 用 root provider
    // 解析 scoped 服务而 500；修复后这些端点必须返回 2xx。
    expect(
      evidence.serverErrors,
      `发现服务端 5xx（疑似 PluginAwareControllerActivator scope 问题）：\n${evidence.serverErrors.join('\n')}`,
    ).toEqual([])

    // ---- 截图读图视觉检查（常规化）----
    // 自动化代理：根节点真实渲染（有可见尺寸、非空白占位）。
    // 左上角/配色/对齐等主观质量请人工（或子代理）读取 screenshots/e2e/ai-agent/*.png 核对。
    const box = await page.locator('.agent').boundingBox()
    expect(box, '插件根节点未渲染出可见尺寸').not.toBeNull()
    expect(box && box.width > 0 && box.height > 0, '插件根节点尺寸为 0，疑似未真正渲染').toBe(true)

    await page.screenshot({ path: path.join(OUT_DIR, 'ai-agent-after.png'), fullPage: true })
  })

  test('流式聊天端点返回结构化 SSE（前端已接通插件自带后端 /api/ai-agent/chat/stream）', async ({ page }) => {
    test.setTimeout(60_000)
    mkdirSync(OUT_DIR, { recursive: true })
    await injectRealApiKey(page)
    await page.goto(PLUGIN_ROUTE)
    // 等插件根节点渲染，确认前端已加载、token 已注入 localStorage。
    await expect(page.locator('.agent')).toBeVisible({ timeout: 30000 })

    // 经页面上下文调插件流式接口（复用 localStorage 真实 token，走 vite 代理到真实宿主，零 mock）。
    // 流式读取首批事件后主动中止：真实 AI 上游的完整生成可能远超用例时限，
    // 结构断言（200 + SSE + "type" 字段）只需读到首个结构化事件即可。
    const result = await page.evaluate(async () => {
      const token = localStorage.getItem('forge_api_token')
      const controller = new AbortController()
      const res = await fetch('/api/ai-agent/chat/stream', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          ...(token ? { Authorization: `Bearer ${token}` } : {}),
        },
        body: JSON.stringify({ sessionId: `e2e-${Date.now()}`, message: '你好' }),
        signal: controller.signal,
      })
      const ct = res.headers.get('content-type') ?? ''
      let text = ''
      try {
        const reader = res.body?.getReader()
        if (reader) {
          const decoder = new TextDecoder()
          while (!text.includes('"type"') && text.length < 4096) {
            const { done, value } = await reader.read()
            if (done) break
            text += decoder.decode(value, { stream: true })
          }
        }
      } catch {
        /* 中止/断流时保留已读内容 */
      } finally {
        controller.abort()
      }
      return { status: res.status, contentType: ct, body: text.slice(0, 2000) }
    })

    dumpEvidence('ai-agent-stream', { network: [], consoleAll: [], consoleErrors: [], serverErrors: [] }, [
      `流式端点 status=${result.status} content-type=${result.contentType}`,
      `流式响应体（前 2000 字符）: ${result.body.replace(/\n/g, ' | ')}`,
    ])

    // 端点必须 200 且为 SSE，且输出结构化事件（含 "type" 字段）。
    // 不依赖真实 AI 上游：无 provider 时为 error 事件，有 provider 时为 content/tool 事件，均为结构化。
    expect(result.status, `流式端点非 200：${result.status} ${result.body}`).toBe(200)
    expect(result.contentType, `流式端点 content-type 非 SSE：${result.contentType}`).toContain('text/event-stream')
    expect(result.body, `未输出结构化 SSE 事件（缺少 "type" 字段）：${result.body}`).toContain('"type"')
  })

  test('composer 工作台选择交互（🔧工具多选 / ⚡技能 / 🤖Agent / 💠模型 + chips 所见即所发）', async ({ page }) => {
    test.setTimeout(60_000)
    mkdirSync(OUT_DIR, { recursive: true })
    const evidence = attachCollectors(page)

    await injectRealApiKey(page)
    await page.goto(PLUGIN_ROUTE)
    await expect(page.locator('.agent')).toBeVisible({ timeout: 30000 })

    // ---- 0. composer 卡片 + 操作栏五个入口 + 模型下拉 + 发送按钮 ----
    await expect(page.locator('.chat__composer'), 'composer 卡片未渲染').toBeVisible({ timeout: 15000 })
    const actionbar = page.locator('.chat__actionbar')
    await expect(actionbar).toBeVisible()
    await expect(actionbar.getByRole('button', { name: /目录/ })).toBeVisible()
    await expect(actionbar.getByRole('button', { name: /工具/ })).toBeVisible()
    await expect(actionbar.getByRole('button', { name: /技能/ })).toBeVisible()
    await expect(actionbar.getByRole('button', { name: /Agent/ })).toBeVisible()
    await expect(page.locator('.chat__model-pick'), '模型下拉应位于操作栏（发送旁）').toBeVisible()
    await expect(page.locator('.chat__send'), '发送按钮').toBeVisible()

    // ---- 1. 🔧 工具 popover：打开 → 多选 2 个 → chips + 计数徽标 ----
    await actionbar.getByRole('button', { name: /工具/ }).click()
    await expect(page.locator('.chat__pop'), '工具 popover 未打开').toBeVisible()
    const toolOpts = page.locator('.chat__pop .chat__opt')
    // 工具列表异步加载（onMounted 内 loadAgentTools 晚于 loadMeta/loadAgents）：先等首个选项渲染再计数，避免 count 竞态为 0。
    await expect(toolOpts.first(), '🔧 工具 popover 应展示真实工具列表（/api/ai-agent/chat/tools）').toBeVisible({ timeout: 15000 })
    const toolCount = await toolOpts.count()
    expect(toolCount, '🔧 工具 popover 应展示真实工具列表（/api/ai-agent/chat/tools）').toBeGreaterThan(0)
    const toolName1 = (await toolOpts.nth(0).locator('.chat__opt-name').innerText()).trim()
    const toolName2 = (await toolOpts.nth(1).locator('.chat__opt-name').innerText()).trim()
    await toolOpts.nth(0).click()
    await toolOpts.nth(1).click()
    // chips 区出现 2 个 🔧 chip（文本 = 工具名，所见即所发）
    await expect(page.locator('.chat__chip', { hasText: toolName1 })).toBeVisible()
    await expect(page.locator('.chat__chip', { hasText: toolName2 })).toBeVisible()
    await expect(actionbar.getByRole('button', { name: /工具/ }).locator('.chat__abtn-n')).toHaveText('2')
    // 再次点击操作栏按钮关闭 popover
    await actionbar.getByRole('button', { name: /工具/ }).click()
    await expect(page.locator('.chat__pop')).not.toBeVisible()

    // ---- 2. ⚡ 技能 popover：打开 → 勾选首个 → ⚡ chip ----
    await actionbar.getByRole('button', { name: /技能/ }).click()
    await expect(page.locator('.chat__pop'), '技能 popover 未打开').toBeVisible()
    const skillOpts = page.locator('.chat__pop .chat__opt')
    // 技能列表异步加载（loadAgentSkills 与 loadAgentTools 同批）：先等首个选项渲染再计数。
    await expect(skillOpts.first(), '⚡ 技能 popover 应展示技能列表（/api/skills 或 /api/project/skills）').toBeVisible({ timeout: 15000 })
    const skillCount = await skillOpts.count()
    expect(skillCount, '⚡ 技能 popover 应展示技能列表（/api/skills 或 /api/project/skills）').toBeGreaterThan(0)
    const skillName = (await skillOpts.nth(0).locator('.chat__opt-name').innerText()).trim()
    await skillOpts.nth(0).click()
    await expect(page.locator('.chat__chip', { hasText: skillName })).toBeVisible()
    await actionbar.getByRole('button', { name: /技能/ }).click()
    await expect(page.locator('.chat__pop')).not.toBeVisible()

    // ---- 3. 🤖 Agent popover：打开 → 单选（选完自动关闭）----
    await actionbar.getByRole('button', { name: /Agent/ }).click()
    await expect(page.locator('.chat__pop'), 'Agent popover 未打开').toBeVisible()
    const agentOpts = page.locator('.chat__pop .chat__opt')
    // Agent 列表异步加载（loadAgents）：先等首个选项渲染再计数。
    await expect(agentOpts.first(), '🤖 Agent popover 应展示 Agent 列表（/api/agents）').toBeVisible({ timeout: 15000 })
    expect(await agentOpts.count(), '🤖 Agent popover 应展示 Agent 列表（/api/agents）').toBeGreaterThan(0)
    await agentOpts.nth(0).click()
    await expect(page.locator('.chat__pop')).not.toBeVisible()

    // ---- 4. 💠 模型下拉：打开 → 当前模型高亮 → 切选后关闭 ----
    await page.locator('.chat__model-pick').click()
    await expect(page.locator('.chat__pop--right'), '模型 popover 未打开').toBeVisible()
    const modelOpts = page.locator('.chat__pop--right .chat__opt')
    const modelHint = page.locator('.chat__pop--right .chat__pop-hint')
    // e2e 临时环境（fresh Data 无 AI Provider 密钥）模型列表可能为空：有选项或空态提示均为合法。
    await expect(modelOpts.first().or(modelHint), '模型 popover 应展示模型列表或空态提示').toBeVisible({ timeout: 15000 })
    const modelCount = await modelOpts.count()
    if (modelCount > 0) {
      expect(
        await page.locator('.chat__pop--right .chat__opt--on').count(),
        '应恰好有一个当前选中模型高亮',
      ).toBeGreaterThanOrEqual(1)
      if (modelCount > 1) {
        await modelOpts.nth(1).click()
      } else {
        await page.keyboard.press('Escape')
      }
    } else {
      // 无模型（e2e 环境无 Provider 配置）：空态提示可见，Esc 关闭。
      await expect(modelHint).toBeVisible()
      await page.keyboard.press('Escape')
    }
    await expect(page.locator('.chat__pop--right')).not.toBeVisible()

    // ---- 5. 发送前 chips 保留（所见即所发）；截图取证 ----
    await expect(page.locator('.chat__chips'), '已选上下文 chips 区应可见').toBeVisible()
    await page.screenshot({ path: path.join(OUT_DIR, 'ai-agent-composer.png'), fullPage: true })

    // ---- 6. 无致命报错 + 无服务端 5xx ----
    const fatalPatterns = [
      /Failed to resolve component/i,
      /does not provide an export named/i,
      /Failed to (fetch|resolve) dynamically imported module/i,
      /Failed to load module script/i,
      /is not defined/i,
    ]
    const fatal = evidence.consoleErrors.filter((line) => fatalPatterns.some((re) => re.test(line)))
    expect(fatal, `控制台出现致命报错：\n${fatal.join('\n')}`).toEqual([])
    expect(
      evidence.serverErrors,
      `composer 交互期间发现服务端 5xx：\n${evidence.serverErrors.join('\n')}`,
    ).toEqual([])
  })
})
