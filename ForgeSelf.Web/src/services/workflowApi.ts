/**
 * 工作流API服务层 - 封装工作流管理相关后端通信
 */

import type {
  WorkflowDefinition,
  WorkflowExecution,
  WorkflowListParams,
  ExecutionListParams,
  PlanWorkflowRequest,
  PlanWorkflowResponse,
  WorkflowTemplate
} from '@/types/workflow'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api'

function parseWorkflow(data: Record<string, unknown>): WorkflowDefinition {
  return {
    ...data,
    createdAt: new Date(data.createdAt as string),
    updatedAt: new Date(data.updatedAt as string),
    lastExecutedAt: data.lastExecutedAt ? new Date(data.lastExecutedAt as string) : undefined
  } as WorkflowDefinition
}

function parseExecution(data: Record<string, unknown>): WorkflowExecution {
  return {
    ...data,
    startTime: data.startTime ? new Date(data.startTime as string) : undefined,
    endTime: data.endTime ? new Date(data.endTime as string) : undefined,
    steps: (data.steps as Array<Record<string, unknown>>)?.map(step => ({
      ...step,
      startTime: step.startTime ? new Date(step.startTime as string) : undefined,
      endTime: step.endTime ? new Date(step.endTime as string) : undefined
    })) || [],
    logs: (data.logs as Array<Record<string, unknown>>)?.map(log => ({
      ...log,
      timestamp: new Date(log.timestamp as string)
    })) || []
  } as WorkflowExecution
}

export const workflowApi = {
  /**
   * 获取工作流列表
   */
  async listWorkflows(params?: WorkflowListParams): Promise<{ items: WorkflowDefinition[]; total: number }> {
    const queryParams = new URLSearchParams()
    if (params?.keyword) {
      queryParams.append('keyword', params.keyword)
    }
    if (params?.category) {
      queryParams.append('category', params.category)
    }
    if (params?.page !== undefined) {
      queryParams.append('page', String(params.page))
    }
    if (params?.pageSize !== undefined) {
      queryParams.append('pageSize', String(params.pageSize))
    }

    const queryString = queryParams.toString()
    const url = `${API_BASE_URL}/workflows${queryString ? `?${queryString}` : ''}`

    const response = await fetch(url)

    if (!response.ok) {
      throw new Error(`获取工作流列表失败: ${response.status}`)
    }

    const data = await response.json()
    // 后端返回 ApiResponse 包装：{ data: { items, total, page, pageSize } }
    const payload = (data.data ?? data) as {
      items?: unknown[]
      total?: number
      page?: number
      pageSize?: number
    }
    return {
      items: (payload.items ?? []).map((item) => parseWorkflow(item as Record<string, unknown>)),
      total: payload.total ?? 0
    }
  },

  /**
   * 获取工作流详情
   */
  async getWorkflow(id: string): Promise<WorkflowDefinition> {
    const response = await fetch(`${API_BASE_URL}/workflows/${id}`)

    if (!response.ok) {
      throw new Error(`获取工作流详情失败: ${response.status}`)
    }

    const data = await response.json()
    return parseWorkflow(data)
  },

  /**
   * 创建工作流
   */
  async createWorkflow(definition: Omit<WorkflowDefinition, 'id' | 'createdAt' | 'updatedAt' | 'usageCount'>): Promise<WorkflowDefinition> {
    const response = await fetch(`${API_BASE_URL}/workflows`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(definition)
    })

    if (!response.ok) {
      throw new Error(`创建工作流失败: ${response.status}`)
    }

    const data = await response.json()
    return parseWorkflow(data)
  },

  /**
   * 更新工作流
   */
  async updateWorkflow(id: string, definition: Partial<WorkflowDefinition>): Promise<WorkflowDefinition> {
    const response = await fetch(`${API_BASE_URL}/workflows/${id}`, {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(definition)
    })

    if (!response.ok) {
      throw new Error(`更新工作流失败: ${response.status}`)
    }

    const data = await response.json()
    return parseWorkflow(data)
  },

  /**
   * 删除工作流
   */
  async deleteWorkflow(id: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/workflows/${id}`, {
      method: 'DELETE'
    })

    if (!response.ok) {
      throw new Error(`删除工作流失败: ${response.status}`)
    }
  },

  /**
   * 收藏/取消收藏工作流
   */
  async favoriteWorkflow(id: string, isFavorite: boolean): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/workflows/${id}/favorite`, {
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

  /**
   * 执行工作流
   */
  async executeWorkflow(id: string, inputVariables?: Record<string, unknown>): Promise<WorkflowExecution> {
    const response = await fetch(`${API_BASE_URL}/workflows/${id}/execute`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({ inputVariables })
    })

    if (!response.ok) {
      throw new Error(`执行工作流失败: ${response.status}`)
    }

    const data = await response.json()
    return parseExecution(data)
  },

  /**
   * 获取执行记录列表
   */
  async listExecutions(params?: ExecutionListParams): Promise<{ items: WorkflowExecution[]; total: number }> {
    const queryParams = new URLSearchParams()
    if (params?.workflowId) {
      queryParams.append('workflowId', params.workflowId)
    }
    if (params?.page !== undefined) {
      queryParams.append('page', String(params.page))
    }
    if (params?.pageSize !== undefined) {
      queryParams.append('pageSize', String(params.pageSize))
    }

    const queryString = queryParams.toString()
    const url = `${API_BASE_URL}/workflows/executions${queryString ? `?${queryString}` : ''}`

    const response = await fetch(url)

    if (!response.ok) {
      throw new Error(`获取执行记录失败: ${response.status}`)
    }

    const data = await response.json()
    return {
      items: (data.items || data).map(parseExecution),
      total: data.total ?? (data.items || data).length
    }
  },

  /**
   * 获取执行详情
   */
  async getExecutionDetail(executionId: string): Promise<WorkflowExecution> {
    const response = await fetch(`${API_BASE_URL}/workflows/executions/${executionId}`)

    if (!response.ok) {
      throw new Error(`获取执行详情失败: ${response.status}`)
    }

    const data = await response.json()
    return parseExecution(data)
  },

  /**
   * 暂停执行
   */
  async pauseExecution(executionId: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/workflows/executions/${executionId}/pause`, {
      method: 'POST'
    })

    if (!response.ok) {
      throw new Error(`暂停执行失败: ${response.status}`)
    }
  },

  /**
   * 继续执行
   */
  async resumeExecution(executionId: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/workflows/executions/${executionId}/resume`, {
      method: 'POST'
    })

    if (!response.ok) {
      throw new Error(`继续执行失败: ${response.status}`)
    }
  },

  /**
   * 取消执行
   */
  async cancelExecution(executionId: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/workflows/executions/${executionId}/cancel`, {
      method: 'POST'
    })

    if (!response.ok) {
      throw new Error(`取消执行失败: ${response.status}`)
    }
  },

  /**
   * AI规划工作流
   */
  async planWorkflow(request: PlanWorkflowRequest): Promise<PlanWorkflowResponse> {
    const response = await fetch(`${API_BASE_URL}/workflows/plan`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(request)
    })

    if (!response.ok) {
      throw new Error(`AI规划工作流失败: ${response.status}`)
    }

    const data = await response.json()
    return {
      ...data,
      workflow: parseWorkflow(data.workflow)
    }
  },

  /**
   * 获取推荐模板
   */
  async getWorkflowTemplates(): Promise<WorkflowTemplate[]> {
    const response = await fetch(`${API_BASE_URL}/workflows/templates`)

    if (!response.ok) {
      throw new Error(`获取工作流模板失败: ${response.status}`)
    }

    return response.json()
  }
}
