import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import type { PluginFrontendManifest } from '@/types/plugin'
import { usePluginManifestStore } from '../pluginManifest'

vi.mock('@/services/pluginManifestApi', () => ({
  pluginManifestApi: {
    fetchFrontendManifest: vi.fn(),
  },
}))

import { pluginManifestApi } from '@/services/pluginManifestApi'

function makeManifestItem(partial: Partial<PluginFrontendManifest>): PluginFrontendManifest {
  return {
    id: 'p1',
    name: '插件',
    frontend: null,
    isEnabled: false,
    ...partial,
  }
}

describe('pluginManifest store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('menus 只返回已启用且声明了菜单的贡献条目', () => {
    const store = usePluginManifestStore()
    store.manifest = [
      makeManifestItem({
        id: 'memory',
        name: '记忆系统插件',
        isEnabled: true,
        frontend: { views: ['MemoryView'], menu: '记忆', route: '/memory', icon: 'fa-brain' },
      }),
      makeManifestItem({
        id: 'quick-links',
        name: '快捷链接插件',
        isEnabled: false,
        frontend: { views: ['QuickLinksView'], menu: '快捷链接', route: '/quick-links', icon: 'fa-link' },
      }),
      makeManifestItem({
        id: 'backend',
        name: '纯后端插件',
        isEnabled: true,
        frontend: { views: [] },
      }),
      makeManifestItem({
        id: 'no-frontend',
        name: '无前端块插件',
        isEnabled: true,
        frontend: null,
      }),
    ]

    expect(store.menus).toHaveLength(1)
    expect(store.menus[0]).toEqual({
      id: 'memory',
      name: '记忆系统插件',
      menu: '记忆',
      route: '/memory',
      icon: 'fa-brain',
      views: ['MemoryView'],
    })
  })

  it('isEnabled 依据 id 与启用状态判断', () => {
    const store = usePluginManifestStore()
    store.manifest = [
      makeManifestItem({ id: 'on', isEnabled: true }),
      makeManifestItem({ id: 'off', isEnabled: false }),
    ]

    expect(store.isEnabled('on')).toBe(true)
    expect(store.isEnabled('off')).toBe(false)
    expect(store.isEnabled('missing')).toBe(false)
  })

  it('loadManifest 成功时缓存清单并标记 loaded', async () => {
    const data: PluginFrontendManifest[] = [
      makeManifestItem({ id: 'a', isEnabled: true, frontend: { views: ['AView'], menu: 'A' } }),
    ]
    vi.mocked(pluginManifestApi.fetchFrontendManifest).mockResolvedValue(data)

    const store = usePluginManifestStore()
    await store.loadManifest()

    expect(store.manifest).toEqual(data)
    expect(store.loaded).toBe(true)
    expect(store.error).toBeNull()
    expect(store.isLoading).toBe(false)
  })

  it('loadManifest 失败时记录错误且 loaded 保持 false', async () => {
    vi.mocked(pluginManifestApi.fetchFrontendManifest).mockRejectedValue(new Error('网络错误'))

    const store = usePluginManifestStore()
    await store.loadManifest()

    expect(store.manifest).toEqual([])
    expect(store.loaded).toBe(false)
    expect(store.error).toBe('网络错误')
    expect(store.isLoading).toBe(false)
  })

  it('surface getters 按 surface 分发已启用插件（含旧插件默认 tab）', () => {
    const store = usePluginManifestStore()
    store.manifest = [
      // 旧插件：无 surfaces，带 route+views → 默认 tab
      makeManifestItem({
        id: 'memory',
        name: '记忆',
        isEnabled: true,
        frontend: { views: ['MemoryView'], menu: '记忆', route: '/memory' },
      }),
      // 显式 home-widget
      makeManifestItem({
        id: 'home-widget-a',
        name: '看板插件',
        isEnabled: true,
        frontend: { views: ['BoardView'], route: '/boards', surfaces: ['home-widget'] },
      }),
      // 显式多 surface：tab + quick-action
      makeManifestItem({
        id: 'composer',
        name: '快捷动作',
        isEnabled: true,
        frontend: { views: ['ComposerView'], route: '/composer', surfaces: ['tab', 'quick-action'] },
      }),
      // 禁用：不进任何 getter
      makeManifestItem({
        id: 'disabled-badge',
        name: '徽标(禁用)',
        isEnabled: false,
        frontend: { views: ['BadgeView'], surfaces: ['status-badge'] },
      }),
      // 纯后端：无 frontend
      makeManifestItem({ id: 'backend', name: '后端', isEnabled: true, frontend: null }),
    ]

    expect(store.tabRoutes.map((m) => m.id).sort()).toEqual(['composer', 'memory'])
    expect(store.homeWidgets.map((m) => m.id)).toEqual(['home-widget-a'])
    expect(store.quickActions.map((m) => m.id)).toEqual(['composer'])
    expect(store.statusBadges.map((m) => m.id)).toEqual([]) // 唯一 badge 被禁用
  })

  it('surfacesOf 返回指定插件归一后的 surfaces', () => {
    const store = usePluginManifestStore()
    store.manifest = [
      makeManifestItem({
        id: 'home',
        isEnabled: true,
        frontend: { views: ['HomeView'], route: '/home', surfaces: ['tab', 'home-widget'] },
      }),
    ]

    expect(store.surfacesOf('home')).toEqual(['tab', 'home-widget'])
    expect(store.surfacesOf('missing')).toEqual([])
  })
})
