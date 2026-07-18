import { ref, computed } from 'vue'
import { defineStore } from 'pinia'
import {
  AgentType,
  AgentStatus,
  TaskPriority,
  type AgentDefinition,
  type AgentInstance,
  type CoordinatorPlan,
  type AgentExecutionResult
} from '@/types/agent'
import { agentApi } from '@/services/agentApi'

export const useAgentStore = defineStore('agent', () => {
  const agents = ref<AgentDefinition[]>([])
  const instances = ref<AgentInstance[]>([])
  const currentPlan = ref<CoordinatorPlan | null>(null)
  const executionResult = ref<AgentExecutionResult | null>(null)
  const loading = ref(false)
  const executing = ref(false)
  const error = ref<string | null>(null)
  const filterType = ref<AgentType | null>(null)
  const searchKeyword = ref('')

  const filteredAgents = computed(() => {
    let result = agents.value
    if (filterType.value) {
      result = result.filter(a => a.type === filterType.value)
    }
    if (searchKeyword.value) {
      const keyword = searchKeyword.value.toLowerCase()
      result = result.filter(a =>
        a.name.toLowerCase().includes(keyword) ||
        a.description.toLowerCase().includes(keyword)
      )
    }
    return result
  })

  const agentsByType = computed(() => {
    const map: Record<string, AgentDefinition[]> = {}
    for (const agent of agents.value) {
      if (!map[agent.type]) {
        map[agent.type] = []
      }
      map[agent.type].push(agent)
    }
    return map
  })

  const activeInstances = computed(() => {
    return instances.value.filter(i =>
      i.status === AgentStatus.Working ||
      i.status === AgentStatus.Thinking ||
      i.status === AgentStatus.Waiting
    )
  })

  async function loadAgents(): Promise<void> {
    try {
      loading.value = true
      error.value = null
      agents.value = await agentApi.getAllAgents()
    } catch (e) {
      console.error('加载 Agent 列表失败:', e)
      error.value = e instanceof Error ? e.message : '加载 Agent 列表失败'
    } finally {
      loading.value = false
    }
  }

  async function loadInstances(): Promise<void> {
    try {
      error.value = null
      instances.value = await agentApi.getActiveInstances()
    } catch (e) {
      console.error('加载运行实例失败:', e)
      error.value = e instanceof Error ? e.message : '加载运行实例失败'
    }
  }

  async function createPlan(userRequest: string): Promise<CoordinatorPlan> {
    try {
      loading.value = true
      error.value = null
      const plan = await agentApi.createPlan(userRequest)
      currentPlan.value = plan
      return plan
    } catch (e) {
      console.error('创建执行计划失败:', e)
      error.value = e instanceof Error ? e.message : '创建执行计划失败'
      throw e
    } finally {
      loading.value = false
    }
  }

  async function executePlan(plan: CoordinatorPlan): Promise<AgentExecutionResult> {
    try {
      executing.value = true
      error.value = null
      const result = await agentApi.executePlan(plan)
      executionResult.value = result
      return result
    } catch (e) {
      console.error('执行计划失败:', e)
      error.value = e instanceof Error ? e.message : '执行计划失败'
      throw e
    } finally {
      executing.value = false
    }
  }

  async function handleRequest(userRequest: string): Promise<string> {
    try {
      executing.value = true
      error.value = null
      const result = await agentApi.handleRequest(userRequest)
      return result
    } catch (e) {
      console.error('处理请求失败:', e)
      error.value = e instanceof Error ? e.message : '处理请求失败'
      throw e
    } finally {
      executing.value = false
    }
  }

  function getAgentTypeLabel(type: AgentType): string {
    const labels: Record<AgentType, string> = {
      [AgentType.Coordinator]: '协调者',
      [AgentType.Researcher]: '研究员',
      [AgentType.Writer]: '写作者',
      [AgentType.Programmer]: '程序员',
      [AgentType.Analyst]: '分析师',
      [AgentType.Critic]: '评论家',
      [AgentType.Generalist]: '通用助手'
    }
    return labels[type] || type
  }

  function getAgentTypeIcon(type: AgentType): string {
    const icons: Record<AgentType, string> = {
      [AgentType.Coordinator]: 'fa-solid fa-users-gear',
      [AgentType.Researcher]: 'fa-solid fa-magnifying-glass-chart',
      [AgentType.Writer]: 'fa-solid fa-pen-fancy',
      [AgentType.Programmer]: 'fa-solid fa-code',
      [AgentType.Analyst]: 'fa-solid fa-chart-line',
      [AgentType.Critic]: 'fa-solid fa-comments',
      [AgentType.Generalist]: 'fa-solid fa-robot'
    }
    return icons[type] || 'fa-solid fa-robot'
  }

  function getTaskPriorityLabel(priority: TaskPriority): string {
    const labels: Record<TaskPriority, string> = {
      [TaskPriority.Low]: '低',
      [TaskPriority.Medium]: '中',
      [TaskPriority.High]: '高',
      [TaskPriority.Critical]: '紧急'
    }
    return labels[priority] || '中'
  }

  function getTaskStatusLabel(status: number): string {
    const labels: Record<number, string> = {
      0: '待处理',
      1: '进行中',
      2: '已完成',
      3: '失败',
      4: '已取消'
    }
    return labels[status] || '未知'
  }

  function getAgentStatusLabel(status: AgentStatus): string {
    const labels: Record<AgentStatus, string> = {
      [AgentStatus.Idle]: '空闲',
      [AgentStatus.Thinking]: '思考中',
      [AgentStatus.Working]: '工作中',
      [AgentStatus.Waiting]: '等待中',
      [AgentStatus.Completed]: '已完成',
      [AgentStatus.Failed]: '失败'
    }
    return labels[status] || '未知'
  }

  function getStatusColor(status: string): string {
    const colors: Record<string, string> = {
      Idle: 'text-gray-500',
      Thinking: 'text-blue-500',
      Working: 'text-green-500',
      Waiting: 'text-yellow-500',
      Completed: 'text-emerald-500',
      Failed: 'text-red-500'
    }
    return colors[status] || 'text-gray-500'
  }

  function clearError() {
    error.value = null
  }

  function clearResult() {
    currentPlan.value = null
    executionResult.value = null
  }

  return {
    agents,
    instances,
    currentPlan,
    executionResult,
    loading,
    executing,
    error,
    filterType,
    searchKeyword,
    filteredAgents,
    agentsByType,
    activeInstances,
    loadAgents,
    loadInstances,
    createPlan,
    executePlan,
    handleRequest,
    getAgentTypeLabel,
    getAgentTypeIcon,
    getTaskPriorityLabel,
    getTaskStatusLabel,
    getAgentStatusLabel,
    getStatusColor,
    clearError,
    clearResult
  }
})
