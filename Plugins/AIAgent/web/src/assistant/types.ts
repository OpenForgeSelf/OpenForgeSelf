/** 新聊天页与统一入口 /api/agent 的前端类型。不依赖旧聊天页。 */

export type AgentMode = 'open' | 'planned'
export type AgentAssistantLayout = 'page' | 'panel'
export type AgentSessionStatus =
  | 'idle'
  | 'running'
  | 'awaiting_plan'
  | 'awaiting_approval'
  | 'stuck'
  | 'completed'
  | 'failed'
  | 'cancelled'

export type AgentWorkspacePanelId =
  | 'catalog'
  | 'plan'
  | 'records'
  | 'workflow'
  | 'memory'
  | 'design'
  | 'suggest'
  | 'project'
  | 'todo'
  | 'skills'
  | 'tools'
  | 'snippets'

export type AgentPanelLayer = 'chat' | 'sessions' | 'workspace'

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
  allowTools?: string[]
  denyTools?: string[]
  allowCapabilities?: string[]
}

export interface AgentEmbed {
  surface: AgentAssistantLayout
  pluginId: string
  surfaceKey?: string
  context?: AgentEmbedContext
  toolScope?: AgentToolScope
}

export type AgentAttachmentKind = 'skill' | 'prompt' | 'memory' | 'mcp' | 'project' | 'image'

export interface AgentAttachment {
  kind: AgentAttachmentKind
  id: string
  name: string
}

export interface AgentSessionSummary {
  id: string
  title: string
  mode: AgentMode
  status: AgentSessionStatus
  updatedAt: string
}

export interface AgentWorkspaceItemStatus {
  id: Exclude<AgentWorkspacePanelId, 'catalog'>
  name: string
  description: string
  statusText: string
  tone?: 'default' | 'hot' | 'warn'
}

export interface AgentPlan {
  goal?: string
  steps?: Array<{ id?: string; title: string; detail?: string; status?: string }>
  constraints?: string[]
}

export interface AgentApproval {
  id: string
  summary: string
  risk?: string
  action?: string
  resolved?: boolean
  approved?: boolean
}

export interface AgentStuck {
  runId: string
  reason: string
  allowedActions: Array<'continue' | 'skip' | 'override' | 'cancel' | 'retry'>
  stepId?: string
  resolved?: boolean
  outcome?: 'cancelled' | 'continued' | 'skipped'
}

export interface AgentToolCall {
  id: string
  name: string
  summary: string
  detail?: string
  status: 'running' | 'done' | 'failed'
}

export interface AgentThreadUserMessage {
  kind: 'user'
  id: string
  text: string
}

export interface AgentThreadAssistantMessage {
  kind: 'assistant'
  id: string
  text: string
  streaming?: boolean
}

export interface AgentThreadTool {
  kind: 'tool'
  id: string
  tool: AgentToolCall
}

export interface AgentThreadApproval {
  kind: 'approval'
  id: string
  approval: AgentApproval
}

export interface AgentThreadStuck {
  kind: 'stuck'
  id: string
  stuck: AgentStuck
}

export type AgentThreadItem =
  | AgentThreadUserMessage
  | AgentThreadAssistantMessage
  | AgentThreadTool
  | AgentThreadApproval
  | AgentThreadStuck

export interface AgentEventEnvelope {
  type: string
  sessionId: string
  runId?: string
  seq: number
  ts: string
  payload: Record<string, unknown>
}

export interface AgentCatalogModel {
  id: string
  name: string
}

export interface AgentCatalogAgent {
  id: string
  name: string
}

export interface AgentSendResult {
  sessionId: string
  runId: string
}
