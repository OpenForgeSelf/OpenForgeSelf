import { agentRequest, agentAuthHeaders, agentUrl } from './http'
import type {
  AgentAttachment,
  AgentCatalogAgent,
  AgentCatalogModel,
  AgentEmbed,
  AgentMode,
  AgentSendResult,
  AgentSessionSummary,
  AgentWorkspacePanelId,
} from './types'

export interface ListSessionsQuery {
  pluginId?: string
  surfaceKey?: string
}

export function listSessions(query: ListSessionsQuery = {}): Promise<AgentSessionSummary[]> {
  const q = new URLSearchParams()
  if (query.pluginId) q.set('pluginId', query.pluginId)
  if (query.surfaceKey) q.set('surfaceKey', query.surfaceKey)
  const suffix = q.toString() ? `?${q}` : ''
  return agentRequest<AgentSessionSummary[]>(`/sessions${suffix}`)
}

export function createSession(body: {
  mode: AgentMode
  agentId?: string
  modelId?: string
  title?: string
  embed?: AgentEmbed
}): Promise<AgentSessionSummary> {
  return agentRequest('/sessions', { method: 'POST', json: body })
}

export function getSession(id: string): Promise<Record<string, unknown>> {
  return agentRequest(`/sessions/${encodeURIComponent(id)}`)
}

export function patchSession(
  id: string,
  body: { mode?: AgentMode; agentId?: string; modelId?: string; title?: string }
): Promise<AgentSessionSummary> {
  return agentRequest(`/sessions/${encodeURIComponent(id)}`, { method: 'PATCH', json: body })
}

export function sendMessage(
  id: string,
  body: { text: string; attachments?: AgentAttachment[]; embed?: AgentEmbed }
): Promise<AgentSendResult> {
  return agentRequest(`/sessions/${encodeURIComponent(id)}/messages`, { method: 'POST', json: body })
}

export function confirmPlan(id: string): Promise<void> {
  return agentRequest(`/sessions/${encodeURIComponent(id)}/plan/confirm`, { method: 'POST', json: {} })
}

export function revisePlan(id: string, body: { steps?: unknown; note?: string }): Promise<void> {
  return agentRequest(`/sessions/${encodeURIComponent(id)}/plan/revise`, { method: 'POST', json: body })
}

export function decideApproval(id: string, approvalId: string, approved: boolean, note?: string): Promise<void> {
  return agentRequest(
    `/sessions/${encodeURIComponent(id)}/approvals/${encodeURIComponent(approvalId)}/decide`,
    { method: 'POST', json: { approved, note } }
  )
}

export function stopRun(id: string, runId: string): Promise<void> {
  return agentRequest(`/sessions/${encodeURIComponent(id)}/runs/${encodeURIComponent(runId)}/stop`, {
    method: 'POST',
    json: {},
  })
}

export function intervene(
  id: string,
  runId: string,
  body: { action: 'continue' | 'skip' | 'override' | 'retry' | 'cancel'; input?: string; stepId?: string }
): Promise<void> {
  return agentRequest(`/sessions/${encodeURIComponent(id)}/runs/${encodeURIComponent(runId)}/intervene`, {
    method: 'POST',
    json: body,
  })
}

export function pauseWorkflow(id: string, workflowId: string): Promise<void> {
  return agentRequest(
    `/sessions/${encodeURIComponent(id)}/workflows/${encodeURIComponent(workflowId)}/pause`,
    { method: 'POST', json: {} }
  )
}

export function cancelWorkflow(id: string, workflowId: string): Promise<void> {
  return agentRequest(
    `/sessions/${encodeURIComponent(id)}/workflows/${encodeURIComponent(workflowId)}/cancel`,
    { method: 'POST', json: {} }
  )
}

export function getWorkspace(id: string, panel?: Exclude<AgentWorkspacePanelId, 'catalog'>): Promise<Record<string, unknown>> {
  const q = panel ? `?panel=${encodeURIComponent(panel)}` : ''
  return agentRequest(`/sessions/${encodeURIComponent(id)}/workspace${q}`)
}

export function writeMemory(id: string, body: Record<string, unknown>): Promise<void> {
  return agentRequest(`/sessions/${encodeURIComponent(id)}/workspace/memory/writeback`, { method: 'POST', json: body })
}

export function commitDesign(id: string, body: Record<string, unknown>): Promise<void> {
  return agentRequest(`/sessions/${encodeURIComponent(id)}/workspace/design/commit`, { method: 'POST', json: body })
}

export function dispatchTodo(id: string, todoId: string): Promise<void> {
  return agentRequest(
    `/sessions/${encodeURIComponent(id)}/workspace/todos/${encodeURIComponent(todoId)}/dispatch`,
    { method: 'POST', json: {} }
  )
}

export function addSnippet(id: string, body: Record<string, unknown>): Promise<void> {
  return agentRequest(`/sessions/${encodeURIComponent(id)}/workspace/snippets`, { method: 'POST', json: body })
}

export function listModels(): Promise<AgentCatalogModel[]> {
  return agentRequest('/catalog/models')
}

export function listAgents(): Promise<AgentCatalogAgent[]> {
  return agentRequest('/catalog/agents')
}

export function eventsUrl(id: string, after?: number): string {
  const q = after != null ? `?after=${encodeURIComponent(String(after))}` : ''
  return agentUrl(`/sessions/${encodeURIComponent(id)}/events${q}`)
}

export { agentAuthHeaders }
