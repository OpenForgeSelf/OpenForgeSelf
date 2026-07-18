<script setup lang="ts">
import { ref } from 'vue'
import { useScriptRunnerStore } from '@/stores/scriptRunner'
import type { ScriptLanguage, GenerateScriptResponse, ScriptParameter } from '@/types/scriptRunner'

const emit = defineEmits<{
  (e: 'apply', code: string, parameters: ScriptParameter[], description: string): void
  (e: 'save-to-library', code: string, parameters: ScriptParameter[], description: string, language: ScriptLanguage): void
  (e: 'close'): void
}>()

const store = useScriptRunnerStore()

const description = ref('')
const requirements = ref('')
const language = ref<ScriptLanguage>('powershell')
const result = ref<GenerateScriptResponse | null>(null)
const showResult = ref(false)

const languageOptions: { value: ScriptLanguage; label: string; icon: string }[] = [
  { value: 'powershell', label: 'PowerShell', icon: '💠' },
  { value: 'python', label: 'Python', icon: '🐍' },
  { value: 'nodejs', label: 'Node.js', icon: '📦' },
  { value: 'shell', label: 'Shell', icon: '🐚' },
  { value: 'cmd', label: 'CMD', icon: '⚫' }
]

async function handleGenerate(): Promise<void> {
  if (!description.value.trim()) return

  try {
    result.value = await store.generateScript({
      language: language.value,
      description: description.value,
      requirements: requirements.value || undefined
    })
    showResult.value = true
  } catch {
    // ignore
  }
}

function handleApply(): void {
  if (result.value) {
    emit('apply', result.value.code, result.value.parameters, result.value.description)
  }
}

function handleSaveToLibrary(): void {
  if (result.value) {
    emit('save-to-library', result.value.code, result.value.parameters, result.value.description, result.value.language)
  }
}

function handleClose(): void {
  showResult.value = false
  result.value = null
  store.clearGeneratedScript()
  emit('close')
}
</script>

<template>
  <div class="ai-script-generator">
    <div class="generator-header">
      <h3>🤖 AI 脚本生成</h3>
      <button class="btn-close" @click="handleClose">×</button>
    </div>

    <div class="generator-body">
      <div class="form-group">
        <label>脚本语言</label>
        <select v-model="language">
          <option v-for="opt in languageOptions" :key="opt.value" :value="opt.value">
            {{ opt.icon }} {{ opt.label }}
          </option>
        </select>
      </div>

      <div class="form-group">
        <label>脚本描述 *</label>
        <textarea
          v-model="description"
          placeholder="请描述你想要的脚本功能，例如：批量重命名指定目录下的所有 jpg 文件，按序号命名"
          rows="4"
        />
      </div>

      <div class="form-group">
        <label>附加要求（可选）</label>
        <textarea
          v-model="requirements"
          placeholder="例如：需要支持递归子目录、添加错误处理、使用异步方式等"
          rows="3"
        />
      </div>

      <button
        class="btn btn-primary btn-generate"
        :disabled="!description.trim() || store.isGeneratingScript"
        @click="handleGenerate"
      >
        {{ store.isGeneratingScript ? '生成中...' : '✨ 生成脚本' }}
      </button>

      <div v-if="store.error" class="error-message">
        {{ store.error }}
      </div>

      <div v-if="showResult && result" class="result-section">
        <div class="result-header">
          <h4>生成结果</h4>
          <div class="result-actions">
            <button class="btn btn-secondary" @click="handleApply">应用到编辑器</button>
            <button class="btn btn-success" @click="handleSaveToLibrary">保存到脚本库</button>
          </div>
        </div>

        <div class="result-description">
          <strong>描述：</strong>{{ result.description }}
        </div>

        <div v-if="result.parameters.length > 0" class="result-parameters">
          <strong>参数：</strong>
          <ul>
            <li v-for="param in result.parameters" :key="param.name">
              <span class="param-name">{{ param.name }}</span>
              <span class="param-type">({{ param.type }})</span>
              <span v-if="param.description"> - {{ param.description }}</span>
              <span v-if="param.defaultValue !== undefined"> [默认: {{ param.defaultValue }}]</span>
            </li>
          </ul>
        </div>

        <div class="result-code">
          <strong>代码：</strong>
          <pre><code>{{ result.code }}</code></pre>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.ai-script-generator {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: var(--bg-secondary);
  border-radius: 8px;
  overflow: hidden;
}

.generator-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px;
  border-bottom: 1px solid var(--border-color);
  background: var(--bg-primary);
}

.generator-header h3 {
  margin: 0;
  font-size: 16px;
}

.btn-close {
  background: none;
  border: none;
  font-size: 20px;
  cursor: pointer;
  color: var(--text-secondary);
  padding: 4px 8px;
}

.btn-close:hover {
  color: var(--text-primary);
}

.generator-body {
  padding: 16px;
  flex: 1;
  overflow-y: auto;
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

.form-group select,
.form-group textarea {
  width: 100%;
  padding: 8px 12px;
  border: 1px solid var(--border-color);
  border-radius: 6px;
  background: var(--bg-primary);
  color: var(--text-primary);
  font-size: 14px;
  font-family: inherit;
  resize: vertical;
}

.form-group select:focus,
.form-group textarea:focus {
  outline: none;
  border-color: var(--primary-color);
}

.btn-generate {
  width: 100%;
  padding: 12px;
  font-size: 15px;
}

.btn-generate:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.error-message {
  margin-top: 12px;
  padding: 10px 12px;
  background: rgba(239, 68, 68, 0.1);
  border: 1px solid rgba(239, 68, 68, 0.3);
  border-radius: 6px;
  color: #ef4444;
  font-size: 13px;
}

.result-section {
  margin-top: 24px;
  padding-top: 20px;
  border-top: 1px solid var(--border-color);
}

.result-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}

.result-header h4 {
  margin: 0;
  font-size: 15px;
}

.result-actions {
  display: flex;
  gap: 8px;
}

.result-description,
.result-parameters {
  margin-bottom: 16px;
  font-size: 14px;
  line-height: 1.6;
}

.result-parameters ul {
  margin: 8px 0 0 0;
  padding-left: 20px;
}

.result-parameters li {
  margin-bottom: 4px;
}

.param-name {
  font-weight: 600;
  color: var(--primary-color);
}

.param-type {
  color: var(--text-secondary);
  font-size: 13px;
}

.result-code {
  margin-top: 16px;
}

.result-code pre {
  margin: 8px 0 0 0;
  padding: 12px;
  background: var(--bg-tertiary);
  border-radius: 6px;
  overflow-x: auto;
  font-size: 13px;
  line-height: 1.5;
}

.result-code code {
  font-family: 'Consolas', 'Monaco', monospace;
}
</style>
