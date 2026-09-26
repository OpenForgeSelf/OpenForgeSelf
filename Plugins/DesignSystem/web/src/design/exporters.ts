/**
 * 设计系统导出器。
 *
 * 把一份 DesignSystem 序列化为「合理、可直接用」的产出文件：
 * - design-system.md    —— 人读版设计系统规格（给研发/评审/归档）
 * - preview.html        —— 自包含单文件预览（内联生成的 token，离线可开）
 * - design-system.json  —— 结构化本体（可回导 / 版本 diff / 二次加工）
 * - tokens.json         —— 结构化 token（供 Figma 插件 / 主题包 / 构建管线）
 * - tokens.css          —— CSS 变量（任意 Web 项目直接引入）
 * - tailwind.tokens.js  —— Tailwind theme.extend（直接 spread 进 config）
 *
 * 全部在浏览器本地生成（Blob + 下载），不依赖后端、不上传数据。
 */

import type { DesignSystem } from './schema.ts'
import { tokensToCss } from './tokensToCss.ts'

export interface Artifact {
  filename: string
  label: string
  language: 'json' | 'css' | 'javascript' | 'markdown' | 'html'
  mime: string
  content: string
}

function esc(s: string): string {
  return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
}

/* =========================================================================
 * 1. tokens.json
 * ====================================================================== */
export function toTokensJson(ds: DesignSystem): string {
  return JSON.stringify(
    {
      $schema: 'https://forgeself.dev/schema/design-system/v1',
      name: ds.meta.name,
      generatedAt: ds.meta.generatedAt,
      seed: ds.meta.seed,
      tokens: ds.tokens,
    },
    null,
    2,
  )
}

/* =========================================================================
 * 2. design-system.json（本体）
 * ====================================================================== */
export function toDesignSystemJson(ds: DesignSystem): string {
  return JSON.stringify({ version: 1, generator: 'forgeself-design-system', designSystem: ds }, null, 2)
}

/* =========================================================================
 * 3. tokens.css
 * ====================================================================== */
export function toCssFile(ds: DesignSystem): string {
  const head = [
    '/* ' + ds.meta.name + ' · 设计系统 tokens（由 ForgeSelf 设计插件生成） */',
    '/* seed: hue=' +
      ds.meta.seed.hue +
      ' accentHue=' +
      ds.meta.seed.accentHue +
      ' industry=' +
      ds.meta.seed.industry +
      ' */',
    '/* 生成时间：' + ds.meta.generatedAt + ' */',
    '',
  ].join('\n')
  return head + tokensToCss(ds, ':root')
}

/* =========================================================================
 * 4. tailwind.tokens.js
 * ====================================================================== */
export function toTailwindConfig(ds: DesignSystem): string {
  const t = ds.tokens
  const drop = (m: Record<string, string>, p: string): Record<string, string> =>
    Object.fromEntries(Object.entries(m).map(([k, v]) => [k.replace(p, ''), v]))
  const colors = {
    brand: drop(t.color.brand, 'brand-'),
    accent: drop(t.color.accent, 'accent-'),
    neutral: t.color.neutral,
    surface: drop(t.color.surface, 'surface-'),
    fg: drop(
      Object.fromEntries(Object.entries(t.color.text).filter(([k]) => k !== 'link')),
      'fg-',
    ),
    ...t.color.semantic,
    link: t.color.text.link,
  }
  const fontSize = Object.fromEntries(
    Object.entries(t.typography.scale).map(([k, v]) => [
      k,
      [v, { lineHeight: k === 'display' ? t.typography.lineHeights.display : t.typography.lineHeights.normal }],
    ]),
  )
  const rest: Record<string, Record<string, string>> = {
    spacing: drop(t.spacing, 'space-'),
    borderRadius: t.radius,
    boxShadow: drop(t.elevation, 'shadow-'),
    transitionTimingFunction: t.motion.easing,
    transitionDuration: Object.fromEntries(
      Object.entries(t.motion.duration).map(([k, v]) => [k, v.replace('ms', '')]),
    ),
  }
  const restBody = Object.entries(rest)
    .map(([k, v]) => '      ' + k + ': ' + JSON.stringify(v, null, 8).replace(/\n/g, '\n      '))
    .join(',\n')

  return [
    '// ' + ds.meta.name + ' · 设计系统 tokens（由 ForgeSelf 设计插件生成）',
    '// 用法：在 tailwind.config.js 中使用 `...designTokens.theme.extend`',
    '',
    'export const designTokens = {',
    '  theme: {',
    '    extend: {',
    '      colors: ' + JSON.stringify(colors, null, 8).replace(/\n/g, '\n      ') + ',',
    '      fontFamily: {',
    '        sans: [' + JSON.stringify(t.typography.fontSans) + '],',
    '        mono: [' + JSON.stringify(t.typography.fontMono) + '],',
    '      },',
    '      fontSize: ' + JSON.stringify(fontSize, null, 8).replace(/\n/g, '\n      ') + ',',
    restBody + ',',
    '    },',
    '  },',
    '}',
    '',
    'export default designTokens',
    '',
  ].join('\n')
}

