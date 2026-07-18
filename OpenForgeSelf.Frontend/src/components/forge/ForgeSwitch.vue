<script setup lang="ts">
const props = defineProps<{
  modelValue: boolean
  disabled?: boolean
}>()

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
}>()

function toggle(): void {
  emit('update:modelValue', !props.modelValue)
}
</script>

<template>
  <div
    class="forge-switch"
    :class="{ 'forge-switch--on': modelValue, 'forge-switch--disabled': disabled }"
    role="switch"
    :aria-checked="modelValue"
    :aria-disabled="disabled"
    :tabindex="disabled ? -1 : 0"
    @click="!disabled && toggle()"
    @keydown.enter.prevent="!disabled && toggle()"
    @keydown.space.prevent="!disabled && toggle()"
  >
    <span class="forge-switch__slider" />
  </div>
</template>

<style scoped>
.forge-switch {
  width: 44px;
  height: 24px;
  border-radius: var(--fs-radius-full);
  background: var(--fs-color-bg-elevated);
  cursor: pointer;
  position: relative;
  transition: background var(--fs-transition-fast);
  flex-shrink: 0;
}

.forge-switch--on {
  background: linear-gradient(135deg, var(--fs-color-primary) 0%, var(--fs-color-primary-hover) 100%);
  box-shadow: 0 0 12px rgba(245, 158, 11, 0.25);
}

.forge-switch:hover {
  opacity: 0.9;
}

.forge-switch--disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.forge-switch--disabled:hover {
  opacity: 0.5;
}

.forge-switch__slider {
  position: absolute;
  top: 2px;
  left: 2px;
  width: 20px;
  height: 20px;
  border-radius: var(--fs-radius-full);
  background: var(--fs-color-text-tertiary);
  transition: left var(--fs-transition-fast);
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.3);
}

.forge-switch--on .forge-switch__slider {
  left: 22px;
  background: var(--fs-color-bg-primary);
}
</style>
