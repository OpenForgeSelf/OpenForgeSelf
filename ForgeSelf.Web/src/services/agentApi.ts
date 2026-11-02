import type {
  AgentDefinition,
  AgentType
} from '../types/agent'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api'

interface ApiResponse<T = unknown> {
  data?: T
  message?: string
}

function unwrap<T>(response: Response): Promise<T> {
  return response.json().then((data: ApiResponse<T>) => {
    if (!response.ok) {
      throw new Error(data.message || `请求失败: ${response.status}`)
    }
    return (data.data ?? data) as T
  })
}

export const agentApi = {
  getAllAgents: async (): Promise<AgentDefinition[]> => {
    const response = await fetch(`${API_BASE_URL}/agents`)
    return unwrap<AgentDefinition[]>(response)
  },

  getAgent: async (agentId: string): Promise<AgentDefinition> => {
    const response = await fetch(`${API_BASE_URL}/agents/${agentId}`)
    return unwrap<AgentDefinition>(response)
  },

  getAgentsByType: async (type: AgentType): Promise<AgentDefinition[]> => {
    const response = await fetch(`${API_BASE_URL}/agents/type/${type}`)
    return unwrap<AgentDefinition[]>(response)
  },

  findAgentsByCapability: async (capability: string): Promise<AgentDefinition[]> => {
    const response = await fetch(`${API_BASE_URL}/agents/find`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ capability })
    })
    return unwrap<AgentDefinition[]>(response)
  },

  getBestAgentForTask: async (taskDescription: string, requiredCapabilities: string[]): Promise<AgentDefinition> => {
    const response = await fetch(`${API_BASE_URL}/agents/best-match`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ taskDescription, requiredCapabilities })
    })
    return unwrap<AgentDefinition>(response)
  }
}

// B5（2026-11-02-b5-legacy-cleanup ③，agenthub-adjudication.md）：
// 多智能体启发式编排死代码已删——createPlan/executePlan/handleRequest/getActiveInstances/
// getInstance/executeTask 六个方法连同后端 AgentsController 的 coordinate/execute/handle/
// instances/instances/{id}/execute-task 端点一并移除；能力归计划任务主路径（029 统一循环）。
