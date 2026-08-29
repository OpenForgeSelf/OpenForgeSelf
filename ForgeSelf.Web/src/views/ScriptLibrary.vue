<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed } from 'vue'
import AppLogo from '@/components/AppLogo.vue'
import { useScriptRunnerStore } from '@/stores/scriptRunner'
import { scriptHub } from '@/services/scriptHub'
import type { Script, ScriptParameter, ScriptExecutionLog, ScriptListParams } from '@/types/scriptRunner'
import ParameterForm from '@/components/scriptrunner/ParameterForm.vue'
import ExecutionDetail from '@/components/scriptrunner/ExecutionDetail.vue'

const scriptRunnerStore = useScriptRunnerStore()

const activeTab = ref<'editor' | 'executions'>('editor')
const showEditor = ref(false)
const showParameterForm = ref(false)
const showConsole = ref(false)
const showExecutionDetail = ref(false)
const editingScript = ref<Script | null>(null)
const executingScript = ref<Script | null>(null)
const selectedExecutionId = ref<string | null>(null)

// ---- 设计稿状态 ----
const activeLangFilter = ref<string>('全部')
const codeText = ref('')
const outputLines = ref<string[]>([])
const outputExitCode = ref<number | null>(null)
const outputDuration = ref('')

const langFilters = ['全部', 'PowerShell', 'Python', 'Node.js']

const langBadgeClass = (lang: string): string => {
  switch (lang) {
    case 'powershell': return 'fs-lang-badge--ps'
    case 'python': return 'fs-lang-badge--py'
    case 'nodejs': return 'fs-lang-badge--js'
    default: return ''
  }
}

const langBadgeLabel = (lang: string): string => {
  switch (lang) {
    case 'powershell': return 'PowerShell'
    case 'python': return 'Python'
    case 'nodejs': return 'Node.js'
    default: return lang
  }
}

const languageCssMap: Record<string, string> = {
  'PowerShell': 'powershell',
  'Python': 'python',
  'Node.js': 'nodejs'
}

const filteredScripts = computed(() => {
  const filter = activeLangFilter.value
  if (filter === '全部') return scriptRunnerStore.scripts
  const lang = languageCssMap[filter]
  if (!lang) return scriptRunnerStore.scripts
  return scriptRunnerStore.scripts.filter(s => s.language === lang)
})

const lineNumbers = computed(() => {
  const count = codeText.value.split('\n').length
  return Array.from({ length: count }, (_, i) => i + 1)
})

async function loadScripts(params?: ScriptListParams): Promise<void> {
  await scriptRunnerStore.loadScripts(params)
}

function handleCreate(): void {
  editingScript.value = null
  showEditor.value = true
  activeTab.value = 'editor'
}

function handleSelect(script: Script): void {
  editingScript.value = script
  showEditor.value = true
  activeTab.value = 'editor'
  codeText.value = script.code
  outputLines.value = []
  outputExitCode.value = null
  outputDuration.value = ''
}

async function executeScript(script: Script, parameters?: Record<string, unknown>): Promise<void> {
  try {
    const execution = await scriptRunnerStore.executeScript(script.id, { parameters })
    selectedExecutionId.value = execution.id
    showConsole.value = true
    showParameterForm.value = false
    outputLines.value = []
    outputExitCode.value = null
    outputDuration.value = ''
  } catch (e) {
    console.error('执行脚本失败:', e)
  }
}

async function handleParameterFormSubmit(parameters: Record<string, unknown>): Promise<void> {
  if (executingScript.value) {
    await executeScript(executingScript.value, parameters)
  }
}

function handleParameterFormCancel(): void {
  showParameterForm.value = false
  executingScript.value = null
}

async function handleEditorSave(scriptData: Partial<Script>): Promise<void> {
  try {
    if (editingScript.value) {
      await scriptRunnerStore.updateScript(editingScript.value.id, scriptData)
    } else {
      await scriptRunnerStore.createScript({
        name: scriptData.name!,
        description: scriptData.description,
        language: scriptData.language!,
        code: scriptData.code!,
        category: scriptData.category,
        tags: scriptData.tags,
        parameters: scriptData.parameters as ScriptParameter[] || [],
        timeout: scriptData.timeout
      })
    }
    showEditor.value = false
    editingScript.value = null
  } catch (e) {
    console.error('保存脚本失败:', e)
  }
}

