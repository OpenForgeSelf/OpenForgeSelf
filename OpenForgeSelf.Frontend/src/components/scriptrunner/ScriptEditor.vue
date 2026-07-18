<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import type { Script, ScriptLanguage, ScriptParameter } from '@/types/scriptRunner'
import ParameterEditor from './ParameterEditor.vue'
import AIScriptGenerator from './AIScriptGenerator.vue'
import AIScriptDebugger from './AIScriptDebugger.vue'
import ScriptTemplateSelector from './ScriptTemplateSelector.vue'

const props = defineProps<{
  script?: Script | null
}>()

const emit = defineEmits<{
  (e: 'save', script: Partial<Script>): void
  (e: 'cancel'): void
  (e: 'run', script: Script): void
}>()

const name = ref('')
const description = ref('')
const category = ref('')
const tagsInput = ref('')
const language = ref<ScriptLanguage>('powershell')
const code = ref('')
const timeout = ref(30)
const parameters = ref<ScriptParameter[]>([])
const activeTab = ref<'code' | 'parameters' | 'settings'>('code')
const aiPanel = ref<'generator' | 'debugger' | 'templates' | null>(null)

const languageOptions: { value: ScriptLanguage; label: string; icon: string }[] = [
  { value: 'powershell', label: 'PowerShell', icon: '💠' },
  { value: 'python', label: 'Python', icon: '🐍' },
  { value: 'nodejs', label: 'Node.js', icon: '📦' },
  { value: 'shell', label: 'Shell', icon: '🐚' },
  { value: 'cmd', label: 'CMD', icon: '⚫' }
]

const isEditing = computed(() => !!props.script)

watch(() => props.script, (script) => {
  if (script) {
    name.value = script.name
    description.value = script.description || ''
    category.value = script.category || ''
    tagsInput.value = script.tags.join(', ')
    language.value = script.language
    code.value = script.code
    timeout.value = script.timeout
    parameters.value = [...script.parameters]
  } else {
    resetForm()
  }
}, { immediate: true })

function resetForm(): void {
  name.value = ''
  description.value = ''
  category.value = ''
  tagsInput.value = ''
  language.value = 'powershell'
  code.value = getDefaultCode('powershell')
  timeout.value = 30
  parameters.value = []
}

function getDefaultCode(lang: ScriptLanguage): string {
  switch (lang) {
    case 'powershell':
      return '# PowerShell Script\nWrite-Host "Hello, World!"\n'
    case 'python':
      return '# Python Script\nprint("Hello, World!")\n'
    case 'nodejs':
      return '// Node.js Script\nconsole.log("Hello, World!");\n'
    case 'shell':
      return '#!/bin/bash\n# Shell Script\necho "Hello, World!"\n'
    case 'cmd':
      return '@echo off\nREM CMD Script\necho Hello, World!\n'
    default:
      return ''
  }
}

function handleLanguageChange(): void {
  if (!isEditing.value) {
    code.value = getDefaultCode(language.value)
  }
}

function handleSave(): void {
  const tags = tagsInput.value
    .split(',')
    .map(t => t.trim())
    .filter(t => t.length > 0)

  const scriptData: Partial<Script> = {
    name: name.value,
    description: description.value,
    category: category.value,
    tags,
    language: language.value,
    code: code.value,
    timeout: timeout.value,
    parameters: parameters.value
  }
  emit('save', scriptData)
}

function handleCancel(): void {
  emit('cancel')
}

function handleRun(): void {
  if (props.script) {
    emit('run', props.script)
  }
}

function handleParametersUpdate(params: ScriptParameter[]): void {
  parameters.value = params
}

function openAIGenerator(): void {
  aiPanel.value = 'generator'
}

function openAIDebugger(): void {
  aiPanel.value = 'debugger'
}

function openTemplates(): void {
  aiPanel.value = 'templates'
}

function closeAIPanel(): void {
  aiPanel.value = null
}

function handleApplyGeneratedCode(newCode: string, params: ScriptParameter[], desc: string): void {
  code.value = newCode
  parameters.value = params
  if (!description.value) {
    description.value = desc
  }
  aiPanel.value = null
}

function handleSaveToLibrary(newCode: string, params: ScriptParameter[], desc: string, lang: ScriptLanguage): void {
  code.value = newCode
  parameters.value = params
  language.value = lang
  if (!description.value) {
    description.value = desc
  }
  if (!name.value) {
    name.value = desc.slice(0, 50)
  }
  aiPanel.value = null
}

function handleApplyFix(fixedCode: string): void {
  code.value = fixedCode
  aiPanel.value = null
}

function handleUseTemplate(
  templateCode: string,
  templateParams: ScriptParameter[],
  templateName: string,
  templateDesc: string,
  templateLang: ScriptLanguage
): void {
  code.value = templateCode
  parameters.value = templateParams
  language.value = templateLang
  if (!name.value) {
    name.value = templateName
  }
  if (!description.value) {
    description.value = templateDesc
  }
  aiPanel.value = null
}
</script>

