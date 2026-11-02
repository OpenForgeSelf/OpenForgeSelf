import { expect, type Page, type Response } from '@playwright/test'
import { existsSync, mkdirSync, writeFileSync, readFileSync } from 'node:fs'
import { spawnSync } from 'node:child_process'
import os from 'node:os'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

// 使用插件层 fixture：beforeEach 自动注入真实 API token 到 localStorage（零 mock）。
import { test } from '../../fixtures/e2e'
// e2e 运行快照真源（current.json → dataDir/backendUrl）：几何/语义锁种子定位 publish 实例真实 DB。
import { readCurrentRun } from '../../helpers/e2e-env'

/**
 * 文件级串行（shared publish DB 约束）：playwright.config fullyParallel=true 下同文件用例会分头并行，
 * 而几何/语义锁要向 publish 实例真实 DB 种子数据——与既有「空态」用例并发会互踩（种子联系人会让空态断言翻红）。
 * 串行不改变任何既有断言语义，只消除同文件用例的执行并发；共享 DB 的套件本就该串行。
 */
test.describe.configure({ mode: 'serial' })

/**
 * 统一 e2e（插件层）：im-gateway 配置页（真实后端，零 mock）。
 * 复用 globalSetup 拉起的整套环境（宿主 7102 + 前端 dev 7002，临时数据目录全新）。
 *
 * 铁律：零 mock，全部对接真实后端（vite 代理 → 7102）。本文件只验证与取证，不改业务代码。
 * v2.0.0：仅企业微信「智能机器人」长连接一通道（回调形态已移除）。
 * 覆盖点：
 * 1. /im-gateway 路由下插件根节点 .ig-root 是否渲染
 * 2. /plugins/im-gateway/web/dist/index.js 是否 200 且 MIME 为 JS
 * 3. 标题「IM 网关」是否显示（已知稳定文案）
 * 4. 配置接口 GET /api/im-gateway/config、GET /api/im-gateway/status 是否 200 且结构正确（单通道）
 * 5. 表单可交互（填字、开关可切）
 * 6. 控制台是否出现致命报错
 * 7. 截图 + 读图视觉检查（图标/间距/颜色/留白/对齐/遮挡/溢出）
 */
/** 动态读取插件清单：route 来自 plugin.json.frontend.route（避免硬编码命名空间歧义）。 */
const PLUGIN_MANIFEST = JSON.parse(
  readFileSync(
    fileURLToPath(new URL('../../../../Plugins/ImGateway/plugin.json', import.meta.url)),
    'utf-8',
  ),
) as { version?: string; Version?: string; frontend: { route: string } }
const PLUGIN_ROUTE = PLUGIN_MANIFEST.frontend.route
/** 清单版本键为 PascalCase「Version」（plugin.json 实际形态）；版本徽标断言用。 */
const PLUGIN_VERSION = PLUGIN_MANIFEST.Version ?? PLUGIN_MANIFEST.version ?? ''

/** 远程入口 JS（清单 entry=web/dist/index.js）。 */
const ENTRY_RE = /\/plugins\/im-gateway\/web\/dist\/index\.js(\?|\s|$)/

/** 取证产物目录（截图 + 网络/控制台日志）。 */
const OUT_DIR = path.resolve(fileURLToPath(new URL('../../../screenshots/e2e/im-gateway', import.meta.url)))

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

/** 清空配置（e2e 隔离目录，跨运行共享，必须确定性清空）。 */
async function resetConfig(page: Page): Promise<void> {
  await page.evaluate(async () => {
    const token = localStorage['forge_api_token'] as string | undefined
    await fetch('/api/im-gateway/config', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
      body: JSON.stringify({ weCom: { enabled: false, botId: '', secret: '', boundAgentId: null, boundChatModelId: null } }),
    })
  })
}

