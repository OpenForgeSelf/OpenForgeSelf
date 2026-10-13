<script setup lang="ts">
import { ref, watch, onMounted } from 'vue';
import { useThemeStore } from '@/stores/theme';
import type { ThemeMode } from '@/stores/theme';
import BackgroundSettings from '@/components/BackgroundSettings.vue';

const themeStore = useThemeStore();

const accentColor = ref('amber');
const FONT_SIZE_KEY = 'forgeself:fontSize';
const fontSize = ref(15);
const animationEnabled = ref(true);

function applyFontSize(px: number) {
  document.documentElement.style.fontSize = `${px}px`;
}

onMounted(() => {
  const saved = Number(localStorage.getItem(FONT_SIZE_KEY));
  if (saved >= 12 && saved <= 20) {
    fontSize.value = saved;
    applyFontSize(saved);
  } else {
    applyFontSize(fontSize.value);
  }
});

watch(fontSize, (val) => {
  applyFontSize(val);
  localStorage.setItem(FONT_SIZE_KEY, String(val));
});

const colorOptions: { key: string; color: string; label: string }[] = [
  { key: 'amber', color: '#F59E0B', label: '琥珀色' },
  { key: 'blue', color: '#58A6FF', label: '蓝色' },
  { key: 'green', color: '#3FB950', label: '绿色' },
  { key: 'purple', color: '#A371F7', label: '紫色' },
  { key: 'red', color: '#F85149', label: '红色' }
];

function setTheme(mode: ThemeMode): void {
  themeStore.setMode(mode);
}
</script>

<template>
  <div class="space-y-6">
    <!-- 基础设置 -->
    <el-card shadow="never">
      <!-- 主题 -->
      <div class="flex items-center justify-between py-3 border-b border-border-light">
        <div>
          <span class="text-sm font-medium text-text">主题</span>
          <p class="text-xs text-text-secondary mt-0.5 mb-0">界面明暗模式</p>
        </div>
        <el-radio-group
          :model-value="themeStore.mode"
          @update:model-value="(val: ThemeMode) => setTheme(val)">
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
            @click="accentColor = opt.key" />
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
          <el-slider
            v-model="fontSize"
            :min="12"
            :max="20"
            :step="1"
            :show-tooltip="false"
            class="flex-1" />
          <span class="text-lg text-text-secondary">A</span>
        </div>
      </div>

      <!-- 动画效果 -->
      <div class="flex items-center justify-between py-3">
        <div>
          <span class="text-sm font-medium text-text">动画效果</span>
          <p class="text-xs text-text-secondary mt-0.5 mb-0">界面过渡与微交互动画</p>
        </div>
        <el-switch v-model="animationEnabled" />
      </div>
    </el-card>

    <!-- 背景设置（复用右上角浮层同款组件，单一实现） -->
    <el-card shadow="never">
      <div class="pb-3 border-b border-border-light">
        <span class="text-sm font-medium text-text">背景</span>
        <p class="text-xs text-text-secondary mt-0.5 mb-0">纯色 / 预设图 / 自定义 URL / 上传本地图片</p>
      </div>
      <div class="pt-4">
        <BackgroundSettings />
      </div>
    </el-card>
  </div>
</template>
