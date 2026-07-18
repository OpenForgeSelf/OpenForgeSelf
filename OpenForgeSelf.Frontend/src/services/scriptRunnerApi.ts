import type {
  Script,
  ScriptExecution,
  ScriptListParams,
  ExecutionListParams,
  CreateScriptRequest,
  UpdateScriptRequest,
  ExecuteScriptRequest,
  ExecuteCodeRequest,
  RuntimeEnvironment,
  PaginatedResponse,
  ScriptTemplate,
  GenerateScriptRequest,
  GenerateScriptResponse,
  AnalyzeScriptErrorRequest,
  AnalyzeScriptErrorResponse,
  SuggestScriptFixRequest,
  SuggestScriptFixResponse,
  ScriptTemplateListParams
} from '@/types/scriptRunner'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api'

function parseScript(data: Record<string, unknown>): Script {
  return {
    ...data,
    createdAt: new Date(data.createdAt as string),
    updatedAt: new Date(data.updatedAt as string),
    lastExecutedAt: data.lastExecutedAt ? new Date(data.lastExecutedAt as string) : undefined
  } as Script
}

function parseExecution(data: Record<string, unknown>): ScriptExecution {
  return {
    ...data,
    startTime: data.startTime ? new Date(data.startTime as string) : undefined,
    endTime: data.endTime ? new Date(data.endTime as string) : undefined,
    logs: (data.logs as Array<Record<string, unknown>>)?.map(log => ({
      ...log,
      timestamp: new Date(log.timestamp as string)
    })) || []
  } as ScriptExecution
}

