/**
 * AIAgent 插件界面入口。
 *
 * 约定（契约见 specs/010-plugin-frontend-runtime/contracts/frontend-contributes.md）：
 * - 产物为 ESM，入口固定 `web/dist/index.js`。
 * - 导出名必须与 plugin.json 中 `Frontend.Views[0]` 一致（当前为 `AiAgentView`），
 *   宿主加载器按该名字取组件；取不到时回退到 default 导出。
 * - vue / element-plus 等为外部依赖，经宿主页面的 import map 解析到宿主同一份实例。
 */

import { createApp, h } from 'vue'
import AiAgentView from './AiAgentView.vue'

export { AiAgentView }
export default AiAgentView

/**
 * 独立预览挂载（仅用于本地开发调试，宿主加载时不会调用）。
 * 直接以静态方式打开 dist/index.html 时可看到界面；宿主远程加载走上面的具名导出。
 */
if (typeof document !== 'undefined' && document.getElementById('aiagent-preview')) {
  createApp({ render: () => h(AiAgentView) }).mount('#aiagent-preview')
}
