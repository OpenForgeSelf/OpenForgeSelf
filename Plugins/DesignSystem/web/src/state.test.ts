/**
 * 模块单例状态的并发纪律守卫（e2e 一次红一次绿之后补的）。
 *
 * 背景：切主题会触发 `loadEffective`/`loadSkin` 两个异步取数。它们的写法是"await 完直接赋值"，
 * 于是**先发但后到的旧请求会把新主题的结果覆盖掉** —— 表现就是"点了浅色，预览还是深色"。
 * 这条竞态在 e2e 里随机命中（同一份代码一跑失败一跑通过），单测里可以用受控 promise 稳定复现。
 *
 * 纪律：**任何"await 之后写共享 ref"的地方都要有序号守卫**（当前调用是不是最新一次）。
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'

const mocks = vi.hoisted(() => ({ exportText: vi.fn(), effective: vi.fn() }))

vi.mock('./api', async (importOriginal) => {
  const actual = await importOriginal<typeof import('./api')>()
  return { ...actual, api: { ...actual.api, exportText: mocks.exportText, effective: mocks.effective } }
})

import {
  cssVar,
  effective as effectiveView,
  effectiveState,
  loadEffective,
  loadSkin,
  meta,
  currentProject,
  reset,
  resolveCssVar,
  skinApplied,
  skinCss,
  skinTheme,
  themeCode,
} from './state'
import type { EffectiveView, MetaInfo, Project } from './api'

/** 手动可控的 promise：用来精确安排"谁先到、谁后到" */
function deferred<T>() {
  let resolve!: (v: T) => void
  const promise = new Promise<T>((r) => { resolve = r })
  return { promise, resolve }
}

function view(theme: string): EffectiveView {
  return { theme, count: 1, items: [{ path: `t.${theme}`, tier: 'semantic', type: 'color', value: theme }] } as unknown as EffectiveView
}

beforeEach(() => {
  reset()
  vi.clearAllMocks()
  meta.value = { pluginId: 'design-system', capabilities: ['export'], modelVersion: '9.9.9' } as unknown as MetaInfo
  currentProject.value = { id: 1, code: 'demo', name: 'Demo' } as unknown as Project
})

describe('切主题的取数竞态', () => {
  it('旧主题的后到时，不得覆盖新主题的换肤 CSS', async () => {
    const late = deferred<string>()
    mocks.exportText.mockImplementation((_id: number, _format: string, theme?: string) =>
      theme === 'dark' ? Promise.resolve('css-dark') : late.promise)

    themeCode.value = 'light'
    const first = loadSkin()          // light 的请求会**后**到
    themeCode.value = 'dark'
    const second = loadSkin()         // dark 的请求先到
    await second
    expect(skinCss.value).toBe('css-dark')

    late.resolve('css-light')
    await first                       // 后到的旧响应必须被丢弃
    expect(skinCss.value, '旧主题的迟到响应把预览覆盖回了 light = 点了深色但界面还是浅色').toBe('css-dark')
  })

  it('旧主题的后到时，不得覆盖新主题的有效令牌视图', async () => {
    const late = deferred<EffectiveView>()
    mocks.effective.mockImplementation((_id: number, theme?: string) =>
      theme === 'dark' ? Promise.resolve(view('dark')) : late.promise)

    themeCode.value = 'light'
    const first = loadEffective(true)
    themeCode.value = 'dark'
    const second = loadEffective(true)
    await second
    expect(effectiveView.value?.theme).toBe('dark')

    late.resolve(view('light'))
    await first
    expect(effectiveView.value?.theme, '有效令牌表被迟到响应换回 light 主题').toBe('dark')
    expect(effectiveState.value).toBe('ready')
  })

  it('reset 之后迟到的旧响应一律作废（换项目/重进界面不留残影）', async () => {
    const late = deferred<string>()
    mocks.exportText.mockReturnValue(late.promise)
    themeCode.value = 'light'
    const first = loadSkin()
    reset()
    late.resolve('stale')
    await first
    expect(skinCss.value).toBe('')
  })

  it('正常单飞路径仍然把结果写进状态（守卫不许把有效请求也丢掉）', async () => {
    mocks.exportText.mockResolvedValue('css-light')
    mocks.effective.mockResolvedValue(view('light'))
    await loadSkin()
    await loadEffective()
    expect(skinCss.value).toBe('css-light')
    expect(effectiveView.value?.theme).toBe('light')
  })
})

/**
 * 「画布现在是哪一档」必须是状态，不能只靠肉眼看明暗。
 *
 * 背景（v2.6.7 读图）：同一步骤的截图两次运行一暗一亮。投影只在 `nav.skin` 页注入，
 * 而非皮肤页切主题不会重取 —— 于是**界面选中的主题**和**画布实际用的主题**可以是两回事，
 * 而状态里只有 `skinCss`（一段文本），没有任何地方记"它是哪个主题来的"。
 * 结果既看不出真假，也没法在 e2e 里断言，截图证据因此不可复现。
 */
