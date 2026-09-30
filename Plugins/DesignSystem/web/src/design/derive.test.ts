// 展示层纯函数单测（跑在宿主 vitest：include 里已含 Plugins/<id>/web/src 下的 *.test.ts）。
//
// 重点盯两类会真出事的地方：
// 1. `cssVarName` 与后端 `ExportService.CssVarName` 必须同规则，否则换肤别名指向空变量（表现为颜色整片透明）；
// 2. 对比度 -1 的含义是"后端没测"，不能显示成 `1:1` 或 `0:1` 误导设计同学。
import { describe, expect, it } from 'vitest'
import type { EffectiveToken } from '../api'
import {
  aliasTarget,
  axisValues,
  colorFamilies,
  compareSteps,
  cssVarName,
  groupByRoot,
  isColorTheme,
  matchesKeyword,
  nextVersion,
  rampSteps,
  ratioText,
  severityClass,
  stepOf,
  tierRank,
  valueSummary,
  variantJsonOf,
  wcagBadge,
} from './derive'

const eff = (path: string, value: string, type = 'color', alias?: string): EffectiveToken => ({
  path,
  tier: path.startsWith('semantic.') ? 'semantic' : 'primitive',
  type,
  value,
  sourcePath: path,
  aliasPath: alias ?? null,
  resolved: true,
  error: null,
  colorHex: null,
  contrastRatio: -1,
  wcagLevel: null,
})

describe('cssVarName 与后端投影同规则', () => {
  it('点全部换成连字符并加 --ds- 前缀', () => {
    // 后端：static String CssVarName(String path) => "--ds-" + path.Replace('.', '-');
    expect(cssVarName('semantic.text-1')).toBe('--ds-semantic-text-1')
    expect(cssVarName('color.brand.500')).toBe('--ds-color-brand-500')
    expect(cssVarName('space.3')).toBe('--ds-space-3')
    expect(cssVarName('component.focus.outline-width')).toBe('--ds-component-focus-outline-width')
  })
})

describe('令牌分组与色阶', () => {
  const items = [
    eff('color.brand.500', '#7c3aed'),
    eff('color.brand.50', '#f5f3ff'),
    eff('color.brand.900', '#2e1065'),
    eff('color.neutral.100', '#f4f4f5'),
    eff('semantic.brand', '#7c3aed', 'color', 'color.brand.500'),
    eff('space.4', '16px', 'dimension'),
  ]

  it('按根段分组并按路径排序', () => {
    const groups = groupByRoot(items)
    expect(groups.map((g) => g.root)).toEqual(['color', 'semantic', 'space'])
    expect(groups[0].items.map((i) => i.path)).toEqual([
      'color.brand.50',
      'color.brand.500',
      'color.brand.900',
      'color.neutral.100',
    ])
  })

  it('色族顺序取自传入词表：表外的族排最后按字母序，词表为空则整体字母序', () => {
    const order = ['brand', 'accent', 'neutral', 'success', 'warning', 'danger', 'info']
    expect(colorFamilies(items, order)).toEqual(['brand', 'neutral'])
    // 族内的档位仍按数值升序（族序来自词表，档序来自档号 —— 两件事各管各的）
    expect(rampSteps(items, 'brand').map((t) => t.path)).toEqual(['color.brand.50', 'color.brand.500', 'color.brand.900'])
    // 后端把顺序改了，界面必须跟着改 —— 这条就是"界面没有自己抄一份词表"的可执行证明
    expect(colorFamilies(items, ['neutral', 'brand'])).toEqual(['neutral', 'brand'])
    // 用户手工加的一族（color.custom.*）要看得见，但不替它编顺序
    const withCustom = [...items, eff('color.custom.500', '#123456')]
    expect(colorFamilies(withCustom, order)).toEqual(['brand', 'neutral', 'custom'])
    // /meta 还没回来：不假装知道顺序，退回字母序（稳定、可解释）
    expect(colorFamilies(withCustom, [])).toEqual(['brand', 'custom', 'neutral'])
  })

  it('层级排序读传入词表：词表外的层级排最后', () => {
    const tiers = ['primitive', 'semantic', 'component']
    expect(tierRank('primitive', tiers)).toBe(0)
    expect(tierRank('component', tiers)).toBe(2)
    expect(tierRank('nonsense', tiers)).toBe(tiers.length)
    expect(tierRank('component', ['component', 'primitive'])).toBe(0)
  })

  it('轴清单按名字取档位：没有这条轴就返回空数组（不猜）', () => {
    const axes = [
      { axis: 'size', values: ['xs', 'sm', 'md', 'lg', 'xl'] },
      { axis: 'role', values: ['primary', 'secondary'] },
    ]
    expect(axisValues(axes, 'role')).toEqual(['primary', 'secondary'])
    expect(axisValues(axes, 'state')).toEqual([])
    expect(axisValues([], 'state')).toEqual([])
  })

  it('按轴拼 variantJson：一条轴一个值，空值退回 {}，引号要转义', () => {
    expect(variantJsonOf('size', 'md')).toBe('{"size":"md"}')
    expect(variantJsonOf('', 'md')).toBe('{}')
    expect(variantJsonOf('size', '')).toBe('{}')
    expect(variantJsonOf('size', 'a"b')).toBe('{"size":"a\\"b"}')
    // 拼出来的必须能被 JSON.parse 认（后端按 canonical JSON 解析，坏串会整批被写路径拒掉）
    expect(() => JSON.parse(variantJsonOf('role', 'primary'))).not.toThrow()
  })
})