function handleEditorRun(script: Script): void {
  if (script.parameters.length > 0) {
    executingScript.value = script
    showParameterForm.value = true
  } else {
    executeScript(script)
  }
}

function handleConsoleCancel(): void {
  if (selectedExecutionId.value) {
    scriptRunnerStore.cancelExecution(selectedExecutionId.value)
  }
}

function handleExecutionDetailClose(): void {
  showExecutionDetail.value = false
}

function handleOutputReceived(executionId: string, log: ScriptExecutionLog): void {
  scriptRunnerStore.appendExecutionLog(executionId, log)
  outputLines.value.push(`[${new Date(log.timestamp).toLocaleString()}] ${log.message}`)
}

function handleExecutionUpdate(execution: import('@/types/scriptRunner').ScriptExecution): void {
  scriptRunnerStore.updateCurrentExecution(execution)
  if (execution.status === 'completed' || execution.status === 'failed' || execution.status === 'cancelled' || execution.status === 'timeout') {
    outputExitCode.value = execution.exitCode ?? null
    if (execution.duration) {
      const s = Math.floor(execution.duration / 1000)
      const m = Math.floor(s / 60)
      outputDuration.value = m > 0 ? `${m}m ${s % 60}s` : `${s}s`
    }
  }
}

function handleClearOutput(): void {
  outputLines.value = []
  outputExitCode.value = null
  outputDuration.value = ''
}

function handleCodeSave(): void {
  if (editingScript.value) {
    handleEditorSave({ ...editingScript.value, code: codeText.value })
  }
}

const activeScript = computed(() => editingScript.value)

const formatDate = (date: Date | undefined): string => {
  if (!date) return ''
  const now = new Date()
  const d = new Date(date)
  const diffMs = now.getTime() - d.getTime()
  const diffDays = Math.floor(diffMs / 86400000)
  if (diffDays === 0) return '今天'
  if (diffDays === 1) return '昨天'
  if (diffDays < 7) return `${diffDays}天前`
  if (diffDays < 30) return `${Math.floor(diffDays / 7)}周前`
  return d.toLocaleDateString()
}

const fileSize = (code: string): string => {
  const bytes = new Blob([code]).size
  if (bytes < 1024) return `${bytes}B`
  return `${(bytes / 1024).toFixed(1)}KB`
}

onMounted(() => {
  loadScripts()
  scriptRunnerStore.loadCategories()
  scriptRunnerStore.loadRuntimes()
  scriptHub.connect({
    onOutputReceived: handleOutputReceived,
    onExecutionUpdate: handleExecutionUpdate
  })
})

onUnmounted(() => {
  scriptHub.disconnect()
})
</script>

