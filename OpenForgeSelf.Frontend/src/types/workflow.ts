/**
 * 工作流类型定义
 */

export type WorkflowStepType = 'tool_call' | 'condition' | 'loop' | 'parallel' | 'wait' | 'http' | 'script'

export type ScriptLanguage = 'powershell' | 'python' | 'nodejs' | 'shell' | 'cmd'

export type WorkflowStatus = 'draft' | 'ready' | 'running' | 'paused' | 'completed' | 'failed' | 'cancelled'

export interface WorkflowVariable {
  id: string
  name: string
  type: 'string' | 'number' | 'boolean' | 'object' | 'array'
  defaultValue?: unknown
  description?: string
  required?: boolean
}

export interface WorkflowStep {
  id: string
  name: string
  type: WorkflowStepType
  description?: string
  config: Record<string, unknown>
  position?: number
  timeout?: number
  retryCount?: number
  retryDelay?: number
  continueOnError?: boolean

  scriptId?: string
  scriptCode?: string
  scriptLanguage?: ScriptLanguage
  parameterMappings?: Record<string, string>
  outputVariable?: string
  workingDirectory?: string
  timeoutSeconds?: number
  successExitCodes?: number[]
}

export interface WorkflowDefinition {
  id: string
  name: string
  description?: string
  category?: string
  icon?: string
  steps: WorkflowStep[]
  variables: WorkflowVariable[]
  isFavorite: boolean
  usageCount: number
  status: WorkflowStatus
  createdAt: Date
  updatedAt: Date
  lastExecutedAt?: Date
  createdBy?: string
}

export interface ExecutionLogEntry {
  id: string
  timestamp: Date
  level: 'info' | 'warn' | 'error' | 'debug'
  stepId?: string
  stepName?: string
  message: string
  data?: Record<string, unknown>
}

export interface WorkflowExecution {
  id: string
  workflowId: string
  workflowName: string
  status: WorkflowStatus
  startTime?: Date
  endTime?: Date
  currentStepId?: string
  steps: Array<{
    stepId: string
    stepName: string
    status: WorkflowStatus
    startTime?: Date
    endTime?: Date
    result?: unknown
    error?: string
  }>
  logs: ExecutionLogEntry[]
  inputVariables?: Record<string, unknown>
  outputVariables?: Record<string, unknown>
  error?: string
}

export interface WorkflowListParams {
  keyword?: string
  category?: string
  page?: number
  pageSize?: number
}

export interface ExecutionListParams {
  workflowId?: string
  page?: number
  pageSize?: number
}

export interface PlanWorkflowRequest {
  description: string
  inputVariables?: WorkflowVariable[]
}

export interface PlanWorkflowResponse {
  workflow: WorkflowDefinition
  confidence: number
  suggestedName: string
}

export interface WorkflowTemplate {
  id: string
  name: string
  description: string
  category: string
  icon: string
  stepsCount: number
}
