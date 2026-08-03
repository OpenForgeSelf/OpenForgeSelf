<template>
  <div class="code-snippet-editor">
    <div class="editor-header">
      <h3>{{ isEditing ? '编辑代码片段' : '新建代码片段' }}</h3>
      <div class="editor-actions">
        <button class="btn btn-secondary" @click="$emit('cancel')">
          取消
        </button>
        <button class="btn btn-primary" :disabled="isSaving || !canSave" @click="onSave">
          <i v-if="isSaving" class="fa-solid fa-spinner fa-spin" />
          {{ isSaving ? '保存中...' : '保存' }}
        </button>
      </div>
    </div>

    <div class="editor-body">
      <div class="form-group">
        <label>标题 <span class="required">*</span></label>
        <input
          v-model="form.title"
          type="text"
          placeholder="输入代码片段标题"
        />
      </div>

      <div class="form-row">
        <div class="form-group">
          <label>语言 <span class="required">*</span></label>
          <input
            v-model="form.language"
            type="text"
            placeholder="如: csharp, python, javascript"
          />
        </div>
        <div class="form-group">
          <label>分类</label>
          <input
            v-model="form.category"
            type="text"
            placeholder="输入分类名称"
            list="category-list"
          />
          <datalist id="category-list">
            <option v-for="cat in categories" :key="cat" :value="cat" />
          </datalist>
        </div>
      </div>

      <div class="form-group">
        <label>描述</label>
        <textarea
          v-model="form.description"
          placeholder="描述这个代码片段的用途..."
          rows="2"
        />
      </div>

      <div class="form-group">
        <label>标签</label>
        <div class="tags-input">
          <span v-for="(tag, index) in form.tags" :key="index" class="tag">
            #{{ tag }}
            <button type="button" class="remove-tag" @click="removeTag(index)">×</button>
          </span>
          <input
            v-model="tagInput"
            type="text"
            placeholder="输入标签，按回车添加"
            @keydown.enter.prevent="addTag"
          />
        </div>
      </div>

      <div class="form-group code-editor-group">
        <label>代码 <span class="required">*</span></label>
        <div class="code-textarea-wrapper">
          <textarea
            v-model="form.code"
            placeholder="在此输入代码..."
            spellcheck="false"
            @keydown.tab.prevent="handleTab"
          />
        </div>
        <div class="code-info">
          <span>{{ form.code.length }} 字符</span>
          <span>{{ form.code.split('\n').length }} 行</span>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import type { CodeSnippet, CreateCodeSnippetRequest, UpdateCodeSnippetRequest } from '@/types/codeSnippet'

const props = defineProps<{
  snippet?: CodeSnippet | null
  categories: string[]
  isSaving?: boolean
}>()

const emit = defineEmits<{
  save: [data: CreateCodeSnippetRequest | UpdateCodeSnippetRequest]
  cancel: []
}>()

const isEditing = computed(() => !!props.snippet?.id)
const tagInput = ref('')

const form = ref<{
  title: string
  description: string
  code: string
  language: string
  category: string
  tags: string[]
}>({
  title: '',
  description: '',
  code: '',
  language: '',
  category: '',
  tags: []
})

const canSave = computed(() => {
  return form.value.title.trim() && form.value.code.trim() && form.value.language.trim()
})

watch(() => props.snippet, (snippet) => {
  if (snippet) {
    form.value = {
      title: snippet.title,
      description: snippet.description,
      code: snippet.code,
      language: snippet.language,
      category: snippet.category,
      tags: [...snippet.tags]
    }
  } else {
    form.value = {
      title: '',
      description: '',
      code: '',
      language: '',
      category: '',
      tags: []
    }
  }
}, { immediate: true })

function addTag() {
  const tag = tagInput.value.trim()
  if (tag && !form.value.tags.includes(tag)) {
    form.value.tags.push(tag)
  }
  tagInput.value = ''
}

function removeTag(index: number) {
  form.value.tags.splice(index, 1)
}

function handleTab(e: KeyboardEvent) {
  const textarea = e.target as HTMLTextAreaElement
  const start = textarea.selectionStart
  const end = textarea.selectionEnd
  const value = form.value.code

  form.value.code = value.substring(0, start) + '  ' + value.substring(end)
  
  setTimeout(() => {
    textarea.selectionStart = textarea.selectionEnd = start + 2
  }, 0)
}