<template>
  <div class="script-runner">
    <!-- ===== LEFT: Script Library (260px) ===== -->
    <aside class="sidebar">
      <!-- Script Library Header -->
      <div class="sidebar-header">
        <h2 class="sidebar-title">脚本库</h2>
        <button class="btn-new" @click="handleCreate">
          <svg
            width="12"
            height="12"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2.5"
            stroke-linecap="round"
            stroke-linejoin="round"
          >
            <line x1="12" y1="5" x2="12" y2="19" /><line x1="5" y1="12" x2="19" y2="12" />
          </svg>
          新建
        </button>
      </div>

      <!-- Language Filter -->
      <div class="lang-filters">
        <button
          v-for="filter in langFilters"
          :key="filter"
          class="lang-pill"
          :data-active="activeLangFilter === filter"
          @click="activeLangFilter = filter"
        >
          {{ filter }}
        </button>
      </div>

      <!-- Script List -->
      <div class="script-list">
        <button
          v-for="script in filteredScripts"
          :key="script.id"
          class="script-item"
          :data-active="editingScript?.id === script.id"
          @click="handleSelect(script)"
        >
          <div class="script-item-info">
            <span class="script-item-name truncate">{{ script.name }}</span>
            <div class="script-item-meta">
              <span class="lang-badge" :class="langBadgeClass(script.language)">{{ langBadgeLabel(script.language) }}</span>
              <span class="script-item-size">{{ fileSize(script.code) }}</span>
            </div>
          </div>
          <span class="script-item-date">{{ formatDate(script.updatedAt) }}</span>
        </button>

        <div v-if="filteredScripts.length === 0 && !scriptRunnerStore.isLoading" class="script-list-empty">
          <p>暂无脚本</p>
        </div>
        <div v-if="scriptRunnerStore.isLoading" class="script-list-loading">
          <p>加载中...</p>
        </div>
      </div>
    </aside>

    <!-- ===== RIGHT: Editor & Output ===== -->
    <div class="main-panel">
      <!-- No Script Selected State -->
      <div v-if="!showEditor && !showConsole" class="welcome-panel">
        <div class="welcome-content">
          <svg
            width="48"
            height="48"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="1.5"
            stroke-linecap="round"
            stroke-linejoin="round"
            style="color: var(--el-text-color-secondary); opacity: 0.4;"
          >
            <polyline points="16 18 22 12 16 6" /><polyline points="8 6 2 12 8 18" />
          </svg>
          <h3>选择或创建一个脚本</h3>
          <p>从左侧列表选择一个脚本，或点击"新建"创建新的脚本</p>
          <button class="btn-new btn-new--large" @click="handleCreate">
            <svg
              width="14"
              height="14"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="2.5"
              stroke-linecap="round"
              stroke-linejoin="round"
            >
              <line x1="12" y1="5" x2="12" y2="19" /><line x1="5" y1="12" x2="19" y2="12" />
            </svg>
            新建脚本
          </button>
        </div>
      </div>

      <!-- Script Editor & Console -->
      <template v-if="showEditor || showConsole">
        <!-- Editor Toolbar -->
        <div class="editor-toolbar">
          <div class="toolbar-left">
            <span v-if="activeScript" class="toolbar-script-name">{{ activeScript.name }}</span>
            <span v-if="activeScript" class="lang-badge" :class="langBadgeClass(activeScript.language)">{{ langBadgeLabel(activeScript.language) }}</span>
          </div>
          <div class="toolbar-actions">
            <button class="btn-action btn-action--run" @click="activeScript && handleEditorRun(activeScript)">
              <svg
                width="14"
                height="14"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                stroke-width="2"
                stroke-linecap="round"
                stroke-linejoin="round"
              >
                <polygon points="5 3 19 12 5 21 5 3" />
              </svg>
              运行
            </button>
            <button class="btn-action btn-action--stop" :disabled="!showConsole" @click="handleConsoleCancel">
              <svg
                width="14"
                height="14"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                stroke-width="2"
                stroke-linecap="round"
                stroke-linejoin="round"
              >
                <rect x="6" y="4" width="4" height="16" /><rect x="14" y="4" width="4" height="16" />
              </svg>
              停止
            </button>
            <button class="btn-action btn-action--ghost">
              <svg
                width="14"
                height="14"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                stroke-width="2"
                stroke-linecap="round"
                stroke-linejoin="round"
              >
                <path d="M12 2l2.4 7.4H22l-6.2 4.5 2.4 7.4L12 16.8l-6.2 4.5 2.4-7.4L2 9.4h7.6z" />
              </svg>
              AI 生成
            </button>
            <button class="btn-action btn-action--ghost" @click="handleCodeSave">
              <svg
                width="14"
                height="14"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                stroke-width="2"
                stroke-linecap="round"
                stroke-linejoin="round"
              >
                <path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z" /><polyline points="17 21 17 13 7 13 7 21" /><polyline points="7 3 7 8 15 8" />
              </svg>
              保存
            </button>
          </div>
        </div>

        <!-- Code Editor Area -->
        <div class="editor-body">
          <div class="code-lines" aria-hidden="true">
            <span v-for="n in lineNumbers" :key="n">{{ n }}</span>
          </div>
          <textarea
            v-model="codeText"
            class="code-editor"
            spellcheck="false"
            placeholder="在此输入代码..."
          />
        </div>

        <!-- Output Panel -->
        <div class="output-panel">
          <div class="output-header">
            <span class="output-title">输出</span>
            <button class="btn-action btn-action--ghost btn-action--sm" @click="handleClearOutput">
              <svg
                width="12"
                height="12"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                stroke-width="2"
                stroke-linecap="round"
                stroke-linejoin="round"
              >
                <path d="M3 6h18" /><path d="M19 6v14c0 1-1 2-2 2H7c-1 0-2-1-2-2V6" /><path d="M8 6V4c0-1 1-2 2-2h4c1 0 2 1 2 2v2" />
              </svg>
              清除
            </button>
          </div>
          <div class="output-content">
            <pre class="output-text"><span
  v-for="(line, i) in outputLines"
  :key="i"
  class="output-line"
            >{{ line }}</span><span v-if="outputLines.length === 0 && showConsole" class="output-line output-placeholder">等待输出...</span><span v-if="outputLines.length === 0 && !showConsole" class="output-line output-placeholder">点击"运行"开始执行脚本</span></pre>
          </div>
          <div v-if="outputExitCode !== null || outputDuration" class="output-footer">
            <span v-if="outputDuration" class="footer-duration">耗时 {{ outputDuration }}</span>
            <span class="footer-exit-code" :class="{ 'exit-success': outputExitCode === 0, 'exit-error': outputExitCode !== null && outputExitCode !== 0 }">退出代码: {{ outputExitCode ?? '-' }}</span>
          </div>
        </div>
      </template>
    </div>

    <!-- Parameter Form Modal -->
    <div v-if="showParameterForm" class="modal-overlay" @click.self="handleParameterFormCancel">
      <div class="modal modal-medium">
        <div class="modal-header">
          <div class="flex items-center gap-2">
            <AppLogo :size="20" />
            <h3>配置参数</h3>
          </div>
          <button class="modal-close" aria-label="关闭" @click="handleParameterFormCancel">
            <svg
              width="16"
              height="16"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="2.5"
              stroke-linecap="round"
              stroke-linejoin="round"
            >
              <line x1="18" y1="6" x2="6" y2="18" /><line x1="6" y1="6" x2="18" y2="18" />
            </svg>
          </button>
        </div>
        <div class="modal-body">
          <ParameterForm
            v-if="executingScript"
            :parameters="executingScript.parameters"
            @submit="handleParameterFormSubmit"
            @cancel="handleParameterFormCancel"
          />
        </div>
      </div>
    </div>

    <!-- Execution Detail Modal -->
    <div v-if="showExecutionDetail" class="modal-overlay" @click.self="handleExecutionDetailClose">
      <div class="modal modal-large">
        <ExecutionDetail
          v-if="scriptRunnerStore.currentExecution"
          :execution="scriptRunnerStore.currentExecution"
          @close="handleExecutionDetailClose"
        />
      </div>
    </div>
  </div>
