/**
 * 展厅衣柜数据层（03-plan §C 契约的唯一真源）。
 *
 * 三件事，都必须是纯逻辑（不碰 DOM、不发请求本身）：
 * ① `buildOutfits`：把项目与内置预设整理成"衣服"清单（谁进衣柜、排序、上限、未生成提示）；
 * ② `applyProjectThemes`：把项目的主题轴补成"舞台可用主题集"（色向主题 + compact 密度主题）；
 * ③ `createOutfitLoader`：按「衣服 × 主题 × 疏密」取 CSS 文本，带缓存与并发上限。
 *
 * 同源纪律：这里**只搬运文本**（后端 `export?format=css` / `preview-css` 的产物），
 * 一个颜色/尺寸都不在前端计算；紧凑疏密靠 `composeCss` 顺序拼接而不是前端改值。
 */
import type { GenerateRequest, PreviewCssInput, PreviewCssResult, Project, StylePreset, Theme } from '../api'
import { mapLimit } from '../design/pool'
import { composeCss } from '../design/skin'
import type { DensityId } from './tune'

/** 衣柜衣服的种类（id 前缀即 §U 契约判据） */
export type OutfitKind = 'project' | 'preset' | 'tuned'

/** 一件衣服：展厅衣柜的一个选项，舞台按它渲染 */
export interface Outfit {
  /** `preset:<id>` / `project:<code>` / `tuned:<n>`（§U `[data-outfit-id]` 判据） */
  id: string
  kind: OutfitKind
  label: string
  /** 项目衣服才有：用于 `listThemes` 与导出取数 */
  projectId?: number
  /** 预设 / 微调衣服：生成请求（微调衣服已叠加微调值） */
  request?: GenerateRequest
  presetId?: string
  /** 舞台可用主题集：项目 = 该项目色向主题 code；预设 = 明/暗（`PRESET_THEMES`） */
  themes: string[]
  /** 项目密度主题 code（`compact`）；无则不叠加紧凑密度 */
  densityTheme: string | null
}

/** 预设衣服的舞台主题档（03-plan §C：预设 = light/dark） */
export const PRESET_THEMES: readonly string[] = ['light', 'dark']

/**
 * `buildOutfits` 的预设入参：M1 `GET presets` 的目录字段，外挂 `recommend` 侧补来的 `request`。
 * 为什么外挂：`GET presets` 不回 `request`（REST 只给目录），只有 `presets/recommend` 才带，
 * 前端把两者按 id 合并后交进来，这里保持传入顺序（= 后端枚举顺序）。
 */
export type PresetInput = StylePreset & { request?: GenerateRequest | null }

export interface BuildOutfitsInput {
  projects: readonly Project[]
  presets: readonly PresetInput[]
  /** 衣柜里"我的设计系统"的显示上限（预设不受限） */
  limit?: number
}

/** 衣柜内容（`ungenerated` 用于"还有 N 个未生成令牌的项目"提示） */
export interface OutfitBundle {
  /** 我的项目（非归档且有令牌，按更新降序，最多 limit 件） */
  mine: Outfit[]
  /** 内置预设（保持后端顺序） */
  presets: Outfit[]
  /** 因超过上限而未显示的我的项目数 */
  hiddenCount: number
  /** 非归档但还没有令牌的项目（无法预览，只提示） */
  ungenerated: Project[]
}

/** 默认衣柜上限（§C：limit=12） */
export const OUTFIT_LIMIT = 12

/**
 * 把项目与预设整理成衣柜清单。
 *
 * 筛选：项目需 `status !== 'archived'` 且 `tokenCount > 0`；`archived` 与零令牌都不进衣柜，
 * 其中零令牌项目归入 `ungenerated` 让界面给出"去生成"的提示（而不是静默消失）。
 */
export function buildOutfits({ projects, presets, limit = OUTFIT_LIMIT }: BuildOutfitsInput): OutfitBundle {
  const usable = projects.filter((p) => p.status !== 'archived')
  const generated = usable
    .filter((p) => p.tokenCount > 0)
    .slice()
    .sort((a, b) => (a.updatedAt < b.updatedAt ? 1 : a.updatedAt > b.updatedAt ? -1 : 0))
  const ungenerated = usable.filter((p) => p.tokenCount <= 0)

  const mine = generated.slice(0, Math.max(0, limit)).map(
    (p): Outfit => ({
      id: `project:${p.code}`,
      kind: 'project',
      label: p.name || p.code,
      projectId: p.id,
      themes: [],
      densityTheme: null,
    }),
  )

  const presetOutfits = presets.map(
    (s): Outfit => ({
      id: `preset:${s.id}`,
      kind: 'preset',
      label: s.name || s.id,
      presetId: s.id,
      request: s.request ?? {},
      themes: [...PRESET_THEMES],
      densityTheme: null,
    }),
  )

  return { mine, presets: presetOutfits, hiddenCount: Math.max(0, generated.length - mine.length), ungenerated }
}

/**
 * 补项目的舞台主题集：色向主题（非 `density` 轴）作明暗/品牌档，`density` 轴主题单独记为
 * `densityTheme` 供紧凑疏密叠加。纯函数，返回新对象（不改入参）。
 */
