<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import AppLogo from '@/components/AppLogo.vue'
import type { WorkflowDefinition, WorkflowStep, WorkflowVariable, WorkflowStepType } from '@/types/workflow'
import ScriptStepEditor from './ScriptStepEditor.vue'

const props = defineProps<{
  workflow?: WorkflowDefinition | null
}>()

const emit = defineEmits<{
  (e: 'save', workflow: Partial<WorkflowDefinition>): void
  (e: 'cancel'): void
}>()

const name = ref('')
const description = ref('')
const category = ref('')
const icon = ref('⚙️')
const steps = ref<WorkflowStep[]>([])
const variables = ref<WorkflowVariable[]>([])
const selectedStepIndex = ref(-1)
const showAddStep = ref(false)
const activeTab = ref<'steps' | 'variables'>('steps')

const stepTypes: { value: WorkflowStepType; label: string; icon: string }[] = [
  { value: 'tool_call', label: '工具调用', icon: '🔧' },
  { value: 'condition', label: '条件判断', icon: '🔀' },
  { value: 'loop', label: '循环', icon: '🔄' },
  { value: 'parallel', label: '并行', icon: '⚡' },
  { value: 'wait', label: '等待', icon: '⏱️' },
  { value: 'http', label: 'HTTP请求', icon: '🌐' },
  { value: 'script', label: '脚本执行', icon: '📜' }
]

const selectedStep = computed(() => {
  if (selectedStepIndex.value >= 0 && selectedStepIndex.value < steps.value.length) {
    return steps.value[selectedStepIndex.value]
  }
  return null
})

watch(() => props.workflow, (workflow) => {
  if (workflow) {
    name.value = workflow.name
    description.value = workflow.description || ''
    category.value = workflow.category || ''
    icon.value = workflow.icon || '⚙️'
    steps.value = [...workflow.steps]
    variables.value = [...workflow.variables]
  } else {
    resetForm()
  }
}, { immediate: true })

function resetForm(): void {
  name.value = ''
  description.value = ''
  category.value = ''
  icon.value = '⚙️'
  steps.value = []
  variables.value = []
  selectedStepIndex.value = -1
  showAddStep.value = false
}

function generateId(): string {
  return `${Date.now()}-${Math.random().toString(36).substring(2, 9)}`
}

function addStep(type: WorkflowStepType): void {
  const typeInfo = stepTypes.find(t => t.value === type)
  const newStep: WorkflowStep = {
    id: generateId(),
    name: typeInfo?.label || '新步骤',
    type,
    description: '',
    config: {},
    position: steps.value.length,
    timeout: 0,
    retryCount: 0,
    retryDelay: 0,
    continueOnError: false
  }
  steps.value.push(newStep)
  selectedStepIndex.value = steps.value.length - 1
  showAddStep.value = false
}

function removeStep(index: number): void {
  steps.value.splice(index, 1)
  if (selectedStepIndex.value >= steps.value.length) {
    selectedStepIndex.value = steps.value.length - 1
  } else if (selectedStepIndex.value === index) {
    selectedStepIndex.value = -1
  }
  updateStepPositions()
}

function moveStep(index: number, direction: 'up' | 'down'): void {
  const newIndex = direction === 'up' ? index - 1 : index + 1
  if (newIndex < 0 || newIndex >= steps.value.length) return

  const [step] = steps.value.splice(index, 1)
  steps.value.splice(newIndex, 0, step)
  selectedStepIndex.value = newIndex
  updateStepPositions()
}

function updateStepPositions(): void {
  steps.value.forEach((step, index) => {
    step.position = index
  })
}

function selectStep(index: number): void {
  selectedStepIndex.value = index
}

function addVariable(): void {
  variables.value.push({
    id: generateId(),
    name: '',
    type: 'string',
    defaultValue: '',
    description: '',
    required: false
  })
}

function removeVariable(index: number): void {
  variables.value.splice(index, 1)
}

function handleSave(): void {
  const workflowData: Partial<WorkflowDefinition> = {
    name: name.value,
    description: description.value,
    category: category.value,
    icon: icon.value,
    steps: steps.value,
    variables: variables.value,
    status: 'draft'
  }
  emit('save', workflowData)
}

function handleCancel(): void {
  emit('cancel')
}

function getStepTypeLabel(type: WorkflowStepType): string {
  return stepTypes.find(t => t.value === type)?.label || type
}