</template>

<style scoped>
/* ============================================
   脚本运行器 — 工坊锻造风格
   ============================================ */

.script-runner {
  display: flex;
  height: 100%;
  overflow: hidden;
  background: var(--el-bg-color);
  color: var(--el-text-color-primary);
  font-family: var(--font-family-base);
}

/* ===== LEFT SIDEBAR (260px) ===== */
.sidebar {
  width: 260px;
  min-width: 260px;
  display: flex;
  flex-direction: column;
  border-right: 1px solid var(--el-border-color);
  background: var(--el-bg-color-page);
}

.sidebar-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  border-bottom: 1px solid var(--el-border-color);
}

.sidebar-title {
  font-size: 0.8125rem;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin: 0;
}

.btn-new {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 2px 12px;
  font-size: 0.75rem;
  font-weight: 500;
  color: var(--el-color-primary);
  background: transparent;
  border: 1px solid var(--el-color-primary);
  border-radius: var(--el-border-radius-base, 0.75rem);
  cursor: pointer;
  transition: background-color 180ms ease, color 180ms ease;
}

.btn-new:hover {
  background: var(--primary-light);
  color: var(--el-color-primary-light-3);
  border-color: var(--el-color-primary-light-3);
}

.btn-new--large {
  padding: 8px 20px;
  font-size: 0.8125rem;
  gap: 8px;
}

/* Language Filter Pills */
.lang-filters {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px 12px;
  border-bottom: 1px solid var(--el-border-color);
  flex-wrap: nowrap;
  overflow-x: auto;
}

.lang-pill {
  padding: 4px 12px;
  font-size: 0.75rem;
  font-weight: 500;
  border-radius: var(--radius-pill, 999px);
  border: 1px solid transparent;
  background: transparent;
  color: var(--el-text-color-regular);
  cursor: pointer;
  white-space: nowrap;
  transition: background-color 180ms ease, color 180ms ease, border-color 180ms ease;
}

