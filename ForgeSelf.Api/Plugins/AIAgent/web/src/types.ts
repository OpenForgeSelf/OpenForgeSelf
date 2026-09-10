/**
 * 插件界面共享类型。
 *
 * 字段与后端 DTO 对应（后端为 PascalCase，JSON 序列化后按 ASP.NET Core 默认策略为 camelCase）。
 * 只声明界面真正用到的字段，不追求与后端 DTO 完全一一对应。
 */

/** 可用模型（来自 GET /api/ai-models）。 */
export interface AIModel {
  id?: number
  alias?: string
  upstreamModelId?: string
  chatModelId?: string
  providerId?: number
  providerName?: string
}

/** 一次工具调用的实时状态（流式期间逐步填充：先 tool_call，后 tool_result）。 */
export interface ToolEvent {
  /** 工具名。 */
  name?: string
  /** 调用参数 JSON。 */
  args?: string
  /** 工具返回结果。 */
  result?: string
  /** 执行是否成功（result 到达后填充）。 */
  success?: boolean
  /** 是否仍在执行中（result 未到达）。 */
  pending?: boolean
}

/** token 用量（对应后端 UnifiedUsage）。 */
export interface ChatUsage {
  promptTokens?: number
  completionTokens?: number
  totalTokens?: number
}

/** 聊天消息（对应后端 ChatResponse）。 */
export interface ChatMessage {
  id: number | string
  /** 消息角色：user / assistant。 */
  role: string
  /** 消息正文。 */
  content: string
  /** 创建时间（后端返回 DateTime 字符串）。 */
  createTime?: string
  /** 该条回复触发的工具调用名（非流式返回时携带）。 */
  toolCalls?: string[]
  /** 该条回复触发的工具调用明细（流式实时收集，含参数/结果）。 */
  toolEvents?: ToolEvent[]
  /** token 用量。 */
  usage?: ChatUsage
}

/** MCP 服务器（来自 GET /api/mcp/servers）。 */
export interface McpServer {
  id?: string
  name?: string
  enabled?: boolean
}

/** MCP 工具（来自 GET /api/mcp/servers/{id}/tools）。 */
export interface McpTool {
  name?: string
  description?: string
  /** 所属服务器名，由前端在平铺时补上。 */
  serverName?: string
}

/** AI Agent 工具（来自 GET /api/ai-agent/chat/tools，宿主 IToolRegistry；composer 🔧 按 pluginId 白名单过滤后展示）。 */
export interface AgentTool {
  id?: string
  name?: string
  description?: string
  pluginId?: string
}

/** 技能（来自 GET /api/skills）。 */
export interface SkillItem {
  id?: string
  name?: string
  description?: string
  enabled?: boolean
}

/** 项目自动识别的技能（来自 GET /api/project/skills，后端 ProjectSkillItem）。 */
export interface ProjectSkillItem {
  id?: string
  name?: string
  description?: string
  /** 来源分类：agents（.agents/skills） / commands（.codebuddy/commands）。 */
  source?: string
  /** 相对项目根的 SKILL.md / .md 路径。 */
  path?: string
  isDirectory?: boolean
}

/** Agent 人格画像（来自 GET /api/agents 的 AgentDefinition.Personality；可编辑）。 */
export interface AgentPersonality {
  /** 人格名称。 */
  name?: string
  /** 人格描述。 */
  description?: string
  /** 人格头像 emoji。 */
  avatar?: string
  /** 创造力（0~1）。 */
  creativity?: number
  /** 分析力（0~1）。 */
  analytical?: number
  /** 同理心（0~1）。 */
  empathy?: number
  /** 自信度（0~1）。 */
  confidence?: number
  /** 正式度（0~1）。 */
  formality?: number
  /** 语气风格。 */
  toneStyle?: string
  /** 沟通风格。 */
  communicationStyle?: string
  /** 擅长领域。 */
  strengths?: string[]
  /** 局限性。 */
  limitations?: string[]
  /** 追加到系统提示词的内容。 */
  systemPromptAddon?: string
}

/** 工作流定义（来自 GET /api/workflows。PagedResult<WorkflowDefinitionDto> 的 items 项）。 */
export interface WorkflowItem {
  id?: number
  name?: string
  description?: string
  category?: string
}

/** Agent 关联的工作流引用（冗余名称/描述；后端随 ConfigJson 持久化）。 */
export interface AgentWorkflowRef {
  workflowId: number
  name?: string
  description?: string
}

