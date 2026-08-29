import { ref, computed } from 'vue'
import { defineStore } from 'pinia'
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
  ScriptExecutionLog,
  ScriptTemplate,
  GenerateScriptRequest,
  GenerateScriptResponse,
  AnalyzeScriptErrorRequest,
  AnalyzeScriptErrorResponse,
  SuggestScriptFixRequest,
  SuggestScriptFixResponse,
  ScriptTemplateListParams
} from '@/types/scriptRunner'
import { scriptRunnerApi } from '@/services/scriptRunnerApi'
import { scriptHub } from '@/services/scriptHub'

export const useScriptRunnerStore = defineStore('scriptRunner', () => {
  const scripts = ref<Script[]>([])
  const currentScript = ref<Script | null>(null)
  const currentExecution = ref<ScriptExecution | null>(null)
  const executions = ref<ScriptExecution[]>([])
  const runtimes = ref<RuntimeEnvironment[]>([])
  const categories = ref<string[]>([])
  const tags = ref<string[]>([])
  const isLoading = ref(false)
  const error = ref<string | null>(null)
  const totalScripts = ref(0)
  const totalExecutions = ref(0)
  const scriptTemplates = ref<ScriptTemplate[]>([])
  const isGeneratingScript = ref(false)
  const isAnalyzingError = ref(false)
  const isSuggestingFix = ref(false)
  const generatedScript = ref<GenerateScriptResponse | null>(null)
  const errorAnalysis = ref<AnalyzeScriptErrorResponse | null>(null)
  const scriptFix = ref<SuggestScriptFixResponse | null>(null)

  const favoriteScripts = computed(() =>
    scripts.value.filter(s => s.isFavorite)
  )

  const recentScripts = computed(() => {
    const sorted = [...scripts.value].filter(s => s.lastExecutedAt)
    sorted.sort((a, b) => {
      const aTime = a.lastExecutedAt?.getTime() || 0
      const bTime = b.lastExecutedAt?.getTime() || 0
      return bTime - aTime
    })
    return sorted.slice(0, 10)
  })

  const languages = computed(() => {
    const langs = new Set<string>()
    scripts.value.forEach(s => langs.add(s.language))
    return Array.from(langs)
  })

  const scriptCategories = computed(() => {
    const cats = new Set<string>()
    scripts.value.forEach(s => {
      if (s.category) {
        cats.add(s.category)
      }
    })
    return Array.from(cats)
  })

  async function loadScripts(params?: ScriptListParams): Promise<void> {
    try {
      isLoading.value = true
      error.value = null
      const result = await scriptRunnerApi.listScripts(params)
      scripts.value = result.items
      totalScripts.value = result.total
    } catch (e) {
      console.error('加载脚本列表失败:', e)
      error.value = e instanceof Error ? e.message : '加载脚本列表失败'
    } finally {
      isLoading.value = false
    }
  }

  async function loadScript(id: string): Promise<void> {
    try {
      isLoading.value = true
      error.value = null
      currentScript.value = await scriptRunnerApi.getScript(id)
    } catch (e) {
      console.error('加载脚本详情失败:', e)
      error.value = e instanceof Error ? e.message : '加载脚本详情失败'
    } finally {
      isLoading.value = false
    }
  }

  async function createScript(script: CreateScriptRequest): Promise<Script> {
    try {
      error.value = null
      const newScript = await scriptRunnerApi.createScript(script)
      scripts.value.unshift(newScript)
      totalScripts.value++
      return newScript
    } catch (e) {
      console.error('创建脚本失败:', e)
      error.value = e instanceof Error ? e.message : '创建脚本失败'
      throw e
    }
  }

  async function updateScript(id: string, script: UpdateScriptRequest): Promise<void> {
    try {
      error.value = null
      const updated = await scriptRunnerApi.updateScript(id, script)
      const index = scripts.value.findIndex(s => s.id === id)
      if (index !== -1) {
        scripts.value[index] = updated
      }
      if (currentScript.value?.id === id) {
        currentScript.value = updated
      }
    } catch (e) {
      console.error('更新脚本失败:', e)
      error.value = e instanceof Error ? e.message : '更新脚本失败'
      throw e
    }
  }

  async function deleteScript(id: string): Promise<void> {
    try {
      error.value = null
      await scriptRunnerApi.deleteScript(id)
      scripts.value = scripts.value.filter(s => s.id !== id)
      totalScripts.value--
      if (currentScript.value?.id === id) {
        currentScript.value = null
      }
    } catch (e) {
      console.error('删除脚本失败:', e)
      error.value = e instanceof Error ? e.message : '删除脚本失败'
      throw e
    }
  }

  async function favoriteScript(id: string, isFavorite: boolean): Promise<void> {
    try {
      error.value = null
      await scriptRunnerApi.favoriteScript(id, isFavorite)
      const script = scripts.value.find(s => s.id === id)
      if (script) {
        script.isFavorite = isFavorite
      }
      if (currentScript.value?.id === id) {
        currentScript.value.isFavorite = isFavorite
      }
    } catch (e) {
      console.error('收藏操作失败:', e)
      error.value = e instanceof Error ? e.message : '收藏操作失败'
      throw e
    }
  }

  async function loadCategories(): Promise<void> {
    try {
      error.value = null
      categories.value = await scriptRunnerApi.getCategories()
    } catch (e) {
      console.error('加载分类列表失败:', e)
      error.value = e instanceof Error ? e.message : '加载分类列表失败'
    }
  }

  async function loadTags(): Promise<void> {
    try {
      error.value = null
      tags.value = await scriptRunnerApi.getTags()
    } catch (e) {
      console.error('加载标签列表失败:', e)
      error.value = e instanceof Error ? e.message : '加载标签列表失败'
    }
  }

  async function loadRuntimes(): Promise<void> {
    try {
      error.value = null
      runtimes.value = await scriptRunnerApi.getRuntimes()
    } catch (e) {
      console.error('加载运行环境失败:', e)
      error.value = e instanceof Error ? e.message : '加载运行环境失败'
    }
  }

  async function executeScript(id: string, request?: ExecuteScriptRequest): Promise<ScriptExecution> {
    try {
      error.value = null
      const execution = await scriptRunnerApi.executeScript(id, request)
      currentExecution.value = execution
      executions.value.unshift(execution)
      const script = scripts.value.find(s => s.id === id)
      if (script) {
        script.usageCount++
        script.lastExecutedAt = new Date()
      }
      if (currentScript.value?.id === id) {
        currentScript.value.usageCount++
        currentScript.value.lastExecutedAt = new Date()
      }
      scriptHub.subscribe(execution.id, execution)
      return execution
    } catch (e) {
      console.error('执行脚本失败:', e)
      error.value = e instanceof Error ? e.message : '执行脚本失败'
      throw e
    }
  }

  async function executeCode(request: ExecuteCodeRequest): Promise<ScriptExecution> {
    try {
      error.value = null
      const execution = await scriptRunnerApi.executeCode(request)
      currentExecution.value = execution
      executions.value.unshift(execution)
      scriptHub.subscribe(execution.id, execution)
      return execution
    } catch (e) {
      console.error('执行代码失败:', e)
      error.value = e instanceof Error ? e.message : '执行代码失败'
      throw e
    }
  }

  async function loadExecution(id: string): Promise<void> {
    try {
      isLoading.value = true
      error.value = null
      currentExecution.value = await scriptRunnerApi.getExecution(id)
    } catch (e) {
      console.error('加载执行详情失败:', e)
      error.value = e instanceof Error ? e.message : '加载执行详情失败'
    } finally {
      isLoading.value = false
    }
  }

  async function cancelExecution(id: string): Promise<void> {
    try {
      error.value = null
      await scriptRunnerApi.cancelExecution(id)
      if (currentExecution.value?.id === id) {
        currentExecution.value.status = 'cancelled'
      }
      const execution = executions.value.find(e => e.id === id)
      if (execution) {
        execution.status = 'cancelled'
      }
    } catch (e) {
      console.error('取消执行失败:', e)
      error.value = e instanceof Error ? e.message : '取消执行失败'
      throw e
    }
  }

  async function loadExecutions(params?: ExecutionListParams): Promise<void> {
    try {
      isLoading.value = true
      error.value = null
      const result = await scriptRunnerApi.listExecutions(params)
      executions.value = result.items
      totalExecutions.value = result.total
    } catch (e) {
      console.error('加载执行记录失败:', e)
      error.value = e instanceof Error ? e.message : '加载执行记录失败'
    } finally {
      isLoading.value = false
    }
  }

  function appendExecutionLog(executionId: string, log: ScriptExecutionLog): void {
    if (currentExecution.value?.id === executionId) {
      currentExecution.value.logs.push(log)
      currentExecution.value.output += log.message
    }
    const execution = executions.value.find(e => e.id === executionId)
    if (execution) {
      execution.logs.push(log)
      execution.output += log.message
    }
  }

  function updateCurrentExecution(execution: ScriptExecution): void {
    if (currentExecution.value?.id === execution.id) {
      currentExecution.value = execution
    }
    const index = executions.value.findIndex(e => e.id === execution.id)
    if (index !== -1) {
      executions.value[index] = execution
    }
  }

  async function generateScript(request: GenerateScriptRequest): Promise<GenerateScriptResponse> {
    try {
      isGeneratingScript.value = true
      error.value = null
      const result = await scriptRunnerApi.generateScript(request)
      generatedScript.value = result
      return result
    } catch (e) {
      console.error('生成脚本失败:', e)
      error.value = e instanceof Error ? e.message : '生成脚本失败'
      throw e
    } finally {
      isGeneratingScript.value = false
    }
  }

  async function analyzeScriptError(request: AnalyzeScriptErrorRequest): Promise<AnalyzeScriptErrorResponse> {
    try {
      isAnalyzingError.value = true
      error.value = null
      const result = await scriptRunnerApi.analyzeScriptError(request)
      errorAnalysis.value = result
      return result
    } catch (e) {
      console.error('分析错误失败:', e)
      error.value = e instanceof Error ? e.message : '分析错误失败'
      throw e
    } finally {
      isAnalyzingError.value = false
    }
  }

  async function suggestScriptFix(request: SuggestScriptFixRequest): Promise<SuggestScriptFixResponse> {
    try {
      isSuggestingFix.value = true
      error.value = null
      const result = await scriptRunnerApi.suggestScriptFix(request)
      scriptFix.value = result
      return result
    } catch (e) {
      console.error('获取修复建议失败:', e)
      error.value = e instanceof Error ? e.message : '获取修复建议失败'
      throw e
    } finally {
      isSuggestingFix.value = false
    }
  }

  async function loadScriptTemplates(params?: ScriptTemplateListParams): Promise<void> {
    try {
      isLoading.value = true
      error.value = null
      scriptTemplates.value = await scriptRunnerApi.getScriptTemplates(params)
    } catch (e) {
      console.error('加载脚本模板失败:', e)
      error.value = e instanceof Error ? e.message : '加载脚本模板失败'
    } finally {
      isLoading.value = false
    }
  }

  function clearGeneratedScript(): void {
    generatedScript.value = null
  }

  function clearErrorAnalysis(): void {
    errorAnalysis.value = null
  }

  function clearScriptFix(): void {
    scriptFix.value = null
  }

  function clearError(): void {
    error.value = null
  }

  function setCurrentScript(script: Script | null): void {
    currentScript.value = script
  }

  function setCurrentExecution(execution: ScriptExecution | null): void {
    currentExecution.value = execution
  }

  return {
    scripts,
    currentScript,
    currentExecution,
    executions,
    runtimes,
    categories,
    tags,
    isLoading,
    error,
    totalScripts,
    totalExecutions,
    scriptTemplates,
    isGeneratingScript,
    isAnalyzingError,
    isSuggestingFix,
    generatedScript,
    errorAnalysis,
    scriptFix,
    favoriteScripts,
    recentScripts,
    languages,
    scriptCategories,
    loadScripts,
    loadScript,
    createScript,
    updateScript,
    deleteScript,
    favoriteScript,
    loadCategories,
    loadTags,
    loadRuntimes,
    executeScript,
    executeCode,
    loadExecution,
    cancelExecution,
    loadExecutions,
    appendExecutionLog,
    updateCurrentExecution,
    generateScript,
    analyzeScriptError,
    suggestScriptFix,
    loadScriptTemplates,
    clearGeneratedScript,
    clearErrorAnalysis,
    clearScriptFix,
    clearError,
    setCurrentScript,
    setCurrentExecution
  }
})
