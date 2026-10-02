import type { Locator, Page, Response } from '@playwright/test'
import { mkdirSync, writeFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

/**
 * design-system 插件 e2e 的共享工具（M2 新 spec 专用）。
 *
 * 只把「取真实后端数据 / 解 { success, data } 信封 / 落截图 / 收集证据」这几件事抽出来。
 * 既有 `design-system.spec.ts` 保持原样不动（它自带一份等价实现）——不为复用去改一份已在跑的长用例（03-plan §E）。
 */

export const PLUGIN_ID = 'design-system'

/** 截图/证据落盘目录（03-plan §E：`screenshots/e2e/design-system/m2/`） */
export const OUT_DIR = path.resolve(
  fileURLToPath(new URL('../../../screenshots/e2e/design-system/m2', import.meta.url)),
)

const ENTRY_RE = new RegExp(`/plugins/${PLUGIN_ID}/web/dist/index\\.js(\\?|\\s|$)`)
const STYLE_RE = new RegExp(`/plugins/${PLUGIN_ID}/web/dist/style\\.css(\\?|\\s|$)`)

export interface Evidence {
  network: string[]
  consoleAll: string[]
  consoleErrors: string[]
}

/** 挂网络/控制台收集器（插件产物 + 设计系统 API + 控制台 error / 未捕获异常） */
export function attachCollectors(page: Page): Evidence {
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
      evidence.network.push(
        `${resp.status()} ${resp.request().method()} ${url} ct=${resp.headers()['content-type'] ?? '-'}`,
      )
    }
  })
  return evidence
}

/**
 * 证据落盘。**不能只在用例末尾做**：中间任一步失败，网络/控制台证据就全丢，
 * 于是「请求没发 / 发了没回 / 回了没生效」三者不可区分。挂在 afterEach，无论通过与否都留一份。
 */
export function dumpEvidence(name: string, evidence: Evidence, extra: string[]): void {
  mkdirSync(OUT_DIR, { recursive: true })
  const file = path.join(OUT_DIR, `${name}.log`)
  const body = [
    `=== plugin: ${PLUGIN_ID} ===`,
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

/** 全页截图，落 OUT_DIR */
export async function shot(page: Page, name: string): Promise<void> {
  mkdirSync(OUT_DIR, { recursive: true })
  await page.screenshot({ path: path.join(OUT_DIR, `${name}.png`), fullPage: true })
}

/**
 * 元素级截图（视觉 QA 矩阵专用）。只拍某个元素（如舞台 `[data-stage]`）：
 * 图像更聚焦、体积更小，逐张读图时能看清模特页的间距/颜色/对齐（AC24）。
 */
export async function shotOf(locator: Locator, name: string): Promise<void> {
  mkdirSync(OUT_DIR, { recursive: true })
  await locator.screenshot({ path: path.join(OUT_DIR, `${name}.png`) })
}

/**
 * 借页面里的真实 token 直取 API，并解 `{ success, data }` 信封。
 * 只对 SQLite `code = Busy` 这类 500 重试（宿主并发读写的已知缺陷），其它错误原样抛出、绝不吞。
 */
export async function apiData<T>(page: Page, url: string): Promise<T> {
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
          return (body.data ?? body) as unknown
        } catch {
          throw new Error(`${res.status} 响应不是 JSON：${text.slice(0, 160)}`)
        }
      }
      last = `${res.status} ${text.slice(0, 160)}`
      if (!/code = Busy|database is locked/.test(text)) break
    }
    throw new Error(last)
  }, url) as Promise<T>
}

/** 导出类端点的**原文**（非信封），单独一条通道；同样只重试 Busy 一类 */
export async function apiText(page: Page, url: string): Promise<string> {
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

/** 写端点：Busy 一律不重试（重复落库比红屏更糟）；失败时抛后端原文，让断言直接指向原因 */
export async function apiPost<T>(page: Page, url: string, body: unknown): Promise<T> {
  return page.evaluate(
    async ({ u, b }) => {
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
    },
    { u: url, b: body },
  ) as Promise<T>
}

/** `#rgb`/`#rrggbb` → 浏览器 `getComputedStyle` 的 `rgb(...)` 写法，用于逐位比对；不可解析回 null */
export function hexToRgb(hex: string): string | null {
  const m = /^#?([0-9a-f]{2})([0-9a-f]{2})([0-9a-f]{2})$/i.exec(hex.trim())
  return m
    ? `rgb(${Number.parseInt(m[1], 16)}, ${Number.parseInt(m[2], 16)}, ${Number.parseInt(m[3], 16)})`
    : null
}