/* =========================================================================
 * 5. design-system.md（人读规格）
 * ====================================================================== */
export function toDesignSystemMarkdown(ds: DesignSystem): string {
  const L: string[] = []
  L.push('# ' + ds.meta.name + ' · 设计系统')
  L.push('')
  L.push(
    '> 由 ForgeSelf 设计插件生成 · seed：hue=' +
      ds.meta.seed.hue +
      ' / accentHue=' +
      ds.meta.seed.accentHue +
      ' / 行业=' +
      ds.meta.seed.industry +
      ' · ' +
      ds.meta.generatedAt,
  )
  L.push('')
  L.push('## 0. 需求原文')
  L.push('')
  L.push('```')
  L.push(ds.meta.brief)
  L.push('```')
  L.push('')
  L.push('## 1. 品牌')
  L.push('')
  L.push('- **Logo 概念**：' + ds.brand.logoConcept)
  L.push('- **品牌渐变**：' + ds.brand.gradient)
  L.push('- **图标系统**：' + ds.brand.iconSystem)
  L.push('- **装饰母题**：' + ds.brand.motif)
  L.push('')
  L.push('## 2. 颜色')
  L.push('')
  for (const [group, map] of Object.entries(ds.tokens.color)) {
    L.push('**' + group + '**：' + Object.entries(map).map(([k, v]) => k + '=' + v).join(', '))
    L.push('')
  }
  L.push('## 3. 类型')
  L.push('')
  L.push('- 字体：' + ds.tokens.typography.fontSans)
  L.push('- 等宽：' + ds.tokens.typography.fontMono)
  L.push('- 字阶：' + Object.entries(ds.tokens.typography.scale).map(([k, v]) => k + '=' + v).join(', '))
  L.push('')
  L.push('## 4. 间距与质感')
  L.push('')
  L.push('- 间距：' + Object.entries(ds.tokens.spacing).map(([k, v]) => k + '=' + v).join(', '))
  L.push('- 圆角：' + Object.entries(ds.tokens.radius).map(([k, v]) => k + '=' + v).join(', '))
  L.push('- 描边：' + Object.entries(ds.tokens.border).map(([k, v]) => k + '=' + v).join(', '))
  L.push('')
  L.push('## 5. 组件')
  L.push('')
  L.push('| 组件 | 用途 | 变体 | 构成 |')
  L.push('| --- | --- | --- | --- |')
  for (const c of ds.components) {
    L.push('| ' + c.name + ' | ' + c.purpose + ' | ' + (c.variants ?? '—') + ' | ' + c.anatomy.join('、') + ' |')
  }
  L.push('')
  L.push('## 6. 应用示例')
  L.push('')
  for (const a of ds.applications) {
    L.push('### ' + a.name)
    L.push('')
    L.push(a.desc)
    L.push('')
    L.push('区块：' + a.sections.join(' / '))
    L.push('')
  }
  L.push('## 7. 设计自检')
  L.push('')
  for (const c of ds.selfCheck) L.push('- [ ] **' + c.item + '**：' + c.detail)
  L.push('')
  return L.join('\n')
}

/* =========================================================================
 * 6. preview.html（自包含预览）
 * ====================================================================== */
