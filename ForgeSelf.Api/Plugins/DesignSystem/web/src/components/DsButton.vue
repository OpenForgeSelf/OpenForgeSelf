<script setup lang="ts">
/**
 * 按钮（Buttons）· Primary / Secondary / Ghost / Danger。
 * 状态：default → hover → active → focus → disabled（由 CSS 伪类表达）。
 */
import Icon from './Icon.vue'

withDefaults(
  defineProps<{
    variant?: 'primary' | 'secondary' | 'ghost' | 'danger'
    size?: 'sm' | 'md' | 'lg'
    disabled?: boolean
    icon?: string
    block?: boolean
  }>(),
  { variant: 'primary', size: 'md', disabled: false, block: false },
)
</script>

<template>
  <button
    class="ds-btn"
    :class="[`ds-btn--${variant}`, `ds-btn--${size}`, { 'ds-btn--block': block }]"
    :disabled="disabled"
    type="button"
  >
    <Icon v-if="icon" :name="icon" :size="size === 'sm' ? 14 : 16" />
    <slot />
  </button>
</template>

<style scoped>
.ds-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: var(--ds-space-2);
  border: 1px solid transparent;
  border-radius: var(--ds-radius-md);
  font-family: var(--ds-font-sans);
  font-weight: var(--ds-fw-semibold);
  cursor: pointer;
  transition:
    background var(--ds-dur-fast) var(--ds-ease-out-expo),
    border-color var(--ds-dur-fast) var(--ds-ease-out-expo),
    box-shadow var(--ds-dur-fast) var(--ds-ease-out-expo),
    transform var(--ds-dur-fast) var(--ds-ease-out-expo);
  white-space: nowrap;
  user-select: none;
}
.ds-btn--sm {
  font-size: var(--ds-fs-small);
  padding: 6px var(--ds-space-3);
}
.ds-btn--md {
  font-size: var(--ds-fs-body);
  padding: 9px var(--ds-space-4);
}
.ds-btn--lg {
  font-size: var(--ds-fs-body-lg);
  padding: 12px var(--ds-space-5);
}
.ds-btn--block {
  width: 100%;
}

/* Primary */
.ds-btn--primary {
  background: var(--ds-color-primary);
  color: #fff;
}
.ds-btn--primary:hover {
  background: var(--ds-color-primary-hover);
  box-shadow: 0 6px 16px rgba(124, 58, 237, 0.28);
}
.ds-btn--primary:active {
  background: var(--ds-color-primary-active);
  transform: translateY(1px);
}

/* Secondary */
.ds-btn--secondary {
  background: var(--ds-surface-1);
  color: var(--ds-fg-1);
  border-color: var(--ds-border-2);
}
.ds-btn--secondary:hover {
  background: var(--ds-surface-3);
  border-color: var(--ds-border-3);
}
.ds-btn--secondary:active {
  background: var(--ds-surface-2);
}

/* Ghost */
.ds-btn--ghost {
  background: transparent;
  color: var(--ds-color-primary);
}
.ds-btn--ghost:hover {
  background: var(--ds-brand-50);
}
.ds-btn--ghost:active {
  background: var(--ds-brand-100);
}

/* Danger */
.ds-btn--danger {
  background: var(--ds-danger);
  color: #fff;
}
.ds-btn--danger:hover {
  background: #dc2626;
  box-shadow: 0 6px 16px rgba(239, 68, 68, 0.28);
}
.ds-btn--danger:active {
  background: #b91c1c;
  transform: translateY(1px);
}

/* Focus（可达性） */
.ds-btn:focus-visible {
  outline: 2px solid var(--ds-color-accent);
  outline-offset: 2px;
}

/* Disabled */
.ds-btn:disabled {
  opacity: 0.45;
  cursor: not-allowed;
  box-shadow: none;
  transform: none;
}
</style>
