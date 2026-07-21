<script setup lang="ts">
/**
 * AI 提供者面板 — 独立组件
 * 对齐设计稿 forgeself-design/pages/ai-provider.html
 * 纯 Element Plus 组件 + Tailwind 布局
 */
import { ref, reactive, watch, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Plus, Refresh, Connection, ArrowDown, Delete, Edit, CopyDocument, Link } from '@element-plus/icons-vue'
import { aiProvidersApi } from '@/services/aiProvidersApi'
import { aiModelsApi } from '@/services/aiModelsApi'
import type { AIProvider, AIProviderRequest, AIProviderTestResult, AIProviderType } from '@/types/aiProvider'
import type { AIModel, FetchModelsResult } from '@/types/aiModel'

// ===== 提供方列表 =====
const providers = ref<AIProvider[]>([])
const loading = ref(false)
const error = ref('')

/** 提供方类型 → el-tag type 映射（对齐设计稿） */
const typeTagMap: Record<AIProviderType, 'success' | 'warning' | 'info'> = {
  OpenAI: 'success',
  Anthropic: 'warning',
  Custom: 'info',
}

async function loadProviders() {
  loading.value = true
  error.value = ''
  try {
    providers.value = await aiProvidersApi.list()
  } catch (e) {
    error.value = e instanceof Error ? e.message : '加载失败'
  } finally {
    loading.value = false
  }
}

// ===== 展开/折叠 =====
const expandedId = ref<number | null>(null)
const providerModels = ref<Record<number, AIModel[]>>({})
const modelsLoading = ref<Record<number, boolean>>({})

function toggleExpand(id: number) {
  expandedId.value = expandedId.value === id ? null : id
}

watch(expandedId, async (pid) => {
  if (pid == null || providerModels.value[pid]) return
  modelsLoading.value = { ...modelsLoading.value, [pid]: true }
  try {
    const groups = await aiModelsApi.list({ providerId: pid })
    providerModels.value = { ...providerModels.value, [pid]: groups.length > 0 ? groups[0].models : [] }
  } catch (e) {
    ElMessage.error(e instanceof Error ? e.message : '加载模型失败')
  } finally {
    modelsLoading.value = { ...modelsLoading.value, [pid]: false }
  }
})

// ===== 测试连接 =====
const testingId = ref<number | null>(null)
const testResults = ref<Record<number, AIProviderTestResult>>({})
const fetchResults = ref<Record<number, FetchModelsResult>>({})

async function testProvider(p: AIProvider) {
  testingId.value = p.id
  try {
    const result = await aiProvidersApi.test(p.id)
    testResults.value = { ...testResults.value, [p.id]: result }
    if (result.success) {
      const fr = await aiModelsApi.fetchForProvider(p.id)
      fetchResults.value = { ...fetchResults.value, [p.id]: fr }
      const groups = await aiModelsApi.list({ providerId: p.id })
      providerModels.value = { ...providerModels.value, [p.id]: groups.length > 0 ? groups[0].models : [] }
      expandedId.value = p.id
    }
  } catch (e) {
    testResults.value = { ...testResults.value, [p.id]: { success: false, latencyMs: 0, message: e instanceof Error ? e.message : '测试失败', statusCode: null } }
  } finally {
    testingId.value = null
  }
}

async function reFetchModels(p: AIProvider) {
  modelsLoading.value = { ...modelsLoading.value, [p.id]: true }
  try {
    const fr = await aiModelsApi.fetchForProvider(p.id)
    fetchResults.value = { ...fetchResults.value, [p.id]: fr }
    const groups = await aiModelsApi.list({ providerId: p.id })
    providerModels.value = { ...providerModels.value, [p.id]: groups.length > 0 ? groups[0].models : [] }
    ElMessage.success(`已拉取 ${fr.fetched} 个模型`)
  } catch (e) {
    ElMessage.error(e instanceof Error ? e.message : '拉取失败')
  } finally {
    modelsLoading.value = { ...modelsLoading.value, [p.id]: false }
  }
}

