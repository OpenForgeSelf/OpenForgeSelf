<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { aiModelsApi } from '@/services/aiModelsApi'
import { settingsApi } from '@/services/settingsApi'
import type { AIModel } from '@/types/aiModel'

const localModelPath = ref('~/.forgeself/models')
const maxContext = ref(4096)
const temperature = ref(0.7)
const systemPrompt = ref('你是一个高效的个人 AI 助手，专注于工具调用和任务执行。')

// ===== 默认模型（动态拉取实际模型列表，持久化到 ForgeSetting）=====
const models = ref<AIModel[]>([])
const defaultModel = ref('')
const modelLoading = ref(false)

/** 下拉展示名：优先别名，否则用 chatModelId */
function modelLabel(m: AIModel): string {
  return m.alias || m.chatModelId
}

async function loadModels() {
  modelLoading.value = true
  try {
    const groups = await aiModelsApi.list({ enabledOnly: true })
    models.value = groups.flatMap((g) => g.models)
  } catch {
    models.value = []
  } finally {
    modelLoading.value = false
  }
}

async function loadDefaultModel() {
  try {
    const settings = await settingsApi.getSettings()
    defaultModel.value = settings.defaultModel
  } catch {
    defaultModel.value = ''
  }
}

async function onDefaultModelChange(val: string) {
  defaultModel.value = val
  try {
    await settingsApi.updateSettings({ defaultModel: val })
  } catch {
    // 保存失败静默处理，下拉仍反映本次选择
  }
}

onMounted(() => {
  loadModels()
  loadDefaultModel()
})
</script>

<template>
  <div class="space-y-6">
    <!-- 页头 -->
    <div class="flex items-start justify-between pb-4 border-b border-border">
      <div>
        <h2 class="text-xl font-bold text-text m-0 leading-tight">AI Agent 配置</h2>
        <p class="text-sm text-text-regular mt-1 mb-0">调整模型参数、上下文长度与推理行为</p>
      </div>
    </div>

    <!-- 设置项列表 -->
    <el-card shadow="never">
      <!-- 默认模型 -->
      <div class="flex items-center justify-between py-3 border-b border-border-light">
        <div>
          <span class="text-sm font-medium text-text">默认模型</span>
          <p class="text-xs text-text-secondary mt-0.5 mb-0">推理使用的模型</p>
        </div>
        <el-select
          v-model="defaultModel"
          class="w-[200px]"
          :loading="modelLoading"
          placeholder="请选择默认模型"
          @change="onDefaultModelChange"
        >
          <el-option
            v-for="m in models"
            :key="m.chatModelId"
            :value="m.chatModelId"
            :label="modelLabel(m)"
          />
        </el-select>
      </div>

      <!-- 本地模型路径 -->
      <div class="flex items-center justify-between py-3 border-b border-border-light">
        <div>
          <span class="text-sm font-medium text-text">本地模型路径</span>
          <p class="text-xs text-text-secondary mt-0.5 mb-0">本地模型权重文件目录</p>
        </div>
        <div class="flex items-center gap-2">
          <code class="text-xs font-mono text-text-regular">{{ localModelPath }}</code>
          <el-button size="small">浏览</el-button>
        </div>
      </div>

      <!-- 最大上下文长度 -->
      <div class="flex items-center justify-between py-3 border-b border-border-light">
        <div>
          <span class="text-sm font-medium text-text">最大上下文长度</span>
          <p class="text-xs text-text-secondary mt-0.5 mb-0">单次对话的最大 token 数</p>
        </div>
        <el-input-number
          v-model="maxContext"
          :min="512"
          :max="8192"
          :step="512"
          class="w-[160px]"
        />
      </div>

      <!-- 温度 -->
      <div class="flex items-center justify-between py-3 border-b border-border-light">
        <div>
          <span class="text-sm font-medium text-text">温度</span>
          <p class="text-xs text-text-secondary mt-0.5 mb-0">控制生成文本的随机性</p>
        </div>
        <el-input-number
          v-model="temperature"
          :min="0"
          :max="2"
          :step="0.1"
          :precision="1"
          class="w-[160px]"
        />
      </div>

      <!-- 系统提示词 -->
      <div class="flex flex-col gap-3 py-3 last:border-b-0">
        <div>
          <span class="text-sm font-medium text-text">系统提示词</span>
          <p class="text-xs text-text-secondary mt-0.5 mb-0">AI Agent 的系统级指令</p>
        </div>
        <el-input
          v-model="systemPrompt"
          type="textarea"
          :rows="3"
          placeholder="输入系统提示词..."
        />
      </div>
    </el-card>
  </div>
</template>
