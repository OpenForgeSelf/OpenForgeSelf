<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { Plus, FolderOpened } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { pluginApi } from '@/services/pluginApi'

interface PluginItem {
  name: string
  icon: string
  version: string
  enabled: boolean
}

const plugins = ref<PluginItem[]>([
  { name: 'JSON 格式化', icon: 'M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8l-6-6zM6 20V4h7v5h5v11H6z', version: 'v1.2.0', enabled: true },
  { name: '正则测试器', icon: 'M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8l-6-6zM6 20V4h7v5h5v11H6z', version: 'v1.0.3', enabled: true },
  { name: '图片压缩', icon: 'M21 19V5a2 2 0 00-2-2H5a2 2 0 00-2 2v14a2 2 0 002 2h14a2 2 0 002-2zM8.5 10a1.5 1.5 0 100-3 1.5 1.5 0 000 3zm10.5 5l-4-4-4 4-3-3-4 4', version: 'v0.9.1', enabled: false },
])

function togglePlugin(index: number): void {
  plugins.value[index].enabled = !plugins.value[index].enabled
}

// ── 插件更新源（本地包目录，2026-09-28 输入27；与宿主「版本更新-更新源配置」分离） ──
const localDir = ref('')
const saving = ref(false)

async function loadUpdateSource(): Promise<void> {
  try {
    const settings = await pluginApi.fetchPluginUpdateSettings()
    localDir.value = settings.localDir ?? ''
  } catch (e) {
    ElMessage.error({ message: `加载插件更新源配置失败: ${e instanceof Error ? e.message : e}`, offset: 60 })
  }
}

async function onSaveUpdateSource(): Promise<void> {
  if (saving.value) return
  saving.value = true
  try {
    const saved = await pluginApi.updatePluginUpdateSettings({ localDir: localDir.value.trim() })
    localDir.value = saved.localDir ?? ''
    ElMessage.success({ message: saved.localDir ? '插件更新源已保存' : '插件更新源已停用（留空即停用）', offset: 60 })
  } catch (e) {
    ElMessage.error({ message: `保存插件更新源配置失败: ${e instanceof Error ? e.message : e}`, offset: 60 })
  } finally {
    saving.value = false
  }
}

onMounted(loadUpdateSource)
</script>

<template>
  <div class="space-y-6">
    <!-- 页头 -->
    <div class="flex items-start justify-between pb-4 border-b border-border">
      <div>
        <h2 class="text-xl font-bold text-text m-0 leading-tight">插件管理</h2>
        <p class="text-sm text-text-regular mt-1 mb-0">启用、禁用或导入第三方插件</p>
      </div>
    </div>

    <!-- 插件更新源（本地包目录） -->
    <el-card shadow="never">
      <div class="flex items-center justify-between mb-3">
        <div>
          <div class="text-base font-medium text-text">插件更新源</div>
          <div class="text-xs text-text-secondary mt-1">
            配置插件本地包目录后，插件市场「检查更新」会扫描目录内 .forgeself-plugin 包并发现更高版本（与宿主更新互不影响）
          </div>
        </div>
      </div>
      <div class="flex items-start gap-3">
        <el-input
          v-model="localDir"
          placeholder="插件包目录，如 D:\plugin-packages；留空 = 停用插件更新源"
          :disabled="saving"
          style="flex: 1"
        />
        <el-button type="primary" :loading="saving" @click="onSaveUpdateSource">保存</el-button>
      </div>
    </el-card>

    <!-- 插件列表 -->
    <el-card shadow="never">
      <div
        v-for="(plugin, index) in plugins"
        :key="plugin.name"
        class="flex items-center justify-between py-3 border-b border-border-light last:border-b-0"
      >
        <div class="flex items-center gap-3">
          <svg
            class="w-[18px] h-[18px] text-text-secondary shrink-0"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
            stroke-linejoin="round"
            aria-hidden="true"
          >
            <path :d="plugin.icon" />
          </svg>
          <div>
            <span class="text-sm font-medium text-text">{{ plugin.name }}</span>
            <p class="text-xs text-text-secondary mt-0.5 mb-0">{{ plugin.version }}</p>
          </div>
        </div>
        <el-switch
          :model-value="plugin.enabled"
          @update:model-value="togglePlugin(index)"
        />
      </div>
    </el-card>

    <!-- 操作按钮 -->
    <div class="flex items-center gap-3">
      <el-button :icon="Plus">导入插件</el-button>
      <el-button :icon="FolderOpened">打开插件目录</el-button>
    </div>
  </div>
</template>
