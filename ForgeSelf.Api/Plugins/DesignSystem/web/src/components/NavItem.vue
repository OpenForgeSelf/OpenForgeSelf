<script setup lang="ts">
/**
 * 导航项（Nav Item）· 侧栏导航项 active + rest 两种状态。
 */
import Icon from './Icon.vue'

withDefaults(
  defineProps<{
    label: string
    icon?: string
    active?: boolean
  }>(),
  { icon: 'box', active: false },
)
</script>

<template>
  <a class="ds-navitem" :class="{ 'ds-navitem--active': active }">
    <Icon :name="icon" :size="18" />
    <span class="ds-navitem__label">{{ label }}</span>
  </a>
</template>

<style scoped>
.ds-navitem {
  display: flex;
  align-items: center;
  gap: var(--ds-space-3);
  padding: var(--ds-space-3) var(--ds-space-4);
  border-radius: var(--ds-radius-md);
  color: var(--ds-fg-2);
  font-size: var(--ds-fs-body);
  font-weight: var(--ds-fw-medium);
  cursor: pointer;
  text-decoration: none;
  transition:
    background var(--ds-dur-fast) var(--ds-ease-out-expo),
    color var(--ds-dur-fast) var(--ds-ease-out-expo);
}
.ds-navitem:hover {
  background: var(--ds-surface-3);
  color: var(--ds-fg-1);
}
.ds-navitem--active {
  background: var(--ds-brand-50);
  color: var(--ds-color-primary);
  box-shadow: inset 3px 0 0 var(--ds-color-primary);
}
</style>
