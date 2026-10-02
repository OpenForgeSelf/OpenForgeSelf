/**
 * 原生对话框守卫（AC7，v3）。
 *
 * 判据（读盘，同 classes.test.ts 写法）：
 * - `web/src` 内 `window.prompt` 与 `alert(` 命中为 0（顶栏「新建」已改为切模式 + 页内向导，不再弹原生框）；
 * - `window.confirm` 只允许出现在 `Projects.vue`、`TokenStudio.vue`（既有，e2e 依赖）；
 * - **反向探针**：往一个临时字符串里造 `window.prompt(`，守卫必须红 —— 全绿不代表守卫在空转。
 */
import path from 'node:path'
import { readdirSync, readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'

function toFsPath(url: string): string {
  const u = new URL(url)
  if (u.protocol === 'file:') return fileURLToPath(u)
  return decodeURIComponent(u.pathname).replace(/^\/@fs/, '').replace(/^\/([A-Za-z]:)/, '$1')
}

const SRC_DIR = path.resolve(path.dirname(toFsPath(import.meta.url)), '..')

function walk(dir: string): string[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap((e) => {
    const full = path.join(dir, e.name)
    if (e.isDirectory()) return full.includes('node_modules') ? [] : walk(full)
    return e.isFile() ? [full] : []
  })
}

/** 界面源码（排除单测自身）：相对路径 → 文本 */
function uiSources(): { file: string; source: string }[] {
  return walk(SRC_DIR)
    .filter((f) => /\.(ts|vue)$/.test(f) && !f.endsWith('.test.ts'))
    .map((f) => ({ file: path.relative(SRC_DIR, f).replace(/\\/g, '/'), source: readFileSync(f, 'utf8') }))
}

function hitsOf(source: string, needle: string): string[] {
  const out: string[] = []
  let idx = 0
  while ((idx = source.indexOf(needle, idx)) !== -1) {
    const line = source.slice(0, idx).split(/\r?\n/).length
    out.push(`L${line}`)
    idx += needle.length
  }
  return out
}

/** confirm 白名单：仅这两个既有文件（e2e 依赖原生确认框） */
const CONFIRM_ALLOWLIST = new Set(['sections/Projects.vue', 'sections/TokenStudio.vue'])

describe('原生对话框守卫（AC7）', () => {
  const files = uiSources()

  it('扫到了界面源码（一条没扫到 = 守卫本身失效）', () => {
    expect(files.length).toBeGreaterThan(10)
  })

  it('window.prompt 命中为 0', () => {
    const bad = files.flatMap(({ file, source }) => (hitsOf(source, 'window.prompt').length ? [`${file} ${hitsOf(source, 'window.prompt').join(',')}`] : []))
    expect(bad, `web/src 里仍在使用 window.prompt（应改为页内向导）：\n${bad.join('\n')}`).toEqual([])
  })

  it('alert( 命中为 0', () => {
    const bad = files.flatMap(({ file, source }) => (hitsOf(source, 'alert(').length ? [`${file} ${hitsOf(source, 'alert(').join(',')}`] : []))
    expect(bad, `web/src 里出现 alert(：\n${bad.join('\n')}`).toEqual([])
  })

  it('window.confirm 只出现在白名单文件', () => {
    const bad = files.flatMap(({ file, source }) => {
      const hits = hitsOf(source, 'window.confirm')
      if (!hits.length) return []
      return CONFIRM_ALLOWLIST.has(file) ? [] : [`${file} ${hits.join(',')}`]
    })
    expect(bad, `window.confirm 出现在白名单外（新模式一律用页内确认）：\n${bad.join('\n')}`).toEqual([])
  })

  it('反向探针：临时串里造 window.prompt( 守卫必须红', () => {
    const fake = 'async function f(){ const n = window.prompt("x"); }'
    const bad = hitsOf(fake, 'window.prompt')
    expect(bad.length).toBeGreaterThan(0)
  })

  it('反向探针：白名单外的 confirm 会被抓到', () => {
    const fake = '<script>if (!window.confirm("x")) return</script>'
    expect(CONFIRM_ALLOWLIST.has('anything/New.vue')).toBe(false)
    expect(hitsOf(fake, 'window.confirm').length).toBeGreaterThan(0)
  })
})