<template>
  <div class="script-editor">
    <div class="editor-header">
      <h2>{{ isEditing ? '编辑脚本' : '创建脚本' }}</h2>
      <div class="header-actions">
        <button class="btn btn-secondary" @click="handleCancel">取消</button>
        <button
          v-if="isEditing"
          class="btn btn-success"
          @click="handleRun"
        >
          ▶ 运行
        </button>
        <button class="btn btn-primary" :disabled="!name.trim()" @click="handleSave">
          {{ isEditing ? '保存' : '创建' }}
        </button>
      </div>
    </div>

    <div class="editor-body">
      <div class="basic-info">
        <div class="form-group">
          <label>脚本名称 *</label>
          <input v-model="name" type="text" placeholder="输入脚本名称" />
        </div>
        <div class="form-group">
          <label>语言</label>
          <select v-model="language" @change="handleLanguageChange">
            <option v-for="opt in languageOptions" :key="opt.value" :value="opt.value">
              {{ opt.icon }} {{ opt.label }}
            </option>
          </select>
        </div>
        <div class="form-group">
          <label>分类</label>
          <input v-model="category" type="text" placeholder="输入分类名称" />
        </div>
        <div class="form-group">
          <label>超时时间（秒）</label>
          <input v-model.number="timeout" type="number" min="1" max="3600" />
        </div>
        <div class="form-group full-width">
          <label>描述</label>
          <textarea v-model="description" placeholder="输入脚本描述" rows="2" />
        </div>
        <div class="form-group full-width">
          <label>标签（逗号分隔）</label>
          <input v-model="tagsInput" type="text" placeholder="tag1, tag2, tag3" />
        </div>
      </div>

      <div class="editor-tabs">
        <button
          class="tab-btn"
          :class="{ active: activeTab === 'code' }"
          @click="activeTab = 'code'"
        >
          📝 代码
        </button>
        <button
          class="tab-btn"
          :class="{ active: activeTab === 'parameters' }"
          @click="activeTab = 'parameters'"
        >
          ⚙️ 参数 ({{ parameters.length }})
        </button>
        <button
          class="tab-btn"
          :class="{ active: activeTab === 'settings' }"
          @click="activeTab = 'settings'"
        >
          🔧 设置
        </button>
      </div>

      <div v-if="activeTab === 'code'" class="code-section">
        <div class="code-toolbar">
          <div class="toolbar-left">
            <button class="toolbar-btn" :class="{ active: aiPanel === 'generator' }" @click="openAIGenerator">
              ✨ AI 生成
            </button>
            <button class="toolbar-btn" :class="{ active: aiPanel === 'debugger' }" @click="openAIDebugger">
              🔍 AI 调试
            </button>
            <button class="toolbar-btn" :class="{ active: aiPanel === 'templates' }" @click="openTemplates">
              📚 模板库
            </button>
          </div>
        </div>
        <div class="code-content">
          <div class="code-editor-wrapper">
            <textarea
              v-model="code"
              class="code-editor"
              spellcheck="false"
              :placeholder="`在此输入 ${language} 代码...`"
            />
          </div>
          <div v-if="aiPanel" class="ai-side-panel">
            <AIScriptGenerator
              v-if="aiPanel === 'generator'"
              @apply="handleApplyGeneratedCode"
              @save-to-library="handleSaveToLibrary"
              @close="closeAIPanel"
            />
            <AIScriptDebugger
              v-if="aiPanel === 'debugger'"
              :code="code"
              :language="language"
              @apply-fix="handleApplyFix"
              @close="closeAIPanel"
            />
            <ScriptTemplateSelector
              v-if="aiPanel === 'templates'"
              @use-template="handleUseTemplate"
              @close="closeAIPanel"
            />
          </div>
        </div>
      </div>

      <div v-if="activeTab === 'parameters'" class="parameters-section">
        <ParameterEditor
          :parameters="parameters"
          @update:parameters="handleParametersUpdate"
        />
      </div>

      <div v-if="activeTab === 'settings'" class="settings-section">
        <div class="settings-info">
          <h4>脚本设置</h4>
          <p class="settings-hint">在这里配置脚本的高级选项。</p>
        </div>
        <div class="settings-form">
          <div class="form-group">
            <label>超时时间</label>
            <input v-model.number="timeout" type="number" min="1" max="3600" />
            <span class="hint-text">脚本执行的最长时间，单位：秒</span>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.script-editor {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: #fff;
  border-radius: 12px;
  overflow: hidden;
}

.editor-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px 20px;
  border-bottom: 1px solid #e9ecef;
}

