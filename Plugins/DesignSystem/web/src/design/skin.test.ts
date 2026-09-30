/**
 * 换肤层单测：证明"预览与交付同源"这条设计约束成立。
 *
 * 关键风险：换肤靠把后端 CSS 的 `:root` 收窄成 `.ds-skin` + 一层变量别名。
 * 一旦作用域没收窄，用户系统会污染插件外壳；一旦别名指向不存在的令牌，
 * 页面表现是"颜色突然变透明"。两条都有用例钉住。
 */
import { describe, expect, it } from 'vitest'
import { buildAliasCss, buildSkinStyles, definedVars, scopeCssToSkin } from './skin'

const BACKEND_CSS = `/* 生成物 */
:root {
  --ds-semantic-text-1: #111827;
  --ds-component-card-background: #ffffff;
}

@media (prefers-reduced-motion: reduce) {
  :root {
    --ds-duration-base: 0.01ms;
  }
}

:where(a, button, input, select, textarea, [tabindex]):focus-visible {
  outline: var(--ds-component-focus-outline-width, 2px) solid currentColor;
}`

describe('scopeCssToSkin', () => {
  it('把两处 :root 都收窄进 .ds-skin，产物里不再有任何全局根选择器', () => {
    const out = scopeCssToSkin(BACKEND_CSS)
    expect(out).toContain('.ds-skin {')
    expect(out).not.toContain(':root')
    // reduced-motion 媒体块必须保留，否则"动效可关闭"在预览里看不见
    expect(out).toContain('@media (prefers-reduced-motion: reduce)')
  })

  it('焦点环选择器挂到容器内，不影响外壳自身', () => {
    const out = scopeCssToSkin(BACKEND_CSS)
    expect(out).toContain('.ds-skin :where(a, button, input, select, textarea, [tabindex]):focus-visible')
  })

  it('空 CSS 返回空串（后端还没令牌时不该注入空样式块）', () => {
    expect(scopeCssToSkin('')).toBe('')
    expect(scopeCssToSkin('   ')).toBe('')
  })
})

describe('definedVars', () => {
  it('从导出 CSS 扫出真正定义了哪些变量（别名表只能引用这些名字）', () => {
    const vars = definedVars(BACKEND_CSS)
    expect(vars.has('--ds-semantic-text-1')).toBe(true)
    expect(vars.has('--ds-component-card-background')).toBe(true)
    expect(vars.has('--ds-semantic-brand')).toBe(false)
  })
})

describe('buildAliasCss', () => {
  it('只为后端 CSS 里存在的令牌写别名，缺失的一律不写', () => {
    const css = buildAliasCss(definedVars(BACKEND_CSS))
    // semantic.surface-1 不存在 → --ds-surface-1 只能由组件层那条写出
    expect(css).toContain('--ds-surface-1: var(--ds-component-card-background);')
    expect(css).toContain('--ds-fg-1: var(--ds-semantic-text-1);')
    expect(css).not.toContain('--ds-color-primary:')
  })

  it('组件层优先于语义层：同一变量只留一条且取 component 值', () => {
    const css = buildAliasCss(new Set(['--ds-semantic-text-1', '--ds-component-card-foreground']))
    const hits = css.split('\n').filter((l) => l.trim().startsWith('--ds-fg-1:'))
    expect(hits).toHaveLength(1)
    expect(hits[0]).toContain('var(--ds-component-card-foreground)')
  })

  it('一个令牌都没有时返回空串', () => {
    expect(buildAliasCss(new Set())).toBe('')
  })
})

describe('buildSkinStyles', () => {
  it('两段样式齐备：scoped 有令牌定义，alias 有外壳重键', () => {
    const { scoped, alias } = buildSkinStyles(BACKEND_CSS, definedVars(BACKEND_CSS))
    expect(scoped).toContain('--ds-semantic-text-1: #111827;')
    expect(alias).toMatch(/^\.ds-skin \{/)
    expect(alias).toContain('--ds-fg-1: var(--ds-semantic-text-1);')
  })

  it('后端 CSS 为空时两段都给空串（没有值可换就不注入样式）', () => {
    expect(buildSkinStyles('', definedVars(''))).toEqual({ scoped: '', alias: '' })
  })

  // 真跑出来的缺陷：别名指向未定义的变量时，整条声明在 computed-value 阶段失效，
  // 页面表现是"背景突然全透明"——比不换肤更糟，且只有真实浏览器会发现。
  it('回归：别名里引用的每个 var() 都必须在注入的 CSS 里有定义', () => {
    const { scoped, alias } = buildSkinStyles(BACKEND_CSS, definedVars(BACKEND_CSS))
    const defined = definedVars(scoped)
    const refs = [...alias.matchAll(/var\((--ds-[\w-]+)\)/g)].map((m) => m[1] as string)
    expect(refs.length).toBeGreaterThan(0)
    expect(refs.filter((r) => !defined.has(r))).toEqual([])
  })
})
