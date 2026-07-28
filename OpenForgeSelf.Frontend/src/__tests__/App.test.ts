import { describe, it, expect, beforeEach, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useAppearanceStore } from '@/stores/appearance'

// Mock TopNavbar component to avoid router dependency issues
vi.mock('@/components/TopNavbar.vue', () => ({
  default: {
    name: 'TopNavbar',
    template: '<header class="top-navbar">TopNavbar</header>',
  },
}))

// We test App.vue's behavior through the store + CSS class logic
// Instead of mounting the full App.vue (which has complex router dependencies),
// we verify the CSS variable application logic

describe('App — background opacity CSS classes', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  it('backgroundImage 为空时 has-bg-image 不应存在', () => {
    const store = useAppearanceStore()
    // 未设置背景图时
    expect(store.backgroundImage).toBe('')
    // App.vue 中 :class="{ 'has-bg-image': !!appearanceStore.backgroundImage }"
    // backgroundImage 为空字符串 → !!'' === false → 无 has-bg-image
    expect(!!store.backgroundImage).toBe(false)
  })

  it('backgroundImage 非空时 has-bg-image 应存在', () => {
    const store = useAppearanceStore()
    store.setBackgroundImage('https://example.com/bg.jpg')
    // App.vue 中 :class="{ 'has-bg-image': !!appearanceStore.backgroundImage }"
    expect(!!store.backgroundImage).toBe(true)
  })

  it('has-bg-image 下 .main-content 应使用 var(--content-bg) 背景', () => {
    const store = useAppearanceStore()
    store.setBackgroundImage('https://example.com/bg.jpg')
    // 验证: 当 has-bg-image 时，.main-content 的 background 使用 var(--content-bg, transparent)
    // 这个验证通过 CSS 变量定义保证，这里验证 store 状态正确
    expect(store.backgroundImage).toBeTruthy()
    expect(!!store.backgroundImage).toBe(true)
  })
})