// ===== 模型操作 =====
async function toggleModelEnabled(m: AIModel, providerId: number) {
  try {
    const updated = await aiModelsApi.toggleEnabled(m.id, !m.enabled)
    const models = (providerModels.value[providerId] ?? []).map(mm => mm.id === m.id ? { ...mm, enabled: updated.enabled } : mm)
    providerModels.value = { ...providerModels.value, [providerId]: models }
  } catch (e) {
    ElMessage.error(e instanceof Error ? e.message : '切换失败')
  }
}

async function copyChatId(chatModelId: string) {
  try {
    await navigator.clipboard.writeText(chatModelId)
    ElMessage.success('已复制')
  } catch {
    ElMessage.error('复制失败')
  }
}

// ===== 模型编辑抽屉 =====
const modelDrawerVisible = ref(false)
const modelSaving = ref(false)
const editingModel = ref<AIModel | null>(null)
const modelForm = reactive({
  alias: '',
  capabilities: [] as string[],
  maxContext: 0,
})

function openModelEdit(m: AIModel) {
  editingModel.value = m
  Object.assign(modelForm, {
    alias: m.alias ?? '',
    capabilities: [...m.capabilities],
    maxContext: m.maxContext,
  })
  modelDrawerVisible.value = true
}

async function saveModel() {
  const m = editingModel.value
  if (!m) return
  modelSaving.value = true
  try {
    const updated = await aiModelsApi.update(m.id, {
      alias: modelForm.alias.trim() || null,
      capabilities: modelForm.capabilities,
      maxContext: modelForm.maxContext,
    })
    const models = (providerModels.value[m.providerId] ?? []).map(mm => mm.id === m.id ? updated : mm)
    providerModels.value = { ...providerModels.value, [m.providerId]: models }
    modelDrawerVisible.value = false
    ElMessage.success('模型已更新')
  } catch (e) {
    ElMessage.error(e instanceof Error ? e.message : '更新失败')
  } finally {
    modelSaving.value = false
  }
}

// ===== 删除 =====
async function deleteProvider(p: AIProvider) {
  try {
    await ElMessageBox.confirm(`确定删除提供方「${p.name}」？此操作不可撤销。`, '删除提供方', { type: 'warning', confirmButtonText: '删除', cancelButtonText: '取消' })
    await aiProvidersApi.remove(p.id)
    if (expandedId.value === p.id) expandedId.value = null
    await loadProviders()
    ElMessage.success('已删除')
  } catch { /* 用户取消 */ }
}

// ===== 新增/编辑抽屉 =====
const drawerVisible = ref(false)
const editingId = ref<number | null>(null)
const formError = ref('')
const form = reactive({
  name: '',
  providerType: 'OpenAI' as AIProviderType,
  endpoint: '',
  apiKey: '',
  isDefault: false,
  timeoutSeconds: 120,
  visionModel: '',
  enableMultimodal: true,
  supportedModels: '',
})

function openCreate() {
  editingId.value = null
  Object.assign(form, { name: '', providerType: 'OpenAI', endpoint: '', apiKey: '', isDefault: false, timeoutSeconds: 120, visionModel: '', enableMultimodal: true, supportedModels: '' })
  formError.value = ''
  drawerVisible.value = true
}

function openEdit(p: AIProvider) {
  editingId.value = p.id
  Object.assign(form, {
    name: p.name,
    providerType: p.providerType,
    endpoint: p.endpoint,
    apiKey: '',
    isDefault: p.isDefault,
    timeoutSeconds: p.timeoutSeconds,
    visionModel: p.visionModel ?? '',
    enableMultimodal: p.enableMultimodal,
    supportedModels: p.supportedModels.join(', '),
  })
  formError.value = ''
  drawerVisible.value = true
}

