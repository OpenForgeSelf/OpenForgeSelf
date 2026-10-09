/**
 * TodoTracker 插件界面的类型定义（与 `Plugins/TodoTracker/Models/*` 的出参一一对应）。
 *
 * 为什么插件里再写一份 TS 类型而不是 import 宿主的 `@/types/todo`：
 * `@/` 是宿主的路径别名，插件是独立预编译产物，运行时解析不到（plugin-development 铁律 4）。
 * 两边靠后端契约测试（`TodoTrackerContractTests`）钉住字段，而不是靠人记得同步。
 */

/** 旧二元状态（Home 面板语义）。 */
export type TodoStatus = 'Pending' | 'Completed'

/** 下发阶段。 */
export type TodoStage =
  | 'Draft'
  | 'Ready'
  | 'Dispatched'
  | 'Running'
  | 'Blocked'
  | 'Review'
  | 'Done'
  | 'Cancelled'

export interface TodoItem {
  id: number
  taskKey: string
  title: string
  remark?: string | null
  status: TodoStatus
  stage: TodoStage
  stageLabel: string
  priority: number
  assignee: string
  objective: string
  content: string
  allowedScope: string
  forbiddenScope: string
  acceptance: string
  verification: string
  projectId: number
  projectRoot: string
  projectPathRaw: string
  projectName: string
  artifactRef: string
  dueDate?: string | null
  dispatchedAt?: string | null
  agentTaskKey: string
  agentId: number
  agentEngine: string
  permissionMode: string
  recordCount: number
  /** 服务端给出的可达下一阶段（界面只照它渲染按钮，不自己抄流转表）。 */
  allowedTargets: TodoStage[]
  createdAt: string
  updatedAt: string
  completedAt?: string | null
  missing: string[]
}

export interface PagedResult<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}

export interface TodoSaveRequest {
  title?: string
  remark?: string | null
  dueDate?: string | null
  objective?: string | null
  content?: string | null
  allowedScope?: string | null
  forbiddenScope?: string | null
  acceptance?: string | null
  verification?: string | null
  priority?: number
  assignee?: string | null
  projectPath?: string | null
  /** 新建时预选项目（宿主档案 id；后端 CreateTodoRequest.ProjectId）。 */
  projectId?: number
  stage?: string | null
}

export interface TodoProject {
  id: number
  root: string
  key: string
  name: string
  type: string
  description: string
  tags: string[]
  source: string
  pathExists: boolean
  isGitRepo: boolean
  taskCount: number
  openTaskCount: number
  lastActiveAt: string
}

export interface ResolveProjectResult {
  ok: boolean
  error?: string | null
  root: string
  key: string
  raw: string
  projectId: number
  projectName: string
  registered: boolean
  registryAvailable: boolean
}

export interface ChangedFile {
  path: string
  change: string
}

export interface TaskExecution {
  id: number
  todoId: number
  seq: number
  actor: string
  action: string
  detail: string
  result: string
  filesChanged: ChangedFile[]
  filesChangedRaw: string
  verification: string
  risks: string
  residuals: string
  evidence: string
  stageFrom: string
  stageTo: string
  elapsedMs: number
  blockReason: string
  nextStep: string
  createdAt: string
}

export interface ExecutionDraft {
  actor?: string
  action: string
  detail?: string
  result?: string
  filesChangedText?: string
  verification?: string
  risks?: string
  residuals?: string
  evidence?: string
  nextStep?: string
  blockReason?: string
  stageTo?: string
  elapsedMs?: number
}

export interface AgentOption {
  id: number
  name: string
  vendor: string
  defaultCwd?: string | null
}

export interface DispatchPreview {
  taskKey: string
  promptMarkdown: string
  payloadJson: string
  missing: string[]
  warnings: string[]
  canDispatch: boolean
  canDelegate: boolean
  delegationAvailable: boolean
  delegationError?: string | null
  agents: AgentOption[]
  builtInAvailable: boolean
  builtInError?: string | null
  builtInAgents: AgentOption[]
}

export interface DelegateResult {
  ok: boolean
  error?: string | null
  seamMissing: boolean
  taskKey: string
  agentId: number
  agentName: string
  status: string
  cwd?: string | null
  backfilled: boolean
  backfillWarning?: string | null
  steps: string[]
}

export interface AgentStatus {
  ok: boolean
  error?: string | null
  statusCode: number
  taskKey: string
  /** 执行 agent 名（后端按 AgentId 解析；未知为 null，界面兜底 agent#id）。 */
  agentName?: string | null
  status: string
  terminal: boolean
  exitCode?: number | null
  errorCode?: string | null
  elapsedMs: number
  resultSummary?: string | null
  filesChanged: ChangedFile[]
  cwd?: string | null
  verification: string
  notFound: boolean
}

export interface ArtifactFile {
  name: string
  size: number
  index: number
  isCore: boolean
}

export interface ArtifactSet {
  dir: string
  relativeDir: string
  files: ArtifactFile[]
  hasCoreFiles: boolean
}

export interface ImportResult {
  ok: boolean
  error?: string | null
  conflict: boolean
  artifactRef: string
  imported: string[]
  contentLength: number
  acceptanceExtracted: number
}

/** 阶段中文标签（与后端 TodoStage.ToLabel 同源口径，仅用于展示）。 */
export const StageLabels: Record<TodoStage, string> = {
  Draft: '草稿',
  Ready: '就绪',
  Dispatched: '已下发',
  Running: '执行中',
  Blocked: '阻塞',
  Review: '待验收',
  Done: '完成',
  Cancelled: '已取消'
}

/** 下发必填四栏的中文名（错误提示与界面缺项标记共用）。 */
export const RequiredFieldLabels: Record<string, string> = {
  objective: '可验证目标',
  content: '任务正文',
  acceptance: '验收判据',
  verification: '验证命令'
}
