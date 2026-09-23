import { describe, it, expect, beforeEach } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useTabsStore } from '@/stores/tabs'

// 标签栏状态测试：打开页面注册标签、重复不追加、关闭移除、首页不可关
describe('tabs store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('初始种子为基础标签（含首页）', () => {
    const store = useTabsStore()
    expect(store.tabs.length).toBe(4)
    expect(store.tabs.some((t) => t.path === '/')).toBe(true)
    expect(store.hasTab('/ai-agent')).toBe(true)
    expect(store.hasTab('/mcp-tools')).toBe(false) // MCP 中心已迁入插件（034 v2.0.0），宿主不再内置标签
  })

  it('openTab 追加新标签', () => {
    const store = useTabsStore()
    store.openTab('/text-tools', '文本工具')
    expect(store.tabs.some((t) => t.path === '/text-tools')).toBe(true)
    expect(store.hasTab('/text-tools')).toBe(true)
  })

  it('openTab 重复路径不重复追加', () => {
    const store = useTabsStore()
    store.openTab('/memory', '记忆管理')
    const before = store.tabs.length
    store.openTab('/memory', '记忆管理')
    expect(store.tabs.length).toBe(before)
  })

  it('closeTab 移除非基础标签', () => {
    const store = useTabsStore()
    store.openTab('/dev-tools', '开发者工具箱')
    expect(store.hasTab('/dev-tools')).toBe(true)
    store.closeTab('/dev-tools')
    expect(store.hasTab('/dev-tools')).toBe(false)
  })

  it('closeTab 首页不可关闭', () => {
    const store = useTabsStore()
    const before = store.tabs.length
    store.closeTab('/')
    expect(store.tabs.length).toBe(before)
    expect(store.hasTab('/')).toBe(true)
  })
})
