/**
 * 微调映射单测（AC8/AC11 共用的纯逻辑）。
 * 关键判据：① 微调叠加在预设之上且只改该改的；② 品牌色为空保留预设；③ 初值 == 预设值（不动控件 = 不改结果）。
 */
import { describe, expect, it } from 'vitest'
import type { GenerateRequest } from '../api'
import {
  DENSITY_OPTIONS,
  MOTION_OPTIONS,
  RADIUS_DEFAULT,
  RADIUS_MAX,
  RADIUS_MIN,
  axisFields,
  axesFromPreset,
  defaultTune,
  isValidBrandColor,
  tuneFromPreset,
  tuneToRequest,
} from './tune'

const preset: GenerateRequest = {
  hue: 240,
  chroma: 0.12,
  density: 'compact',
  typeRatio: 1.15,
  radiusBase: 4,
  motionScale: 0.8,
  seedColor: '#3366ff',
  industry: 'finance',
  themes: ['light', 'dark'],
}

describe('tuneFromPreset', () => {
  it('初值取自预设 request（滑块显示真实值，不拍默认）', () => {
    expect(tuneFromPreset(preset)).toEqual({
      seedColor: '#3366ff',
      density: 'compact',
      radiusBase: 4,
      motionScale: 0.8,
      axes: {},
    })
  })

  it('无预设 → 通用默认', () => {
    expect(tuneFromPreset(null)).toEqual(defaultTune())
    expect(defaultTune()).toEqual({ seedColor: '', density: 'default', radiusBase: RADIUS_DEFAULT, motionScale: 1, axes: {} })
  })

  it('预设缺字段 → 该项回落默认', () => {
    expect(tuneFromPreset({ hue: 10 })).toEqual(defaultTune())
  })
})

describe('tuneToRequest', () => {
  it('不动控件（初值 == 预设值）→ 结果与预设逐字一致', () => {
    const out = tuneToRequest(preset, tuneFromPreset(preset))
    expect(out).toEqual(preset)
  })

  it('改动圆润度/疏密/动效 → 只覆盖这三项，其余沿用预设', () => {
    const out = tuneToRequest(preset, { ...tuneFromPreset(preset), radiusBase: 12, density: 'comfortable', motionScale: 1.2 })
    expect(out.radiusBase).toBe(12)
    expect(out.density).toBe('comfortable')
    expect(out.motionScale).toBe(1.2)
    // 未动项仍来自预设
    expect(out.hue).toBe(240)
    expect(out.chroma).toBe(0.12)
    expect(out.typeRatio).toBe(1.15)
    expect(out.industry).toBe('finance')
    expect(out.seedColor).toBe('#3366ff')
    expect(out.themes).toEqual(['light', 'dark'])
  })

  it('品牌色为空 → 保留预设种子色（不是清空）', () => {
    const out = tuneToRequest(preset, { ...tuneFromPreset(preset), seedColor: '   ' })
    expect(out.seedColor).toBe('#3366ff')
  })

  it('品牌色有值 → 覆盖预设种子色', () => {
    const out = tuneToRequest(preset, { ...tuneFromPreset(preset), seedColor: '#ff8800' })
    expect(out.seedColor).toBe('#ff8800')
  })

  it('无预设 + 空品牌色 → 不产生 seedColor 键', () => {
    const out = tuneToRequest(null, defaultTune())
    expect('seedColor' in out).toBe(false)
    expect(out.density).toBe('default')
    expect(out.radiusBase).toBe(RADIUS_DEFAULT)
  })
})

describe('isValidBrandColor', () => {
  it('接受 #rgb / #rrggbb 与空串', () => {
    expect(isValidBrandColor('')).toBe(true)
    expect(isValidBrandColor('  ')).toBe(true)
    expect(isValidBrandColor('#fff')).toBe(true)
    expect(isValidBrandColor('#3366FF')).toBe(true)
  })

  it('拒绝非十六进制/长度不对/无井号', () => {
    expect(isValidBrandColor('#ff')).toBe(false)
    expect(isValidBrandColor('#fffffff')).toBe(false)
    expect(isValidBrandColor('3366ff')).toBe(false)
    expect(isValidBrandColor('red')).toBe(false)
  })
})

describe('档位词表（§U 契约文案）', () => {
  it('疏密三档文案 = 舒展/适中/紧凑', () => {
    expect(DENSITY_OPTIONS.map((d) => d.label)).toEqual(['舒展', '适中', '紧凑'])
  })

  it('动效三档 = 克制/适中/活泼 对应 0.8/1/1.2', () => {
    expect(MOTION_OPTIONS.map((m) => [m.label, m.value])).toEqual([
      ['克制', 0.8],
      ['适中', 1],
      ['活泼', 1.2],
    ])
  })

  it('圆润度范围 2–16', () => {
    expect([RADIUS_MIN, RADIUS_MAX]).toEqual([2, 16])
  })
})

/**
 * 风格轴（M3 AC10）。判据落在"叠加语义"上，不落在取值词表上：
 * 词表必须来自 `meta.styleAxes`（`design/vocabulary.test.ts` 盯着界面别抄一份），
 * 所以这里的字段名与取值都是测试自己造的样例值。
 */
describe('风格轴叠加（M3）', () => {
  const fields = ['shadowStyle', 'fontPairing', 'radiusStyle', 'shadowStrength']
  const withAxes: GenerateRequest = { ...preset, shadowStyle: 'crisp', fontPairing: 'editorial', shadowStrength: 1.4 }

  it('axisFields 原样沿用 meta 给的字段与顺序（前端不重排、不补默认）', () => {
    expect(axisFields(undefined)).toEqual([])
    expect(axisFields([{ field: 'radiusStyle' }, { field: 'shadowStyle' }])).toEqual(['radiusStyle', 'shadowStyle'])
  })

  it('axesFromPreset 只挑声明过的字段，别的一律不带进来', () => {
    expect(axesFromPreset(withAxes, fields)).toEqual({ shadowStyle: 'crisp', fontPairing: 'editorial', shadowStrength: 1.4 })
    expect(axesFromPreset(withAxes, [])).toEqual({})
    expect(axesFromPreset(null, fields)).toEqual({})
  })

  it('不传 fields 时旧调用方行为不变（预设里没有的键不会凭空冒出来）', () => {
    expect(tuneFromPreset(withAxes).axes).toEqual({})
  })

  it('不动控件（初值 == 预设值）→ 带轴的预设也逐字一致', () => {
    const out = tuneToRequest(withAxes, tuneFromPreset(withAxes, fields))
    expect(out).toEqual(withAxes)
  })

  it('用户改一条轴 → 只覆盖那一条，其余轴与预设参数都不动', () => {
    const tune = { ...tuneFromPreset(withAxes, fields), axes: { shadowStyle: 'flat', fontPairing: 'editorial', shadowStrength: 1.4 } }
    const out = tuneToRequest(withAxes, tune)
    expect(out.shadowStyle).toBe('flat')
    expect(out.fontPairing).toBe('editorial')
    expect(out.hue).toBe(preset.hue)
    expect(out.density).toBe(tune.density)
  })

  it('空值不写进 request（后端按 null = 默认处理，空串会被当成一个取值）', () => {
    const out = tuneToRequest(preset, { ...tuneFromPreset(preset, fields), axes: { shadowStyle: '' } })
    expect(out.shadowStyle).toBeUndefined()
  })
})
