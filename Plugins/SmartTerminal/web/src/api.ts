import { apiGet, apiPost, apiPut } from './http'
import type {
  AuditEvent,
  CommandRecord,
  LaunchTerminalRequest,
  LaunchTerminalResponse,
  OutputChunk,
  PendingApproval,
  PermissionMode,
  RunCommandResponse,
  ShellProfile,
  TerminalSession,
} from './types'

const BASE = '/api/smart-terminal'

export const smartTerminalApi = {
  listShells: () => apiGet<ShellProfile[]>(`${BASE}/shells`),
  getPermissionMode: () => apiGet<{ mode: string }>(`${BASE}/permission-mode`),
  setPermissionMode: (mode: PermissionMode) => apiPut<{ mode: string }>(`${BASE}/permission-mode`, { mode }),
  listSessions: () => apiGet<TerminalSession[]>(`${BASE}/sessions`),
  createSession: (body: { title?: string; shellId?: string; cwd?: string; permissionMode?: PermissionMode }) =>
    apiPost<TerminalSession>(`${BASE}/sessions`, body),
  launch: (body: LaunchTerminalRequest) => apiPost<LaunchTerminalResponse>(`${BASE}/launch`, body),
  renameSession: (sessionId: string, title: string) =>
    apiPut<TerminalSession>(`${BASE}/sessions/${encodeURIComponent(sessionId)}/title`, { title }),
  getSession: (sessionId: string) => apiGet<TerminalSession>(`${BASE}/sessions/${encodeURIComponent(sessionId)}`),
  setSessionMode: (sessionId: string, mode: PermissionMode) =>
    apiPut(`${BASE}/sessions/${encodeURIComponent(sessionId)}/permission-mode`, { mode }),
  run: (sessionId: string, command: string) =>
    apiPost<RunCommandResponse>(`${BASE}/sessions/${encodeURIComponent(sessionId)}/run`, { command }),
  output: (sessionId: string, afterIndex = 0) =>
    apiGet<{ sessionId: string; status: string; chunks: OutputChunk[]; nextIndex: number; activeCommandId?: string }>(
      `${BASE}/sessions/${encodeURIComponent(sessionId)}/output?afterIndex=${afterIndex}`,
    ),
  commands: (sessionId: string) =>
    apiGet<CommandRecord[]>(`${BASE}/sessions/${encodeURIComponent(sessionId)}/commands`),
  rerun: (sessionId: string, commandId: string) =>
    apiPost<RunCommandResponse>(`${BASE}/sessions/${encodeURIComponent(sessionId)}/rerun/${encodeURIComponent(commandId)}`),
  close: (sessionId: string) => apiPost(`${BASE}/sessions/${encodeURIComponent(sessionId)}/close`),
  listApprovals: (sessionId?: string) =>
    apiGet<PendingApproval[]>(`${BASE}/approvals${sessionId ? `?sessionId=${encodeURIComponent(sessionId)}` : ''}`),
  resolveApproval: (approvalId: string, approve: boolean, note?: string) =>
    apiPost<RunCommandResponse>(`${BASE}/approvals/${encodeURIComponent(approvalId)}`, { approve, note }),
  audit: (take = 80) => apiGet<{ items: AuditEvent[] }>(`${BASE}/audit?take=${take}`),
}

export async function fetchPluginVersion(pluginId: string): Promise<string> {
  const data = await apiGet<{ id?: string; version?: string }[]>('/api/plugin')
  const list = Array.isArray(data) ? data : []
  return list.find(p => p?.id === pluginId)?.version ?? '未知版本'
}
