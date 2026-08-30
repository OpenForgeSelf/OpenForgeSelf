/**
 * Element Plus 共享模块桥（程序化 API 子集）。
 *
 * 说明：与 vue / vue-router / pinia 不同，本文件为**手工维护**的清单，只包含
 * 插件常用的**程序化** API（ElMessage / ElMessageBox / ElNotification / ElLoading 等）。
 *
 * 原因：Element Plus 的组件按项目约定（AGENTS.md）由宿主全局注册，插件在模板中
 * 直接使用 <ElXxx> 即可，无需也无法从 element-plus 具名导入组件；
 * 而 element-plus 完整导出达数百项且包含浏览器端资源，不适合在 Node 侧自动枚举。
 *
 * 运行期从宿主挂载的 window.__FORGE_SHARED__.elementPlus 再导出，
 * 保证插件调用的是宿主同一份实例。若某个导出在宿主上不存在则导出为 undefined，
 * 由插件侧自行判空，避免整体加载失败。
 */
const m = window.__FORGE_SHARED__?.elementPlus ?? {}

/** 取宿主上的导出；不存在时返回 undefined 而不是抛错，避免拖垮整个插件界面。 */
const pick = (name) => m[name]

export const ElMessage = pick('ElMessage')
export const ElMessageBox = pick('ElMessageBox')
export const ElNotification = pick('ElNotification')
export const ElLoading = pick('ElLoading')
export const ElLoadingDirective = pick('ElLoadingDirective')
export const ElInfiniteScroll = pick('ElInfiniteScroll')
export const ElConfigProvider = pick('ElConfigProvider')
export const dayjs = pick('dayjs')
export const zhCn = pick('zhCn')
export const enUs = pick('enUs')
export const locale = pick('locale')
export const useFormItem = pick('useFormItem')
export const useLocale = pick('useLocale')
export const useZIndex = pick('useZIndex')
export const useId = pick('useId')
export const version = pick('version')
export const install = pick('install')
