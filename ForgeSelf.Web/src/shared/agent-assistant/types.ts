/** 智能体助手嵌入契约。实现在 ai-agent 插件，宿主与其它插件只消费。 */

export type AgentAssistantLayout = 'page' | 'panel'

export interface AgentEmbedContext {
  pluginId: string
  surfaceKey?: string
  projectId?: string
  cwd?: string
  objectType?: string
  objectId?: string
  title?: string
  extras?: Record<string, string>
}

export interface AgentToolScope {
  allow?: string[]
  deny?: string[]
  allowCapabilities?: string[]
}

export interface AgentAssistantProps {
  layout: AgentAssistantLayout
  context?: AgentEmbedContext
  toolScope?: AgentToolScope
  surfaceKey?: string
  initialPanel?: string
}
