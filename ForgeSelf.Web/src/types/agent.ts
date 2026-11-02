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

export interface FindAgentRequest {
  capability: string
}

export interface BestMatchRequest {
  taskDescription: string
  requiredCapabilities: string[]
}

// B5（2026-11-02-b5-legacy-cleanup ③，agenthub-adjudication.md）：
// 多智能体启发式编排死代码已删——AgentInstance/AgentTask/AgentTaskStatus/CoordinatorPlan/
// AgentExecutionResult/CoordinateRequest 随后端 coordinate/execute/handle/instances/execute-task
// 端点与 agentApi 对应方法一并移除；能力归计划任务主路径（029 统一循环）。