function getStepTypeIcon(type: WorkflowStepType): string {
  return stepTypes.find(t => t.value === type)?.icon || '📋'
}
</script>

<template>
  <div class="workflow-editor">
    <div class="editor-header">
      <div class="flex items-center gap-2">
        <AppLogo :size="20" />
        <h2>{{ workflow ? '编辑工作流' : '创建工作流' }}</h2>
      </div>
      <div class="header-actions">
        <button class="btn btn-secondary" @click="handleCancel">取消</button>
        <button class="btn btn-primary" :disabled="!name.trim()" @click="handleSave">
          {{ workflow ? '保存' : '创建' }}
        </button>
      </div>
    </div>

    <div class="editor-body">
      <div class="basic-info">
        <div class="form-group">
          <label>名称</label>
          <input v-model="name" type="text" placeholder="输入工作流名称" />
        </div>
        <div class="form-group">
          <label>图标</label>
          <input v-model="icon" type="text" placeholder="输入emoji或图标" maxlength="4" />
        </div>
        <div class="form-group">
          <label>分类</label>
          <input v-model="category" type="text" placeholder="输入分类名称" />
        </div>
        <div class="form-group full-width">
          <label>描述</label>
          <textarea v-model="description" placeholder="输入工作流描述" rows="2" />
        </div>
      </div>

      <div class="editor-tabs">
        <button
          class="tab-btn"
          :class="{ active: activeTab === 'steps' }"
          @click="activeTab = 'steps'"
        >
          步骤 ({{ steps.length }})
        </button>
        <button
          class="tab-btn"
          :class="{ active: activeTab === 'variables' }"
          @click="activeTab = 'variables'"
        >
          变量 ({{ variables.length }})
        </button>
      </div>

      <div v-if="activeTab === 'steps'" class="steps-section">
        <div class="steps-list">
          <div v-if="steps.length === 0" class="empty-steps">
            <p>暂无步骤，点击下方按钮添加</p>
          </div>
          <div
            v-for="(step, index) in steps"
            :key="step.id"
            class="step-item"
            :class="{ selected: selectedStepIndex === index }"
            @click="selectStep(index)"
          >
            <div class="step-number">{{ index + 1 }}</div>
            <div class="step-icon">{{ getStepTypeIcon(step.type) }}</div>
            <div class="step-info">
              <div class="step-name">{{ step.name }}</div>
              <div class="step-type">{{ getStepTypeLabel(step.type) }}</div>
            </div>
            <div class="step-actions">
              <button
                class="icon-btn"
                :disabled="index === 0"
                aria-label="上移"
                @click.stop="moveStep(index, 'up')"
              >
                ↑
              </button>
              <button
                class="icon-btn"
                :disabled="index === steps.length - 1"
                aria-label="下移"
                @click.stop="moveStep(index, 'down')"
              >
                ↓
              </button>
              <button
                class="icon-btn delete"
                aria-label="删除步骤"
                @click.stop="removeStep(index)"
              >
                ✕
              </button>
            </div>
          </div>
        </div>

        <div v-if="!showAddStep" class="add-step-btn">
          <button class="btn btn-outline" @click="showAddStep = true">
            + 添加步骤
          </button>
        </div>

        <div v-if="showAddStep" class="step-type-selector">
          <h4>选择步骤类型</h4>
          <div class="step-type-grid">
            <button
              v-for="type in stepTypes"
              :key="type.value"
              class="step-type-card"
              @click="addStep(type.value)"
            >
              <span class="type-icon">{{ type.icon }}</span>
              <span class="type-label">{{ type.label }}</span>
            </button>
          </div>
          <button class="btn btn-text" @click="showAddStep = false">取消</button>
        </div>
      </div>

      <div v-if="activeTab === 'variables'" class="variables-section">
        <div class="variables-list">
          <div v-if="variables.length === 0" class="empty-variables">
            <p>暂无变量，点击下方按钮添加</p>
          </div>
          <div v-for="(variable, index) in variables" :key="variable.id" class="variable-item">
            <input v-model="variable.name" type="text" placeholder="变量名" class="var-name" />
            <select v-model="variable.type" class="var-type">
              <option value="string">字符串</option>
              <option value="number">数字</option>
              <option value="boolean">布尔值</option>
              <option value="object">对象</option>
              <option value="array">数组</option>
            </select>
            <input v-model="variable.description" type="text" placeholder="描述" class="var-desc" />
            <button
              class="icon-btn delete"
              aria-label="删除变量"
              @click="removeVariable(index)"
            >
              ✕
            </button>
          </div>
        </div>
        <button class="btn btn-outline" @click="addVariable">
          + 添加变量
        </button>
      </div>
    </div>

    <div v-if="selectedStep" class="step-properties-panel">
      <div class="panel-header">
        <h3>步骤属性</h3>
        <button class="icon-btn" aria-label="关闭" @click="selectedStepIndex = -1">✕</button>
      </div>
      <div class="panel-body">
        <div class="form-group">
          <label>步骤名称</label>
          <input v-model="selectedStep.name" type="text" />
        </div>
        <div class="form-group">
          <label>描述</label>
          <textarea v-model="selectedStep.description" rows="2" />
        </div>

        <div v-if="selectedStep.type === 'script'" class="step-type-editor">
          <ScriptStepEditor
            :step="selectedStep"
            :variables="variables"
            @update="(updates) => { if (selectedStep) Object.assign(selectedStep, updates) }"
          />
        </div>

        <div class="form-group">
          <label>超时时间（秒）</label>
          <input v-model.number="selectedStep.timeout" type="number" min="0" />
        </div>
        <div class="form-group">
          <label>重试次数</label>
          <input v-model.number="selectedStep.retryCount" type="number" min="0" />
        </div>
        <div class="form-group">
          <label>重试延迟（秒）</label>
          <input v-model.number="selectedStep.retryDelay" type="number" min="0" />
        </div>
        <div class="form-group checkbox">
          <input :id="'continue-' + selectedStep.id" v-model="selectedStep.continueOnError" type="checkbox" />
          <label :for="'continue-' + selectedStep.id">出错时继续</label>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.workflow-editor {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: var(--el-bg-color);
  border-radius: 12px;
  overflow: hidden;
}

