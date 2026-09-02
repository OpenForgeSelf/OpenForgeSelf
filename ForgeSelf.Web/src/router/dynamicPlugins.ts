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
import { buildPluginAssetUrl, clearPluginViewCache, loadPluginView } from '@/utils/pluginViewLoader'

/**
 * 冲突回退用的命名空间前缀。
 *
 * 路由方案（终决 2026-08-30）：**以插件清单声明的 `route` 直接作为最终路径**
 * （如 `/ai-agent`），仅当该路径与宿主既有静态路由冲突时才回退到本前缀
 * （`/plugin-view/ai-agent`）。
 *
 * 选择「直接路径优先」的理由：
 * - `plugin.json` 的 `route` 是插件自声明的路由契约，直接兑现最符合直觉；
 * - 应用内已有大量导航指向裸路径（HomeView 快捷提问、tabs store、TopNavbar、
 *   McpToolsView 链接、features.ts），直接路径可零改动复用；
 * - 宿主静态路由数量有限且稳定，冲突是少数情况，用回退兜底即可，
 *   不必为少数冲突牺牲全部插件的 URL 观感。
 * 安全性由「注册前冲突检测」保证：冲突时回退命名空间，绝不覆盖宿主页面。
 */
export const MANIFEST_ROUTE_PREFIX = '/plugin-view'

/** 已通过 registerManifestRoutes 注册的路由名，保证幂等、可清理。 */
const registeredRouteNames = new Set<string>()

/**
 * 解析插件视图组件。
 *
 * 优先按清单声明的界面入口（entry）从插件目录**远程加载**界面资源；
 * entry 为空时回退既有硬编码映射（主包内组件），
 * 保证「已声明界面但未自带资源」的插件（MemorySystem / QuickLinks / TodoTracker）零回归（SC-006）。
 *
 * @param item 清单条目（含插件 id、版本与界面贡献声明）
 * @returns 路由组件；无法确定视图时返回 undefined，调用方应跳过注册
 */
function resolvePluginView(item: PluginFrontendManifest): RouteRecordRaw['component'] | undefined {
  const viewName = item.frontend?.views?.[0]
  if (!viewName) return undefined

  const remoteEntry = item.frontend?.entry?.trim()
  if (remoteEntry) {
    // 资源缓存标识优先用后端下发的 WebVersion（内容指纹），回退插件 version；
    // 内容变化即变化，确保重发插件后刷新即取到新界面（绕开 immutable 长缓存）。
    const assetVersion = item.webVersion ?? item.version ?? ''
    return loadPluginView({
      pluginId: item.id,
      pluginName: item.name,
      version: assetVersion,
      entryUrl: buildPluginAssetUrl(item.id, remoteEntry, assetVersion),
      exportName: viewName,
    })
  }

  // 回退路径：既有试点插件仍走主包内组件映射。
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

/**
 * 归一化插件声明的 route 为最终挂载路径。
 *
 * 终决方案：**直接使用插件声明的路径**（`/ai-agent` → `/ai-agent`），
 * 不做任何前缀化。路径所有权归插件，宿主静态路由让位（迁移后宿主不再内置同名页）。
 *
 * @param route 插件清单声明的 route，如 `/ai-agent`
 * @returns 归一化后的绝对路径，如 `/ai-agent`
 */
export function buildManifestRoutePath(route: string): string {
  const normalized = route.startsWith('/') ? route.slice(1) : route
  return `/${normalized}`
}

/** 冲突回退路径（仅当直接路径被宿主静态路由占用时使用）。 */
export function buildFallbackRoutePath(route: string): string {
  const normalized = route.startsWith('/') ? route.slice(1) : route
  return `${MANIFEST_ROUTE_PREFIX}/${normalized}`
}

/**
 * 判断路径是否已被 router 中的既有路由占用（用于插件路由冲突检测）。
 *
 * vue-router 对参数化路由（如 `/plugins/:id`）与具体路径（`/plugins/updates`）
 * 的匹配优先级由注册顺序决定，这里采用保守判定：只要存在任一路由的 path
 * 与目标完全相同，即视为冲突；同时对参数化路由做同段数前缀比对，
 * 避免 `/ai-agent` 被 `/plugins/:id` 之类的通配规则吞掉。
 */
function isPathTaken(router: Router, path: string): boolean {
  const target = path.replace(/\/+$/, '')
  const segments = target.split('/').filter(Boolean)

  return router.getRoutes().some((r) => {
    const existing = r.path.replace(/\/+$/, '')
    if (existing === target) return true
    if (!existing.includes(':')) return false
    // 参数化路由：段数相同且非参数段逐段一致 → 视为占用
    const parts = existing.split('/').filter(Boolean)
    if (parts.length !== segments.length) return false
    return parts.every((p, i) => p.startsWith(':') || p === segments[i])
  })
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

    const component = resolvePluginView(entry)
    if (!component) continue

    const routeName = `manifest-${entry.id}`
    if (router.hasRoute(routeName) || registeredRouteNames.has(routeName)) continue

    // 路径冲突检测：插件声明路径若已被宿主静态路由占用，则回退命名空间，
    // 保证插件永远不会覆盖宿主页面（安全兜底）。
    let path = buildManifestRoutePath(frontend.route)
    if (isPathTaken(router, path)) {
      const fallback = buildFallbackRoutePath(frontend.route)
      console.warn(
        `[dynamicPlugins] 插件 ${entry.id} 声明的路由 ${path} 与宿主既有路由冲突，` +
          `已回退到 ${fallback}。请在宿主迁移/移除该静态路由后重发插件。`
      )
      path = isPathTaken(router, fallback) ? '' : fallback
      if (!path) {
        console.warn(`[dynamicPlugins] 回退路径同样冲突，跳过插件 ${entry.id} 的路由注册`)
        continue
      }
    }

    const route: RouteRecordRaw = {
      path,
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

/**
 * 清理所有由本模块注册的 manifest 动态路由（供后续卸载/重载场景使用）。
 * 同时清空远程加载的插件界面缓存，避免插件版本变化后复用过期组件。
 */
export function clearManifestRoutes(router: Router): void {
  for (const name of registeredRouteNames) {
    if (router.hasRoute(name)) router.removeRoute(name)
  }
  registeredRouteNames.clear()
  clearPluginViewCache()
}

/** 判断指定路由名是否由本模块注册。 */
export function hasManifestRoute(routeName: string): boolean {
  return registeredRouteNames.has(routeName)
}
