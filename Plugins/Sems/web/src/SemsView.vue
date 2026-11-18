<script setup lang="ts">
/**
 * sems 插件界面入口。/sems 只有工作台这一套产品界面（需求 F26）。
 * 经典实现仍留在 LegacySemsView.vue 便于对照，产品界面不再提供入口。
 */
import { onMounted } from 'vue'
import { loadProjects } from './workbench/workbenchStore'
import WorkbenchView from './workbench/WorkbenchView.vue'
import './workbench/workbench.css'

onMounted(() => { void loadProjects() })
</script>

<template>
  <div class="sems-root">
    <WorkbenchView />
  </div>
</template>

<style scoped>
.sems-root { min-height: 100vh; }
</style>