.editor-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px 20px;
  border-bottom: 1px solid var(--el-border-color-lighter);
}

.editor-header h2 {
  font-size: 18px;
  font-weight: 600;
  color: var(--el-text-color-primary);
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
  background: var(--el-color-primary);
  color: var(--el-color-white);
  border-color: var(--el-color-primary);
}

.btn-primary:hover:not(:disabled) {
  background: var(--el-color-primary-dark-2);
  border-color: var(--el-color-primary-dark-2);
}

.btn-secondary {
  background: var(--el-fill-color-light);
  color: var(--el-text-color-regular);
  border-color: var(--el-border-color-light);
}

.btn-secondary:hover {
  background: var(--el-fill-color);
}

.btn-outline {
  background: var(--el-bg-color);
  color: var(--el-color-primary);
  border-color: var(--el-color-primary);
}

.btn-outline:hover {
  background: var(--el-color-info-light-9);
}

.btn-text {
  background: none;
  color: var(--el-text-color-secondary);
  border: none;
  padding: 4px 8px;
}

.btn-text:hover {
  color: var(--el-text-color-regular);
}

.btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
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
  grid-template-columns: repeat(3, 1fr);
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
  color: var(--el-text-color-regular);
}

.form-group input,
.form-group textarea,
.form-group select {
  padding: 8px 12px;
  border: 1px solid var(--el-border-color-light);
  border-radius: 8px;
  font-size: 14px;
  color: var(--el-text-color-primary);
  transition: border-color 0.2s ease;
}

.form-group input:focus,
.form-group textarea:focus,
.form-group select:focus {
  outline: none;
  border-color: var(--el-color-primary);
  box-shadow: 0 0 0 3px var(--gold-tint-strong);
}

.form-group.checkbox {
  flex-direction: row;
  align-items: center;
  gap: 8px;
}

.form-group.checkbox input {
  width: 16px;
  height: 16px;
}

.editor-tabs {
  display: flex;
  gap: 4px;
  border-bottom: 2px solid var(--el-border-color-lighter);
}

.tab-btn {
  padding: 10px 16px;
  background: none;
  border: none;
  font-size: 14px;
  font-weight: 500;
  color: var(--el-text-color-secondary);
  cursor: pointer;
  border-bottom: 2px solid transparent;
  margin-bottom: -2px;
  transition: all 0.2s ease;
}

.tab-btn:hover {
  color: var(--el-text-color-regular);
}

