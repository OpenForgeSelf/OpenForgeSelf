/**
 * 换肤层：把**后端导出的 CSS** 注入到预览容器，并把外壳读的变量名重键到用户设计系统的令牌上。
 *
 * 分工（刻意为之）：
 * - 色彩/尺度/阴影/动效的值与投影**全部由 C# 侧算出**（`export?format=css`），这里一个像素都不再计算；
 *   v1 的玩具感正是来自"前端另写一套 generate/exporters"，两套实现必然漂移。
 * - 本文件只做两件事：① 把 `:root` 作用域收窄成 `.ds-skin`（外壳中性主题不被污染）
 *   ② 外壳组件读的变量名（`--ds-fg-1`）→ 令牌名（`--ds-semantic-text-1`）的别名表。
 *
 * 于是"页面看到的"与"导出交付的"必然同源：同一份 CSS，只是换了选择器。
 */
import { cssVarName } from './derive'

/** 外壳变量 → 令牌路径。左侧是界面在读的名字，右侧是用户系统里的角色。 */
export const SKIN_ALIASES: Record<string, string> = {
  '--ds-bg': 'semantic.surface-bg',
  '--ds-surface-1': 'semantic.surface-1',
  '--ds-surface-2': 'semantic.surface-2',
  '--ds-surface-3': 'semantic.surface-3',
  '--ds-fg-1': 'semantic.text-1',
  '--ds-fg-2': 'semantic.text-2',
  '--ds-fg-3': 'semantic.text-3',
  '--ds-fg-4': 'semantic.text-3',
  '--ds-border-1': 'semantic.border-1',
  '--ds-border-2': 'semantic.border-1',
  '--ds-border-3': 'semantic.border-strong',
  '--ds-color-primary': 'semantic.brand',
  '--ds-color-primary-hover': 'semantic.brand-hover',
  '--ds-color-primary-active': 'semantic.brand-strong',
  '--ds-color-accent': 'semantic.link',
  '--ds-color-accent-hover': 'semantic.brand-hover',
  '--ds-danger': 'semantic.danger',
  '--ds-success': 'semantic.success',
  '--ds-warning': 'semantic.warning',
  '--ds-info': 'semantic.info',
  '--ds-brand-mark': 'semantic.brand',
  // 组件层是"实际用的那一个值"，命中即覆盖语义层
  '--ds-surface-1@component': 'component.card.background',
  '--ds-fg-1@component': 'component.card.foreground',
  // 尺度与排版（档名对齐后端实际生成值：duration=micro/base/macro/emphasized，ease=standard/decelerate/...）
  '--ds-dur-fast': 'duration.micro',
  '--ds-dur-base': 'duration.base',
  '--ds-dur-slow': 'duration.macro',
  '--ds-ease-standard': 'ease.standard',
  '--ds-ease-out-expo': 'ease.decelerate',
  '--ds-font-sans': 'font.sans',
  '--ds-font-mono': 'font.mono',
  '--ds-fs-display': 'size.display',
  '--ds-fs-h1': 'size.h1',
  '--ds-fs-h2': 'size.h2',
  '--ds-fs-h3': 'size.h3',
  '--ds-fs-h4': 'size.h4',
  '--ds-fs-body': 'size.body',
  '--ds-fs-small': 'size.small',
  '--ds-fs-micro': 'size.caption',
  '--ds-shadow-sm': 'shadow.elevation-1',
  '--ds-shadow-md': 'shadow.elevation-2',
  '--ds-shadow-lg': 'shadow.elevation-3',
  '--ds-shadow-xl': 'shadow.elevation-4',
}

/** 这些令牌路径与外壳变量**同名**（space.3 → --ds-space-3），直接由注入的 CSS 命中，无需别名 */
export const PASS_THROUGH_PREFIXES = ['space.', 'radius.', 'border.']

/**
 * 把导出 CSS 的选择器从全局收窄到指定容器内（默认 `.ds-skin`，保持既有行为不变）。
 * 后端产物里只有两类选择器：`:root{...}`（含 reduced-motion 媒体块内的那一个）和焦点环 `:where(...)`。
 * `scope` 允许属性选择器（如 `[data-outfit="…"]`），供展厅并排对比互不污染。
 */