/** Agent 定义（来自 GET /api/agents，后端 AgentDefinition）。 */
export interface AgentDefinition {
  id?: string
  name?: string
  description?: string
  /** Agent 类型枚举值（0=Coordinator, 1=Researcher, 2=Writer, 3=Programmer, 4=Analyst, 5=Critic, 99=Generalist）。 */
  type?: number
  /* avatar 为 emoji 字符，非图标名。 */
  avatar?: string
  personality?: AgentPersonality
  capabilities?: string[]
  tools?: string[]
  /** 关联的工作流（执行时注入 system prompt，由 LLM 按需 execute_workflow）。 */
  workflows?: AgentWorkflowRef[]
  /** 系统提示词（可编辑，Stage 4）。 */
  systemPrompt?: string
  maxIterations?: number
  sortOrder?: number
  /** 执行模式：free（自由循环，默认）/ plan（计划驱动）。随 ConfigJson 持久化（029）。 */
  executionMode?: 'free' | 'plan'
  isEnabled?: boolean
}

/* ------------------------------------------------------------------ */
/* 计划驱动执行（029）：Plan DSL / Run / StepRun / SSE 事件               */
/* ------------------------------------------------------------------ */

/** 执行计划（Plan DSL，存 AgentRun.PlanJson）。 */
export interface AgentPlan {
  /** 整体目标一句话。 */
  goal?: string
  /** 步骤列表（顺序执行）。 */
  steps: AgentPlanStep[]
}

/** 执行计划中的单个步骤。 */
export interface AgentPlanStep {
  /** 步骤 id（如 s1）。 */
  id?: string
  /** 步骤名。 */
  name?: string
  /** 步骤目标（给 LLM 的执行指令）。 */
  objective?: string
  /** 期望产出物（LLM 完成声明时对照；V1 仅记录）。 */
  expectedOutput?: string
  /** 本步允许的工具白名单；空 = 使用 Agent 全部工具。 */
  allowedTools?: string[]
  /** 是否必须步骤（V1 仅记录不启用强校验）。 */
  mandatory?: boolean
}

/** 执行实例状态（对应后端 AgentRunStatus，UI 侧小写字符串语义）。 */
export type AgentRunStatus =
  | 'pending'
  | 'planning'
  | 'running'
  | 'completed'
  | 'stuck'
  | 'failed'
  | 'cancelled'

/** 执行步骤状态（对应后端 AgentStepStatus，UI 侧小写字符串语义）。 */
export type AgentStepStatus = 'pending' | 'running' | 'completed' | 'stuck' | 'skipped' | 'failed'

/**
 * 后端枚举 int → UI 小写状态名。
 * 后端 DTO 的 status 以 System.Text.Json 默认序列化为 **int**（枚举序号，见 AgentRunStatus/AgentStepStatus），
 * 而 UI 侧统一用小写字符串语义（badge/图标/比较）。契约约定：DTO 透传 int，前端在此映射为名字。
 * - AgentRunStatus:  0=pending 1=planning 2=running 3=completed 4=stuck 5=failed 6=cancelled
 * - AgentStepStatus: 0=pending 1=running 2=completed 3=stuck 4=skipped 5=failed
 * 越界/未知一律兜底为 'pending'，避免渲染破版。
 */
export const RUN_STATUS_NAMES: readonly AgentRunStatus[] = [
  'pending',
  'planning',
  'running',
  'completed',
  'stuck',
  'failed',
  'cancelled',
]

export const STEP_STATUS_NAMES: readonly AgentStepStatus[] = [
  'pending',
  'running',
  'completed',
  'stuck',
  'skipped',
  'failed',
]

/** int → AgentRunStatus 名字。 */
export function runStatusName(code: number): AgentRunStatus {
  return RUN_STATUS_NAMES[code] ?? 'pending'
}

/** int → AgentStepStatus 名字。 */
export function stepStatusName(code: number): AgentStepStatus {
  return STEP_STATUS_NAMES[code] ?? 'pending'
}

/** 执行实例 DTO（列表/详情返回）。status 为后端枚举 int。 */
export interface AgentRunDto {
  id: number
  agentId?: string
  agentName?: string
  sessionId?: string
  workflowId?: number
  workflowName?: string
  taskInput?: string
  planJson?: string
  status: number
  currentStepIndex?: number
  stuckReason?: string
  stepCount?: number
  totalTokens?: number
  createTime?: string
  updateTime?: string
}

