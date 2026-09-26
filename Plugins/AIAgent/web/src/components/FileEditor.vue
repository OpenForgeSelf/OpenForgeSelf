<template>
  <section class="fedit">
    <!-- 头部：项目文件路径 -->
    <header class="fedit__head">
      <span class="fedit__label">
        <svg class="fedit__icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
          <polyline points="14 2 14 8 20 8" />
        </svg>
        编辑项目文件
      </span>
      <button type="button" class="fedit__close" @click="$emit('close')">关闭 ✕</button>
    </header>

    <!-- 文件路径 + 保存 -->
    <div class="fedit__bar">
      <span class="fedit__path" :title="file.name">{{ file.path }}</span>
      <button type="button" class="fedit__save" :disabled="saving" @click="$emit('save', text)">
        {{ saving ? '保存中…' : '保存' }}
      </button>
    </div>

    <!-- 编辑区 -->
    <textarea
      class="fedit__textarea"
      :value="text"
      spellcheck="false"
      placeholder="文件内容…"
      @input="text = ($event.target as HTMLTextAreaElement).value"
    />

    <!-- 状态提示 -->
    <p v-if="hint" class="fedit__hint" :class="{ 'fedit__hint--ok': !isError }">{{ hint }}</p>
  </section>
</template>

<script setup lang="ts">
/**
 * 项目文件编辑器：在中间栏（聊天区上方）提供文件的查看/编辑/保存。
 *
 * 设计约束：同 AiAgentView——不使用 Element Plus 组件，纯原生 HTML + CSS，
 * 颜色走 --el-* 变量并带兜底值。
 */
import { ref, watch } from 'vue'
import type { EditingFile } from '../types'

const props = defineProps<{
  /** 正在编辑的文件（内容由父层读取后端后传入）。 */
  file: EditingFile
  /** 是否正在保存。 */
  saving?: boolean
  /** 顶部状态提示文案（成功/失败由父层写入）。 */
  hint?: string
  /** hint 是否为错误提示（决定颜色）。 */
  isError?: boolean
}>()

const emit = defineEmits<{
  (e: 'save', content: string): void
  (e: 'close'): void
}>()

/** 本地编辑文本，随 file 变化重置。 */
const text = ref(props.file.content)

watch(
  () => props.file.content,
  (v) => {
    text.value = v
  }
)
</script>

<style scoped>
.fedit {
  display: flex;
  flex-direction: column;
  flex: 0 0 auto;
  max-height: 40%;
  border-bottom: 1px solid var(--el-border-color, #414243);
  background: var(--el-bg-color, #1d1e1f);
  padding-bottom: 8px;
}

.fedit__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 12px;
  border-bottom: 1px solid var(--el-border-color-dark, #2b2b2c);
}

.fedit__label {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: var(--el-font-size-small, 13px);
  font-weight: var(--el-weight-medium, 500);
  color: var(--el-text-color-primary, #e5eaf3);
}

.fedit__icon {
  width: 13px;
  height: 13px;
  color: var(--el-text-color-secondary, #a3a6ad);
}

.fedit__close {
  padding: 2px 6px;
  font-size: var(--el-font-size-extra-small, 12px);
  color: var(--el-text-color-secondary, #a3a6ad);
  background: transparent;
  border: none;
  cursor: pointer;
}

.fedit__close:hover {
  color: var(--el-color-primary, #ffb84d);
}

.fedit__bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  padding: 6px 12px;
}

.fedit__path {
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-family: var(--el-font-family-mono, monospace);
  font-size: var(--el-font-size-extra-small, 12px);
  color: var(--el-text-color-secondary, #a3a6ad);
}

.fedit__save {
  padding: 2px 14px;
  font-size: var(--el-font-size-extra-small, 12px);
  color: var(--el-color-primary, #ffb84d);
  background: var(--el-color-primary-light, rgba(255, 184, 77, 0.12));
  border: 1px solid var(--el-color-primary, #ffb84d);
  border-radius: 4px;
  cursor: pointer;
}

.fedit__save:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.fedit__textarea {
  flex: 1;
  min-height: 120px;
  margin: 0 12px;
  padding: 8px;
  resize: vertical;
  font-family: var(--el-font-family-mono, monospace);
  font-size: var(--el-font-size-extra-small, 12px);
  line-height: 1.6;
  color: var(--el-text-color-primary, #e5eaf3);
  background: var(--el-fill-color, #262727);
  border: 1px solid var(--el-border-color, #414243);
  border-radius: 4px;
  outline: none;
}

.fedit__textarea:focus {
  border-color: var(--el-color-primary, #ffb84d);
}

.fedit__hint {
  margin: 6px 12px 0;
  font-size: var(--el-font-size-extra-small, 12px);
  color: var(--el-color-danger, #f56c6c);
}

.fedit__hint--ok {
  color: var(--el-color-success, #67c23a);
}
</style>