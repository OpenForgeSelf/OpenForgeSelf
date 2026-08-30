/**
 * Vue Router 共享模块桥（自动生成，请勿手工编辑）。
 *
 * 由 scripts/generate-shared-shims.mjs 依据宿主实际安装版本生成；
 * 运行期从宿主挂载的 window.__FORGE_SHARED__ 再导出，保证插件与宿主共用同一实例。
 */
const m = window.__FORGE_SHARED__?.vueRouter
if (!m) throw new Error(`[ForgeSelf] 共享依赖未就绪: ${target.globalKey}，宿主未挂载 window.__FORGE_SHARED__`)

export const NavigationFailureType = m.NavigationFailureType
export const RouterLink = m.RouterLink
export const RouterView = m.RouterView
export const START_LOCATION = m.START_LOCATION
export const createMemoryHistory = m.createMemoryHistory
export const createRouter = m.createRouter
export const createRouterMatcher = m.createRouterMatcher
export const createWebHashHistory = m.createWebHashHistory
export const createWebHistory = m.createWebHistory
export const isNavigationFailure = m.isNavigationFailure
export const loadRouteLocation = m.loadRouteLocation
export const matchedRouteKey = m.matchedRouteKey
export const onBeforeRouteLeave = m.onBeforeRouteLeave
export const onBeforeRouteUpdate = m.onBeforeRouteUpdate
export const parseQuery = m.parseQuery
export const routeLocationKey = m.routeLocationKey
export const routerKey = m.routerKey
export const routerViewLocationKey = m.routerViewLocationKey
export const stringifyQuery = m.stringifyQuery
export const useLink = m.useLink
export const useRoute = m.useRoute
export const useRouter = m.useRouter
export const viewDepthKey = m.viewDepthKey
