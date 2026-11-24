<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import type { WorkflowStep, WorkflowVariable, ScriptLanguage } from '@/types/workflow'
import ScriptSelector from './ScriptSelector.vue'

const props = defineProps<{
  step: WorkflowStep
  variables: WorkflowVariable[]
}>()

const emit = defineEmits<{
  (e: 'update', step: Partial<WorkflowStep>): void
}>()

const scriptMode = ref<'reference' | 'inline'>(props.step.scriptId ? 'reference' : 'inline')
const scriptId = ref(props.step.scriptId || '')
const scriptCode = ref(props.step.scriptCode || '')
const scriptLanguage = ref<ScriptLanguage>(props.step.scriptLanguage || 'powershell')
const outputVariable = ref(props.step.outputVariable || '')
const workingDirectory = ref(props.step.workingDirectory || '')
const timeoutSeconds = ref(props.step.timeoutSeconds || 300)
const successExitCodes = ref(props.step.successExitCodes?.join(', ') || '0')
const parameterMappings = ref<Array<{ paramName: string; variableExpr: string }>>(
  Object.entries(props.step.parameterMappings || {}).map(([paramName, variableExpr]) => ({
    paramName,
    variableExpr
  }))
)

const languages: { value: ScriptLanguage; label: string }[] = [
  { value: 'powershell', label: 'PowerShell' },
  { value: 'python', label: 'Python' },
  { value: 'nodejs', label: 'Node.js' },
  { value: 'shell', label: 'Shell' },
  { value: 'cmd', label: 'CMD' }
]

const variableSuggestions = computed(() => {
  return props.variables.map(v => `{{var.${v.name}}}`)
})

function addParameterMapping(): void {
  parameterMappings.value.push({ paramName: '', variableExpr: '' })
}

function removeParameterMapping(index: number): void {
  parameterMappings.value.splice(index, 1)
}

function applyChanges(): void {
  const mappings: Record<string, string> = {}
  parameterMappings.value.forEach(m => {
    if (m.paramName.trim()) {
      mappings[m.paramName.trim()] = m.variableExpr
    }
  })

  const exitCodes = successExitCodes.value
    .split(',')
    .map(c => parseInt(c.trim()))
    .filter(c => !isNaN(c))

  const update: Partial<WorkflowStep> = {
    scriptId: scriptMode.value === 'reference' && scriptId.value ? scriptId.value : undefined,
    scriptCode: scriptMode.value === 'inline' ? scriptCode.value : undefined,
    scriptLanguage: scriptMode.value === 'inline' ? scriptLanguage.value : undefined,
    parameterMappings: Object.keys(mappings).length > 0 ? mappings : undefined,
    outputVariable: outputVariable.value || undefined,
    workingDirectory: workingDirectory.value || undefined,
    timeoutSeconds: timeoutSeconds.value || undefined,
    successExitCodes: exitCodes.length > 0 ? exitCodes : undefined
  }

  emit('update', update)
}

watch([scriptMode, scriptId, scriptCode, scriptLanguage, outputVariable, workingDirectory, timeoutSeconds, successExitCodes, parameterMappings], () => {
  applyChanges()
}, { deep: true })

watch(() => props.step, (newStep) => {
  scriptMode.value = newStep.scriptId ? 'reference' : 'inline'
  scriptId.value = newStep.scriptId || ''
  scriptCode.value = newStep.scriptCode || ''
  scriptLanguage.value = newStep.scriptLanguage || 'powershell'
  outputVariable.value = newStep.outputVariable || ''
  workingDirectory.value = newStep.workingDirectory || ''
  timeoutSeconds.value = newStep.timeoutSeconds || 300
  successExitCodes.value = newStep.successExitCodes?.join(', ') || '0'
  parameterMappings.value = Object.entries(newStep.parameterMappings || {}).map(([paramName, variableExpr]) => ({
    paramName,
    variableExpr
  }))
}, { deep: true })
</script>

<template>
  <div class="script-step-editor">
    <div class="editor-section">
      <div class="section-title">脚本模式</div>
      <div class="mode-switch">
        <button
          class="mode-btn"
          :class="{ active: scriptMode === 'reference' }"
          @click="scriptMode = 'reference'"
        >
          引用脚本库
        </button>
        <button
          class="mode-btn"
          :class="{ active: scriptMode === 'inline' }"
          @click="scriptMode = 'inline'"
        >
          嵌入代码
        </button>
      </div>
    </div>

    <div v-if="scriptMode === 'reference'" class="editor-section">
      <div class="section-title">选择脚本</div>
      <ScriptSelector v-model="scriptId" />
    </div>

    <div v-if="scriptMode === 'inline'" class="editor-section">
      <div class="form-group">
        <label>脚本语言</label>
        <select v-model="scriptLanguage">
          <option v-for="lang in languages" :key="lang.value" :value="lang.value">
            {{ lang.label }}
          </option>
        </select>
      </div>
      <div class="form-group">
        <label>脚本代码</label>
        <textarea
          v-model="scriptCode"
          rows="8"
          placeholder="输入脚本代码..."
          class="code-editor"
        />
      </div>
    </div>

    <div class="editor-section">
      <div class="section-title">
        参数映射
        <span class="section-hint">工作流变量 → 脚本参数</span>
      </div>
      <div class="param-mappings">
        <div
          v-for="(mapping, index) in parameterMappings"
          :key="index"
          class="param-mapping-row"
        >
          <input
            v-model="mapping.paramName"
            type="text"
            placeholder="参数名"
            class="param-name"
          />
          <span class="mapping-arrow">→</span>
          <input
            v-model="mapping.variableExpr"
            type="text"
            placeholder="变量表达式 (如 {{var.name}})"
            class="variable-expr"
          />
          <button class="remove-btn" title="删除" @click="removeParameterMapping(index)">
            ✕
          </button>
        </div>
        <button class="add-param-btn" @click="addParameterMapping">
          + 添加参数映射
        </button>
      </div>
      <div v-if="variableSuggestions.length > 0" class="variable-suggestions">
        <span class="hint-label">可用变量:</span>
        <span
          v-for="v in variableSuggestions"
          :key="v"
          class="var-chip"
          @click="parameterMappings.push({ paramName: '', variableExpr: v })"
        >
          {{ v }}
        </span>
      </div>
    </div>

    <div class="editor-section">
      <div class="section-title">输出设置</div>
      <div class="form-group">
        <label>输出变量名</label>
        <input
          v-model="outputVariable"
          type="text"
          placeholder="将脚本输出保存到哪个工作流变量"
        />
      </div>
    </div>

    <div class="editor-section">
      <div class="section-title">高级设置</div>
      <div class="form-group">
        <label>工作目录</label>
        <input
          v-model="workingDirectory"
          type="text"
          placeholder="脚本执行的工作目录（可选）"
        />
      </div>
      <div class="form-group">
        <label>超时时间（秒）</label>
        <input
          v-model.number="timeoutSeconds"
          type="number"
          min="1"
          placeholder="300"
        />
      </div>
      <div class="form-group">
        <label>成功退出码</label>
        <input
          v-model="successExitCodes"
          type="text"
          placeholder="0"
        />
        <span class="field-hint">多个退出码用逗号分隔，如: 0,1</span>
      </div>
    </div>
  </div>
