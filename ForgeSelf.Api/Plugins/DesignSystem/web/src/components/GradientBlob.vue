<script setup lang="ts">
/**
 * 品牌渐变光晕（Brand Gradient Blob）· 紫→青。
 * 用于 Logo / Hero 背景光晕（设计系统「Brand Gradient」token）。
 */
withDefaults(defineProps<{ size?: number }>(), { size: 360 })
</script>

<template>
  <span class="ds-blob" :style="{ width: size + 'px', height: size + 'px' }" aria-hidden="true" />
</template>

<style scoped>
.ds-blob {
  display: block;
  border-radius: 50%;
  background: var(--ds-gradient-brand);
  filter: blur(64px);
  opacity: 0.45;
}
</style>
