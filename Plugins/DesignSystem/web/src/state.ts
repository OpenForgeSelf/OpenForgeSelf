/**
 * 插件界面的共享状态（模块级单例 + Vue ref）。
 *
 * 为什么不用 pinia store：插件产物是远程加载的独立 ESM，pinia 实例必须由宿主注入才有作用域，
 * 而本界面只有"一个视图树"，用模块单例即可，且能在 vitest 里直接 import 断言（无需挂载 app）。
 *
 * 三条纪律：
 * 1. **数据只从后端来**：界面不再自带 fixture / localStorage 真相；localStorage 只当离线草稿（见 design/draft.ts）。
 * 2. **有效值一律来自 `tokens/effective`**：原始行含别名（`{semantic.brand}`），直接渲染会显示成花括号字符串。
 * 3. **换肤用后端导出的 CSS**：预览与交付同源，杜绝"界面好看、导出另一套"（这是 v1 玩具感的根因）。
 */
import { computed, ref, shallowRef } from 'vue'
import { api, type AuditView, type EffectiveView, type ExportFormats, type MetaInfo, type Project, type Theme } from './api'
import { cssVarName } from './design/derive'
import { ApiError, hasToken } from './http'

export type LoadState = 'idle' | 'loading' | 'ready' | 'error'

/** 后端自描述（能力面 + 版本三元组）。拿不到时界面必须显式降级，不能假装支持。 */
export const meta = shallowRef<MetaInfo | null>(null)
export const projects = shallowRef<Project[]>([])
export const currentProject = shallowRef<Project | null>(null)
export const themes = shallowRef<Theme[]>([])
export const exportFormats = shallowRef<ExportFormats | null>(null)

/** 当前预览主题（mode 轴上的一个取值，不是"另一套令牌"） */
export const themeCode = ref<string>('light')

/** 有效令牌视图缓存：themeCode → EffectiveView。切主题不该重复打后端。 */
const effectiveCache = new Map<string, EffectiveView>()
export const effective = shallowRef<EffectiveView | null>(null)
/**
 * 取数序号（有效值 / 换肤各一条）：快速切主题时会有多个请求同时在途，
 * **只有最新一次调用的响应才允许写回共享 ref**，否则"点了浅色但界面还是深色"——
 * 这条竞态不是理论问题，e2e 里同一份代码一跑失败一跑通过就是它（见 `state.test.ts`）。
 */
let effectiveSeq = 0
let skinSeq = 0
/** 审计视图（发布门禁的可见面） */
export const audit = shallowRef<AuditView | null>(null)

export const projectsState = ref<LoadState>('idle')
export const effectiveState = ref<LoadState>('idle')
export const lastError = ref<string>('')
/** 后端明确回了 401/403（token 过期或没权限），与"本地根本没 token"合并成一个可判定的空态 */
const rejectedByServer = ref(false)
export const unauthorized = computed(() => !hasToken() || rejectedByServer.value)

/** 后端能力判定：界面按能力显示入口，而不是按"我以为插件有"。 */
export function supports(capability: string): boolean {
  return (meta.value?.capabilities ?? []).includes(capability)
}

/** 主题列表里没有 light 时的兜底：取第一个色向主题，再兜共享层 */
function defaultTheme(): string {
  const colorThemes = themes.value.filter((t) => t.modeKind === 'color')
  return colorThemes.find((t) => t.code === 'light')?.code ?? colorThemes[0]?.code ?? 'shared'
}

function noteError(err: unknown): void {
  lastError.value = err instanceof Error ? err.message : String(err)
  if (err instanceof ApiError && err.isUnauthorized) rejectedByServer.value = true
}

export async function loadMeta(): Promise<void> {
  try {
    meta.value = await api.meta()
  } catch (err) {
    noteError(err)
  }
}

export async function loadProjects(): Promise<void> {
  if (projectsState.value === 'loading') return
  projectsState.value = 'loading'
  rejectedByServer.value = false
  try {
    projects.value = await api.listProjects()
    projectsState.value = 'ready'
    if (!currentProject.value && projects.value.length > 0) await selectProject(projects.value[0])
  } catch (err) {
    projectsState.value = 'error'
    noteError(err)
  }
}

export async function selectProject(project: Project): Promise<void> {
  currentProject.value = project
  effectiveCache.clear()
  effective.value = null
  audit.value = null
  try {
    themes.value = await api.listThemes(project.id)
    themeCode.value = defaultTheme()
    exportFormats.value = supports('export') ? await api.exportFormats(project.id) : null
    await loadEffective()
  } catch (err) {
    noteError(err)
  }
}

/** 新建项目后立即选中（后端返回的就是完整 ProjectDto） */
export async function createProject(input: { code: string; name: string; description?: string; kind?: string; seedText?: string }): Promise<Project | null> {
  try {
    const p = await api.createProject(input)
    await loadProjects()
    await selectProject(p)
    return p
  } catch (err) {
    noteError(err)
    return null
  }
}

export async function setTheme(code: string): Promise<void> {
  if (themeCode.value === code) return
  themeCode.value = code
  await loadEffective()
}

