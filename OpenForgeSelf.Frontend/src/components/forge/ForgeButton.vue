<script setup lang="ts">
const props = defineProps<{
  variant?: 'primary' | 'ghost' | 'danger'
  size?: 'sm' | 'md'
  disabled?: boolean
}>()

const emit = defineEmits<{
  click: [event: MouseEvent]
}>()

function handleClick(event: MouseEvent): void {
  if (!props.disabled) {
    emit('click', event)
  }
}
</script>

<template>
  <button
    class="forge-btn"
    :class="[
      `forge-btn--${variant || 'primary'}`,
      `forge-btn--${size || 'md'}`,
      { 'forge-btn--disabled': disabled }
    ]"
    :disabled="disabled"
    @click="handleClick"
  >
    <slot />
  </button>
</template>

<style scoped>
.forge-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: var(--fs-space-2);
  border: 1px solid transparent;
  border-radius: var(--fs-radius-md);
  font-family: var(--fs-font-body);
  font-weight: var(--fs-weight-medium);
  cursor: pointer;
  white-space: nowrap;
  transition:
    background-color var(--fs-transition-fast),
    border-color var(--fs-transition-fast),
    color var(--fs-transition-fast),
    box-shadow var(--fs-transition-fast);
}

.forge-btn:focus-visible {
  outline: 2px solid var(--fs-color-primary);
  outline-offset: 2px;
}

/* Sizes */
.forge-btn--sm {
  padding: var(--fs-space-1) var(--fs-space-3);
  font-size: var(--fs-text-xs);
}

.forge-btn--md {
  padding: var(--fs-space-2) var(--fs-space-4);
  font-size: var(--fs-text-sm);
}

/* Variant: primary */
.forge-btn--primary {
  background: var(--fs-color-primary);
  border-color: var(--fs-color-primary);
  color: var(--fs-color-text-inverse);
}

.forge-btn--primary:hover:not(:disabled) {
  background: var(--fs-color-primary-hover);
  border-color: var(--fs-color-primary-hover);
}

.forge-btn--primary:active:not(:disabled) {
  background: var(--fs-color-primary-active);
  border-color: var(--fs-color-primary-active);
}

/* Variant: ghost */
.forge-btn--ghost {
  background: transparent;
  border-color: var(--fs-color-border-default);
  color: var(--fs-color-text-secondary);
}

.forge-btn--ghost:hover:not(:disabled) {
  background: var(--fs-color-bg-tertiary);
  border-color: var(--fs-color-border-default);
  color: var(--fs-color-text-primary);
}

.forge-btn--ghost:active:not(:disabled) {
  background: var(--fs-color-bg-elevated);
}

/* Variant: danger */
.forge-btn--danger {
  background: transparent;
  border-color: var(--state-error);
  color: var(--state-error);
}

.forge-btn--danger:hover:not(:disabled) {
  background: var(--state-error);
  border-color: var(--state-error);
  color: var(--fs-color-text-inverse);
}

.forge-btn--danger:active:not(:disabled) {
  opacity: 0.85;
}

/* Disabled */
.forge-btn--disabled,
.forge-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
  pointer-events: none;
}
</style>
