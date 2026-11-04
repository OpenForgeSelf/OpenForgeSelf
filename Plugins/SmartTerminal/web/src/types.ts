export type PermissionMode = 'Auto' | 'AskWrite' | 'AskHighRisk'
export type LaunchType = 'local' | 'ssh' | 'wsl-ssh'

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
  launchType?: LaunchType | string
  remoteHost?: string
  remoteUser?: string
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

export interface LaunchTerminalRequest {
  type?: LaunchType | string
  shellId?: string
  cwd?: string
  title?: string
  permissionMode?: PermissionMode
  agentId?: string
  commands?: string[]
  host?: string
  port?: number
  user?: string
  /** 短暂凭证：前端读完即从地址栏擦除，请求体发送后丢弃 */
  password?: string
  auth?: 'sshpass' | 'key' | 'interactive' | string
  identityFile?: string
  acceptNewHostKey?: boolean
}

export interface LaunchTerminalResponse {
  session: TerminalSession
  commandResults: RunCommandResponse[]
  launchType: string
  message: string
  passwordProvided: boolean
  warning?: string
}

/** CT 画廊 / 演示用固定态（跳过网络） */
export interface SmartTerminalFixture {
  version?: string
  shells?: ShellProfile[]
  sessions?: TerminalSession[]
  activeId?: string
  chunks?: OutputChunk[]
  /** 按 sessionId 分桶的输出（多标签演示） */
  chunksBySession?: Record<string, OutputChunk[]>
  commands?: CommandRecord[]
  commandsBySession?: Record<string, CommandRecord[]>
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
  /** 模拟 URL 启动横幅 */
  launchBanner?: string
  launchWarning?: string
  renamingId?: string
  showNewTabMenu?: boolean
  /** CT：展示密码风险知情确认模态 */
  showPasswordAck?: boolean
  passwordAckChecked?: boolean
  passwordAckMode?: 'always-ack' | 'intranet' | 'disabled'
  pendingLaunchSummary?: string
}