describe('别名与复合值的可读显示', () => {
  it('aliasPath 优先，其次解 {path} 形式的值', () => {
    expect(aliasTarget({ aliasPath: 'color.brand.500', value: null })).toBe('color.brand.500')
    expect(aliasTarget({ aliasPath: null, value: '{semantic.brand}' })).toBe('semantic.brand')
    expect(aliasTarget({ aliasPath: null, value: '#7c3aed' })).toBe('')
  })

  it('shadow 数组值给层数摘要，typography 对象值给键摘要', () => {
    const shadow = eff('shadow.elevation-2', '[{"x":0},{"x":1},{"x":2}]', 'shadow')
    expect(valueSummary(shadow)).toBe('3 层')
    const typo = eff('type.body', '{"fontSize":"16px","lineHeight":"24px"}', 'typography')
    expect(valueSummary(typo)).toContain('fontSize: 16px')
  })

  it('解析不了的 JSON 原样返回，不抛异常', () => {
    expect(valueSummary(eff('type.body', '{broken', 'typography'))).toBe('{broken')
  })
})

describe('对比度显示', () => {
  it('-1 是未测，不是 1:1', () => {
    expect(ratioText(-1)).toBe('未测')
    expect(wcagBadge(-1).level).toBe('unknown')
  })

  it('按 WCAG 2.2 阈值分档', () => {
    expect(wcagBadge(7.2).label).toBe('AAA')
    expect(wcagBadge(4.6).label).toBe('AA')
    expect(wcagBadge(3.2).label).toBe('仅大字/图形')
    expect(wcagBadge(2.9).label).toBe('不达标')
  })

  it('严重级只分三档，其余按 info', () => {
    expect(severityClass('critical')).toBe('critical')
    expect(severityClass('warning')).toBe('warning')
    expect(severityClass('note')).toBe('info')
  })
})

describe('筛选与主题轴', () => {
  it('关键词大小写不敏感、空串不过滤', () => {
    expect(matchesKeyword('Semantic.Text-1', 'text')).toBe(true)
    expect(matchesKeyword('semantic.text-1', '')).toBe(true)
    expect(matchesKeyword('semantic.text-1', 'space')).toBe(false)
  })

  it('density 轴不当配色主题', () => {
    expect(isColorTheme('color')).toBe(true)
    expect(isColorTheme('density')).toBe(false)
  })
})

describe('版本号推进', () => {
  it('默认只动 patch，缺段自动补 0', () => {
    expect(nextVersion('1.2.3')).toBe('1.2.4')
    expect(nextVersion('1.2', 'minor')).toBe('1.3.0')
    expect(nextVersion('1.2.3', 'minor')).toBe('1.3.0')
    expect(nextVersion('', 'major')).toBe('1.0.0')
    expect(nextVersion('2.0.0', 'patch')).toBe('2.0.1')
  })
})

describe('档位序 compareSteps（词表由后端 /meta 供给）', () => {
  const sorted = (paths: string[], order: string[]): string[] =>
    [...paths].sort((a, b) => compareSteps(a, b, order))

  it('命名档按后端词表排，不是字典序', () => {
    const radius = ['xs', 'sm', 'md', 'lg', 'xl', '2xl']
    expect(sorted(['radius.2xl', 'radius.md', 'radius.xs'], radius)).toEqual(['radius.xs', 'radius.md', 'radius.2xl'])
  })

  it('数值档按数值排（space.10 不能在 space.2 前面）', () => {
    expect(sorted(['space.10', 'space.2', 'space.1'], ['1', '2', '10'])).toEqual(['space.1', 'space.2', 'space.10'])
  })

  it('词表外的档名排到最后并按字典序；空词表时命名档退回字典序', () => {
    expect(sorted(['radius.pill', 'radius.xs'], ['xs', 'sm'])).toEqual(['radius.xs', 'radius.pill'])
    expect(sorted(['border.thick', 'border.hairline'], [])).toEqual(['border.hairline', 'border.thick'])
  })

  it('breakpoint / z-index 这类纯数值档不依赖词表也有确定序', () => {
    expect(sorted(['breakpoint.10', 'breakpoint.2'], [])).toEqual(['breakpoint.2', 'breakpoint.10'])
  })
})

describe('档名里的数字陷阱（e2e 抓到的真缺陷）', () => {
  const sorted = (paths: string[], order: string[]): string[] =>
    [...paths].sort((a, b) => compareSteps(a, b, order))

  it('`radius.2xl` 的 "2" 不是档位号：不能被读成数值 2 插进命名档中间', () => {
    const radius = ['xs', 'sm', 'md', 'lg', 'xl', '2xl', 'pill', 'full']
    expect(sorted(['radius.2xl', 'radius.pill', 'radius.xs', 'radius.full'], radius))
      .toEqual(['radius.xs', 'radius.2xl', 'radius.pill', 'radius.full'])
  })

  it('阴影层号仍按数值排（shadow.10 在 shadow.2 之后）', () => {
    expect(sorted(['shadow.10', 'shadow.2', 'shadow.1'], [])).toEqual(['shadow.1', 'shadow.2', 'shadow.10'])
  })

  it('stepOf 只认整段数字', () => {
    expect(stepOf('space.4')).toBe(4)
    expect(stepOf('radius.2xl')).toBe(0)
    expect(stepOf('border.hairline')).toBe(0)
  })
})
