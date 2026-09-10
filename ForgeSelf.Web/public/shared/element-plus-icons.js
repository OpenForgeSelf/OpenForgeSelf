/**
 * Element Plus 图标共享模块桥（插件常用图标清单）。
 *
 * 说明：与 element-plus.js 同理，本文件为**手工维护**的清单，只包含插件实际使用
 * 的图标组件（@element-plus/icons-vue 达 300+ 个，逐个枚举白白膨胀，无需全量暴露）。
 * 插件把 `@element-plus/icons-vue` 声明为外部依赖，模板显式 `import { Xxx } from
 * '@element-plus/icons-vue'` 后，经 import map 解析到本 shim，从宿主
 * window.__FORGE_SHARED__.elementPlusIcons 取**同一份**图标实例（避免 Vue 双实例）。
 *
 * 运行期从宿主挂载的 window.__FORGE_SHARED__.elementPlusIcons 再导出；
 * 若某个导出在宿主上不存在则导出为 undefined，由插件侧自行判空，避免整体加载失败。
 */
const m = window.__FORGE_SHARED__?.elementPlusIcons ?? {}

/** 取宿主上的导出；不存在时返回 undefined 而不是抛错，避免拖垮整个插件界面。 */
const pick = (name) => m[name]

export const Folder = pick('Folder')
export const FolderOpened = pick('FolderOpened')
export const Document = pick('Document')
export const Tools = pick('Tools')
export const Lightning = pick('Lightning')
export const Cpu = pick('Cpu')
export const Coin = pick('Coin')
export const Promotion = pick('Promotion')
export const VideoPause = pick('VideoPause')
export const ChatDotRound = pick('ChatDotRound')
export const PieChart = pick('PieChart')
export const Top = pick('Top')
export const Back = pick('Back')
export const Fold = pick('Fold')
export const Expand = pick('Expand')
export const List = pick('List')
export const CircleCheck = pick('CircleCheck')
export const CircleClose = pick('CircleClose')
export const WarningFilled = pick('WarningFilled')
export const Clock = pick('Clock')
export const Loading = pick('Loading')
export const RefreshRight = pick('RefreshRight')
export const Refresh = pick('Refresh')
export const EditPen = pick('EditPen')
export const Right = pick('Right')