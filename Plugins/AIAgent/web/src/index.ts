/**
 * AIAgent 插件界面入口。
 *
 * 新聊天页为 assistant/ 下的全新实现，不再导出或挂载既有聊天页视图。
 * 导出名 AiAgentView 仍与 plugin.json frontend.views[0] 一致。
 * 另导出 AgentAssistant，供宿主 loadAgentAssistant() 嵌入右侧助手面板。
 */

import { createApp, h } from 'vue'
import AgentAssistant from './assistant/AgentAssistant.vue'
import AgentAssistantPage from './assistant/AgentAssistantPage.vue'

export { AgentAssistant }
export const AiAgentView = AgentAssistantPage
export default AgentAssistantPage

if (typeof document !== 'undefined' && document.getElementById('aiagent-preview')) {
  createApp({ render: () => h(AgentAssistantPage) }).mount('#aiagent-preview')
}
