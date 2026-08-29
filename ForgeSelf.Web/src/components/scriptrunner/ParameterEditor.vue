<script setup lang="ts">
import { ref, watch } from 'vue'
import type { ScriptParameter, ScriptParameterType } from '@/types/scriptRunner'

const props = defineProps<{
  parameters: ScriptParameter[]
}>()

const emit = defineEmits<{
  (e: 'update:parameters', parameters: ScriptParameter[]): void
}>()

const localParameters = ref<ScriptParameter[]>([])

watch(() => props.parameters, (newParams) => {
  localParameters.value = JSON.parse(JSON.stringify(newParams))
}, { immediate: true, deep: true })

const parameterTypes: { value: ScriptParameterType; label: string; icon: string }[] = [
  { value: 'string', label: '字符串', icon: '📝' },
  { value: 'number', label: '数字', icon: '🔢' },
  { value: 'boolean', label: '布尔值', icon: '✅' },
  { value: 'select', label: '下拉选择', icon: '📋' },
  { value: 'filePath', label: '文件路径', icon: '📄' },
  { value: 'directoryPath', label: '目录路径', icon: '📁' }
]

function generateId(): string {
  return `${Date.now()}-${Math.random().toString(36).substring(2, 9)}`
}

function addParameter(): void {
  const newParam: ScriptParameter = {
    id: generateId(),
    name: '',
    type: 'string',
    description: '',
    defaultValue: '',
    required: false,
    options: []
  }
  localParameters.value.push(newParam)
  emitUpdate()
}

function removeParameter(index: number): void {
  localParameters.value.splice(index, 1)
  emitUpdate()
}

function moveParameter(index: number, direction: 'up' | 'down'): void {
  const newIndex = direction === 'up' ? index - 1 : index + 1
  if (newIndex < 0 || newIndex >= localParameters.value.length) return

  const [param] = localParameters.value.splice(index, 1)
  localParameters.value.splice(newIndex, 0, param)
  emitUpdate()
}

function updateParameter(): void {
  emitUpdate()
}

function emitUpdate(): void {
  emit('update:parameters', JSON.parse(JSON.stringify(localParameters.value)))
}

function addOption(paramIndex: number): void {
  const param = localParameters.value[paramIndex]
  if (param.options) {
    param.options.push('')
    emitUpdate()
  }
}

function removeOption(paramIndex: number, optionIndex: number): void {
  const param = localParameters.value[paramIndex]
  if (param.options) {
    param.options.splice(optionIndex, 1)
    emitUpdate()
  }
}
</script>

