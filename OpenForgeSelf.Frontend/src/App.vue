<script setup lang="ts">
import { onMounted, watch } from 'vue'
import { RouterView } from 'vue-router'
import TopNavbar from '@/components/TopNavbar.vue'
import { useAppearanceStore } from '@/stores/appearance'

const appearanceStore = useAppearanceStore()

onMounted(() => {
  appearanceStore.initialize()
})

// 背景图模式标记挂到 <body>：让 bg-image-mode.css 能选中 teleport 到 body 的弹窗（ElDialog append-to-body）
watch(() => appearanceStore.backgroundImage, (val) => {
  document.body.classList.toggle('app-bg-image-mode', !!val)
}, { immediate: true })
</script>

<template>
  <!-- 实心主题色垫底：对应 VS Code body 实底色，保证背景图之下始终有主题色兜底，文字可读 -->
  <div
    v-if="appearanceStore.backgroundImage"
    class="app-bg-backing"
    aria-hidden="true"
  />
  <!-- 背景图片（fixed cover）；透明度由「背景图透明度」滑块唯一控制，0 模糊，不叠加任何遮罩/毛玻璃 -->
  <img
    v-if="appearanceStore.backgroundImage"
    :src="appearanceStore.backgroundImage"
    :style="{ opacity: appearanceStore.backgroundOpacity / 100 }"
    class="app-bg"
    alt=""
  />
  <div
    class="app-layout"
    :class="{ 'has-bg-image': !!appearanceStore.backgroundImage }"
  >
    <TopNavbar />
    <main class="main-content">
      <RouterView />
    </main>
  </div>
</template>

<style scoped>
.app-bg-backing {
  position: fixed;
  inset: 0;
  z-index: 0;
  background-color: var(--el-bg-color);
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

.app-layout {
  position: relative;
  z-index: 2;
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
   本文件只保留结构层：垫底实色 .app-bg-backing、背景图 .app-bg、布局 .app-layout。 */

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
</style>
