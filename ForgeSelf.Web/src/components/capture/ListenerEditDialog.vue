<script setup lang="ts">
import { reactive, ref, watch } from 'vue'
import type { FormInstance, FormRules } from 'element-plus'
import type { ListenerConfig, CreateListenerRequest } from '@/types/capture'

/**
 * 监听器新建/编辑弹窗。
 * visible 为 true 且 listener 非空时进入编辑模式（预填），否则为新建模式。
 */

const props = defineProps<{
  visible: boolean
  listener: ListenerConfig | null
}>()

const emit = defineEmits<{
  'update:visible': [value: boolean]
  submit: [payload: { id?: number; data: CreateListenerRequest }]
  cancel: []
}>()

const formRef = ref<FormInstance>()
const errorMessage = ref<string | null>(null)
const form = reactive<CreateListenerRequest>({
  name: '',
  listenAddress: '0.0.0.0',
  listenPort: 8080,
  targetHost: '',
  targetPort: null,
  enabled: true,
  description: ''
})

const rules: FormRules = {
  name: [{ required: true, message: '请输入监听器名称', trigger: 'blur' }],
  listenPort: [
    { required: true, message: '请输入监听端口', trigger: 'blur' },
    { type: 'number', min: 1, max: 65535, message: '端口范围为 1-65535', trigger: 'blur' }
  ]
}

watch(
  () => props.visible,
  (visible) => {
    if (!visible) return
    // 打开时：编辑模式预填，新建模式重置为默认值
    if (props.listener) {
      form.name = props.listener.name
      form.listenAddress = props.listener.listenAddress
      form.listenPort = props.listener.listenPort
      form.targetHost = props.listener.targetHost ?? ''
      form.targetPort = props.listener.targetPort ?? null
      form.enabled = props.listener.enabled
      form.description = props.listener.description ?? ''
    } else {
      form.name = ''
      form.listenAddress = '0.0.0.0'
      form.listenPort = 8080
      form.targetHost = ''
      form.targetPort = null
      form.enabled = true
      form.description = ''
    }
    formRef.value?.clearValidate()
    errorMessage.value = null
  }
)

function handleCancel() {
  emit('cancel')
  emit('update:visible', false)
}

async function handleSubmit() {
  if (!formRef.value) return
  errorMessage.value = null

  // 表单规则校验（名称/端口）
  try {
    await formRef.value.validate()
  } catch {
    return
  }

  const trimmedName = form.name.trim()
  if (!trimmedName) {
    errorMessage.value = '请输入监听器名称'
    return
  }

  // 业务联动校验（不依赖表单规则环境，提交时强制）：配置了目标主机则必须填目标端口
  if (form.targetHost?.trim() && !form.targetPort) {
    errorMessage.value = '配置了目标主机时必须填写目标端口'
    return
  }

  emit('submit', {
    id: props.listener?.id,
    data: {
      name: trimmedName,
      listenAddress: form.listenAddress.trim() || '0.0.0.0',
      listenPort: form.listenPort,
      targetHost: form.targetHost?.trim() || null,
      targetPort: form.targetPort ?? null,
      enabled: form.enabled,
      description: form.description?.trim() || null
    }
  })
}
</script>

<template>
  <ElDialog
    :model-value="visible"
    :title="listener ? '编辑监听器' : '新建监听器'"
    width="520px"
    @update:model-value="emit('update:visible', $event)"
  >
    <ElAlert
      v-if="errorMessage"
      :title="errorMessage"
      type="error"
      :closable="false"
      show-icon
      class="!mb-3"
    />

    <ElForm ref="formRef" :model="form" :rules="rules" label-width="90px">
      <ElFormItem label="名称" prop="name">
        <ElInput v-model="form.name" placeholder="例如：本机测试代理" maxlength="50" />
      </ElFormItem>

      <ElFormItem label="监听地址" prop="listenAddress">
        <ElInput v-model="form.listenAddress" placeholder="0.0.0.0（所有网卡）" />
      </ElFormItem>

      <ElFormItem label="监听端口" prop="listenPort">
        <ElInputNumber
          v-model="form.listenPort"
          :min="1"
          :max="65535"
          :step="1"
          style="width: 100%"
        />
      </ElFormItem>

      <ElDivider content-position="left">目标转发（留空则仅抓包不转发）</ElDivider>

      <ElFormItem label="目标主机" prop="targetHost">
        <ElInput v-model="form.targetHost" placeholder="例如：127.0.0.1 或 example.com" />
      </ElFormItem>

      <ElFormItem label="目标端口" prop="targetPort">
        <ElInputNumber
          v-model="form.targetPort"
          :min="1"
          :max="65535"
          :step="1"
          :controls="false"
          placeholder="例如：80"
          style="width: 100%"
        />
      </ElFormItem>

      <ElFormItem label="启用" prop="enabled">
        <ElSwitch v-model="form.enabled" active-text="创建后立即开始监听" />
      </ElFormItem>

      <ElFormItem label="描述" prop="description">
        <ElInput v-model="form.description" type="textarea" :rows="2" maxlength="200" />
      </ElFormItem>
    </ElForm>

    <template #footer>
      <ElButton @click="handleCancel">取消</ElButton>
      <ElButton type="primary" @click="handleSubmit">{{ listener ? '保存' : '创建' }}</ElButton>
    </template>
  </ElDialog>
</template>