function onSave() {
  if (!canSave.value) return

  const data = {
    title: form.value.title.trim(),
    description: form.value.description,
    code: form.value.code,
    language: form.value.language.trim(),
    category: form.value.category.trim(),
    tags: form.value.tags
  }

  emit('save', data)
}
</script>

<style scoped>
.code-snippet-editor {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: var(--el-bg-color, #fff);
  overflow: hidden;
}

.editor-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px;
  border-bottom: 1px solid var(--el-border-color, #e5e7eb);
}

.editor-header h3 {
  margin: 0;
  font-size: 18px;
  font-weight: 600;
  color: var(--el-text-color-primary, #1f2937);
}

.editor-actions {
  display: flex;
  gap: 8px;
}

.btn {
  padding: 8px 16px;
  border: none;
  border-radius: 6px;
  font-size: 13px;
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 6px;
}

.btn-primary {
  background: var(--el-color-primary, #3b82f6);
  color: white;
}

.btn-primary:hover:not(:disabled) {
  background: var(--el-color-primary-light-3);
}

.btn-primary:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.btn-secondary {
  background: var(--el-bg-color-page, #f3f4f6);
  color: var(--el-text-color-regular, #6b7280);
}

.btn-secondary:hover {
  background: var(--el-fill-color-light, #e5e7eb);
}

.editor-body {
  flex: 1;
  overflow-y: auto;
  padding: 16px;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.form-group label {
  font-size: 13px;
  font-weight: 500;
  color: var(--el-text-color-regular, #374151);
}

.required {
  color: #ef4444;
}

.form-group input,
.form-group textarea {
  padding: 8px 12px;
  border: 1px solid var(--el-border-color, #d1d5db);
  border-radius: 6px;
  font-size: 13px;
  background: var(--el-bg-color, #fff);
  color: var(--el-text-color-primary, #1f2937);
  font-family: inherit;
}

.form-group input:focus,
.form-group textarea:focus {
  outline: none;
  border-color: var(--el-color-primary, #3b82f6);
  box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.1);
}

.form-group textarea {
  resize: vertical;
  min-height: 60px;
}

.form-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
}

.tags-input {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  padding: 6px 8px;
  border: 1px solid var(--el-border-color, #d1d5db);
  border-radius: 6px;
  min-height: 40px;
  align-items: center;
}

.tags-input:focus-within {
  border-color: var(--el-color-primary, #3b82f6);
  box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.1);
}

.tag {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 2px 8px;
  background: var(--el-bg-color-page, #f3f4f6);
  border-radius: 4px;
  font-size: 12px;
  color: var(--el-text-color-regular, #6b7280);
}

.remove-tag {
  background: none;
  border: none;
  cursor: pointer;
  color: var(--el-text-color-secondary, #9ca3af);
  font-size: 14px;
  padding: 0;
  line-height: 1;
}

.remove-tag:hover {
  color: var(--el-text-color-regular, #6b7280);
}

.tags-input input {
  flex: 1;
  min-width: 100px;
  border: none;
  outline: none;
  padding: 4px;
  font-size: 13px;
  background: transparent;
}

.code-editor-group {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-height: 0;
}

.code-textarea-wrapper {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-height: 200px;
}

.code-textarea-wrapper textarea {
  flex: 1;
  font-family: 'Consolas', 'Monaco', 'Courier New', monospace;
  font-size: 13px;
  line-height: 1.6;
  resize: none;
  tab-size: 2;
  background: var(--el-bg-color, #1e293b);
  color: #e2e8f0;
  border-color: var(--el-border-color, #334155);
}

.code-info {
  display: flex;
  justify-content: flex-end;
  gap: 16px;
  padding: 6px 8px;
  font-size: 11px;
  color: var(--el-text-color-secondary, #9ca3af);
  background: var(--el-bg-color-page, #f9fafb);
  border: 1px solid var(--el-border-color, #e5e7eb);
  border-top: none;
  border-radius: 0 0 6px 6px;
}
</style>
