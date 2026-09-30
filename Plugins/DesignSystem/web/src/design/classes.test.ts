/**
 * 样式类引用完整性守卫（M7 期间真实踩过的坑）。
 *
 * 背景：`BrandAssets.vue` 模板用了 `ds-btn--ghost`，而 `ds-btn` / `ds-input` 也只写在别的
 * section 的 `<style scoped>` 里 —— 构建与 vue-tsc 全绿，页面却渲染成浏览器默认控件。
 * **「build 通过」不等于「样式存在」**，所以把它机械化。
 *
 * 射程 = **共享词汇表类**（`ds-*`）：跨文件漂移、且漂了就直接掉样式的表面，必须零误报
 * （有误报的守卫早晚被人删掉）。组件私有的 `xx__yyy` BEM 类不在射程内 —— 少一条规则只是
 * “这个 div 没额外样式”，属命名洁癖级别，发现后记 TODO 清理（见 TODO.md 同名待办）。
 *
 * 只查静态 `class="..."`；`:class` 动态绑定不在射程内（那是表达式求值，属另一类检查）。
 * 源码一律从磁盘读：`.css` 走 `import.meta.glob('?raw')` 会被 vitest 的 `css:false` stub 成空串
 * （实测一片假红），`.vue` 用 glob 还额外要求 tsconfig 带 vite/client 类型 —— 都不如读盘确定。
 */
import path from 'node:path'
import { readdirSync, readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'

/**
 * `import.meta.url` 在 vitest（jsdom）下不是 `file:` 而是 `/@fs/` 形式的 http URL，
 * 直接 `fileURLToPath` 会抛 “The URL must be of scheme file”，故两种形态都归一成磁盘路径。
 */
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

/** 组件源码：路径 → 原文（相对路径用于测试名，可读且不依赖运行目录） */
const sfcSources = new Map(
  walk(SRC_DIR)
    .filter((f) => f.endsWith('.vue'))
    .map((f) => [path.relative(SRC_DIR, f).replace(/\\/g, '/'), readFileSync(f, 'utf8')]),
)

const GLOBAL_CSS = walk(path.join(SRC_DIR, 'styles'))
  .filter((f) => f.endsWith('.css'))
  .map((f) => readFileSync(f, 'utf8'))

/** `.foo` / `.foo-bar_baz` 形式的类名（伪类、组合选择器里的类都算“已定义”，宁可宽松不误杀） */
function definedClasses(css: string): Set<string> {
  return new Set([...css.matchAll(/\.(-?[a-zA-Z_][\w-]*)/g)].map((m) => m[1]))
}

const globalClasses = new Set(GLOBAL_CSS.flatMap((css) => [...definedClasses(css)]))

/** 去掉 `<script>` 与 `<style>` 块，只留模板文本 */
function templateOf(source: string): string {
  return source.replace(/<script[\s\S]*?<\/script>/g, '').replace(/<style[\s\S]*?<\/style>/g, '')
}

/** 本组件样式块里定义的类（scoped 与非 scoped 都计入：它们都对该组件生效） */
function ownClasses(source: string): Set<string> {
  return new Set([...source.matchAll(/<style[\s\S]*?<\/style>/g)].flatMap((m) => [...definedClasses(m[0])]))
}

/** 模板里用到的 `ds-*` 词汇表类 */
function vocabularyClasses(template: string): Set<string> {
  const out = new Set<string>()
  for (const m of template.matchAll(/\sclass="([^"]*)"/g)) {
    for (const token of m[1].trim().split(/\s+/)) if (token.startsWith('ds-')) out.add(token)
  }
  return out
}

function danglingClasses(source: string): string[] {
  const own = ownClasses(source)
  return [...vocabularyClasses(templateOf(source))].filter((c) => !globalClasses.has(c) && !own.has(c))
}

describe('样式类引用完整性', () => {
  const files = [...sfcSources.entries()]

  /** 守卫自身的防呆：解析不到东西就等于没在检查，必须红而不是绿 */
  it('守卫确实扫到了组件与全局样式', () => {
    expect(files.length, `未读到 .vue 源码（SRC_DIR=${SRC_DIR}）`).toBeGreaterThan(10)
    expect(GLOBAL_CSS.length, '未读到全局样式').toBeGreaterThan(1)
    expect(globalClasses.has('ds-surface'), '全局样式里找不到 ds-surface').toBe(true)
  })

  /**
   * 反例自证：本守卫必须真的能抓到漏类，否则“全绿”只是空转。
   * 下面第一条就是 M7 出过的真实缺陷形状（用了 ds-btn--ghost，全局与本地都没有定义）。
   */
  it('漏写的修饰类会被抓到（守卫不空转）', () => {
    const broken = '<template><button class="ds-btn ds-btn--ghost">x</button></template><style>.other{color:red}</style>'
    expect(danglingClasses(broken)).toEqual(['ds-btn', 'ds-btn--ghost'])
    const fixed = '<template><button class="ds-btn ds-btn--ghost">x</button></template><style>.ds-btn{color:red}.ds-btn--ghost{background:transparent}</style>'
    expect(danglingClasses(fixed)).toEqual([])
  })

  it.each(files)('%s 的 ds-* 词汇表类都有定义', (_rel, source) => {
    const dangling = danglingClasses(source)
    expect(dangling, `未定义的样式类：${dangling.join(', ')}`).toEqual([])
  })
})
