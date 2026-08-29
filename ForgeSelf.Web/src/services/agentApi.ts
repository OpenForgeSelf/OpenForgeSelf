import type {
  AgentDefinition,
  AgentInstance,
  CoordinatorPlan,
  AgentExecutionResult,
  AgentTask,
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
  },

  createPlan: async (userRequest: string): Promise<CoordinatorPlan> => {
    const response = await fetch(`${API_BASE_URL}/agents/coordinate`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ userRequest })
    })
    return unwrap<CoordinatorPlan>(response)
  },

  executePlan: async (plan: CoordinatorPlan): Promise<AgentExecutionResult> => {
    const response = await fetch(`${API_BASE_URL}/agents/execute`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(plan)
    })
    return unwrap<AgentExecutionResult>(response)
  },

  handleRequest: async (userRequest: string): Promise<string> => {
    const response = await fetch(`${API_BASE_URL}/agents/handle`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ userRequest })
    })
    return unwrap<string>(response)
  },

  getActiveInstances: async (): Promise<AgentInstance[]> => {
    const response = await fetch(`${API_BASE_URL}/agents/instances`)
    return unwrap<AgentInstance[]>(response)
  },

  getInstance: async (instanceId: string): Promise<AgentInstance> => {
    const response = await fetch(`${API_BASE_URL}/agents/instances/${instanceId}`)
    return unwrap<AgentInstance>(response)
  },

  executeTask: async (task: AgentTask): Promise<AgentExecutionResult> => {
    const response = await fetch(`${API_BASE_URL}/agents/execute-task`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(task)
    })
    return unwrap<AgentExecutionResult>(response)
  }
}
