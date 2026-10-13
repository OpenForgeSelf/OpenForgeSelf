<script setup lang="ts">
import { ref } from 'vue'
import { UploadFilled } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import {
  useAppearanceStore,
  BG_SOLID_PRESETS,
  BG_IMAGE_PRESETS,
  DEFAULT_BG_SOLID,
  DEFAULT_OVERLAY_OPACITY,
} from '@/stores/appearance'

// 背景设置内联组件（设计稿 kit-styles.css:448-568 / App.jsx:82-165）：
// 纯色 12 档 6 列网格；图 6 预设 3 列 60px；上传本地图片；URL 输入；
// 遮罩滑竿（0-90，金 accent + mono 值）；恢复默认。
// 右上角浮层（BackgroundPanel.vue）与设置-外观页（AppearancePanel.vue）共用此组件，避免两处实现。
const appearanceStore = useAppearanceStore()
const fileInput = ref<HTMLInputElement | null>(null)

// URL 输入
const customUrl = ref('')
const isLoadingUrl = ref(false)

function selectSolid(color: string): void {
  appearanceStore.selectSolidColor(color)
}

function selectImage(url: string): void {
  appearanceStore.selectImageBackground(url)
}

function onUploadChange(event: Event): void {
  const input = event.target as HTMLInputElement | null
  const file = input?.files?.[0]
  if (!file) return
  const reader = new FileReader()
  reader.onload = () => {
    if (typeof reader.result === 'string') {
      appearanceStore.selectImageBackground(reader.result)
      ElMessage.success({ message: '背景图片已设置', offset: 60 })
    }
  }
  reader.readAsDataURL(file)
  input.value = ''
}

function applyUrl(): void {
  const url = customUrl.value.trim()
  if (!url) {
    ElMessage.warning({ message: '请输入图片 URL', offset: 60 })
    return
  }
  // 允许相对路径（如 /backgrounds/xxx.jpg）或 http(s)
  const isRelative = url.startsWith('/') || url.startsWith('./')
  if (!isRelative) {
    try {
      const parsed = new URL(url)
      if (parsed.protocol !== 'http:' && parsed.protocol !== 'https:') {
        ElMessage.warning({ message: '请输入有效的 http/https 图片 URL 或相对路径', offset: 60 })
        return
      }
    } catch {
      ElMessage.warning({ message: '请输入有效的图片 URL', offset: 60 })
      return
    }
  }
  // 预加载验证
  isLoadingUrl.value = true
  const img = new Image()
  img.onload = () => {
    appearanceStore.selectImageBackground(url)
    isLoadingUrl.value = false
    ElMessage.success({ message: '背景图片已设置', offset: 60 })
  }
  img.onerror = () => {
    isLoadingUrl.value = false
    ElMessage.error({ message: '图片加载失败，请检查 URL 是否正确', offset: 60 })
  }
  img.src = url
}

function clearImage(): void {
  customUrl.value = ''
  appearanceStore.clearBackgroundImage()
  ElMessage.success({ message: '背景图片已清除', offset: 60 })
}

function resetDefaults(): void {
  appearanceStore.selectSolidColor(DEFAULT_BG_SOLID)
  appearanceStore.setOverlayOpacity(DEFAULT_OVERLAY_OPACITY)
  ElMessage.success({ message: '已恢复默认', offset: 60 })
}
</script>

<template>
  <div class="bg-settings">
    <!-- 纯色（已隐藏：主题切换自动联动背景色，不再独立选） -->
    <!-- <div class="row-label">纯色</div>
    <div class="bg-swatches">
      <div
        v-for="s in BG_SOLID_PRESETS"
        :key="s.value"
        class="bg-swatch"
        :class="{
          active: appearanceStore.backgroundMode === 'solid' && appearanceStore.solidColor === s.value,
        }"
        :style="{
          background: s.value,
          borderColor: s.value === '#F4EEDF' || s.value === '#FBF7EE' ? 'color-mix(in srgb, var(--ink-950) 15%, transparent)' : undefined,
        }"
        :title="s.label"
        @click="selectSolid(s.value)"
      />
    </div> -->

    <!-- 预设背景图 -->
    <div class="row-label">背景图</div>
    <div class="bg-presets">
      <div
        v-for="p in BG_IMAGE_PRESETS"
        :key="p.url"
        class="bg-preset"
        :class="{ active: appearanceStore.backgroundMode === 'image' && appearanceStore.backgroundImage === p.url }"
        :style="{ backgroundImage: `url(${p.url})` }"
        @click="selectImage(p.url)"
      >
        <div class="label">{{ p.label }}</div>
      </div>
    </div>

    <!-- 上传本地图片 -->
    <input
      ref="fileInput"
      type="file"
      accept="image/*"
      style="display: none"
      @change="onUploadChange"
    />
    <button class="upload-btn" @click="fileInput?.click()">
      <el-icon :size="13"><UploadFilled /></el-icon>
      上传本地图片
    </button>

    <!-- 自定义 URL -->
    <div class="row-label">自定义 URL</div>
    <div class="url-row">
      <input
        v-model="customUrl"
        class="url-input"
        type="text"
        placeholder="输入图片 URL，如 /backgrounds/xxx.jpg 或 https://…"
        @keyup.enter="applyUrl"
      />
      <button class="url-apply" :disabled="isLoadingUrl" @click="applyUrl">
        {{ isLoadingUrl ? '加载中…' : '应用' }}
      </button>
      <button
        v-if="appearanceStore.backgroundImage"
        class="url-clear"
        @click="clearImage"
      >清除</button>
    </div>

    <!-- 遮罩浓度 -->
    <div class="row-label">遮罩浓度</div>
    <div class="slider-row">
      <span>淡化</span>
      <input
        type="range"
        min="0"
        max="90"
        step="1"
        :value="appearanceStore.overlayOpacity"
        @input="appearanceStore.setOverlayOpacity(Number(($event.target as HTMLInputElement).value))"
      />
      <span class="slider-val">{{ appearanceStore.overlayOpacity }}%</span>
    </div>

    <button class="reset-btn" @click="resetDefaults">恢复默认</button>
  </div>
