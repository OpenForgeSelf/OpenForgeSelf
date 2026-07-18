<script setup lang="ts">
interface SelectOption {
  value: string
  label: string
}

defineProps<{
  modelValue: string
  options: SelectOption[]
  disabled?: boolean
}>()

const emit = defineEmits<{
  'update:modelValue': [value: string]
}>()

function onChange(event: Event): void {
  const target = event.target as HTMLSelectElement
  emit('update:modelValue', target.value)
}
</script>

<template>
  <div class="forge-select" :class="{ 'forge-select--disabled': disabled }">
    <select
      class="forge-select__native"
      :value="modelValue"
      :disabled="disabled"
      @change="onChange"
    >
      <option
        v-for="opt in options"
        :key="opt.value"
        :value="opt.value"
        :selected="opt.value === modelValue"
      >
        {{ opt.label }}
      </option>
    </select>
    <svg
      class="forge-select__arrow"
      width="14"
      height="14"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      stroke-width="2"
      stroke-linecap="round"
      stroke-linejoin="round"
      aria-hidden="true"
    >
      <polyline points="6 9 12 15 18 9" />
    </svg>
  </div>
</template>

<style scoped>
.forge-select {
  position: relative;
  display: inline-flex;
  align-items: center;
}

.forge-select__native {
  appearance: none;
  background: var(--fs-color-bg-tertiary);
  border: 1px solid var(--fs-color-border-default);
  border-radius: var(--fs-radius-md);
  color: var(--fs-color-text-primary);
  font-size: var(--fs-text-sm);
  font-family: var(--fs-font-body);
  padding: var(--fs-space-2) var(--fs-space-8) var(--fs-space-2) var(--fs-space-3);
  cursor: pointer;
  min-width: 160px;
  transition: border-color var(--fs-transition-fast);
  outline: none;
}

.forge-select__native:focus {
  border-color: var(--fs-color-primary);
}

.forge-select__native:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.forge-select__arrow {
  position: absolute;
  right: 10px;
  top: 50%;
  transform: translateY(-50%);
  color: var(--fs-color-text-tertiary);
  pointer-events: none;
}

.forge-select--disabled {
  opacity: 0.6;
}
</style>
