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
        id: 'memory.plugin',
        name: '记忆系统插件',
        isEnabled: true,
        frontend: { views: ['MemoryView'], menu: '记忆', route: '/memory', icon: 'fa-brain' },
      }),
      makeManifestItem({
        id: 'quicklinks.plugin',
        name: '快捷链接插件',
        isEnabled: false,
        frontend: { views: ['QuickLinksView'], menu: '快捷链接', route: '/quick-links', icon: 'fa-link' },
      }),
      makeManifestItem({
        id: 'backend.plugin',
        name: '纯后端插件',
        isEnabled: true,
        frontend: { views: [] },
      }),
      makeManifestItem({
        id: 'no-frontend.plugin',
        name: '无前端块插件',
        isEnabled: true,
        frontend: null,
      }),
    ]

    expect(store.menus).toHaveLength(1)
    expect(store.menus[0]).toEqual({
      id: 'memory.plugin',
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
      makeManifestItem({ id: 'on.plugin', isEnabled: true }),
      makeManifestItem({ id: 'off.plugin', isEnabled: false }),
    ]

    expect(store.isEnabled('on.plugin')).toBe(true)
    expect(store.isEnabled('off.plugin')).toBe(false)
    expect(store.isEnabled('missing.plugin')).toBe(false)
  })

  it('loadManifest 成功时缓存清单并标记 loaded', async () => {
    const data: PluginFrontendManifest[] = [
      makeManifestItem({ id: 'a.plugin', isEnabled: true, frontend: { views: ['AView'], menu: 'A' } }),
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
})
