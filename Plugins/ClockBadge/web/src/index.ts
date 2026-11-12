/**
 * ClockBadge 插件界面入口。
 *
 * 约定（契约见 QuickLinks/web/src/index.ts 同款）：
 * - 产物为 ESM，入口固定 `web/dist/index.js`。
 * - 导出名必须与 plugin.json 中 `frontend.views[0]` 一致（当前为 `ClockBadgeView`），
 *   宿主加载器按该名字取组件；取不到时回退到 default 导出。
 * - vue / vue-router / pinia / element-plus 为外部依赖，经宿主页面的 import map
 *   解析到宿主同一份实例。
 */

import ClockBadgeView from './ClockBadgeView.vue'

export { ClockBadgeView }
export default ClockBadgeView
