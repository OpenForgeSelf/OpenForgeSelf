/**
 * 展厅衣柜数据层单测（AC11/AC15 + 03-plan §C 契约）。
 *
 * 钉死的判据：① 项目筛选/排序/上限（archived 与非令牌项目排除）；② id 前缀即 §U 衣柜判据
 * （`preset:` / `project:` / `tuned:`）；③ 主题集 = 色向主题（非 density 轴）+ compact 密度主题；
 * ④ 取数缓存命中不重取、并发受 mapLimit 约束、项目紧凑密度由 `composeCss` 叠加。
 */
import { describe, expect, it } from 'vitest'
import type { GenerateRequest, PreviewCssInput, PreviewCssResult, Project, StylePreset, Theme } from '../api'
import { PRESET_THEMES, applyProjectThemes, buildOutfits, createOutfitLoader, makeTunedOutfit, type Outfit } from './outfits'
import type { DensityId } from './tune'

/* ---------------- 测试数据构造 ---------------- */

/** 造一个项目 DTO（只需断言用到的字段，其余给稳定默认值） */
function project(over: Partial<Project> & { id: number; code: string }): Project {
  return {
    name: over.code,
    kind: 'product',
    version: '1',
    status: 'draft',
    parentProjectId: 0,
    defaultThemeId: 1,
    tokenCount: 100,
    componentCount: 0,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    ...over,
  }
}

/** 造一个内置预设 DTO（`request` 由 recommend 侧补，可选） */
function preset(over: Partial<StylePreset> & { id: string }, request?: GenerateRequest | null): StylePreset & { request?: GenerateRequest | null } {
  return {
    name: over.id,
    tagline: '',
    tones: [],
    kinds: [],
    industries: [],
    keywords: [],
    ...over,
    request,
  }
}

/** 造一个主题 DTO */
function theme(code: string, modeKind: string): Theme {
  return { id: code.length, projectId: 1, code, name: code, modeKind, isDefault: false, baseThemeId: 0, sortOrder: 0, updatedAt: '2026-01-01T00:00:00Z' }
}

/** 一个可直接喂给 loader 的预设衣服 */
function presetOutfit(id: string, request: GenerateRequest | null = null): Outfit {
  return { id: `preset:${id}`, kind: 'preset', label: id, presetId: id, request: request ?? {}, themes: [...PRESET_THEMES], densityTheme: null }
}

const LIGHT: PreviewCssResult = { theme: 'light', css: 'P', seed: '', industry: '', hue: 0, notes: [] }

/* ---------------- buildOutfits ---------------- */

describe('buildOutfits：项目筛选与排序', () => {
  it('排除已归档与零令牌项目，其余按 updatedAt 降序', () => {
    const { mine, ungenerated } = buildOutfits({
      projects: [
        project({ id: 1, code: 'a', updatedAt: '2026-01-02T00:00:00Z' }),
        project({ id: 2, code: 'archived', status: 'archived' }),
        project({ id: 3, code: 'empty', tokenCount: 0 }),
        project({ id: 4, code: 'b', updatedAt: '2026-03-09T00:00:00Z' }),
        project({ id: 5, code: 'c', updatedAt: '2026-02-01T00:00:00Z' }),
      ],
      presets: [],
    })
    expect(mine.map((o) => o.id)).toEqual(['project:b', 'project:c', 'project:a'])
    // 零令牌但不是归档 → 进 ungenerated（提示"还有 N 个未生成"），不入衣柜
    expect(ungenerated.map((p) => p.code)).toEqual(['empty'])
  })

  it('超过上限的合格项目进 hiddenCount', () => {
    const { mine, hiddenCount } = buildOutfits({
      projects: Array.from({ length: 5 }, (_, i) => project({ id: i + 1, code: `p${i}`, updatedAt: `2026-01-0${i + 1}T00:00:00Z` })),
      presets: [],
      limit: 2,
    })
    expect(mine).toHaveLength(2)
    expect(hiddenCount).toBe(3)
  })

  it('默认上限 12', () => {
    const { mine, hiddenCount } = buildOutfits({
      projects: Array.from({ length: 15 }, (_, i) => project({ id: i + 1, code: `p${i}` })),
      presets: [],
    })
    expect(mine).toHaveLength(12)
    expect(hiddenCount).toBe(3)
  })
})

