/**
 * 工作流状态管理Store
 */

import { ref, computed } from 'vue'
import { defineStore } from 'pinia'
import type {
  WorkflowDefinition,
  WorkflowExecution,
  WorkflowListParams,
  ExecutionListParams,
  PlanWorkflowRequest,
  PlanWorkflowResponse,
  WorkflowTemplate
} from '@/types/workflow'
import { workflowApi } from '@/services/workflowApi'

export const useWorkflowStore = defineStore('workflow', () => {
  const workflows = ref<WorkflowDefinition[]>([])
  const currentWorkflow = ref<WorkflowDefinition | null>(null)
  const currentExecution = ref<WorkflowExecution | null>(null)
  const executions = ref<WorkflowExecution[]>([])
  const templates = ref<WorkflowTemplate[]>([])
  const isLoading = ref(false)
  const error = ref<string | null>(null)
  const totalWorkflows = ref(0)
  const totalExecutions = ref(0)

  const favoriteWorkflows = computed(() =>
    workflows.value.filter(w => w.isFavorite)
  )

  const recentExecutions = computed(() => {
    const sorted = [...executions.value]
    sorted.sort((a, b) => {
      const aTime = a.startTime?.getTime() || 0
      const bTime = b.startTime?.getTime() || 0
      return bTime - aTime
    })
    return sorted.slice(0, 10)
  })

  const categories = computed(() => {
    const cats = new Set<string>()
    workflows.value.forEach(w => {
      if (w.category) {
        cats.add(w.category)
      }
    })
    return Array.from(cats)
  })

  async function loadWorkflows(params?: WorkflowListParams): Promise<void> {
    try {
      isLoading.value = true
      error.value = null
      const result = await workflowApi.listWorkflows(params)
      workflows.value = result.items
      totalWorkflows.value = result.total
    } catch (e) {
      console.error('加载工作流列表失败:', e)
      error.value = e instanceof Error ? e.message : '加载工作流列表失败'
    } finally {
      isLoading.value = false
    }
  }

  async function loadWorkflow(id: string): Promise<void> {
    try {
      isLoading.value = true
      error.value = null
      currentWorkflow.value = await workflowApi.getWorkflow(id)
    } catch (e) {
      console.error('加载工作流详情失败:', e)
      error.value = e instanceof Error ? e.message : '加载工作流详情失败'
    } finally {
      isLoading.value = false
    }
  }

  async function createWorkflow(definition: Omit<WorkflowDefinition, 'id' | 'createdAt' | 'updatedAt' | 'usageCount'>): Promise<WorkflowDefinition> {
    try {
      error.value = null
      const newWorkflow = await workflowApi.createWorkflow(definition)
      workflows.value.unshift(newWorkflow)
      totalWorkflows.value++
      return newWorkflow
    } catch (e) {
      console.error('创建工作流失败:', e)
      error.value = e instanceof Error ? e.message : '创建工作流失败'
      throw e
    }
  }

  async function updateWorkflow(id: string, definition: Partial<WorkflowDefinition>): Promise<void> {
    try {
      error.value = null
      const updated = await workflowApi.updateWorkflow(id, definition)
      const index = workflows.value.findIndex(w => w.id === id)
      if (index !== -1) {
        workflows.value[index] = updated
      }
      if (currentWorkflow.value?.id === id) {
        currentWorkflow.value = updated
      }
    } catch (e) {
      console.error('更新工作流失败:', e)
      error.value = e instanceof Error ? e.message : '更新工作流失败'
      throw e
    }
  }

  async function deleteWorkflow(id: string): Promise<void> {
    try {
      error.value = null
      await workflowApi.deleteWorkflow(id)
      workflows.value = workflows.value.filter(w => w.id !== id)
      totalWorkflows.value--
      if (currentWorkflow.value?.id === id) {
        currentWorkflow.value = null
      }
    } catch (e) {
      console.error('删除工作流失败:', e)
      error.value = e instanceof Error ? e.message : '删除工作流失败'
      throw e
    }
  }

  async function favoriteWorkflow(id: string, isFavorite: boolean): Promise<void> {
    try {
      error.value = null
      await workflowApi.favoriteWorkflow(id, isFavorite)
      const workflow = workflows.value.find(w => w.id === id)
      if (workflow) {
        workflow.isFavorite = isFavorite
      }
      if (currentWorkflow.value?.id === id) {
        currentWorkflow.value.isFavorite = isFavorite
      }
    } catch (e) {
      console.error('收藏操作失败:', e)
      error.value = e instanceof Error ? e.message : '收藏操作失败'
      throw e
    }
  }

  async function executeWorkflow(id: string, inputVariables?: Record<string, unknown>): Promise<WorkflowExecution> {
    try {
      error.value = null
      const execution = await workflowApi.executeWorkflow(id, inputVariables)
      currentExecution.value = execution
      executions.value.unshift(execution)
      const workflow = workflows.value.find(w => w.id === id)
      if (workflow) {
        workflow.usageCount++
      }
      return execution
    } catch (e) {
      console.error('执行工作流失败:', e)
      error.value = e instanceof Error ? e.message : '执行工作流失败'
      throw e
    }
  }

  async function loadExecutions(params?: ExecutionListParams): Promise<void> {
    try {
      isLoading.value = true
      error.value = null
      const result = await workflowApi.listExecutions(params)
      executions.value = result.items
      totalExecutions.value = result.total
    } catch (e) {
      console.error('加载执行记录失败:', e)
      error.value = e instanceof Error ? e.message : '加载执行记录失败'
    } finally {
      isLoading.value = false
    }
  }

  async function loadExecution(executionId: string): Promise<void> {
    try {
      isLoading.value = true
      error.value = null
      currentExecution.value = await workflowApi.getExecutionDetail(executionId)
    } catch (e) {
      console.error('加载执行详情失败:', e)
      error.value = e instanceof Error ? e.message : '加载执行详情失败'
    } finally {
      isLoading.value = false
    }
  }

  async function pauseExecution(executionId: string): Promise<void> {
    try {
      error.value = null
      await workflowApi.pauseExecution(executionId)
      if (currentExecution.value?.id === executionId) {
        currentExecution.value.status = 'paused'
      }
      const execution = executions.value.find(e => e.id === executionId)
      if (execution) {
        execution.status = 'paused'
      }
    } catch (e) {
      console.error('暂停执行失败:', e)
      error.value = e instanceof Error ? e.message : '暂停执行失败'
      throw e
    }
  }

  async function resumeExecution(executionId: string): Promise<void> {
    try {
      error.value = null
      await workflowApi.resumeExecution(executionId)
      if (currentExecution.value?.id === executionId) {
        currentExecution.value.status = 'running'
      }
      const execution = executions.value.find(e => e.id === executionId)
      if (execution) {
        execution.status = 'running'
      }
    } catch (e) {
      console.error('继续执行失败:', e)
      error.value = e instanceof Error ? e.message : '继续执行失败'
      throw e
    }
  }

  async function cancelExecution(executionId: string): Promise<void> {
    try {
      error.value = null
      await workflowApi.cancelExecution(executionId)
      if (currentExecution.value?.id === executionId) {
        currentExecution.value.status = 'cancelled'
      }
      const execution = executions.value.find(e => e.id === executionId)
      if (execution) {
        execution.status = 'cancelled'
      }
    } catch (e) {
      console.error('取消执行失败:', e)
      error.value = e instanceof Error ? e.message : '取消执行失败'
      throw e
    }
  }

  async function planWorkflow(request: PlanWorkflowRequest): Promise<PlanWorkflowResponse> {
    try {
      isLoading.value = true
      error.value = null
      return await workflowApi.planWorkflow(request)
    } catch (e) {
      console.error('AI规划工作流失败:', e)
      error.value = e instanceof Error ? e.message : 'AI规划工作流失败'
      throw e
    } finally {
      isLoading.value = false
    }
  }

  async function loadTemplates(): Promise<void> {
    try {
      error.value = null
      templates.value = await workflowApi.getWorkflowTemplates()
    } catch (e) {
      console.error('加载工作流模板失败:', e)
      error.value = e instanceof Error ? e.message : '加载工作流模板失败'
    }
  }

  function clearError(): void {
    error.value = null
  }

  function setCurrentWorkflow(workflow: WorkflowDefinition | null): void {
    currentWorkflow.value = workflow
  }

  function setCurrentExecution(execution: WorkflowExecution | null): void {
    currentExecution.value = execution
  }

  return {
    workflows,
    currentWorkflow,
    currentExecution,
    executions,
    templates,
    isLoading,
    error,
    totalWorkflows,
    totalExecutions,
    favoriteWorkflows,
    recentExecutions,
    categories,
    loadWorkflows,
    loadWorkflow,
    createWorkflow,
    updateWorkflow,
    deleteWorkflow,
    favoriteWorkflow,
    executeWorkflow,
    loadExecutions,
    loadExecution,
    pauseExecution,
    resumeExecution,
    cancelExecution,
    planWorkflow,
    loadTemplates,
    clearError,
    setCurrentWorkflow,
    setCurrentExecution
  }
})