describe('投影主题身份（skinTheme / skinApplied）', () => {
  it('注入成功才记身份：skinTheme 等于取数时选中的主题', async () => {
    mocks.exportText.mockImplementation((_id: number, _f: string, theme?: string) =>
      Promise.resolve(`css-${theme ?? 'shared'}`))
    themeCode.value = 'dark'
    await loadSkin()
    expect(skinTheme.value).toBe('dark')
    expect(skinApplied.value, '选中档 == 已注入档，画布应当显示为已生效').toBe(true)
  })

  it('切了主题但没重取投影 → skinApplied 为假（这就是"选了深色画布还是浅色"）', async () => {
    mocks.exportText.mockResolvedValue('css-light')
    themeCode.value = 'light'
    await loadSkin()
    expect(skinApplied.value).toBe(true)
    themeCode.value = 'dark'          // setTheme 在非皮肤页不会触发 loadSkin
    expect(skinTheme.value).toBe('light')
    expect(skinApplied.value, '界面已到 dark、画布还停在 light 时必须能被判出来').toBe(false)
  })

  it('旧主题的后到时，不得把 skinTheme 一起换掉（身份必须跟着生效的那份 CSS 走）', async () => {
    const late = deferred<string>()
    mocks.exportText.mockImplementation((_id: number, _f: string, theme?: string) =>
      theme === 'dark' ? Promise.resolve('css-dark') : late.promise)
    themeCode.value = 'light'
    const first = loadSkin()
    themeCode.value = 'dark'
    await loadSkin()
    expect(skinTheme.value).toBe('dark')
    late.resolve('css-light')
    await first
    expect(skinTheme.value, '迟到响应把画布身份改回 light = 角标开始说谎').toBe('dark')
    expect(skinApplied.value).toBe(true)
  })

  it('投影取数失败 → 身份清空，不许残留上一档（否则角标显示深色而画布是外壳色）', async () => {
    mocks.exportText.mockResolvedValue('css-light')
    themeCode.value = 'light'
    await loadSkin()
    expect(skinTheme.value).toBe('light')
    mocks.exportText.mockRejectedValue(new Error('boom'))
    themeCode.value = 'compact'
    await loadSkin()
    expect(skinCss.value).toBe('')
    expect(skinTheme.value).toBe('')
    expect(skinApplied.value).toBe(false)
  })

  it('没有项目时 loadSkin 把身份一起清掉', async () => {
    mocks.exportText.mockResolvedValue('css-light')
    await loadSkin()
    expect(skinTheme.value).toBe('light')
    currentProject.value = null
    await loadSkin()
    expect(skinTheme.value).toBe('')
    expect(skinCss.value).toBe('')
  })
})

/**
 * 画布角标要从投影文本里**真取到值**，所以 `cssVar` 得是可靠的。
 * 它原本是"界面各处取色不再自己算"的声明式工具，却没有任何调用点、也没有测试，
 * 正则把 `--` 前缀拼错（`--${name.replace(/^ds-/,'')}` → `--semantic-…`），一条也取不到。
 */
describe('cssVar 从投影文本取变量值', () => {
  const css = '.ds-skin { --ds-semantic-surface-bg: #211f25;\n  --ds-space-4: 16px; }'

  it('三种写法都要取到同一个值（--ds-x / ds-x / 令牌路径）', () => {
    expect(cssVar(css, '--ds-semantic-surface-bg')).toBe('#211f25')
    expect(cssVar(css, 'ds-space-4')).toBe('16px')
    expect(cssVar(css, 'semantic.surface-bg')).toBe('#211f25')
  })

  it('取不到就回空串，不许回 undefined 或抛错（角标要显示 —）', () => {
    expect(cssVar(css, 'semantic.nope')).toBe('')
    expect(cssVar('', 'radius.lg')).toBe('')
  })

  it('末条声明没有分号也要取得到', () => {
    expect(cssVar('.ds-skin{--ds-radius-lg:20px}', 'radius.lg')).toBe('20px')
  })
})

/**
 * 投影里的语义层是**别名**（`--ds-semantic-surface-bg: var(--ds-color-neutral-950)`），
 * 原语层才写字面颜色。画布角标要显示"这块画布到底是什么色"，就得在同一份投影文档里把 var() 链跟到底 ——
 * 这是取数，不是在前端重算颜色（值仍然只有后端那一份）。
 */
describe('resolveCssVar 顺 var() 引用取字面值', () => {
  const aliased = '.ds-skin{--ds-semantic-surface-bg: var(--ds-color-neutral-950); --ds-color-neutral-950: #211f25; --ds-space-4: 16px;}'

  it('别名一跳取到原语字面值', () => {
    expect(resolveCssVar(aliased, 'semantic.surface-bg')).toBe('#211f25')
    expect(resolveCssVar(aliased, 'space.4')).toBe('16px')          // 本来就是字面值：不许多改
  })

  it('多跳也要跟到（component → semantic → primitive）', () => {
    const css = '.ds-skin{--ds-component-card-background: var(--ds-semantic-surface-1);--ds-semantic-surface-1: var(--ds-color-neutral-900);--ds-color-neutral-900: #26242c;}'
    expect(resolveCssVar(css, 'component.card.background')).toBe('#26242c')
  })

  it('取不到就回空串；引用悬空或成环时回原始声明文本（宁可显示 var(…) 也不编一个色）', () => {
    expect(resolveCssVar(aliased, 'semantic.nope')).toBe('')
    expect(resolveCssVar('.ds-skin{--ds-a: var(--ds-missing);}', 'a')).toBe('var(--ds-missing)')
    expect(resolveCssVar('.ds-skin{--ds-a: var(--ds-b);--ds-b: var(--ds-a);}', 'a')).toBe('var(--ds-b)')
  })
})
