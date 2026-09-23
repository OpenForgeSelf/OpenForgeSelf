/**
 * MCP 中心插件界面入口（034 v2.0.0，前身 mcp-gateway + 宿主 mcp-tools 合并）。
 *
 * 约定（契约见 specs/010-plugin-frontend-runtime/contracts/frontend-contributes.md）：
 * - 产物为 ESM，入口固定 `web/dist/index.js`。
 * - 导出名必须与 plugin.json 中 `Frontend.Views[0]` 一致（当前为 `McpCenterView`），
 *   宿主加载器按该名字取组件；取不到时回退到 default 导出。
 * - vue / vue-router / pinia / element-plus 等为外部依赖，经宿主页面的 import map 解析到宿主同一份实例。
 */

import { createApp, h } from 'vue'
import './styles/tailwind.css'
import McpCenterView from './McpCenterView.vue'

// Element Plus 界面组件样式（组件经 import map 从宿主共享桥取同一份实例，样式由插件自备，
// 随插件 style.css 打包；颜色走宿主 --el-* 变量自动跟随主题）。
import 'element-plus/es/components/button/style/css'
import 'element-plus/es/components/scrollbar/style/css'
import 'element-plus/es/components/tag/style/css'
import 'element-plus/es/components/empty/style/css'
import 'element-plus/es/components/skeleton/style/css'
import 'element-plus/es/components/dialog/style/css'
import 'element-plus/es/components/overlay/style/css'
import 'element-plus/es/components/message-box/style/css'
import 'element-plus/es/components/message/style/css'
import 'element-plus/es/components/tabs/style/css'
import 'element-plus/es/components/switch/style/css'
import 'element-plus/es/components/input/style/css'
import 'element-plus/es/components/input-number/style/css'
import 'element-plus/es/components/checkbox/style/css'
import 'element-plus/es/components/loading/style/css'

export { McpCenterView }
export default McpCenterView

/**
 * 独立预览挂载（仅用于本地开发调试，宿主加载时不会调用）。
 * 直接以静态方式打开 dist/index.html 时可看到界面；宿主远程加载走上面的具名导出。
 */
if (typeof document !== 'undefined' && document.getElementById('mcp-center-preview')) {
  createApp({ render: () => h(McpCenterView) }).mount('#mcp-center-preview')
}
