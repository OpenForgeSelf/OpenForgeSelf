import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import {
  useAppearanceStore,
  APPEARANCE_BG_OPACITY_KEY,
  clampOpacity,
} from '@/stores/appearance'

describe('clampOpacity', () => {
  it('should keep 80 as 80', () => {
    expect(clampOpacity(80)).toBe(80)
  })

  it('should clamp 999 to 100', () => {
    expect(clampOpacity(999)).toBe(100)
  })

  it('should clamp -1 to 0', () => {
    expect(clampOpacity(-1)).toBe(0)
  })

  it('should round 45.7 to 46', () => {
    expect(clampOpacity(45.7)).toBe(46)
  })

  it('should round 45.3 to 45', () => {
    expect(clampOpacity(45.3)).toBe(45)
  })
})

describe('Appearance Store — backgroundOpacity', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  it('backgroundOpacity 默认值应为 60', () => {
    const store = useAppearanceStore()
    expect(store.backgroundOpacity).toBe(60)
  })

  it('setBackgroundOpacity(80) 应更新值为 80', () => {
    const store = useAppearanceStore()
    store.setBackgroundOpacity(80)
    expect(store.backgroundOpacity).toBe(80)
  })

  it('setBackgroundOpacity(999) 应 clamp 到 100', () => {
    const store = useAppearanceStore()
    store.setBackgroundOpacity(999)
    expect(store.backgroundOpacity).toBe(100)
  })

  it('setBackgroundOpacity(-1) 应 clamp 到 0', () => {
    const store = useAppearanceStore()
    store.setBackgroundOpacity(-1)
    expect(store.backgroundOpacity).toBe(0)
  })

  it('setBackgroundOpacity 应写入 localStorage 键 forgeself.appearance.backgroundOpacity', () => {
    const store = useAppearanceStore()
    store.setBackgroundOpacity(80)
    expect(localStorage.getItem(APPEARANCE_BG_OPACITY_KEY)).toBe('80')
  })

  it('initialize() 应从 localStorage 读取背景图透明度', () => {
    localStorage.setItem(APPEARANCE_BG_OPACITY_KEY, '75')
    const store = useAppearanceStore()
    store.initialize()
    expect(store.backgroundOpacity).toBe(75)
  })

  it('initialize() 应在 localStorage 无值时使用默认值 60', () => {
    const store = useAppearanceStore()
    store.initialize()
    expect(store.backgroundOpacity).toBe(60)
  })

  it('clearBackgroundImage() 不应清除 backgroundOpacity 值', () => {
    const store = useAppearanceStore()
    store.setBackgroundImage('https://example.com/bg.jpg')
    store.setBackgroundOpacity(80)
    store.clearBackgroundImage()
    expect(store.backgroundOpacity).toBe(80)
    expect(store.backgroundImage).toBe('')
  })

  it('重新 setBackgroundImage() 后背景图透明度不受影响', () => {
    const store = useAppearanceStore()
    store.setBackgroundOpacity(80)
    store.clearBackgroundImage()
    store.setBackgroundImage('https://example.com/new-bg.jpg')
    expect(store.backgroundOpacity).toBe(80)
    expect(store.backgroundImage).toBe('https://example.com/new-bg.jpg')
  })

  it('从未设置过透明度时，首次设置背景图透明度为默认值 60', () => {
    const store = useAppearanceStore()
    expect(store.backgroundOpacity).toBe(60)
    store.setBackgroundImage('https://example.com/bg.jpg')
    expect(store.backgroundOpacity).toBe(60)
  })

  it('setBackgroundOpacity 浮点数应取整（45.7 → 46）', () => {
    const store = useAppearanceStore()
    store.setBackgroundOpacity(45.7)
    expect(store.backgroundOpacity).toBe(46)
  })

  it('setBackgroundOpacity 浮点数应取整（45.3 → 45）', () => {
    const store = useAppearanceStore()
    store.setBackgroundOpacity(45.3)
    expect(store.backgroundOpacity).toBe(45)
  })

  it('initialize() 在 localStorage 读取失败时回退到默认值 60', () => {
    // 模拟 localStorage 不可用（getStorage 返回 null）
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('localStorage unavailable')
    })
    const store = useAppearanceStore()
    store.initialize()
    expect(store.backgroundOpacity).toBe(60)
    vi.restoreAllMocks()
  })
})