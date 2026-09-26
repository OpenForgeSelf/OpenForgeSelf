import { expect, type Page, type Response } from '@playwright/test'
import { mkdirSync, writeFileSync, readFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

// 使用插件层 fixture：beforeEach 自动注入真实 API token 到 localStorage（零 mock）。
import { test } from '../../fixtures/e2e'

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
) as { version: string; frontend: { route: string } }
const PLUGIN_ROUTE = PLUGIN_MANIFEST.frontend.route

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
