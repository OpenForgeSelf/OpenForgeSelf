export type ScriptLanguage = 'powershell' | 'python' | 'nodejs' | 'shell' | 'cmd'

export type ScriptParameterType = 'string' | 'number' | 'boolean' | 'select' | 'filePath' | 'directoryPath'

export type ScriptExecutionStatus = 'pending' | 'running' | 'completed' | 'failed' | 'cancelled' | 'timeout'

export interface ScriptParameter {
  id: string
  name: string
  type: ScriptParameterType
  description?: string
  defaultValue?: unknown
  required?: boolean
  options?: string[]
}

export interface Script {
  id: string
  name: string
  description?: string
  language: ScriptLanguage
  code: string
  category?: string
  tags: string[]
  parameters: ScriptParameter[]
  timeout: number
  isFavorite: boolean
  usageCount: number
  createdAt: Date
  updatedAt: Date
  lastExecutedAt?: Date
  createdBy?: string
}

export interface ScriptExecutionLog {
  id: string
  timestamp: Date
  stream: 'stdout' | 'stderr'
  message: string
}

export interface ScriptExecution {
  id: string
  scriptId: string
  scriptName: string
  language: ScriptLanguage
  status: ScriptExecutionStatus
  startTime?: Date
  endTime?: Date
  duration?: number
  exitCode?: number
  parameters?: Record<string, unknown>
  output: string
  logs: ScriptExecutionLog[]
  error?: string
}

export interface RuntimeEnvironment {
  language: ScriptLanguage
  name: string
  version?: string
  available: boolean
  path?: string
}

export interface ScriptListParams {
  keyword?: string
  category?: string
  language?: ScriptLanguage
  isFavorite?: boolean
  page?: number
  pageSize?: number
}

export interface ExecutionListParams {
  scriptId?: string
  page?: number
  pageSize?: number
}

export interface CreateScriptRequest {
  name: string
  description?: string
  language: ScriptLanguage
  code: string
  category?: string
  tags?: string[]
  parameters?: ScriptParameter[]
  timeout?: number
}

export interface UpdateScriptRequest {
  name?: string
  description?: string
  language?: ScriptLanguage
  code?: string
  category?: string
  tags?: string[]
  parameters?: ScriptParameter[]
  timeout?: number
}

export interface ExecuteScriptRequest {
  parameters?: Record<string, unknown>
}

export interface ExecuteCodeRequest {
  code: string
  language: ScriptLanguage
  parameters?: Record<string, unknown>
  timeout?: number
}

export interface ScriptTemplate {
  id: string
  name: string
  description: string
  language: ScriptLanguage
  category: string
  code: string
  parameters: ScriptParameter[]
  tags: string[]
  version: string
  author?: string
  createdAt?: Date
}

export interface GenerateScriptRequest {
  language: ScriptLanguage
  description: string
  requirements?: string
}

export interface GenerateScriptResponse {
  code: string
  description: string
  language: ScriptLanguage
  parameters: ScriptParameter[]
}

export interface AnalyzeScriptErrorRequest {
  language: ScriptLanguage
  code: string
  errorMessage: string
}

export interface AnalyzeScriptErrorResponse {
  errorAnalysis: string
  possibleCauses: string[]
  suggestion: string
}

export interface SuggestScriptFixRequest {
  language: ScriptLanguage
  code: string
  errorMessage: string
}

export interface SuggestScriptFixResponse {
  errorAnalysis: string
  fixedCode: string
  changes: string[]
  explanation: string
}

export interface ScriptTemplateListParams {
  category?: string
  keyword?: string
  language?: ScriptLanguage
}

export interface PaginatedResponse<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}
