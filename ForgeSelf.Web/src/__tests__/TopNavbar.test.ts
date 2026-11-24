import { describe, it, expect, beforeEach, vi } from 'vitest'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import TopNavbar from '@/components/TopNavbar.vue'
import { useAppearanceStore } from '@/stores/appearance'
import { useTabsStore } from '@/stores/tabs'

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

describe('TopNavbar — 页签图标槽常驻预留（悬浮零位移守卫）', () => {
  // 根因（2026-11-23 用户输入22）：.close-x 用 display:none ↔ hover display 切换显隐，
  // 悬浮瞬间页签变宽、其后所有页签整体位移。
  // 守卫不变量：图标槽必须常驻占位（visibility/opacity 显隐），display 不得参与 .close-x 显隐。
  // 与 PageHead/ForgeCard 守卫同款读源方式（import.meta.url 在 vitest jsdom 下非 file:，不可用）
  const sfcSource = readFileSync(resolve(process.cwd(), 'src/components/TopNavbar.vue'), 'utf-8')
  // 先剥 CSS 注释：规则前的注释文本会被朴素解析器并进选择器，导致 .close-x 匹配失败
  const styleBlock = sfcSource.slice(sfcSource.indexOf('<style')).replace(/\/\*[\s\S]*?\*\//g, '')
  const rules = [...styleBlock.matchAll(/([^{}]+)\{([^{}]*)\}/g)].map((m) => ({
    selector: m[1].trim(),
    body: m[2],
  }))
  const closeXMain = rules.filter((r) =>
    r.selector.split(',').map((s) => s.trim()).includes('.close-x'),
  )
  const closeXStateRules = rules.filter(
    (r) => r.selector.includes('.close-x') && /:(hover|focus-within)/.test(r.selector),
  )

  it('.close-x 不得含 display:none（display 出入布局流 = 悬浮位移根因）', () => {
    expect(closeXMain.length).toBeGreaterThan(0)
    for (const rule of closeXMain) {
      expect(rule.body).not.toMatch(/display\s*:\s*none/)
    }
  })

  it('hover/focus 态规则不得用 display 切换 .close-x（只允许显隐语义切换）', () => {
    for (const rule of closeXStateRules) {
      expect(rule.body).not.toMatch(/(^|[^-])display\s*:/)
    }
  })

  it('槽位规格：visibility:hidden 隐而占位 + width:16px + flex:none（防 flex 压缩）', () => {
    const body = closeXMain.map((r) => r.body).join('\n')
    expect(body).toMatch(/visibility\s*:\s*hidden/)
    expect(body).toMatch(/width\s*:\s*16px/)
    expect(body).toMatch(/flex\s*:\s*none/)
  })

  it('DOM：非首页页签恒渲染 1 个 .close-x 图标槽，首页签不渲染', () => {
    const tabsStore = useTabsStore()
    tabsStore.openTab('/settings', '设置')
    tabsStore.openTab('/plugins', '插件管理')
    const wrapper = mount(TopNavbar)
    const items = wrapper.findAll('.tab-item')
    expect(items.length).toBeGreaterThanOrEqual(4)
    for (const item of items) {
      const label = item.find('span')?.text() ?? ''
      const close = item.findAll('.close-x')
      if (label === '首页') {
        expect(close.length).toBe(0)
      } else {
        expect(close.length).toBe(1)
      }
    }
  })
})
