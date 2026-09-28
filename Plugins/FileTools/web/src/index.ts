import FileToolsView from './views/FileToolsView.vue'

/**
 * FileTools 插件界面入口（ESM，产物固定 web/dist/index.js）。
 *
 * 导出名必须等于 plugin.json 的 `frontend.views[0]`（=`FileToolsView`）：
 * 宿主 `dynamicPlugins.ts` 按该名取组件，取不到才回退 default。
 * Tailwind 由本插件自备（宿主只编译宿主自己的工具类）。
 */
import './styles/tailwind.css'

export { FileToolsView }
export default FileToolsView
