<script setup lang="ts">
/**
 * 标签列表输入（PILOT-079 FR-7 任务网关小节：命令前缀 / 同意关键字 / 拒绝关键字共用）。
 * 交互（02-spec「FR-7 视觉与交互规格（design · 2026-11-03）」§2）：32px 文本框回车/失焦成 chip；
 * chip=既有 .ig-badge 形态（mono 11px + --radius-sm + --paper-tint 底 + fg-secondary 字 + 右侧 ✕），
 * hover 才金边金字（禁主色实心填充=ui-ux-design 技能 J 条）。
 * 纯受控组件：v-model:string[]；提交时 trim、空串不入列、去重（重复输入静默忽略）。
 * 样式铁律：插件是独立预编译产物，禁 <ElXxx>，一律原生 HTML + --el-* / design token（零裸色值）。
 */
import { ref } from 'vue'

const props = defineProps<{
  modelValue: string[]
  /** 输入框占位文案（如「如 /task 或 任务：」）。 */
  placeholder?: string
  /** 禁用（总开关 off / 审批子开关 off 时由父组件传入），禁用≠隐藏、数值保留。 */
  disabled?: boolean
  /** 输入框无障碍名。 */
  inputLabel: string
}>()

const emit = defineEmits<{ (e: 'update:modelValue', value: string[]): void }>()

/** 当前草稿（回车/失焦时提交成 chip）。 */
const draft = ref('')

/** 提交草稿：trim、空串不入列、去重（保留既有顺序）。 */
function commitDraft(): void {
  const v = draft.value.trim()
  draft.value = ''
  if (props.disabled || !v || props.modelValue.includes(v)) return
  emit('update:modelValue', [...props.modelValue, v])
}

/** 删除第 index 个 chip（禁用时不响应）。 */
function removeAt(index: number): void {
  if (props.disabled) return
  emit('update:modelValue', props.modelValue.filter((_, i) => i !== index))
}
</script>

<template>
  <div class="ig-tags" :class="{ 'ig-tags--disabled': disabled }">
    <span v-for="(tag, i) in modelValue" :key="`${tag}-${i}`" class="ig-chip">
      <span class="ig-chip__text">{{ tag }}</span>
      <button
        type="button"
        class="ig-chip__del"
        :disabled="disabled"
        :aria-label="`删除 ${inputLabel} ${tag}`"
        @click="removeAt(i)"
      >
        ✕
      </button>
    </span>
    <input
      v-model="draft"
      type="text"
      class="ig-tags__input"
      :placeholder="placeholder"
      :disabled="disabled"
      :aria-label="inputLabel"
      @keydown.enter.prevent="commitDraft"
      @blur="commitDraft"
    />
  </div>
</template>

<style>
/* 全 token：零裸色值（--el-* 宿主级联 + design token），形态对齐面板既有 .ig-badge/.ig-field input */
.ig-tags {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 6px;
  min-width: 0;
}
.ig-tags--disabled {
  opacity: 0.6;
}
.ig-chip {
  display: inline-flex;
  align-items: center;
  gap: 2px;
  flex: none;
  max-width: 100%;
  padding: 1px 2px 1px 8px;
  border: 1px solid transparent;
  border-radius: var(--radius-sm);
  background: var(--paper-tint);
  color: var(--fg-secondary);
  font-family: var(--font-mono, monospace);
  font-size: 11px;
  line-height: 1.4;
  transition: color var(--dur-fast) var(--ease-out), border-color var(--dur-fast) var(--ease-out);
}
/* hover 才金边金字（禁主色实心填充） */
.ig-chip:hover {
  border-color: var(--gold-400);
  color: var(--gold-400);
}
.ig-chip__text {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.ig-chip__del {
  border: none;
  background: transparent;
  color: inherit;
  font-size: 11px;
  line-height: 1;
  padding: 2px 4px;
  cursor: pointer;
}
.ig-chip__del:disabled {
  cursor: not-allowed;
}
.ig-tags__input {
  flex: 1;
  min-width: 120px;
  height: 32px;
  padding: 0 10px;
  border: 1px solid var(--el-border-color);
  border-radius: var(--radius-md);
  background: var(--el-bg-color);
  color: var(--fg-primary);
  font-size: 13px;
  box-sizing: border-box;
  outline: none;
}
.ig-tags__input:focus {
  border-color: var(--gold-400);
  box-shadow: 0 0 0 3px var(--gold-tint);
}
.ig-tags__input:disabled {
  cursor: not-allowed;
  opacity: 0.6;
}
.ig-tags__input::placeholder {
  color: var(--fg-muted);
}
</style>