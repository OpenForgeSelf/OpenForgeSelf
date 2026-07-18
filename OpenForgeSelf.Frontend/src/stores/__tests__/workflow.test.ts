import { describe, it, expect, beforeEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useWorkflowStore } from '../workflow'

describe('Workflow Store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('should initialize with default state', () => {
    const store = useWorkflowStore()
    
    expect(store.workflows).toEqual([])
    expect(store.currentWorkflow).toBeNull()
    expect(store.currentExecution).toBeNull()
    expect(store.executions).toEqual([])
    expect(store.templates).toEqual([])
    expect(store.isLoading).toBe(false)
    expect(store.error).toBeNull()
    expect(store.totalWorkflows).toBe(0)
    expect(store.totalExecutions).toBe(0)
  })

  it('favoriteWorkflows should return only favorite workflows', () => {
    const store = useWorkflowStore()
    
    store.workflows = [
      { id: '1', name: '工作流1', isFavorite: true, steps: [], variables: [], status: 'draft' as const, createdAt: new Date(), updatedAt: new Date(), usageCount: 0 },
      { id: '2', name: '工作流2', isFavorite: false, steps: [], variables: [], status: 'draft' as const, createdAt: new Date(), updatedAt: new Date(), usageCount: 0 },
      { id: '3', name: '工作流3', isFavorite: true, steps: [], variables: [], status: 'draft' as const, createdAt: new Date(), updatedAt: new Date(), usageCount: 0 },
    ]
    
    expect(store.favoriteWorkflows).toHaveLength(2)
    expect(store.favoriteWorkflows.every(w => w.isFavorite)).toBe(true)
  })

  it('recentExecutions should return top 10 sorted by startTime desc', () => {
    const store = useWorkflowStore()
    
    const now = Date.now()
    store.executions = Array.from({ length: 15 }, (_, i) => ({
      id: String(i),
      workflowId: '1',
      workflowName: `执行${i}`,
      status: 'completed' as const,
      startTime: new Date(now - i * 1000),
      steps: [],
      logs: [],
    }))
    
    expect(store.recentExecutions).toHaveLength(10)
    expect(store.recentExecutions[0].id).toBe('0')
    expect(store.recentExecutions[9].id).toBe('9')
  })

  it('categories should return unique categories from workflows', () => {
    const store = useWorkflowStore()
    
    store.workflows = [
      { id: '1', name: '工作流1', category: '文件处理', steps: [], variables: [], status: 'draft' as const, isFavorite: false, createdAt: new Date(), updatedAt: new Date(), usageCount: 0 },
      { id: '2', name: '工作流2', category: '系统监控', steps: [], variables: [], status: 'draft' as const, isFavorite: false, createdAt: new Date(), updatedAt: new Date(), usageCount: 0 },
      { id: '3', name: '工作流3', category: '文件处理', steps: [], variables: [], status: 'draft' as const, isFavorite: false, createdAt: new Date(), updatedAt: new Date(), usageCount: 0 },
      { id: '4', name: '工作流4', category: undefined, steps: [], variables: [], status: 'draft' as const, isFavorite: false, createdAt: new Date(), updatedAt: new Date(), usageCount: 0 },
    ]
    
    expect(store.categories).toHaveLength(2)
    expect(store.categories).toContain('文件处理')
    expect(store.categories).toContain('系统监控')
  })

  it('clearError should reset error to null', () => {
    const store = useWorkflowStore()
    
    store.error = '测试错误'
    store.clearError()
    
    expect(store.error).toBeNull()
  })

  it('setCurrentWorkflow should update currentWorkflow', () => {
    const store = useWorkflowStore()
    const workflow = {
      id: '1',
      name: '测试工作流',
      steps: [],
      variables: [],
      status: 'draft' as const,
      isFavorite: false,
      createdAt: new Date(),
      updatedAt: new Date(),
      usageCount: 0,
    }
    
    store.setCurrentWorkflow(workflow)
    expect(store.currentWorkflow).toStrictEqual(workflow)

    store.setCurrentWorkflow(null)
    expect(store.currentWorkflow).toBeNull()
  })

  it('setCurrentExecution should update currentExecution', () => {
    const store = useWorkflowStore()
    const execution = {
      id: '1',
      workflowId: '1',
      workflowName: '测试执行',
      status: 'running' as const,
      startTime: new Date(),
      steps: [],
      logs: [],
    }

    store.setCurrentExecution(execution)
    expect(store.currentExecution).toStrictEqual(execution)
    
    store.setCurrentExecution(null)
    expect(store.currentExecution).toBeNull()
  })
})
