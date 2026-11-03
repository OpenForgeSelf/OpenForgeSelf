<script setup lang="ts">
/**
 * CT 画廊宿主壳：把 SmartTerminal 插件真实视图挂进宿主页面。
 * 插件目录不引入 @playwright/test；fixture 由 story props 注入，跳过网络。
 */
import SmartTerminalView from '../../../Plugins/SmartTerminal/web/src/SmartTerminalView.vue'
import type { SmartTerminalFixture } from '../../../Plugins/SmartTerminal/web/src/types'

defineProps<{
  fixture: SmartTerminalFixture
}>()
</script>

<template>
  <div class="st-story-host">
    <SmartTerminalView :fixture="fixture" />
  </div>
</template>

<style scoped>
.st-story-host {
  width: 960px;
  height: 640px;
  background: var(--el-bg-color, #0f1419);
  overflow: hidden;
}
</style>