export const scriptRunnerApi = {
  async listScripts(params?: ScriptListParams): Promise<PaginatedResponse<Script>> {
    const queryParams = new URLSearchParams()
    if (params?.keyword) {
      queryParams.append('keyword', params.keyword)
    }
    if (params?.category) {
      queryParams.append('category', params.category)
    }
    if (params?.language) {
      queryParams.append('language', params.language)
    }
    if (params?.isFavorite !== undefined) {
      queryParams.append('isFavorite', String(params.isFavorite))
    }
    if (params?.page !== undefined) {
      queryParams.append('page', String(params.page))
    }
    if (params?.pageSize !== undefined) {
      queryParams.append('pageSize', String(params.pageSize))
    }

    const queryString = queryParams.toString()
    const url = `${API_BASE_URL}/scripts${queryString ? `?${queryString}` : ''}`

    const response = await fetch(url)

    if (!response.ok) {
      throw new Error(`获取脚本列表失败: ${response.status}`)
    }

    const data = await response.json()
    return {
      items: (data.items || data).map(parseScript),
      total: data.total ?? (data.items || data).length,
      page: data.page ?? 1,
      pageSize: data.pageSize ?? (data.items || data).length
    }
  },

  async getScript(id: string): Promise<Script> {
    const response = await fetch(`${API_BASE_URL}/scripts/${id}`)

    if (!response.ok) {
      throw new Error(`获取脚本详情失败: ${response.status}`)
    }

    const data = await response.json()
    return parseScript(data)
  },

  async createScript(script: CreateScriptRequest): Promise<Script> {
    const response = await fetch(`${API_BASE_URL}/scripts`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(script)
    })

    if (!response.ok) {
      throw new Error(`创建脚本失败: ${response.status}`)
    }

    const data = await response.json()
    return parseScript(data)
  },

  async updateScript(id: string, script: UpdateScriptRequest): Promise<Script> {
    const response = await fetch(`${API_BASE_URL}/scripts/${id}`, {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(script)
    })

    if (!response.ok) {
      throw new Error(`更新脚本失败: ${response.status}`)
    }

    const data = await response.json()
    return parseScript(data)
  },

  async deleteScript(id: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/scripts/${id}`, {
      method: 'DELETE'
    })

    if (!response.ok) {
      throw new Error(`删除脚本失败: ${response.status}`)
    }
  },

  async favoriteScript(id: string, isFavorite: boolean): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/scripts/${id}/favorite`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({ isFavorite })
    })

    if (!response.ok) {
      throw new Error(`收藏操作失败: ${response.status}`)
    }
  },

  async getCategories(): Promise<string[]> {
    const response = await fetch(`${API_BASE_URL}/scripts/categories`)

    if (!response.ok) {
      throw new Error(`获取分类列表失败: ${response.status}`)
    }

    return response.json()
  },

  async getTags(): Promise<string[]> {
    const response = await fetch(`${API_BASE_URL}/scripts/tags`)

    if (!response.ok) {
      throw new Error(`获取标签列表失败: ${response.status}`)
    }

    return response.json()
  },

  async getRuntimes(): Promise<RuntimeEnvironment[]> {
    const response = await fetch(`${API_BASE_URL}/scripts/runtimes`)

    if (!response.ok) {
      throw new Error(`获取运行环境失败: ${response.status}`)
    }

    return response.json()
  },

  async executeScript(id: string, request?: ExecuteScriptRequest): Promise<ScriptExecution> {
    const response = await fetch(`${API_BASE_URL}/scripts/${id}/execute`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(request || {})
    })

    if (!response.ok) {
      throw new Error(`执行脚本失败: ${response.status}`)
    }

    const data = await response.json()
    return parseExecution(data)
  },

  async executeCode(request: ExecuteCodeRequest): Promise<ScriptExecution> {
    const response = await fetch(`${API_BASE_URL}/scripts/execute-code`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(request)
    })

    if (!response.ok) {
      throw new Error(`执行代码失败: ${response.status}`)
    }

    const data = await response.json()
    return parseExecution(data)
  },

  async getExecution(id: string): Promise<ScriptExecution> {
    const response = await fetch(`${API_BASE_URL}/scripts/executions/${id}`)

    if (!response.ok) {
      throw new Error(`获取执行详情失败: ${response.status}`)
    }

    const data = await response.json()
    return parseExecution(data)
  },

  async cancelExecution(id: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/scripts/executions/${id}/cancel`, {
      method: 'POST'
    })

    if (!response.ok) {
      throw new Error(`取消执行失败: ${response.status}`)
    }
  },

  async listExecutions(params?: ExecutionListParams): Promise<PaginatedResponse<ScriptExecution>> {
    const queryParams = new URLSearchParams()
    if (params?.scriptId) {
      queryParams.append('scriptId', params.scriptId)
    }
    if (params?.page !== undefined) {
      queryParams.append('page', String(params.page))
    }
    if (params?.pageSize !== undefined) {
      queryParams.append('pageSize', String(params.pageSize))
    }

    const queryString = queryParams.toString()
    const url = `${API_BASE_URL}/scripts/executions${queryString ? `?${queryString}` : ''}`

    const response = await fetch(url)

    if (!response.ok) {
      throw new Error(`获取执行记录失败: ${response.status}`)
    }

    const data = await response.json()
    return {
      items: (data.items || data).map(parseExecution),
      total: data.total ?? (data.items || data).length,
      page: data.page ?? 1,
      pageSize: data.pageSize ?? (data.items || data).length
    }
  },

  async generateScript(request: GenerateScriptRequest): Promise<GenerateScriptResponse> {
    const response = await fetch(`${API_BASE_URL}/ai-agent/script/generate`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(request)
    })

    if (!response.ok) {
      throw new Error(`生成脚本失败: ${response.status}`)
    }

    return response.json()
  },

  async analyzeScriptError(request: AnalyzeScriptErrorRequest): Promise<AnalyzeScriptErrorResponse> {
    const response = await fetch(`${API_BASE_URL}/ai-agent/script/analyze-error`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(request)
    })

    if (!response.ok) {
      throw new Error(`分析错误失败: ${response.status}`)
    }

    return response.json()
  },

  async suggestScriptFix(request: SuggestScriptFixRequest): Promise<SuggestScriptFixResponse> {
    const response = await fetch(`${API_BASE_URL}/ai-agent/script/suggest-fix`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(request)
    })

    if (!response.ok) {
      throw new Error(`获取修复建议失败: ${response.status}`)
    }

    return response.json()
  },

  async getScriptTemplates(params?: ScriptTemplateListParams): Promise<ScriptTemplate[]> {
    const queryParams = new URLSearchParams()
    if (params?.category) {
      queryParams.append('category', params.category)
    }
    if (params?.keyword) {
      queryParams.append('keyword', params.keyword)
    }
    if (params?.language) {
      queryParams.append('language', params.language)
    }

    const queryString = queryParams.toString()
    const url = `${API_BASE_URL}/ai-agent/script/templates${queryString ? `?${queryString}` : ''}`

    const response = await fetch(url)

    if (!response.ok) {
      throw new Error(`获取脚本模板失败: ${response.status}`)
    }

    return response.json()
  }
}
