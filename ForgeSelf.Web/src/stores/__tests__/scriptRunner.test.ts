import { describe, it, expect, beforeEach, vi } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useScriptRunnerStore } from '../scriptRunner'

vi.mock('@/services/scriptRunnerApi', () => ({
  scriptRunnerApi: {
    listScripts: vi.fn(),
    getScript: vi.fn(),
    createScript: vi.fn(),
    updateScript: vi.fn(),
    deleteScript: vi.fn(),
    favoriteScript: vi.fn(),
    getCategories: vi.fn(),
    getTags: vi.fn(),
    getRuntimes: vi.fn(),
    executeScript: vi.fn(),
    executeCode: vi.fn(),
    getExecution: vi.fn(),
    cancelExecution: vi.fn(),
    listExecutions: vi.fn(),
    generateScript: vi.fn(),
    analyzeScriptError: vi.fn(),
    suggestScriptFix: vi.fn(),
    getScriptTemplates: vi.fn(),
  }
}))

vi.mock('@/services/scriptHub', () => ({
  scriptHub: {
    subscribe: vi.fn(),
    unsubscribe: vi.fn(),
  }
}))

describe('ScriptRunner Store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('should initialize with default state', () => {
    const store = useScriptRunnerStore()
    
    expect(store.scripts).toEqual([])
    expect(store.currentScript).toBeNull()
    expect(store.currentExecution).toBeNull()
    expect(store.executions).toEqual([])
    expect(store.runtimes).toEqual([])
    expect(store.categories).toEqual([])
    expect(store.tags).toEqual([])
    expect(store.isLoading).toBe(false)
    expect(store.error).toBeNull()
    expect(store.totalScripts).toBe(0)
    expect(store.totalExecutions).toBe(0)
    expect(store.scriptTemplates).toEqual([])
    expect(store.isGeneratingScript).toBe(false)
    expect(store.isAnalyzingError).toBe(false)
    expect(store.isSuggestingFix).toBe(false)
    expect(store.generatedScript).toBeNull()
    expect(store.errorAnalysis).toBeNull()
    expect(store.scriptFix).toBeNull()
  })

  it('favoriteScripts should return only favorite scripts', () => {
    const store = useScriptRunnerStore()
    
    store.scripts = [
      { id: '1', name: '脚本1', isFavorite: true, language: 'powershell', code: '', createdAt: new Date(), updatedAt: new Date(), usageCount: 0 },
      { id: '2', name: '脚本2', isFavorite: false, language: 'python', code: '', createdAt: new Date(), updatedAt: new Date(), usageCount: 0 },
      { id: '3', name: '脚本3', isFavorite: true, language: 'powershell', code: '', createdAt: new Date(), updatedAt: new Date(), usageCount: 0 },
    ] as any
    
    expect(store.favoriteScripts).toHaveLength(2)
    expect(store.favoriteScripts.every(s => s.isFavorite)).toBe(true)
  })

  it('recentScripts should return top 10 sorted by lastExecutedAt desc', () => {
    const store = useScriptRunnerStore()
    
    const now = Date.now()
    store.scripts = Array.from({ length: 15 }, (_, i) => ({
      id: String(i),
      name: `脚本${i}`,
      language: 'powershell',
      code: '',
      isFavorite: false,
      createdAt: new Date(),
      updatedAt: new Date(),
      usageCount: i,
      lastExecutedAt: new Date(now - i * 1000),
    })) as any
    
    expect(store.recentScripts).toHaveLength(10)
    expect(store.recentScripts[0].id).toBe('0')
    expect(store.recentScripts[9].id).toBe('9')
  })

  it('languages should return unique languages from scripts', () => {
    const store = useScriptRunnerStore()
    
    store.scripts = [
      { id: '1', name: '脚本1', language: 'powershell', code: '', isFavorite: false, createdAt: new Date(), updatedAt: new Date(), usageCount: 0 },
      { id: '2', name: '脚本2', language: 'python', code: '', isFavorite: false, createdAt: new Date(), updatedAt: new Date(), usageCount: 0 },
      { id: '3', name: '脚本3', language: 'powershell', code: '', isFavorite: false, createdAt: new Date(), updatedAt: new Date(), usageCount: 0 },
      { id: '4', name: '脚本4', language: 'nodejs', code: '', isFavorite: false, createdAt: new Date(), updatedAt: new Date(), usageCount: 0 },
    ] as any
    
    expect(store.languages).toHaveLength(3)
    expect(store.languages).toContain('powershell')
    expect(store.languages).toContain('python')
    expect(store.languages).toContain('nodejs')
  })

  it('scriptCategories should return unique categories from scripts', () => {
    const store = useScriptRunnerStore()
    
    store.scripts = [
      { id: '1', name: '脚本1', category: '文件处理', language: 'powershell', code: '', isFavorite: false, createdAt: new Date(), updatedAt: new Date(), usageCount: 0 },
      { id: '2', name: '脚本2', category: '系统监控', language: 'python', code: '', isFavorite: false, createdAt: new Date(), updatedAt: new Date(), usageCount: 0 },
      { id: '3', name: '脚本3', category: '文件处理', language: 'powershell', code: '', isFavorite: false, createdAt: new Date(), updatedAt: new Date(), usageCount: 0 },
      { id: '4', name: '脚本4', language: 'nodejs', code: '', isFavorite: false, createdAt: new Date(), updatedAt: new Date(), usageCount: 0 },
    ] as any
    
    expect(store.scriptCategories).toHaveLength(2)
    expect(store.scriptCategories).toContain('文件处理')
    expect(store.scriptCategories).toContain('系统监控')
  })

  it('clearError should reset error to null', () => {
    const store = useScriptRunnerStore()
    
    store.error = '测试错误'
    store.clearError()
    
    expect(store.error).toBeNull()
  })

  it('setCurrentScript should update currentScript', () => {
    const store = useScriptRunnerStore()
    const script = {
      id: '1',
      name: '测试脚本',
      language: 'powershell',
      code: 'Write-Host "hello"',
      isFavorite: false,
      createdAt: new Date(),
      updatedAt: new Date(),
      usageCount: 0,
    } as any
    
    store.setCurrentScript(script)
    expect(store.currentScript?.id).toBe('1')
    expect(store.currentScript?.name).toBe('测试脚本')
    
    store.setCurrentScript(null)
    expect(store.currentScript).toBeNull()
  })

  it('setCurrentExecution should update currentExecution', () => {
    const store = useScriptRunnerStore()
    const execution = {
      id: '1',
      scriptId: '1',
      scriptName: '测试执行',
      status: 'running',
      startTime: new Date(),
      output: '',
      errorOutput: '',
      exitCode: null,
      logs: [],
    } as any
    
    store.setCurrentExecution(execution)
    expect(store.currentExecution?.id).toBe('1')
    expect(store.currentExecution?.scriptName).toBe('测试执行')
    expect(store.currentExecution?.status).toBe('running')
    
    store.setCurrentExecution(null)
    expect(store.currentExecution).toBeNull()
  })

  it('appendExecutionLog should add log to currentExecution and executions list', () => {
    const store = useScriptRunnerStore()
    const executionId = 'exec-1'
    const log = {
      id: 'log-1',
      timestamp: new Date(),
      stream: 'stdout' as const,
      message: 'test output',
    }
    
    store.currentExecution = { id: executionId, output: '', logs: [] } as any
    store.executions = [{ id: executionId, output: '', logs: [] }] as any
    
    store.appendExecutionLog(executionId, log)
    
    expect(store.currentExecution?.logs).toHaveLength(1)
    expect(store.currentExecution?.output).toBe('test output')
    expect(store.executions[0].logs).toHaveLength(1)
    expect(store.executions[0].output).toBe('test output')
  })

  it('updateCurrentExecution should update execution in both places', () => {
    const store = useScriptRunnerStore()
    const executionId = 'exec-1'
    const updatedExecution = {
      id: executionId,
      status: 'completed',
      output: 'done',
      logs: [],
    }
    
    store.currentExecution = { id: executionId, status: 'running', output: '', logs: [] } as any
    store.executions = [{ id: executionId, status: 'running', output: '', logs: [] }] as any
    
    store.updateCurrentExecution(updatedExecution as any)
    
    expect(store.currentExecution?.status).toBe('completed')
    expect(store.currentExecution?.output).toBe('done')
    expect(store.executions[0].status).toBe('completed')
    expect(store.executions[0].output).toBe('done')
  })

  it('clearGeneratedScript should reset generatedScript to null', () => {
    const store = useScriptRunnerStore()
    
    store.generatedScript = { script: 'test', explanation: 'test' } as any
    store.clearGeneratedScript()
    
    expect(store.generatedScript).toBeNull()
  })

  it('clearErrorAnalysis should reset errorAnalysis to null', () => {
    const store = useScriptRunnerStore()
    
    store.errorAnalysis = { analysis: 'test' } as any
    store.clearErrorAnalysis()
    
    expect(store.errorAnalysis).toBeNull()
  })

  it('clearScriptFix should reset scriptFix to null', () => {
    const store = useScriptRunnerStore()
    
    store.scriptFix = { fix: 'test' } as any
    store.clearScriptFix()
    
    expect(store.scriptFix).toBeNull()
  })
})
