/**
 * 首页（Home）插件界面入口。
 *
 * 约定（契约见 specs/010-plugin-frontend-runtime/contracts/frontend-contributes.md）：
 * - 产物为 ESM，入口固定 `web/dist/index.js`。
 * - 导出名必须与 plugin.json 中 `frontend.views[0]` 一致（当前为 `HomeView`），
 *   宿主加载器按该名字取组件；取不到时回退到 default 导出。
 * - vue / element-plus 等为外部依赖，经宿主页面的 import map 解析到宿主同一份实例。
 *
 * 首页 UI 用原生 HTML + `--el-*` CSS 变量（见 HomeView.vue 的 scoped 样式），
 * 不依赖 Element Plus 组件，故此处无需 import EP 组件样式；如后续引入 EP 组件，
 * 须在此按宿主 exposeSharedDeps 清单补对应 `element-plus/es/components/<x>/style/css`。
 */

import { createApp, h } from 'vue'
import './styles/tailwind.css'
import HomeView from './HomeView.vue'

export { HomeView }
export default HomeView

/**
 * 独立预览挂载（仅用于本地开发调试，宿主加载时不会调用）。
 * 直接以静态方式打开 dist/index.html 时可看到界面；宿主远程加载走上面的具名导出。
 */
if (typeof document !== 'undefined' && document.getElementById('home-preview')) {
  createApp({ render: () => h(HomeView) }).mount('#home-preview')
}