.editor-header h2 {
  font-size: 18px;
  font-weight: 600;
  color: #212529;
  margin: 0;
}

.header-actions {
  display: flex;
  gap: 8px;
}

.btn {
  padding: 8px 16px;
  border-radius: 8px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s ease;
  border: 1px solid transparent;
}

.btn-primary {
  background: #1976d2;
  color: #fff;
  border-color: #1976d2;
}

.btn-primary:hover:not(:disabled) {
  background: #1565c0;
  border-color: #1565c0;
}

.btn-primary:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.btn-secondary {
  background: #f8f9fa;
  color: #495057;
  border-color: #dee2e6;
}

.btn-secondary:hover {
  background: #e9ecef;
}

.btn-success {
  background: #28a745;
  color: #fff;
  border-color: #28a745;
}

.btn-success:hover {
  background: #218838;
  border-color: #218838;
}

.editor-body {
  flex: 1;
  overflow-y: auto;
  padding: 20px;
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.basic-info {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 16px;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.form-group.full-width {
  grid-column: 1 / -1;
}

.form-group label {
  font-size: 13px;
  font-weight: 500;
  color: #495057;
}

.form-group input,
.form-group textarea,
.form-group select {
  padding: 8px 12px;
  border: 1px solid #dee2e6;
  border-radius: 8px;
  font-size: 14px;
  color: #212529;
  transition: border-color 0.2s ease;
  font-family: inherit;
}

.form-group input:focus,
.form-group textarea:focus,
.form-group select:focus {
  outline: none;
  border-color: #1976d2;
  box-shadow: 0 0 0 3px rgba(25, 118, 210, 0.1);
}

.hint-text {
  font-size: 12px;
  color: #6c757d;
}

.editor-tabs {
  display: flex;
  gap: 4px;
  border-bottom: 2px solid #e9ecef;
}

.tab-btn {
  padding: 10px 16px;
  background: none;
  border: none;
  font-size: 14px;
  font-weight: 500;
  color: #6c757d;
  cursor: pointer;
  border-bottom: 2px solid transparent;
  margin-bottom: -2px;
  transition: all 0.2s ease;
}

.tab-btn:hover {
  color: #495057;
}

.tab-btn.active {
  color: #1976d2;
  border-bottom-color: #1976d2;
}

.code-section {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-height: 300px;
}

.code-toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 12px;
  background: #f8f9fa;
  border: 1px solid #dee2e6;
  border-bottom: none;
  border-radius: 8px 8px 0 0;
}

.toolbar-left {
  display: flex;
  gap: 8px;
}

.toolbar-btn {
  padding: 6px 12px;
  background: #fff;
  border: 1px solid #dee2e6;
  border-radius: 6px;
  font-size: 13px;
  color: #495057;
  cursor: pointer;
  transition: all 0.2s ease;
}

.toolbar-btn:hover {
  background: #e9ecef;
  border-color: #ced4da;
}

.toolbar-btn.active {
  background: #1976d2;
  color: #fff;
  border-color: #1976d2;
}

.code-content {
  display: flex;
  flex: 1;
  gap: 0;
}

.code-content .code-editor-wrapper {
  flex: 1;
  border-radius: 0;
}

.code-content > .code-editor-wrapper {
  border-radius: 0 0 0 8px;
}

.ai-side-panel {
  width: 380px;
  border: 1px solid #dee2e6;
  border-left: none;
  border-radius: 0 0 8px 0;
  overflow: hidden;
  flex-shrink: 0;
}

.code-editor-wrapper {
  flex: 1;
  border: 1px solid #dee2e6;
  border-radius: 8px;
  overflow: hidden;
  background: #1e1e1e;
}

.code-editor {
  width: 100%;
  height: 100%;
  min-height: 300px;
  padding: 12px;
  border: none;
  background: #1e1e1e;
  color: #d4d4d4;
  font-family: 'Consolas', 'Monaco', 'Courier New', monospace;
  font-size: 14px;
  line-height: 1.6;
  resize: vertical;
  box-sizing: border-box;
}

.code-editor:focus {
  outline: none;
}

.parameters-section {
  flex: 1;
}

.settings-section {
  flex: 1;
}

.settings-info {
  margin-bottom: 16px;
}

.settings-info h4 {
  font-size: 15px;
  font-weight: 600;
  color: #212529;
  margin: 0 0 4px 0;
}

.settings-hint {
  font-size: 13px;
  color: #6c757d;
  margin: 0;
}

.settings-form {
  max-width: 400px;
}

@media (max-width: 768px) {
  .basic-info {
    grid-template-columns: 1fr;
  }

  .editor-header {
    flex-direction: column;
    gap: 12px;
    align-items: flex-start;
  }

  .header-actions {
    width: 100%;
  }

  .header-actions .btn {
    flex: 1;
  }

  .code-editor {
    min-height: 200px;
  }
}
</style>