export function toPreviewHtml(ds: DesignSystem): string {
  const P: string[] = []
  const t = ds.tokens
  P.push('<!doctype html>')
  P.push('<html lang="zh-CN">')
  P.push('<head>')
  P.push('<meta charset="utf-8" />')
  P.push('<meta name="viewport" content="width=device-width, initial-scale=1" />')
  P.push('<title>' + esc(ds.meta.name) + ' · 设计系统预览</title>')
  P.push('<style>')
  P.push(tokensToCss(ds, ':root'))
  P.push(
    [
      'body{margin:0;font-family:var(--ds-font-sans);font-size:var(--ds-fs-body);line-height:var(--ds-lh-normal);',
      'color:var(--ds-fg-1);background:var(--ds-surface-bg)}',
      '.wrap{max-width:1080px;margin:0 auto;padding:var(--ds-space-7) var(--ds-space-6)}',
      '.hero{border-radius:var(--ds-radius-2xl);padding:var(--ds-space-8);background:var(--ds-gradient-brand);color:#fff;margin-bottom:var(--ds-space-8)}',
      '.hero h1{margin:0 0 var(--ds-space-2);font-size:var(--ds-fs-h1);font-weight:var(--ds-fw-bold)}',
      '.hero p{margin:0;opacity:.92}',
      'h2{font-size:var(--ds-fs-h2);margin:var(--ds-space-8) 0 var(--ds-space-4)}',
      'h3{font-size:var(--ds-fs-h3);margin:var(--ds-space-5) 0 var(--ds-space-3)}',
      '.card{background:var(--ds-surface-1);border:1px solid var(--ds-border-1);border-radius:var(--ds-radius-lg);padding:var(--ds-space-5);box-shadow:var(--ds-shadow-sm);margin-bottom:var(--ds-space-4)}',
      '.row{display:flex;flex-wrap:wrap;gap:var(--ds-space-2)}',
      '.sw{width:64px;height:48px;border-radius:var(--ds-radius-md);display:flex;align-items:flex-end;justify-content:center;font-size:10px;color:#fff;text-shadow:0 1px 2px rgba(0,0,0,.35);padding-bottom:4px}',
      '.chip{font-size:var(--ds-fs-small);padding:4px 10px;border:1px solid var(--ds-border-1);border-radius:var(--ds-radius-md);background:var(--ds-surface-2);color:var(--ds-fg-2)}',
      '.btn{display:inline-flex;align-items:center;gap:8px;border:1px solid transparent;border-radius:var(--ds-radius-md);padding:9px 16px;font-weight:var(--ds-fw-semibold);font-size:var(--ds-fs-body)}',
      '.btn-primary{background:var(--ds-color-primary);color:#fff}',
      '.btn-secondary{background:var(--ds-surface-1);color:var(--ds-fg-1);border-color:var(--ds-border-2)}',
      '.btn-ghost{background:transparent;color:var(--ds-color-primary)}',
      '.btn-danger{background:var(--ds-danger);color:#fff}',
      '.muted{color:var(--ds-fg-3);font-size:var(--ds-fs-small)}',
      'table{width:100%;border-collapse:collapse;font-size:var(--ds-fs-small)}',
      'th,td{text-align:left;padding:8px 12px;border-bottom:1px solid var(--ds-border-1)}',
      'th{color:var(--ds-fg-3);font-weight:600}',
      '.foot{text-align:center;color:var(--ds-fg-4);font-size:var(--ds-fs-small);border-top:1px solid var(--ds-border-1);padding-top:var(--ds-space-4);margin-top:var(--ds-space-8)}',
    ].join('\n'),
  )
  P.push('</style>')
  P.push('</head>')
  P.push('<body>')
  P.push('<div class="wrap">')
  P.push('<div class="hero"><h1>' + esc(ds.meta.name) + '</h1><p>' + esc(ds.brand.logoConcept) + '</p></div>')

  P.push('<h2>颜色</h2>')
  for (const [group, map] of Object.entries(t.color)) {
    P.push('<div class="card"><h3>' + esc(group) + '</h3><div class="row">')
    for (const [k, v] of Object.entries(map)) {
      P.push('<div class="sw" style="background:' + v + '">' + esc(k) + '</div>')
    }
    P.push('</div></div>')
  }

  P.push('<h2>类型</h2><div class="card">')
  P.push(
    '<div style="font-size:var(--ds-fs-display);font-weight:var(--ds-fw-bold);line-height:var(--ds-lh-display)">Display 64</div>',
  )
  for (const k of ['h1', 'h2', 'h3', 'h4', 'body-lg', 'body', 'small', 'micro']) {
    P.push(
      '<div style="font-size:var(--ds-fs-' +
        k +
        ');font-weight:' +
        (k.startsWith('h') ? 'var(--ds-fw-semibold)' : 'var(--ds-fw-regular)') +
        '">' +
        k +
        ' = ' +
        t.typography.scale[k] +
        '</div>',
    )
  }
  P.push('<div class="muted" style="margin-top:12px">等宽：' + esc(t.typography.fontMono) + '</div>')
  P.push('</div>')

  P.push('<h2>组件</h2><div class="card">')
  P.push(
    '<div class="row"><span class="btn btn-primary">Primary</span><span class="btn btn-secondary">Secondary</span><span class="btn btn-ghost">Ghost</span><span class="btn btn-danger">Danger</span></div>',
  )
  P.push('<table style="margin-top:16px"><tr><th>组件</th><th>用途</th><th>变体</th></tr>')
  for (const c of ds.components) {
    P.push(
      '<tr><td>' + esc(c.name) + '</td><td>' + esc(c.purpose) + '</td><td>' + esc(c.variants ?? '—') + '</td></tr>',
    )
  }
  P.push('</table></div>')

  P.push('<h2>应用示例</h2>')
  for (const a of ds.applications) {
    P.push(
      '<div class="card"><h3>' +
        esc(a.name) +
        '</h3><div class="muted">' +
        esc(a.desc) +
        '</div><div class="row" style="margin-top:12px">',
    )
    for (const s of a.sections) P.push('<span class="chip">' + esc(s) + '</span>')
    P.push('</div></div>')
  }

  P.push('<h2>设计自检</h2><div class="card">')
  for (const c of ds.selfCheck) P.push('<div><strong>' + esc(c.item) + '</strong>：' + esc(c.detail) + '</div>')
  P.push('</div>')

  P.push(
    '<div class="foot">' + esc(ds.meta.name) + ' · 由 ForgeSelf 设计插件生成 · 本文件自包含，可离线打开</div>',
  )
  P.push('</div>')
  P.push('</body>')
  P.push('</html>')
  return P.join('\n')
}

