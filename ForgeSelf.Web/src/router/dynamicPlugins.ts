/**
 * 插件「清单驱动的动态 import 视图挂载」。
 *
 * 由后端 /api/plugin/frontend-manifest 返回的插件前端贡献（route + views）驱动，
 * 在启动时把声明了独立视图的插件路由通过真实 `import()` 懒加载挂载到 router，
 * 取代把插件视图写死在静态路由表的做法。
 *
 * 说明：
 * - 视图名 → 懒加载组件的映射集中在 resolvePluginView，试点覆盖
 *   MemorySystem(MemoryView)、QuickLinks(QuickLinksView)、TodoTracker(TodoView)。
 * - 动态路由统一挂在 MANIFEST_ROUTE_PREFIX 命名空间下，避免与现有 21 个静态
 *   功能页（如 /memory、/quick-links、/todo）发生同名路径冲突，保证静态路由
 *   永远是 manifest 失败/为空时的兜底。
 */

import type { Router, RouteRecordRaw } from 'vue-router'
import type { PluginFrontendManifest } from '@/types/plugin'

/** manifest 驱动动态路由的统一命名空间前缀。 */
export const MANIFEST_ROUTE_PREFIX = '/plugin-view'

/** 已通过 registerManifestRoutes 注册的路由名，保证幂等、可清理。 */
const registeredRouteNames = new Set<string>()

/**
 * 插件视图名 → 懒加载视图组件。
 * 返回 undefined 表示该视图名不在试点覆盖范围内（前端无对应实现），应跳过注册。
 */
function resolvePluginView(viewName: string): RouteRecordRaw['component'] {
  switch (viewName) {
    case 'MemoryView':
      return () => import('@/views/MemoryView.vue')
    case 'QuickLinksView':
      return () => import('@/views/QuickLinksView.vue')
    case 'TodoView':
      return () => import('@/views/TodoView.vue')
    default:
      return undefined
  }
}

/** 由 manifest 的 route（如 /memory）推导出命名空间下的动态路径（/plugin-view/memory）。 */
export function buildManifestRoutePath(route: string): string {
  const normalized = route.startsWith('/') ? route.slice(1) : route
  return `${MANIFEST_ROUTE_PREFIX}/${normalized}`
}

/**
 * 根据插件前端清单动态注册路由。
 *
 * 只处理「已启用 + 声明了 route + 至少一个 views 且首个视图在试点注册表内」的条目；
 * 其余（未启用/无 frontend/无 route/未知视图）一律跳过，因此清单失败或为空时
 * 不会向 router 注入任何路由，现有静态路由原样兜底。
 */
export function registerManifestRoutes(router: Router, manifest: PluginFrontendManifest[]): void {
  for (const entry of manifest) {
    const frontend = entry.frontend
    if (!entry.isEnabled || !frontend || !frontend.route) continue

    const viewName = frontend.views?.[0]
    if (!viewName) continue

    const component = resolvePluginView(viewName)
    if (!component) continue

    const routeName = `manifest-${entry.id}`
    if (router.hasRoute(routeName) || registeredRouteNames.has(routeName)) continue

    const route: RouteRecordRaw = {
      path: buildManifestRoutePath(frontend.route),
      name: routeName,
      component,
      meta: {
        pluginId: entry.id,
        title: frontend.menu ?? entry.name,
        icon: frontend.icon ?? null,
        source: 'manifest',
      },
    }
    router.addRoute(route)
    registeredRouteNames.add(routeName)
  }
}

/** 清理所有由本模块注册的 manifest 动态路由（供后续卸载/重载场景使用）。 */
export function clearManifestRoutes(router: Router): void {
  for (const name of registeredRouteNames) {
    if (router.hasRoute(name)) router.removeRoute(name)
  }
  registeredRouteNames.clear()
}

/** 判断指定路由名是否由本模块注册。 */
export function hasManifestRoute(routeName: string): boolean {
  return registeredRouteNames.has(routeName)
}