describe('buildOutfits：id 前缀与预设顺序（§U 衣柜判据）', () => {
  it('项目衣服 id = project:<code>，label = name', () => {
    const { mine } = buildOutfits({ projects: [project({ id: 9, code: 'demo', name: '演示系统' })], presets: [] })
    expect(mine[0].id).toBe('project:demo')
    expect(mine[0].kind).toBe('project')
    expect(mine[0].label).toBe('演示系统')
    expect(mine[0].projectId).toBe(9)
    // 主题集未知，等 applyProjectThemes 补
    expect(mine[0].themes).toEqual([])
    expect(mine[0].densityTheme).toBeNull()
  })

  it('预设衣服 id = preset:<id>，保持后端顺序，request 透传', () => {
    const req: GenerateRequest = { hue: 210, density: 'compact' }
    const { presets } = buildOutfits({
      projects: [],
      presets: [preset({ id: 'saas' }, req), preset({ id: 'docs' }), preset({ id: 'fintech' })],
    })
    expect(presets.map((o) => o.id)).toEqual(['preset:saas', 'preset:docs', 'preset:fintech'])
    expect(presets[0].request).toEqual(req)
    expect(presets[0].kind).toBe('preset')
    // 预设舞台主题 = 明/暗两档
    expect(presets[0].themes).toEqual(['light', 'dark'])
  })
})

/* ---------------- 主题集 ---------------- */

describe('applyProjectThemes：舞台主题可用集', () => {
  it('色向主题（非 density 轴）保留，density 轴单独记为 compact 主题', () => {
    const outfit: Outfit = { id: 'project:demo', kind: 'project', label: 'Demo', projectId: 1, themes: [], densityTheme: null }
    const filled = applyProjectThemes(outfit, [
      theme('light', 'color'),
      theme('dark', 'color'),
      theme('brand-b', 'brand'),
      theme('compact', 'density'),
    ])
    expect(filled.themes).toEqual(['light', 'dark', 'brand-b'])
    expect(filled.densityTheme).toBe('compact')
    // 纯函数：不改原对象
    expect(outfit.themes).toEqual([])
    expect(outfit.densityTheme).toBeNull()
  })

  it('无 density 主题 → densityTheme 为 null', () => {
    const filled = applyProjectThemes(
      { id: 'project:x', kind: 'project', label: 'X', projectId: 2, themes: [], densityTheme: null },
      [theme('light', 'color')],
    )
    expect(filled.densityTheme).toBeNull()
  })
})

describe('makeTunedOutfit：微调副本 id = tuned:<n>（§U 判据）', () => {
  it('id 前缀为 tuned:，主题同预设', () => {
    const o = makeTunedOutfit(3, { hue: 20, density: 'comfortable' }, '我的微调 1')
    expect(o.id).toBe('tuned:3')
    expect(o.kind).toBe('tuned')
    expect(o.label).toBe('我的微调 1')
    expect(o.themes).toEqual(['light', 'dark'])
    expect(o.request).toEqual({ hue: 20, density: 'comfortable' })
  })
})

/* ---------------- createOutfitLoader ---------------- */

