export type SessionKind = 'local' | 'ssh'
export type AuthMethod = 'key' | 'interactive'
export type SessionStatus = 'connecting' | 'connected' | 'disconnected'
export type PermissionMode = 'Auto' | 'AskWrite' | 'AskHighRisk'
export type Risk = 'read' | 'write' | 'high' | 'deny'
export type Decision = 'allow' | 'ask' | 'deny'
export type BlockKind = 'command' | 'system'
export type BlockStatus = 'running' | 'done' | 'failed' | 'awaiting_approval' | 'rejected'
export type CommandSource = 'prompt' | 'snippet' | 'assistant'
export type RailTab = 'ai' | 'snippets' | 'history'
export type PageState = 'loading' | 'ready' | 'empty' | 'error' | 'unauthorized'

export interface ShellInfo {
  id: string
  displayName: string
  executable: string
  kind: string
  available: boolean
  note?: string
}

export interface Profile {
  id: string
  name: string
  kind: SessionKind
  shellId?: string
  cwd?: string
  host?: string | null
  port?: number | null
  user?: string | null
  auth?: AuthMethod | null
  secretId?: string | null
  identityFile?: string | null
  summary?: string
}

export interface SecretMeta {
  id: string
  kind: string
  fingerprint: string
  label: string
  createdAt: string
}

export interface Block {
  id: string
  seq: number
  kind: BlockKind
  command: string
  output: string
  exitCode?: number | null
  status: BlockStatus
  durationMs?: number
  startedAt?: string
}

export interface Session {
  id: string
  title: string
  profileId?: string | null
  kind: SessionKind
  shell?: string
  cwd?: string
  status: SessionStatus
  host?: string | null
  port?: number | null
  user?: string | null
  auth?: AuthMethod | null
  secretId?: string | null
  processAlive?: boolean
  prompt?: string
  permissionMode?: PermissionMode
  blocks?: Block[]
}

export interface Approval {
  id: string
  command: string
  risk: Risk
  mode: PermissionMode
  status: 'pending' | 'approved' | 'rejected'
  sessionId?: string
}

export interface CommandResult {
  status: 'running' | 'awaiting_approval' | 'rejected'
  decision: Decision
  risk: Risk
  block?: Block
  approval?: Approval
}

export interface Snippet {
  id: string
  name: string
  command: string
}

export interface ChatMessage {
  id?: string
  role: 'user' | 'assistant'
  content: string
  suggestedCommand?: string | null
}

export interface CreateSessionBody {
  profileId?: string
  kind: SessionKind
  title?: string
  shellId?: string
  cwd?: string
  host?: string
  port?: number
  user?: string
  auth?: AuthMethod | null
  secretId?: string | null
  identityFile?: string | null
  password?: string
  permissionMode?: PermissionMode
}

export interface RestoreTab {
  id: string
  title: string
  profileId?: string | null
  kind: SessionKind
  shell?: string | null
  cwd?: string | null
  status: SessionStatus
  host?: string | null
  port?: number | null
  user?: string | null
  auth?: AuthMethod | null
  blocks: Block[]
}

export interface RestoreSnapshot {
  activeId: string | null
  railOpen: boolean
  railTab: RailTab
  tabs: RestoreTab[]
}

export type StoryId =
  | 'S0'
  | 'S1'
  | 'S2'
  | 'S3'
  | 'S4'
  | 'S5'
  | 'S6'
  | 'S7'
  | 'S8'
  | 'S9'
  | 'S10'
  | 'S11-loading'
  | 'S11-error'
  | 'S11-401'
  | 'S-R'

/** 旧版启动器兼容类型（本里程碑不作为 S0–S-R 验收主路径） */
export type LaunchType = 'local' | 'ssh' | 'wsl-ssh'
export interface LaunchStep { command: string; args?: string[] }
export interface LaunchTerminalRequest {
  profile?: string
  type?: LaunchType | string
  shellId?: string
  cwd?: string
  title?: string
  permissionMode?: PermissionMode
  agentId?: string
  commands?: string[]
  steps?: LaunchStep[]
  env?: Record<string, string>
  host?: string
  port?: number
  user?: string
  password?: string
  auth?: 'sshpass' | 'key' | 'interactive' | string
  identityFile?: string
  acceptNewHostKey?: boolean
}

/** ===== v0.1 兼容垫片（2026-11-12 输入5 · 发布链解堵）=====
 * v0.2 提示符主路径重做（6892948）从本文件移除了下列旧类型，但迁移残留的
 * `sessionRestore.ts` / `sessionRestore.test.ts` 与宿主 CT story
 * （ForgeSelf.Web/src/components/SmartTerminal.story.ts / SmartTerminalStoryHost.vue）
 * 仍按 v0.1 形状引用，HEAD `vue-tsc -b` 因此失败、release-local 发布链卡死。
 * 本段按 fb48fe5 版本原样补回，**纯类型、零运行时行为**；
 * 待 smart-terminal v2 批完成迁移（sessionRestore 死代码退役、story 迁 restore/fixtures）后整体删除本段。
 */
export type ShellProfile = ShellInfo

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
  /** CT：custom:<id> 不可用红条（testid=custom-unsupported-banner） */
  customUnsupported?: boolean
  /** CT：S-R 重连黄条文案 */
  reconnectBanner?: string
  /**
   * CT：写入 sessionStorage 的恢复快照（宿主壳负责落盘）。
   * 可含故意污染的 password 字段；落盘前必须 strip。
   */
  seedSessionRestore?: Record<string, unknown>
}
