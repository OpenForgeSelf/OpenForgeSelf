<template>
  <p class="hint">{{ text }}</p>
</template>

<script setup lang="ts">
/**
 * 空态/占位提示。
 * 用于后端暂无接口或数据为空时**如实说明**，而不是编造假数据填充界面。
 */
defineProps<{
  /** 提示文案。 */
  text: string
}>()
</script>

<style scoped>
.hint {
  margin: 2px 0;
  padding: 4px 8px;
  font-size: var(--el-font-size-extra-small, 12px);
  color: var(--el-text-color-secondary, #a3a6ad);
  opacity: 0.75;
}
</style>
