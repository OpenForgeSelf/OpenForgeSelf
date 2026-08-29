<script setup lang="ts">
import { ref, computed } from 'vue'
import AppLogo from '@/components/AppLogo.vue'
import type { PlanWorkflowResponse, WorkflowDefinition, WorkflowStepType } from '@/types/workflow'
import { useWorkflowStore } from '@/stores/workflow'

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'confirm', workflow: WorkflowDefinition): void
}>()

const workflowStore = useWorkflowStore()

const description = ref('')
const isGenerating = ref(false)
const generatedWorkflow = ref<WorkflowDefinition | null>(null)
const confidence = ref(0)
const suggestedName = ref('')
const saveAsTemplate = ref(false)
const generationError = ref('')

const stepTypes: Record<WorkflowStepType, { label: string; icon: string }> = {
  tool_call: { label: '工具调用', icon: '🔧' },
  condition: { label: '条件判断', icon: '🔀' },
  loop: { label: '循环', icon: '🔄' },
  parallel: { label: '并行', icon: '⚡' },
  wait: { label: '等待', icon: '⏱️' },
  http: { label: 'HTTP请求', icon: '🌐' },
  script: { label: '脚本', icon: 'code' }
}

const canGenerate = computed(() => description.value.trim().length > 0 && !isGenerating.value)

async function handleGenerate(): Promise<void> {
  if (!description.value.trim()) return

  isGenerating.value = true
  generationError.value = ''
  generatedWorkflow.value = null

  try {
    const result: PlanWorkflowResponse = await workflowStore.planWorkflow({
      description: description.value
    })
    generatedWorkflow.value = result.workflow
    confidence.value = result.confidence
    suggestedName.value = result.suggestedName
  } catch (e) {
      generationError.value = e instanceof Error ? e.message : '生成失败，请重试'
    console.error('AI生成工作流失败:', e)
  } finally {
    isGenerating.value = false
  }
}

function handleConfirm(): void {
  if (!generatedWorkflow.value) return
  emit('confirm', generatedWorkflow.value)
}

function handleClose(): void {
  emit('close')
}

function getStepTypeInfo(type: WorkflowStepType) {
  return stepTypes[type] || { label: type, icon: '📋' }
}

function resetGenerator(): void {
  generatedWorkflow.value = null
  confidence.value = 0
  suggestedName.value = ''
  generationError.value = ''
}
</script>

<template>
  <div class="ai-generator">
    <div class="generator-header">
      <div class="header-left">
        <AppLogo :size="20" />
        <h2>🤖 AI 工作流生成</h2>
        <p>用自然语言描述你的需求，AI 将自动生成工作流</p>
      </div>
      <button class="close-btn" aria-label="关闭" @click="handleClose">✕</button>
    </div>

    <div class="generator-body">
      <div v-if="!generatedWorkflow" class="input-section">
        <div class="form-group">
          <label>需求描述</label>
          <textarea
            v-model="description"
            class="description-input"
            placeholder="例如：批量重命名某个目录下的所有图片文件，按照日期序号命名，然后压缩打包..."
            rows="6"
            :disabled="isGenerating"
          />
          <p class="hint">
            提示：描述越详细，生成的工作流越准确。可以包含具体的步骤、条件、输入输出等。
          </p>
        </div>

        <div class="generate-btn-wrapper">
          <button
            class="btn btn-primary btn-large"
            :disabled="!canGenerate"
            @click="handleGenerate"
          >
            <span v-if="isGenerating" class="spinner" />
            {{ isGenerating ? 'AI 生成中...' : '✨ 生成工作流' }}
          </button>
        </div>

        <div v-if="generationError" class="error-message">
          {{ generationError }}
        </div>

        <div class="examples-section">
          <h4>示例</h4>
          <div class="example-list">
            <button
              v-for="(example, index) in examples"
              :key="index"
              class="example-item"
              :disabled="isGenerating"
              @click="description = example"
            >
              {{ example }}
            </button>
          </div>
        </div>
      </div>

      <div v-else class="preview-section">
        <div class="preview-header">
          <div>
            <h3>{{ suggestedName || generatedWorkflow.name }}</h3>
            <div class="confidence">
              置信度: {{ (confidence * 100).toFixed(0) }}%
            </div>
          </div>
          <button class="btn btn-text" @click="resetGenerator">
            重新生成
          </button>
        </div>

        <div class="workflow-preview">
          <h4>工作流步骤</h4>
          <div class="steps-preview">
            <div
              v-for="(step, index) in generatedWorkflow.steps"
              :key="step.id"
              class="preview-step"
            >
              <div class="step-number">{{ index + 1 }}</div>
              <div class="step-icon">{{ getStepTypeInfo(step.type).icon }}</div>
              <div class="step-info">
                <div class="step-name">{{ step.name }}</div>
                <div class="step-type">{{ getStepTypeInfo(step.type).label }}</div>
              </div>
            </div>
          </div>
        </div>

        <div v-if="generatedWorkflow.variables.length > 0" class="variables-preview">
          <h4>输入变量</h4>
          <div class="variables-list">
            <div v-for="variable in generatedWorkflow.variables" :key="variable.id" class="variable-item">
              <span class="var-name">{{ variable.name }}</span>
              <span class="var-type">{{ variable.type }}</span>
              <span class="var-desc">{{ variable.description }}</span>
            </div>
          </div>
        </div>

        <div class="template-option">
          <input id="save-template" v-model="saveAsTemplate" type="checkbox" />
          <label for="save-template">保存为模板，方便以后使用</label>
        </div>

        <div class="confirm-actions">
          <button class="btn btn-secondary" @click="resetGenerator">
            重新生成
          </button>
          <button class="btn btn-primary" @click="handleConfirm">
            ✓ 确认并保存
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script lang="ts">
const examples = [
  '批量重命名指定文件夹中的所有图片，按照"日期_序号"格式命名',
  '清理下载目录中超过30天未访问的文件，并统计释放空间',
  '监控CPU使用率，超过80%时自动记录进程列表',
  '将Markdown文件批量转换为HTML并生成索引页'
]
</script>

