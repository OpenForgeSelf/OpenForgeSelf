/**
 * 四模式默认规则单测（AC6）：优先级 = 哈希 → 无项目 start → 存储值 → showroom。
 */
import { describe, expect, it } from 'vitest'
import { isMode, modeFromHash, resolveInitialMode, storeMode, MODES, MODE_LABELS } from './mode'

describe('MODE_LABELS / MODES', () => {
  it('四模式标签恰为 §U 契约的可访问名', () => {
    expect(MODES).toEqual(['start', 'showroom', 'workbench', 'delivery'])
    expect(MODE_LABELS).toEqual({
      start: '开始',
      showroom: '展厅',
      workbench: '工作台',
      delivery: '交付与接入',
    })
  })

  it('isMode 收窄合法值、拒绝非法值', () => {
    expect(isMode('showroom')).toBe(true)
    expect(isMode('Settings')).toBe(false)
    expect(isMode(undefined)).toBe(false)
    expect(isMode('')).toBe(false)
  })
})

describe('modeFromHash', () => {
  it('解析 #/mode 形态', () => {
    expect(modeFromHash('#/workbench')).toBe('workbench')
    expect(modeFromHash('#/showroom/dashboard?outfit=preset:x')).toBe('showroom')
    expect(modeFromHash('#/start')).toBe('start')
  })

  it('非法哈希一律 null（回落默认规则，不猜）', () => {
    expect(modeFromHash('')).toBe(null)
    expect(modeFromHash('#')).toBe(null)
    expect(modeFromHash('#/nope')).toBe(null)
    expect(modeFromHash('not-a-hash')).toBe(null)
  })
})

describe('resolveInitialMode（FR2 默认规则）', () => {
  it('哈希合法 → 用它（优先级最高，覆盖存储值）', () => {
    expect(resolveInitialMode({ hash: '#/delivery', hasProjects: true, stored: 'workbench' })).toBe('delivery')
  })

  it('无项目 → start（即使有存储值）', () => {
    expect(resolveInitialMode({ hash: '', hasProjects: false, stored: 'showroom' })).toBe('start')
    expect(resolveInitialMode({ hash: '', hasProjects: false, stored: null })).toBe('start')
  })

  it('有项目 + 合法存储值 → 用它', () => {
    expect(resolveInitialMode({ hash: '', hasProjects: true, stored: 'showroom' })).toBe('showroom')
    expect(resolveInitialMode({ hash: '', hasProjects: true, stored: 'workbench' })).toBe('workbench')
  })

  it('有项目 + 无存储值 → showroom', () => {
    expect(resolveInitialMode({ hash: '', hasProjects: true, stored: null })).toBe('showroom')
  })

  it('哈希非法（#/nope）不挡后续规则', () => {
    expect(resolveInitialMode({ hash: '#/nope', hasProjects: false, stored: null })).toBe('start')
    expect(resolveInitialMode({ hash: '#/nope', hasProjects: true, stored: null })).toBe('showroom')
  })
})

describe('storeMode / 持久化键', () => {
  it('写回 ds.mode（供页面刷新还原）', () => {
    storeMode('showroom')
    expect(localStorage.getItem('ds.mode')).toBe('showroom')
    storeMode('delivery')
    expect(localStorage.getItem('ds.mode')).toBe('delivery')
  })
})
