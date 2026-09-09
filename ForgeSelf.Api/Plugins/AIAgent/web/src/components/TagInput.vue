<template>
  <div class="ti">
    <div v-if="modelValue.length > 0" class="ti__chips">
      <span
        v-for="(t, i) in modelValue"
        :key="`${t}-${i}`"
        class="ti__chip"
        :title="t"
      >
        <span class="ti__chip-text">{{ t }}</span>
        <button
          type="button"
          class="ti__chip-x"
          aria-label="移除"
          @click="removeAt(i)"
        >
          ×
        </button>
      </span>
    </div>
    <input
      v-model="draft"
      class="ti__input"
      type="text"
      :placeholder="modelValue.length === 0 ? placeholder : '回车或失焦添加…'"
      @keydown.enter.prevent="add"
      @blur="commit"
    />
  </div>
</template>

<script setup lang="ts">
/* 轻量多值输入（标签式）：chip + 输入框，Enter/失焦添加，点击删除。 */
import { ref } from 'vue'

const props = defineProps<{
  modelValue: string[]
  placeholder?: string
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', v: string[]): void
}>()

const draft = ref('')

function add() {
  const v = draft.value.trim()
  if (v && !props.modelValue.includes(v)) {
    emit('update:modelValue', [...props.modelValue, v])
  }
  draft.value = ''
}

function commit() {
  if (draft.value.trim()) add()
  else draft.value = ''
}

function removeAt(i: number) {
  const next = props.modelValue.slice()
  next.splice(i, 1)
  emit('update:modelValue', next)
}
</script>

<style scoped>
.ti {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.ti__chips {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.ti__chip {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  max-width: 100%;
  padding: 2px 8px;
  border-radius: var(--el-border-radius-round, 999px);
  background: var(--el-color-primary-light-9, rgba(255, 184, 77, 0.08));
  border: 1px solid var(--el-color-primary-light-8, rgba(255, 184, 77, 0.2));
  font-size: var(--el-font-size-extra-small, 12px);
  color: var(--el-color-primary, #ffb84d);
}

.ti__chip-text {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.ti__chip-x {
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 0;
  width: 14px;
  height: 14px;
  border: none;
  border-radius: 50%;
  background: transparent;
  color: var(--el-text-color-secondary, #a3a6ad);
  font-size: 12px;
  line-height: 1;
  cursor: pointer;
  flex-shrink: 0;
}

.ti__chip-x:hover {
  color: var(--el-color-danger, #f56c6c);
}

.ti__input {
  padding: 6px 10px;
  border: 1px solid var(--el-border-color, #414243);
  border-radius: var(--el-border-radius-small, 4px);
  background: var(--el-bg-color, #1d1e1f);
  color: var(--el-text-color-primary, #e5eaf3);
  font-size: var(--el-font-size-extra-small, 12px);
  outline: none;
  transition: border-color 0.2s;
}

.ti__input:focus {
  border-color: var(--el-color-primary, #ffb84d);
}
</style>