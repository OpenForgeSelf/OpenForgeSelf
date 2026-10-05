/**
 * 展厅视图档单测（2026-10-04 输入22：「应该缩放，或者可以最大化，或者可以自由调尺寸，并且有滚动条，不能只看到部分」）。
 *
 * 只钉两件"只有跑起来才知道对不对"的事：
 * ① `fitScale` 的数学——缩放比一旦能超过 1 或落到 0，预览要么失真要么整个压没；
 * ② 三档的可访问名与持久化回落——e2e 按名点 radio，存储里躺着脏值时页面不能崩成空白。
 */
import { beforeEach, describe, expect, it } from 'vitest'
import {
  VIEW_MODES,
  VIEW_STORAGE_KEY,
  fitScale,
  parseViewMode,
  readStoredView,
  storeView,
} from './fit'
import { deviceById } from './scenes'

describe('fitScale（适应档缩放比）', () => {
  it('可用宽小于稿宽 → 等比缩到刚好放下', () => {
    expect(fitScale(648, 1280)).toBeCloseTo(0.50625, 5)
    expect(fitScale(390, 390)).toBe(1)
  })

  it('可用宽富余 → 恒为 1，绝不放大（放大让字距与留白失真）', () => {
    expect(fitScale(1920, 1280)).toBe(1)
    expect(fitScale(9999, 390)).toBe(1)
  })

  it('可用宽拿不到（0 / 负 / NaN / Infinity）→ 回落 1 交给滚动条，压成 0 是更坏的失败', () => {
    expect(fitScale(0, 1280)).toBe(1)
    expect(fitScale(-500, 1280)).toBe(1)
    expect(fitScale(Number.NaN, 1280)).toBe(1)
    expect(fitScale(Number.POSITIVE_INFINITY, 1280)).toBe(1)
    expect(fitScale(648, 0)).toBe(1)
    expect(fitScale(648, Number.NaN)).toBe(1)
  })

  it('极窄可用宽 → 是正数且小于 1，不为 0、不为负', () => {
    const k = fitScale(1, 1280)
    expect(k).toBeGreaterThan(0)
    expect(k).toBeLessThan(1)
  })

  it('三档设备各自在窄列下的比值都落在 (0, 1]（含移动档不放大）', () => {
    for (const id of ['desktop', 'tablet', 'mobile']) {
      const width = deviceById(id)!.width
      const k = fitScale(648, width)
      expect(k).toBeGreaterThan(0)
      expect(k).toBeLessThanOrEqual(1)
    }
    expect(fitScale(648, deviceById('mobile')!.width)).toBe(1)
  })
})

describe('VIEW_MODES（radio 名即 e2e 判据）', () => {
  it('恰为「适应 / 1:1 / 最大化」三档且顺序固定', () => {
    expect(VIEW_MODES.map((m) => m.label)).toEqual(['适应', '1:1', '最大化'])
    expect(VIEW_MODES.map((m) => m.id)).toEqual(['fit', 'actual', 'max'])
  })

  it('每档都有给用户的提示（title 不能是空串）', () => {
    for (const m of VIEW_MODES) expect(m.hint.length).toBeGreaterThan(0)
  })
})

describe('parseViewMode（脏值不崩）', () => {
  it('只认三个已知档', () => {
    expect(parseViewMode('fit')).toBe('fit')
    expect(parseViewMode('actual')).toBe('actual')
    expect(parseViewMode('max')).toBe('max')
  })

  it('其余一律回落 fit（手改存储、旧版本残留、非字符串）', () => {
    for (const raw of ['', 'FIT', 'zoom', 'full', null, undefined, 3, {}, '[]']) {
      expect(parseViewMode(raw)).toBe('fit')
    }
  })
})

describe('readStoredView / storeView（刷新不丢档）', () => {
  beforeEach(() => localStorage.clear())

  it('写入即可读回，三档逐一 round-trip', () => {
    for (const mode of ['fit', 'actual', 'max'] as const) {
      storeView(mode)
      expect(readStoredView()).toBe(mode)
      expect(localStorage.getItem(VIEW_STORAGE_KEY)).toBe(mode)
    }
  })

  it('无存储 → 默认适应档', () => {
    expect(readStoredView()).toBe('fit')
  })

  it('存储里躺着脏值 → 回落适应档而不是原样返回', () => {
    localStorage.setItem(VIEW_STORAGE_KEY, 'maximise')
    expect(readStoredView()).toBe('fit')
  })
})
