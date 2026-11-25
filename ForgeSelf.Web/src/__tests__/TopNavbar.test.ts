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

describe('TopNavbar — 页签图标槽常驻预留（悬浮零位移守卫 · logo⇄动作图标同位互换）', () => {
  // 根因（2026-11-23 用户输入22）：.close-x 用 display:none ↔ hover display 切换显隐，
  // 悬浮瞬间页签变宽、其后所有页签整体位移。
  // 模型升级（2026-11-25 用户输入28 v2）：右侧 16px 槽默认显示功能/插件 logo，悬浮时同位
  // 换成动作图标（×/📌）。不变量不变：槽与两图标同一 16px 盒、盒宽恒定、显隐一律走
  // visibility/opacity（display 不得参与显隐）⇒ 任何状态布局零变化。
  // 与 PageHead/ForgeCard 守卫同款读源方式（import.meta.url 在 vitest jsdom 下非 file:，不可用）
  const sfcSource = readFileSync(resolve(process.cwd(), 'src/components/TopNavbar.vue'), 'utf-8')
  // 先剥 CSS 注释：规则前的注释文本会被朴素解析器并进选择器，导致类名匹配失败
  const styleBlock = sfcSource.slice(sfcSource.indexOf('<style')).replace(/\/\*[\s\S]*?\*\//g, '')
  const rules = [...styleBlock.matchAll(/([^{}]+)\{([^{}]*)\}/g)].map((m) => ({
    selector: m[1].trim(),
    body: m[2],
  }))
  const rulesFor = (cls: string) =>
    rules.filter((r) => r.selector.split(',').map((s) => s.trim()).includes(cls))
  const stateRulesFor = (cls: string) =>
    rules.filter(
      (r) =>
        r.selector.includes(cls) &&
        // 只取「由页签 hover/focus 态驱动」的显隐规则；.close-x:hover 这类图标自身反馈不算
        /\.tab-item:(hover|focus-within)/.test(r.selector),
    )
  const closeXMain = rulesFor('.close-x')
  const slotMain = rulesFor('.tab-icon-slot')
  const logoMain = rulesFor('.tab-logo')
  const iconStateRules = rules.filter(
    (r) =>
      /(\.close-x|\.tab-logo)/.test(r.selector) && /:(hover|focus-within)/.test(r.selector),
  )

  it('显隐禁令：.close-x/.tab-logo/.tab-icon-slot 一律不得含 display:none（display 出入布局流 = 悬浮位移根因）', () => {
    expect(closeXMain.length + slotMain.length + logoMain.length).toBeGreaterThanOrEqual(3)
    for (const rule of [...closeXMain, ...slotMain, ...logoMain]) {
      expect(rule.body).not.toMatch(/display\s*:\s*none/)
    }
    for (const rule of iconStateRules) {
      expect(rule.body).not.toMatch(/(^|[^-])display\s*:/)
    }
  })

  it('槽位规格：.tab-icon-slot 恒 16px + flex:none + position:relative（动作图标绝对定位的锚）', () => {
    expect(slotMain.length).toBe(1)
    expect(slotMain[0].body).toMatch(/width\s*:\s*16px/)
    expect(slotMain[0].body).toMatch(/flex\s*:\s*none/)
    expect(slotMain[0].body).toMatch(/position\s*:\s*relative/)
  })

  it('动作图标：.close-x 绝对定位覆盖槽位（inset:0）+ 默认 visibility:hidden（隐而不占外流）', () => {
    const body = closeXMain.map((r) => r.body).join('\n')
    expect(body).toMatch(/position\s*:\s*absolute/)
    expect(body).toMatch(/inset\s*:\s*0/)
    expect(body).toMatch(/visibility\s*:\s*hidden/)
  })

  it('logo：默认可见，hover/focus 同位隐藏（visibility 语义），显隐不改变布局', () => {
    expect(logoMain.length).toBeGreaterThan(0)
    expect(logoMain.map((r) => r.body).join('\n')).toMatch(/visibility\s*:\s*visible/)
    const hideRules = stateRulesFor('.tab-logo')
    expect(hideRules.length).toBeGreaterThan(0)
    for (const rule of hideRules) {
      expect(rule.body).toMatch(/visibility\s*:\s*hidden/)
    }
    // 动作图标悬浮显现（与 logo 互补，同一盒内同位互换）
    const showRules = stateRulesFor('.close-x')
    expect(showRules.length).toBeGreaterThan(0)
    for (const rule of showRules) {
      expect(rule.body).toMatch(/visibility\s*:\s*visible/)
    }
  })

  it('DOM：非首页页签恒渲染 1 个图标槽（logo + 动作图标各 1），首页签不渲染；已知功能 logo 渲染 SVG', () => {
    const tabsStore = useTabsStore()
    tabsStore.openTab('/settings', '设置')
    tabsStore.openTab('/plugins', '插件管理')
    const wrapper = mount(TopNavbar)
    const items = wrapper.findAll('.tab-item')
    expect(items.length).toBeGreaterThanOrEqual(4)
    let svgLogoCount = 0
    for (const item of items) {
      const label = item.find('.tab-label')?.text() ?? ''
      const slot = item.findAll('.tab-icon-slot')
      if (label === '首页') {
        expect(slot.length).toBe(0)
      } else {
        expect(slot.length).toBe(1)
        expect(slot[0].findAll('.tab-logo').length).toBe(1)
        expect(slot[0].findAll('.close-x').length).toBe(1)
        if (slot[0].element.innerHTML.includes('<svg')) svgLogoCount++
      }
    }
    // 智能体/技能管理/系统监控/任务中心 均在功能单一真源有 lucide 图标
    expect(svgLogoCount).toBeGreaterThanOrEqual(4)
  })
})