test.describe('统一 e2e（插件层）：im-gateway 配置页（真实后端，零 mock）', () => {
  test('远程插件界面渲染 + 入口 JS 200 + 配置/状态接口可用 + 表单可交互', async ({ page }) => {
    mkdirSync(OUT_DIR, { recursive: true })
    const evidence = attachCollectors(page)

    await page.goto(PLUGIN_ROUTE)

    // 三态之一必须出现：插件根节点 / 加载失败占位 / 加载中占位
    const pluginRoot = page.locator('.ig-root')
    const errorPanel = page.locator('.plugin-view-state--error')
    const loadingPanel = page.locator('.plugin-view-state--loading')

    await expect(pluginRoot.or(errorPanel).or(loadingPanel)).toBeVisible({ timeout: 30000 })
    await expect(pluginRoot.or(errorPanel)).toBeVisible({ timeout: 30000 })

    const rendered = (await pluginRoot.count()) > 0
    const errorText = rendered ? '(未渲染错误占位)' : await errorPanel.innerText()

    await page.screenshot({ path: path.join(OUT_DIR, 'im-gateway.png'), fullPage: true })

    // 诊断：确认插件界面是否带上了宿主主题变量（取根节点计算样式，排查纯白闪屏）
    const rootStyle = await page.evaluate(() => {
      const el = document.querySelector('.ig-root')
      if (!el) return '(no plugin root)'
      const s = getComputedStyle(el as Element)
      return `background=${s.backgroundColor} color=${s.color}`
    })

    const extra = [
      `渲染状态: ${rendered ? '插件根节点已渲染' : '渲染失败占位'}`,
      `错误占位内容: ${errorText.replace(/\n/g, ' | ')}`,
      `插件根节点计算样式: ${rootStyle}`,
    ]
    dumpEvidence('im-gateway', evidence, extra)

    // ---- 断言 1：插件界面根节点真的渲染出来 ----
    expect(rendered, `插件界面未渲染，错误占位内容：${errorText}`).toBe(true)

    // ---- 断言 2：远程入口 JS 200 且 MIME 为 JS ----
    const entryHit = await expect
      .poll(() => evidence.network.find((l) => ENTRY_RE.test(l)) ?? '', {
        message: '未捕获到 /plugins/im-gateway/web/dist/index.js 请求',
        timeout: 15000,
      })
      .toMatch(/^200 /)
      .then(() => evidence.network.find((l) => ENTRY_RE.test(l)) as string)
    expect(entryHit).toContain('javascript')

    // ---- 断言 3：标题稳定文案 ----
    await expect(page.locator('.ig-title')).toHaveText('IM 网关', { timeout: 15000 })

    // 起始清状态：回填空配置
    await resetConfig(page)

    // ---- 断言 4：配置接口真实可用（单通道结构）----
    const configResp = await page.evaluate(async () => {
      const token = localStorage['forge_api_token'] as string | undefined
      const r = await fetch('/api/im-gateway/config', {
        cache: 'no-store',
        headers: { Accept: 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
      })
      return { status: r.status, body: await r.json().catch(() => null) }
    })
    expect(configResp.status, `GET /api/im-gateway/config 非 200：status=${configResp.status}`).toBe(200)
    expect(configResp.body?.weCom, 'config.weCom 应存在（v2.0.0 单通道结构）').toBeTruthy()

    // ---- 断言 4b：保存往返（验证 WeCom 单通道字段还原）----
    // 用占位符且 enabled=false：企微是长连接形态，若填真凭据并启用会真的外连企微、
    // 抢占该机器人唯一的长连接（踢掉使用者别处的连接），故 e2e 只验证序列化往返，不建连。
    const wecomBotId = 'aib_e2e_roundtrip_bot_id'
    const saveResp = await page.evaluate(async (botId) => {
      const token = localStorage['forge_api_token'] as string | undefined
      const payload = {
        weCom: {
          enabled: false,
          boundAgentId: null,
          boundChatModelId: null,
          botId,
          secret: 'e2e_placeholder_long_connection_secret',
        },
      }
      const r = await fetch('/api/im-gateway/config', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Accept: 'application/json',
          ...(token ? { Authorization: `Bearer ${token}` } : {}),
        },
        body: JSON.stringify(payload),
      })
      return { status: r.status, body: await r.json().catch(() => null) }
    }, wecomBotId)
    expect(saveResp.status, `POST /api/im-gateway/config 非 200：status=${saveResp.status}`).toBe(200)

    const reloadResp = await page.evaluate(async () => {
      const token = localStorage['forge_api_token'] as string | undefined
      const r = await fetch('/api/im-gateway/config', {
        cache: 'no-store',
        headers: { Accept: 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
      })
      return { status: r.status, body: await r.json().catch(() => null) }
    })
    // 诊断：把 save/reload 实际返回体落入证据，便于根因定位
    extra.push(`[诊断] save 返回体: ${JSON.stringify(saveResp.body)}`)
    extra.push(`[诊断] reload 返回体: ${JSON.stringify(reloadResp.body)}`)
    const reloadedWeCom = reloadResp.body?.weCom as { botId?: string } | undefined
    expect(reloadedWeCom, `保存后应能读回 weCom 配置；reload 体=${JSON.stringify(reloadResp.body)}`).toBeTruthy()
    expect(reloadedWeCom?.botId, '读回的 BotId 应还原').toBe(wecomBotId)

    // 清理测试数据：回填空配置
    await resetConfig(page)

    // ---- 断言 4c：状态接口（v2.0.0 仅 1 个长连接通道）----
    const statusResp = await page.evaluate(async () => {
      const token = localStorage['forge_api_token'] as string | undefined
      const r = await fetch('/api/im-gateway/status', {
        cache: 'no-store',
        headers: { Accept: 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
      })
      return { status: r.status, body: await r.json().catch(() => null) }
    })
    expect(statusResp.status, `GET /api/im-gateway/status 非 200：status=${statusResp.status}`).toBe(200)
    const statuses = (statusResp.body as Array<{ type?: string; transport?: string; connection?: { state?: string } }> | undefined) ?? []
    expect(statuses.length, `状态通道数应为 1（v2.0.0 仅企微长连接），实际 ${statuses.length}`).toBe(1)

    // 形态断言：唯一通道必须是 websocket（回调形态已整体移除）。
    const wecomStatus = statuses.find((s) => s.type === 'wecom')
    expect(wecomStatus, '状态里应包含 wecom 通道').toBeTruthy()
    expect(wecomStatus?.transport, '企业微信应为长连接（websocket）形态').toBe('websocket')
    expect(wecomStatus?.connection?.state, '长连接通道应上报连接状态').toBeTruthy()

    // ---- 断言 5：表单可交互（填字 + 开关切换）----
    // v2.1.0（PILOT-078）起默认视图=会话视图，配置表单迁入 .ig-config（v-show 隐藏）：
    // 先点 header「配置」切进配置视图，再断言既有表单可交互（AC-5：切换后既有功能全在）。
    await page.locator('.ig-header__config').click()
    await expect(page.locator('.ig-config')).toBeVisible({ timeout: 15000 })
    const botIdInput = page.locator('.ig-field input').first()
    await expect(botIdInput).toBeVisible({ timeout: 15000 })
    await botIdInput.fill('aib_e2e_placeholder_bot_id')
    await expect(botIdInput).toHaveValue('aib_e2e_placeholder_bot_id')
    const firstSwitch = page.locator('.ig-switch input').first()
    const before = await firstSwitch.isChecked()
    await firstSwitch.setChecked(!before)
    await expect(firstSwitch).toBeChecked({ checked: !before })

    // ---- 断言 6：无模块解析 / 组件解析 / 导出缺失类报错 ----
    const fatalPatterns = [
      /Failed to resolve component/i,
      /does not provide an export named/i,
      /Failed to (fetch|resolve) dynamically imported module/i,
      /Failed to load module script/i,
      /is not defined/i,
    ]
    const fatal = evidence.consoleErrors.filter((line) => fatalPatterns.some((re) => re.test(line)))
    expect(fatal, `控制台出现致命报错：\n${fatal.join('\n')}`).toEqual([])

    // ---- 截图读图视觉检查（常规化）----
    // 自动化代理：根节点真实渲染（有可见尺寸、非空白占位），作为「读图」的可断言替代；
    // 主题/配色等主观质量请人工读取 screenshots/e2e/im-gateway/*.png 核对。
    const box = await page.locator('.ig-root').boundingBox()
    expect(box, '插件根节点未渲染出可见尺寸').not.toBeNull()
    expect(box && box.width > 0 && box.height > 0, '插件根节点尺寸为 0，疑似未真正渲染').toBe(true)

    await page.screenshot({ path: path.join(OUT_DIR, 'im-gateway-after.png'), fullPage: true })
  })
})

test.describe('统一 e2e（插件层）：im-gateway 会话视图主链路（PILOT-078，真实后端，零 mock）', () => {
  /**
   * 覆盖点（02-spec AC-1 / AC-7 + 05 走查冒烟口径）：
   * 1. /im-gateway 默认会话视图：左栏 .ig-chat 渲染；全新宿主（无任何会话）→ 引导空态
   *    .ig-chat__empty 可见（「还没有 IM 用户发来消息」+「去配置」），右栏占位文案可见。
   * 2. 版本徽标 .ig-version 可见且文本 = plugin.json version（FR-5 保留）。
   * 3. header「配置」按钮切换到配置视图（.ig-config 可见），「返回会话」切回（FR-5 双视图）。
   * 4. API 契约：带 token GET /api/im-gateway/conversations = 200 且 camelCase 数组；
   *    无 token → 401（AC-7，FR-4 类级鉴权）。
   * 5. 控制台无致命报错；/api/im-gateway/* 无 5xx（全新宿主基线 = 零 5xx）。
   * 6. 截图（chats 空态 + config 视图）+ 取证日志 im-gateway-chat.log（照既有 spec 风格）。
   */
  test('会话视图主链路：引导空态 → 配置切换往返 → 版本徽标 → 无致命报错/无 5xx', async ({ page }) => {
    const gatewayResponses: string[] = []
    const consoleErrors: string[] = []
    page.on('console', (msg) => {
      if (msg.type() === 'error') consoleErrors.push(msg.text())
    })
    page.on('pageerror', (err) => consoleErrors.push(`[pageerror] ${err.message}`))
    page.on('response', (resp) => {
      const url = resp.url()
      if (url.includes('/api/im-gateway/')) {
        gatewayResponses.push(`${resp.status()} ${resp.request().method()} ${url}`)
      }
    })

    await page.goto(PLUGIN_ROUTE)
    await expect(page.locator('.ig-root')).toBeVisible()

    // ---- 1. 会话视图默认渲染 + 全新宿主引导空态（AC-1）----
    const chat = page.locator('.ig-chat')
    await expect(chat).toBeVisible()
    const empty = page.locator('.ig-chat__empty')
    await expect(empty).toBeVisible()
    await expect(page.locator('.ig-chat__empty-title')).toHaveText('还没有 IM 用户发来消息')
    await expect(page.locator('.ig-chat__empty-btn')).toHaveText('去配置')
    await expect(page.locator('.ig-chat__placeholder-title')).toContainText('通道收到消息后')

    // ---- 2. 版本徽标 = plugin.json version（FR-5）----
    const badge = page.locator('.ig-version')
    await expect(badge).toBeVisible()
    await expect(badge).toHaveText(`v${PLUGIN_VERSION}`)

    // ---- 3. 配置视图切换往返（FR-5：配置视图原样迁移）----
    await page.locator('.ig-header__config').click()
    await expect(page.locator('.ig-config')).toBeVisible()
    await expect(chat).toBeHidden()
    await page.locator('.ig-header__back').click()
    await expect(chat).toBeVisible()
    await expect(empty).toBeVisible()

    // ---- 4. API 契约：带 token 200 + camelCase；无 token 401（AC-7）----
    const api = await page.evaluate(async (): Promise<{
      withTokenStatus: number
      noTokenStatus: number
      isArray: boolean
      camelCase: boolean
      count: number
    }> => {
      const token = localStorage['forge_api_token'] as string | undefined
      const withToken = await fetch('/api/im-gateway/conversations', {
        headers: token ? { Authorization: `Bearer ${token}` } : {},
      })
      const bodyText = await withToken.text()
      const noToken = await fetch('/api/im-gateway/conversations')
      let isArray = false
      let camelCase = false
      let count = -1
      try {
        const arr = JSON.parse(bodyText) as Array<Record<string, unknown>>
        isArray = Array.isArray(arr)
        camelCase = arr.length === 0 ? true : Object.prototype.hasOwnProperty.call(arr[0], 'channelKey')
        count = arr.length
      } catch {
        // 保持默认（isArray=false/camelCase=false/count=-1）
      }
      return { withTokenStatus: withToken.status, noTokenStatus: noToken.status, isArray, camelCase, count }
    })
    expect(api.withTokenStatus, '带 token GET conversations 应 200').toBe(200)
    expect(api.noTokenStatus, '无 token GET conversations 必须 401（AC-7，FR-4）').toBe(401)
    expect(api.isArray, 'conversations 应返回 JSON 数组').toBe(true)
    expect(api.camelCase, 'conversations 条目必须 camelCase（FR-2）').toBe(true)

    // ---- 5. 无致命报错 / 无 5xx（与既有用例同一组 fatal 模式）----
    const fatalPatterns = [
      /Failed to resolve component/i,
      /does not provide an export named/i,
      /Failed to (fetch|resolve) dynamically imported module/i,
      /Failed to load module script/i,
      /is not defined/i,
    ]
    const fatal = consoleErrors.filter((line) => fatalPatterns.some((re) => re.test(line)))
    expect(fatal, `控制台出现致命报错：\n${fatal.join('\n')}`).toEqual([])

    const fiveHundred = gatewayResponses.filter((line) => /^5\d\d /.test(line))
    expect(fiveHundred, `im-gateway API 出现 5xx（基线零 5xx）：\n${fiveHundred.join('\n')}`).toEqual([])

    // ---- 6. 截图 + 取证（照既有 spec 风格落 screenshots/e2e/im-gateway/）----
    mkdirSync(OUT_DIR, { recursive: true })
    await page.screenshot({ path: path.join(OUT_DIR, 'im-gateway-chat-empty.png'), fullPage: true })

    // 切到配置视图补一张，再切回（终态落在会话视图）
    await page.locator('.ig-header__config').click()
    await expect(page.locator('.ig-config')).toBeVisible()
    await page.screenshot({ path: path.join(OUT_DIR, 'im-gateway-chat-config.png'), fullPage: true })
    await page.locator('.ig-header__back').click()
    await expect(page.locator('.ig-chat')).toBeVisible()

    const logBody = [
      `=== route: ${PLUGIN_ROUTE}（PILOT-078 会话视图主链路） ===`,
      '',
      '--- /api/im-gateway/* 响应（status method url） ---',
      ...(gatewayResponses.length ? gatewayResponses : ['(无)']),
      '',
      '--- 控制台 error / 未捕获异常 ---',
      ...(consoleErrors.length ? consoleErrors : ['(无)']),
      '',
      '--- API 契约探针（withToken/noToken/isArray/camelCase/count） ---',
      JSON.stringify(api),
      '',
    ].join('\n')
    writeFileSync(path.join(OUT_DIR, 'im-gateway-chat.log'), logBody, 'utf8')
    console.log(`\n[evidence] ${path.join(OUT_DIR, 'im-gateway-chat.log')}\n${logBody}\n`)
  })
})

test.describe('统一 e2e（插件层）：im-gateway 几何/语义锁（方向修复批 2026-10-31，真实 publish DB 种子，零 mock）', () => {
  /**
   * 两把锁（mini-task docs/ai/pilot/2026-10-31-imgateway-layout-direction-fix/mini-task.md 机器验收）：
   * - 几何锁（D1）：.ig-chat__side 补 min-width:0 后，60+ 字符 nowrap 预览不再把 300px 侧栏撑到 ~788px——
   *   压力数据（120 字符中文消息）下侧栏实测 ≤360px、主栏 > 侧栏、预览省略号生效。
   * - 语义锁（D2）：方向语义=用户 2026-10-31 拍板——AI 回复（out）在左（align-self:flex-start）、
   *   用户发来（in）在右（align-self:flex-end）；几何侧证：out 气泡中心 x < in 气泡中心 x（防纯样式断言同源自洽）。
   *
   * 种子方式（05-evidence §E4 先例）：对 e2e publish 实例真实 DB（<dataDir>/Plugins/im-gateway/ImGateway.db，
   * XCodeConfig.DbFiles 真源）直接 sqlite3 INSERT；须走 UTF-8 SQL 文件 .read 执行（CLI 参数直传中文按控制台
   * 码页乱码，§E4 实证踩坑）。会话/消息键统一 'wecom:e2e-' 哨兵前缀，用例开始与 afterAll 各清一次
   * （数据卫生：跑完两表零种子残留，对齐走查后清零惯例）。
   * 可见性说明：conversations 走 XCode 无条件查询可能命中实体缓存（≤120s 过期）——列表出现哨兵联系人
   * 用 150s 宽限重试，对「缓存开/关」两种行为都正确；messages 走带条件查询直读 DB，种子即时可见。
   */
  test.setTimeout(240_000)

  const GEOM_KEY = 'wecom:e2e-geom-side:sideuser'
  const DIR_KEY = 'wecom:e2e-geom-dir:diruser'
  const SENTINEL_LIKE = 'wecom:e2e-%'
  /** 120 个中文字符：后端 preview 截 60（FR-2），nowrap min-content ≈ 720px，构成 D1 压力源。 */
  const LONG_PREVIEW_CONTENT = '布局压力预览文本'.repeat(15)

  function stamp(minutesAgo: number): string {
    const d = new Date(Date.now() - minutesAgo * 60_000)
    const p = (n: number) => String(n).padStart(2, '0')
    return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())} ${p(d.getHours())}:${p(d.getMinutes())}:${p(d.getSeconds())}`
  }

  function publishImGatewayDbPath(): string {
    const run = readCurrentRun()
    if (!run?.dataDir) throw new Error('e2e 运行快照缺失（current.json 无 dataDir）——无法定位 publish 实例数据根，种子中止')
    const dbPath = path.join(run.dataDir, 'Plugins', 'im-gateway', 'ImGateway.db')
    if (!existsSync(dbPath)) throw new Error(`ImGateway 库不存在（宿主未建库？）：${dbPath}`)
    return dbPath
  }

  /** UTF-8 SQL 文件 + sqlite3 .read 执行（§E4 教训：CLI 参数直传中文按控制台码页入库乱码）。 */
  function runSql(label: string, sql: string): void {
    const dbPath = publishImGatewayDbPath()
    const sqlFile = path.join(process.env.TEMP ?? os.tmpdir(), `im-test-seed-${label}.sql`)
    writeFileSync(sqlFile, sql + '\n', 'utf8')
    // .read 与文件名必须在同一参数内（".read <file>"）——拆成两个参数会得到 Usage: .read FILE
    const res = spawnSync('sqlite3', [dbPath, `.read ${sqlFile}`], { encoding: 'utf8' })
    if (res.error || res.status !== 0) {
      throw new Error(
        `sqlite3 ${label} 失败：status=${res.status} error=${String(res.error)} ` +
        `stdout=${res.stdout ?? ''} stderr=${res.stderr ?? ''}`,
      )
    }
  }

  /**
   * XCode 惰性建表：实体表在宿主侧按「首次使用」创建——若某实体从未被 API 调过，表尚不存在，
   * 外部种子/清理都必须先补表（DDL 与 XCode 生成库逐字一致，取自 dev-stack 实库 sqlite_master；
   * IF NOT EXISTS 保证已建库时零副作用）。clean 与 seed 共用。
   */
  function ensureImGatewayTables(): void {
    runSql('ensure', [
      `CREATE TABLE IF NOT EXISTS ImGatewaySession(`,
      `  Id integer Primary Key AUTOINCREMENT,`,
      `  ChannelKey nvarchar(200) NOT NULL DEFAULT '' COLLATE NOCASE,`,
      `  SessionId nvarchar(64) NOT NULL DEFAULT '' COLLATE NOCASE,`,
      `  AgentId nvarchar(100) NULL COLLATE NOCASE,`,
      `  ModelId nvarchar(200) NULL COLLATE NOCASE,`,
      `  UpdatedAt datetime NULL`,
      `);`,
      `CREATE UNIQUE INDEX IF NOT EXISTS IU_ImGatewaySession_ChannelKey On ImGatewaySession (ChannelKey);`,
      `CREATE TABLE IF NOT EXISTS ImGatewayMessage(`,
      `  Id integer Primary Key AUTOINCREMENT,`,
      `  ChannelKey nvarchar(200) NOT NULL DEFAULT '' COLLATE NOCASE,`,
      `  ChannelType nvarchar(50) NOT NULL DEFAULT '' COLLATE NOCASE,`,
      `  Direction nvarchar(10) NOT NULL DEFAULT '' COLLATE NOCASE,`,
      `  Content text NOT NULL COLLATE NOCASE,`,
      `  SessionId nvarchar(64) NULL COLLATE NOCASE,`,
      `  PlatformMsgId nvarchar(100) NULL COLLATE NOCASE,`,
      `  Status nvarchar(10) NOT NULL DEFAULT 'ok' COLLATE NOCASE,`,
      `  ErrorText nvarchar(1000) NULL COLLATE NOCASE,`,
      `  CreatedAt datetime NULL`,
      `);`,
      `CREATE INDEX IF NOT EXISTS IX_ImGatewayMessage_ChannelKey_Id On ImGatewayMessage (ChannelKey, Id);`,
    ].join('\n'))
  }

  function cleanSentinelRows(): void {
    ensureImGatewayTables()
    runSql('clean', [
      `DELETE FROM ImGatewayMessage WHERE ChannelKey LIKE '${SENTINEL_LIKE}';`,
      `DELETE FROM ImGatewaySession WHERE ChannelKey LIKE '${SENTINEL_LIKE}';`,
    ].join('\n'))
  }

  function seedGeometryAndDirectionRows(): void {
    cleanSentinelRows()
    runSql('seed', [
      `INSERT INTO ImGatewaySession (ChannelKey, SessionId, AgentId, ModelId, UpdatedAt) VALUES`,
      `  ('${GEOM_KEY}', 'e2e-geom-side-1', NULL, NULL, '${stamp(1)}'),`,
      `  ('${DIR_KEY}', 'e2e-geom-dir-1', NULL, NULL, '${stamp(2)}');`,
      `INSERT INTO ImGatewayMessage (ChannelKey, ChannelType, Direction, Content, SessionId, PlatformMsgId, Status, ErrorText, CreatedAt) VALUES`,
      `  ('${GEOM_KEY}', 'wecom', 'in', '${LONG_PREVIEW_CONTENT}', 'e2e-geom-side-1', 'e2e-msg-side-1', 'ok', NULL, '${stamp(2)}'),`,
      `  ('${DIR_KEY}', 'wecom', 'in', '用户发来的消息应当显示在右侧气泡', 'e2e-geom-dir-1', 'e2e-msg-dir-in-1', 'ok', NULL, '${stamp(6)}'),`,
      `  ('${DIR_KEY}', 'wecom', 'out', 'AI 的回复应当显示在左侧气泡', 'e2e-geom-dir-1', 'e2e-msg-dir-out-1', 'ok', NULL, '${stamp(5)}');`,
    ].join('\n'))
  }

  test.afterAll(() => {
    // 数据卫生：两把锁跑完清哨兵行，publish DB 回到零种子态（对齐 §E4 走查后两表归零惯例）
    cleanSentinelRows()
  })

  test('几何锁（D1）：60+ 字符预览压力下侧栏不撑破（≤360px）且主栏占主导、预览省略生效', async ({ page }) => {
    seedGeometryAndDirectionRows()
    await page.goto(PLUGIN_ROUTE)
    await expect(page.locator('.ig-root')).toBeVisible()

    // 列表出现哨兵联系人（conversations 可能命中 ≤120s 实体缓存 → 150s 宽限重试，缓存开/关两种行为都正确）
    const geomItem = page.locator('.ig-clist__item', { hasText: 'sideuser' })
    await expect(geomItem).toBeVisible({ timeout: 150_000 })

    // 几何断言：侧栏 ≤360（修复前 ~788px 会在这一条翻红）且主栏 > 侧栏（主次归位）
    const sideBox = await page.locator('.ig-chat__side').boundingBox()
    const mainBox = await page.locator('.ig-chat__main').boundingBox()
    expect(sideBox, '侧栏应有可见盒').not.toBeNull()
    expect(mainBox, '主栏应有可见盒').not.toBeNull()
    expect(sideBox!.width, `侧栏宽 ${sideBox!.width}px 应 ≤360（min-width:0 防撑破）`).toBeLessThanOrEqual(360)
    expect(mainBox!.width, `主栏宽 ${mainBox!.width}px 应大于侧栏 ${sideBox!.width}px（主次归位）`).toBeGreaterThan(sideBox!.width)

    // 预览省略旁证：60 字符 preview 溢出被 ellipsis 截断 + nowrap（min-content 压力来源佐证）
    const preview = geomItem.locator('.ig-clist__preview')
    const previewProbe = await preview.evaluate((el) => ({
      text: el.textContent ?? '',
      clientWidth: el.clientWidth,
      scrollWidth: el.scrollWidth,
      textOverflow: getComputedStyle(el).textOverflow,
      whiteSpace: getComputedStyle(el).whiteSpace,
    }))
    expect(previewProbe.text.length, '后端 preview 契约：截 60 字符').toBe(60)
    expect(previewProbe.textOverflow, '预览应声明 text-overflow:ellipsis').toBe('ellipsis')
    expect(previewProbe.whiteSpace, '预览应 nowrap（min-content 压力来源）').toBe('nowrap')
    expect(previewProbe.scrollWidth, `预览实际溢出（sw=${previewProbe.scrollWidth} > cw=${previewProbe.clientWidth}）`).toBeGreaterThan(previewProbe.clientWidth)

    mkdirSync(OUT_DIR, { recursive: true })
    await page.screenshot({ path: path.join(OUT_DIR, 'im-gateway-geometry-lock.png'), fullPage: true })
  })

  test('语义锁（D2）：out（AI 回复）在左 / in（用户发来）在右，几何侧证 out 中心 x < in 中心 x', async ({ page }) => {
    seedGeometryAndDirectionRows()
    await page.goto(PLUGIN_ROUTE)
    await expect(page.locator('.ig-root')).toBeVisible()

    const dirItem = page.locator('.ig-clist__item', { hasText: 'diruser' })
    await expect(dirItem).toBeVisible({ timeout: 150_000 })
    await dirItem.click()

    // messages 走带条件查询（直读 DB，无实体缓存），种子即时可见
    const outRow = page.locator('.ig-msg--out').first()
    const inRow = page.locator('.ig-msg--in').first()
    await expect(outRow).toBeVisible({ timeout: 30_000 })
    await expect(inRow).toBeVisible({ timeout: 30_000 })

    // 语义断言（mini-task 目标态，方向语义=用户拍板）：out=flex-start（左）、in=flex-end（右）
    const aligns = await page.evaluate(() => ({
      out: getComputedStyle(document.querySelector('.ig-msg--out')!).alignSelf,
      in: getComputedStyle(document.querySelector('.ig-msg--in')!).alignSelf,
    }))
    expect(aligns.out, 'AI 回复（out）应 align-self:flex-start（左）').toBe('flex-start')
    expect(aligns.in, '用户发来（in）应 align-self:flex-end（右）').toBe('flex-end')

    // 几何侧证：out 气泡中心 x < in 气泡中心 x（防纯样式断言同源自洽）
    const outBox = await page.locator('.ig-msg--out .ig-msg__bubble').first().boundingBox()
    const inBox = await page.locator('.ig-msg--in .ig-msg__bubble').first().boundingBox()
    expect(outBox, 'out 气泡应有可见盒').not.toBeNull()
    expect(inBox, 'in 气泡应有可见盒').not.toBeNull()
    const outCenterX = outBox!.x + outBox!.width / 2
    const inCenterX = inBox!.x + inBox!.width / 2
    expect(outCenterX, `out 气泡中心 x=${outCenterX} 应小于 in 中心 x=${inCenterX}（out 左 / in 右）`).toBeLessThan(inCenterX)

    mkdirSync(OUT_DIR, { recursive: true })
    await page.screenshot({ path: path.join(OUT_DIR, 'im-gateway-direction-lock.png'), fullPage: true })
  })
})
