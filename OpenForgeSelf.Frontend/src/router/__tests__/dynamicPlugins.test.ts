import { describe, expect, it } from 'vitest'
import { createMemoryHistory, createRouter } from 'vue-router'
import type { PluginFrontendManifest } from '@/types/plugin'
import {
  buildManifestRoutePath,
  clearManifestRoutes,
  registerManifestRoutes,
} from '../dynamicPlugins'

function makeManifestItem(partial: Partial<PluginFrontendManifest>): PluginFrontendManifest {
  return {
    id: 'p1',
    name: '插件',
    frontend: null,
    isEnabled: false,
    ...partial,
  }
}

function makeRouter() {
  return createRouter({ history: createMemoryHistory(), routes: [] })
}

describe('dynamicPlugins 动态视图挂载', () => {
  it('buildManifestRoutePath 把 manifest route 归一化为命名空间路径', () => {
    expect(buildManifestRoutePath('/memory')).toBe('/plugin-view/memory')
    expect(buildManifestRoutePath('/quick-links')).toBe('/plugin-view/quick-links')
    expect(buildManifestRoutePath('todo')).toBe('/plugin-view/todo')
  })

  it('为已启用且声明了试点视图的插件注册懒加载路由', () => {
    const router = makeRouter()
    registerManifestRoutes(router, [
      makeManifestItem({
        id: 'memorysystem.plugin',
        name: '记忆系统插件',
        isEnabled: true,
        frontend: { views: ['MemoryView'], menu: '记忆', route: '/memory', icon: 'fa-brain' },
      }),
    ])

    const route = router.getRoutes().find((r) => r.name === 'manifest-memorysystem.plugin')
    expect(route).toBeDefined()
    expect(route?.path).toBe('/plugin-view/memory')
    expect(route?.meta.source).toBe('manifest')
    expect(route?.meta.title).toBe('记忆')
    expect(typeof route?.components?.default).toBe('function')
  })

  it('跳过未启用、无 frontend、无 route、未知视图的条目', () => {
    const router = makeRouter()
    registerManifestRoutes(router, [
      makeManifestItem({
        id: 'disabled.plugin',
        isEnabled: false,
        frontend: { views: ['MemoryView'], menu: '禁用', route: '/memory' },
      }),
      makeManifestItem({ id: 'no-frontend.plugin', isEnabled: true, frontend: null }),
      makeManifestItem({
        id: 'no-route.plugin',
        isEnabled: true,
        frontend: { views: ['MemoryView'], menu: '无路由' },
      }),
      makeManifestItem({
        id: 'unknown-view.plugin',
        isEnabled: true,
        frontend: { views: ['UnknownView'], menu: '未知视图', route: '/unknown' },
      }),
    ])

    expect(router.getRoutes().filter((r) => String(r.name).startsWith('manifest-'))).toHaveLength(0)
  })

  it('空清单不注入任何路由（回退静态路由）', () => {
    const router = makeRouter()
    registerManifestRoutes(router, [])

    expect(router.getRoutes().filter((r) => String(r.name).startsWith('manifest-'))).toHaveLength(0)
  })

  it('重复注册保持幂等（不产生同名重复路由）', () => {
    const router = makeRouter()
    const manifest = [
      makeManifestItem({
        id: 'todotracker.plugin',
        name: '待办追踪插件',
        isEnabled: true,
        frontend: { views: ['TodoView'], menu: '待办事项', route: '/todo', icon: 'fa-check-square' },
      }),
    ]

    registerManifestRoutes(router, manifest)
    registerManifestRoutes(router, manifest)

    const names = router
      .getRoutes()
      .map((r) => String(r.name))
      .filter((n) => n.startsWith('manifest-'))
    expect(names).toEqual(['manifest-todotracker.plugin'])
  })

  it('clearManifestRoutes 移除已注册的动态路由', () => {
    const router = makeRouter()
    registerManifestRoutes(router, [
      makeManifestItem({
        id: 'quicklinks.plugin',
        name: '快捷链接插件',
        isEnabled: true,
        frontend: { views: ['QuickLinksView'], menu: '快捷链接', route: '/quick-links', icon: 'fa-link' },
      }),
    ])

    expect(router.hasRoute('manifest-quicklinks.plugin')).toBe(true)

    clearManifestRoutes(router)

    expect(router.hasRoute('manifest-quicklinks.plugin')).toBe(false)
  })
})