.lang-pill:hover {
  color: var(--el-text-color-primary);
  background: var(--el-fill-color);
}

.lang-pill[data-active="true"] {
  background: var(--el-color-primary);
  color: var(--el-color-white);
  border-color: var(--el-color-primary);
}

/* Script List */
.script-list {
  flex: 1;
  overflow-y: auto;
  padding: 8px 0;
}

.script-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px 16px;
  cursor: pointer;
  border: none;
  border-left: 3px solid transparent;
  border-radius: 0 var(--el-border-radius-base, 0.75rem) var(--el-border-radius-base, 0.75rem) 0;
  background: transparent;
  color: var(--el-text-color-primary);
  text-align: left;
  width: 100%;
  transition: background-color 180ms ease, border-color 180ms ease;
}

.script-item:hover {
  background: var(--el-fill-color);
}

.script-item[data-active="true"] {
  border-left-color: var(--el-color-primary);
  background: var(--el-fill-color);
}

.script-item-info {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.script-item-name {
  font-family: var(--font-family-mono);
  font-size: 0.8125rem;
  font-weight: 500;
  color: var(--el-text-color-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.script-item-meta {
  display: flex;
  align-items: center;
  gap: 8px;
}

.script-item-size {
  font-family: var(--font-family-mono);
  font-size: 0.75rem;
  color: var(--el-text-color-secondary);
}

.script-item-date {
  flex-shrink: 0;
  font-family: var(--font-family-mono);
  font-size: 0.75rem;
  color: var(--el-text-color-secondary);
}

.script-list-empty,
.script-list-loading {
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 32px 16px;
  color: var(--el-text-color-secondary);
  font-size: 0.8125rem;
}

.script-list-empty p,
.script-list-loading p {
  margin: 0;
}

/* Language Badge */
.lang-badge {
  font-size: 0.75rem;
  font-weight: 500;
  padding: 1px 8px;
  border-radius: var(--radius-pill, 999px);
  white-space: nowrap;
}

.lang-badge--ps {
  background: rgba(88, 166, 255, 0.15);
  color: #58A6FF;
}

.lang-badge--py {
  background: rgba(63, 185, 80, 0.15);
  color: #3FB950;
}

.lang-badge--js {
  background: rgba(245, 158, 11, 0.15);
  color: #F59E0B;
}

/* ===== RIGHT MAIN PANEL ===== */
.main-panel {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-width: 0;
  overflow: hidden;
}

/* Welcome Panel */
.welcome-panel {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
}

.welcome-content {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 12px;
  text-align: center;
  padding: 32px;
}

.welcome-content h3 {
  font-size: 1.125rem;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin: 0;
}

.welcome-content p {
  font-size: 0.8125rem;
  color: var(--el-text-color-regular);
  margin: 0 0 8px;
}

/* Editor Toolbar */
.editor-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 20px;
  border-bottom: 1px solid var(--el-border-color);
  background: var(--el-bg-color-page);
  flex-shrink: 0;
}

.toolbar-left {
  display: flex;
  align-items: center;
  gap: 12px;
}

.toolbar-script-name {
  font-family: var(--font-family-mono);
  font-size: 0.9375rem;
  font-weight: 600;
  color: var(--el-text-color-primary);
}

.toolbar-actions {
  display: flex;
  align-items: center;
  gap: 8px;
}

/* Action Buttons */
.btn-action {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 8px 16px;
  font-size: 0.8125rem;
  font-weight: 500;
  border-radius: var(--el-border-radius-base, 0.75rem);
  cursor: pointer;
  border: 1px solid var(--el-border-color);
  background: transparent;
  color: var(--el-text-color-regular);
  transition: background-color 180ms ease, color 180ms ease, border-color 180ms ease;
}

.btn-action:hover:not(:disabled) {
  background: var(--el-fill-color);
  color: var(--el-text-color-primary);
  border-color: var(--el-text-color-secondary);
}

.btn-action:disabled {
  opacity: 0.4;
  cursor: not-allowed;
}

.btn-action--sm {
  padding: 4px 12px;
}

.btn-action--run {
  color: var(--el-color-white);
  background: var(--el-color-primary);
  border-color: var(--el-color-primary);
}

.btn-action--run:hover:not(:disabled) {
  background: var(--el-color-primary-light-3);
  border-color: var(--el-color-primary-light-3);
}

.btn-action--stop:hover:not(:disabled) {
  background: rgba(248, 81, 73, 0.12);
  color: #F85149;
  border-color: #F85149;
}

/* Code Editor Area */
.editor-body {
  display: flex;
  flex: 1;
  overflow: auto;
  border-bottom: 1px solid var(--el-border-color);
  min-height: 0;
}

.code-lines {
  display: flex;
  flex-direction: column;
  font-family: var(--font-family-mono);
  font-size: 0.8125rem;
  line-height: 1.7;
  color: var(--el-text-color-secondary);
  text-align: right;
  padding: 16px 12px 16px 16px;
  user-select: none;
  border-right: 1px solid var(--el-border-color);
  min-width: 48px;
  background: var(--el-bg-color-page);
}

.code-lines span {
  display: block;
}

.code-editor {
  flex: 1;
  font-family: var(--font-family-mono);
  font-size: 0.8125rem;
  line-height: 1.7;
  color: var(--el-text-color-primary);
  background: var(--el-bg-color);
  padding: 16px;
  border: none;
  resize: none;
  outline: none;
  white-space: pre;
  overflow-wrap: normal;
  overflow-x: auto;
  tab-size: 4;
  min-height: 200px;
}

.code-editor::placeholder {
  color: var(--el-text-color-secondary);
  opacity: 0.5;
}

/* Output Panel */
.output-panel {
  display: flex;
  flex-direction: column;
  flex-shrink: 0;
  min-height: 160px;
  max-height: 300px;
  background: var(--el-bg-color-page);
}

.output-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 20px;
  border-bottom: 1px solid var(--el-border-color);
  flex-shrink: 0;
}

.output-title {
  font-size: 0.8125rem;
  font-weight: 600;
  color: var(--el-text-color-primary);
}

.output-content {
  flex: 1;
  overflow: auto;
  padding: 12px 16px;
}

.output-text {
  margin: 0;
  font-family: var(--font-family-mono);
  font-size: 0.8125rem;
  line-height: 1.7;
  color: var(--el-text-color-regular);
  white-space: pre-wrap;
  word-break: break-all;
}

.output-line {
  display: block;
}

.output-placeholder {
  color: var(--el-text-color-secondary);
  opacity: 0.6;
}

.output-line.success {
  color: var(--el-color-success);
}

.output-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 20px;
  border-top: 1px solid var(--el-border-color);
  background: var(--el-fill-color-light);
  flex-shrink: 0;
}