</template>

<style scoped>
.script-step-editor {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.editor-section {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.section-title {
  font-size: 13px;
  font-weight: 600;
  color: var(--el-text-color-regular);
  display: flex;
  align-items: center;
  gap: 8px;
}

.section-hint {
  font-size: 11px;
  font-weight: 400;
  color: var(--el-text-color-placeholder);
}

.mode-switch {
  display: flex;
  gap: 4px;
  background: var(--el-fill-color-light);
  padding: 4px;
  border-radius: 8px;
}

.mode-btn {
  flex: 1;
  padding: 8px 12px;
  border: none;
  background: transparent;
  border-radius: 6px;
  font-size: 13px;
  color: var(--el-text-color-secondary);
  cursor: pointer;
  transition: all 0.2s ease;
}

.mode-btn:hover {
  color: var(--el-text-color-regular);
}

.mode-btn.active {
  background: var(--el-bg-color);
  color: var(--el-color-primary);
  font-weight: 500;
  box-shadow: var(--el-box-shadow-lighter);
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.form-group label {
  font-size: 12px;
  font-weight: 500;
  color: var(--el-text-color-regular);
}

.form-group input,
.form-group select,
.form-group textarea {
  padding: 8px 10px;
  border: 1px solid var(--el-border-color-light);
  border-radius: 6px;
  font-size: 13px;
  color: var(--el-text-color-primary);
  font-family: inherit;
}

.form-group input:focus,
.form-group select:focus,
.form-group textarea:focus {
  outline: none;
  border-color: var(--el-color-primary);
  box-shadow: 0 0 0 3px var(--gold-tint-strong);
}

.code-editor {
  font-family: 'Consolas', 'Monaco', 'Courier New', monospace;
  font-size: 12px;
  resize: vertical;
  min-height: 120px;
}

.field-hint {
  font-size: 11px;
  color: var(--el-text-color-placeholder);
}

.param-mappings {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.param-mapping-row {
  display: flex;
  align-items: center;
  gap: 6px;
}

.param-name {
  flex: 1;
  padding: 6px 8px;
  border: 1px solid var(--el-border-color-light);
  border-radius: 6px;
  font-size: 12px;
}

.mapping-arrow {
  color: var(--el-text-color-placeholder);
  font-size: 12px;
  flex-shrink: 0;
}

.variable-expr {
  flex: 1.5;
  padding: 6px 8px;
  border: 1px solid var(--el-border-color-light);
  border-radius: 6px;
  font-size: 12px;
  font-family: 'Consolas', 'Monaco', 'Courier New', monospace;
}

.remove-btn {
  width: 24px;
  height: 24px;
  border: 1px solid var(--el-border-color-light);
  background: var(--el-bg-color);
  border-radius: 4px;
  font-size: 10px;
  color: var(--el-color-error);
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.remove-btn:hover {
  background: var(--el-color-error-light-9);
  border-color: var(--el-color-error);
}

.add-param-btn {
  padding: 6px 12px;
  border: 1px dashed var(--el-border-color-light);
  background: var(--el-fill-color-light);
  border-radius: 6px;
  font-size: 12px;
  color: var(--el-text-color-secondary);
  cursor: pointer;
  text-align: left;
}

.add-param-btn:hover {
  border-color: var(--el-color-primary);
  color: var(--el-color-primary);
  background: var(--el-color-info-light-9);
}

.variable-suggestions {
  margin-top: 8px;
  padding: 8px;
  background: var(--el-fill-color-light);
  border-radius: 6px;
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  align-items: center;
}

.hint-label {
  font-size: 11px;
  color: var(--el-text-color-secondary);
  margin-right: 4px;
}

.var-chip {
  font-size: 11px;
  padding: 2px 6px;
  background: var(--el-color-info-light-9);
  color: var(--el-color-primary);
  border-radius: 4px;
  cursor: pointer;
  font-family: 'Consolas', 'Monaco', 'Courier New', monospace;
}

.var-chip:hover {
  background: var(--el-color-info-light-8);
}
</style>
