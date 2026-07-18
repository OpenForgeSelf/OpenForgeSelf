<script setup lang="ts">
import { RouterView } from 'vue-router'
import TopNavbar from '@/components/TopNavbar.vue'
</script>

<template>
  <div class="app-layout">
    <TopNavbar />
    <main class="main-content">
      <RouterView />
    </main>
  </div>
</template>

<style scoped>
.app-layout {
  display: flex;
  flex-direction: column;
  height: 100vh;
  overflow: hidden;
  background:
    radial-gradient(circle at top, var(--app-shell-glow), transparent 46%),
    var(--bg-primary);
  color: var(--text-primary);
  transition: background-color var(--motion-base), color var(--motion-base);
}

.main-content {
  flex: 1;
  overflow-y: auto;
  background:
    linear-gradient(180deg, var(--app-content-tint), transparent 18rem),
    var(--bg-primary);
  transition: background-color var(--motion-base);
}
</style>