.footer-duration {
  font-family: var(--font-family-mono);
  font-size: 0.75rem;
  color: var(--el-text-color-secondary);
}

.footer-exit-code {
  font-family: var(--font-family-mono);
  font-size: 0.75rem;
  font-weight: 500;
  padding: 1px 8px;
  border-radius: var(--radius-pill, 999px);
  background: rgba(63, 185, 80, 0.15);
  color: var(--el-color-success);
}

.footer-exit-code.exit-error {
  background: rgba(248, 81, 73, 0.15);
  color: var(--el-color-danger);
}

/* ===== MODAL OVERLAY ===== */
.modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
  padding: 20px;
}

.modal {
  background: var(--el-bg-color);
  border: 1px solid var(--el-border-color);
  border-radius: 1rem;
  max-height: 90vh;
  overflow: hidden;
  display: flex;
  flex-direction: column;
  box-shadow: 0 10px 15px rgba(0, 0, 0, 0.1);
}

.modal-medium {
  width: 100%;
  max-width: 520px;
}

.modal-large {
  width: 100%;
  max-width: 700px;
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px 20px;
  border-bottom: 1px solid var(--el-border-color);
}

.modal-header h3 {
  font-size: 0.9375rem;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin: 0;
}

.modal-close {
  width: 32px;
  height: 32px;
  display: flex;
  align-items: center;
  justify-content: center;
  border: 1px solid var(--el-border-color);
  background: transparent;
  border-radius: var(--el-border-radius-base, 0.75rem);
  cursor: pointer;
  color: var(--el-text-color-regular);
  transition: background-color 180ms ease, color 180ms ease;
}

.modal-close:hover {
  background: var(--el-fill-color);
  color: var(--el-text-color-primary);
}

.modal-body {
  padding: 20px;
  overflow-y: auto;
}

/* ===== UTILITY ===== */
.truncate {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>