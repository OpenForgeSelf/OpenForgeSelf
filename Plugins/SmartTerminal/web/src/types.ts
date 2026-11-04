export type PermissionMode = 'Auto' | 'AskWrite' | 'AskHighRisk'
export type LaunchType = 'local' | 'ssh' | 'wsl-ssh'

/** 通用启动器单步：command + 可选 args；亦可整行写入 command */
export interface LaunchStep {
  command: string
  args?: string[]
}

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
  /** 活动进程是否仍在（GET session） */
  processAlive?: boolean
  /** 未 close 时可继续拉 output */
  reconnectable?: boolean
  /** 重连提示；缺密 SSH 需再走 S5 后 launch */
  reconnectHint?: string
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
  /** 通用启动器主键；遗留 type 仍兼容 */
  profile?: string
  type?: LaunchType | string
  shellId?: string
  cwd?: string
  title?: string
  permissionMode?: PermissionMode
  agentId?: string
  /** 扁平命令行列表（兼容）；由 step/run 展开 */
  commands?: string[]
  /** 结构化步骤（优先于仅 commands 的语义说明） */
  steps?: LaunchStep[]
  /** 仅注入本会话进程环境 */
  env?: Record<string, string>
  host?: string
  port?: number
  user?: string
  /** 短暂凭证：前端读完即从地址栏擦除，请求体发送后丢弃；交互输入亦仅内存 */
  password?: string
  auth?: 'sshpass' | 'key' | 'interactive' | string
  identityFile?: string
  acceptNewHostKey?: boolean
}

export interface LaunchTerminalResponse {
  session: TerminalSession
  commandResults: RunCommandResponse[]
  launchType: string
  profile?: string
  message: string
  passwordProvided: boolean
  warning?: string
  needsInteractivePassword?: boolean
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
  /** CT：交互式密码输入模态（无 URL 密码时） */
  showInteractivePassword?: boolean
  interactivePasswordHost?: string
  interactivePasswordUser?: string
  interactivePasswordPort?: number
  interactivePasswordProfile?: string
}