<style scoped>
.ai-generator {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: #fff;
  border-radius: 12px;
  overflow: hidden;
}

.generator-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  padding: 20px 24px;
  border-bottom: 1px solid #e9ecef;
  background: linear-gradient(135deg, #f0f7ff 0%, #e8f4fd 100%);
}

.header-left h2 {
  font-size: 20px;
  font-weight: 600;
  color: #212529;
  margin: 0 0 4px 0;
}

.header-left p {
  font-size: 13px;
  color: #6c757d;
  margin: 0;
}

.close-btn {
  width: 32px;
  height: 32px;
  border: none;
  background: rgba(255, 255, 255, 0.8);
  border-radius: 8px;
  font-size: 16px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s ease;
}

.close-btn:hover {
  background: #fff;
  color: #dc3545;
}

.generator-body {
  flex: 1;
  overflow-y: auto;
  padding: 24px;
}

.input-section {
  max-width: 700px;
  margin: 0 auto;
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.form-group label {
  font-size: 14px;
  font-weight: 500;
  color: #495057;
}

.description-input {
  padding: 12px 16px;
  border: 1px solid #dee2e6;
  border-radius: 10px;
  font-size: 14px;
  line-height: 1.6;
  resize: vertical;
  transition: border-color 0.2s ease;
}

.description-input:focus {
  outline: none;
  border-color: #1976d2;
  box-shadow: 0 0 0 3px rgba(25, 118, 210, 0.1);
}

.description-input:disabled {
  background: #f8f9fa;
  cursor: not-allowed;
}

.hint {
  font-size: 12px;
  color: #6c757d;
  margin: 0;
}

.generate-btn-wrapper {
  display: flex;
  justify-content: center;
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

.btn-text {
  background: none;
  color: #6c757d;
  border: none;
}

.btn-text:hover {
  color: #1976d2;
}

.btn-large {
  padding: 12px 32px;
  font-size: 15px;
}

.spinner {
  display: inline-block;
  width: 16px;
  height: 16px;
  border: 2px solid rgba(255, 255, 255, 0.3);
  border-top-color: #fff;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
  margin-right: 8px;
  vertical-align: middle;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

.error-message {
  padding: 12px 16px;
  background: #fff5f5;
  border: 1px solid #fecaca;
  border-radius: 8px;
  color: #dc3545;
  font-size: 13px;
}

.examples-section h4 {
  font-size: 14px;
  font-weight: 600;
  color: #495057;
  margin: 0 0 12px 0;
}

.example-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.example-item {
  padding: 10px 14px;
  background: #f8f9fa;
  border: 1px solid #e9ecef;
  border-radius: 8px;
  font-size: 13px;
  color: #495057;
  text-align: left;
  cursor: pointer;
  transition: all 0.2s ease;
}

.example-item:hover:not(:disabled) {
  background: #f0f7ff;
  border-color: #1976d2;
  color: #1976d2;
}

.example-item:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.preview-section {
  max-width: 800px;
  margin: 0 auto;
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.preview-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding-bottom: 16px;
  border-bottom: 1px solid #e9ecef;
}

.preview-header h3 {
  font-size: 18px;
  font-weight: 600;
  color: #212529;
  margin: 0 0 4px 0;
}

.confidence {
  font-size: 13px;
  color: #28a745;
  font-weight: 500;
}

.workflow-preview h4,
.variables-preview h4 {
  font-size: 14px;
  font-weight: 600;
  color: #495057;
  margin: 0 0 12px 0;
}

.steps-preview {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.preview-step {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px 16px;
  background: #f8f9fa;
  border-radius: 8px;
}

.step-number {
  width: 28px;
  height: 28px;
  background: #1976d2;
  color: #fff;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 13px;
  font-weight: 600;
  flex-shrink: 0;
}

.step-icon {
  font-size: 20px;
  flex-shrink: 0;
}

.step-info {
  flex: 1;
}

.step-name {
  font-size: 14px;
  font-weight: 500;
  color: #212529;
  margin-bottom: 2px;
}

.step-type {
  font-size: 12px;
  color: #6c757d;
}

.variables-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.variable-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px 14px;
  background: #f8f9fa;
  border-radius: 8px;
}

.var-name {
  font-size: 13px;
  font-weight: 500;
  color: #212529;
  min-width: 100px;
}

.var-type {
  font-size: 11px;
  padding: 2px 8px;
  background: #e9ecef;
  border-radius: 10px;
  color: #495057;
}

.var-desc {
  flex: 1;
  font-size: 12px;
  color: #6c757d;
}

.template-option {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 12px;
  background: #f8f9fa;
  border-radius: 8px;
}

.template-option input[type="checkbox"] {
  width: 16px;
  height: 16px;
}

.template-option label {
  font-size: 13px;
  color: #495057;
  cursor: pointer;
}

.confirm-actions {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
  padding-top: 16px;
  border-top: 1px solid #e9ecef;
}

@media (max-width: 768px) {
  .generator-header {
    padding: 16px;
  }

  .generator-body {
    padding: 16px;
  }

  .header-left h2 {
    font-size: 18px;
  }

  .confirm-actions {
    flex-direction: column-reverse;
  }

  .confirm-actions .btn {
    width: 100%;
  }
}
</style>
