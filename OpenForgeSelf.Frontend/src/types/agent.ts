export enum AgentType {
  Coordinator = 'Coordinator',
  Researcher = 'Researcher',
  Writer = 'Writer',
  Programmer = 'Programmer',
  Analyst = 'Analyst',
  Critic = 'Critic',
  Generalist = 'Generalist'
}

export enum AgentStatus {
  Idle = 'Idle',
  Thinking = 'Thinking',
  Working = 'Working',
  Waiting = 'Waiting',
  Completed = 'Completed',
  Failed = 'Failed'
}

export enum TaskPriority {
  Low = 0,
  Medium = 1,
  High = 2,
  Critical = 3
}

export enum AgentTaskStatus {
  Pending = 0,
  InProgress = 1,
  Completed = 2,
  Failed = 3,
  Cancelled = 4
}

export interface AgentPersonality {
  name: string
  creativity: number
  analytical: number
  empathy: number
  confidence: number
  formality: number
  toneStyle: string
  communicationStyle: string
  strengths: string[]
  limitations: string[]
  systemPromptAddon: string
}

export interface AgentDefinition {
  id: string
  name: string
  type: AgentType
  description: string
  version: string
  author: string
  icon: string
  capabilities: string[]
  tools: string[]
  personality: AgentPersonality
  systemPrompt: string
  isBuiltin: boolean
  isEnabled: boolean
  createdAt: string
  updatedAt: string
}

export interface AgentInstance {
  instanceId: string
  agentId: string
  agentName: string
  agentType: AgentType
  status: AgentStatus
  sessionId: string
  currentTaskId: string
  createdAt: string
  lastActiveAt: string
  context: AIChatMessage[]
}

export interface AIChatMessage {
  role: string
  content: string
}

export interface AgentTask {
  taskId: string
  description: string
  assignedAgentType: AgentType
  assignedAgentId: string | null
  assignedAgentInstanceId: string | null
  priority: TaskPriority
  status: AgentTaskStatus
  input: string
  output: string | null
  errorMessage: string | null
  parentTaskId: string | null
  subTaskIds: string[]
  createdAt: string
  startedAt: string | null
  completedAt: string | null
}

export interface CoordinatorPlan {
  planId: string
  originalRequest: string
  strategy: string
  requiredCapabilities: string[]
  tasks: AgentTask[]
  coordinatorReasoning: string
}

export interface AgentExecutionResult {
  success: boolean
  agentId: string
  agentName: string
  taskId: string
  output: string | null
  errorMessage: string | null
  iterations: number
  toolsUsed: string[]
  duration: string
}

export interface FindAgentRequest {
  capability: string
}

export interface BestMatchRequest {
  taskDescription: string
  requiredCapabilities: string[]
}

export interface CoordinateRequest {
  userRequest: string
}