/** 执行步骤 DTO（Run 详情返回）。status 为后端枚举 int。 */
export interface AgentStepRunDto {
  id: number
  runId: number
  stepIndex: number
  stepId?: string
  name?: string
  objective?: string
  status: number
  inputJson?: string
  outputJson?: string
  toolCallsJson?: string
  errorMessage?: string
  stuckReason?: string
  humanNote?: string
  humanOverride?: string
  retryCount?: number
  tokensUsed?: number
  durationMs?: number
  startedAt?: string
  completedAt?: string
}

/** Run 列表分页响应。 */
export interface AgentRunListResponse {
  success?: boolean
  total?: number
  page?: number
  pageSize?: number
  items: AgentRunDto[]
}

/** Run 详情响应（Run + 步骤列表）。 */
export interface AgentRunDetailResponse {
  run?: AgentRunDto
  steps: AgentStepRunDto[]
}

/** 创建计划驱动执行的请求体（POST /api/ai-agent/runs）。 */
export interface RunRequest {
  /** 关联聊天会话 id。 */
  sessionId?: string
  /** 所选 Agent id。 */
  agentId: string
  /** 任务原文（必填）。 */
  taskInput: string
  /** 关联工作流 id；大于 0 时从工作流定义生成 Plan 草稿。 */
  workflowId?: number
  /** 聊天模型 id。 */
  chatModelId?: string
}

/** 人工介入请求体（PATCH /api/ai-agent/runs/{id}/steps/{index}）。 */
export interface InterveneRequest {
  /** 介入动作：skip 跳过 / override 补位。 */
  action: 'skip' | 'override'
  /** override 时的人工补位产出。 */
  output?: string
  /** 人工批注。 */
  note?: string
}

/** step_started SSE 载荷（payload 字段内容）。 */
export interface StepStartedPayload {
  runId: number
  stepIndex: number
  stepId?: string
  name?: string
  objective?: string
}

/** step_completed SSE 载荷。 */
export interface StepCompletedPayload {
  runId: number
  stepIndex: number
  output?: string
}

/** run_stuck SSE 载荷。 */
export interface RunStuckPayload {
  runId: number
  stepIndex: number
  reason?: string
}

/** plan_created SSE 载荷（payload 字段内容：{ plan, planJson }）。 */
export interface PlanCreatedPayload {
  plan: AgentPlan
  planJson?: string
}

/** 中栏步骤进度卡的步骤视图状态（由 AiAgentView 依据 SSE 事件增量维护）。 */
export interface PlanStepView {
  /** 步骤序号（0 起，对应 StepRun.StepIndex）。 */
  index: number
  /** 步骤 id（如 s1，来自 Plan DSL）。 */
  id?: string
  name?: string
  objective?: string
  status: AgentStepStatus
  /** 步骤产出（step_completed 的 output 或步骤内 content 累积）。 */
  output?: string
  /** 卡住原因（run_stuck / 详情返回）。 */
  stuckReason?: string
}

/** 中栏步骤进度卡整体运行状态（AiAgentView 依据 SSE 事件维护；ChatPanel 只读展示）。 */
export interface PlanRunCard {
  runId: number
  plan: AgentPlan | null
  status: AgentRunStatus
  steps: PlanStepView[]
  stuckReason?: string
  error?: string
}

/** 项目目录/文件项（来自 GET /api/project/files，后端 ProjectFileEntry）。 */
export interface ProjectEntry {
  name?: string
  isDirectory?: boolean
  relativePath?: string
  size?: number
  updatedAt?: string
}

/** 正在编辑的项目文件（内容由 GET /api/project/file 读取后填充）。 */
export interface EditingFile {
  /** 相对项目根的文件路径。 */
  path: string
  /** 文件名（展示用）。 */
  name: string
  /** 文件内容。 */
  content: string
}

/** 长期记忆条目（来自 GET /api/ai-agent/chat/memories）。 */
export interface MemoryItem {
  id?: number
  title?: string
  content?: string
  /** fact / preference / project / personal / workflow / skill / other。 */
  type?: string
  /** low / medium / high / critical。 */
  importance?: string
  tags?: string[]
  categoryName?: string
  createdAt?: string
  lastAccessedAt?: string
}
