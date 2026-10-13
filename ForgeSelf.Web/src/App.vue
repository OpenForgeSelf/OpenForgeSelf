<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import { RouterView } from 'vue-router'
import { MagicStick } from '@element-plus/icons-vue'
import TopNavbar from '@/components/TopNavbar.vue'
import StatusBar from '@/components/StatusBar.vue'
import { useAppearanceStore } from '@/stores/appearance'
import { useOpenPage } from '@/composables/useOpenPage'

const appearanceStore = useAppearanceStore()
const { openPage } = useOpenPage()

onMounted(() => {
  appearanceStore.initialize()
})

// 图片加载失败 → 降级回纯色（DESIGN.md「Error Handling」：不白屏、不报错堆栈）
const isImageBroken = ref(false)
function onBackgroundError(): void {
  isImageBroken.value = true
  appearanceStore.clearBackgroundImage()
}

// 背景图模式标记挂到 <body>：让 bg-image-mode.css 能选中 teleport 到 body 的弹窗（ElDialog append-to-body）
watch(() => appearanceStore.backgroundImage, (val) => {
  document.body.classList.toggle('app-bg-image-mode', !!val)
  isImageBroken.value = false
}, { immediate: true })

// 遮罩浓度挂到 <body> CSS 变量（设计稿 body::before 遮罩机制）
// 设计稿规则（App.jsx:168）：图片模式用用户滑块值；纯色模式自动压 15%（浅米色 #F4EEDF/#FBF7EE 压 0%）
function effectiveOverlay(): number {
  if (appearanceStore.backgroundMode === 'image') {
    return appearanceStore.overlayOpacity / 100
  }
  const c = appearanceStore.solidColor
  if (c === '#F4EEDF' || c === '#FBF7EE') return 0
  return 0.15
}
watch(
  () => [appearanceStore.overlayOpacity, appearanceStore.backgroundMode, appearanceStore.solidColor],
  () => {
    document.body.style.setProperty('--overlay-opacity', String(effectiveOverlay()))
  },
  { immediate: true },
)
</script>

<template>
  <!-- 实心主题色垫底：背景图/纯色之下始终有兜底色，文字可读 -->
  <div
    class="app-bg-backing"
    :style="{ backgroundColor: appearanceStore.solidColor }"
    aria-hidden="true"
  />
  <!-- 纯色背景（设计稿 12 档纯色预设，mode=solid） -->
  <div
    v-if="!appearanceStore.backgroundImage"
    class="app-bg-solid"
    :style="{ backgroundColor: appearanceStore.solidColor }"
    aria-hidden="true"
  />
  <!-- 背景图片（fixed cover，全不透明铺满；遮罩浓度由 .app-bg-overlay 层控制）；加载失败降级纯色 -->
  <img
    v-if="appearanceStore.backgroundImage && !isImageBroken"
    :src="appearanceStore.backgroundImage"
    class="app-bg"
    alt=""
    @error="onBackgroundError"
  />
  <!-- 遮罩层（设计稿：body::before 固定遮罩，浓度 0-90% 默认 55%，保证前景可读） -->
  <div class="app-bg-overlay" aria-hidden="true" />
  <div
    class="app-layout"
    :class="{ 'has-bg-image': !!appearanceStore.backgroundImage }"
    :style="!appearanceStore.backgroundImage ? { backgroundColor: appearanceStore.solidColor } : {}"
  >
    <TopNavbar />
    <main class="main-content">
      <RouterView />
    </main>
    <StatusBar />
  </div>
  <!-- 助手气泡（设计稿 kit-styles.css:430-445：右下 40px 圆钮，glass+金边+forge 光晕） -->
  <button
    class="assistant-bubble"
    title="召唤助手"
    aria-label="召唤助手"
    @click="openPage('/ai-agent', 'AI Agent')"
  >
    <el-icon :size="18"><MagicStick /></el-icon>
  </button>
</template>

<style scoped>
.app-bg-backing {
  position: fixed;
  inset: 0;
  z-index: 0;
  background-color: var(--el-bg-color);
  pointer-events: none;
}

.app-bg-solid {
  position: fixed;
  inset: 0;
  z-index: 1;
  pointer-events: none;
}

.app-bg {
  position: fixed;
  inset: 0;
  width: 100%;
  height: 100%;
  object-fit: cover;
  z-index: 1;
  pointer-events: none;
  filter: blur(0px);
}

/* 设计稿遮罩：顶部弱（5%）向底部浓（overlay 浓度）的墨黑渐变，保证前景可读 */
.app-bg-overlay {
  position: fixed;
  inset: 0;
  z-index: 2;
  pointer-events: none;
  background-image: linear-gradient(
    180deg,
    rgba(11, 9, 8, calc(var(--overlay-opacity, 0.55) - 0.05)) 0%,
    rgba(11, 9, 8, var(--overlay-opacity, 0.55)) 100%
  );
  transition: opacity 360ms ease-out;
}

.app-layout {
  position: relative;
  z-index: 3;
  display: flex;
  flex-direction: column;
  height: 100vh;
  overflow: hidden;
  background-image:
    radial-gradient(circle at top, color-mix(in srgb, var(--el-color-primary) 12%, transparent), transparent 46%);
  background-color: var(--el-bg-color);
  color: var(--el-text-color-primary);
  transition: background-color 150ms ease, background 150ms ease, color 150ms ease;
  --topnav-bg: var(--el-bg-color);
  --content-bg: var(--el-bg-color);
}

/* 背景图片模式的「表面透明」规则已移至独立文件 themes/bg-image-mode.css
   （.app-layout.has-bg-image 块，唯一来源，含 surface 层与适配清单），此处不再散落 :deep 覆盖。
   本文件只保留结构层：垫底实色 .app-bg-backing、纯色 .app-bg-solid、背景图 .app-bg、
   遮罩 .app-bg-overlay、布局 .app-layout。 */

.main-content {
  position: relative;
  z-index: 1;
  flex: 1;
  overflow-y: auto;
  background: transparent;
  transition: background-color 150ms ease, background 150ms ease;
}

.top-navbar {
  transition: background-color 150ms ease, background 150ms ease;
}

/* 助手气泡：右下 40px 圆钮（right:24px bottom:44px），hover scale 1.06 是唯一缩放例外 */
.assistant-bubble {
  position: fixed;
  right: 24px;
  bottom: 44px;
  width: 40px;
  height: 40px;
  border-radius: 50%;
  background: var(--glass-strong-bg);
  backdrop-filter: blur(20px);
  -webkit-backdrop-filter: blur(20px);
  border: 1px solid var(--border-brand);
  color: var(--el-color-primary);
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  box-shadow: var(--bubble-shadow);
  z-index: 30;
  transition: all 0.2s;
}

.assistant-bubble:hover {
  transform: scale(1.06);
  border-color: var(--border-brand-hover);
}
</style>
