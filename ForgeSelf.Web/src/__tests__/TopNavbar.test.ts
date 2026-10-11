import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import TopNavbar from '@/components/TopNavbar.vue'
import { useAppearanceStore } from '@/stores/appearance'

// Mock router
vi.mock('vue-router', () => ({
  useRouter: () => ({ push: vi.fn() }),
  useRoute: () => ({ path: '/' }),
}))

describe('TopNavbar — 设计稿玻璃壳层（DESIGN.md Components/TopBar）', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  it('默认渲染 48px 玻璃条（.topbar 类，不再依赖 --topnav-bg 实底）', () => {
    const wrapper = mount(TopNavbar)
    const header = wrapper.find('header')
    expect(header.exists()).toBe(true)
    // 新壳层：统一玻璃条类名 .topbar（kit-styles.css:32-43），背景由 .topbar 样式驱动
    expect(header.classes()).toContain('topbar')
  })

  it('品牌区渲染 AppLogo（/logo/logo-128.png）与「铸己匣」', () => {
    const wrapper = mount(TopNavbar)
    const brandImg = wrapper.find('header .brand img')
    expect(brandImg.exists()).toBe(true)
    // AppLogo 组件渲染 /logo/logo-128.png（v2.4.2 起 logo 保持原样，不再用 /brand/logo-seal.svg）
    const src = brandImg.attributes('src') ?? ''
    expect(src === '/logo/logo-128.png' || src.startsWith('data:image/')).toBe(true)
    expect(wrapper.find('header .brand .name').text()).toBe('铸己匣')
  })

  it('设置背景图后仍为玻璃条 .topbar（玻璃样式不随背景图模式切换实底）', () => {
    const store = useAppearanceStore()
    store.setBackgroundImage('https://example.com/bg.jpg')

    const wrapper = mount(TopNavbar, {
      global: {
        provide: {
          appearanceStore: store,
        },
      },
    })
    const header = wrapper.find('header')
    expect(header.exists()).toBe(true)
    expect(header.classes()).toContain('topbar')
  })

  it('顶部存在「背景设置」按钮（开合背景面板）', () => {
    const wrapper = mount(TopNavbar)
    const bgBtn = wrapper.find('header .actions button[title="背景设置"]')
    expect(bgBtn.exists()).toBe(true)
  })
})
