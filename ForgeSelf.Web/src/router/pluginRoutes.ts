/**
 * 插件动态路由管理
 */

import type { RouteRecordRaw, Router } from 'vue-router'
import type { PluginMenuItem } from '@/types/plugin'

const PLUGIN_ROUTE_PREFIX = '/plugin'

const addedRouteNames = new Set<string>()

export function generatePluginRouteName(pluginId: string, path: string): string {
  const normalizedPath = path.replace(/\//g, '-').replace(/^-/, '')
  return `plugin-${pluginId}-${normalizedPath}`
}

export function buildPluginRoutes(menuItems: PluginMenuItem[]): RouteRecordRaw[] {
  const routeMap = new Map<string, RouteRecordRaw>()
  const rootRoutes: RouteRecordRaw[] = []

  for (const item of menuItems) {
    const routeName = generatePluginRouteName(item.pluginId, item.path)
    const route: RouteRecordRaw = {
      path: item.path,
      name: routeName,
      component: () => import('@/views/PluginPage.vue'),
      meta: {
        pluginId: item.pluginId,
        menuItemId: item.id,
        title: item.name,
        icon: item.icon
      }
    }
    routeMap.set(item.id, route)
  }

  for (const item of menuItems) {
    const route = routeMap.get(item.id)!
    if (item.parentId && routeMap.has(item.parentId)) {
      const parent = routeMap.get(item.parentId)!
      if (!parent.children) {
        parent.children = []
      }
      parent.children.push(route)
    } else {
      rootRoutes.push(route)
    }
  }

  return rootRoutes
}

export function registerPluginRoutes(router: Router, menuItems: PluginMenuItem[]): void {
  const routes = buildPluginRoutes(menuItems)

  for (const route of routes) {
    const routeName = String(route.name)
    if (!addedRouteNames.has(routeName)) {
      router.addRoute({
        path: `${PLUGIN_ROUTE_PREFIX}/${route.path.replace(/^\//, '')}`,
        name: route.name,
        component: route.component ?? undefined,
        children: route.children,
        meta: route.meta
      } as RouteRecordRaw)
      addedRouteNames.add(routeName)
    }
  }
}

export function unregisterPluginRoutes(router: Router, pluginId: string): void {
  const routesToRemove: string[] = []
  for (const name of addedRouteNames) {
    if (name.startsWith(`plugin-${pluginId}-`)) {
      routesToRemove.push(name)
    }
  }
  for (const name of routesToRemove) {
    if (router.hasRoute(name)) {
      router.removeRoute(name)
    }
    addedRouteNames.delete(name)
  }
}

export function clearAllPluginRoutes(router: Router): void {
  for (const name of addedRouteNames) {
    if (router.hasRoute(name)) {
      router.removeRoute(name)
    }
  }
  addedRouteNames.clear()
}

export function hasPluginRoute(routeName: string): boolean {
  return addedRouteNames.has(routeName)
}

export function getPluginRoutePrefix(): string {
  return PLUGIN_ROUTE_PREFIX
}
