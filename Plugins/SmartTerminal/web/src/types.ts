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


/** CT 画廊 / 演示用固定态（跳过网络） */
export interface SmartTerminalFixture {
  version?: string
  shells?: ShellProfile[]
  sessions?: TerminalSession[]
  activeId?: string
  chunks?: OutputChunk[]
  commands?: CommandRecord[]
  globalMode?: PermissionMode
  approvals?: PendingApproval[]
  showApproval?: boolean
  activeApproval?: PendingApproval | null
  showAudit?: boolean
  auditItems?: AuditEvent[]
  auditKindFilter?: string
  auditQuery?: string
  errorText?: string
  unauthorized?: boolean
  draftTitle?: string
  draftShell?: string
  draftCwd?: string
  commandInput?: string
}
