/**
 * 设计系统插件界面入口（Stardust Design System）。
 *
 * 契约（specs/010-plugin-frontend-runtime）：
 * - 产物为 ESM，入口固定 web/dist/index.js。
 * - 导出名必须与 plugin.json 中 frontend.views[0] 一致（DesignSystemView），
 *   宿主加载器按该名字取组件；取不到时回退 default 导出。
 * - vue 为外部依赖，经宿主 import map 解析到宿主同一份实例。
 */

import { createApp, h } from 'vue'
import DesignSystemView from './DesignSystemView.vue'
import './styles/tokens.css'
import './styles/base.css'

export { DesignSystemView }
export default DesignSystemView

/**
 * 独立预览挂载（仅本地调试；宿主远程加载走上面的具名导出）。
 * 以静态方式打开 dist/index.html 时可见界面。
 */
if (typeof document !== 'undefined' && document.getElementById('design-system-preview')) {
  createApp({ render: () => h(DesignSystemView) }).mount('#design-system-preview')
}
