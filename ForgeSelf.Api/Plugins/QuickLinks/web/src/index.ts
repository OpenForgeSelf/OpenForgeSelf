/**
 * QuickLinks 插件界面入口。
 *
 * 约定（契约见 specs/010-plugin-frontend-runtime/contracts/frontend-contributes.md）：
 * - 产物为 ESM，入口固定 `web/dist/index.js`。
 * - 导出名必须与 plugin.json 中 `Frontend.Views[0]` 一致（当前为 `QuickLinksView`），
 *   宿主加载器按该名字取组件；取不到时回退到 default 导出。
 * - vue / vue-router / pinia / element-plus 等为外部依赖，经宿主页面的 import map
 *   解析到宿主同一份实例（含宿主已安装的 Pinia 实例）。
 */

import { createApp, h } from 'vue'
import { createPinia } from 'pinia'
import QuickLinksView from './QuickLinksView.vue'

export { QuickLinksView }
export default QuickLinksView

/**
 * 独立预览挂载（仅用于本地开发调试，宿主加载时不会调用）。
 * 宿主模式下 Pinia 由宿主安装，此处预览需自行 createPinia。
 */
if (typeof document !== 'undefined' && document.getElementById('quicklinks-preview')) {
  const app = createApp({ render: () => h(QuickLinksView) })
  app.use(createPinia())
  app.mount('#quicklinks-preview')
}
