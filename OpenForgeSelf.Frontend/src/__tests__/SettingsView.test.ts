import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import SettingsView from '@/views/SettingsView.vue'
import { useThemeStore, ROOT_THEME_ATTRIBUTE, ROOT_THEME_MODE_ATTRIBUTE, THEME_STORAGE_KEY } from '@/stores/theme'

function createMatchMediaMock(prefersDark = false) {
  const listeners = new Set<(event: MediaQueryListEvent) => void>()
  return {
    matches: prefersDark,
    media: '(prefers-color-scheme: dark)',
    addEventListener: vi.fn((type: string, listener: EventListenerOrEventListenerObject) => {
      if (type === 'change' && typeof listener === 'function') {
        listeners.add(listener as (event: MediaQueryListEvent) => void)
      }
    }),
    removeEventListener: vi.fn((type: string, listener: EventListenerOrEventListenerObject) => {
      if (type === 'change' && typeof listener === 'function') {
        listeners.delete(listener as (event: MediaQueryListEvent) => void)
      }
    }),
    dispatch(matches: boolean) {
      this.matches = matches
      listeners.forEach((listener) => listener({ matches, media: this.media } as MediaQueryListEvent))
    },
  }
}

describe('SettingsView', () => {
  let matchMediaMock: ReturnType<typeof createMatchMediaMock>

  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.documentElement.removeAttribute(ROOT_THEME_ATTRIBUTE)
    document.documentElement.removeAttribute(ROOT_THEME_MODE_ATTRIBUTE)
    document.documentElement.style.colorScheme = ''

    matchMediaMock = createMatchMediaMock(false)
    Object.defineProperty(window, 'matchMedia', {
      writable: true,
      value: vi.fn(() => matchMediaMock),
    })
  })

  afterEach(() => {
    const store = useThemeStore()
    store.dispose()
  })

  function mountView() {
    return mount(SettingsView, {
      attachTo: document.body,
      global: {
        stubs: {
          ForgeSwitch: true,
          ForgeSelect: true,
          ForgeButton: true,
          ForgeCard: {
            template: '<div class="forge-card"><slot /></div>',
          },
        },
      },
    })
  }

  it('默认渲染通用设置面板', () => {
    const wrapper = mountView()
    expect(wrapper.find('.settings-view').exists()).toBe(true)
    expect(wrapper.find('.settings-nav').exists()).toBe(true)
    expect(wrapper.findAll('.settings-nav__item').length).toBe(6)
  })

  it('导航切换激活分类', async () => {
    const wrapper = mountView()
    const navItems = wrapper.findAll('.settings-nav__item')

    await navItems[3].trigger('click')
    await wrapper.vm.$nextTick()

    expect(navItems[3].classes()).toContain('settings-nav__item--active')
  })

  it('外观面板渲染主题切换按钮', async () => {
    const wrapper = mountView()
    const navItems = wrapper.findAll('.settings-nav__item')

    await navItems[3].trigger('click')
    await wrapper.vm.$nextTick()

    const buttons = wrapper.findAll('.theme-toggle-btn')
    expect(buttons.length).toBe(3)
    expect(buttons[0].text()).toBe('浅色')
    expect(buttons[1].text()).toBe('深色')
    expect(buttons[2].text()).toBe('跟随系统')
  })

  it('点击浅色按钮设置主题为浅色', async () => {
    const wrapper = mountView()
    const store = useThemeStore()
    store.initialize()

    const navItems = wrapper.findAll('.settings-nav__item')
    await navItems[3].trigger('click')
    await wrapper.vm.$nextTick()

    const buttons = wrapper.findAll('.theme-toggle-btn')
    await buttons[0].trigger('click')

    expect(store.mode).toBe('light')
    expect(store.resolvedTheme).toBe('light')
    expect(document.documentElement.getAttribute(ROOT_THEME_ATTRIBUTE)).toBe('light')
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('light')
  })

  it('点击深色按钮设置主题为深色', async () => {
    const wrapper = mountView()
    const store = useThemeStore()
    store.initialize()

    const navItems = wrapper.findAll('.settings-nav__item')
    await navItems[3].trigger('click')
    await wrapper.vm.$nextTick()

    const buttons = wrapper.findAll('.theme-toggle-btn')
    await buttons[1].trigger('click')

    expect(store.mode).toBe('dark')
    expect(store.resolvedTheme).toBe('dark')
    expect(document.documentElement.getAttribute(ROOT_THEME_ATTRIBUTE)).toBe('dark')
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('dark')
  })

  it('点击跟随系统按钮设置主题为系统', async () => {
    const wrapper = mountView()
    const store = useThemeStore()
    store.initialize()
    store.setMode('dark')

    const navItems = wrapper.findAll('.settings-nav__item')
    await navItems[3].trigger('click')
    await wrapper.vm.$nextTick()

    const buttons = wrapper.findAll('.theme-toggle-btn')
    await buttons[2].trigger('click')

    expect(store.mode).toBe('system')
    expect(store.resolvedTheme).toBe('light')
    expect(document.documentElement.getAttribute(ROOT_THEME_MODE_ATTRIBUTE)).toBe('system')
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('system')
  })

  it('主题按钮根据当前模式显示激活状态', async () => {
    const wrapper = mountView()
    const store = useThemeStore()
    store.initialize()
    store.setMode('dark')

    const navItems = wrapper.findAll('.settings-nav__item')
    await navItems[3].trigger('click')
    await wrapper.vm.$nextTick()

    const buttons = wrapper.findAll('.theme-toggle-btn')
    expect(buttons[1].classes()).toContain('theme-toggle-btn--active')
    expect(buttons[0].classes()).not.toContain('theme-toggle-btn--active')
  })
})
