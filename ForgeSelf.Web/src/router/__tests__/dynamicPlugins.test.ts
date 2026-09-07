import { describe, expect, it } from 'vitest'
import { createMemoryHistory, createRouter } from 'vue-router'
import type { PluginFrontendManifest } from '@/types/plugin'
import {
  buildFallbackRoutePath,
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
  it('buildManifestRoutePath 直接使用插件声明的路径（不做前缀化）', () => {
    // 终决方案：插件清单的 route 即最终路径，路径所有权归插件。
    expect(buildManifestRoutePath('/memory')).toBe('/memory')
    expect(buildManifestRoutePath('/quick-links')).toBe('/quick-links')
    expect(buildManifestRoutePath('todo')).toBe('/todo')
    expect(buildManifestRoutePath('/ai-agent')).toBe('/ai-agent')
  })

  it('buildFallbackRoutePath 生成命名空间回退路径', () => {
    expect(buildFallbackRoutePath('/ai-agent')).toBe('/plugin-view/ai-agent')
    expect(buildFallbackRoutePath('todo')).toBe('/plugin-view/todo')
  })

  it('为已启用且声明了试点视图的插件注册懒加载路由', () => {
    const router = makeRouter()
    registerManifestRoutes(router, [
      makeManifestItem({
        id: 'memory-system',
        name: '记忆系统插件',
        isEnabled: true,
        frontend: { views: ['MemoryView'], menu: '记忆', route: '/memory', icon: 'fa-brain' },
      }),
    ])

    const route = router.getRoutes().find((r) => r.name === 'manifest-memory-system')
    expect(route).toBeDefined()
    // 空 router 无冲突 → 直接用插件声明路径
    expect(route?.path).toBe('/memory')
    expect(route?.meta.source).toBe('manifest')
    expect(route?.meta.title).toBe('记忆')
    expect(typeof route?.components?.default).toBe('function')
  })

  it('路径被宿主静态路由占用时回退到 /plugin-view 命名空间', () => {
    const router = createRouter({
      history: createMemoryHistory(),
      // 模拟宿主已有一个 /todo 静态路由
      routes: [{ path: '/todo', name: 'todo', component: { template: '<div/>' } }],
    })

    registerManifestRoutes(router, [
      makeManifestItem({
        // 用独立 id：registeredRouteNames 是模块级共享集合，
        // 复用其它用例的 id 会互相污染（实测导致幂等用例失败）。
        id: 'demo-conflict',
        name: '冲突演示插件',
        isEnabled: true,
        frontend: { views: ['TodoView'], menu: '待办事项', route: '/todo' },
      }),
    ])

    const route = router.getRoutes().find((r) => r.name === 'manifest-demo-conflict')
    expect(route).toBeDefined()
    expect(route?.path).toBe('/plugin-view/todo')
    // 宿主静态路由必须原样保留，不能被插件覆盖
    expect(router.hasRoute('todo')).toBe(true)
    expect(router.getRoutes().find((r) => r.name === 'todo')?.path).toBe('/todo')
  })

  it('无冲突时插件接管声明路径（宿主已让位）', () => {
    const router = makeRouter()
    registerManifestRoutes(router, [
      makeManifestItem({
        id: 'ai-agent',
        name: 'AI代理插件',
        isEnabled: true,
        // 用可解析的视图作为替身：本用例断言的是「路径接管」行为，
        // 与视图解析无关（AiAgentView 由插件自带 entry 提供，宿主已无回退实现）。
        frontend: { views: ['MemoryView'], menu: 'AI Agent', route: '/ai-agent' },
      }),
    ])

    const route = router.getRoutes().find((r) => r.name === 'manifest-ai-agent')
    expect(route?.path).toBe('/ai-agent')
    // 且不应回退到命名空间
    expect(route?.path).not.toBe('/plugin-view/ai-agent')
  })

  it('跳过未启用、无 frontend、无 route、未知视图的条目', () => {
    const router = makeRouter()
    registerManifestRoutes(router, [
      makeManifestItem({
        id: 'disabled',
        isEnabled: false,
        frontend: { views: ['MemoryView'], menu: '禁用', route: '/memory' },
      }),
      makeManifestItem({ id: 'no-frontend', isEnabled: true, frontend: null }),
      makeManifestItem({
        id: 'no-route',
        isEnabled: true,
        frontend: { views: ['MemoryView'], menu: '无路由' },
      }),
      makeManifestItem({
        id: 'unknown-view',
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
        id: 'todo-tracker',
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
    expect(names).toEqual(['manifest-todo-tracker'])
  })

  it('clearManifestRoutes 移除已注册的动态路由', () => {
    const router = makeRouter()
    registerManifestRoutes(router, [
      makeManifestItem({
        id: 'quick-links',
        name: '快捷链接插件',
        isEnabled: true,
        // QuickLinks 已自带界面资源，清单带 entry → 走远程加载分支
        frontend: {
          views: ['QuickLinksView'],
          menu: '快捷链接',
          route: '/quick-links',
          icon: 'fa-link',
          entry: 'web/dist/index.js',
        },
      }),
    ])

    expect(router.hasRoute('manifest-quick-links')).toBe(true)

    clearManifestRoutes(router)

    expect(router.hasRoute('manifest-quick-links')).toBe(false)
  })
})
