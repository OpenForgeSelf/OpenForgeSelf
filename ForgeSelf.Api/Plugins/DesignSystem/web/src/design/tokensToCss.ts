/**
 * 设计系统 → CSS 变量。
 *
 * 关键机制：**同名覆盖（cascade override）**。
 * 组件样式一律只消费 `--ds-*` 这套「语义插槽」（如 `--ds-brand-600` / `--ds-color-primary`），
 * 从不写死具体色值。因此只要在某个容器上重新声明这些变量，
 * 其所有后代组件就会**整体换肤**为另一套设计系统 ——
 * 这正是「预览任意生成结果」的实现基础，无需为每套系统重写组件。
 *
 * 同一套变量命名同时服务于两处：
 * - 插件外壳：`scripts/gen-tokens-css.ts` 用 `SHELL` 生成 `src/styles/tokens.css`（selector `:root`）
 * - 生成结果预览：运行时按生成的设计系统生成，内联到预览容器（selector 为容器）
 */

import type { DesignSystem } from './schema.ts'

/** 把 `{ 'space-1': '4px' }` 这类映射转成 `--ds-space-1: 4px;` 声明。 */
function decls(map: Record<string, string>, prefix = '', indent = '  '): string {
  return Object.entries(map)
    .map(([k, v]) => `${indent}--ds-${prefix}${k}: ${v};`)
    .join('\n')
}

/**
 * 生成 CSS 变量声明块（不含 selector 外壳，便于内联复用）。
 * @param ds 设计系统
 */
export function cssVarBody(ds: DesignSystem): string {
  const t = ds.tokens
  return [
    '/* 字体 */',
    `  --ds-font-sans: ${t.typography.fontSans};`,
    `  --ds-font-mono: ${t.typography.fontMono};`,
    `  --ds-num: ${t.typography.fontMono};`,
    `  --ds-num-feature: 'tnum' 1, 'lnum' 1;`,
    '',
    '/* 颜色：品牌 / 辅助 / 中性 / 语义 / 表面 / 前景 */',
    decls(t.color.brand, 'brand-'),
    '',
    decls(t.color.accent, 'accent-'),
    '',
    decls(t.color.neutral, 'gray-'),
    '',
    decls(t.color.semantic),
    '',
    decls(t.color.surface),
    '',
    decls(t.color.text),
    '',
    '/* 类型 */',
    decls(t.typography.scale, 'fs-'),
    '',
    decls(t.typography.weights, 'fw-'),
    '',
    decls(t.typography.lineHeights, 'lh-'),
    '',
    '/* 间距 / 圆角 / 阴影 / 描边 */',
    decls(t.spacing),
    '',
    decls(t.radius, 'radius-'),
    '',
    decls(t.elevation),
    '',
    decls(t.border),
    '',
    '/* 动效 */',
    decls(t.motion.easing),
    '',
    decls(t.motion.duration, 'dur-'),
    '',
    '/* 语义别名（组件一律消费别名，不直接消费色阶） */',
    '  --ds-color-primary: var(--ds-brand-600);',
    '  --ds-color-primary-hover: var(--ds-brand-700);',
    '  --ds-color-primary-active: var(--ds-brand-800);',
    '  --ds-color-accent: var(--ds-accent-500);',
    '  --ds-color-accent-hover: var(--ds-accent-600);',
    '  --ds-gradient-brand: linear-gradient(135deg, var(--ds-brand-500) 0%, var(--ds-accent-400) 100%);',
    '  --ds-gradient-brand-soft: linear-gradient(135deg, color-mix(in srgb, var(--ds-brand-500) 12%, transparent) 0%, color-mix(in srgb, var(--ds-accent-400) 12%, transparent) 100%);',
    '  --ds-brand-mark: var(--ds-gradient-brand);',
    '  --ds-brand-wordmark: var(--ds-fg-1);',
    '  --ds-surface-0: var(--ds-surface-bg);',
  ].join('\n')
}

/**
 * 生成完整 CSS 规则块。
 * @param ds 设计系统
 * @param selector CSS 选择器，默认 `:root`
 */
export function tokensToCss(ds: DesignSystem, selector = ':root'): string {
  return `${selector} {\n${cssVarBody(ds)}\n}\n`
}

/**
 * 生成可内联到元素 `style` 的字符串（预览换肤用）。
 * @param ds 设计系统
 */
export function tokensToStyleAttr(ds: DesignSystem): string {
  return Object.entries(flattenVars(ds))
    .map(([k, v]) => `${k}:${v}`)
    .join(';')
}

/** 展平为「变量名 → 值」映射（只含具体值，含别名）。 */
export function flattenVars(ds: DesignSystem): Record<string, string> {
  const t = ds.tokens
  const out: Record<string, string> = {}
  const put = (map: Record<string, string>, prefix = '') => {
    for (const [k, v] of Object.entries(map)) out[`--ds-${prefix}${k}`] = v
  }
  out['--ds-font-sans'] = t.typography.fontSans
  out['--ds-font-mono'] = t.typography.fontMono
  out['--ds-num'] = t.typography.fontMono
  out['--ds-num-feature'] = "'tnum' 1, 'lnum' 1"
  put(t.color.brand, 'brand-')
  put(t.color.accent, 'accent-')
  put(t.color.neutral, 'gray-')
  put(t.color.semantic)
  put(t.color.surface)
  put(t.color.text)
  put(t.typography.scale, 'fs-')
  put(t.typography.weights, 'fw-')
  put(t.typography.lineHeights, 'lh-')
  put(t.spacing)
  put(t.radius, 'radius-')
  put(t.elevation)
  put(t.border)
  put(t.motion.easing)
  put(t.motion.duration, 'dur-')
  // 别名（指向具体色阶值，预览内联时用具体值，避免嵌套 var 在未定义时失效）
  out['--ds-color-primary'] = t.color.brand['600']
  out['--ds-color-primary-hover'] = t.color.brand['700']
  out['--ds-color-primary-active'] = t.color.brand['800']
  out['--ds-color-accent'] = t.color.accent['500']
  out['--ds-color-accent-hover'] = t.color.accent['600']
  out['--ds-gradient-brand'] = `linear-gradient(135deg, ${t.color.brand['500']} 0%, ${t.color.accent['400']} 100%)`
  out['--ds-gradient-brand-soft'] = `linear-gradient(135deg, ${t.color.brand['50']} 0%, ${t.color.accent['50']} 100%)`
  out['--ds-surface-1'] = t.color.surface['surface-1']
  out['--ds-surface-2'] = t.color.surface['surface-2']
  out['--ds-surface-3'] = t.color.surface['surface-3']
  out['--ds-surface-bg'] = t.color.surface['surface-bg']
  out['--ds-brand-mark'] = out['--ds-gradient-brand']
  out['--ds-brand-wordmark'] = t.color.text['fg-1']
  return out
}
