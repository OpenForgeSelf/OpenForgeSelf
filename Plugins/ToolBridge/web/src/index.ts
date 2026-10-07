/**
 * ToolBridge 插件界面入口（契约同 Plugins/QuickLinks/web/src/index.ts）：
 * - 产物 ESM，入口固定 web/dist/index.js；
 * - 导出名必须等于 plugin.json 的 views[0]（ToolBridgeView），宿主取不到时回退 default。
 */

import ToolBridgeView from './ToolBridgeView.vue'

export { ToolBridgeView }
export default ToolBridgeView