async function saveProvider() {
  formError.value = ''
  if (!form.name.trim()) { formError.value = '名称不能为空'; return }
  if (!form.endpoint.trim()) { formError.value = '接入地址不能为空'; return }
  if (editingId.value == null && !form.apiKey) { formError.value = '新增时 API Key 不能为空'; return }

  const payload: AIProviderRequest = {
    name: form.name.trim(),
    providerType: form.providerType,
    endpoint: form.endpoint.trim(),
    apiKey: form.apiKey || undefined,
    supportedModels: form.supportedModels.split(',').map(s => s.trim()).filter(Boolean),
    isDefault: form.isDefault,
    timeoutSeconds: form.timeoutSeconds,
    visionModel: form.visionModel || null,
    enableMultimodal: form.enableMultimodal,
  }

  try {
    if (editingId.value == null) {
      await aiProvidersApi.create(payload)
    } else {
      await aiProvidersApi.update(editingId.value, payload)
    }
    drawerVisible.value = false
    await loadProviders()
    ElMessage.success('已保存')
  } catch (e) {
    formError.value = e instanceof Error ? e.message : '保存失败'
  }
}

onMounted(() => { loadProviders() })
</script>

<template>
  <div class="space-y-4">
    <!-- 页头 -->
    <div class="flex items-center justify-between pb-4 border-b border-[var(--el-border-color)]">
      <div class="flex items-center gap-3">
        <h2 class="text-xl font-bold text-[var(--el-text-color-primary)] m-0">AI 提供者</h2>
        <el-tag size="small" type="info">{{ providers.length }} 个提供方</el-tag>
      </div>
      <el-button type="primary" size="small" :icon="Plus" @click="openCreate">
        新建提供方
      </el-button>
    </div>

    <!-- 加载 -->
    <el-skeleton v-if="loading && providers.length === 0" :rows="4" animated />

    <!-- 错误 -->
    <el-alert v-if="error && !loading" :title="error" type="error" show-icon :closable="false" />

    <!-- 空状态 -->
    <el-empty v-if="!loading && providers.length === 0 && !error" description="还没有 AI 提供方，添加一个接入点以启用 AI 能力">
      <el-button type="primary" @click="openCreate">新增提供方</el-button>
    </el-empty>

    <!-- 提供方卡片列表 -->
    <template v-if="providers.length > 0">
      <el-card
        v-for="p in providers"
        :key="p.id"
        shadow="never"
        class="cursor-pointer hover:!border-[var(--el-color-primary)]/50 transition-colors"
        :class="{ '!border-[var(--el-color-primary)]/60': expandedId === p.id }"
        @click="toggleExpand(p.id)"
      >
        <!-- 卡片头部 -->
        <div class="flex items-center justify-between">
          <div class="flex items-center gap-3 min-w-0">
            <el-icon :size="18" class="text-[var(--el-color-accent)] shrink-0"><Connection /></el-icon>
            <div class="min-w-0">
              <div class="flex items-center gap-2">
                <span class="text-sm font-semibold text-[var(--el-text-color-primary)]">{{ p.name }}</span>
                <el-tag size="small" :type="typeTagMap[p.providerType] ?? 'info'" effect="plain">{{ p.providerType }}</el-tag>
                <el-tag v-if="p.isDefault" size="small" type="warning" effect="dark">默认</el-tag>
              </div>
              <div class="text-xs text-[var(--el-text-color-secondary)] mt-0.5 font-mono truncate">{{ p.endpoint }}</div>
            </div>
          </div>
          <div class="flex items-center gap-2 shrink-0" @click.stop>
            <el-tag v-if="providerModels[p.id]" size="small" type="info" effect="plain">{{ providerModels[p.id].length }} 模型</el-tag>
            <el-button size="small" text :icon="Edit" @click="openEdit(p)" />
            <el-button size="small" text :icon="Delete" class="!text-[var(--el-color-error)]" @click="deleteProvider(p)" />
            <el-icon class="text-[var(--el-text-color-secondary)] transition-transform" :class="{ 'rotate-180': expandedId === p.id }"><ArrowDown /></el-icon>
          </div>
        </div>

        <!-- 展开：操作栏 + 模型列表 -->
        <div v-if="expandedId === p.id" class="mt-4 pt-4 border-t border-[var(--el-border-color-light)]" @click.stop>
          <!-- 操作栏（单行：按钮 + 测试结果 + 拉取统计） -->
          <div class="flex items-center gap-2 mb-3 flex-wrap">
            <el-button size="small" :icon="Link" :loading="testingId === p.id" @click="testProvider(p)">
              测试连接
            </el-button>
            <el-button size="small" :icon="Refresh" :loading="modelsLoading[p.id]" @click="reFetchModels(p)">
              获取模型
            </el-button>
            <template v-if="testResults[p.id]">
              <span class="inline-flex items-center gap-1.5 text-xs" :class="testResults[p.id].success ? 'text-[var(--el-color-success)]' : 'text-[var(--el-color-error)]'">
                <span class="w-1.5 h-1.5 rounded-full" :class="testResults[p.id].success ? 'bg-[var(--el-color-success)]' : 'bg-[var(--el-color-error)]'" />
                {{ testResults[p.id].message }}
                <span v-if="testResults[p.id].latencyMs" class="text-[var(--el-text-color-secondary)]">{{ testResults[p.id].latencyMs }}ms</span>
              </span>
            </template>
            <template v-if="fetchResults[p.id]">
              <span class="text-xs text-[var(--el-text-color-secondary)] ml-auto">
                拉取 <b class="text-[var(--el-text-color-regular)]">{{ fetchResults[p.id].fetched }}</b>
                · 新增 <b class="text-[var(--el-color-success)]">{{ fetchResults[p.id].added }}</b>
                · 更新 <b class="text-[var(--el-color-warning)]">{{ fetchResults[p.id].updated }}</b>
                · 保留 <b class="text-[var(--el-text-color-regular)]">{{ fetchResults[p.id].kept }}</b>
              </span>
            </template>
          </div>

          <!-- 模型列表 -->
          <div class="text-xs text-[var(--el-text-color-secondary)] mb-2 font-medium">模型列表</div>

          <p v-if="modelsLoading[p.id]" class="text-sm text-[var(--el-text-color-secondary)] py-4 text-center">加载模型列表…</p>
          <p v-else-if="!providerModels[p.id] || providerModels[p.id].length === 0" class="text-sm text-[var(--el-text-color-secondary)] py-6 text-center">暂无模型，点击「测试连接」自动获取</p>

          <div v-else class="space-y-1">
            <div
              v-for="m in providerModels[p.id]"
              :key="m.id"
              class="flex items-center justify-between py-2 px-3 rounded-md hover:bg-[var(--el-fill-color-light)] transition-colors group"
            >
              <div class="flex items-center gap-2 min-w-0">
                <span class="text-sm text-[var(--el-text-color-regular)]">{{ m.alias || m.upstreamModelId }}</span>
                <span v-if="m.alias" class="text-xs text-[var(--el-text-color-secondary)] font-mono">{{ m.upstreamModelId }}</span>
                <el-tag v-for="cap in m.capabilities" :key="cap" size="small" effect="plain" class="ml-0.5">{{ cap }}</el-tag>
              </div>
              <div class="flex items-center gap-2 shrink-0">
                <el-button size="small" text :icon="CopyDocument" class="opacity-0 group-hover:opacity-100 transition-opacity" @click="copyChatId(m.chatModelId)" />
                <el-button size="small" text :icon="Edit" class="opacity-0 group-hover:opacity-100 transition-opacity" @click="openModelEdit(m)" />
                <el-switch :model-value="m.enabled" size="small" @change="toggleModelEnabled(m, p.id)" @click.stop />
              </div>
            </div>
          </div>
        </div>
      </el-card>

      <p class="text-xs text-[var(--el-text-color-secondary)]">
        密钥以密文存储，列表仅显示掩码；编辑时留空密钥表示保留原值。点击提供方卡片可展开查看模型列表。
      </p>
    </template>

    <!-- 新增/编辑抽屉 -->
    <el-drawer v-model="drawerVisible" :title="editingId == null ? '新增提供方' : '编辑提供方'" size="420px" destroy-on-close>
      <div class="space-y-4">
        <el-alert v-if="formError" :title="formError" type="error" show-icon :closable="false" class="mb-2" />
        <div>
          <label class="block text-sm font-medium text-[var(--el-text-color-primary)] mb-1">显示名称</label>
          <el-input v-model="form.name" placeholder="如 Ollama (本地)" />
        </div>
        <div>
          <label class="block text-sm font-medium text-[var(--el-text-color-primary)] mb-1">类型</label>
          <el-select v-model="form.providerType" class="w-full">
            <el-option label="OpenAI" value="OpenAI" />
            <el-option label="Anthropic" value="Anthropic" />
            <el-option label="Custom" value="Custom" />
          </el-select>
        </div>
        <div>
          <label class="block text-sm font-medium text-[var(--el-text-color-primary)] mb-1">接入地址 (Endpoint)</label>
          <el-input v-model="form.endpoint" placeholder="http://localhost:11434/v1" class="font-mono" />
        </div>
        <div>
          <label class="block text-sm font-medium text-[var(--el-text-color-primary)] mb-1">
            API Key
            <span class="text-xs text-[var(--el-text-color-secondary)] font-normal ml-1">{{ editingId == null ? '必填' : '留空 = 保留原密钥' }}</span>
          </label>
          <el-input v-model="form.apiKey" type="password" show-password placeholder="sk-..." />
        </div>
        <div>
          <label class="block text-sm font-medium text-[var(--el-text-color-primary)] mb-1">超时（秒）</label>
          <el-input-number v-model="form.timeoutSeconds" :min="10" :max="600" class="w-full" />
        </div>
        <div class="flex items-center justify-between">
          <label class="text-sm font-medium text-[var(--el-text-color-primary)]">默认提供方</label>
          <el-switch v-model="form.isDefault" />
        </div>
        <div class="flex items-center justify-between">
          <label class="text-sm font-medium text-[var(--el-text-color-primary)]">启用多模态</label>
          <el-switch v-model="form.enableMultimodal" />
        </div>
        <div>
          <label class="block text-sm font-medium text-[var(--el-text-color-primary)] mb-1">视觉模型</label>
          <el-input v-model="form.visionModel" placeholder="如 gpt-4o" />
        </div>
        <div>
          <label class="block text-sm font-medium text-[var(--el-text-color-primary)] mb-1">
            支持模型
            <span class="text-xs text-[var(--el-text-color-secondary)] font-normal ml-1">逗号分隔</span>
          </label>
          <el-input v-model="form.supportedModels" placeholder="gpt-4o, gpt-4o-mini" />
        </div>
      </div>
      <template #footer>
        <el-button @click="drawerVisible = false">取消</el-button>
        <el-button type="primary" @click="saveProvider">保存</el-button>
      </template>
    </el-drawer>

    <!-- 编辑模型抽屉 -->
    <el-drawer v-model="modelDrawerVisible" title="编辑模型" size="380px" destroy-on-close>
      <div v-if="editingModel" class="space-y-4">
        <div>
          <label class="block text-sm font-medium text-[var(--el-text-color-primary)] mb-1">上游模型 ID</label>
          <el-input :model-value="editingModel.upstreamModelId" disabled class="font-mono" />
        </div>
        <div>
          <label class="block text-sm font-medium text-[var(--el-text-color-primary)] mb-1">ChatModelId</label>
          <el-input :model-value="editingModel.chatModelId" disabled class="font-mono" />
        </div>
        <div>
          <label class="block text-sm font-medium text-[var(--el-text-color-primary)] mb-1">别名</label>
          <el-input v-model="modelForm.alias" placeholder="用户自定义显示名称" clearable />
        </div>
        <div>
          <label class="block text-sm font-medium text-[var(--el-text-color-primary)] mb-1">能力标签</label>
          <el-select v-model="modelForm.capabilities" multiple filterable allow-create default-first-option placeholder="vision, stream, reasoning…" class="w-full" />
        </div>
        <div>
          <label class="block text-sm font-medium text-[var(--el-text-color-primary)] mb-1">最大上下文（tokens，0 = 未设置）</label>
          <el-input-number v-model="modelForm.maxContext" :min="0" :max="1000000" :step="1024" class="w-full" />
        </div>
      </div>
      <template #footer>
        <el-button @click="modelDrawerVisible = false">取消</el-button>
        <el-button type="primary" :loading="modelSaving" @click="saveModel">保存</el-button>
      </template>
    </el-drawer>
  </div>
</template>
