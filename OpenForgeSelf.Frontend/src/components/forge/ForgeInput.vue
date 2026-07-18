<script setup lang="ts">
defineProps<{
  modelValue: string
  type?: string
  placeholder?: string
  disabled?: boolean
}>()

const emit = defineEmits<{
  'update:modelValue': [value: string]
}>()

function onInput(event: Event): void {
  const target = event.target as HTMLInputElement
  emit('update:modelValue', target.value)
}
</script>

<template>
  <input
    class="forge-input"
    :class="{ 'forge-input--disabled': disabled }"
    :type="type || 'text'"
    :value="modelValue"
    :placeholder="placeholder"
    :disabled="disabled"
    @input="onInput"
  />
</template>

<style scoped>
.forge-input {
  width: 100%;
  background: var(--fs-color-bg-tertiary);
  border: 1px solid var(--fs-color-border-default);
  border-radius: var(--fs-radius-md);
  color: var(--fs-color-text-primary);
  font-size: var(--fs-text-sm);
  font-family: var(--fs-font-body);
  padding: var(--fs-space-2) var(--fs-space-3);
  outline: none;
  transition: border-color var(--fs-transition-fast);
  box-sizing: border-box;
}

.forge-input::placeholder {
  color: var(--fs-color-text-tertiary);
}

.forge-input:focus {
  border-color: var(--fs-color-primary);
}

.forge-input--disabled {
  opacity: 0.5;
  cursor: not-allowed;
}
</style>
