<script setup lang="ts">
import { ref, computed } from 'vue'
import { useScriptRunnerStore } from '@/stores/scriptRunner'
import type { ScriptLanguage, SuggestScriptFixResponse } from '@/types/scriptRunner'

const props = defineProps<{
  code?: string
  language?: ScriptLanguage
}>()

const emit = defineEmits<{
  (e: 'apply-fix', fixedCode: string): void
  (e: 'close'): void
}>()

const store = useScriptRunnerStore()

const errorMessage = ref('')
const currentLanguage = ref<ScriptLanguage>(props.language || 'powershell')
const currentCode = ref(props.code || '')
const fixResult = ref<SuggestScriptFixResponse | null>(null)
const activeTab = ref<'analyze' | 'fix'>('fix')

const canAnalyze = computed(() => currentCode.value.trim() && errorMessage.value.trim())

async function handleAnalyzeError(): Promise<void> {
  if (!canAnalyze.value) return

  try {
    await store.analyzeScriptError({
      language: currentLanguage.value,
      code: currentCode.value,
      errorMessage: errorMessage.value
    })
  } catch {
    // ignore
  }
}

async function handleSuggestFix(): Promise<void> {
  if (!canAnalyze.value) return

  try {
    fixResult.value = await store.suggestScriptFix({
      language: currentLanguage.value,
      code: currentCode.value,
      errorMessage: errorMessage.value
    })
  } catch {
    // ignore
  }
}

function handleApplyFix(): void {
  if (fixResult.value) {
    emit('apply-fix', fixResult.value.fixedCode)
  }
}

function handleClose(): void {
  fixResult.value = null
  store.clearErrorAnalysis()
  store.clearScriptFix()
  emit('close')
}
</script>

<template>
  <div class="ai-script-debugger">
    <div class="debugger-header">
      <h3>🔍 AI 脚本调试</h3>
      <button class="btn-close" @click="handleClose">×</button>
    </div>

    <div class="debugger-body">
      <div class="tabs">
        <button
          class="tab-btn"
          :class="{ active: activeTab === 'analyze' }"
          @click="activeTab = 'analyze'"
        >
          错误分析
        </button>
        <button
          class="tab-btn"
          :class="{ active: activeTab === 'fix' }"
          @click="activeTab = 'fix'"
        >
          修复建议
        </button>
      </div>

      <div class="input-section">
        <div class="form-group">
          <label>错误信息 *</label>
          <textarea
            v-model="errorMessage"
            placeholder="请粘贴错误信息..."
            rows="4"
          />
        </div>

        <div v-if="activeTab === 'analyze'" class="action-section">
          <button
            class="btn btn-primary"
            :disabled="!canAnalyze || store.isAnalyzingError"
            @click="handleAnalyzeError"
          >
            {{ store.isAnalyzingError ? '分析中...' : '分析错误' }}
          </button>
        </div>

        <div v-if="activeTab === 'fix'" class="action-section">
          <button
            class="btn btn-primary"
            :disabled="!canAnalyze || store.isSuggestingFix"
            @click="handleSuggestFix"
          >
            {{ store.isSuggestingFix ? '生成中...' : '获取修复建议' }}
          </button>
        </div>
      </div>

      <div v-if="store.error" class="error-message">
        {{ store.error }}
      </div>

      <div v-if="activeTab === 'analyze' && store.errorAnalysis" class="result-section">
        <h4>错误分析</h4>
        <div class="analysis-content">
          <p><strong>分析：</strong>{{ store.errorAnalysis.errorAnalysis }}</p>
        </div>

        <div v-if="store.errorAnalysis.possibleCauses.length > 0" class="causes-section">
          <strong>可能的原因：</strong>
          <ul>
            <li v-for="(cause, index) in store.errorAnalysis.possibleCauses" :key="index">
              {{ cause }}
            </li>
          </ul>
        </div>

        <div class="suggestion-section">
          <strong>建议：</strong>
          <p>{{ store.errorAnalysis.suggestion }}</p>
        </div>
      </div>

      <div v-if="activeTab === 'fix' && fixResult" class="result-section">
        <h4>修复建议</h4>

        <div class="analysis-content">
          <p><strong>问题分析：</strong>{{ fixResult.errorAnalysis }}</p>
        </div>

        <div v-if="fixResult.changes.length > 0" class="changes-section">
          <strong>修改内容：</strong>
          <ul>
            <li v-for="(change, index) in fixResult.changes" :key="index">
              {{ change }}
            </li>
          </ul>
        </div>

        <div class="explanation-section">
          <strong>修改说明：</strong>
          <p>{{ fixResult.explanation }}</p>
        </div>

        <div class="fixed-code-section">
          <div class="section-header">
            <strong>修复后的代码：</strong>
            <button class="btn btn-success btn-sm" @click="handleApplyFix">
              应用修复
            </button>
          </div>
          <pre><code>{{ fixResult.fixedCode }}</code></pre>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.ai-script-debugger {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: var(--el-bg-color-page);
  border-radius: 8px;
  overflow: hidden;
}

