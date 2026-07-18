<script setup lang="ts">
withDefaults(defineProps<{
  padding?: string
}>(), {
  padding: '16px',
})
</script>

<template>
  <div class="forge-card" :style="{ padding }">
    <slot />
  </div>
</template>

<style scoped>
.forge-card {
  border: 1px solid var(--fs-color-border-default);
  border-radius: var(--fs-radius-lg);
  background: var(--fs-color-bg-secondary);
  box-sizing: border-box;
}
</style>
