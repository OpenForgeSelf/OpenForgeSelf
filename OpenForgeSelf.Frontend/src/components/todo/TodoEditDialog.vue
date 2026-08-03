<script setup lang="ts">
import { ref, reactive, computed, watch } from 'vue'
import {
  ElDialog,
  ElForm,
  ElFormItem,
  ElInput,
  ElDatePicker,
  ElButton,
  ElAlert,
  type FormInstance,
  type FormRules
} from 'element-plus'
import type { TodoItem, TodoCreateRequest, TodoUpdateRequest } from '@/types/todo'

const props = defineProps<{
  visible: boolean
  todo?: TodoItem | null
}>()

const emit = defineEmits<{
  (e: 'update:visible', value: boolean): void
  (e: 'submit', payload: { id?: number; data: TodoCreateRequest | TodoUpdateRequest }): void
  (e: 'cancel'): void
}>()

const isEdit = computed(() => !!props.todo?.id)
const title = computed(() => isEdit.value ? '编辑待办' : '新建待办')

const formRef = ref<FormInstance>()
const form = reactive({
  title: '',
  remark: '',
  dueDate: '' as string
})

const rules: FormRules = {
  title: [
    { required: true, message: '标题不能为空', trigger: 'blur' },
    { max: 200, message: '标题长度不能超过 200 字符', trigger: 'blur' }
  ],
  remark: [
    { max: 1000, message: '备注长度不能超过 1000 字符', trigger: 'blur' }
  ]
}

const errorMessage = ref<string | null>(null)

watch(
  () => props.visible,
  (visible) => {
    if (visible) {
      errorMessage.value = null
      if (props.todo) {
        form.title = props.todo.title
        form.remark = props.todo.remark ?? ''
        form.dueDate = props.todo.dueDate ?? ''
      } else {
        form.title = ''
        form.remark = ''
        form.dueDate = ''
      }
      formRef.value?.clearValidate()
    }
  }
)

function closeDialog() {
  emit('update:visible', false)
  emit('cancel')
}

async function handleSubmit() {
  if (!formRef.value) return
  errorMessage.value = null

  try {
    await formRef.value.validate()
  } catch {
    return
  }

  const trimmedTitle = form.title.trim()
  if (!trimmedTitle) {
    errorMessage.value = '标题不能为空'
    return
  }

  const data: TodoCreateRequest | TodoUpdateRequest = {
    title: trimmedTitle,
    remark: form.remark?.trim() || undefined,
    dueDate: form.dueDate || undefined
  }

  emit('submit', {
    id: props.todo?.id,
    data
  })
}

defineExpose({ errorMessage })
</script>

<template>
  <ElDialog
    :model-value="visible"
    :title="title"
    width="500"
    append-to-body
    @update:model-value="(v: boolean) => emit('update:visible', v)"
    @close="errorMessage = null"
  >
    <ElAlert
      v-if="errorMessage"
      :title="errorMessage"
      type="error"
      :closable="false"
      show-icon
      class="!mb-3"
    />

    <ElForm
      ref="formRef"
      :model="form"
      :rules="rules"
      label-width="80"
      label-position="left"
    >
      <ElFormItem label="标题" prop="title">
        <ElInput
          v-model="form.title"
          placeholder="请输入待办标题"
          maxlength="200"
          show-word-limit
          clearable
        />
      </ElFormItem>

      <ElFormItem label="备注" prop="remark">
        <ElInput
          v-model="form.remark"
          type="textarea"
          :rows="3"
          placeholder="可选：备注说明"
          maxlength="1000"
          show-word-limit
          resize="vertical"
        />
      </ElFormItem>

      <ElFormItem label="截止日期" prop="dueDate">
        <ElDatePicker
          v-model="form.dueDate"
          type="datetime"
          placeholder="可选：选择截止日期"
          format="YYYY-MM-DD HH:mm"
          value-format="YYYY-MM-DDTHH:mm:ss"
          clearable
          class="!w-full"
        />
      </ElFormItem>
    </ElForm>

    <template #footer>
      <ElButton @click="closeDialog">取消</ElButton>
      <ElButton type="primary" @click="handleSubmit">
        {{ isEdit ? '保存' : '创建' }}
      </ElButton>
    </template>
  </ElDialog>
</template>
