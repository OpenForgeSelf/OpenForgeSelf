/**
 * 模特零字面量守卫（AC12 + 03-plan §M「写法约束」的机器核对）。
 *
 * 为什么必须机械化：模特页是"设计系统的样板间"，它一旦写死一个 `#fff` 或 `12px`，
 * 换主题/换密度时这一处就不跟着走 —— 用户看到的样板间与导出交付的系统就不是同一个东西了。
 * 这种漂移在 build/test 全绿的情况下完全看不出来（`classes.test.ts` 的教训一模一样）。
 *
 * 射程 = `showroom/mannequins/**` 的 `.vue` 与 `.css`：
 * ① 禁用字面色：`#hex`、颜色函数、17 个基础命名色；
 * ② 禁长度单位字面量：`px/rem/em/ms/s/vh/vw/ch`（尺寸一律走 `var(--ds-*)`，布局用 %/无单位/auto）；
 * ③ `font-family` 与 `box-shadow` 的值必须含 `var(--ds-`；
 * ④ 每页必须是 `div.mq-page[data-mq-page="<id>"]`，且 `data-mq-wear` 元素数与种类数达 §M 下限。
 * 源码从磁盘读（与 `classes.test.ts` 同写法），并带反向探针证明守卫不空转。
 */
import path from 'node:path'
import { existsSync, readdirSync, readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'

/** `import.meta.url` 在 vitest 下可能是 `/@fs/` 形式的 http URL，两种形态都归一成磁盘路径 */
function toFsPath(url: string): string {
  const u = new URL(url)
  if (u.protocol === 'file:') return fileURLToPath(u)
  return decodeURIComponent(u.pathname).replace(/^\/@fs/, '').replace(/^\/([A-Za-z]:)/, '$1')
}

const SRC_DIR = path.resolve(path.dirname(toFsPath(import.meta.url)), '..')
const DIR = path.join(SRC_DIR, 'showroom', 'mannequins')

function walk(dir: string): string[] {
  // 目录尚未建（模特还没落地）时返回空集，让"扫不到源码"那条用例去红，而不是整个套件炸掉
  if (!existsSync(dir)) return []
  return readdirSync(dir, { withFileTypes: true }).flatMap((e) => {
    const full = path.join(dir, e.name)
    if (e.isDirectory()) return full.includes('node_modules') ? [] : walk(full)
    return e.isFile() ? [full] : []
  })
}

/** 17 个基础命名色（作为颜色值出现即违规） */
const NAMED_COLORS = new Set([
  'black', 'white', 'red', 'green', 'blue', 'yellow', 'orange', 'purple', 'pink', 'gray', 'grey',
  'brown', 'cyan', 'magenta', 'lime', 'navy', 'teal', 'olive', 'maroon', 'silver', 'aqua', 'fuchsia',
])

/** 去掉 CSS 块注释 / HTML 注释 / JS 行注释：说明文字里出现 `#fff` 是举反例，不该算违规 */
export function stripComments(source: string): string {
  return source
    .replace(/\/\*[\s\S]*?\*\//g, ' ')
    .replace(/<!--[\s\S]*?-->/g, ' ')
    .replace(/^[ \t]*\/\/.*$/gm, ' ')
}

/** 违规扫描（纯函数，反向探针直接调它） */
export function violations(source: string): string[] {
  const src = stripComments(source)
  const out: string[] = []
  if (/#[0-9a-fA-F]{3}(?:[0-9a-fA-F]{1,5})?\b/.test(src)) out.push('十六进制色值')
  if (/\b(?:rgba?|hsla?|hwb|oklch|oklab|lab|lch|color)\(/.test(src)) out.push('颜色函数')
  const units = src.match(/\b\d+(?:\.\d+)?(?:px|rem|em|ms|s|vh|vw|ch)\b/g)
  if (units) out.push(`长度/时间单位字面量：${[...new Set(units)].join(', ')}`)
  for (const m of src.matchAll(/([a-z-]+)\s*:\s*([^;{}\n]+)/g)) {
    const prop = m[1]
    const value = m[2].trim()
    if (prop === 'font-family' && !value.includes('var(--ds-')) out.push('font-family 未走 var(--ds-*)')
    if (prop === 'box-shadow' && !value.includes('var(--ds-')) out.push('box-shadow 未走 var(--ds-*)')
    for (const token of value.split(/[\s,()]+/)) {
      if (NAMED_COLORS.has(token.toLowerCase())) out.push(`命名色 ${token}（属性 ${prop}）`)
    }
  }
  return out
}

/** 数一页的 `data-mq-wear` 元素数与种类数（§M "关键元素下限" 的判据） */
export function wearCounts(source: string): { total: number; kinds: number } {
  const wears = [...source.matchAll(/data-mq-wear="([a-z]+)"/g)].map((m) => m[1])
  return { total: wears.length, kinds: new Set(wears).size }
}

/** §M 模特规格：A 片 6 页 + B 片 3 页的 wear 元素数 / 种类数下限（新增页面时在此续号） */
const MINIMUMS: Record<string, { wear: number; kinds: number }> = {
  'admin-dashboard': { wear: 14, kinds: 5 },
  'admin-list': { wear: 18, kinds: 6 },
  'admin-form': { wear: 12, kinds: 5 },
  'admin-detail': { wear: 14, kinds: 6 },
  'admin-settings': { wear: 12, kinds: 6 },
  'status-board': { wear: 14, kinds: 4 },
  'workbench-editor': { wear: 14, kinds: 6 },
  'landing-home': { wear: 16, kinds: 5 },
  'mobile-home': { wear: 12, kinds: 5 },
}

const FILES = walk(DIR)
  .filter((f) => f.endsWith('.vue') || f.endsWith('.css'))
  .map((f) => ({ rel: path.relative(SRC_DIR, f).replace(/\\/g, '/'), raw: readFileSync(f, 'utf8') }))

describe('模特零字面量与结构守卫', () => {
  it('守卫确实扫到了模特源码（否则"全绿"只是空转）', () => {
    expect(FILES.length, `未读到模特源码（DIR=${DIR}）`).toBeGreaterThanOrEqual(7)
    expect(FILES.some((f) => f.rel.endsWith('mannequin.css')), '找不到 mannequin.css').toBe(true)
  })

  it('反向探针：含字面色/尺寸字面量的片段必须被判红（守卫不空转）', () => {
    expect(violations('<div style="color: #fff"></div>')).toContain('十六进制色值')
    expect(violations('.x { background: rgb(1, 2, 3); }')).toContain('颜色函数')
    expect(violations('.x { padding: 8px var(--ds-space-2); }').join()).toContain('8px')
    expect(violations('.x { font-family: Inter, sans-serif; }')).toContain('font-family 未走 var(--ds-*)')
    expect(violations('.x { color: red; }').join()).toContain('命名色 red')
    // 正例不误杀；注释里的反例也不算违规
    expect(violations('.x { padding: var(--ds-space-2); color: var(--ds-semantic-text-1); }')).toEqual([])
    expect(violations('/* 反面例子：#fff */ .y { padding: 0; }')).toEqual([])
    expect(wearCounts('<i data-mq-wear="card"></i><i data-mq-wear="badge"></i>')).toEqual({ total: 2, kinds: 2 })
  })

  // 注意：必须传「元组数组」；直接传对象数组时 Vitest 只把整个对象当单参，回调第二个参数恒为 undefined
  it.each(FILES.map((f) => [f.rel, f.raw] as const))('%s 无字面色/尺寸字面量', (_rel, source) => {
    expect(violations(source)).toEqual([])
  })

  it.each(Object.entries(MINIMUMS))('%s 的关键元素数 ≥ 规格下限', (pageId, min) => {
    const file = FILES.find((f) => f.raw.includes(`data-mq-page="${pageId}"`))
    expect(file, `找不到 data-mq-page="${pageId}" 的模特`).toBeTruthy()
    expect(/class="[^"]*\bmq-page\b/.test(file!.raw), `${pageId} 根节点缺 .mq-page 类`).toBe(true)
    const counts = wearCounts(file!.raw)
    expect(counts.total, `${pageId} 的 data-mq-wear 元素数不足`).toBeGreaterThanOrEqual(min.wear)
    expect(counts.kinds, `${pageId} 的 data-mq-wear 种类数不足`).toBeGreaterThanOrEqual(min.kinds)
  })
})