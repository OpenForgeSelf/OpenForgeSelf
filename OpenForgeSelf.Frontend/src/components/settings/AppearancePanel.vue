<script setup lang="ts">
import { ref, watch } from 'vue';
import { ElMessage } from 'element-plus';
import { useThemeStore } from '@/stores/theme';
import { useAppearanceStore } from '@/stores/appearance';
import type { ThemeMode } from '@/stores/theme';

const themeStore = useThemeStore();
const appearanceStore = useAppearanceStore();

const accentColor = ref('amber');
const fontSize = ref(15);
const animationEnabled = ref(true);

// 背景图片
const bgImageUrl = ref(appearanceStore.backgroundImage || '');
const isSettingImage = ref(false);

// 背景图透明度
const backgroundOpacity = ref(appearanceStore.backgroundOpacity);
watch(backgroundOpacity, (val) => {
  appearanceStore.setBackgroundOpacity(val);
});

function validateUrl(url: string): boolean {
  if (!url.trim()) return false;
  try {
    const parsed = new URL(url);
    return parsed.protocol === 'http:' || parsed.protocol === 'https:';
  } catch {
    return false;
  }
}

function setImage(): void {
  const url = bgImageUrl.value.trim();

  // 空 URL → 清除
  if (!url) {
    appearanceStore.clearBackgroundImage();
    ElMessage.success({ message: '背景图片已清除', offset: 60 });
    return;
  }

  // URL 格式校验
  if (!validateUrl(url)) {
    ElMessage.warning({ message: '请输入有效的 http/https 图片 URL', offset: 60 });
    return;
  }

  // 预加载图片，验证可访问性
  isSettingImage.value = true;
  const img = new Image();
  img.onload = () => {
    appearanceStore.setBackgroundImage(url);
    isSettingImage.value = false;
    ElMessage.success({ message: '背景图片已设置', offset: 60 });
  };
  img.onerror = () => {
    isSettingImage.value = false;
    ElMessage.error({ message: '图片加载失败，请检查 URL 是否正确', offset: 60 });
  };
  img.src = url;
}

function clearImage(): void {
  bgImageUrl.value = '';
  appearanceStore.clearBackgroundImage();
  ElMessage.success({ message: '背景图片已清除', offset: 60 });
}

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
      <div class="flex items-center justify-between py-3 border-b border-border-light">
        <div>
          <span class="text-sm font-medium text-text">动画效果</span>
          <p class="text-xs text-text-secondary mt-0.5 mb-0">界面过渡与微交互动画</p>
        </div>
        <el-switch v-model="animationEnabled" />
      </div>

      <!-- 背景图片 -->
      <div class="py-3 last:border-b-0">
        <div class="flex items-start justify-between">
          <div class="flex-shrink-0">
            <span class="text-sm font-medium text-text">背景图片</span>
            <p class="text-xs text-text-secondary mt-0.5 mb-0">应用背景图片 URL</p>
          </div>
          <div class="flex flex-col items-end gap-2 ml-4 min-w-0">
            <div class="flex items-center gap-2">
              <el-input
                v-model="bgImageUrl"
                placeholder="输入图片 URL..."
                clearable
                class="w-[320px]"
                :maxlength="2048"
                @keyup.enter="setImage" />
              <el-button type="primary" :loading="isSettingImage" @click="setImage">设置</el-button>
              <el-button :disabled="!appearanceStore.backgroundImage" @click="clearImage">
                清除
              </el-button>
            </div>
            <div
              v-if="appearanceStore.backgroundImage"
              class="w-[120px] h-[68px] rounded border border-border-light bg-cover bg-center bg-no-repeat"
              :style="{ backgroundImage: `url(${appearanceStore.backgroundImage})` }"
              title="当前背景图片预览" />
          </div>
        </div>
      </div>

      <!-- 背景图透明度 -->
      <div class="py-3 border-t border-border-light">
        <div class="flex items-center justify-between">
          <div>
            <span class="text-sm font-medium text-text">背景图透明度</span>
            <p class="text-xs text-text-secondary mt-0.5 mb-0">控制背景图片的清晰程度</p>
          </div>
          <div class="flex items-center gap-2 w-[240px]">
            <span class="text-xs text-text-secondary">淡化</span>
            <el-slider
              v-model="backgroundOpacity"
              :min="0"
              :max="100"
              :step="1"
              :disabled="!appearanceStore.backgroundImage"
              :show-tooltip="true"
              class="flex-1" />
            <span class="text-xs text-text-secondary w-[50px] text-right">
              {{ backgroundOpacity }}%
            </span>
          </div>
        </div>
      </div>
    </el-card>
  </div>
</template>
