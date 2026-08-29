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

describe('TopNavbar — background opacity', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  it('未设置背景图时使用 var(--topnav-bg,var(--el-bg-color)) 背景（不透明回退）', () => {
    const wrapper = mount(TopNavbar)
    const header = wrapper.find('header')
    expect(header.exists()).toBe(true)
    // 默认无背景图时，header 应使用 bg-[var(--topnav-bg,var(--el-bg-color))]
    expect(header.classes()).toContain('bg-[var(--topnav-bg,var(--el-bg-color))]')
  })

  it('设置背景图后 TopNavbar 使用 var(--topnav-bg) 背景（半透明）', () => {
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
    // TopNavbar 通过 CSS 变量 var(--topnav-bg,var(--el-bg-color)) 驱动
    // App.vue 的 .has-bg-image 设置 --topnav-bg 为半透明值
    expect(header.classes()).toContain('bg-[var(--topnav-bg,var(--el-bg-color))]')
  })
})