.debugger-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px;
  border-bottom: 1px solid var(--el-border-color);
  background: var(--el-bg-color);
}

.debugger-header h3 {
  margin: 0;
  font-size: 16px;
}

.btn-close {
  background: none;
  border: none;
  font-size: 20px;
  cursor: pointer;
  color: var(--el-text-color-regular);
  padding: 4px 8px;
}

.btn-close:hover {
  color: var(--el-text-color-primary);
}

.debugger-body {
  padding: 16px;
  flex: 1;
  overflow-y: auto;
}

.tabs {
  display: flex;
  gap: 4px;
  margin-bottom: 16px;
  border-bottom: 1px solid var(--el-border-color);
}

.tab-btn {
  padding: 10px 16px;
  background: none;
  border: none;
  border-bottom: 2px solid transparent;
  color: var(--el-text-color-regular);
  cursor: pointer;
  font-size: 14px;
  margin-bottom: -1px;
}

.tab-btn:hover {
  color: var(--el-text-color-primary);
}

.tab-btn.active {
  color: var(--el-color-primary);
  border-bottom-color: var(--el-color-primary);
}

.input-section {
  margin-bottom: 16px;
}

.form-group {
  margin-bottom: 16px;
}

.form-group label {
  display: block;
  margin-bottom: 6px;
  font-weight: 500;
  font-size: 14px;
}

.form-group textarea {
  width: 100%;
  padding: 8px 12px;
  border: 1px solid var(--el-border-color);
  border-radius: 6px;
  background: var(--el-bg-color);
  color: var(--el-text-color-primary);
  font-size: 14px;
  font-family: inherit;
  resize: vertical;
}

.form-group textarea:focus {
  outline: none;
  border-color: var(--el-color-primary);
}

.action-section {
  margin-top: 12px;
}

.action-section .btn {
  width: 100%;
  padding: 10px;
}

.action-section .btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.error-message {
  margin-bottom: 16px;
  padding: 10px 12px;
  background: rgba(239, 68, 68, 0.1);
  border: 1px solid rgba(239, 68, 68, 0.3);
  border-radius: 6px;
  color: #ef4444;
  font-size: 13px;
}

.result-section {
  margin-top: 20px;
  padding-top: 20px;
  border-top: 1px solid var(--el-border-color);
}

.result-section h4 {
  margin: 0 0 16px 0;
  font-size: 15px;
}

.analysis-content,
.causes-section,
.suggestion-section,
.explanation-section {
  margin-bottom: 16px;
  font-size: 14px;
  line-height: 1.6;
}

.causes-section ul,
.changes-section ul {
  margin: 8px 0 0 0;
  padding-left: 20px;
}

.causes-section li,
.changes-section li {
  margin-bottom: 4px;
}

.changes-section {
  margin-bottom: 16px;
  font-size: 14px;
}

.fixed-code-section {
  margin-top: 16px;
}

.section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 8px;
}

.btn-sm {
  padding: 4px 12px;
  font-size: 12px;
}

.fixed-code-section pre {
  margin: 8px 0 0 0;
  padding: 12px;
  background: var(--el-fill-color-light);
  border-radius: 6px;
  overflow-x: auto;
  font-size: 13px;
  line-height: 1.5;
}

.fixed-code-section code {
  font-family: 'Consolas', 'Monaco', monospace;
}
</style>
