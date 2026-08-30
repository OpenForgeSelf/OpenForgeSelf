/**
 * Pinia 状态管理 共享模块桥（自动生成，请勿手工编辑）。
 *
 * 由 scripts/generate-shared-shims.mjs 依据宿主实际安装版本生成；
 * 运行期从宿主挂载的 window.__FORGE_SHARED__ 再导出，保证插件与宿主共用同一实例。
 */
const m = window.__FORGE_SHARED__?.pinia
if (!m) throw new Error(`[ForgeSelf] 共享依赖未就绪: ${target.globalKey}，宿主未挂载 window.__FORGE_SHARED__`)

export const MutationType = m.MutationType
export const acceptHMRUpdate = m.acceptHMRUpdate
export const createPinia = m.createPinia
export const defineStore = m.defineStore
export const disposePinia = m.disposePinia
export const getActivePinia = m.getActivePinia
export const mapActions = m.mapActions
export const mapGetters = m.mapGetters
export const mapState = m.mapState
export const mapStores = m.mapStores
export const mapWritableState = m.mapWritableState
export const setActivePinia = m.setActivePinia
export const setMapStoreSuffix = m.setMapStoreSuffix
export const shouldHydrate = m.shouldHydrate
export const skipHydrate = m.skipHydrate
export const storeToRefs = m.storeToRefs