<template>
  <div class="parameter-editor">
    <div class="editor-header">
      <h4>参数配置</h4>
      <button class="btn btn-outline btn-sm" @click="addParameter">
        + 添加参数
      </button>
    </div>

    <div v-if="localParameters.length === 0" class="empty-params">
      <p>暂无参数，点击上方按钮添加</p>
    </div>

    <div v-else class="params-list">
      <div
        v-for="(param, index) in localParameters"
        :key="param.id"
        class="param-item"
      >
        <div class="param-header">
          <div class="param-order">{{ index + 1 }}</div>
          <div class="param-actions">
            <button
              class="icon-btn"
              :disabled="index === 0"
              aria-label="上移"
              @click="moveParameter(index, 'up')"
            >
              ↑
            </button>
            <button
              class="icon-btn"
              :disabled="index === localParameters.length - 1"
              aria-label="下移"
              @click="moveParameter(index, 'down')"
            >
              ↓
            </button>
            <button
              class="icon-btn delete"
              aria-label="删除参数"
              @click="removeParameter(index)"
            >
              ✕
            </button>
          </div>
        </div>

        <div class="param-body">
          <div class="form-group">
            <label>参数名</label>
            <input
              v-model="param.name"
              type="text"
              placeholder="paramName"
              @input="updateParameter"
            />
          </div>

          <div class="form-group">
            <label>类型</label>
            <select v-model="param.type" @change="updateParameter">
              <option v-for="pt in parameterTypes" :key="pt.value" :value="pt.value">
                {{ pt.icon }} {{ pt.label }}
              </option>
            </select>
          </div>

          <div class="form-group">
            <label>描述</label>
            <input
              v-model="param.description"
              type="text"
              placeholder="参数描述"
              @input="updateParameter"
            />
          </div>

          <div class="form-group">
            <label>默认值</label>
            <input
              v-if="param.type !== 'boolean'"
              v-model="param.defaultValue"
              type="text"
              placeholder="默认值"
              @input="updateParameter"
            />
            <select v-else v-model="param.defaultValue" @change="updateParameter">
              <option :value="true">true</option>
              <option :value="false">false</option>
            </select>
          </div>

          <div class="form-group checkbox">
            <input
              :id="'required-' + param.id"
              v-model="param.required"
              type="checkbox"
              @change="updateParameter"
            />
            <label :for="'required-' + param.id">必填</label>
          </div>
        </div>

        <div v-if="param.type === 'select'" class="param-options">
          <div class="options-header">
            <span class="options-label">选项列表</span>
            <button class="btn btn-text btn-sm" @click="addOption(index)">
              + 添加选项
            </button>
          </div>
          <div class="options-list">
            <div
              v-for="(_, optIndex) in param.options"
              :key="optIndex"
              class="option-item"
            >
              <input
                v-model="param.options![optIndex]"
                type="text"
                :placeholder="`选项 ${optIndex + 1}`"
                @input="updateParameter"
              />
              <button
                class="icon-btn delete"
                aria-label="删除选项"
                @click="removeOption(index, optIndex)"
              >
                ✕
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.parameter-editor {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.editor-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.editor-header h4 {
  font-size: 14px;
  font-weight: 600;
  color: #212529;
  margin: 0;
}

.btn {
  padding: 6px 12px;
  border-radius: 6px;
  font-size: 12px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s ease;
  border: 1px solid transparent;
}

.btn-outline {
  background: #fff;
  color: #1976d2;
  border-color: #1976d2;
}

.btn-outline:hover {
  background: #f0f7ff;
}

.btn-text {
  background: none;
  color: #1976d2;
  border: none;
  padding: 4px 8px;
}

.btn-text:hover {
  color: #1565c0;
}

.btn-sm {
  padding: 4px 10px;
  font-size: 12px;
}

.empty-params {
  text-align: center;
  padding: 30px 20px;
  color: #6c757d;
  background: #f8f9fa;
  border-radius: 8px;
  font-size: 13px;
}

.params-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.param-item {
  background: #f8f9fa;
  border: 1px solid #e9ecef;
  border-radius: 8px;
  overflow: hidden;
}

.param-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 10px 12px;
  background: #fff;
  border-bottom: 1px solid #e9ecef;
}

.param-order {
  width: 24px;
  height: 24px;
  background: #1976d2;
  color: #fff;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 12px;
  font-weight: 600;
}

.param-actions {
  display: flex;
  gap: 4px;
}

.icon-btn {
  width: 26px;
  height: 26px;
  border: 1px solid #dee2e6;
  background: #fff;
  border-radius: 6px;
  font-size: 12px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s ease;
}

.icon-btn:hover:not(:disabled) {
  border-color: #1976d2;
  color: #1976d2;
}

.icon-btn.delete:hover:not(:disabled) {
  border-color: #dc3545;
  color: #dc3545;
}

.icon-btn:disabled {
  opacity: 0.4;
  cursor: not-allowed;
}

.param-body {
  padding: 12px;
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 10px;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.form-group.checkbox {
  flex-direction: row;
  align-items: center;
  gap: 8px;
}

.form-group label {
  font-size: 12px;
  font-weight: 500;
  color: #495057;
}

.form-group input,
.form-group select {
  padding: 6px 10px;
  border: 1px solid #dee2e6;
  border-radius: 6px;
  font-size: 13px;
  color: #212529;
  transition: border-color 0.2s ease;
}

.form-group input:focus,
.form-group select:focus {
  outline: none;
  border-color: #1976d2;
  box-shadow: 0 0 0 2px rgba(25, 118, 210, 0.1);
}

.param-options {
  padding: 0 12px 12px 12px;
}

.options-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 8px;
}

.options-label {
  font-size: 12px;
  font-weight: 500;
  color: #495057;
}

.options-list {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.option-item {
  display: flex;
  gap: 6px;
}

.option-item input {
  flex: 1;
  padding: 6px 10px;
  border: 1px solid #dee2e6;
  border-radius: 6px;
  font-size: 13px;
}

@media (max-width: 768px) {
  .param-body {
    grid-template-columns: 1fr;
  }
}
</style>
