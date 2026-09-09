/**
 * Element Plus 共享模块桥（程序化 API 子集 + 界面组件）。
 *
 * 说明：与 vue / vue-router / pinia 不同，本文件为**手工维护**的清单，只包含
 * 插件常用的**程序化** API（ElMessage / ElMessageBox / ElNotification / ElLoading 等）
 * 与**界面组件**（ElButton / ElScrollbar / ElTag / ElProgress / ElEmpty 等）。
 *
 * 原因：插件是独立预编译产物（宿主 unplugin-vue-components 不处理插件模板），
 * 插件模板显式 `import { ElXxx } from 'element-plus'` 后，经 import map 解析到本 shim，
 * 从宿主 window.__FORGE_SHARED__.elementPlus 取**同一份**组件实例（避免 Vue 双实例）；
 * 组件样式由插件构建时自行引入（index.ts 内 element-plus 各组件 style 入口）。
 * 完整导出达数百项且含浏览器端资源，不适合在 Node 侧自动枚举，故只维护插件实际使用的清单。
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

// ---- 界面组件（插件模板显式 import 使用；清单与宿主 exposeSharedDeps.ts 同步）----
export const ElButton = pick('ElButton')
export const ElScrollbar = pick('ElScrollbar')
export const ElTag = pick('ElTag')
export const ElProgress = pick('ElProgress')
export const ElEmpty = pick('ElEmpty')
export const ElSkeleton = pick('ElSkeleton')
export const ElSkeletonItem = pick('ElSkeletonItem')
