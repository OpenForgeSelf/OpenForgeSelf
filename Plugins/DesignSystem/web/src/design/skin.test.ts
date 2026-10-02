/**
 * 换肤层单测：证明"预览与交付同源"这条设计约束成立。
 *
 * 关键风险：换肤靠把后端 CSS 的 `:root` 收窄成 `.ds-skin` + 一层变量别名。
 * 一旦作用域没收窄，用户系统会污染插件外壳；一旦别名指向不存在的令牌，
 * 页面表现是"颜色突然变透明"。两条都有用例钉住。
 */
import { describe, expect, it } from 'vitest'
import { buildAliasCss, buildSkinStyles, composeCss, definedVars, pickVars, scopeCssToSkin } from './skin'

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

describe('scopeCssToSkin 自定义作用域（v3 参数化，AC10）', () => {
  it('自定义属性选择器作用域下两处 :root 与焦点环都被收窄', () => {
    const out = scopeCssToSkin(BACKEND_CSS, '[data-outfit="preset:a"]')
    expect(out).toContain('[data-outfit="preset:a"] {')
    expect(out).not.toContain(':root')
    expect(out).toContain('[data-outfit="preset:a"] :where(a, button, input, select, textarea, [tabindex]):focus-visible')
    // reduced-motion 媒体块必须保留
    expect(out).toContain('@media (prefers-reduced-motion: reduce)')
  })

  it('默认作用域仍是 .ds-skin（旧行为不变，旧用例已锁）', () => {
    expect(scopeCssToSkin(':root { --a: 1; }')).toBe('.ds-skin { --a: 1; }')
  })
})

describe('pickVars（v3，AC10）', () => {
  const CSS = `/* 头注释 */
:root {
  --ds-semantic-brand: #2563eb;
  --ds-semantic-text-1: #111827;
  --ds-component-card-background: #ffffff;
  --ds-radius-md: 10px;
}

@media (prefers-reduced-motion: reduce) {
  :root {
    --ds-duration-base: 0.01ms;
  }
}

@font-face {
  font-family: "x";
}`

  it('只保留 wanted 且 CSS 里真定义的声明，逐字不变', () => {
    const out = pickVars(CSS, new Set(['--ds-semantic-brand', '--ds-radius-md']))
    expect(out).toContain('--ds-semantic-brand: #2563eb;')
    expect(out).toContain('--ds-radius-md: 10px;')
    expect(out).not.toContain('--ds-semantic-text-1')
    expect(out).not.toContain('--ds-component-card-background')
  })

  it('丢弃 @media 与 @font-face 及注释', () => {
    const out = pickVars(CSS, new Set(['--ds-semantic-brand']))
    expect(out).not.toContain('@media')
    expect(out).not.toContain('@font-face')
    expect(out).not.toContain('/*')
    expect(out).not.toContain('--ds-duration-base')
  })

  it('wanted 里 CSS 没定义的变量被忽略（不新造声明）', () => {
    const out = pickVars(CSS, new Set(['--ds-semantic-brand', '--ds-no-such']))
    expect(out).toContain('--ds-semantic-brand')
    expect(out).not.toContain('--ds-no-such')
  })

  it('引用链补齐：保留项的值引用 var(--ds-x) 而 x 未保留时，把 x 一并保留', () => {
    const css = `:root {
  --ds-component-card-background: var(--ds-semantic-surface-1);
  --ds-semantic-surface-1: #f8fafc;
  --ds-semantic-brand: #2563eb;
}`
    const out = pickVars(css, new Set(['--ds-component-card-background']))
    expect(out).toContain('--ds-component-card-background: var(--ds-semantic-surface-1);')
    // 被引用的 --ds-semantic-surface-1 沿链补进来，避免悬空引用
    expect(out).toContain('--ds-semantic-surface-1: #f8fafc;')
    // 无关变量不掺和
    expect(out).not.toContain('--ds-semantic-brand')
  })

  it('无 :root 块返回空串', () => {
    expect(pickVars('@media print { body { color: red } }', new Set(['--ds-x']))).toBe('')
    expect(pickVars('', new Set(['--ds-x']))).toBe('')
  })
})

describe('composeCss（v3，AC10）', () => {
  it('按序拼接，后者覆盖前者（密度 CSS 覆盖配色 CSS 的同名变量）', () => {
    const color = ':root {\n  --ds-space-4: 16px;\n}'
    const compact = ':root {\n  --ds-space-4: 8px;\n  --ds-radius-md: 6px;\n}'
    const out = composeCss([color, compact])
    expect(out.indexOf('--ds-space-4: 16px;')).toBeLessThan(out.indexOf('--ds-space-4: 8px;'))
  })

  it('空段跳过', () => {
    expect(composeCss(['', '  ', ':root {\n  --a: 1;\n}'])).toBe(':root {\n  --a: 1;\n}')
    expect(composeCss([])).toBe('')
  })
})