.tab-btn.active {
  color: var(--el-color-primary);
  border-bottom-color: var(--el-color-primary);
}

.steps-section,
.variables-section {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.steps-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.empty-steps,
.empty-variables {
  text-align: center;
  padding: 40px 20px;
  color: var(--el-text-color-secondary);
  background: var(--el-fill-color-light);
  border-radius: 8px;
}

.step-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px;
  background: var(--el-fill-color-light);
  border: 2px solid transparent;
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.step-item:hover {
  background: var(--el-fill-color);
}

.step-item.selected {
  border-color: var(--el-color-primary);
  background: var(--el-color-info-light-9);
}

.step-number {
  width: 24px;
  height: 24px;
  background: var(--el-fill-color);
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 12px;
  font-weight: 600;
  color: var(--el-text-color-regular);
  flex-shrink: 0;
}

.step-item.selected .step-number {
  background: var(--el-color-primary);
  color: var(--el-color-white);
}

.step-icon {
  font-size: 20px;
  flex-shrink: 0;
}

.step-info {
  flex: 1;
  min-width: 0;
}

.step-name {
  font-size: 14px;
  font-weight: 500;
  color: var(--el-text-color-primary);
  margin-bottom: 2px;
}

.step-type {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.step-actions {
  display: flex;
  gap: 4px;
  flex-shrink: 0;
}

.icon-btn {
  width: 28px;
  height: 28px;
  border: 1px solid var(--el-border-color-light);
  background: var(--el-bg-color);
  border-radius: 6px;
  font-size: 12px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s ease;
}

.icon-btn:hover:not(:disabled) {
  border-color: var(--el-color-primary);
  color: var(--el-color-primary);
}

.icon-btn.delete:hover:not(:disabled) {
  border-color: var(--el-color-error);
  color: var(--el-color-error);
}

.icon-btn:disabled {
  opacity: 0.4;
  cursor: not-allowed;
}

.add-step-btn {
  display: flex;
  justify-content: center;
}

.step-type-selector {
  padding: 16px;
  background: var(--el-fill-color-light);
  border-radius: 8px;
  text-align: center;
}

.step-type-selector h4 {
  font-size: 14px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin: 0 0 12px 0;
}

.step-type-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 8px;
  margin-bottom: 12px;
}

.step-type-card {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 6px;
  padding: 12px;
  background: var(--el-bg-color);
  border: 1px solid var(--el-border-color-light);
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.step-type-card:hover {
  border-color: var(--el-color-primary);
  background: var(--el-color-info-light-9);
}

.type-icon {
  font-size: 24px;
}

.type-label {
  font-size: 12px;
  color: var(--el-text-color-regular);
}

.variables-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.variable-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px;
  background: var(--el-fill-color-light);
  border-radius: 8px;
}

.var-name {
  flex: 1;
  padding: 6px 10px;
  border: 1px solid var(--el-border-color-light);
  border-radius: 6px;
  font-size: 13px;
}

.var-type {
  width: 100px;
  padding: 6px 10px;
  border: 1px solid var(--el-border-color-light);
  border-radius: 6px;
  font-size: 13px;
}

.var-desc {
  flex: 1;
  padding: 6px 10px;
  border: 1px solid var(--el-border-color-light);
  border-radius: 6px;
  font-size: 13px;
}

.step-properties-panel {
  width: 320px;
  border-left: 1px solid var(--el-border-color-lighter);
  display: flex;
  flex-direction: column;
  background: var(--el-fill-color-lighter);
}

.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px;
  border-bottom: 1px solid var(--el-border-color-lighter);
}

.panel-header h3 {
  font-size: 16px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin: 0;
}

.panel-body {
  flex: 1;
  overflow-y: auto;
  padding: 16px;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.step-type-editor {
  padding: 12px;
  background: var(--el-fill-color-light);
  border-radius: 8px;
}

@media (max-width: 1024px) {
  .basic-info {
    grid-template-columns: repeat(2, 1fr);
  }

  .step-properties-panel {
    width: 280px;
  }
}

@media (max-width: 768px) {
  .basic-info {
    grid-template-columns: 1fr;
  }

  .step-type-grid {
    grid-template-columns: repeat(2, 1fr);
  }

  .step-properties-panel {
    display: none;
  }

  .variable-item {
    flex-wrap: wrap;
  }

  .var-name,
  .var-desc {
    flex: 1 1 100%;
  }
}
</style>