describe('createOutfitLoader：缓存与同源取数', () => {
  it('同 key 二次取数命中缓存（不重复打后端）', async () => {
    let projectCalls = 0
    let previewCalls = 0
    const loader = createOutfitLoader({
      loadProjectCss: async () => {
        projectCalls++
        return 'A'
      },
      previewCss: async () => {
        previewCalls++
        return LIGHT
      },
    })
    const o = presetOutfit('p')
    const [a, b] = await Promise.all([loader.load(o, { theme: 'light', density: 'default' }), loader.load(o, { theme: 'light', density: 'default' })])
    expect(a).toBe('P')
    expect(b).toBe('P')
    expect(previewCalls).toBe(1)
    expect(projectCalls).toBe(0)
    // 顺序调用同样命中
    await loader.load(o, { theme: 'light', density: 'default' })
    expect(previewCalls).toBe(1)
  })

  it('主题或疏密不同 → 各自取数（缓存键含 theme/density）', async () => {
    const calls: string[] = []
    const loader = createOutfitLoader({
      loadProjectCss: async () => '',
      previewCss: async (i) => {
        calls.push(`${i.theme}|${i.density}`)
        return LIGHT
      },
    })
    const o = presetOutfit('p')
    await loader.load(o, { theme: 'light', density: 'default' })
    await loader.load(o, { theme: 'dark', density: 'default' })
    await loader.load(o, { theme: 'light', density: 'compact' })
    expect(calls.sort()).toEqual(['dark|default', 'light|compact', 'light|default'])
  })

  it('预设衣服走 previewCss：theme/density 覆盖 request，且不透传 themes', async () => {
    const seen: PreviewCssInput[] = []
    const loader = createOutfitLoader({
      loadProjectCss: async () => '',
      previewCss: async (i) => {
        seen.push(i)
        return LIGHT
      },
    })
    const o = presetOutfit('saas', { hue: 210, chroma: 0.1, density: 'compact', radiusBase: 4, themes: ['light', 'dark'] })
    const css = await loader.load(o, { theme: 'dark', density: 'comfortable' })
    expect(css).toBe('P')
    expect(seen[0].theme).toBe('dark')
    expect(seen[0].density).toBe('comfortable')
    expect(seen[0].hue).toBe(210)
    expect(seen[0].radiusBase).toBe(4)
    // `themes` 恒被后端忽略，前端不传（避免"以为它能选主题"的假象）
    expect('themes' in seen[0]).toBe(false)
  })

  it('项目衣服走 loadProjectCss(projectId, theme)', async () => {
    const calls: string[] = []
    const loader = createOutfitLoader({
      loadProjectCss: async (pid, t) => {
        calls.push(`${pid}:${t}`)
        return 'COLOR'
      },
      previewCss: async () => {
        throw new Error('项目衣服不应走 previewCss')
      },
    })
    const o: Outfit = { id: 'project:demo', kind: 'project', label: 'Demo', projectId: 7, themes: ['light', 'dark'], densityTheme: 'compact' }
    const css = await loader.load(o, { theme: 'dark', density: 'default' })
    expect(calls).toEqual(['7:dark'])
    expect(css).toBe('COLOR')
  })

  it('项目紧凑疏密 → 叠加 compact 主题 CSS（同源：composeCss 顺序拼接）', async () => {
    const calls: string[] = []
    const loader = createOutfitLoader({
      loadProjectCss: async (pid, t) => {
        calls.push(`${pid}:${t}`)
        return t === 'compact' ? 'COMPACT' : 'COLOR'
      },
      previewCss: async () => LIGHT,
    })
    const o: Outfit = { id: 'project:demo', kind: 'project', label: 'Demo', projectId: 7, themes: ['light', 'dark'], densityTheme: 'compact' }
    const css = await loader.load(o, { theme: 'dark', density: 'compact' })
    expect(calls).toEqual(['7:dark', '7:compact'])
    expect(css).toBe('COLOR\nCOMPACT')
  })

  it('项目无 compact 主题 → 即使选了紧凑也不叠加', async () => {
    const calls: string[] = []
    const loader = createOutfitLoader({
      loadProjectCss: async (pid, t) => {
        calls.push(`${pid}:${t}`)
        return 'COLOR'
      },
      previewCss: async () => LIGHT,
    })
    const o: Outfit = { id: 'project:demo', kind: 'project', label: 'Demo', projectId: 7, themes: ['light'], densityTheme: null }
    const css = await loader.load(o, { theme: 'light', density: 'compact' })
    expect(calls).toEqual(['7:light'])
    expect(css).toBe('COLOR')
  })
})

describe('createOutfitLoader：并发上限', () => {
  it('loadMany 并发不超过 concurrency，且结果与入参同序', async () => {
    let inFlight = 0
    let peak = 0
    const loader = createOutfitLoader({
      concurrency: 3,
      loadProjectCss: async () => '',
      previewCss: async (i) => {
        inFlight++
        peak = Math.max(peak, inFlight)
        await new Promise((r) => setTimeout(r, 0))
        inFlight--
        return { ...LIGHT, css: `css-${i.theme}` }
      },
    })
    const reqs = Array.from({ length: 6 }, (_, n) => ({ outfit: presetOutfit(`p${n}`), theme: n % 2 ? 'dark' : 'light', density: 'default' as DensityId }))
    const out = await loader.loadMany(reqs)
    expect(out).toHaveLength(6)
    expect(out[0]).toBe('css-light')
    expect(out[1]).toBe('css-dark')
    expect(peak).toBe(3)
  })
})