export function applyProjectThemes(outfit: Outfit, themes: readonly Theme[]): Outfit {
  const colors = themes.filter((t) => t.modeKind !== 'density').map((t) => t.code)
  const density = themes.find((t) => t.modeKind === 'density')?.code ?? null
  return { ...outfit, themes: colors, densityTheme: density }
}

/**
 * 造一件微调衣服（§U 契约 id = `tuned:<n>`）。
 * 微调值已由 `tuneToRequest` 叠加进 `request`，这里只负责 id/主题档的单一真源。
 */
export function makeTunedOutfit(seq: number, request: GenerateRequest, label: string): Outfit {
  return { id: `tuned:${seq}`, kind: 'tuned', label, request, themes: [...PRESET_THEMES], densityTheme: null }
}

/** 舞台取数上下文：主题 code × 疏密档 */
export interface LoadOpts {
  theme: string
  density: DensityId
}

/** 批量取数的一项 */
export interface LoadRequest extends LoadOpts {
  outfit: Outfit
}

/** 取数依赖（真实实现由 Showroom 用 `api.*` 注入；测试用受控 stub） */
export interface OutfitLoaderDeps {
  /** 项目某主题的导出 CSS（`export?format=css&theme=`）；紧凑密度用 densityTheme code 再取一份 */
  loadProjectCss: (projectId: number, theme: string) => Promise<string>
  /** 预设/微调衣服的预览 CSS（与导出同源） */
  previewCss: (input: PreviewCssInput) => Promise<PreviewCssResult>
  /** 缩略图取数并发上限（默认 3，防把宿主的 SQLite 打爆） */
  concurrency?: number
}

export interface OutfitLoader {
  /** 取一件衣服在「主题 × 疏密」下的 CSS（同 key 命中缓存，不重复取数） */
  load(outfit: Outfit, opts: LoadOpts): Promise<string>
  /** 批量取数（缩略图用）：并发 ≤ concurrency，返回与入参同序的 CSS */
  loadMany(requests: readonly LoadRequest[]): Promise<string[]>
}

/** 缓存键 = `衣服id|主题|疏密`（§C 契约；命中即不再取数） */
function cacheKey(outfit: Outfit, opts: LoadOpts): string {
  return `${outfit.id}|${opts.theme}|${opts.density}`
}

/**
 * 把衣服请求拍平成 `PreviewCssInput`：舞台的 `theme`/`density` 覆盖 request 上的同名项，
 * 其余字段沿用；`themes` **显式不传**（后端恒忽略它，传了只会让人误以为能选主题）。
 */
function toPreviewInput(request: GenerateRequest | undefined, opts: LoadOpts): PreviewCssInput {
  const r = request ?? {}
  return {
    brief: r.brief ?? undefined,
    seedColor: r.seedColor ?? undefined,
    hue: r.hue ?? undefined,
    chroma: r.chroma ?? undefined,
    density: opts.density,
    typeRatio: r.typeRatio ?? undefined,
    typeBasePx: r.typeBasePx ?? undefined,
    radiusBase: r.radiusBase ?? undefined,
    motionScale: r.motionScale ?? undefined,
    brandName: r.brandName ?? undefined,
    industry: r.industry ?? undefined,
    accentHueOffset: r.accentHueOffset ?? undefined,
    theme: opts.theme,
  }
}

/**
 * 创建取数器。
 *
 * - 缓存存的是 **Promise**：并发同 key 只发一次（两个缩略图同时要看同一件衣服时不重复取数）。
 * - 项目衣服：`loadProjectCss(projectId, theme)`；当疏密非 `default` 且项目有 `compact` 密度主题时，
 *   再取一份该主题 CSS 并用 `composeCss([色向css, compactCss])` 叠加（同源：不前端改值）。
 * - 预设 / 微调衣服：`previewCss({...request, theme, density})`。
 */
export function createOutfitLoader({ loadProjectCss, previewCss, concurrency = 3 }: OutfitLoaderDeps): OutfitLoader {
  const cache = new Map<string, Promise<string>>()

  const load = (outfit: Outfit, opts: LoadOpts): Promise<string> => {
    const key = cacheKey(outfit, opts)
    const hit = cache.get(key)
    if (hit) return hit

    const task =
      outfit.kind === 'project' && outfit.projectId != null
        ? (async () => {
            const colorCss = await loadProjectCss(outfit.projectId as number, opts.theme)
            const useCompact = opts.density !== 'default' && !!outfit.densityTheme
            if (!useCompact) return colorCss
            const compactCss = await loadProjectCss(outfit.projectId as number, outfit.densityTheme as string)
            return composeCss([colorCss, compactCss])
          })()
        : previewCss(toPreviewInput(outfit.request, opts)).then((r) => r.css)

    cache.set(key, task)
    // 取数失败不缓存失败态：下次重试仍可命中后端（否则一次网络抖动会把这件衣服永久钉死）
    task.catch(() => cache.delete(key))
    return task
  }

  const loadMany = async (requests: readonly LoadRequest[]): Promise<string[]> => {
    const indexed = requests.map((req, index) => ({ req, index }))
    const out = new Array<string>(requests.length)
    await mapLimit(indexed, concurrency, async ({ req, index }) => {
      out[index] = await load(req.outfit, req)
    })
    return out
  }

  return { load, loadMany }
}