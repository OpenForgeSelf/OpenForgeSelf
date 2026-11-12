import { apiDelete, apiGet, apiPatch, apiPost, apiPut } from './http'
import type {
  Approval,
  Block,
  ChatMessage,
  CommandResult,
  CommandSource,
  CreateSessionBody,
  PermissionMode,
  Profile,
  SecretMeta,
  Session,
  ShellInfo,
  Snippet,
} from './types'

export const API_PREFIX = '/api/smart-terminal'
export const SESSIONS_PATH = `${API_PREFIX}/sessions`

export const api = {
  meta: () => apiGet<{ pluginId: string; version: string; permissionDefault: PermissionMode }>(`${API_PREFIX}/meta`),
  shells: () => apiGet<ShellInfo[]>(`${API_PREFIX}/shells`),
  permissionMode: () => apiGet<{ mode: PermissionMode; sessionDefault: PermissionMode }>(`${API_PREFIX}/permission-mode`),
  setPermissionMode: (mode: PermissionMode) => apiPut(`${API_PREFIX}/permission-mode`, { mode }),
  setSessionPermissionMode: (id: string, mode: PermissionMode) =>
    apiPut(`${API_PREFIX}/sessions/${id}/permission-mode`, { mode }),

  profiles: (q?: { kind?: string; q?: string }) => {
    const qs = new URLSearchParams()
    if (q?.kind) qs.set('kind', q.kind)
    if (q?.q) qs.set('q', q.q)
    const suffix = qs.toString() ? `?${qs}` : ''
    return apiGet<Profile[]>(`${API_PREFIX}/profiles${suffix}`)
  },
  createProfile: (body: Partial<Profile>) => apiPost<Profile>(`${API_PREFIX}/profiles`, body),

  secrets: () => apiGet<SecretMeta[]>(`${API_PREFIX}/secrets`),

  sessions: () => apiGet<Session[]>(SESSIONS_PATH),
  session: (id: string) => apiGet<Session>(`${API_PREFIX}/sessions/${id}`),
  createSession: (body: CreateSessionBody) => apiPost<Session>(SESSIONS_PATH, body),
  disconnect: (id: string) => apiPost(`${API_PREFIX}/sessions/${id}/disconnect`),
  reconnect: (id: string, password?: string) =>
    apiPost<Session>(`${API_PREFIX}/sessions/${id}/reconnect`, password ? { password } : {}),
  closeSession: (id: string) => apiPost(`${API_PREFIX}/sessions/${id}/close`),
  renameSession: (id: string, title: string) => apiPatch(`${API_PREFIX}/sessions/${id}`, { title }),

  runCommand: (id: string, command: string, source: CommandSource) =>
    apiPost<CommandResult>(`${API_PREFIX}/sessions/${id}/commands`, { command, source }),
  blocks: (id: string, afterSeq = 0) =>
    apiGet<{ prompt?: string; status?: string; items: Block[] }>(`${API_PREFIX}/sessions/${id}/blocks?afterSeq=${afterSeq}`),
  history: (id: string) => apiGet<string[] | { items: string[] }>(`${API_PREFIX}/sessions/${id}/commands`),

  approvals: (sessionId?: string) => {
    const qs = sessionId ? `?sessionId=${encodeURIComponent(sessionId)}&status=pending` : '?status=pending'
    return apiGet<Approval[]>(`${API_PREFIX}/approvals${qs}`)
  },
  resolveApproval: (id: string, decision: 'approve' | 'reject') =>
    apiPost<CommandResult | { status: string }>(`${API_PREFIX}/approvals/${id}/resolve`, { decision }),

  snippets: () => apiGet<Snippet[]>(`${API_PREFIX}/snippets`),

  chat: (id: string) => apiGet<{ messages: ChatMessage[]; model?: string }>(`${API_PREFIX}/sessions/${id}/chat`),
  sendChat: (id: string, content: string, blockId?: string) =>
    apiPost<ChatMessage>(`${API_PREFIX}/sessions/${id}/chat/messages`, { content, blockId }),
}

export function asList<T>(value: T[] | { items?: T[] } | undefined): T[] {
  if (!value) return []
  if (Array.isArray(value)) return value
  return value.items ?? []
}
