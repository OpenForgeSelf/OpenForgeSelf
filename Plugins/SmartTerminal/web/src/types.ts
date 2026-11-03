export type PermissionMode = 'Auto' | 'AskWrite' | 'AskHighRisk'

export interface ShellProfile {
  id: string
  displayName: string
  executable: string
  kind: string
  available: boolean
  note?: string
}

export interface TerminalSession {
  sessionId: string
  title: string
  shellId: string
  shellDisplayName: string
  cwd: string
  permissionMode: PermissionMode | number
  status: string
  agentId?: string
  createdAt: string
  updatedAt: string
  commandCount: number
}

export interface OutputChunk {
  index: number
  stream: string
  text: string
  at: string
}

export interface PendingApproval {
  approvalId: string
  sessionId: string
  commandId: string
  commandRedacted: string
  risk: number | string
  matchedRule: string
  reason: string
  mode: PermissionMode | number
  createdAt: string
}

export interface AuditEvent {
  eventId: string
  time: string
  kind: string
  sessionId?: string
  agentId?: string
  shellId?: string
  cwd?: string
  commandRedacted?: string
  decision?: string
  matchedRule?: string
  exitCode?: number
  detail?: string
}

export interface RunCommandResponse {
  sessionId: string
  commandId: string
  status: string
  approvalId?: string
  message: string
}

export interface CommandRecord {
  commandId: string
  sessionId: string
  commandRedacted: string
  risk: number | string
  decision: number | string
  matchedRule: string
  status: string
  exitCode?: number
  createdAt: string
  approvalId?: string
}
