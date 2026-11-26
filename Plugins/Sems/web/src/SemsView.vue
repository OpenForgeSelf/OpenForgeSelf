<script setup lang="ts">
/**
 * sems 插件界面入口。/sems 只有工作台这一套产品界面（需求 F26）。
 * 经典实现仍留在 LegacySemsView.vue 便于对照，产品界面不再提供入口。
 */
import { onMounted } from 'vue'
import { semsFocusFromSearch } from './deepLink'
import { go, loadProjects, openProject } from './workbench/workbenchStore'
import WorkbenchView from './workbench/WorkbenchView.vue'
import './workbench/workbench.css'

onMounted(() => {
  const focus = semsFocusFromSearch(window.location.search)
  void loadProjects().then(() => {
    if (focus.projectId > 0) openProject(focus.projectId)
    if (focus.pid > 0) go('/runs')
  })
})
</script>

<template>
  <div class="sems-root">
    <WorkbenchView />
  </div>
</template>

<style scoped>
.sems-root { min-height: 100vh; }
</style>