export function scopeCssToSkin(css: string, scope = '.ds-skin'): string {
  if (!css.trim()) return ''
  return css
    .replace(/:root\s*\{/g, `${scope} {`)
    .replace(/:where\(([^)]*)\):focus-visible/g, (_m, list: string) => `${scope} :where(${list}):focus-visible`)
}

/**
 * 按变量名取子集：只保留 `wanted` 里**后端 CSS 真定义了**的 `--ds-*` 声明（逐字不变）。
 * 丢弃 `@media` / `@font-face` 与注释；若保留项的取值引用了 `var(--ds-x)` 而 `x` 没被保留，
 * 则沿引用链把 `x` 一并补进来 —— 防止模特页面上出现"指向未定义变量"的悬空引用。
 * 纯文本处理，不解析颜色、不换算任何值（同源纪律：画布上的值只能来自后端文本）。
 */
export function pickVars(css: string, wanted: ReadonlySet<string>): string {
  const root = /:root\s*\{([\s\S]*?)\}/.exec(css)
  if (!root) return ''
  const body = root[1].replace(/\/\*[\s\S]*?\*\//g, '')
  const decls = new Map<string, string>()
  for (const m of body.matchAll(/(--ds-[\w-]+)\s*:\s*([^;]+);/g)) {
    decls.set(m[1], `  ${m[1]}: ${m[2].trim()};`)
  }
  const keep = new Set(wanted)
  const refsOf = (decl: string): string[] => [...decl.matchAll(/var\((--ds-[\w-]+)/g)].map((m) => m[1])
  let changed = true
  while (changed) {
    changed = false
    for (const name of [...keep]) {
      const decl = decls.get(name)
      if (!decl) continue
      for (const ref of refsOf(decl)) {
        if (!keep.has(ref) && decls.has(ref)) {
          keep.add(ref)
          changed = true
        }
      }
    }
  }
  const lines = [...keep].filter((n) => decls.has(n)).map((n) => decls.get(n) as string)
  if (!lines.length) return ''
  return `:root {\n${lines.join('\n')}\n}`
}

/** 按序拼接（后者覆盖前者），用于"配色主题 CSS + 密度主题 CSS"叠加；空段跳过 */
export function composeCss(parts: readonly string[]): string {
  return parts.filter((p) => p && p.trim()).join('\n')
}

/**
 * 生成别名块：外壳读的变量名 = 令牌变量。
 *
 * @param available 后端 CSS 里**真的定义了**的令牌变量（从导出文本扫出来）；
 *                  不存在就不写这条别名 —— 指向未定义变量的 var() 会让整条声明
 *                  在 computed-value 阶段失效，表现是"页面突然全透明"，比不换肤更糟。
 */
export function buildAliasCss(available: Set<string>): string {
  const seen = new Map<string, string>()
  for (const [alias, path] of Object.entries(SKIN_ALIASES)) {
    if (!available.has(cssVarName(path))) continue
    const name = alias.replace('@component', '')
    // 组件层排在后面，天然覆盖同名的语义层别名
    seen.set(name, `  ${name}: var(${cssVarName(path)});`)
  }
  if (seen.size === 0) return ''
  return `.ds-skin {\n${[...seen.values()].join('\n')}\n}`
}

/** 从后端导出的 CSS 文本里扫出已定义的变量名（`--ds-xxx:` 形式） */
export function definedVars(css: string): Set<string> {
  const out = new Set<string>()
  for (const m of css.matchAll(/(--ds-[\w-]+)\s*:/g)) out.add(m[1])
  return out
}

/** 换肤需要注入的两段样式：① 后端 CSS（收窄作用域）② 外壳别名（只在 ① 有内容时给） */
export function buildSkinStyles(css: string, available: Set<string>): { scoped: string; alias: string } {
  const scoped = scopeCssToSkin(css)
  if (!scoped.trim()) return { scoped: '', alias: '' }
  return { scoped, alias: buildAliasCss(available) }
}
