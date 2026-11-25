<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import type { ScriptParameter } from '@/types/scriptRunner'

const props = defineProps<{
  parameters: ScriptParameter[]
  initialValues?: Record<string, unknown>
}>()

const emit = defineEmits<{
  (e: 'submit', values: Record<string, unknown>): void
  (e: 'cancel'): void
  (e: 'validChange', isValid: boolean): void
}>()

const formValues = ref<Record<string, unknown>>({})
const touched = ref<Record<string, boolean>>({})

watch(() => props.parameters, () => {
  initForm()
}, { immediate: true, deep: true })

watch(() => props.initialValues, (newVals) => {
  if (newVals) {
    formValues.value = { ...newVals }
  }
}, { immediate: true, deep: true })

function initForm(): void {
  const values: Record<string, unknown> = {}
  for (const param of props.parameters) {
    if (props.initialValues && param.name in props.initialValues) {
      values[param.name] = props.initialValues[param.name]
    } else {
      values[param.name] = param.defaultValue ?? getDefaultValue(param.type)
    }
  }
  formValues.value = values
}

function getDefaultValue(type: string): unknown {
  switch (type) {
    case 'boolean':
      return false
    case 'number':
      return 0
    default:
      return ''
  }
}

const isValid = computed(() => {
  for (const param of props.parameters) {
    if (param.required) {
      const value = formValues.value[param.name]
      if (value === '' || value === null || value === undefined) {
        return false
      }
    }
  }
  return true
})

watch(isValid, (newVal) => {
  emit('validChange', newVal)
}, { immediate: true })

function getError(param: ScriptParameter): string | null {
  if (!param.required || !touched.value[param.name]) {
    return null
  }
  const value = formValues.value[param.name]
  if (value === '' || value === null || value === undefined) {
    return '此字段为必填项'
  }
  return null
}

function handleBlur(paramName: string): void {
  touched.value[paramName] = true
}

function handleSubmit(): void {
  for (const param of props.parameters) {
    touched.value[param.name] = true
  }
  if (isValid.value) {
    emit('submit', { ...formValues.value })
  }
}

function handleCancel(): void {
  emit('cancel')
}
</script>

<template>
  <div class="parameter-form">
    <div v-if="parameters.length === 0" class="no-params">
      <p>此脚本没有需要配置的参数</p>
    </div>

    <div v-else class="form-fields">
      <div
        v-for="param in parameters"
        :key="param.id"
        class="form-field"
        :class="{ error: getError(param) }"
      >
        <label :for="`param-${param.id}`">
          {{ param.name }}
          <span v-if="param.required" class="required-mark">*</span>
        </label>
        <p v-if="param.description" class="field-description">{{ param.description }}</p>

        <div v-if="param.type === 'string'" class="field-input">
          <input
            :id="`param-${param.id}`"
            v-model="formValues[param.name]"
            type="text"
            :placeholder="`请输入 ${param.name}`"
            @blur="handleBlur(param.name)"
          />
        </div>

        <div v-else-if="param.type === 'number'" class="field-input">
          <input
            :id="`param-${param.id}`"
            v-model.number="formValues[param.name]"
            type="number"
            :placeholder="`请输入 ${param.name}`"
            @blur="handleBlur(param.name)"
          />
        </div>

        <div v-else-if="param.type === 'boolean'" class="field-input checkbox-field">
          <input
            :id="`param-${param.id}`"
            v-model="formValues[param.name]"
            type="checkbox"
            @change="handleBlur(param.name)"
          />
          <label :for="`param-${param.id}`" class="checkbox-label">
            {{ formValues[param.name] ? '是' : '否' }}
          </label>
        </div>

        <div v-else-if="param.type === 'select'" class="field-input">
          <select
            :id="`param-${param.id}`"
            v-model="formValues[param.name]"
            @change="handleBlur(param.name)"
          >
            <option value="">请选择...</option>
            <option v-for="(opt, idx) in param.options" :key="idx" :value="opt">
              {{ opt }}
            </option>
          </select>
        </div>

        <div v-else-if="param.type === 'filePath'" class="field-input">
          <input
            :id="`param-${param.id}`"
            v-model="formValues[param.name]"
            type="text"
            placeholder="请输入文件路径"
            @blur="handleBlur(param.name)"
          />
        </div>

        <div v-else-if="param.type === 'directoryPath'" class="field-input">
          <input
            :id="`param-${param.id}`"
            v-model="formValues[param.name]"
            type="text"
            placeholder="请输入目录路径"
            @blur="handleBlur(param.name)"
          />
        </div>

        <p v-if="getError(param)" class="field-error">{{ getError(param) }}</p>
      </div>
    </div>

    <div class="form-actions">
      <button class="btn btn-secondary" @click="handleCancel">取消</button>
      <button class="btn btn-primary" :disabled="!isValid" @click="handleSubmit">
        执行脚本
      </button>
    </div>
  </div>
</template>

<style scoped>
.parameter-form {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.no-params {
  text-align: center;
  padding: 20px;
  color: var(--el-text-color-secondary);
  background: var(--el-fill-color-light);
  border-radius: 8px;
  font-size: 14px;
}

.form-fields {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.form-field {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.form-field label {
  font-size: 13px;
  font-weight: 500;
  color: var(--el-text-color-regular);
}

.required-mark {
  color: var(--el-color-error);
  margin-left: 2px;
}

.field-description {
  font-size: 12px;
  color: var(--el-text-color-secondary);
  margin: 0;
}

.field-input input,
.field-input select {
  width: 100%;
  padding: 8px 12px;
  border: 1px solid var(--el-border-color-light);
  border-radius: 8px;
  font-size: 14px;
  color: var(--el-text-color-primary);
  transition: border-color 0.2s ease, box-shadow 0.2s ease;
  box-sizing: border-box;
}

.field-input input:focus,
.field-input select:focus {
  outline: none;
  border-color: var(--el-color-primary);
  box-shadow: 0 0 0 3px var(--gold-tint-strong);
}

.checkbox-field {
  display: flex;
  align-items: center;
  gap: 10px;
}

.checkbox-field input[type="checkbox"] {
  width: auto;
}

.checkbox-label {
  margin: 0;
  font-size: 14px;
  color: var(--el-text-color-regular);
}

.form-field.error input,
.form-field.error select {
  border-color: var(--el-color-error);
}

.form-field.error input:focus,
.form-field.error select:focus {
  box-shadow: 0 0 0 3px color-mix(in srgb, var(--el-color-error) 10%, transparent);
}

.field-error {
  font-size: 12px;
  color: var(--el-color-error);
  margin: 0;
}

.form-actions {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  padding-top: 8px;
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

.btn-primary:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.btn-secondary {
  background: var(--el-fill-color-light);
  color: var(--el-text-color-regular);
  border-color: var(--el-border-color-light);
}

.btn-secondary:hover {
  background: var(--el-fill-color);
}
</style>
