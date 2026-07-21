<script setup lang="ts">
import { ref } from 'vue'
import { useThemeStore } from '@/stores/theme'
import type { ThemeMode } from '@/stores/theme'

const themeStore = useThemeStore()

const accentColor = ref('amber')
const fontSize = ref(15)
const animationEnabled = ref(true)

const colorOptions: { key: string; color: string; label: string }[] = [
  { key: 'amber', color: '#F59E0B', label: '琥珀色' },
  { key: 'blue', color: '#58A6FF', label: '蓝色' },
  { key: 'green', color: '#3FB950', label: '绿色' },
  { key: 'purple', color: '#A371F7', label: '紫色' },
  { key: 'red', color: '#F85149', label: '红色' },
]

function setTheme(mode: ThemeMode): void {
  themeStore.setMode(mode)
}
</script>

<template>
  <div class="space-y-6">
    <!-- 页头 -->
    <div class="flex items-start justify-between pb-4 border-b border-border">
      <div>
        <h2 class="text-xl font-bold text-text m-0 leading-tight">外观</h2>
        <p class="text-sm text-text-regular mt-1 mb-0">主题、配色与视觉偏好</p>
      </div>
    </div>

    <!-- 设置项列表 -->
    <el-card shadow="never">
      <!-- 主题 -->
      <div class="flex items-center justify-between py-3 border-b border-border-light">
        <div>
          <span class="text-sm font-medium text-text">主题</span>
          <p class="text-xs text-text-secondary mt-0.5 mb-0">界面明暗模式</p>
        </div>
        <el-radio-group :model-value="themeStore.mode" @update:model-value="(val: ThemeMode) => setTheme(val)">
          <el-radio-button value="light">浅色</el-radio-button>
          <el-radio-button value="dark">深色</el-radio-button>
          <el-radio-button value="system">跟随系统</el-radio-button>
        </el-radio-group>
      </div>

      <!-- 强调色 -->
      <div class="flex items-center justify-between py-3 border-b border-border-light">
        <div>
          <span class="text-sm font-medium text-text">强调色</span>
          <p class="text-xs text-text-secondary mt-0.5 mb-0">按钮、链接与交互元素的颜色</p>
        </div>
        <div class="flex items-center gap-2">
          <button
            v-for="opt in colorOptions"
            :key="opt.key"
            class="w-6 h-6 rounded-full cursor-pointer border-2 transition-transform hover:scale-110"
            :class="accentColor === opt.key ? 'border-text scale-110' : 'border-transparent'"
            :style="{ background: opt.color }"
            :aria-label="opt.label"
            @click="accentColor = opt.key"
          />
        </div>
      </div>

      <!-- 字体大小 -->
      <div class="flex items-center justify-between py-3 border-b border-border-light">
        <div>
          <span class="text-sm font-medium text-text">字体大小</span>
          <p class="text-xs text-text-secondary mt-0.5 mb-0">界面文字的缩放比例</p>
        </div>
        <div class="flex items-center gap-2 w-[200px]">
          <span class="text-xs text-text-secondary">A</span>
          <el-slider v-model="fontSize" :min="12" :max="20" :step="1" :show-tooltip="false" class="flex-1" />
          <span class="text-lg text-text-secondary">A</span>
        </div>
      </div>

      <!-- 动画效果 -->
      <div class="flex items-center justify-between py-3 last:border-b-0">
        <div>
          <span class="text-sm font-medium text-text">动画效果</span>
          <p class="text-xs text-text-secondary mt-0.5 mb-0">界面过渡与微交互动画</p>
        </div>
        <el-switch v-model="animationEnabled" />
      </div>
    </el-card>
  </div>
</template>
