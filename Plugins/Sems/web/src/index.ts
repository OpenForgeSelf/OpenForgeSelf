/**
 * sems 插件界面入口。
 * 产物为 ESM，入口固定 `web/dist/index.js`；导出名与 plugin.json 的
 * `Frontend.Views[0]`（SemsView）一致，宿主加载器按该名字取组件。
 * vue 为外部依赖，经宿主 import map 解析到宿主同一份实例。
 */
import { createApp, h } from 'vue'
import SemsView from './SemsView.vue'

export { SemsView }
export default SemsView

/**
 * 独立预览挂载（仅本地开发调试用，宿主加载时不会调用）。
 */
if (typeof document !== 'undefined' && document.getElementById('sems-preview')) {
  createApp({ render: () => h(SemsView) }).mount('#sems-preview')
}