</template>

<style scoped>
.bg-settings {
  display: flex;
  flex-direction: column;
}

.row-label {
  font-family: var(--font-mono);
  font-size: 10px;
  letter-spacing: 0.12em;
  color: var(--fg-tertiary);
  text-transform: uppercase;
  margin: 10px 0 8px;
}

.bg-swatches {
  display: grid;
  grid-template-columns: repeat(6, 1fr);
  gap: 8px;
}

.bg-swatch {
  aspect-ratio: 1;
  border-radius: 7px;
  cursor: pointer;
  border: 2px solid transparent;
  transition: all var(--dur-base);
}

.bg-swatch:hover {
  transform: scale(1.06);
}

.bg-swatch.active {
  border-color: var(--el-color-primary);
  box-shadow: var(--ring-active);
}

.bg-presets {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 8px;
  margin-top: 6px;
}

.bg-preset {
  height: 60px;
  border-radius: 7px;
  cursor: pointer;
  border: 2px solid transparent;
  background-size: cover;
  background-position: center;
  transition: all var(--dur-base);
  position: relative;
  overflow: hidden;
}

.bg-preset:hover {
  transform: scale(1.03);
}

.bg-preset.active {
  border-color: var(--el-color-primary);
  box-shadow: var(--ring-active);
}

.bg-preset .label {
  position: absolute;
  left: 0;
  right: 0;
  bottom: 0;
  font-family: var(--font-mono);
  font-size: 9px;
  padding: 2px 6px;
  background: linear-gradient(180deg, transparent, color-mix(in srgb, var(--ink-950) 75%, transparent));
  color: var(--paper-white);
  letter-spacing: 0.05em;
}

.upload-btn {
  width: 100%;
  margin-top: 10px;
  padding: 10px;
  background: var(--paper-tint-weak);
  border: 1px dashed var(--border-default);
  border-radius: 8px;
  color: var(--fg-secondary);
  cursor: pointer;
  font-family: var(--font-sans-cn);
  font-size: 12px;
  transition: all var(--dur-base);
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
}

.upload-btn:hover {
  border-color: var(--el-color-primary);
  color: var(--el-color-primary);
  background: var(--gold-tint);
}

.url-row {
  display: flex;
  gap: 6px;
  margin-top: 4px;
}

.url-input {
  flex: 1;
  min-width: 0;
  padding: 6px 10px;
  background: var(--paper-tint-weak);
  border: 1px solid var(--border-default);
  border-radius: 6px;
  color: var(--fg-primary);
  font-family: var(--font-mono);
  font-size: 11px;
  outline: none;
  transition: border-color var(--dur-base);
}

.url-input:focus {
  border-color: var(--el-color-primary);
}

.url-input::placeholder {
  color: var(--fg-tertiary);
}

.url-apply {
  padding: 6px 12px;
  background: var(--el-color-primary);
  border: 0;
  border-radius: 6px;
  color: var(--paper-white);
  font-size: 12px;
  cursor: pointer;
  transition: opacity var(--dur-base);
}

.url-apply:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.url-clear {
  padding: 6px 10px;
  background: transparent;
  border: 1px solid var(--border-default);
  border-radius: 6px;
  color: var(--fg-secondary);
  font-size: 12px;
  cursor: pointer;
  transition: all var(--dur-base);
}

.url-clear:hover {
  border-color: var(--el-color-danger);
  color: var(--el-color-danger);
}

.slider-row {
  display: flex;
  align-items: center;
  gap: 10px;
  font-size: 11px;
  color: var(--fg-secondary);
}

.slider-row input[type='range'] {
  flex: 1;
  accent-color: var(--el-color-primary);
}

.slider-val {
  font-family: var(--font-mono);
  font-size: 10px;
  color: var(--el-color-primary);
  min-width: 36px;
  text-align: right;
}

.reset-btn {
  margin-top: 12px;
  width: 100%;
  padding: 7px;
  background: transparent;
  border: 1px solid var(--border-default);
  border-radius: 6px;
  color: var(--fg-secondary);
  font-size: 12px;
  cursor: pointer;
  transition: all var(--dur-base);
}

.reset-btn:hover {
  border-color: var(--el-color-primary);
  color: var(--el-color-primary);
}
</style>