/* =========================================================================
 * 清单
 * ====================================================================== */
export function buildArtifacts(ds: DesignSystem): Artifact[] {
  return [
    {
      filename: 'design-system.md',
      label: '设计系统规格（人读版）',
      language: 'markdown',
      mime: 'text/markdown',
      content: toDesignSystemMarkdown(ds),
    },
    {
      filename: 'preview.html',
      label: '单文件预览（可离线打开）',
      language: 'html',
      mime: 'text/html',
      content: toPreviewHtml(ds),
    },
    {
      filename: 'design-system.json',
      label: '设计系统本体（可回导/diff）',
      language: 'json',
      mime: 'application/json',
      content: toDesignSystemJson(ds),
    },
    {
      filename: 'tokens.json',
      label: 'tokens（结构化）',
      language: 'json',
      mime: 'application/json',
      content: toTokensJson(ds),
    },
    {
      filename: 'tokens.css',
      label: 'tokens（CSS 变量）',
      language: 'css',
      mime: 'text/css',
      content: toCssFile(ds),
    },
    {
      filename: 'tailwind.tokens.js',
      label: 'Tailwind theme 扩展',
      language: 'javascript',
      mime: 'text/javascript',
      content: toTailwindConfig(ds),
    },
  ]
}

/** 触发浏览器下载。 */
export function downloadArtifact(a: Artifact): void {
  const blob = new Blob([a.content], { type: a.mime + ';charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const el = document.createElement('a')
  el.href = url
  el.download = a.filename
  document.body.appendChild(el)
  el.click()
  el.remove()
  URL.revokeObjectURL(url)
}

/** 在新窗口打开独立预览。 */
export function openPreviewInNewTab(a: Artifact): void {
  const blob = new Blob([a.content], { type: 'text/html;charset=utf-8' })
  window.open(URL.createObjectURL(blob), '_blank')
}
