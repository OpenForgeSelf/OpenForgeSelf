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
