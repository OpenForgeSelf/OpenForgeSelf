<script setup lang="ts">
import { ref } from 'vue'
import { Plus, FolderOpened } from '@element-plus/icons-vue'

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
