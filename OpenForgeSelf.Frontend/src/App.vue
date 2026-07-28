<script setup lang="ts">
import { onMounted } from 'vue'
import { RouterView } from 'vue-router'
import TopNavbar from '@/components/TopNavbar.vue'
import { useAppearanceStore } from '@/stores/appearance'

const appearanceStore = useAppearanceStore()

onMounted(() => {
  appearanceStore.initialize()
})
</script>

<template>
  <img
    v-if="appearanceStore.backgroundImage"
    :src="appearanceStore.backgroundImage"
    :style="{ opacity: appearanceStore.backgroundOpacity / 100 }"
    class="app-bg"
    alt=""
  />
  <!-- Semi-transparent overlay for FR-008 content readability -->
  <div
    v-if="appearanceStore.backgroundImage"
    class="app-bg-overlay"
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
.app-bg {
  position: fixed;
  inset: 0;
  width: 100%;
  height: 100%;
  object-fit: cover;
  z-index: 0;
  pointer-events: none;
}

.app-bg-overlay {
  position: fixed;
  inset: 0;
  z-index: 1;
  background: rgba(0, 0, 0, 0.35);
  pointer-events: none;
}

.app-layout {
  position: relative;
  z-index: 2;
  display: flex;
  flex-direction: column;
  height: 100vh;
  overflow: hidden;
  background-image:
    radial-gradient(circle at top, var(--app-shell-glow), transparent 46%);
  background-color: var(--bg-primary);
  color: var(--text-primary);
  transition: background-color var(--motion-base), background var(--motion-base), color var(--motion-base);
  --topnav-bg: var(--el-bg-color);
  --content-bg: var(--bg-primary);
}

.app-layout.has-bg-image {
  background-color: transparent;
  background-image: none;
  --topnav-bg: color-mix(in srgb, var(--el-bg-color) 85%, transparent);
  --content-bg: color-mix(in srgb, var(--bg-primary) 70%, transparent);
}

.main-content {
  position: relative;
  z-index: 1;
  flex: 1;
  overflow-y: auto;
  background:
    linear-gradient(180deg, var(--app-content-tint, transparent), transparent 18rem),
    var(--content-bg, transparent);
  transition: background-color var(--motion-base), background var(--motion-base);
}

.top-navbar {
  transition: background-color var(--motion-base), background var(--motion-base);
}
</style>