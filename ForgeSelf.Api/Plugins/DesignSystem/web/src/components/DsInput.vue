<script setup lang="ts">
/**
 * 输入框（Inputs）· default + focus 状态。
 * v-model 双向绑定。
 */
const props = withDefaults(
  defineProps<{
    modelValue?: string
    placeholder?: string
    label?: string
    type?: string
    hint?: string
    invalid?: boolean
  }>(),
  { modelValue: '', placeholder: '', label: '', type: 'text', hint: '', invalid: false },
)

const emit = defineEmits<{
  (e: 'update:modelValue', value: string): void
}>()

function onInput(e: Event) {
  emit('update:modelValue', (e.target as HTMLInputElement).value)
}
</script>

<template>
  <label class="ds-field">
    <span v-if="label" class="ds-field__label ds-small">{{ label }}</span>
    <span class="ds-input" :class="{ 'ds-input--focus': false, 'ds-input--invalid': invalid }">
      <input
        class="ds-input__el"
        :type="type"
        :placeholder="placeholder"
        :value="modelValue"
        @input="onInput"
      />
    </span>
    <span v-if="hint" class="ds-field__hint ds-micro">{{ hint }}</span>
  </label>
</template>

<style scoped>
.ds-field {
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-2);
}
.ds-field__label {
  color: var(--ds-fg-2);
  font-weight: var(--ds-fw-medium);
}
.ds-input {
  display: flex;
  align-items: center;
  background: var(--ds-surface-1);
  border: 1px solid var(--ds-border-2);
  border-radius: var(--ds-radius-md);
  padding: 0 var(--ds-space-3);
  transition:
    border-color var(--ds-dur-fast) var(--ds-ease-out-expo),
    box-shadow var(--ds-dur-fast) var(--ds-ease-out-expo);
}
.ds-input:focus-within {
  border-color: var(--ds-color-primary);
  box-shadow: 0 0 0 3px var(--ds-brand-100);
}
.ds-input--invalid {
  border-color: var(--ds-danger);
}
.ds-input--invalid:focus-within {
  box-shadow: 0 0 0 3px var(--ds-danger-soft);
}
.ds-input__el {
  flex: 1;
  border: none;
  outline: none;
  background: transparent;
  font-family: var(--ds-font-sans);
  font-size: var(--ds-fs-body);
  color: var(--ds-fg-1);
  padding: 10px 0;
}
.ds-input__el::placeholder {
  color: var(--ds-fg-4);
}
.ds-field__hint {
  color: var(--ds-fg-4);
}
</style>