/** 拉某主题的有效值（命中缓存直接用；写入后必须 invalidate；**后到的旧响应只入缓存、不写界面**） */
export async function loadEffective(force = false): Promise<EffectiveView | null> {
  const project = currentProject.value
  if (!project) return null
  const key = themeCode.value
  if (!force && effectiveCache.has(key)) {
    effective.value = effectiveCache.get(key) ?? null
    return effective.value
  }
  const seq = ++effectiveSeq
  effectiveState.value = 'loading'
  try {
    const view = await api.effective(project.id, key)
    effectiveCache.set(key, view)
    if (seq !== effectiveSeq) return view      // 已有更新的一次取数在途/已完成：界面值由它写
    effective.value = view
    effectiveState.value = 'ready'
    return view
  } catch (err) {
    if (seq === effectiveSeq) {
      effectiveState.value = 'error'
      noteError(err)
    }
    return null
  }
}

/** 任何写操作之后调它：缓存失效 + 重拉当前主题（界面立刻自证写进去了） */
export async function invalidate(): Promise<void> {
  effectiveCache.clear()
  await loadEffective(true)
  const project = currentProject.value
  if (project) {
    try {
      currentProject.value = await api.getProject(project.id)
    } catch (err) {
      noteError(err)
    }
  }
}

export async function refreshAudit(run = false): Promise<void> {
  const project = currentProject.value
  if (!project) return
  try {
    if (run) await api.runAudit(project.id)
    audit.value = await api.audit(project.id)
  } catch (err) {
    noteError(err)
  }
}

/**
 * 换肤样式：直接用后端 `export?format=css` 的产物。
 * 预览与交付同源 —— 页面上看到的颜色就是导出文件里的颜色。
 */
export const skinCss = ref<string>('')
/**
 * 已注入投影**属于哪个主题**（''=没有投影）。
 *
 * 必须是状态而不是"看画布明暗"：投影只在预览页（`nav.skin`）注入，在别的页切主题不会重取，
 * 于是「界面选中的档」与「画布实际用的档」可以不一致（v2.6.7 的同步骤截图一暗一亮就是这个）。
 * 只有真正生效的那次取数才写它 —— 迟到的旧响应、失败、没有项目都要清，否则角标说谎。
 */
export const skinTheme = ref<string>('')
/** 画布当前显示的就是界面选中的档吗（角标与"待重取"提示共用这一条判据） */
export const skinApplied = computed(() => skinTheme.value !== '' && skinTheme.value === themeCode.value)

export async function loadSkin(): Promise<void> {
  const project = currentProject.value
  if (!project || !supports('export')) {
    skinSeq++                 // 没有项目/没有能力：把在途请求一并作废
    skinCss.value = ''
    skinTheme.value = ''
    return
  }
  const seq = ++skinSeq
  const theme = themeCode.value
  try {
    const css = await api.exportText(project.id, 'css', theme === 'shared' ? undefined : theme)
    if (seq !== skinSeq) return               // 期间又切过一次：迟到的旧投影必须丢弃，否则预览停在上一档
    skinCss.value = css
    skinTheme.value = css.trim() ? theme : ''
  } catch (err) {
    if (seq !== skinSeq) return
    skinCss.value = ''
    skinTheme.value = ''
    noteError(err)
  }
}

/**
 * 从投影 CSS 文本里取某个变量的值：`--ds-x` / `ds-x` / 令牌路径 `semantic.surface-bg` 三种写法都认。
 * 界面要显示"画布现在到底是什么值"时统一走这里，不在各处再写一遍正则。
 */
export function cssVar(css: string, nameOrPath: string): string {
  if (!css) return ''
  const varName = nameOrPath.startsWith('--')
    ? nameOrPath
    : nameOrPath.startsWith('ds-')
      ? `--${nameOrPath}`
      : cssVarName(nameOrPath)
  const escaped = varName.replace(/[-/\\^$*+?.()|[\]{}]/g, '\\$&')
  const m = new RegExp(`${escaped}\\s*:\\s*([^;}]+)`).exec(css)
  return m ? m[1].trim() : ''
}

/**
 * 顺着 `var(--x)` 引用在**同一份投影文档**里取到字面值（语义层是别名，原语层才写字面颜色）。
 * 这是取数不是重算：每一步的值都来自后端导出的那份 CSS。
 * 悬空引用或成环时原样返回当前文本（宁可显示 `var(--ds-…)`，也不编一个看着像颜色的东西）。
 */
export function resolveCssVar(css: string, nameOrPath: string, maxHops = 8): string {
  let current = cssVar(css, nameOrPath)
  const seen = new Set<string>()
  for (let hop = 0; hop < maxHops; hop++) {
    const m = /^var\(\s*(--[\w-]+)\s*\)$/.exec(current)
    if (!m || seen.has(m[1])) return current
    seen.add(m[1])
    const next = cssVar(css, m[1])
    if (!next) return current
    current = next
  }
  return current
}

/** 便于测试与界面复位（单例状态必须可清，否则用例互相污染） */
export function reset(): void {
  // 在途请求全部作废：换项目/重进界面后，上一轮的迟到响应不许写回这一轮的状态
  effectiveSeq++
  skinSeq++
  meta.value = null
  projects.value = []
  currentProject.value = null
  themes.value = []
  exportFormats.value = null
  effectiveCache.clear()
  effective.value = null
  audit.value = null
  skinCss.value = ''
  skinTheme.value = ''
  themeCode.value = 'light'
  projectsState.value = 'idle'
  effectiveState.value = 'idle'
  lastError.value = ''
  rejectedByServer.value = false
}

export const state = {
  meta,
  projects,
  currentProject,
  themes,
  themeCode,
  effective,
  audit,
  skinCss,
  skinTheme,
  skinApplied,
  lastError,
  unauthorized,
  supports,
}
