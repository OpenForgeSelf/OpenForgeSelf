/**
 * 成本观测插件界面入口。
 *
 * 契约（specs/010-plugin-frontend-runtime）：
 * - 产物为 ESM，入口固定 web/dist/index.js（与 plugin.json 的 frontend.entry 一致）。
 * - 导出名必须与 plugin.json 中 frontend.views[0] 一致（CostScopeView），
 *   宿主加载器按该名字取组件；取不到时回退 default 导出。
 * - vue 为外部依赖，经宿主 import map 解析到宿主同一份实例。
 */
import { createApp, h } from 'vue'
import CostScopeView from './CostScopeView.vue'

export { CostScopeView }
export default CostScopeView

/**
 * 独立预览挂载（仅本地调试；宿主远程加载走上面的具名导出）。
 */
if (typeof document !== 'undefined' && document.getElementById('cost-scope-preview')) {
  createApp({ render: () => h(CostScopeView) }).mount('#cost-scope-preview')
}
